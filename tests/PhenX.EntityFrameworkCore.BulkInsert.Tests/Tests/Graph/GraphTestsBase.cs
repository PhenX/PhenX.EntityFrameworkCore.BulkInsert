using FluentAssertions;

using PhenX.EntityFrameworkCore.BulkInsert.Extensions;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContainer;
using PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

using Xunit;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.Tests.Graph;

public abstract class GraphTestsBase<TDbContext>(TestDbContainer dbContainer) : IAsyncLifetime
    where TDbContext : TestDbContext, new()
{
    private readonly Guid _run = Guid.NewGuid();
    private TDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = await dbContainer.CreateContextAsync<TDbContext>("graph");
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [SkippableFact]
    public async Task InsertGraph_OneToMany_InsertsParentAndChildren()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_Blog1",
            Posts = new List<Post>
            {
                new Post { TestRun = _run, Title = $"{_run}_Post1" },
                new Post { TestRun = _run, Title = $"{_run}_Post2" },
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();
        insertedBlog!.Id.Should().BeGreaterThan(0);

        var insertedPosts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        insertedPosts.Should().HaveCount(2);
        insertedPosts.Should().AllSatisfy(p =>
        {
            p.Id.Should().BeGreaterThan(0);
            p.BlogId.Should().Be(insertedBlog.Id);
        });
    }

    [SkippableFact]
    public async Task InsertGraph_OneToOne_InsertsRelatedEntity()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithSettings",
            Settings = new BlogSettings
            {
                TestRun = _run,
                EnableComments = true
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();
        insertedBlog!.Id.Should().BeGreaterThan(0);

        var insertedSettings = _context.BlogSettings.FirstOrDefault(s => s.TestRun == _run);
        insertedSettings.Should().NotBeNull();
        insertedSettings!.Id.Should().BeGreaterThan(0);
        insertedSettings.BlogId.Should().Be(insertedBlog.Id);
        insertedSettings.EnableComments.Should().BeTrue();
    }

    [SkippableFact]
    public async Task InsertGraph_PropagatesGeneratedIds()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithIdPropagation",
            Posts = new List<Post>
            {
                new Post { TestRun = _run, Title = $"{_run}_PostWithIdPropagation" }
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert - the original entities should have their IDs populated
        blog.Id.Should().BeGreaterThan(0, "Blog ID should be propagated");
        blog.Posts.First().Id.Should().BeGreaterThan(0, "Post ID should be propagated");
        blog.Posts.First().BlogId.Should().Be(blog.Id, "Post BlogId should reference the Blog");
    }

    [SkippableFact]
    public async Task InsertGraph_EmptyCollections_DoesNotFail()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithNoPosts",
            Posts = new List<Post>() // Empty collection
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();
        insertedBlog!.Id.Should().BeGreaterThan(0);

        var posts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        posts.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task InsertGraph_NullNavigations_DoesNotFail()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithNullSettings",
            Settings = null // Null navigation
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();
        insertedBlog!.Id.Should().BeGreaterThan(0);

        var settings = _context.BlogSettings.FirstOrDefault(s => s.TestRun == _run);
        settings.Should().BeNull();
    }

    [SkippableFact]
    public async Task InsertGraph_DeepGraph_RespectsMaxDepth()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithDeepGraph",
            Posts = new List<Post>
            {
                new Post
                {
                    TestRun = _run,
                    Title = $"{_run}_DeepPost",
                    Tags = new List<Tag>
                    {
                        new Tag { TestRun = _run, Name = $"{_run}_DeepTag" }
                    }
                }
            }
        };

        // Act - MaxGraphDepth = 1 means only Blog and Posts, not Tags
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
            options.MaxGraphDepth = 1;
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();

        var insertedPosts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        insertedPosts.Should().HaveCount(1);

        // Tags should NOT be inserted due to MaxGraphDepth
        var insertedTags = _context.Tags.Where(t => t.TestRun == _run).ToList();
        insertedTags.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task InsertGraph_WithExcludeNavigations_SkipsSpecified()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithExcludedPosts",
            Posts = new List<Post>
            {
                new Post { TestRun = _run, Title = $"{_run}_ExcludedPost" }
            },
            Settings = new BlogSettings
            {
                TestRun = _run,
                EnableComments = false
            }
        };

        // Act - Exclude Posts navigation
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
            options.ExcludeNavigations = ["Posts"];
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();

        // Posts should NOT be inserted due to ExcludeNavigations
        var insertedPosts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        insertedPosts.Should().BeEmpty();

        // Settings should still be inserted
        var insertedSettings = _context.BlogSettings.FirstOrDefault(s => s.TestRun == _run);
        insertedSettings.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task InsertGraph_WithIncludeNavigations_OnlyInsertsSpecified()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithIncludedPostsOnly",
            Posts = new List<Post>
            {
                new Post { TestRun = _run, Title = $"{_run}_IncludedPost" }
            },
            Settings = new BlogSettings
            {
                TestRun = _run,
                EnableComments = true
            }
        };

        // Act - Only include Posts navigation
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
            options.IncludeNavigations = ["Posts"];
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();

        // Posts should be inserted
        var insertedPosts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        insertedPosts.Should().HaveCount(1);

        // Settings should NOT be inserted due to IncludeNavigations
        var insertedSettings = _context.BlogSettings.FirstOrDefault(s => s.TestRun == _run);
        insertedSettings.Should().BeNull();
    }

    [SkippableFact]
    public async Task InsertGraph_MultipleRootEntities_InsertsAll()
    {
        // Arrange
        var blogs = new[]
        {
            new Blog
            {
                TestRun = _run,
                Name = $"{_run}_MultiBlog1",
                Posts = new List<Post>
                {
                    new Post { TestRun = _run, Title = $"{_run}_Multi1Post1" }
                }
            },
            new Blog
            {
                TestRun = _run,
                Name = $"{_run}_MultiBlog2",
                Posts = new List<Post>
                {
                    new Post { TestRun = _run, Title = $"{_run}_Multi2Post1" },
                    new Post { TestRun = _run, Title = $"{_run}_Multi2Post2" }
                }
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(blogs, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert
        var insertedBlogs = _context.Blogs.Where(b => b.TestRun == _run).ToList();
        insertedBlogs.Should().HaveCount(2);
        insertedBlogs.Should().AllSatisfy(b => b.Id.Should().BeGreaterThan(0));

        var insertedPosts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        insertedPosts.Should().HaveCount(3);
    }

    [SkippableFact]
    public async Task InsertGraph_SyncVariant_Works()
    {
        // Arrange
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_SyncBlog",
            Posts = new List<Post>
            {
                new Post { TestRun = _run, Title = $"{_run}_SyncPost" }
            }
        };

        // Act (synchronous)
        _context.ExecuteBulkInsert(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();

        var insertedPosts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        insertedPosts.Should().HaveCount(1);
    }

    [SkippableFact]
    public async Task InsertGraph_SelfReferencing_InsertsRootOnly()
    {
        // Arrange - Self-referencing hierarchies require multiple inserts in order
        // This test verifies that root entities without parents can be inserted
        var rootCategory = new Category
        {
            TestRun = _run,
            Name = $"{_run}_RootCategory",
            Parent = null,
            ParentId = null
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { rootCategory }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert
        var insertedCategories = _context.Categories.Where(c => c.TestRun == _run).ToList();
        insertedCategories.Should().HaveCount(1);

        var insertedRoot = insertedCategories.First();
        insertedRoot.Id.Should().BeGreaterThan(0);
        insertedRoot.ParentId.Should().BeNull();
    }

    [SkippableFact]
    public async Task InsertGraph_ManyToMany_TraversesRelatedEntities()
    {
        // Note: Many-to-many join table insertion requires explicit join entity types.
        // Dictionary<string, object> join entities are not supported by the bulk insert infrastructure.
        // This test verifies that many-to-many navigations are traversed and related entities are collected.

        // Arrange - Create a post with tags
        var tag1 = new Tag { TestRun = _run, Name = $"{_run}_Tag1" };
        var tag2 = new Tag { TestRun = _run, Name = $"{_run}_Tag2" };

        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_BlogWithTaggedPost",
            Posts = new List<Post>
            {
                new Post
                {
                    TestRun = _run,
                    Title = $"{_run}_TaggedPost",
                    Tags = new List<Tag> { tag1, tag2 }
                }
            }
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert - Verify the entities were inserted (even if join table wasn't populated)
        var insertedBlog = _context.Blogs.FirstOrDefault(b => b.TestRun == _run);
        insertedBlog.Should().NotBeNull();
        insertedBlog!.Id.Should().BeGreaterThan(0);

        var insertedPost = _context.Posts.FirstOrDefault(p => p.TestRun == _run);
        insertedPost.Should().NotBeNull();
        insertedPost!.Id.Should().BeGreaterThan(0);

        // Tags should be inserted as they were traversed via many-to-many navigation
        var insertedTags = _context.Tags.Where(t => t.TestRun == _run).ToList();
        insertedTags.Should().HaveCount(2);
        insertedTags.Should().AllSatisfy(t => t.Id.Should().BeGreaterThan(0));
    }
}
