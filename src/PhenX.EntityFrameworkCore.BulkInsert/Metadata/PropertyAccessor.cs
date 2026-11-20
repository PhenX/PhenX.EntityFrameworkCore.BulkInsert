using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal static class PropertyAccessor
{
    public static Func<object, object?> CreateGetter(
        PropertyInfo propertyInfo,
        IComplexProperty? complexProperty = null,
        LambdaExpression? converter = null)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        // instance => { }
        var instanceParam = Expression.Parameter(typeof(object), "instance");

        Expression body;

        if (complexProperty == null)
        {
            var propDeclaringType = propertyInfo.DeclaringType!;

            // Convert object to the declaring type
            var typedInstance = GetTypedInstance(propDeclaringType, instanceParam);

            // instance => ((TEntity)instance).Property
            body = Expression.Property(typedInstance, propertyInfo);
        }
        else
        {
            // Nested access: ((TEntity)instance).ComplexProp.Property
            var complexPropInfo = complexProperty.PropertyInfo!;
            var complexPropDeclaringType = complexPropInfo.DeclaringType!;

            var typedInstance = GetTypedInstance(complexPropDeclaringType, instanceParam);

            // instance => ((TEntity)instance).ComplexProp
            Expression complexAccess = Expression.Property(typedInstance, complexPropInfo);

            // instance => ((TEntity)instance).ComplexProp.Property
            body = Expression.Property(complexAccess, propertyInfo);
        }

        // If the converter is provided, we call it
        if (converter != null)
        {
            // Validate the converter input type matches property type
            var converterParamType = converter.Parameters[0].Type;
            if (!converterParamType.IsAssignableFrom(body.Type) && !body.Type.IsAssignableFrom(converterParamType))
            {
                throw new ArgumentException($"Converter input must be assignable from property type ({body.Type} -> {converterParamType})");
            }

            Expression converterInput = body;
            if (converterParamType != body.Type)
            {
                // instance => converter((TConverterType)body)
                converterInput = Expression.Convert(body, converterParamType);
            }

            // instance => converter(body)
            var invokeConverter = Expression.Invoke(converter, converterInput);

            // For nullable types, only call the converter if the value is not null
            if (Nullable.GetUnderlyingType(body.Type) != null)
            {
                // Nullable<> as common result type for the converter and constant 'null'
                var resultType = typeof(Nullable<>).MakeGenericType(invokeConverter.Type);

                // instance => body == null ? null : (Nullable<T>)converter(body)
                var nullCondition = Expression.Equal(body, Expression.Constant(null, body.Type));
                var nullResult = Expression.Constant(null, resultType);
                var nonNullResult = Expression.Convert(invokeConverter, resultType);

                body = Expression.Condition(nullCondition, nullResult, nonNullResult);
            }
            // If the property is a reference type, we need to check for null before calling the converter. even if the
            // property is not nullable
            else if (body.Type.IsClass && !invokeConverter.Type.IsValueType)
            {
                // instance => body == null ? null : converter(body)
                var nullCondition = Expression.Equal(body, Expression.Constant(null, body.Type));
                var nullResult = Expression.Constant(null, invokeConverter.Type);

                body = Expression.Condition(nullCondition, nullResult, invokeConverter);
            }
            else
            {
                body = invokeConverter;
            }
        }

        var finalExpression = body.Type.IsValueType
            ? Expression.Convert(body, typeof(object))
            : body;

        return Expression.Lambda<Func<object, object?>>(finalExpression, instanceParam).Compile();
    }

    private static UnaryExpression GetTypedInstance(Type propDeclaringType, ParameterExpression instanceParam)
    {
        return propDeclaringType.IsValueType
            ? Expression.Unbox(instanceParam, propDeclaringType)
            : Expression.Convert(instanceParam, propDeclaringType);
    }
}
