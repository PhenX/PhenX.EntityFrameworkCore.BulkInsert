using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Graph;

[Trait("Category", "Oracle")]
[Collection(TestDbContainerOracleCollection.Name)]
public class GraphTestsOracle(TestDbContainerOracle dbContainer) : GraphTestsBase<TestDbContextOracle>(dbContainer)
{
}
