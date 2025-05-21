using PhenX.EntityFrameworkCore.BulkInsert.Dialect;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

internal class MySqlServerDialectBuilder : SqlDialectBuilder
{
    protected override string OpenDelimiter => "`";

    protected override string CloseDelimiter => "`";

    protected override bool SupportsMoveRows => false;

    public override bool SupportsReturning => false;
}
