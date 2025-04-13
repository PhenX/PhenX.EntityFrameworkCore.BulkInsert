using EntityFrameworkCore.ExecuteInsert.Abstractions;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.ExecuteInsert;

public class ExecuteInsertOptionsExtension<TProvider> : IDbContextOptionsExtension
    where TProvider : class, IBulkInsertProvider
{
    // Required: Provide metadata about the extension
    public DbContextOptionsExtensionInfo Info
        => new ExecuteInsertOptionsExtensionInfo(this);

    // Register services with EF Core's internal service provider
    public void ApplyServices(IServiceCollection services)
    {
        services.AddSingleton<IBulkInsertProvider, TProvider>();
    }

    // Validate configuration (throw if invalid)
    public void Validate(IDbContextOptions options)
    {
    }

    private class ExecuteInsertOptionsExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExecuteInsertOptionsExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension) { }

        /// <inheritdoc />
        public override int GetServiceProviderHashCode() => 0;

        /// <inheritdoc />
        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;

        /// <inheritdoc />
        public override bool IsDatabaseProvider => false;

        /// <inheritdoc />
        public override string LogFragment => "MyCustomExtension";

        /// <inheritdoc />
        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }
    }
}
