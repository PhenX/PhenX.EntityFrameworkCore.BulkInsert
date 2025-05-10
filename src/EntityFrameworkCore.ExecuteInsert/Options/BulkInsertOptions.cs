namespace EntityFrameworkCore.ExecuteInsert.Options;

public class BulkInsertOptions
{
    public bool Recursive { get; set; }

    public bool MoveRows { get; set; }

    public int? BatchSize { get; set; }
}
