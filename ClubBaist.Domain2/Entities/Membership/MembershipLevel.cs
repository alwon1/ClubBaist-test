using System.ComponentModel.DataAnnotations;

namespace ClubBaist.Domain2;

public class MembershipLevel
{
    [Key]
    public int Id { get; init; }
    [MaxLength(5)]
    public string ShortCode { get; set; } = "M"!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

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
