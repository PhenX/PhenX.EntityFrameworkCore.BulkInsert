using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Various;

[Trait("Category", "Oracle")]
[Collection(TestDbContainerOracleCollection.Name)]
public class VariousTestsOracle(TestDbContainerOracle dbContainer) : VariousTestsBase<TestDbContextOracle>(dbContainer)
{
}
