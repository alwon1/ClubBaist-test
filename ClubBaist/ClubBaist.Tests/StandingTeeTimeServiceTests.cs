using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class StandingTeeTimeServiceTests
{
    // Short season with exactly two Mondays: 2026-04-06 and 2026-04-13.
    private static readonly DateOnly SeasonStart = new(2026, 4, 6);
    private static readonly DateOnly SeasonEnd   = new(2026, 4, 19);
    private static readonly DayOfWeek SlotDay    = DayOfWeek.Monday;
    private static readonly TimeOnly   SlotTime  = new(9, 0);

    // -------------------------------------------------------------------------
    // RequestAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Request_Shareholder_ThreePlayers_ReturnsPendingStt()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var result = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);

        Assert.IsNotNull(result, "Request should succeed for a Shareholder with 3 players");
        Assert.AreEqual(StandingTeeTimeStatus.Pending, result.Status);
        Assert.AreEqual(season.SeasonId, result.SeasonId);
        Assert.AreEqual(SlotDay, result.DayOfWeek);
        Assert.AreEqual(SlotTime, result.SlotTime);
        Assert.AreEqual(booker, result.BookingMemberAccountId);
        Assert.HasCount(3, result.PlayerMemberAccountIds);
    }

    [TestMethod]
    public async Task Request_NonShareholder_ReturnsNull()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Social);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var result = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);

        Assert.IsNull(result, "Only Shareholders may submit a standing tee time request");
    }

    [TestMethod]
    public async Task Request_WrongPlayerCount_ReturnsNull()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 2); // must be exactly 3
        var season  = await dbContext.Seasons.FirstAsync();

        var result = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);

        Assert.IsNull(result, "Exactly 3 players are required for a standing tee time");
    }

    [TestMethod]
    public async Task Request_DuplicatePending_ReturnsNull()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var first = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(first, "First request should succeed");

        var second = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNull(second, "Duplicate request while Pending should be rejected");
    }

    [TestMethod]
    public async Task Request_AllowedAfterDenied()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var first = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(first);
        await sttService.DenyAsync(first.StandingTeeTimeId);

        // After denial the duplicate guard no longer blocks a new request
        var second = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(second, "A new request should be permitted after the previous one was denied");
        Assert.AreEqual(StandingTeeTimeStatus.Pending, second.Status);
    }

    // -------------------------------------------------------------------------
    // ApproveAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Approve_PendingStt_SetsApprovedAndCreatesReservations()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var stt = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(stt);

        var reservationIds = await sttService.ApproveAsync(stt.StandingTeeTimeId);

        // Season Apr 6–19 with DayOfWeek.Monday → Apr 6 and Apr 13 = 2 dates
        Assert.HasCount(2, reservationIds, "Should create one reservation per matching weekday in the season");

        var approved = await dbContext.StandingTeeTimes.FirstAsync(s => s.StandingTeeTimeId == stt.StandingTeeTimeId);
        Assert.AreEqual(StandingTeeTimeStatus.Approved, approved.Status);

        var reservations = await dbContext.Reservations
            .Where(r => r.StandingTeeTimeId == stt.StandingTeeTimeId && !r.IsCancelled)
            .ToListAsync();
        Assert.HasCount(2, reservations);
        Assert.IsTrue(reservations.All(r => r.SlotTime == SlotTime));
        Assert.IsTrue(reservations.All(r => r.SlotDate.DayOfWeek == SlotDay));
    }

    [TestMethod]
    public async Task Approve_NonPendingStt_ReturnsEmptyList()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var stt = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(stt);

        // Deny so the STT is no longer Pending
        await sttService.DenyAsync(stt.StandingTeeTimeId);

        var reservationIds = await sttService.ApproveAsync(stt.StandingTeeTimeId);
        Assert.IsEmpty(reservationIds, "Approving a non-Pending STT should return an empty list");
    }

    // -------------------------------------------------------------------------
    // DenyAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Deny_PendingStt_SetsDeniedAndReturnsTrue()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var stt = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(stt);

        var result = await sttService.DenyAsync(stt.StandingTeeTimeId);

        Assert.IsTrue(result, "Denying a Pending STT should return true");

        var denied = await dbContext.StandingTeeTimes.FirstAsync(s => s.StandingTeeTimeId == stt.StandingTeeTimeId);
        Assert.AreEqual(StandingTeeTimeStatus.Denied, denied.Status);
    }

    [TestMethod]
    public async Task Deny_NonPendingStt_ReturnsFalse()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var stt = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(stt);

        // Approve first so it is no longer Pending
        await sttService.ApproveAsync(stt.StandingTeeTimeId);

        var result = await sttService.DenyAsync(stt.StandingTeeTimeId);
        Assert.IsFalse(result, "Denying a non-Pending STT should return false");
    }

    // -------------------------------------------------------------------------
    // CancelAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Cancel_ApprovedStt_SetsCancelledAndCancelsFutureReservations()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var stt = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(stt);
        await sttService.ApproveAsync(stt.StandingTeeTimeId);

        var result = await sttService.CancelAsync(stt.StandingTeeTimeId);

        Assert.IsTrue(result, "Cancelling an Approved STT should return true");

        var cancelled = await dbContext.StandingTeeTimes.FirstAsync(s => s.StandingTeeTimeId == stt.StandingTeeTimeId);
        Assert.AreEqual(StandingTeeTimeStatus.Cancelled, cancelled.Status);

        var futureReservations = await dbContext.Reservations
            .Where(r => r.StandingTeeTimeId == stt.StandingTeeTimeId && !r.IsCancelled)
            .ToListAsync();
        Assert.IsEmpty(futureReservations, "All future reservations linked to the STT should be cancelled");
    }

    [TestMethod]
    public async Task Cancel_NonApprovedStt_ReturnsFalse()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        var stt = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(stt);
        // STT is still Pending — cancel should be rejected

        var result = await sttService.CancelAsync(stt.StandingTeeTimeId);
        Assert.IsFalse(result, "Cancelling a non-Approved STT should return false");
    }

    // -------------------------------------------------------------------------
    // GenerateReservationsForSeasonAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GenerateReservations_CreatesReservationsForAllApprovedStts()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        // Seed an Approved STT directly (bypassing RequestAsync) so no reservations
        // exist yet — simulating the start-of-season generation workflow.
        var stt = new StandingTeeTime
        {
            SeasonId                = season.SeasonId,
            DayOfWeek               = SlotDay,
            SlotTime                = SlotTime,
            BookingMemberAccountId  = booker,
            PlayerMemberAccountIds  = players,
            Status                  = StandingTeeTimeStatus.Approved,
        };
        dbContext.StandingTeeTimes.Add(stt);
        await dbContext.SaveChangesAsync();

        var result = await sttService.GenerateReservationsForSeasonAsync(season.SeasonId);

        Assert.HasCount(1, result, "Result should contain an entry for each Approved STT");
        Assert.IsTrue(result.ContainsKey(stt.StandingTeeTimeId));

        var ids = result[stt.StandingTeeTimeId];
        // Season Apr 6–19 with DayOfWeek.Monday → Apr 6 and Apr 13 = 2 reservations
        Assert.HasCount(2, ids, "Should generate one reservation per matching weekday");

        var reservations = await dbContext.Reservations
            .Where(r => r.StandingTeeTimeId == stt.StandingTeeTimeId && !r.IsCancelled)
            .ToListAsync();
        Assert.HasCount(2, reservations);
    }

    [TestMethod]
    public async Task GenerateReservations_PendingSttsAreSkipped()
    {
        using var scope = CreateScopeWithSeason();
        var provider   = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();
        var dbContext  = provider.GetRequiredService<ApplicationDbContext>();

        var booker  = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var players = await CreatePlayersAsync(provider, 3);
        var season  = await dbContext.Seasons.FirstAsync();

        // Pending STT — should not have reservations generated
        var stt = await sttService.RequestAsync(season.SeasonId, SlotDay, SlotTime, booker, players);
        Assert.IsNotNull(stt);

        var result = await sttService.GenerateReservationsForSeasonAsync(season.SeasonId);

        Assert.IsEmpty(result, "Pending STTs should not have reservations generated");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IServiceScope CreateScopeWithSeason()
    {
        var scope     = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Seasons.Add(new Season
        {
            SeasonId     = Guid.NewGuid(),
            Name         = $"STT Test Season {Guid.NewGuid():N}",
            StartDate    = SeasonStart,
            EndDate      = SeasonEnd,
            SeasonStatus = SeasonStatus.Active,
        });
        dbContext.SaveChanges();

        return scope;
    }

    private static Task<int> CreateMemberAsync(IServiceProvider provider, MembershipCategory category) =>
        TestDataFactory.CreateMemberAsync(
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<ApplicationDbContext>(),
            category);

    private static async Task<List<int>> CreatePlayersAsync(IServiceProvider provider, int count)
    {
        var ids = new List<int>(count);
        for (var i = 0; i < count; i++)
            ids.Add(await CreateMemberAsync(provider, MembershipCategory.Shareholder));
        return ids;
    }
}
