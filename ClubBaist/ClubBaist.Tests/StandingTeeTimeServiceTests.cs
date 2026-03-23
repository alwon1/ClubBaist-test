using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class StandingTeeTimeServiceTests
{
    private static readonly DayOfWeek TestDayOfWeek = DayOfWeek.Monday;
    private static readonly TimeOnly SlotTime = new(10, 0);

    [TestMethod]
    public async Task RequestAsync_ValidRequest_ReturnsPendingStandingTeeTime()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var bookingMember = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player1 = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player3 = await CreateMemberAsync(provider, MembershipCategory.Social);

        var result = await sttService.RequestAsync(seasonId, TestDayOfWeek, SlotTime, bookingMember, [player1, player2, player3]);

        Assert.IsNotNull(result);
        Assert.AreEqual(StandingTeeTimeStatus.Pending, result.Status);
    }

    [TestMethod]
    public async Task RequestAsync_DuplicatePlayerIds_ReturnsNull()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var bookingMember = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player1 = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Social);

        // player1 appears twice — IDs are not distinct
        var result = await sttService.RequestAsync(seasonId, TestDayOfWeek, SlotTime, bookingMember, [player1, player2, player1]);

        Assert.IsNull(result, "RequestAsync should return null when player IDs are not distinct");
    }

    [TestMethod]
    public async Task RequestAsync_PlayerIdMatchesBookingMember_ReturnsNull()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var bookingMember = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player1 = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Social);

        // bookingMember is also listed as a player
        var result = await sttService.RequestAsync(seasonId, TestDayOfWeek, SlotTime, bookingMember, [player1, player2, bookingMember]);

        Assert.IsNull(result, "RequestAsync should return null when a player ID matches the booking member ID");
    }

    [TestMethod]
    public async Task RequestAsync_WrongPlayerCount_ReturnsNull()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var bookingMember = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player1 = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Social);

        var result = await sttService.RequestAsync(seasonId, TestDayOfWeek, SlotTime, bookingMember, [player1, player2]);

        Assert.IsNull(result, "RequestAsync should return null when fewer than 3 player IDs are provided");
    }

    [TestMethod]
    public async Task RequestAsync_NonShareholderBookingMember_ReturnsNull()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var bookingMember = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player1 = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Social);
        var player3 = await CreateMemberAsync(provider, MembershipCategory.Social);

        var result = await sttService.RequestAsync(seasonId, TestDayOfWeek, SlotTime, bookingMember, [player1, player2, player3]);

        Assert.IsNull(result, "RequestAsync should return null when the booking member is not a Shareholder");
    }

    /// <summary>Creates a test scope with a seeded season, returning the season ID via out parameter.</summary>
    private static IServiceScope CreateScopeWithSeason(out Guid seasonId)
    {
        var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var id = Guid.NewGuid();
        dbContext.Seasons.Add(new Season
        {
            SeasonId = id,
            Name = $"Test Season {id:N}",
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2026, 9, 30),
            SeasonStatus = SeasonStatus.Active
        });
        dbContext.SaveChanges();

        seasonId = id;
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
