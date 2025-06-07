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
        Expression getterExpression = Expression.Call(typedInstance, getMethod);

        var propertyType = propertyInfo.PropertyType;

        // If the converter is provided, we call it
        if (converter != null)
        {
            // Validate the converter input type matches property type
            var converterParamType = converter.Parameters[0].Type;
            if (!converterParamType.IsAssignableFrom(propertyType) &&
                !propertyType.IsAssignableFrom(converterParamType))
            {
                throw new ArgumentException(
                    $"Converter input must be assignable from property type ({propertyType} -> {converterParamType})");
            }

            // If property type != converter param, convert
            var converterInput = getterExpression;
            if (converterParamType != propertyType)
            {
                converterInput = Expression.Convert(getterExpression, converterParamType);
            }

            var invokeConverter = Expression.Invoke(converter, converterInput);

            if (propertyType.IsClass)
            {
                var nullCondition = Expression.Equal(getterExpression, Expression.Constant(null, propertyType));
                var nullResult = Expression.Constant(null, converter.ReturnType);
                getterExpression = Expression.Condition(nullCondition, nullResult, invokeConverter);
            }
            else
            {
                getterExpression = invokeConverter;
            }

            propertyType = getterExpression.Type;
        }

        var finalExpression = propertyType.IsValueType
            ? Expression.Convert(getterExpression, typeof(object))
            : getterExpression;

        return Expression.Lambda<Func<object, object?>>(finalExpression, instanceParam).Compile();
    }
}
