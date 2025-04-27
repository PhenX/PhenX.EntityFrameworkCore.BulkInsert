using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.PostgreSql;

internal class PropertyAccessor
{
    private Func<object, object?> ValueGetter { get; set; }

    public IProperty Property { get; }

    public string Name => Property.Name;

    public PropertyAccessor(IProperty property)
    {
        Property = property;

        var propInfo = property.PropertyInfo!;

        var valueConverter = property.GetValueConverter()?.ConvertToProvider ??
                             property.GetTypeMapping().Converter?.ConvertToProvider;

        if (valueConverter != null)
        {
            ValueGetter = v => valueConverter(propInfo.GetValue(v));
            return;
        }

        ValueGetter = propInfo.GetValue;
    }

    public virtual object GetValue(object entity) => ValueGetter(entity) ?? DBNull.Value;
}
