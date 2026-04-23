using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain2.Entities.Scoring;

[Index(nameof(EffectiveDate), IsUnique = true)]
public class PlayingConditionAdjustment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    public DateOnly EffectiveDate { get; init; }

    [Required]
    [Precision(2, 1)]
    [Range(-1.0, 3.0)]
    public decimal Adjustment { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? EnteredByUserId { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }
}