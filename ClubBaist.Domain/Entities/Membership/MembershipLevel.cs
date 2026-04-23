using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubBaist.Domain;

public enum MemberType
{
    Shareholder,
    Associate
}

public class MembershipLevel
{
    [Key]
    public int Id { get; init; }
    [MaxLength(5)]
    public string ShortCode { get; set; } = "M"!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

    public MemberType MemberType { get; set; } = MemberType.Associate;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AnnualFee { get; set; }

    public int? MinimumAge { get; set; }

    public int? MaximumAge { get; set; }

    public List<MembershipLevelTeeTimeAvailability> Availabilities { get; } = new();
}

public class MembershipLevelTeeTimeAvailability
{
    [Key]
    public int Id { get; init; }

    [Required]
    public required MembershipLevel MembershipLevel { get; init; }

    [Required]
    public DayOfWeek DayOfWeek { get; init; }

    [Required]
    public TimeOnly StartTime { get; init; }

    [Required]
    public TimeOnly EndTime { get; init; }
}
