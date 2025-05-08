using EntityFrameworkCore.ExecuteInsert.OnConflict;

namespace EntityFrameworkCore.ExecuteInsert;

public class BulkInsertOptions
{
    public bool Recursive { get; set; }

    public bool MoveRows { get; set; }
}
