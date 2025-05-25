using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

using PhenX.EntityFrameworkCore.BulkInsert.Benchmark.Providers;

namespace PhenX.EntityFrameworkCore.BulkInsert.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        var config = ManualConfig
            .Create(DefaultConfig.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        var comparators = new[]
        {
            typeof(LibComparatorOracle),
            // typeof(LibComparatorMySql),
            // typeof(LibComparatorPostgreSql),
            // typeof(LibComparatorSqlite),
            // typeof(LibComparatorSqlServer),
        };

        BenchmarkRunner.Run(comparators, config);
    }
}
