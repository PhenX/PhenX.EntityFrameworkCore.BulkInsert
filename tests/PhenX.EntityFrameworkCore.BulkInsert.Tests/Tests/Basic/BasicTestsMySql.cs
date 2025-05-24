using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "MySql")]
public class BasicTestsMySql(TestDbContainerMySql<TestDbContext> dbContainer) : BasicTestsBase<TestDbContainerMySql<TestDbContext>>(dbContainer)
{
}
