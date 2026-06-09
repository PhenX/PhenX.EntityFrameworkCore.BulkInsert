using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class ColumnMetadata
{
    public ColumnMetadata(IProperty property,  SqlDialectBuilder dialect, IComplexProperty? complexProperty = null)
    {
        StoreObjectIdentifier? ownerTable = complexProperty != null
            ? StoreObjectIdentifier.Table(complexProperty.DeclaringType.GetTableName()!, complexProperty.DeclaringType.GetSchema())
            : null;

        _getter = BuildGetter(property, complexProperty);
        Property = property;
        PropertyName = property.Name;
        ColumnName = ownerTable == null ? property.GetColumnName() : property.GetColumnName(ownerTable.Value)!;
        QuotedColumName = dialect.Quote(ColumnName);
        StoreDefinition = GetStoreDefinition(property);
        ClrType = property.ClrType;
        IsGenerated = property.ValueGenerated != ValueGenerated.Never
                      && (property.GetDefaultValueSql() != null
                          || property.GetComputedColumnSql() != null
                          || property.FindAnnotation(RelationalAnnotationNames.DefaultValue) != null
                          || (property.ClrType != typeof(Guid) && property.ClrType != typeof(Guid?)));
    }

    public ColumnMetadata(IComplexProperty jsonComplexProperty, IColumn? column, SqlDialectBuilder dialect)
    {
        var containerColumnName = (string)jsonComplexProperty.ComplexType
            .FindAnnotation(RelationalAnnotationNames.ContainerColumnName)!.Value!;

        _getter = BuildJsonComplexGetter(jsonComplexProperty);
        Property = null;
        PropertyName = jsonComplexProperty.Name;
        ColumnName = containerColumnName;
        QuotedColumName = dialect.Quote(ColumnName);
        StoreDefinition = column != null
            ? $"{column.StoreType} {(column.IsNullable ? "NULL" : "NOT NULL")}"
            : $"nvarchar(max) {(jsonComplexProperty.IsNullable ? "NULL" : "NOT NULL")}";
        ClrType = jsonComplexProperty.ClrType;
        IsGenerated = false;
    }

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new();

    private static Func<object, object?> BuildJsonComplexGetter(IComplexProperty complexProperty)
    {
        var getter = PropertyAccessor.CreateGetter(complexProperty.PropertyInfo!);

        return entity =>
        {
            var value = getter(entity);
            if (value == null) return null;
            return JsonSerializer.Serialize(value, _jsonSerializerOptions);
        };
    }

    private readonly Func<object, object?> _getter;

    public IProperty? Property { get; }

    public string PropertyName { get; }

    public string ColumnName { get; }

    public string QuotedColumName { get; }

    public string StoreDefinition { get; }

    public Type ClrType { get; }

    public bool IsGenerated { get; }

    public object GetValue(object entity, BulkInsertOptions options)
    {
        var result = _getter(entity);

        if (options.Converters != null && result != null)
        {
            foreach (var converter in options.Converters)
            {
                if (converter.TryConvertValue(result, options, out var temp))
                {
                    result = temp;
                    break;
                }
            }
        }

        return result ?? DBNull.Value;
    }

    private static Func<object, object?> BuildGetter(IProperty property, IComplexProperty? complexProperty)
    {
        var valueConverter =
            property.GetValueConverter() ??
            property.GetTypeMapping().Converter;

        var propInfo = property.PropertyInfo!;

        return PropertyAccessor.CreateGetter(propInfo, complexProperty, valueConverter?.ConvertToProviderExpression);
    }

    private static string GetStoreDefinition(IProperty property)
    {
        var typeMapping = property.GetRelationalTypeMapping();

        var nullability = property.IsNullable ? "NULL" : "NOT NULL";

        return $"{typeMapping.StoreType} {nullability}";
    }

    public override string ToString()
    {
        return $"Name: {PropertyName}, Column: {ColumnName}";
    }
}
