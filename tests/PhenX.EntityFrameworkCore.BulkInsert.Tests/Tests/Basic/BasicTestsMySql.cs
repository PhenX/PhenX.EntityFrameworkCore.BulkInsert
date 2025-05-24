using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "MySql")]
[Collection(TestDbContainerMySqlCollection.Name)]
public class BasicTestsMySql(TestDbContainerMySql dbContainer) : BasicTestsBase<TestDbContextMySql>(dbContainer)
{
}
