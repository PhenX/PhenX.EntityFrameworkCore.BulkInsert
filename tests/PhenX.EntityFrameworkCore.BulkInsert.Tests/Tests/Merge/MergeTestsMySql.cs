using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Merge;

[Trait("Category", "MySql")]
[Collection(TestDbContainerMySqlCollection.Name)]
public class MergeTestsMySql(TestDbContainerMySql dbContainer) : MergeTestsBase<TestDbContextMySql>(dbContainer)
{
}
