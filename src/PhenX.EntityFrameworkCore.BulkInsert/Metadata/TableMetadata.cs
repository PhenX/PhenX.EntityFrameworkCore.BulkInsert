using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class TableMetadata(IEntityType entityType, SqlDialectBuilder dialect)
{
    private IReadOnlyList<ColumnMetadata>? _notGeneratedColumns;
    private IReadOnlyList<ColumnMetadata>? _primaryKeys;

    public string QuotedTableName { get; } =
        dialect.QuoteTableName(entityType.GetSchema(), entityType.GetTableName()!);

    public string TableName { get; } =
        entityType.GetTableName() ?? throw new InvalidOperationException("Canot determine table name.");

    public IReadOnlyList<ColumnMetadata> Columns { get; } =
        entityType.GetProperties().Where(p => !p.IsShadowProperty()).Select(x => new ColumnMetadata(x, dialect)).ToList();

    public IReadOnlyList<ColumnMetadata> PrimaryKey =>
        _primaryKeys ??= GetPrimaryKey();

    public IReadOnlyList<ColumnMetadata> GetColumns(bool includeGenerated = true)
    {
        if (includeGenerated)
        {
            return Columns;
        }

        return _notGeneratedColumns ??= Columns.Where(x => !x.IsGenerated).ToList();
    }

    public string GetQuotedColumnName(string propertyName)
    {
        var property = Columns.FirstOrDefault(x => x.PropertyName == propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found in entity type {entityType.Name}.");

        return property.QuotedColumName;
    }

    public string GetColumnName(string propertyName)
    {
        var property = Columns.FirstOrDefault(x => x.PropertyName == propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found in entity type {entityType.Name}.");

        return property.ColumnName;
    }

    private List<ColumnMetadata> GetPrimaryKey()
    {
        var primaryKey = entityType.FindPrimaryKey()?.Properties ?? [];

        return Columns.Where(x => primaryKey.Any(y => x.PropertyName == y.Name)).ToList();
    }
}
