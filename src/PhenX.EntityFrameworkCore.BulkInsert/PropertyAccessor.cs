using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

using RelationalEntityTypeExtensions = Microsoft.EntityFrameworkCore.RelationalEntityTypeExtensions;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal readonly struct PropertyAccessor
{
    private Func<object, object?> EntityToProvider { get; }
    private Action<object, object?> EntityFromProvider { get; }

    public Func<object, object?> ValueToProvider { get; } = v => v;
    public Func<object, object?> ValueFromProvider { get; } = v => v;

    private IPropertyBase Property { get; }

    public Type ProviderClrType { get; }

    public string Name => Property.Name;

    public string ColumnName => Property switch
    {
        IProperty p => p.GetColumnName(),
        INavigation n => n.TargetEntityType.GetContainerColumnName()!,
        _ => string.Empty
    };

    public string? ColumnType => Property switch
    {
        IProperty p => p.GetColumnType(),
        INavigation n => n.TargetEntityType.GetContainerColumnType(),
        _ => string.Empty
    };

    public PropertyAccessor(IProperty property)
    {
        Property = property;

        var propInfo = property.PropertyInfo!;

        var valueConverter = property.GetValueConverter()??
                             property.GetTypeMapping().Converter;

        if (valueConverter != null)
        {
            ProviderClrType = valueConverter.ProviderClrType;

            var toProvider = valueConverter.ConvertToProvider;
            ValueToProvider = toProvider;
            EntityToProvider = v => toProvider(propInfo.GetValue(v));

            var fromProvider = valueConverter.ConvertFromProvider;
            ValueFromProvider = fromProvider;
            EntityFromProvider = (entity, value) => propInfo.SetValue(entity, fromProvider(value));

            return;
        }

        EntityToProvider = propInfo.GetValue;
        EntityFromProvider = propInfo.SetValue;

        ProviderClrType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;
    }

    public PropertyAccessor(INavigation property)
    {
        Property = property;

        ProviderClrType = property.TargetEntityType.ClrType;

        EntityToProvider = property.GetGetter().GetClrValue;
        EntityFromProvider = ((IRuntimePropertyBase)property).MaterializationSetter.SetClrValue;
    }
    //
    // public PropertyAccessor(INavigation jsonNavigation)
    // {
    //     Navigation = jsonNavigation;
    //
    //     var clrType = jsonNavigation.TargetEntityType.ClrType;
    //     ProviderClrType = clrType;
    //
    //     var propName = jsonNavigation.TargetEntityType.GetJsonPropertyName()!;
    //     var propInfo = jsonNavigation.DeclaringEntityType.ClrType.GetProperty(propName)!;
    //
    //     EntityToProvider = v =>
    //     {
    //         var value = propInfo.GetValue(v);
    //         if (value is null or DBNull)
    //         {
    //             return null;
    //         }
    //
    //         var json = JsonSerializer.SerializeToUtf8Bytes(value, clrType);
    //
    //         const byte prefix = 0x01; // JSONB version
    //         var result = new byte[json.Length + 1];
    //         result[0] = prefix;
    //         Buffer.BlockCopy(json, 0, result, 1, json.Length);
    //
    //         return result;
    //     };
    //
    //     EntityFromProvider = (entity, value) =>
    //     {
    //         if (value is null or DBNull)
    //         {
    //             propInfo.SetValue(entity, null);
    //             return;
    //         }
    //
    //         var json = value.ToString()!;
    //         propInfo.SetValue(entity, JsonSerializer.Deserialize(json, clrType));
    //     };
    // }

    public object GetEntityValueToProvider(object entity) => EntityToProvider(entity) ?? DBNull.Value;

    public void SetEntityValueFromProvider(object entity, object? value) => EntityFromProvider(entity, value);
}
