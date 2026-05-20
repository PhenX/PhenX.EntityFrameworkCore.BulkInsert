using OpenTelemetry.Trace;
namespace PhenX.EntityFrameworkCore.BulkInsert.OpenTelemetry;

/// <summary>
/// Extension methods for <see cref="TracerProviderBuilder"/> to add instrumentation for PhenX.EntityFrameworkCore.BulkInsert.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds instrumentation for PhenX.EntityFrameworkCore.BulkInsert to the OpenTelemetry TracerProviderBuilder.
    /// </summary>
    /// <param name="builder">The TracerProviderBuilder to add the instrumentation to.</param>
    /// <returns>The TracerProviderBuilder with the PhenX.EntityFrameworkCore.BulkInsert instrumentation added.</returns>
    public static TracerProviderBuilder AddPhenXEntityFrameworkCoreBulkInsertInstrumentation(this TracerProviderBuilder builder)
    {
        return builder.AddSource("PhenX.EntityFrameworkCore.BulkInsert");
    }
}
