using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

/// <summary>
/// Test entity with column names containing spaces and SQL reserved keywords.
/// This is used to test that column names are properly quoted in SQL statements.
/// </summary>
[PrimaryKey(nameof(Id))]
[Index(nameof(BusinessFunctionText), IsUnique = true)]
[Table("test_entity_special_columns")]
public class TestEntityWithSpecialColumnNames : TestEntityBase
{
    public int Id { get; set; }

    /// <summary>
    /// Column name with spaces and SQL reserved keyword "Function".
    /// </summary>
    [Column("Business Function Text")]
    [MaxLength(255)]
    public string BusinessFunctionText { get; set; } = string.Empty;

    /// <summary>
    /// Column name with SQL reserved keyword "Order".
    /// </summary>
    [Column("Order Number")]
    public int OrderNumber { get; set; }

    /// <summary>
    /// Regular column name for comparison.
    /// </summary>
    [Column("description")]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
