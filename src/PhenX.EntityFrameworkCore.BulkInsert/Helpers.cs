using System.Text;

using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal static class Helpers
{
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
}
