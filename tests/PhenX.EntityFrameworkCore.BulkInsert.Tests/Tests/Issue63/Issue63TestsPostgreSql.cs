using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Issue63;

[Collection(TestDbContainerPostgreSqlCollection.Name)]
public class Issue63TestsPostgreSql(TestDbContainerPostgreSql dbContainer)
    : Issue63TestsBase<TestDbContextPostgreSql>(dbContainer);