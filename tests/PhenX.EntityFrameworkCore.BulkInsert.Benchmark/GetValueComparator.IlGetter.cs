using System.Reflection;
using System.Reflection.Emit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public partial class GetValueComparator
{
    public static Func<object, object?> CreateUntypedGetter(PropertyInfo propertyInfo, Type sourceType, Type valueType)
    {
        var method =
            typeof(GetValueComparator).GetMethod(nameof(CreateInternalUntypedGetter), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(sourceType, valueType);

        return (Func<object, object?>)method.Invoke(null, [propertyInfo])!;
    }

    private static Func<object, object?> CreateInternalUntypedGetter<TSource, TValue>(PropertyInfo propertyInfo)
    {
        var getter = CreateGetter<TSource, TValue>(propertyInfo);

        return source => getter((TSource)source!);
    }

    public static Func<TSource, TValue> CreateGetter<TSource, TValue>(PropertyInfo propertyInfo)
    {
        if (!propertyInfo.CanRead)
        {
            return x => throw new NotSupportedException();
        }

        var bakingField =
            propertyInfo.DeclaringType!.GetField($"<{propertyInfo.Name}>k__BackingField",
                BindingFlags.NonPublic |
                BindingFlags.Instance);

        var propertyGetMethod = propertyInfo.GetGetMethod()!;

        var getMethod = new DynamicMethod(propertyGetMethod.Name, typeof(TValue), [typeof(TSource)], true);
        var getGenerator = getMethod.GetILGenerator();

        // Load this to stack.
        getGenerator.Emit(OpCodes.Ldarg_0);

        if (bakingField != null && !propertyGetMethod.IsVirtual)
        {
            // Get field directly.
            getGenerator.Emit(OpCodes.Ldfld, bakingField);
        }
        else if (propertyGetMethod.IsVirtual)
        {
            // Call the virtual property.
            getGenerator.Emit(OpCodes.Callvirt, propertyGetMethod);
        }
        else
        {
            // Call the non virtual property.
            getGenerator.Emit(OpCodes.Call, propertyGetMethod);
        }

        getGenerator.Emit(OpCodes.Ret);

        return getMethod.CreateDelegate<Func<TSource, TValue>>();
    }
}
