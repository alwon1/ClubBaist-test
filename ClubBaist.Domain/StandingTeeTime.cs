namespace ClubBaist.Domain;

public class StandingTeeTime
{
    public Guid StandingTeeTimeId { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int BookingMemberAccountId { get; set; }
    public List<int> PlayerMemberAccountIds { get; set; } = []; // 3 players; total foursome = 4
    public StandingTeeTimeStatus Status { get; set; } = StandingTeeTimeStatus.Pending;
}
