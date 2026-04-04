using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ClubBaist.Domain2.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain2;

[Index(nameof(TeeTimeSlotStart))]
[Index(nameof(BookingMemberId))]
[Index(nameof(TeeTimeSlotStart), nameof(BookingMemberId), IsUnique = true)]
public class TeeTimeBooking
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    [Required]
    [ForeignKey(nameof(TeeTimeSlot))]
    public DateTime TeeTimeSlotStart { get; init; }
    [Required]
    public required TeeTimeSlot TeeTimeSlot { get; init; }
    [Required]
    [ForeignKey(nameof(BookingMember))]
    public int BookingMemberId { get; init; }

    [Required]
    public required MemberShipInfo BookingMember { get; init; }

    public int? StandingTeeTimeId { get; set; }

    [ForeignKey(nameof(StandingTeeTimeId))]
    public StandingTeeTime? StandingTeeTime { get; set; }

    [NotMapped]
    public int ParticipantCount => 1 + AdditionalParticipants.Count;

    public List<BookingParticipant> AdditionalParticipants { get; set; } = new(3);

    [NotMapped]
    public IReadOnlyList<int> ParticipantIds => [BookingMemberId, ..AdditionalParticipants.Select(participant => participant.Id)];
}

[Owned]
public class BookingParticipant
{
    public int Id { get; init; }

    public MemberShipInfo? Member { get; init; }

    public static BookingParticipant FromMember(MemberShipInfo member) => new()
    {
        Id = member.Id,
        Member = member
    };
}
