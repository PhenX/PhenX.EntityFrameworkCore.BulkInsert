using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class MetadataProvider
{
    private readonly Dictionary<Type, Dictionary<Type, TableMetadata>> _tablesPerContext = new();

    public TableMetadata GetTableInfo<T>(DbContext context)
    {
        lock (_tablesPerContext)
        {
            var type = context.GetType();

            if (!_tablesPerContext.TryGetValue(type, out var tables))
            {
                tables = new Dictionary<Type, TableMetadata>();
                _tablesPerContext[type] = tables;
            }

            var modelType = typeof(T);

            if (tables.TryGetValue(modelType, out var table))
            {
                return table;
            }

            var entityType = context.Model.FindEntityType(modelType);
            if (entityType == null)
            {
                throw new InvalidOperationException($"The type '{modelType.FullName}' is not part of the model for the current context.");
            }


            // Filter out entities without an associated table
            // See also https://learn.microsoft.com/en-us/ef/core/modeling/keyless-entity-types
            if (entityType.GetTableName() is null)
            {
            }

            var provider = context.GetService<IBulkInsertProvider>();

            var tableMetadata = new TableMetadata(entityType, provider.SqlDialect);
            tables[modelType] = tableMetadata;

            return tableMetadata;
        }
    }
}
