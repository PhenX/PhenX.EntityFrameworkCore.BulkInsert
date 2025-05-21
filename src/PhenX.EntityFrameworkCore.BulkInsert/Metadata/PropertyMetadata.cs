using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class PropertyMetadata(IProperty property,  SqlDialectBuilder dialect)
{
    private readonly PropertyAccessor.Getter<object, object?> _getter = BuildGetter(property);

    public string Name { get; } = property.Name;

    public string ColumnName { get; } = property.GetColumnName();

    public string QuotedColumName { get; } = dialect.Quote(property.GetColumnName());

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

        if (valueConverter != null)
        {
            var converter = valueConverter.ConvertToProvider;
            var original = actualGetter;
            actualGetter = source =>
            {
                var value = original(source);

                return converter(value);
            };
        }

        return actualGetter;
    }
}
