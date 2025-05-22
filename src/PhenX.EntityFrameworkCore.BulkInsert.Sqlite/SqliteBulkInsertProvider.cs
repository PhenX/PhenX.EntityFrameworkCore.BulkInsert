using System.Data.Common;
using System.Text;

using JetBrains.Annotations;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Sqlite;

[UsedImplicitly]
internal class SqliteBulkInsertProvider : BulkInsertProviderBase<SqliteDialectBuilder>
{
    public SqliteBulkInsertProvider(ILogger<SqliteBulkInsertProvider>? logger = null) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override string BulkInsertId => "rowid";

    //language=sql
    /// <inheritdoc />
    protected override string AddTableCopyBulkInsertId => "--"; // No need to add an ID column in SQLite

    /// <inheritdoc />
    protected override Task AddBulkInsertIdColumn<T>(
        bool sync,
        DbContext context,
        string tempTableName,
        CancellationToken cancellationToken
    ) where T : class => Task.CompletedTask;

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

        throw new InvalidOperationException($"Unknown Sqlite type for {clrType}");
    }

    private static DbCommand GetInsertCommand(
        DbContext context,
        string tableName,
        IReadOnlyList<PropertyMetadata> columns,
        SqliteType[] columnTypes,
        StringBuilder sb,
        int batchSize)
    {
        var command = context.Database.GetDbConnection().CreateCommand();

        sb.Clear();
        sb.AppendLine($"INSERT INTO {tableName} (");
        sb.AppendColumns(columns);
        sb.AppendLine(")");
        sb.AppendLine("VALUES");

        var p = 0;
        for (var i = 0; i < batchSize; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append('(');

            var columnIndex = 0;
            foreach (var column in columns)
            {
                var parameterName = $"@p{p++}";
                command.Parameters.Add(new SqliteParameter(parameterName, columnTypes[columnIndex]));

                if (columnIndex > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(parameterName);
                columnIndex++;
            }

            sb.Append(')');
            sb.AppendLine();
        }

        command.CommandText = sb.ToString();
        command.Prepare();

        return command;
    }

    /// <inheritdoc />
    protected override async Task BulkInsert<T>(
        bool sync,
        DbContext context,
        TableMetadata tableInfo,
        IEnumerable<T> entities,
        string tableName,
        IReadOnlyList<PropertyMetadata> properties,
        BulkInsertOptions options,
        CancellationToken ctk
    ) where T : class
    {
        const int maxParams = 1000;
        var batchSize = options.BatchSize ?? 5;
        batchSize = Math.Min(batchSize, maxParams / properties.Count);

        // The StringBuilder can be resuse between the batches. 
        var sb = new StringBuilder();

        var columnList = tableInfo.GetProperties(options.CopyGeneratedColumns);
        var columnTypes = columnList.Select(c => GetSqliteType(c.ProviderClrType ?? c.ClrType)).ToArray();

        await using var insertCommand =
            GetInsertCommand(
                context,
                tableName,
                columnList,
                columnTypes,
                sb,
                batchSize);

        foreach (var chunk in entities.Chunk(batchSize))
        {
            // Full chunks
            if (chunk.Length == batchSize)
            {
                FillValues(chunk, insertCommand.Parameters, properties);
                await ExecuteCommand(sync, insertCommand, ctk);
            }
            // Last chunk
            else
            {
                await using var partialInsertCommand =
                GetInsertCommand(
                    context,
                    tableName,
                    columnList,
                    columnTypes,
                    sb,
                    chunk.Length);

                FillValues(chunk, partialInsertCommand.Parameters, properties);
                await ExecuteCommand(sync, partialInsertCommand, ctk);
            }
        }
    }

    private static async Task ExecuteCommand(bool sync, DbCommand insertCommand, CancellationToken ctk)
    {
        if (sync)
        {
            // ReSharper disable once MethodHasAsyncOverloadWithCancellation
            insertCommand.ExecuteNonQuery();
        }
        else
        {
            await insertCommand.ExecuteNonQueryAsync(ctk);
        }
    }

    private static void FillValues<T>(T[] chunk, DbParameterCollection parameters, IReadOnlyList<PropertyMetadata> properties) where T : class
    {
        var p = 0;
        foreach (var entity in chunk)
        {
            foreach (var property in properties)
            {
                var value = property.GetValue(entity);
                parameters[p].Value = value;
                p++;
            }
        }
    }
}

