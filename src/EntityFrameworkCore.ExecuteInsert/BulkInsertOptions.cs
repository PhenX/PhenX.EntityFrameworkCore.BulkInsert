namespace EntityFrameworkCore.ExecuteInsert;

public class BulkInsertOptions
{
    public bool OnlyRootEntities { get; set; } = false;

    public bool ReturnIdentity { get; set; } = false;
}
