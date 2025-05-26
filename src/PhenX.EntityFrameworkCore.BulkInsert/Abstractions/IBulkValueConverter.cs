using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

/// <summary>
/// Provide an interface to control how objects are written.
/// </summary>
public interface IBulkValueConverter
{
    /// <summary>
    /// Converts a value before written to the output.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="result">The result type.</param>
    /// <param name="options">The options.</param>
    /// <returns>Indicates if an object should be written.</returns>
    bool TryConvertValue(object source, BulkInsertOptions options, out object result);
}
