using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class StandingTeeTimeServiceTests
{
    private static readonly TimeOnly SlotTime = new(10, 0);

    [TestMethod]
    public async Task RequestAsync_Shareholder_FirstRequest_Succeeds()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        var result = await sttService.RequestAsync(
            seasonId, DayOfWeek.Monday, SlotTime, memberId, [1, 2, 3]);

        Assert.IsNotNull(result, "First request by a shareholder should succeed");
        Assert.AreEqual(StandingTeeTimeStatus.Pending, result.Status);
    }

    [TestMethod]
    public async Task RequestAsync_Shareholder_SecondRequest_DifferentSlot_Rejected()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // First request
        await sttService.RequestAsync(
            seasonId, DayOfWeek.Monday, SlotTime, memberId, [1, 2, 3]);

        // Second request for a different day/time in the same season
        var result = await sttService.RequestAsync(
            seasonId, DayOfWeek.Wednesday, new TimeOnly(14, 0), memberId, [1, 2, 3]);

        Assert.IsNull(result, "A shareholder's second pending request for a different slot must be rejected");
    }

    [TestMethod]
    public async Task RequestAsync_Shareholder_SecondRequest_DifferentSeason_Rejected()
    {
        using var scope = CreateScopeWithSeason(out var season1Id);
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        // Add a second season
        var season2Id = Guid.NewGuid();
        dbContext.Seasons.Add(new Season
        {
            SeasonId = season2Id,
            Name = $"Season2 {Guid.NewGuid():N}",
            StartDate = new DateOnly(2027, 4, 1),
            EndDate = new DateOnly(2027, 9, 30),
            SeasonStatus = SeasonStatus.Planned
        });
        await dbContext.SaveChangesAsync();

        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // First request in season 1
        await sttService.RequestAsync(
            season1Id, DayOfWeek.Monday, SlotTime, memberId, [1, 2, 3]);

        // Second request in season 2 — still rejected while first is pending/approved
        var result = await sttService.RequestAsync(
            season2Id, DayOfWeek.Monday, SlotTime, memberId, [1, 2, 3]);

        Assert.IsNull(result, "A shareholder with a pending request in another season must be rejected");
    }

    [TestMethod]
    public async Task RequestAsync_Shareholder_AfterDenied_NewRequestAllowed()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // First request
        var first = await sttService.RequestAsync(
            seasonId, DayOfWeek.Monday, SlotTime, memberId, [1, 2, 3]);
        Assert.IsNotNull(first);

        // Deny the first request
        await sttService.DenyAsync(first.StandingTeeTimeId);

        // Second request should now be allowed
        var result = await sttService.RequestAsync(
            seasonId, DayOfWeek.Wednesday, new TimeOnly(14, 0), memberId, [1, 2, 3]);

        Assert.IsNotNull(result, "A new request is allowed after the previous one is denied");
    }

    [TestMethod]
    public async Task RequestAsync_TwoShareholders_CanEachSubmitOneRequest()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var member1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        var result1 = await sttService.RequestAsync(
            seasonId, DayOfWeek.Monday, SlotTime, member1, [1, 2, 3]);
        var result2 = await sttService.RequestAsync(
            seasonId, DayOfWeek.Monday, SlotTime, member2, [1, 2, 3]);

        Assert.IsNotNull(result1, "First shareholder's request should succeed");
        Assert.IsNotNull(result2, "Second shareholder's request should succeed independently");
    }

    [TestMethod]
    public async Task RequestAsync_NonShareholder_AlwaysRejected()
    {
        using var scope = CreateScopeWithSeason(out var seasonId);
        var provider = scope.ServiceProvider;
        var sttService = provider.GetRequiredService<StandingTeeTimeService<Guid>>();

        var memberId = await CreateMemberAsync(provider, MembershipCategory.Social);

        var result = await sttService.RequestAsync(
            seasonId, DayOfWeek.Monday, SlotTime, memberId, [1, 2, 3]);

        Assert.IsNull(result, "Non-shareholders cannot submit standing tee-time requests");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IServiceScope CreateScopeWithSeason(out Guid seasonId)
    {
        var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var id = Guid.NewGuid();
        dbContext.Seasons.Add(new Season
        {
            SeasonId = id,
            Name = $"Test Season {Guid.NewGuid():N}",
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
