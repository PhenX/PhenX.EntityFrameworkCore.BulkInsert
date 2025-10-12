using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class TableMetadata
{
    private ColumnMetadata[]? _notGeneratedColumns;
    private ColumnMetadata[]? _primaryKeys;

    private readonly IEntityType _entityType;

    public string QuotedTableName { get; }

    public string TableName { get; }

    private ColumnMetadata[] Columns { get; }

    public TableMetadata(IEntityType entityType, SqlDialectBuilder dialect)
    {
        _entityType = entityType;
        TableName = entityType.GetTableName() ?? throw new InvalidOperationException("Cannot determine table name.");
        QuotedTableName = dialect.QuoteTableName(entityType.GetSchema(), TableName);
        Columns = GetColumns(entityType, dialect);
    }

    private static bool CanHandleProperty(IProperty property)
    {
        if (property.PropertyInfo == null || property.IsShadowProperty())
        {
            return false;
        }

        var getMethod = property.PropertyInfo.GetGetMethod();
        if (getMethod == null || getMethod.GetParameters().Length > 0)
        {
            return false;
        }

        return true;
    }

    private static ColumnMetadata[] GetColumns(IEntityType entityType, SqlDialectBuilder dialect)
    {
        var properties = entityType.GetProperties()
            .Where(CanHandleProperty)
            .Select(x => new ColumnMetadata(x, dialect));

        var complexProperties = entityType.GetComplexProperties()
            .SelectMany(cp => cp.ComplexType
                .GetProperties()
                .Where(CanHandleProperty)
                .Select(x => new ColumnMetadata(x, dialect, cp)));

        return properties.Concat(complexProperties).ToArray();
    }

    public ColumnMetadata[] PrimaryKey => _primaryKeys ??= GetPrimaryKey();

    public ColumnMetadata[] GetColumns(bool includeGenerated = true)
    {
        if (includeGenerated)
        {
            return Columns;
        }

        return _notGeneratedColumns ??= Columns.Where(x => !x.IsGenerated).ToArray();
    }

    public string GetQuotedColumnName(string propertyName)
    {
        var property = Columns.FirstOrDefault(x => x.PropertyName == propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found in entity type {_entityType.Name}.");

        return property.QuotedColumName;
    }

    public string GetColumnName(string propertyName)
    {
        var property = Columns.FirstOrDefault(x => x.PropertyName == propertyName)
            ?? throw new InvalidOperationException($"Property {propertyName} not found in entity type {_entityType.Name}.");

        return property.ColumnName;
    }

    private ColumnMetadata[] GetPrimaryKey()
    {
        var primaryKey = _entityType.FindPrimaryKey()?.Properties ?? [];

        return Columns
            .Where(x => primaryKey.Any(y => x.PropertyName == y.Name))
            .ToArray();
    }
}
