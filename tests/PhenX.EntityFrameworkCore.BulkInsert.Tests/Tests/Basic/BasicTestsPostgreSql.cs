using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "PostgreSql")]
public class BasicTestsPostgreSql : BasicTestsBase
{
    public BasicTestsPostgreSql() : base(new TestDbContainerPostgreSql<TestDbContext>())
    {
    }
}
