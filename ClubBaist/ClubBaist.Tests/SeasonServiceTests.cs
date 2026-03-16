using ClubBaist.Domain;
using ClubBaist.Services;

namespace ClubBaist.Tests;

[TestClass]
public sealed class SeasonServiceTests
{
    [TestMethod]
    public void GetNextAvailableDate_DateWithinSeason_ReturnsDate()
    {
        var service = new SeasonService([ActiveSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30))]);

        var result = service.GetNextAvailableDate(new DateOnly(2026, 6, 15));

        Assert.AreEqual(new DateOnly(2026, 6, 15), result);
    }

    [TestMethod]
    public void GetNextAvailableDate_DateOnSeasonStart_ReturnsDate()
    {
        var service = new SeasonService([ActiveSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30))]);

        var result = service.GetNextAvailableDate(new DateOnly(2026, 4, 1));

        Assert.AreEqual(new DateOnly(2026, 4, 1), result);
    }

    [TestMethod]
    public void GetNextAvailableDate_DateOnSeasonEnd_ReturnsDate()
    {
        var service = new SeasonService([ActiveSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30))]);

        var result = service.GetNextAvailableDate(new DateOnly(2026, 9, 30));

        Assert.AreEqual(new DateOnly(2026, 9, 30), result, "Season end date itself should be available");
    }

    [TestMethod]
    public void GetNextAvailableDate_BeforeSeasonStart_ReturnsSeasonStart()
    {
        var service = new SeasonService([ActiveSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30))]);

        var result = service.GetNextAvailableDate(new DateOnly(2026, 1, 15));

        Assert.AreEqual(new DateOnly(2026, 4, 1), result);
    }

    [TestMethod]
    public void GetNextAvailableDate_AfterAllSeasons_ReturnsNull()
    {
        var service = new SeasonService([ActiveSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30))]);

        var result = service.GetNextAvailableDate(new DateOnly(2026, 12, 1));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetNextAvailableDate_NoSeasons_ReturnsNull()
    {
        var service = new SeasonService([]);

        var result = service.GetNextAvailableDate(new DateOnly(2026, 6, 15));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetNextAvailableDate_BetweenTwoSeasons_ReturnsNextSeasonStart()
    {
        var service = new SeasonService([
            ActiveSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 9, 30)),
            PlannedSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30))
        ]);

        // Between seasons (Nov 2025) — should jump to 2026 season start
        var result = service.GetNextAvailableDate(new DateOnly(2025, 11, 1));

        Assert.AreEqual(new DateOnly(2026, 4, 1), result);
    }

    [TestMethod]
    public void GetNextAvailableDate_MultipleSeasonsDateInFirst_ReturnsDate()
    {
        var service = new SeasonService([
            ActiveSeason(new DateOnly(2025, 4, 1), new DateOnly(2025, 9, 30)),
            PlannedSeason(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30))
        ]);

        var result = service.GetNextAvailableDate(new DateOnly(2025, 6, 15));

        Assert.AreEqual(new DateOnly(2025, 6, 15), result);
    }

    private static Season ActiveSeason(DateOnly start, DateOnly end) => new()
    {
        SeasonId = Guid.NewGuid(),
        Name = $"Season {start.Year}",
        StartDate = start,
        EndDate = end,
        SeasonStatus = SeasonStatus.Active
    };

    private static Season PlannedSeason(DateOnly start, DateOnly end) => new()
    {
        SeasonId = Guid.NewGuid(),
        Name = $"Season {start.Year}",
        StartDate = start,
        EndDate = end,
        SeasonStatus = SeasonStatus.Planned
    };
}
