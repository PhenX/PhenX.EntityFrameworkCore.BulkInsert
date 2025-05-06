using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using EntityFrameworkCore.ExecuteInsert.Abstractions;
using EntityFrameworkCore.ExecuteInsert.Tests.DbContainer;

using Xunit;

namespace EntityFrameworkCore.ExecuteInsert.Tests.Tests;

public abstract class BulkInsertProviderWithNavigationTestsBase : IAsyncLifetime
{
    protected BulkInsertProviderWithNavigationTestsBase(BulkInsertProviderDbContainer<TestDbContextWithNavigation> dbContainer)
    {
        DbContainer = dbContainer;
    }

    protected BulkInsertProviderDbContainer<TestDbContextWithNavigation> DbContainer { get; }

    [Fact]
    public async Task InsertsEntitiesWithNavigationPropertiesSuccessfully()
    {
        var dbContext = DbContainer.DbContext;

        // Arrange
        var parents = new List<ParentEntity>
        {
            new ParentEntity
            {
                Id = 1,
                Name = "Parent1",
                Children = new List<ChildEntity>
                {
                    new ChildEntity { Id = 1, Name = "Child1" },
                    new ChildEntity { Id = 2, Name = "Child2" }
                },
                GrandParent = new GrandParentEntity { Id = 1, Name = "GrandParent1" }
            },
            new ParentEntity
            {
                Id = 2,
                Name = "Parent2",
                Children = new List<ChildEntity>
                {
                    new ChildEntity { Id = 3, Name = "Child3" }
                },
                GrandParent = new GrandParentEntity { Id = 2, Name = "GrandParent2" }
            }
        };

        // Act
        await dbContext.ParentEntities.ExecuteInsertAsync(parents, o => o.Recursive = true);

        // Assert
        var insertedGrandParents = dbContext.GrandParentEntities.ToList();
        var insertedParents = dbContext.ParentEntities.ToList();
        var insertedChildren = dbContext.ChildEntities.ToList();

        Assert.Equal(2, insertedGrandParents.Count);
        Assert.Equal(2, insertedParents.Count);
        // Assert.Equal(3, insertedChildren.Count);

        Assert.Contains(insertedParents, p => p.Name == "Parent1");
        // Assert.Contains(insertedChildren, c => c.Name == "Child1");
        // Assert.Contains(insertedChildren, c => c.Name == "Child3");
    }

    public Task InitializeAsync() => DbContainer.InitializeAsync();

    public Task DisposeAsync() => DbContainer.DisposeAsync();
}
