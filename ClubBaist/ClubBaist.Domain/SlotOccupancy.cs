namespace ClubBaist.Domain;

public class SlotOccupancy
{
    public const int MaxCapacity = 4;

    public DateOnly SlotDate { get; set; }
    public TimeOnly SlotTime { get; set; }
    public int ReservedPlayers { get; set; }
}
