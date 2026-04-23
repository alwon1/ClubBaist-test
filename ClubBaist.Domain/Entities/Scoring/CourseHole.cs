using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain.Entities.Scoring;

[Index(nameof(TeeColor), nameof(HoleNumber), IsUnique = true)]
public class CourseHole
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    public GolfRound.TeeColor TeeColor { get; init; }

    [Required]
    [Range(1, 18)]
    public int HoleNumber { get; init; }

    [Required]
    [Range(3, 6)]
    public int Par { get; init; }

    [Required]
    [Range(1, 18)]
    public int StrokeIndex { get; init; }

    [MaxLength(500)]
    public string? Source { get; init; }
}