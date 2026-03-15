namespace ClubBaist.Domain;

public class Reservation
{
    public Guid ReservationId { get; set; } = Guid.NewGuid();
    public Guid BookingMemberAccountId { get; set; }
    public DateOnly SlotDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;

    public List<Guid> PlayerMemberAccountIds { get; set; } = [];
}
