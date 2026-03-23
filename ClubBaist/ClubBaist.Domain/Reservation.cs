namespace ClubBaist.Domain;

public class Reservation
{
    public Guid ReservationId { get; set; } = Guid.NewGuid();
    public DateOnly SlotDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int BookingMemberAccountId { get; set; }
    public List<int> PlayerMemberAccountIds { get; set; } = [];
    public bool IsCancelled { get; set; }
    public Guid? StandingTeeTimeId { get; set; }
}
