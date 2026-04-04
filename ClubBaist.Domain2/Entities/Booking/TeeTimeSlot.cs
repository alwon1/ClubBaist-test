using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain2;

[Index(nameof(Start))]
public class TeeTimeSlot
{
    [Key]
    [Required]
    public DateTime Start { get; init; }

    [Required]
    public TimeSpan Duration { get; init; }

    [Required]
    public int SeasonId { get; init; }

    [ForeignKey(nameof(SeasonId))]
    public Season Season { get; init; } = default!;

    public List<TeeTimeBooking> Bookings => field ??= new();

    [NotMapped]
    public int BookedSpots => Bookings.Sum(b => b.ParticipantCount);
}
