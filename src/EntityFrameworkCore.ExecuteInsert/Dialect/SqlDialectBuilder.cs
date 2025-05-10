using System.Linq.Expressions;
using System.Text;

using EntityFrameworkCore.ExecuteInsert.Extensions;
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

    public virtual string BuildMoveDataSql<T>(
        string source,
        string target,
        IProperty[] insertedProperties,
        IProperty[] properties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName()));
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => $"{Escape(p.GetColumnName())} AS {Escape(p.Name)}");
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
                    q.AppendLine($"({string.Join(", ", GetColumns(onConflictTyped.Match).Select(Escape))})");
                }

                if (onConflictTyped.Update != null)
                {
                    q.AppendLine($"DO UPDATE SET {string.Join(", ", GetUpdates(onConflictTyped.Update))}");
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

    protected virtual string GetExcludedColumnName(MemberExpression member)
    {
        var prefix = "EXCLUDED";
        return $"{prefix}.{Escape(member.Member.Name)}";
    }

    /// <summary>
    /// Escapes a column name using database-specific delimiters.
    /// </summary>
    public string Escape(string entity)
    {
        return $"{OpenDelimiter}{entity}{CloseDelimiter}";
    }

    /// <summary>
    /// Escapes a schema and table name using database-specific delimiters.
    /// </summary>
    public string EscapeTableName(string? schema, string tableName)
    {
        return schema != null
            ? $"{Escape(schema)}.{Escape(tableName)}"
            : Escape(tableName);
    }

    public string[] GetEscapedColumns(DbContext context, Type entityType, bool includeGenerated = true)
    {
        return context.GetProperties(entityType, includeGenerated)
            .Select(p => Escape(p.Name))
            .ToArray();
    }

    protected string[] GetColumns<T>(Expression<Func<T, object>> columns)
    {
        return columns.Body switch
        {
            NewExpression newExpression => newExpression.Arguments.OfType<MemberExpression>()
                .Select(m => m.Member.Name)
                .ToArray(),
            MemberExpression memberExpression => [
                memberExpression.Member.Name
            ],
            _ => throw new NotSupportedException("Unsupported expression type")
        };
    }

    protected IEnumerable<string> GetUpdates<T>(Expression<Func<T, object>> update)
    {
        switch (update.Body)
        {
            case NewExpression { Members: not null } newExpr:
            {
                foreach (var arg in newExpr.Arguments.Zip(newExpr.Members, (expr, member) => (expr, member)))
                {
                    yield return $"{Escape(arg.member.Name)} = {ToSqlExpression(arg.expr)}";
                }

                break;
            }
            case MemberInitExpression memberInit:
            {
                foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                {
                    yield return $"{Escape(binding.Member.Name)} = {ToSqlExpression(binding.Expression)}";
                }

                break;
            }
            case MemberExpression memberExpr:
                yield return $"{Escape(memberExpr.Member.Name)} = {ToSqlExpression(memberExpr)}";
                break;
            default:
                throw new NotSupportedException("Unsupported expression type for update");
        }
    }

    private string ToSqlExpression(Expression expr)
    {
        switch (expr)
        {
            case MemberExpression m:
                return GetExcludedColumnName(m);

            case BinaryExpression b:
                var left = ToSqlExpression(b.Left);
                var right = ToSqlExpression(b.Right);
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
                    return ToSqlExpression(u.Operand);
                }
                if (u.NodeType == ExpressionType.Not)
                {
                    return $"NOT ({ToSqlExpression(u.Operand)})";
                }
                throw new NotSupportedException($"Unary operator not supported: {u.NodeType}");

            case MethodCallExpression mce:
                // Supporte quelques méthodes courantes (ToLower, ToUpper, Trim, etc.)
                var objSql = mce.Object != null ? ToSqlExpression(mce.Object) : null;
                var argsSql = mce.Arguments.Select(ToSqlExpression).ToArray();
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
