using Ardalis.SmartEnum;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestSmartEnum : SmartEnum<TestSmartEnum>
{
    private TestSmartEnum(string name, int value) : base(name, value)
    {
    }

    public static readonly TestSmartEnum Value = new TestSmartEnum("test", 1);
}
