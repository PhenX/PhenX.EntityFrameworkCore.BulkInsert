using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.ExecuteInsert.Abstractions;

public static class BulkInsertExtensions
{
    public static async Task ExecuteInsert<T>(this DbSet<T> dbSet, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        var context = dbSet.GetDbContext();
        var provider = context.GetService<IBulkInsertProvider>();

        await provider.BulkInsertAsync(context, entities, cancellationToken: cancellationToken);
    }

    public static async Task ExecuteInsert<T>(this DbContext dbContext, IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        var dbSet = dbContext.Set<T>();
        if (dbSet == null)
        {
            throw new InvalidOperationException($"DbSet of type {typeof(T).Name} not found in DbContext.");
        }

        await dbSet.ExecuteInsert(entities, cancellationToken);
    }

    private static DbContext GetDbContext<T>(this DbSet<T> dbSet) where T : class
    {
        IInfrastructure<IServiceProvider> infrastructure = dbSet;
        return (infrastructure.Instance.GetService(typeof(ICurrentDbContext)) as ICurrentDbContext)!.Context;
    }
}
