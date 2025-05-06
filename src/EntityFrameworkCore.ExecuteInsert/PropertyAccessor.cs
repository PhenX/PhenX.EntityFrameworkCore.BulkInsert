using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert;

public class PropertyAccessor
{
    private Func<object, object?> ValueGetter { get; set; }

    public IProperty Property { get; }

    public string Name => Property.Name;

    public Type ProviderClrType { get; private set; }

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

    public virtual object GetValue(object entity) => ValueGetter(entity) ?? DBNull.Value;
}
