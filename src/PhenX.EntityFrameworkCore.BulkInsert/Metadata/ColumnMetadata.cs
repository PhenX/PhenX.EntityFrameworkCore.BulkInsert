using Microsoft.EntityFrameworkCore;
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
        IsGenerated = property.ValueGenerated != ValueGenerated.Never;
    }

    private readonly Func<object, object?> _getter;

    public IProperty Property { get; }

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
