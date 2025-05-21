using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using PhenX.EntityFrameworkCore.BulkInsert.Dialect;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

internal class MySqlServerDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "`";

    protected override string CloseDelimiter => "`";

    protected override bool SupportsMoveRows => false;

    protected override void AppendConflictCondition<T>(StringBuilder sql, OnConflictOptions<T> onConflictTyped)
    {
        throw new NotSupportedException("Conflict conditions are not supported in MYSQL");
    }

    protected override void AppendOnConflictUpdate(StringBuilder sql, IEnumerable<string> updates)
    {
        sql.AppendLine("UPDATE");

        var i = 0;
        foreach (var update in updates)
        {
            if (i > 0)
            {
                sql.Append(", ");
            }

            sql.Append(update);
            i++;
        }
    }

    protected override void AppendOnConflictStatement(StringBuilder sql)
    {
        sql.Append("ON DUPLICATE KEY");
    }

    protected override void AppendDoNothing(StringBuilder sql, IProperty[] insertedProperties)
    {
        var columnName = insertedProperties[0].GetColumnName();

        sql.Append($"UPDATE {Quote(columnName)} = {GetExcludedColumnName(columnName)}");
    }

    protected override string GetExcludedColumnName(string columnName)
    {
        return $"VALUES({Quote(columnName)})";
    }
}
