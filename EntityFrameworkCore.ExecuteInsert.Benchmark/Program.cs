using BenchmarkDotNet.Running;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<BulkInsertVsExecuteInsert>();
    }
}
