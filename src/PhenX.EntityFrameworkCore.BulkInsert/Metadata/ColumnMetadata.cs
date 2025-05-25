using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class ColumnMetadata(IProperty property,  SqlDialectBuilder dialect)
{
    private readonly PropertyAccessor.Getter<object, object?> _getter = BuildGetter(property);

    public IProperty Property { get; } = property;

    public string PropertyName { get; } = property.Name;

    public string ColumnName { get; } = property.GetColumnName();

    public string QuotedColumName { get; } = dialect.Quote(property.GetColumnName());

    public string StoreDefinition { get; } = GetStoreDefinition(property);

    public Type ClrType { get; } = property.ClrType;

    public bool IsGenerated { get; } = property.ValueGenerated == ValueGenerated.OnAdd;

    public object? GetValue(object entity, List<IBulkValueConverter>? converters)
    {
        var result = _getter(entity);

        if (converters != null && result != null)
        {
            foreach (var converter in converters)
            {
                if (converter.TryConvertValue(result, out var temp))
                {
                    result = temp;
                    break;
                }
            }
        }

        return result;
    }

    private static PropertyAccessor.Getter<object, object?> BuildGetter(IProperty property)
    {
        var valueConverter =
            property.GetValueConverter() ??
            property.GetTypeMapping().Converter;

        var propInfo = property.PropertyInfo!;

        var actualGetter =
            PropertyAccessor.CreateUntypedGetter(
                propInfo,
                property.DeclaringType.ClrType,
                property.ClrType);

        if (valueConverter == null)
        {
            return actualGetter;
        }

        var converter = valueConverter.ConvertToProvider;

        return source => converter(actualGetter(source));
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
