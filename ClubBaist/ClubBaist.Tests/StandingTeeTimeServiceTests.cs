using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class StandingTeeTimeServiceTests
{
    private static readonly TimeOnly SlotTime = new(9, 0);

    /// <summary>
    /// When a standing tee time is approved mid-season (the season's start date is in the
    /// past), <see cref="StandingTeeTimeService{TKey}.ApproveAsync"/> must not create
    /// reservations for dates that have already passed.
    /// </summary>
    [TestMethod]
    public async Task ApproveAsync_MidSeason_DoesNotCreatePastReservations()
    {
        // Arrange: season that started well in the past so today falls inside it.
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seasonStart = today.AddDays(-30);
        var seasonEnd = today.AddDays(60);

        // Pick a day-of-week guaranteed to match at least one date in [seasonStart, seasonEnd].
        var dayOfWeek = today.DayOfWeek;

        using var scope = CreateScopeWithSeason(seasonStart, seasonEnd);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var season = await dbContext.Seasons.FirstAsync();
        var booker = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player3 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Request and then approve the standing tee time.
        var stt = await sttService.RequestAsync(
            season.SeasonId, dayOfWeek, SlotTime,
            booker, [player1, player2, player3]);

        Assert.IsNotNull(stt, "RequestAsync should succeed for a Shareholder");

        var reservationIds = await sttService.ApproveAsync(stt.StandingTeeTimeId);

        // Act: retrieve created reservations.
        var reservations = await dbContext.Reservations
            .Where(r => r.StandingTeeTimeId == stt.StandingTeeTimeId && !r.IsCancelled)
            .ToListAsync();

        // Assert: every reservation must be on or after today.
        Assert.IsTrue(
            reservations.All(r => r.SlotDate >= today),
            $"Expected all reservations on or after {today}, but found past dates: " +
            string.Join(", ", reservations.Where(r => r.SlotDate < today).Select(r => r.SlotDate)));
    }

    /// <summary>
    /// When the season hasn't started yet every occurrence from season start should be created.
    /// </summary>
    [TestMethod]
    public async Task ApproveAsync_FutureSeason_CreatesReservationsFromSeasonStart()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var seasonStart = today.AddDays(7);
        var seasonEnd = today.AddDays(7 + 28); // 4 weeks of future season

        // Use the day-of-week that matches seasonStart so we know at least one slot is created.
        var dayOfWeek = seasonStart.DayOfWeek;

        using var scope = CreateScopeWithSeason(seasonStart, seasonEnd);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var season = await dbContext.Seasons.FirstAsync();
        var booker = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player3 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        var stt = await sttService.RequestAsync(
            season.SeasonId, dayOfWeek, SlotTime,
            booker, [player1, player2, player3]);

        Assert.IsNotNull(stt, "RequestAsync should succeed for a Shareholder");

        await sttService.ApproveAsync(stt.StandingTeeTimeId);

        var reservations = await dbContext.Reservations
            .Where(r => r.StandingTeeTimeId == stt.StandingTeeTimeId && !r.IsCancelled)
            .ToListAsync();

        Assert.IsNotEmpty(reservations, "Should have created at least one reservation");
        Assert.IsTrue(
            reservations.All(r => r.SlotDate >= seasonStart),
            "All reservations should fall on or after season start");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IServiceScope CreateScopeWithSeason(DateOnly start, DateOnly end)
    {
        var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Seasons.Add(new Season
        {
            SeasonId = Guid.NewGuid(),
            Name = $"Test Season {Guid.NewGuid():N}",
            StartDate = start,
            EndDate = end,
            SeasonStatus = SeasonStatus.Active,
        });
        dbContext.SaveChanges();

        return scope;
    }

    private static Task<int> CreateMemberAsync(
        IServiceProvider provider,
        MembershipCategory category) =>
        TestDataFactory.CreateMemberAsync(
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<ApplicationDbContext>(),
            category);
}
