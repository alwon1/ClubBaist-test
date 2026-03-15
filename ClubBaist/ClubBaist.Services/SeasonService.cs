using ClubBaist.Domain;

namespace ClubBaist.Services;

/// <summary>
/// Singleton service that holds the seasons loaded at application startup.
/// Includes Active and Planned seasons so members can book into upcoming seasons.
/// Restart the application to pick up season changes.
/// </summary>
public class SeasonService : ISeasonService
{
    private readonly IReadOnlyList<Season> _seasons;

    public SeasonService(IReadOnlyList<Season> seasons)
    {
        _seasons = seasons;
    }

    public Season? GetSeasonForDate(DateOnly date) =>
        _seasons.FirstOrDefault(s => s.StartDate <= date && s.EndDate >= date);
}
