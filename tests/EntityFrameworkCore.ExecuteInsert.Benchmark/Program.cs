using BenchmarkDotNet.Running;

namespace EntityFrameworkCore.ExecuteInsert.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<BulkInsertVsExecuteInsertPostgreSql>();
        BenchmarkRunner.Run<BulkInsertVsExecuteInsertSqlServer>();
        BenchmarkRunner.Run<BulkInsertVsExecuteInsertSqlite>();
    }
}
