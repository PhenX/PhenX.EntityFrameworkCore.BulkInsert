using Ardalis.SmartEnum;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class TestSmartEnum : SmartEnum<TestSmartEnum>
{
    private TestSmartEnum(string name, int value) : base(name, value)
    {
    }

#if NET10_OR_GREATER
    public static readonly TestSmartEnum Value = new TestSmartEnum("test", 1);
#else
    public static readonly new TestSmartEnum Value = new TestSmartEnum("test", 1);
#endif
}
