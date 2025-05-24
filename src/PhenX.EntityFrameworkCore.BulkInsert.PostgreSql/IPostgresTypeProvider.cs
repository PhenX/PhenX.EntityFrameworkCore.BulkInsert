using Microsoft.EntityFrameworkCore.Metadata;

using NpgsqlTypes;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

/// <summary>
/// Provides the type to write.
/// </summary>
public interface IPostgresTypeProvider
{
    /// <summary>
    /// Gets the type of a value before written to the output.
    /// </summary>
    /// <param name="property">The source property.</param>
    /// <param name="result">The result type.</param>
    /// <returns>Indicates if an object should be written.</returns>
    bool TryGetType(IProperty property, out NpgsqlDbType result);
}
