using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Issue63;

[Collection(TestDbContainerSqliteCollection.Name)]
public class Issue63TestsSqlite(TestDbContainerSqlite dbContainer)
    : Issue63TestsBase<TestDbContextSqlite>(dbContainer);