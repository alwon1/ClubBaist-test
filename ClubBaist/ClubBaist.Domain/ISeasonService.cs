namespace ClubBaist.Domain;

public interface ISeasonService
{
    Season? GetSeasonForDate(DateOnly date);
    DateOnly? GetNextAvailableDate(DateOnly from);
}
