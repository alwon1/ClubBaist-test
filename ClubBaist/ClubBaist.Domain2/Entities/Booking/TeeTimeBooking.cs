using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public int ParticipantCount { get; private set; }

    public List<MemberShipInfo> AdditionalParticipants { get; set; } = new(3);

    public IReadOnlyList<MemberShipInfo> Participants => [BookingMember, ..AdditionalParticipants];
}
