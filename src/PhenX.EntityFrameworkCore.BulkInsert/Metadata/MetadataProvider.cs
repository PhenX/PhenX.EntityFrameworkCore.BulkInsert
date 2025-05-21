using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal static class MetadataProvider<T> where T : DbContext
{
    public static readonly MetadataProvider Instance = new();
}

internal sealed class MetadataProvider
{
    private Dictionary<Type, TableMetadata>? _tables;

    public TableMetadata GetTableInfo<T>(DbContext context)
    {
        var tables = GetTables(context);

        if (!tables.TryGetValue(typeof(T), out var table))
        {
            throw new InvalidOperationException($"Cannot find metadata for type '{typeof(T)}'.");
        }

        return table;
    }

    private Dictionary<Type, TableMetadata> GetTables(DbContext context)
    {
        if (_tables != null)
        {
            return _tables;
        }

        lock (this)
        {
            if (_tables != null)
            {
                return _tables;
            }

            var provider = context.GetService<IBulkInsertProvider>();

            _tables =
                context.Model.GetEntityTypes()
                .ToDictionary(
                    x => x.ClrType,
                    x => new TableMetadata(x, provider.SqlDialect));
            return _tables;
        }
    }
}
