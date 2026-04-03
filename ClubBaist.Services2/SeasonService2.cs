using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services2;

/// <summary>
/// Operating hours configuration for a single day.
/// Used by <see cref="SeasonService2.GenerateSlots"/> to bound slot generation.
///
/// Future: replace the hardcoded default with a per-season, per-day-of-week
/// <c>SeasonOperatingHours</c> entity loaded from the database. The
/// <see cref="GenerateSlots"/> signature accepts this dictionary already,
/// so no changes to slot generation logic will be needed.
/// </summary>
public sealed record OperatingHours(TimeOnly Open, TimeOnly Close)
{
    public static readonly OperatingHours Default = new(new TimeOnly(7, 0), new TimeOnly(19, 0));

    public static IReadOnlyDictionary<DayOfWeek, OperatingHours> AllDaysDefault() =>
        Enum.GetValues<DayOfWeek>().ToDictionary(d => d, _ => Default);
}

public class SeasonService2(IAppDbContext2 db)
{
    /// <summary>
    /// Creates a new season and pre-populates all <see cref="TeeTimeSlot"/> rows for the date range.
    /// Slot times are generated using default operating hours (07:00–19:00 every day).
    /// </summary>
    public Task<Season> CreateSeasonAsync(
        string name,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default) =>
        CreateSeasonAsync(name, start, end, OperatingHours.AllDaysDefault(), cancellationToken);

    /// <summary>
    /// Creates a new season and pre-populates all <see cref="TeeTimeSlot"/> rows using the
    /// supplied per-day-of-week operating hours.
    /// </summary>
    public async Task<Season> CreateSeasonAsync(
        string name,
        DateOnly start,
        DateOnly end,
        IReadOnlyDictionary<DayOfWeek, OperatingHours> operatingHours,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operatingHours);

        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot, cancellationToken);
            try
            {
                var season = new Season { Name = name, StartDate = start, EndDate = end };
                db.Seasons.Add(season);
                await db.SaveChangesAsync(cancellationToken);

                var slots = GenerateSlots(season, operatingHours).ToList();
                await db.TeeTimeSlots.AddRangeAsync(slots, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return season;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <summary>
    /// Generates all tee-time slots for a season using the supplied operating hours.
    /// Slots are 15-minute windows with alternating 7-minute / 8-minute durations starting
    /// from <see cref="OperatingHours.Open"/> up to (but not exceeding) <see cref="OperatingHours.Close"/>.
    /// </summary>
    public static IEnumerable<TeeTimeSlot> GenerateSlots(
        Season season,
        IReadOnlyDictionary<DayOfWeek, OperatingHours> operatingHours)
    {
        for (var date = season.StartDate; date <= season.EndDate; date = date.AddDays(1))
        {
            if (!operatingHours.TryGetValue(date.DayOfWeek, out var hours))
                hours = OperatingHours.Default;

            var open = date.ToDateTime(hours.Open);
            var close = date.ToDateTime(hours.Close);

            // Slots sit in 15-minute windows; slot n within a window has:
            //   offset = (n / 2) * 15 + (n % 2) * 7  minutes from open
            //   duration = n % 2 == 0 ? 7 min : 8 min
            // This gives two non-overlapping slots per 15-minute window.
            for (var n = 0; ; n++)
            {
                var offsetMinutes = (n / 2) * 15 + (n % 2) * 7;
                var slotStart = open.AddMinutes(offsetMinutes);

                if (slotStart >= close)
                    break;

                var duration = TimeSpan.FromMinutes(n % 2 == 0 ? 7 : 8);

                yield return new TeeTimeSlot
                {
                    Start = slotStart,
                    Duration = duration,
                    SeasonId = season.Id
                };
            }
        }
    }
}
