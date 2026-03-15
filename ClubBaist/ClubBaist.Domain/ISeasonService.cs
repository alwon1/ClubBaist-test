namespace ClubBaist.Domain;

public interface ISeasonService
{
    Season? GetSeasonForDate(DateOnly date);
}
