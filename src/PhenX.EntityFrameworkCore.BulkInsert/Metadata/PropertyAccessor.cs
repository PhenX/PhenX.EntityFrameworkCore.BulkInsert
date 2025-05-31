using System.Linq.Expressions;
using System.Reflection;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal static class PropertyAccessor
{
    public static Func<object, object?> CreateGetter(PropertyInfo propertyInfo, LambdaExpression? converter = null)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);
        var getMethod = propertyInfo.GetMethod ?? throw new ArgumentException("Property does not have a getter.");

        var instanceParam = Expression.Parameter(typeof(object), "instance");

        // Convert object to the declaring type
        Expression typedInstance = propertyInfo.DeclaringType!.IsValueType
            ? Expression.Unbox(instanceParam, propertyInfo.DeclaringType)
            : Expression.Convert(instanceParam, propertyInfo.DeclaringType);

        // Call Getter
        Expression getterCall = Expression.Call(typedInstance, getMethod);

        var propertyType = propertyInfo.PropertyType;

        // If the converter is provided, we call it
        if (converter != null)
        {
            // Validate the converter input type matches property type
            var convIn = converter.Parameters[0].Type;
            if (!convIn.IsAssignableFrom(propertyType) && !propertyType.IsAssignableFrom(convIn))
            {
                throw new ArgumentException($"Converter input must be assignable from property type ({propertyType} -> {convIn})");
            }

            // If property type != converter param, convert
            var converterInput = getterCall;
            if (convIn != propertyType)
            {
                converterInput = Expression.Convert(getterCall, convIn);
            }

            getterCall = Expression.Invoke(converter, converterInput);

            propertyType = getterCall.Type;
        }

        var finalExpr = propertyType.IsValueType
            ? Expression.Convert(getterCall, typeof(object))
            : getterCall;

        return Expression.Lambda<Func<object, object?>>(finalExpr, instanceParam).Compile();
    }
}
