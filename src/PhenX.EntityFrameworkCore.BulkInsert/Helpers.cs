using System.Text;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal static class Helpers
{
    private static readonly Random Random = new();

    public static StringBuilder AppendJoin<T>(this StringBuilder sb, string separator, IEnumerable<T> items, Action<StringBuilder, T> formatter)
    {
        var first = true;
        foreach (var item in items)
        {
            if (!first)
            {
                sb.Append(separator);
            }

            formatter(sb, item);
            first = false;
        }

        return sb;
    }

    public static StringBuilder AppendColumns(this StringBuilder sb, IReadOnlyList<ColumnMetadata> columns)
    {
        return sb.AppendJoin(", ", columns.Select(c => c.QuotedColumName));
    }

    /// <summary>
    /// Generates a random alphanumeric string of the specified length.
    /// </summary>
    public static string RandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";

        var sb = new StringBuilder(length);

        for (var i = 0; i < length; i++)
        {
            sb.Append(chars[Random.Next(chars.Length)]);
        }

        return sb.ToString();
    }
}
