using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal sealed class ColumnMetadata(IProperty property,  SqlDialectBuilder dialect)
{
    private readonly Func<object, object?> _getter = BuildGetter(property);

    public IProperty Property { get; } = property;

    public string PropertyName { get; } = property.Name;

    public string ColumnName { get; } = property.GetColumnName();

    public string QuotedColumName { get; } = dialect.Quote(property.GetColumnName());

    public string StoreDefinition { get; } = GetStoreDefinition(property);

    public Type ClrType { get; } = property.ClrType;

    public bool IsGenerated { get; } = property.ValueGenerated != ValueGenerated.Never;

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

    private static Func<object, object?> BuildGetter(IProperty property)
    {
        var valueConverter =
            property.GetValueConverter() ??
            property.GetTypeMapping().Converter;

        var propInfo = property.PropertyInfo!;

        return PropertyAccessor.CreateGetter(propInfo, valueConverter?.ConvertToProviderExpression);
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
