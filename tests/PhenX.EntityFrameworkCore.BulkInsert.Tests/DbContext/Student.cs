namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
