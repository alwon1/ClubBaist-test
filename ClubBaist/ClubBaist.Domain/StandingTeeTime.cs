namespace ClubBaist.Domain;

public class StandingTeeTime
{
    public Guid StandingTeeTimeId { get; set; } = Guid.NewGuid();
    public Guid SeasonId { get; set; }
    public Season? Season { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int BookingMemberAccountId { get; set; }
    public List<int> PlayerMemberAccountIds { get; set; } = [];
    public StandingTeeTimeStatus Status { get; set; } = StandingTeeTimeStatus.Pending;
    public string? AdminNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
