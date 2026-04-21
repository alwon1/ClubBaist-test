using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain;

[Index(nameof(BookingMemberId))]
[Index(nameof(RequestedDayOfWeek), nameof(RequestedTime))]
public class StandingTeeTime
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    [ForeignKey(nameof(BookingMember))]
    public int BookingMemberId { get; init; }

    [Required]
    public required MemberShipInfo BookingMember { get; init; }

    [Required]
    public DayOfWeek RequestedDayOfWeek { get; set; }

    [Required]
    public TimeOnly RequestedTime { get; set; }

    [Range(0, 120)]
    public int ToleranceMinutes { get; set; } = 30;

    [Required]
    public DateOnly StartDate { get; init; }

    [Required]
    public DateOnly EndDate { get; init; }

    public int? PriorityNumber { get; set; }

    public TimeOnly? ApprovedTime { get; set; }

    [Required]
    public StandingTeeTimeStatus Status { get; set; } = StandingTeeTimeStatus.Draft;

    public List<MemberShipInfo> AdditionalParticipants { get; set; } = new(3);

    [NotMapped]
    public int ParticipantCount => 1 + AdditionalParticipants.Count;

    [NotMapped]
    public IReadOnlyList<MemberShipInfo> Participants => [BookingMember, .. AdditionalParticipants];

    [NotMapped]
    public List<TeeTimeBooking> GeneratedBookings => field ??= new();
}

public enum StandingTeeTimeStatus
{
    Draft = 0,
    Approved = 1,
    Allocated = 2,
    Unallocated = 3,
    Cancelled = 4,
    Denied = 5
}
