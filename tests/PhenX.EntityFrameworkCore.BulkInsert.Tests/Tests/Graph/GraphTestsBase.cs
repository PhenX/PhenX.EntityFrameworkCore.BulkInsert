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

    [SkippableFact]
    public async Task InsertGraph_OriginalEntitiesLinked_WithGeneratedKeys()
    {
        // Arrange - Keep references to original entities
        var post1 = new Post { TestRun = _run, Title = $"{_run}_LinkedPost1" };
        var post2 = new Post { TestRun = _run, Title = $"{_run}_LinkedPost2" };
        var settings = new BlogSettings { TestRun = _run, EnableComments = true };
        var blog = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_LinkedBlog",
            Posts = new List<Post> { post1, post2 },
            Settings = settings
        };

        // Act
        await _context.ExecuteBulkInsertAsync(new[] { blog }, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert - Verify the original entity references are the same objects
        // and have their generated IDs populated
        blog.Id.Should().BeGreaterThan(0, "Blog should have generated ID populated");
        post1.Id.Should().BeGreaterThan(0, "Post1 should have generated ID populated");
        post2.Id.Should().BeGreaterThan(0, "Post2 should have generated ID populated");
        settings.Id.Should().BeGreaterThan(0, "Settings should have generated ID populated");

        // Verify FK values are propagated
        post1.BlogId.Should().Be(blog.Id, "Post1.BlogId should reference the Blog");
        post2.BlogId.Should().Be(blog.Id, "Post2.BlogId should reference the Blog");
        settings.BlogId.Should().Be(blog.Id, "Settings.BlogId should reference the Blog");

        // Verify the same objects are in the collections
        blog.Posts.Should().Contain(post1, "Original post1 reference should still be in the collection");
        blog.Posts.Should().Contain(post2, "Original post2 reference should still be in the collection");
        blog.Settings.Should().BeSameAs(settings, "Original settings reference should still be assigned");

        // Verify data matches what's in the database
        var dbBlog = _context.Blogs.FirstOrDefault(b => b.Id == blog.Id);
        dbBlog.Should().NotBeNull();
        dbBlog!.Name.Should().Be(blog.Name);

        var dbPosts = _context.Posts.Where(p => p.BlogId == blog.Id).ToList();
        dbPosts.Should().HaveCount(2);
        dbPosts.Select(p => p.Id).Should().Contain(post1.Id);
        dbPosts.Select(p => p.Id).Should().Contain(post2.Id);
    }

    [SkippableFact]
    public async Task InsertGraph_OriginalEntitiesLinked_WithClientGeneratedKeys()
    {
        // Arrange - Create entities with pre-set GUIDs (client-generated keys)
        // Using TestEntityWithGuidId which has ValueGeneratedNever()
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var entity1 = new TestEntityWithGuidId
        {
            TestRun = _run,
            Id = guid1,
            Name = $"{_run}_ClientGenKey1"
        };
        var entity2 = new TestEntityWithGuidId
        {
            TestRun = _run,
            Id = guid2,
            Name = $"{_run}_ClientGenKey2"
        };

        // Act - Insert without graph (since these don't have navigations)
        // but test that client-generated keys are preserved
        await _context.ExecuteBulkInsertAsync(new[] { entity1, entity2 }, options =>
        {
            // No IncludeGraph needed since no navigations
        });

        // Assert - Verify the original entity references maintain their IDs
        entity1.Id.Should().Be(guid1, "Entity1 should retain its client-generated ID");
        entity2.Id.Should().Be(guid2, "Entity2 should retain its client-generated ID");

        // Verify data is in database with the same IDs
        var dbEntity1 = _context.TestEntitiesWithGuidId.FirstOrDefault(e => e.Id == guid1);
        var dbEntity2 = _context.TestEntitiesWithGuidId.FirstOrDefault(e => e.Id == guid2);

        dbEntity1.Should().NotBeNull();
        dbEntity2.Should().NotBeNull();
        dbEntity1!.Name.Should().Be(entity1.Name);
        dbEntity2!.Name.Should().Be(entity2.Name);
    }

    [SkippableFact]
    public async Task InsertGraph_MultipleRootEntities_OriginalEntitiesLinked()
    {
        // Arrange - Multiple root entities with children, keep all references
        var post1 = new Post { TestRun = _run, Title = $"{_run}_Multi1Post1" };
        var post2 = new Post { TestRun = _run, Title = $"{_run}_Multi2Post1" };
        var post3 = new Post { TestRun = _run, Title = $"{_run}_Multi2Post2" };

        var blog1 = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_MultiBlogLinked1",
            Posts = new List<Post> { post1 }
        };
        var blog2 = new Blog
        {
            TestRun = _run,
            Name = $"{_run}_MultiBlogLinked2",
            Posts = new List<Post> { post2, post3 }
        };

        var blogs = new[] { blog1, blog2 };

        // Act
        await _context.ExecuteBulkInsertAsync(blogs, options =>
        {
            options.IncludeGraph = true;
        });

        // Assert - All original entities should have IDs and be linked correctly
        blog1.Id.Should().BeGreaterThan(0);
        blog2.Id.Should().BeGreaterThan(0);
        blog1.Id.Should().NotBe(blog2.Id, "Different blogs should have different IDs");

        post1.Id.Should().BeGreaterThan(0);
        post2.Id.Should().BeGreaterThan(0);
        post3.Id.Should().BeGreaterThan(0);
        post1.Id.Should().NotBe(post2.Id);
        post2.Id.Should().NotBe(post3.Id);

        // Verify FK relationships
        post1.BlogId.Should().Be(blog1.Id);
        post2.BlogId.Should().Be(blog2.Id);
        post3.BlogId.Should().Be(blog2.Id);

        // Verify original objects are still in collections
        blog1.Posts.Should().Contain(post1);
        blog2.Posts.Should().Contain(post2);
        blog2.Posts.Should().Contain(post3);

        // Verify database state matches
        var dbBlogs = _context.Blogs.Where(b => b.TestRun == _run).ToList();
        dbBlogs.Should().HaveCount(2);
        dbBlogs.Select(b => b.Id).Should().Contain(blog1.Id);
        dbBlogs.Select(b => b.Id).Should().Contain(blog2.Id);

        var dbPosts = _context.Posts.Where(p => p.TestRun == _run).ToList();
        dbPosts.Should().HaveCount(3);
        dbPosts.Select(p => p.Id).Should().Contain(post1.Id);
        dbPosts.Select(p => p.Id).Should().Contain(post2.Id);
        dbPosts.Select(p => p.Id).Should().Contain(post3.Id);
    }
}
