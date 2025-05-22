using System.Linq.Expressions;
using System.Text;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Dialect;

internal abstract class SqlDialectBuilder
{
    protected abstract string OpenDelimiter { get; }
    protected abstract string CloseDelimiter { get; }

    protected virtual string ConcatOperator => "||";
    protected virtual bool SupportsMoveRows => true;

    public abstract string CreateTableCopySql(string tempNameName, TableMetadata tableInfo, IReadOnlyList<PropertyMetadata> columns);

    /// <summary>
    /// Builds the SQL for moving data from one table to another.
    /// </summary>
    /// <param name="source">Source table</param>
    /// <param name="target">Target table name</param>
    /// <param name="insertedProperties">Properties to be inserted</param>
    /// <param name="returnedProperties">Properties to be returned</param>
    /// <param name="options">Bulk insert options</param>
    /// <param name="onConflict">On conflict options</param>
    /// <typeparam name="T">Entity type</typeparam>
    /// <returns>The SQL query</returns>
    public virtual string BuildMoveDataSql<T>(
        TableMetadata target,
        string source,
        IReadOnlyList<PropertyMetadata> insertedProperties,
        IReadOnlyList<PropertyMetadata> returnedProperties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var q = new StringBuilder();

        if (SupportsMoveRows && options.MoveRows)
        {
            // WITH moved_rows AS (DELETE FROM {source) RETURNING {insertedProperties})
            q.Append($"WITH moved_rows AS (DELETE FROM {source} RETURNING ");
            q.AppendColumns(insertedProperties);
            q.AppendLine(")");

            source = "moved_rows";
        }

        // INSERT INTO {target} ({columns}) SELECT {columns} FROM {source} WHERE TRUE
        q.Append($"INSERT INTO {target.QuotedTableName} (");
        q.AppendColumns(insertedProperties);
        q.AppendLine(")");
        q.Append("SELECT ");
        q.AppendColumns(insertedProperties);
        q.AppendLine();
        q.AppendLine($"FROM {source} WHERE TRUE");

        if (onConflict is OnConflictOptions<T> onConflictTyped)
        {
            AppendOnConflictStatement(q);

            if (onConflictTyped.Update != null)
            {
                AppendConflictMatch(q, target, onConflictTyped);

                if (onConflictTyped.Update != null)
                {
                    q.Append(' ');
                    AppendOnConflictUpdate(q, GetUpdates(target, insertedProperties, onConflictTyped.Update));
                }

                if (onConflictTyped.Condition != null)
                {
                    q.Append(' ');
                    AppendConflictCondition(q, onConflictTyped);
                }
            }
            else
            {
                q.Append(' ');
                AppendDoNothing(q, insertedProperties);
            }
        }

        if (returnedProperties.Count != 0)
        {
            q.Append("RETURNING ");
            q.AppendJoin(", ", returnedProperties.Select(p => p.QuotedColumName));
            q.AppendLine();
        }

        q.AppendLine(";");

        var result = q.ToString();
        return result;
    }

    protected virtual void AppendDoNothing(StringBuilder sql, IEnumerable<PropertyMetadata> insertedProperties)
    {
        sql.AppendLine("DO NOTHING");
    }

    protected virtual void AppendOnConflictUpdate(StringBuilder sql, IEnumerable<string> updates)
    {
        sql.AppendLine("DO UPDATE SET");
        sql.AppendJoin(", ", updates);
    }

    protected virtual void AppendConflictMatch<T>(StringBuilder sql, TableMetadata target, OnConflictOptions<T> conflict)
    {
        if (conflict.Match != null)
        {
            sql.Append(' ');
            sql.AppendLine("(");
            sql.AppendJoin(", ", GetColumns(target, conflict.Match));
            sql.AppendLine(")");
        }
    }

    protected virtual void AppendOnConflictStatement(StringBuilder sql)
    {
        sql.AppendLine("ON CONFLICT");
    }

    protected virtual void AppendConflictCondition<T>(StringBuilder sql, OnConflictOptions<T> onConflictTyped)
    {
        sql.AppendLine($"WHERE {onConflictTyped.Condition}");
    }

    /// <summary>
    /// Get the name of the excluded column for the ON CONFLICT clause.
    /// </summary>
    protected virtual string GetExcludedColumnName(string columnName)
    {
        return $"EXCLUDED.{Quote(columnName)}";
    }

    /// <summary>
    /// Quotes a column name using database-specific delimiters.
    /// </summary>
    public string Quote(string entity)
    {
        return $"{OpenDelimiter}{entity}{CloseDelimiter}";
    }

    /// <summary>
    /// Quotes a schema and table name using database-specific delimiters.
    /// </summary>
    public string QuoteTableName(string? schema, string tableName)
    {
        return schema != null ? $"{Quote(schema)}.{Quote(tableName)}" : Quote(tableName);
    }

    /// <summary>
    /// Gets column names for the insert statement, from an object initializer.
    /// </summary>
    protected string[] GetColumns<T>(TableMetadata table, Expression<Func<T, object>> columns)
    {
        return columns.Body switch
        {
            NewExpression newExpression =>
                newExpression.Arguments.OfType<MemberExpression>()
                    .Select(m => table.GetQuotedColumnName(m.Member.Name)).ToArray(),
            MemberExpression memberExpression =>
                [table.GetQuotedColumnName(memberExpression.Member.Name)],
            _ => throw new NotSupportedException("Unsupported expression type")
        };
    }

    /// <summary>
    /// Gets column names for the update statement, from an object initializer or a member initializer.
    /// </summary>
    /// <example>
    /// <code>
    /// var updates = GetUpdates(context, e => new Entity { Prop1 = value1, Prop2 = value2 });
    /// </code>
    /// <code>
    /// var updates = GetUpdates(context, e => e.Prop1);
    /// </code>
    /// </example>
    protected IEnumerable<string> GetUpdates<T>(TableMetadata table, IEnumerable<PropertyMetadata> properties, Expression<Func<T, object>> update)
    {
        switch (update.Body)
        {
            case NewExpression { Members: not null } newExpr:
            {
                foreach (var arg in newExpr.Arguments.Zip(newExpr.Members, (expr, member) => (expr, member)))
                {
                    yield return $"{table.GetColumnName(arg.member.Name)} = {ToSqlExpression<T>(table, arg.expr)}";
                }

                break;
            }
            case MemberInitExpression memberInit:
            {
                foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                {
                    yield return $"{table.GetColumnName(binding.Member.Name)} = {ToSqlExpression<T>(table, binding.Expression)}";
                }

                break;
            }
            case MemberExpression memberExpr:
                yield return $"{table.GetColumnName(memberExpr.Member.Name)} = {ToSqlExpression<T>(table, memberExpr)}";
                break;
            case ParameterExpression parameterExpr when (parameterExpr.Type == typeof(T)):
                foreach (var property in properties)
                {
                    yield return $"{property.QuotedColumName} = {GetExcludedColumnName(property.ColumnName)}";
                }

                break;

            default:
                throw new NotSupportedException($"Unsupported expression type {update.Body.GetType()} for update");
        }
    }

    /// <summary>
    /// Converts an expression to an SQL string.
    /// </summary>
    /// <param name="table">The DbContext</param>
    /// <param name="expr">The expression, with simple operations</param>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <returns>An SQL statement</returns>
    /// <exception cref="NotSupportedException">Thrown when an expression could not be translated.</exception>
    private string ToSqlExpression<TEntity>(TableMetadata table, Expression expr)
    {
        switch (expr)
        {
            case MemberExpression memberExpr:
                return GetExcludedColumnName(table.GetColumnName(memberExpr.Member.Name));

            case BinaryExpression binaryExpr:
                {
                    var op = binaryExpr.NodeType switch
                    {
                        ExpressionType.Add => binaryExpr.Type == typeof(string) ? ConcatOperator : "+",
                        ExpressionType.Subtract => "-",
                        ExpressionType.Multiply => "*",
                        ExpressionType.Divide => "/",
                        ExpressionType.Modulo => "%",
                        ExpressionType.AndAlso => "AND",
                        ExpressionType.OrElse => "OR",
                        ExpressionType.Equal => "=",
                        ExpressionType.NotEqual => "<>",
                        ExpressionType.LessThan => "<",
                        ExpressionType.LessThanOrEqual => "<=",
                        ExpressionType.GreaterThan => ">",
                        ExpressionType.GreaterThanOrEqual => ">=",
                        _ => throw new NotSupportedException($"Unsupported operator: {binaryExpr.NodeType}")
                    };

                    var lhs = ToSqlExpression<TEntity>(table, binaryExpr.Left);
                    var rhs = ToSqlExpression<TEntity>(table, binaryExpr.Right);

                    return $"({lhs} {op} {rhs})";
                }

            case ConstantExpression contantExpr:
                if (contantExpr.Type == typeof(RawSqlValue) && contantExpr.Value != null)
                {
                    return ((RawSqlValue)contantExpr.Value!).Sql;
                }

                if (contantExpr.Type == typeof(string) ||
                    contantExpr.Type == typeof(Guid))
                {
                    return $"'{contantExpr.Value}'";
                }

                if (contantExpr.Type == typeof(bool))
                {
                    return (bool)contantExpr.Value! ? "TRUE" : "FALSE";
                }

                return contantExpr.Value?.ToString() ?? "NULL";

            case UnaryExpression unaryExpr:
                if (unaryExpr.NodeType == ExpressionType.Convert)
                {
                    return ToSqlExpression<TEntity>(table, unaryExpr.Operand);
                }
                if (unaryExpr.NodeType == ExpressionType.Not)
                {
                    return $"NOT ({ToSqlExpression<TEntity>(table, unaryExpr.Operand)})";
                }
                throw new NotSupportedException($"Unary operator not supported: {unaryExpr.NodeType}");

            case MethodCallExpression methodExpr:
                {
                    var lhs = methodExpr.Object != null ? ToSqlExpression<TEntity>(table, methodExpr.Object) : null;

                    switch (methodExpr.Method.Name)
                    {
                        case "ToLower":
                            return $"LOWER({lhs})";
                        case "ToUpper":
                            return $"UPPER({lhs})";
                        case "Trim":
                            return $"BTRIM({lhs})";
                        case "Contains" when methodExpr is { Object: not null, Arguments.Count: 1 }:
                            return $"{lhs} LIKE '%' || {ToSqlExpression<TEntity>(table, methodExpr.Arguments[0])} || '%'";
                        case "EndsWith" when methodExpr is { Object: not null, Arguments.Count: 1 }:
                            return $"{lhs} LIKE '%' || {ToSqlExpression<TEntity>(table, methodExpr.Arguments[0])}";
                        case "StartsWith" when methodExpr is { Object: not null, Arguments.Count: 1 }:
                            return $"{lhs} LIKE {ToSqlExpression<TEntity>(table, methodExpr.Arguments[0])} || '%'";
                        default:
                            throw new NotSupportedException($"Method not supported: {methodExpr.Method.Name}");
                    }
                }

            case ParameterExpression parameterExpr:
                return Quote(parameterExpr.Name ?? "param");

            default:
                throw new NotSupportedException($"Expression not supported: {expr.NodeType}");
        }
    }
}
