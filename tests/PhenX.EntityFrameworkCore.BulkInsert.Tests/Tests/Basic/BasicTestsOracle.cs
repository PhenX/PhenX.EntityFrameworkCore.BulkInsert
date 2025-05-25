using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Basic;

[Trait("Category", "Oracle")]
[Collection(TestDbContainerOracleCollection.Name)]
public class BasicTestsOracle(TestDbContainerOracle dbContainer) : BasicTestsBase<TestDbContextOracle>(dbContainer)
{
}
