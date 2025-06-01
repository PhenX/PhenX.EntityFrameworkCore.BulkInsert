using System.Linq.Expressions;
using System.Reflection;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, launchCount: 1, warmupCount: 0, iterationCount: 10)]
public partial class GetValueComparator
{
    [Params(1_000_000)] public int N;

    private IReadOnlyList<TestEntity> data = [];

    [IterationSetup]
    public void IterationSetup()
    {
        data = Enumerable.Range(1, N).Select(i => new TestEntity
        {
            Name = $"Entity{i}",
            Price = (decimal)(i * 0.1),
            Identifier = Guid.NewGuid(),
            NumericEnumValue = (NumericEnum)(i % 2),
        }).ToList();
    }

    private static Dictionary<string, Expression<Func<object?, object?>>> Converters = new()
    {
        { nameof(TestEntity.NumericEnumValue), v => (int) v},
    };

    private static readonly PropertyInfo[] PropertyInfos = typeof(TestEntity).GetProperties();

    private static readonly Func<object, object?>[] PropertyInfoGetValueGetters = PropertyInfos
        .Select<PropertyInfo, Func<object, object?>>(propertyInfo =>
        {
            var converter = Converters.TryGetValue(propertyInfo.Name, out var expression)
                ? expression.Compile()
                : null;

            if (converter == null)
            {
                return propertyInfo.GetValue;
            }

            return entity => converter(propertyInfo.GetValue(entity));
        })
        .ToArray();

    private static readonly Func<object, object?>[] PropertyInfoIlGetters = PropertyInfos
        .Select<PropertyInfo, Func<object, object?>>(propertyInfo =>
        {
            var converter = Converters.TryGetValue(propertyInfo.Name, out var expression)
                ? expression.Compile()
                : null;

            var getter = CreateUntypedGetter(propertyInfo, propertyInfo.DeclaringType!, propertyInfo.PropertyType);

            if (converter == null)
            {
                return getter;
            }

            return entity => converter(getter(entity));
        })
        .ToArray();

    private static readonly Func<object, object?>[] PropertyAccessorGetters = PropertyInfos
        .Select<PropertyInfo, Func<object, object?>>(propertyInfo =>
        {
            var converter = Converters.TryGetValue(propertyInfo.Name, out var expression)
                ? expression
                : null;

            return PropertyAccessor.CreateGetter(propertyInfo, converter);
        })
        .ToArray();

    [Benchmark(Baseline = true)]
    public void Native()
    {
        for (var i = 0; i < data.Count; i++)
        {
            var entity = data[i];

            _ = entity.Id;
            _ = entity.Name;
            _ = entity.Price;
            _ = entity.Identifier;
            _ = (int) entity.NumericEnumValue;
            _ = entity.CreatedAt;
            _ = entity.UpdatedAt;
        }
    }

    [Benchmark]
    public void PropertyInfo_GetValue()
    {
        for (var i = 0; i < data.Count; i++)
        {
            var entity = data[i];

            for (var j = 0; j < PropertyInfoGetValueGetters.Length; j++)
            {
                var getter = PropertyInfoGetValueGetters[j];

                _ = getter(entity);
            }
        }
    }

    [Benchmark]
    public void ExpressionTreeGetter()
    {
        for (var i = 0; i < data.Count; i++)
        {
            var entity = data[i];

            for (var j = 0; j < PropertyAccessorGetters.Length; j++)
            {
                var getter = PropertyAccessorGetters[j];

                _ = getter(entity);
            }
        }
    }

    [Benchmark]
    public void IlGetter()
    {
        for (var i = 0; i < data.Count; i++)
        {
            var entity = data[i];

            for (var j = 0; j < PropertyInfoIlGetters.Length; j++)
            {
                var getter = PropertyInfoIlGetters[j];

                _ = getter(entity);
            }
        }
    }
}
