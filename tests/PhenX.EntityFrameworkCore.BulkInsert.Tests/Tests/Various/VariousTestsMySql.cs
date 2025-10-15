using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Various;

[Trait("Category", "MySql")]
[Collection(TestDbContainerMySqlCollection.Name)]
public class VariousTestsMySql(TestDbContainerMySql dbContainer) : VariousTestsBase<TestDbContextMySql>(dbContainer)
{
}
