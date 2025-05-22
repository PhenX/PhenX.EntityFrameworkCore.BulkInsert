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

    /// <summary>
    /// Builds the SQL for moving data from one table to another.
    /// </summary>
    /// <param name="source">Source table</param>
    /// <param name="target">Target table name</param>
    /// <param name="insertedProperties">Properties to be copied</param>
    /// <param name="properties">Properties to be returned</param>
    /// <param name="options">Bulk insert options</param>
    /// <param name="onConflict">On conflict options</param>
    /// <typeparam name="T">Entity type</typeparam>
    /// <returns>The SQL query</returns>
    public virtual string BuildMoveDataSql<T>(
        TableMetadata target,
        string source,
        IReadOnlyList<PropertyMetadata> insertedProperties,
        IReadOnlyList<PropertyMetadata> properties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(p => p.QuotedColumName);
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => p.QuotedColumName);
        var columnList = string.Join(", ", returnedColumns);

        var q = new StringBuilder();

        if (SupportsMoveRows && options.MoveRows)
        {
            q.AppendLine($"""
                    WITH moved_rows AS (
                       DELETE FROM {source}
                           RETURNING {insertedColumnList}
                    )
                    """);
            source = "moved_rows";
        }

        q.AppendLine($"""
                      INSERT INTO {target.QuotedTableName} ({insertedColumnList})
                      SELECT {insertedColumnList}
                      FROM {source}
                      WHERE TRUE
                      """);

        if (onConflict is OnConflictOptions<T> onConflictTyped)
        {
            AppendOnConflictStatement(q);

            if (onConflictTyped.Update != null)
            {
                if (onConflictTyped.Match != null)
                {
                    q.Append(' ');
                    AppendConflictMatch(q, GetColumns(target, onConflictTyped.Match));
                }

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

        if (columnList.Length != 0)
        {
            q.AppendLine($"RETURNING {columnList}");
        }

        q.AppendLine(";");

        return q.ToString();
    }

    protected virtual void AppendDoNothing(StringBuilder sql, IEnumerable<PropertyMetadata> insertedProperties)
    {
        sql.AppendLine("DO NOTHING");
    }

    protected virtual void AppendOnConflictUpdate(StringBuilder sql, IEnumerable<string> updates)
    {
        sql.AppendLine("DO UPDATE SET");

        var i = 0;
        foreach (var update in updates)
        {
            if (i > 0)
            {
                sql.Append(", ");
            }

            sql.Append(update);
            i++;
        };
    }

    protected virtual void AppendConflictMatch(StringBuilder sql, IEnumerable<string> columns)
    {
        sql.AppendLine("(");

        var i = 0;
        foreach (var column in columns)
        {
            if (i > 0)
            {
                sql.Append(", ");
            }

            sql.Append(column);
            i++;
        }

        sql.AppendLine(")");
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
    public string Quote(string entity) => $"{OpenDelimiter}{entity}{CloseDelimiter}";

    /// <summary>
    /// Quotes a schema and table name using database-specific delimiters.
    /// </summary>
    public string QuoteTableName(string? schema, string tableName)
    {
        return schema != null
            ? $"{Quote(schema)}.{Quote(tableName)}"
            : Quote(tableName);
    }

    /// <summary>
    /// Gets column names for the insert statement, from an object initializer.
    /// </summary>
    protected string[] GetColumns<T>(TableMetadata table, Expression<Func<T, object>> columns)
    {
        return columns.Body switch
        {
            NewExpression newExpression => newExpression.Arguments.OfType<MemberExpression>()
                .Select(m => table.GetQuotedColumnName(m.Member.Name))
                .ToArray(),
            MemberExpression memberExpression => [
                table.GetQuotedColumnName(memberExpression.Member.Name)
            ],
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
            case MemberExpression m:
                return GetExcludedColumnName(table.GetColumnName(m.Member.Name));

            case BinaryExpression b:
                var left = ToSqlExpression<TEntity>(table, b.Left);
                var right = ToSqlExpression<TEntity>(table, b.Right);
                var op = b.NodeType switch
                {
                    ExpressionType.Add => b.Type == typeof(string) ? ConcatOperator : "+",
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
                    _ => throw new NotSupportedException($"Unsupported operator: {b.NodeType}")
                };
                return $"({left} {op} {right})";

            case ConstantExpression c:
                if (c.Type == typeof(RawSqlValue) && c.Value != null)
                {
                    return ((RawSqlValue)c.Value!).Sql;
                }

                if (c.Type == typeof(string) ||
                    c.Type == typeof(Guid))
                {
                    return $"'{c.Value}'";
                }

                if (c.Type == typeof(bool))
                {
                    return (bool)c.Value! ? "TRUE" : "FALSE";
                }

                return c.Value?.ToString() ?? "NULL";

            case UnaryExpression u:
                if (u.NodeType == ExpressionType.Convert)
                {
                    return ToSqlExpression<TEntity>(table, u.Operand);
                }
                if (u.NodeType == ExpressionType.Not)
                {
                    return $"NOT ({ToSqlExpression<TEntity>(table, u.Operand)})";
                }
                throw new NotSupportedException($"Unary operator not supported: {u.NodeType}");

            case MethodCallExpression mce:
                // Supporte quelques méthodes courantes (ToLower, ToUpper, Trim, etc.)
                var objSql = mce.Object != null ? ToSqlExpression<TEntity>(table, mce.Object) : null;
                var argsSql = mce.Arguments.Select(expr1 => ToSqlExpression<TEntity>(table, expr1)).ToArray();
                switch (mce.Method.Name)
                {
                    case "ToLower":
                        return $"LOWER({objSql})";
                    case "ToUpper":
                        return $"UPPER({objSql})";
                    case "Trim":
                        return $"BTRIM({objSql})";
                    case "Contains" when mce is { Object: not null, Arguments.Count: 1 }:
                        return $"{objSql} LIKE '%' || {argsSql[0]} || '%'";
                    case "StartsWith" when mce is { Object: not null, Arguments.Count: 1 }:
                        return $"{objSql} LIKE {argsSql[0]} || '%'";
                    case "EndsWith" when mce is { Object: not null, Arguments.Count: 1 }:
                        return $"{objSql} LIKE '%' || {argsSql[0]}";
                    default:
                        throw new NotSupportedException($"Method not supported: {mce.Method.Name}");
                }

            case ParameterExpression p:
                return Quote(p.Name ?? "param");

            default:
                throw new NotSupportedException($"Expression not supported: {expr.NodeType}");
        }
    }
}
