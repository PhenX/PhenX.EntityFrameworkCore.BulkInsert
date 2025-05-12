using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkRunner.Run<LibComparatorPostgreSql>(config);
        BenchmarkRunner.Run<LibComparatorSqlServer>(config);
        BenchmarkRunner.Run<LibComparatorSqlite>(config);
    }
}
