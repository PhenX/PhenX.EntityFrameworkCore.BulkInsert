using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Merge;

[Trait("Category", "Oracle")]
[Collection(TestDbContainerOracleCollection.Name)]
public class MergeTestsOracle(TestDbContainerOracle dbContainer) : MergeTestsBase<TestDbContextOracle>(dbContainer)
{
}
