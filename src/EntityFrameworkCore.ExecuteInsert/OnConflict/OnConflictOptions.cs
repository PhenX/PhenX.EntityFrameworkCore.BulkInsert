using System.Linq.Expressions;

namespace EntityFrameworkCore.ExecuteInsert.OnConflict;

public abstract class OnConflictOptions
{
    public string? Condition {get; set; }
}

public class OnConflictOptions<T> : OnConflictOptions
{
    public Expression<Func<T, object>>? Match { get; set; }
    public Expression<Func<T, object>>? Update { get; set; }
}
