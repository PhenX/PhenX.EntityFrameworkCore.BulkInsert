using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

    public Type? ProviderClrType { get; } = property.GetProviderClrType();

    public bool IsGenerated { get; } = property.ValueGenerated == ValueGenerated.OnAdd;

    public object? GetValue(object entity)
    {
        return _getter(entity!);
    }

    private static PropertyAccessor.Getter<object, object?> BuildGetter(IProperty property)
    {
        var valueConverter =
            property.GetValueConverter() ??
            property.GetTypeMapping().Converter;

        var actualGetter =
            PropertyAccessor.CreateUntypedGetter(
                property.PropertyInfo!,
                property.DeclaringType.ClrType,
                property.ClrType);

        var result = actualGetter;
        if (valueConverter != null)
        {
            var converter = valueConverter.ConvertToProvider;

            result = source =>
            {
                var value = actualGetter(source);

                return converter(value);
            };
        }

        return result;
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
