using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal class BulkInsertOptionsExtension<TProvider> : IDbContextOptionsExtension
    where TProvider : class, IBulkInsertProvider
{
    public DbContextOptionsExtensionInfo Info
        => new BulkInsertOptionsExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
        services.AddSingleton<IBulkInsertProvider, TProvider>();
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private class BulkInsertOptionsExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        /// <inheritdoc />
        public override int GetServiceProviderHashCode() => 0;

        /// <inheritdoc />
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;

        /// <inheritdoc />
        public override bool IsDatabaseProvider => false;

        /// <inheritdoc />
        public override string LogFragment => "BulkInsertOptionsExtension";

        /// <inheritdoc />
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }
    }
}
