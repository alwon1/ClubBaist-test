namespace ClubBaist.Domain;

public class Reservation
{
    public Guid ReservationId { get; set; } = Guid.NewGuid();
    public DateOnly SlotDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public Guid BookingMemberAccountId { get; set; }
    public List<Guid> PlayerMemberAccountIds { get; set; } = [];
    public bool IsCancelled { get; set; }
}
