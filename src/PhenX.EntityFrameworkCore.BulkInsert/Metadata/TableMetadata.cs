using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

        var table = entityType.GetTableMappings().FirstOrDefault()?.Table;

        var regularComplexProperties = entityType.GetComplexProperties()
            .Where(cp => !IsJsonMappedComplexProperty(cp))
            .SelectMany(cp => cp.ComplexType
                .GetProperties()
                .Where(CanHandleProperty)
                .Select(x => new ColumnMetadata(x, dialect, cp)));

        var jsonComplexProperties = entityType.GetComplexProperties()
            .Where(IsJsonMappedComplexProperty)
            .Select(cp =>
            {
                var containerColumnName = (string)cp.ComplexType
                    .FindAnnotation(RelationalAnnotationNames.ContainerColumnName)!.Value!;
                var column = table?.FindColumn(containerColumnName);
                return new ColumnMetadata(cp, column, dialect);
            });

        return properties.Concat(regularComplexProperties).Concat(jsonComplexProperties).ToArray();
    }

    private static bool IsJsonMappedComplexProperty(IComplexProperty complexProperty)
    {
        var annotation = complexProperty.ComplexType
            .FindAnnotation(RelationalAnnotationNames.ContainerColumnName)?.Value as string;
        return !string.IsNullOrEmpty(annotation);
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
