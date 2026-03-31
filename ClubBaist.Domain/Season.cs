namespace ClubBaist.Domain;

public class Season
{
    public Guid SeasonId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SeasonStatus SeasonStatus { get; set; } = SeasonStatus.Planned;
}
