namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
