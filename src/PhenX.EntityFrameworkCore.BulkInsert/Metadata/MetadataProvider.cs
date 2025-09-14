using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class MetadataProvider
{
    private Dictionary<Type, Dictionary<Type, TableMetadata>> _tablesPerContext = new();

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
        lock (_tablesPerContext)
        {
            var type = context.GetType();
            if (_tablesPerContext.TryGetValue(context.GetType(), out var tables))
            {
                return tables;
            }

            var provider = context.GetService<IBulkInsertProvider>();

            tables = context.Model.GetEntityTypes()
                .GroupBy(x => x.ClrType)
                .ToDictionary(
                    x => x.Key,
                    x => new TableMetadata(x.First(), provider.SqlDialect));

            _tablesPerContext[type] = tables;

            return tables;
        }
    }
}
