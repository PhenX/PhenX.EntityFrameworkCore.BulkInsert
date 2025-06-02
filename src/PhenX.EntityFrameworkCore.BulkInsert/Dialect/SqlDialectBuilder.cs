using System.Linq.Expressions;
using System.Text;

using Microsoft.EntityFrameworkCore;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Dialect;

internal abstract class SqlDialectBuilder
{
    protected const string PseudoTableInserted = "INSERTED";
    protected const string PseudoTableExcluded = "EXCLUDED";

    protected abstract string OpenDelimiter { get; }
    protected abstract string CloseDelimiter { get; }

    protected virtual string ConcatOperator => "||";

    /// <summary>
    /// Indicates whether the dialect supports moving rows from temporary table to the final table, in order to
    /// theoretically reduce disk space requirements.
    /// </summary>
    protected virtual bool SupportsMoveRows => true;

    /// <summary>
    /// Indicates whether the dialect supports INSERT INTO table AS alias.
    /// </summary>
    protected virtual bool SupportsInsertIntoAlias => true;

    protected static string CreateTableCopySqlBase(string tempTableName, IReadOnlyList<ColumnMetadata> columns)
    {
        var q = new StringBuilder();

        q.Append($"CREATE TABLE {tempTableName} (");
        q.AppendJoin(",", columns, (sb, column) => sb.AppendLine($"{column.QuotedColumName} {column.StoreDefinition}"));
        q.AppendLine(")");

        return q.ToString();
    }

    public abstract string CreateTableCopySql(string tempNameName, TableMetadata tableInfo, IReadOnlyList<ColumnMetadata> columns);

    /// <summary>
    /// Builds the SQL for moving data from one table to another.
    /// </summary>
    /// <param name="context">DB context</param>
    /// <param name="source">Source table</param>
    /// <param name="target">Target table name</param>
    /// <param name="insertedColumns">Columns to be inserted</param>
    /// <param name="returnedColumns">Columns to be returned</param>
    /// <param name="options">Bulk insert options</param>
    /// <param name="onConflict">On conflict options</param>
    /// <typeparam name="T">Entity type</typeparam>
    /// <returns>The SQL query</returns>
    public virtual string BuildMoveDataSql<T>(
        DbContext context,
        TableMetadata target,
        string source,
        IReadOnlyList<ColumnMetadata> insertedColumns,
        IReadOnlyList<ColumnMetadata> returnedColumns,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var q = new StringBuilder();

        if (SupportsMoveRows && options.MoveRows)
        {
            // WITH moved_rows AS (DELETE FROM {source) RETURNING {insertedColumns})
            q.Append($"WITH moved_rows AS (DELETE FROM {source} RETURNING ");
            q.AppendColumns(insertedColumns);
            q.AppendLine(")");

            source = "moved_rows";
        }

        // INSERT INTO {target} ({columns}) SELECT {columns} FROM {source} WHERE TRUE
        q.Append($"INSERT INTO {target.QuotedTableName}");

        if (SupportsInsertIntoAlias)
        {
            q.Append($" AS {PseudoTableInserted}");
        }

        q.AppendLine(" (");
        q.AppendColumns(insertedColumns);
        q.AppendLine(")");
        q.Append("SELECT ");
        q.AppendColumns(insertedColumns);
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
                    AppendOnConflictUpdate(q, GetUpdates(context, target, insertedColumns, onConflictTyped.Update));
                }

                if (onConflictTyped.RawWhere != null || onConflictTyped.Where != null)
                {
                    if (onConflictTyped is { RawWhere: not null, Where: not null })
                    {
                        throw new ArgumentException("Cannot specify both RawWhere and Where in OnConflictOptions.");
                    }

                    q.Append(" WHERE ");
                    AppendConflictCondition(q, target, context, onConflictTyped);
                }
            }
            else
            {
                q.Append(' ');
                AppendDoNothing(q, insertedColumns);
            }
        }

        if (returnedColumns.Count != 0)
        {
            q.Append(" RETURNING ");
            q.AppendJoin(", ", returnedColumns.Select(p => p.QuotedColumName));
            q.AppendLine();
        }

        q.AppendLine(";");

        return q.ToString();
    }

    protected virtual void AppendDoNothing(StringBuilder sql, IEnumerable<ColumnMetadata> insertedColumns)
    {
        sql.AppendLine("DO NOTHING");
    }

    protected virtual void AppendOnConflictUpdate(StringBuilder sql, IEnumerable<string> updates)
    {
        sql.AppendLine("DO UPDATE SET");
        sql.AppendJoin(", ", updates);
    }

    protected virtual string Trim(string lhs) => $"BTRIM({lhs})";

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

    protected virtual void AppendConflictCondition<T>(StringBuilder sql, TableMetadata target, DbContext context,
        OnConflictOptions<T> onConflictTyped)
    {
        var condition = "";

        if (onConflictTyped.RawWhere != null)
        {
            condition = onConflictTyped.RawWhere(PseudoTableInserted, PseudoTableExcluded);
        }
        else if (onConflictTyped.Where != null)
        {
            condition = ToSqlExpression<T>(context, target, onConflictTyped.Where);
        }

        sql.Append(condition).AppendLine();
    }

    /// <summary>
    /// Get the name of the INSERTED column (data already in the table) for the ON CONFLICT clause.
    /// </summary>
    protected virtual string GetInsertedColumnName(string columnName) => $"{PseudoTableInserted}.{Quote(columnName)}";

    /// <summary>
    /// Get the name of the EXCLUDED column (data conflicting with table) for the ON CONFLICT clause.
    /// </summary>
    protected virtual string GetExcludedColumnName(string columnName) => $"{PseudoTableExcluded}.{Quote(columnName)}";

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
    protected static string[] GetColumns<T>(TableMetadata table, Expression<Func<T, object>> columns)
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
    protected IEnumerable<string> GetUpdates<T>(DbContext context, TableMetadata table, IEnumerable<ColumnMetadata> columns, Expression<Func<T, T, object>> update)
    {
        if (update is not LambdaExpression lambda)
        {
            throw new ArgumentException("Update expression must be a lambda expression.");
        }

        switch (update.Body)
        {
            case NewExpression { Members: not null } newExpr:
            {
                foreach (var arg in newExpr.Arguments.Zip(newExpr.Members, (expr, member) => (expr, member)))
                {
                    yield return $"{table.GetColumnName(arg.member.Name)} = {ToSqlExpression<T>(context, table, arg.expr, lambda)}";
                }

                break;
            }
            case MemberInitExpression memberInit:
            {
                foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                {
                    yield return $"{table.GetColumnName(binding.Member.Name)} = {ToSqlExpression<T>(context, table, binding.Expression, lambda)}";
                }

                break;
            }
            case MemberExpression memberExpr:
                yield return $"{table.GetColumnName(memberExpr.Member.Name)} = {ToSqlExpression<T>(context, table, memberExpr, lambda)}";
                break;

            case ParameterExpression parameterExpr when (parameterExpr.Type == typeof(T)):
                foreach (var property in columns)
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
    /// <param name="context">DB context</param>
    /// <param name="table">The DbContext</param>
    /// <param name="expr">The expression, with simple operations</param>
    /// <param name="lambda">Current lambda expression</param>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <returns>An SQL statement</returns>
    /// <exception cref="NotSupportedException">Thrown when an expression could not be translated.</exception>
    private string ToSqlExpression<TEntity>(DbContext context, TableMetadata table, Expression expr, LambdaExpression? lambda = null)
    {
        switch (expr)
        {
            case LambdaExpression memberExpr:
                return ToSqlExpression<TEntity>(context, table, memberExpr.Body, memberExpr);

            case MemberExpression memberExpr:
                var columnName = table.GetColumnName(memberExpr.Member.Name);

                // If the member expression is a property of the current lambda
                if (lambda is { Parameters.Count: > 1 } && memberExpr.Expression is ParameterExpression paramExpr)
                {
                    if (paramExpr.Name == lambda.Parameters[0].Name)
                    {
                        return GetInsertedColumnName(columnName);
                    }

                    if (paramExpr.Name == lambda.Parameters[1].Name)
                    {
                        return GetExcludedColumnName(columnName);
                    }
                }

                return GetExcludedColumnName(columnName);

            case ConditionalExpression condExpr:
                var test = ToSqlExpression<TEntity>(context, table, condExpr.Test, lambda);
                var ifTrue = ToSqlExpression<TEntity>(context, table, condExpr.IfTrue, lambda);
                var ifFalse = ToSqlExpression<TEntity>(context, table, condExpr.IfFalse, lambda);
                return $"CASE WHEN {test} THEN {ifTrue} ELSE {ifFalse} END";

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

                    var lhs = ToSqlExpression<TEntity>(context, table, binaryExpr.Left, lambda);
                    var rhs = ToSqlExpression<TEntity>(context, table, binaryExpr.Right, lambda);

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
                    return ToSqlExpression<TEntity>(context, table, unaryExpr.Operand, lambda);
                }
                if (unaryExpr.NodeType == ExpressionType.Not)
                {
                    return $"NOT ({ToSqlExpression<TEntity>(context, table, unaryExpr.Operand, lambda)})";
                }
                throw new NotSupportedException($"Unary operator not supported: {unaryExpr.NodeType}");

            case MethodCallExpression methodExpr:
                {
                    var lhs = methodExpr.Object != null ? ToSqlExpression<TEntity>(context, table, methodExpr.Object, lambda) : "";

                    switch (methodExpr.Method.Name)
                    {
                        case "ToLower":
                            return $"LOWER({lhs})";
                        case "ToUpper":
                            return $"UPPER({lhs})";
                        case "Trim":
                            return Trim(lhs);
                        case "Contains" when methodExpr is { Object: not null, Arguments.Count: 1 }:
                            return $"{lhs} LIKE '%' {ConcatOperator} {ToSqlExpression<TEntity>(context, table, methodExpr.Arguments[0], lambda)} {ConcatOperator} '%'";
                        case "EndsWith" when methodExpr is { Object: not null, Arguments.Count: 1 }:
                            return $"{lhs} LIKE '%' {ConcatOperator} {ToSqlExpression<TEntity>(context, table, methodExpr.Arguments[0], lambda)}";
                        case "StartsWith" when methodExpr is { Object: not null, Arguments.Count: 1 }:
                            return $"{lhs} LIKE {ToSqlExpression<TEntity>(context, table, methodExpr.Arguments[0], lambda)} {ConcatOperator} '%'";
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
