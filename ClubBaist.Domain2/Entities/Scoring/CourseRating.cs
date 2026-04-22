using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain2.Entities.Scoring;

public enum TeeColor
{
    Red = 0,
    White = 1,
    Blue = 2
}

public enum Gender
{
    Male = 0,
    Female = 1
}

/// <summary>
/// Reference data for course and slope ratings by tee color and gender.
/// </summary>
[Index(nameof(TeeColor), nameof(Gender), IsUnique = true)]
public class CourseRating
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    public TeeColor TeeColor { get; init; }

    [Required]
    public Gender Gender { get; init; }

    [Required]
    [Precision(4, 1)]
    [Column("CourseRating")]
    public decimal Rating { get; init; }

    [Required]
    public int SlopeRating { get; init; }

    [MaxLength(512)]
    public string? Notes { get; init; }
}