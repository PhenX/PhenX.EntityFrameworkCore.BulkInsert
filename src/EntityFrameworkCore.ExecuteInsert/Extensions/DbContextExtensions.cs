using System.Data;
using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.ExecuteInsert.Extensions;

public static class DbContextExtensions
{
    /// <summary>
    /// Gets cached properties for an entity type, using reflection if not already cached.
    /// </summary>
    public static IProperty[] GetProperties(this DbContext context, Type entityType, bool includeGenerated = true)
    {
        var entityTypeInfo = context.Model.FindEntityType(entityType) ?? throw new InvalidOperationException($"Could not determine entity type for type {entityType.Name}");

        return entityTypeInfo
            .GetProperties()
            .Where(p => !p.IsShadowProperty() && (includeGenerated || p.ValueGenerated != ValueGenerated.OnAdd))
            .ToArray();
    }

    public static async Task<(DbConnection connection, bool wasClosed)> GetConnection(this DbContext context, CancellationToken ctk = default)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(ctk);
        }

        return (connection, wasClosed);
    }
}
