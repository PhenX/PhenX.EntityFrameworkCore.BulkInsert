using System.Data.Common;

using EntityFrameworkCore.ExecuteInsert.Extensions;
using EntityFrameworkCore.ExecuteInsert.Options;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace EntityFrameworkCore.ExecuteInsert.Sqlite;

[UsedImplicitly]
public class SqliteBulkInsertProvider : BulkInsertProviderBase<SqliteDialectBuilder>
{
    protected override string BulkInsertId => "rowid";

    //language=sql
    protected override string CreateTableCopySql => "CREATE TEMP TABLE {0} AS SELECT * FROM {1} WHERE 0;";

    //language=sql
    protected override string AddTableCopyBulkInsertId => "--"; // No need to add an ID column in SQLite

    protected override Task AddBulkInsertIdColumn<T>(DbConnection connection, CancellationToken cancellationToken,
        string tempTableName) where T : class
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Taken from https://github.com/dotnet/efcore/blob/667c569c49a1ab7e142621395d3f14f2af0508b4/src/Microsoft.Data.Sqlite.Core/SqliteValueBinder.cs#L231
    /// As the method is not exposed in the public API, we need to copy it here.
    /// </summary>
    private static readonly Dictionary<Type, SqliteType> SqliteTypeMapping =
        new()
        {
            { typeof(bool), SqliteType.Integer },
            { typeof(byte), SqliteType.Integer },
            { typeof(byte[]), SqliteType.Blob },
            { typeof(char), SqliteType.Text },
            { typeof(DateTime), SqliteType.Text },
            { typeof(DateTimeOffset), SqliteType.Text },
            { typeof(DateOnly), SqliteType.Text },
            { typeof(TimeOnly), SqliteType.Text },
            { typeof(DBNull), SqliteType.Text },
            { typeof(decimal), SqliteType.Text },
            { typeof(double), SqliteType.Real },
            { typeof(float), SqliteType.Real },
            { typeof(Guid), SqliteType.Text },
            { typeof(int), SqliteType.Integer },
            { typeof(long), SqliteType.Integer },
            { typeof(sbyte), SqliteType.Integer },
            { typeof(short), SqliteType.Integer },
            { typeof(string), SqliteType.Text },
            { typeof(TimeSpan), SqliteType.Text },
            { typeof(uint), SqliteType.Integer },
            { typeof(ulong), SqliteType.Integer },
            { typeof(ushort), SqliteType.Integer }
        };

    private static SqliteType GetSqliteType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
        type = type.IsEnum ? Enum.GetUnderlyingType(type) : type;

        if (SqliteTypeMapping.TryGetValue(type, out var sqliteType))
        {
            return sqliteType;
        }

        throw new InvalidOperationException("Unknown Sqlite type for " + clrType);
    }

    private DbCommand GetInsertCommand(DbContext context, Type entityType, string tableName,
        int batchSize)
    {
        var columns = context.GetProperties(entityType, false);
        var cmd = context.Database.GetDbConnection().CreateCommand();

        var sqliteColumns = columns
            .Select(c => new
            {
                Name = c.GetColumnName(),
                Type = GetSqliteType(c.GetProviderClrType() ?? c.ClrType)
            })
            .ToArray();

        var i = 0;
        var batches = Enumerable
            .Repeat(0, batchSize)
            .Select(_ =>
            {
                var cols = sqliteColumns.Select(column =>
                {
                    var paramName = $"@p{i++}";

                    cmd.Parameters.Add(new SqliteParameter(paramName, column.Type));

                    return paramName;
                });

                return $"({string.Join(",", cols)})";
            });

        var sql = $"INSERT INTO {tableName} ({string.Join(",", sqliteColumns.Select(c => Quote(c.Name)))}) VALUES {string.Join(",", batches)}";

        cmd.CommandText = sql;

        cmd.Prepare();

        return cmd;
    }

    protected override async Task BulkInsert<T>(DbContext context, IEnumerable<T> entities,
        string tableName, PropertyAccessor[] properties, BulkInsertOptions options, CancellationToken ctk) where T : class
    {
        var connection = context.Database.GetDbConnection();

        await using var transaction = await connection.BeginTransactionAsync(ctk);

        const int maxParams = 1000;
        var batchSize = options.BatchSize ?? 5;
        batchSize = Math.Min(batchSize, maxParams / properties.Length);

        await using var insertCommand = GetInsertCommand(context, typeof(T), tableName, batchSize);

        foreach (var chunk in entities.Chunk(batchSize))
        {
            // Full chunks
            if (chunk.Length == batchSize)
            {
                FillValues(chunk, insertCommand.Parameters, properties);

                await insertCommand.ExecuteNonQueryAsync(ctk);
            }
            // Last chunk
            else
            {
                var partialInsertCommand = GetInsertCommand(context, typeof(T), tableName, chunk.Length);
                FillValues(chunk, partialInsertCommand.Parameters, properties);

                await partialInsertCommand.ExecuteNonQueryAsync(ctk);
            }
        }

        await transaction.CommitAsync(ctk);
    }

    private static void FillValues<T>(T[] chunk, DbParameterCollection parameters, PropertyAccessor[] properties) where T : class
    {
        var index = 0;
        foreach (var entity in chunk)
        {
            foreach (var property in properties)
            {
                var value = property.GetValue(entity);
                parameters[index].Value = value;

                index++;
            }
        }
    }
}

