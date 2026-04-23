using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClubBaist.Domain2.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain2.Entities.Scoring;

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
    public GolfRound.TeeColor TeeColor { get; init; }

    [Required]
    public Gender Gender { get; init; }

    [Required]
    [Precision(4, 1)]
    [Column("CourseRating")]
    public decimal Rating { get; init; }

    [Required]
    public int SlopeRating { get; init; }

    [MaxLength(500)]
    public string? Source { get; init; }
}