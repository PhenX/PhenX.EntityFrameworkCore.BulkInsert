using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class TableMetadata(IEntityType entityType, SqlDialectBuilder dialect)
{
    private IReadOnlyList<PropertyMetadata>? _notGeneratedProperties;
    private IReadOnlyList<PropertyMetadata>? _primaryKeys;

    public string QuotedTableName { get; } =
        dialect.QuoteTableName(entityType.GetSchema(), entityType.GetTableName()!);

    public string TableName { get; } =
        entityType.GetTableName() ?? throw new InvalidOperationException("Canot determine table name.");

    public IReadOnlyList<PropertyMetadata> Properties { get; } =
        entityType.GetProperties().Where(p => !p.IsShadowProperty()).Select(x => new PropertyMetadata(x, dialect)).ToList();

    public IReadOnlyList<PropertyMetadata> PrimaryKey =>
        _primaryKeys ??= GetPrimaryKey();

    public IReadOnlyList<PropertyMetadata> GetProperties(bool includeGenerated = true)
    {
        if (includeGenerated)
        {
            return Properties;
        }

        return _notGeneratedProperties ??= Properties.Where(x => !x.IsGenerated).ToList();
    }

    public string GetQuotedColumnName(string propertyName)
    {
        var property = Properties.FirstOrDefault(x => x.Name == propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found in entity type {entityType.Name}.");

        return property.QuotedColumName;
    }

    public string GetColumnName(string propertyName)
    {
        var property = Properties.FirstOrDefault(x => x.Name == propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found in entity type {entityType.Name}.");

        return property.ColumnName;
    }

    private List<PropertyMetadata> GetPrimaryKey()
    {
        var primaryKey = entityType.FindPrimaryKey()?.Properties ?? [];

        return Properties.Where(x => primaryKey.Any(y => x.Name == y.Name)).ToList();
    }
}
