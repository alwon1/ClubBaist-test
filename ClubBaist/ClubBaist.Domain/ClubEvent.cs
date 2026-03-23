namespace ClubBaist.Domain;

public class ClubEvent
{
    public Guid ClubEventId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public bool BlocksTeeSheet { get; set; }
    public string? Description { get; set; }
}
