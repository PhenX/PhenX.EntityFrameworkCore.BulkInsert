namespace PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

/// <summary>
/// Provide an interface to control how objects are written.
/// </summary>
public interface IValueConverter
{
    /// <summary>
    /// Converts a value before written to the output.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="result">The result type.</param>
    /// <returns>Indicates if an object should be written.</returns>
    bool TryConvertValue(object source, out object result);
}
