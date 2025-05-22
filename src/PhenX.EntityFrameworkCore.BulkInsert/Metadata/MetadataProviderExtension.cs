using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace PhenX.EntityFrameworkCore.BulkInsert.Metadata;

internal class MetadataProviderExtension : IDbContextOptionsExtension
{
    public DbContextOptionsExtensionInfo Info
    => new MetadataProviderExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
        services.AddSingleton<MetadataProvider>();
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private class MetadataProviderExtensionInfo : DbContextOptionsExtensionInfo
    {
        public MetadataProviderExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension) { }

        /// <inheritdoc />
        public override int GetServiceProviderHashCode() => 0;

        /// <inheritdoc />
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;

        /// <inheritdoc />
        public override bool IsDatabaseProvider => false;

        /// <inheritdoc />
        public override string LogFragment => "MetadataProviderExtension";

        /// <inheritdoc />
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }
    }
}
