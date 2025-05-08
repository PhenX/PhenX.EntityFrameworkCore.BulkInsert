using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using EntityFrameworkCore.ExecuteInsert.OnConflict;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Npgsql;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

public class PostgreSqlBulkInsertProvider : BulkInsertProviderBase
{
    public override string OpenDelimiter => "\"";
    public override string CloseDelimiter => "\"";

    //language=sql
    protected override string CreateTableCopySql => "CREATE TEMPORARY TABLE {0} AS TABLE {1} WITH NO DATA;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => $"ALTER TABLE {{0}} ADD COLUMN {BulkInsertId} SERIAL PRIMARY KEY;";

    private string GetBinaryImportCommand(DbContext context, Type entityType, string tableName)
    {
        var columns = GetEscapedColumns(context, entityType, false);

        return $"COPY {tableName} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
    }

    protected override string BuildInsertSelectQuery<T>(string tableName,
        string targetTableName,
        IProperty[] insertedProperties,
        IProperty[] properties,
        BulkInsertOptions options, OnConflictOptions? onConflict = null)
    {
        var insertedColumns = insertedProperties.Select(p => Escape(p.GetColumnName()));
        var insertedColumnList = string.Join(", ", insertedColumns);

        var returnedColumns = properties.Select(p => $"{Escape(p.GetColumnName())} AS {Escape(p.Name)}");
        var columnList = string.Join(", ", returnedColumns);

        var q = new StringBuilder();

        if (options.MoveRows)
        {
            q.AppendLine($"""
                    WITH moved_rows AS (
                       DELETE FROM {tableName}
                           RETURNING {insertedColumnList}
                    )
                    """);
            tableName = "moved_rows";
        }

        q.AppendLine($"""
                      INSERT INTO {targetTableName} ({insertedColumnList})
                      SELECT {insertedColumnList}
                      FROM {tableName}
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

    private IEnumerable<string> GetUpdates<T>(Expression<Func<T, object>> update)
    {
        if (update.Body is NewExpression { Members: not null } newExpr)
        {
            foreach (var arg in newExpr.Arguments.Zip(newExpr.Members, (expr, member) => (expr, member)))
            {
                yield return $"{Escape(arg.member.Name)} = {ToSqlExpression(arg.expr)}";
            }
        }
        else if (update.Body is MemberInitExpression memberInit)
        {
            foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
            {
                yield return $"{Escape(binding.Member.Name)} = {ToSqlExpression(binding.Expression)}";
            }
        }
        else if (update.Body is MemberExpression memberExpr)
        {
            yield return $"{Escape(memberExpr.Member.Name)} = {ToSqlExpression(memberExpr)}";
        }
        else
        {
            throw new NotSupportedException("Unsupported expression type for update");
        }
    }

    private string ToSqlExpression(Expression expr)
    {
        switch (expr)
        {
            case MemberExpression m:
                var prefix = "EXCLUDED";
                return $"{prefix}.{Escape(m.Member.Name)}";

            case BinaryExpression b:
                var left = ToSqlExpression(b.Left);
                var right = ToSqlExpression(b.Right);
                var op = b.NodeType switch
                {
                    ExpressionType.Add => b.Type == typeof(string) ? "||" : "+",
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
                    _ => throw new NotSupportedException($"Opérateur non supporté: {b.NodeType}")
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

    private string[] GetColumns<T>(Expression<Func<T, object>> columns)
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

    protected override async Task BulkImport<T>(DbContext context, DbConnection connection, IEnumerable<T> entities,
        string tableName, PropertyAccessor[] properties, CancellationToken ctk) where T : class
    {
        var importCommand = GetBinaryImportCommand(context, typeof(T), tableName);

        await using (var writer = await ((NpgsqlConnection)connection).BeginBinaryImportAsync(importCommand, ctk))
        {
            foreach (var entity in entities)
            {
                await writer.StartRowAsync(ctk);

                foreach (var property in properties)
                {
                    var value = property.GetValue(entity);

                    await writer.WriteAsync(value, ctk);
                }
            }

            await writer.CompleteAsync(ctk);
        }
    }
}
