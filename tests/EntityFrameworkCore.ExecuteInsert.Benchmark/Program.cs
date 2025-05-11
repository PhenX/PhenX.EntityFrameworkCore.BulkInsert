using BenchmarkDotNet.Running;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<LibComparatorPostgreSql>();
        BenchmarkRunner.Run<LibComparatorSqlServer>();
        BenchmarkRunner.Run<LibComparatorSqlite>();
    }
}
