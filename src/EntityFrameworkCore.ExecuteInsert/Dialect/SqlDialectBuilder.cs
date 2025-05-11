using System.Linq.Expressions;
using System.Text;

using EntityFrameworkCore.ExecuteInsert.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.Dialect;

public abstract class SqlDialectBuilder
{
    protected abstract string OpenDelimiter { get; }
    protected abstract string CloseDelimiter { get; }

    protected virtual string ConcatOperator => "||";
    protected virtual bool SupportsMoveRows => true;

    /// <summary>
    /// Gets the name of the column for a property in a given entity type.
    /// </summary>
    /// <param name="context">The DbContext</param>
    /// <param name="propName">The property name</param>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <returns>The column name</returns>
    /// <exception cref="InvalidOperationException">Thrown when the entity type or property is not found.</exception>
    protected string GetColumnName<TEntity>(DbContext context, string propName)
    {
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        if (entityType == null)
        {
            throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} not found in the model.");
        }

        var property = entityType.FindProperty(propName);
        if (property == null)
        {
            throw new InvalidOperationException($"Property {propName} not found in entity type {typeof(TEntity).Name}.");
        }

        return Escape(property.GetColumnName());
    }

    /// <summary>
    /// Builds the SQL for moving data from one table to another.
    /// </summary>
    /// <param name="context">The DbContext</param>
    /// <param name="source">Source table name</param>
    /// <param name="target">Target table name</param>
    /// <param name="insertedProperties">Properties to be copied</param>
    /// <param name="properties">Properties to be returned</param>
    /// <param name="options">Bulk insert options</param>
    /// <param name="onConflict">On conflict options</param>
    /// <typeparam name="T">Entity type</typeparam>
    /// <returns>The SQL query</returns>
    public virtual string BuildMoveDataSql<T>(DbContext context, string source,
        string target,
        IProperty[] insertedProperties,
        IProperty[] properties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName()));
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => Escape(p.GetColumnName()));
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
                      INSERT INTO {target} ({insertedColumnList})
                      SELECT {insertedColumnList}
                      FROM {source}
                      WHERE TRUE
                      """);

        if (onConflict is OnConflictOptions<T> onConflictTyped)
        {
            q.AppendLine("ON CONFLICT");

            if (onConflictTyped.Update != null)
            {
                if (onConflictTyped.Match != null)
                {
                    q.AppendLine($"({string.Join(", ", GetColumns(context, onConflictTyped.Match))})");
                }

                if (onConflictTyped.Update != null)
                {
                    q.AppendLine($"DO UPDATE SET {string.Join(", ", GetUpdates(context, onConflictTyped.Update))}");
                }

                if (onConflictTyped.Condition != null)
                {
                    q.AppendLine($"WHERE {onConflictTyped.Condition}");
                }
            }
            else
            {
                q.AppendLine("DO NOTHING");
            }
        }

        if (columnList.Length != 0)
        {
            q.AppendLine($"RETURNING {columnList}");
        }

        q.AppendLine(";");

        return q.ToString();
    }

    /// <summary>
    /// Get the name of the excluded column for the ON CONFLICT clause.
    /// </summary>
    protected virtual string GetExcludedColumnName<TEntity>(DbContext context, MemberExpression member)
    {
        return $"EXCLUDED.{GetColumnName<TEntity>(context, member.Member.Name)}";
    }

    /// <summary>
    /// Escapes a column name using database-specific delimiters.
    /// </summary>
    public string Escape(string entity) => $"{OpenDelimiter}{entity}{CloseDelimiter}";

    /// <summary>
    /// Escapes a schema and table name using database-specific delimiters.
    /// </summary>
    public string EscapeTableName(string? schema, string tableName)
    {
        return schema != null
            ? $"{Escape(schema)}.{Escape(tableName)}"
            : Escape(tableName);
    }

    /// <summary>
    /// Gets column names for the insert statement, from an object initializer.
    /// </summary>
    protected string[] GetColumns<T>(DbContext context, Expression<Func<T, object>> columns)
    {
        return columns.Body switch
        {
            NewExpression newExpression => newExpression.Arguments.OfType<MemberExpression>()
                .Select(m => GetColumnName<T>(context, m.Member.Name))
                .ToArray(),
            MemberExpression memberExpression => [
                GetColumnName<T>(context, memberExpression.Member.Name)
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
    protected IEnumerable<string> GetUpdates<T>(DbContext context, Expression<Func<T, object>> update)
    {
        switch (update.Body)
        {
            case NewExpression { Members: not null } newExpr:
            {
                foreach (var arg in newExpr.Arguments.Zip(newExpr.Members, (expr, member) => (expr, member)))
                {
                    yield return $"{GetColumnName<T>(context, arg.member.Name)} = {ToSqlExpression<T>(context, arg.expr)}";
                }

                break;
            }
            case MemberInitExpression memberInit:
            {
                foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                {
                    yield return $"{GetColumnName<T>(context, binding.Member.Name)} = {ToSqlExpression<T>(context, binding.Expression)}";
                }

                break;
            }
            case MemberExpression memberExpr:
                yield return $"{GetColumnName<T>(context, memberExpr.Member.Name)} = {ToSqlExpression<T>(context, memberExpr)}";
                break;
            default:
                throw new NotSupportedException("Unsupported expression type for update");
        }
    }

    /// <summary>
    /// Converts an expression to a SQL string.
    /// </summary>
    /// <param name="context">The DbContext</param>
    /// <param name="expr">The expression, with simple operations</param>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <returns>An SQL statement</returns>
    /// <exception cref="NotSupportedException">Thrown when an expression could not be translated.</exception>
    private string ToSqlExpression<TEntity>(DbContext context, Expression expr)
    {
        switch (expr)
        {
            case MemberExpression m:
                return GetExcludedColumnName<TEntity>(context, m);

            case BinaryExpression b:
                var left = ToSqlExpression<TEntity>(context, b.Left);
                var right = ToSqlExpression<TEntity>(context, b.Right);
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
                    return ToSqlExpression<TEntity>(context, u.Operand);
                }
                if (u.NodeType == ExpressionType.Not)
                {
                    return $"NOT ({ToSqlExpression<TEntity>(context, u.Operand)})";
                }
                throw new NotSupportedException($"Unary operator not supported: {u.NodeType}");

            case MethodCallExpression mce:
                // Supporte quelques méthodes courantes (ToLower, ToUpper, Trim, etc.)
                var objSql = mce.Object != null ? ToSqlExpression<TEntity>(context, mce.Object) : null;
                var argsSql = mce.Arguments.Select(expr1 => ToSqlExpression<TEntity>(context, expr1)).ToArray();
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
                return Escape(p.Name ?? "param");

            default:
                throw new NotSupportedException($"Expression not supported: {expr.NodeType}");
        }
    }
}
