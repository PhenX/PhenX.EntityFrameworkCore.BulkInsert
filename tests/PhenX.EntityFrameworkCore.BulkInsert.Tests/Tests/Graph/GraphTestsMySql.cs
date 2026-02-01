using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Graph;

[Trait("Category", "MySql")]
[Collection(TestDbContainerMySqlCollection.Name)]
public class GraphTestsMySql(TestDbContainerMySql dbContainer) : GraphTestsBase<TestDbContextMySql>(dbContainer)
{
}
