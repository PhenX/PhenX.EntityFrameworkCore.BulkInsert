using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert;

public readonly struct PropertyAccessor
{
    private Func<object, object?> ValueGetter { get; }

    private IProperty Property { get; }

    public Type ProviderClrType { get; }

    public string Name => Property.Name;

    public PropertyAccessor(IProperty property)
    {
        Property = property;

        var propInfo = property.PropertyInfo!;

        var valueConverter = property.GetValueConverter()??
                             property.GetTypeMapping().Converter;

        if (valueConverter != null)
        {
            var conv = valueConverter.ConvertToProvider;
            ValueGetter = v => conv(propInfo.GetValue(v));
            ProviderClrType = valueConverter.ProviderClrType;
            return;
        }

        ValueGetter = propInfo.GetValue;
        ProviderClrType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;
    }

    public object GetValue(object entity) => ValueGetter(entity) ?? DBNull.Value;
}
