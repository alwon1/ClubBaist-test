using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Scoring;
using ClubBaist.Services2.Scoring;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class HandicapCalculationServiceTests
{
    [TestMethod]
    public async Task GetCurrentHandicapAsync_MemberNotFound_ReturnsUnavailableResult()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<HandicapCalculationService>();

        var result = await service.GetCurrentHandicapAsync(memberId: 999999, CancellationToken.None);

        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual("Member not found", result.ErrorMessage);
        Assert.AreEqual(0, result.RoundCount);
        Assert.AreEqual(0, result.DifferentialCount);
        Assert.IsNull(result.CurrentHandicap);
    }

    [TestMethod]
    public async Task GetCurrentHandicapAsync_NoRounds_ReturnsUnavailableResult()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<HandicapCalculationService>();

        var member = await CreateMemberWithGenderAsync(db, userManager, "no-rounds@clubbaist.com", Gender.Male);

        var result = await service.GetCurrentHandicapAsync(member.Id, CancellationToken.None);

        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual("No submitted rounds", result.ErrorMessage);
        Assert.AreEqual(0, result.RoundCount);
        Assert.AreEqual(0, result.DifferentialCount);
        Assert.IsNull(result.CurrentHandicap);
    }

    [TestMethod]
    public async Task GetCurrentHandicapAsync_OneRound_UsesOneDifferentialAndIsProvisional()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<HandicapCalculationService>();

        var member = await CreateMemberWithGenderAsync(db, userManager, "one-round@clubbaist.com", Gender.Male);
        await AddRoundAsync(db, member, GolfRound.TeeColor.White, 1, CreateScores(5, 18));

        var result = await service.GetCurrentHandicapAsync(member.Id, CancellationToken.None);

        Assert.IsTrue(result.IsAvailable);
        Assert.AreEqual(1, result.RoundCount);
        Assert.AreEqual(1, result.DifferentialCount);
        Assert.IsTrue(result.IsProvisional);
        Assert.IsNotNull(result.LastUpdated);
        Assert.IsNotNull(result.CurrentHandicap);
    }

    [TestMethod]
    public async Task GetCurrentHandicapAsync_EightRounds_UsesBestTwoDifferentials()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<HandicapCalculationService>();

        var member = await CreateMemberWithGenderAsync(db, userManager, "eight-rounds@clubbaist.com", Gender.Male);
        await AddRoundsWithUniformScoresAsync(db, member, roundCount: 8, teeColor: GolfRound.TeeColor.White);

        var result = await service.GetCurrentHandicapAsync(member.Id, CancellationToken.None);

        Assert.IsTrue(result.IsAvailable);
        Assert.AreEqual(8, result.RoundCount);
        Assert.AreEqual(2, result.DifferentialCount);
        Assert.IsTrue(result.IsProvisional);
    }

    [TestMethod]
    public async Task GetCurrentHandicapAsync_TwentyRounds_UsesBestEightAndNotProvisional()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<HandicapCalculationService>();

        var member = await CreateMemberWithGenderAsync(db, userManager, "twenty-rounds@clubbaist.com", Gender.Male);
        await AddRoundsWithUniformScoresAsync(db, member, roundCount: 20, teeColor: GolfRound.TeeColor.White);

        var result = await service.GetCurrentHandicapAsync(member.Id, CancellationToken.None);

        Assert.IsTrue(result.IsAvailable);
        Assert.AreEqual(20, result.RoundCount);
        Assert.AreEqual(8, result.DifferentialCount);
        Assert.IsFalse(result.IsProvisional);
    }

    [TestMethod]
    public async Task GetCurrentHandicapAsync_TwentyFiveRounds_StillUsesBestEight()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<HandicapCalculationService>();

        var member = await CreateMemberWithGenderAsync(db, userManager, "twenty-five-rounds@clubbaist.com", Gender.Male);
        await AddRoundsWithUniformScoresAsync(db, member, roundCount: 25, teeColor: GolfRound.TeeColor.White);

        var result = await service.GetCurrentHandicapAsync(member.Id, CancellationToken.None);

        Assert.IsTrue(result.IsAvailable);
        Assert.AreEqual(25, result.RoundCount);
        Assert.AreEqual(8, result.DifferentialCount);
        Assert.IsFalse(result.IsProvisional);
    }

    [TestMethod]
    public async Task GetCurrentHandicapAsync_AllRoundsInvalid_ReturnsUnavailableResult()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<HandicapCalculationService>();

        var member = await CreateMemberWithGenderAsync(db, userManager, "invalid-rounds@clubbaist.com", Gender.Male);
        await AddRoundAsync(db, member, GolfRound.TeeColor.White, 1, CreateScores(5, 17));
        await AddRoundAsync(db, member, GolfRound.TeeColor.White, 2, CreateScores(5, 17));

        var result = await service.GetCurrentHandicapAsync(member.Id, CancellationToken.None);

        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual("No valid rounds available for handicap calculation", result.ErrorMessage);
        Assert.IsNull(result.CurrentHandicap);
    }

    [TestMethod]
    public async Task GetCurrentHandicapAsync_MissingCourseRating_ReturnsUnavailableResult()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<HandicapCalculationService>();

        var member = await CreateMemberWithGenderAsync(db, userManager, "missing-rating@clubbaist.com", Gender.Male);

        var rating = await db.CourseRatings.FirstAsync(r => r.TeeColor == GolfRound.TeeColor.Red && r.Gender == Gender.Male);
        db.CourseRatings.Remove(rating);
        await db.SaveChangesAsync();

        await AddRoundAsync(db, member, GolfRound.TeeColor.Red, 1, CreateScores(5, 18));

        var result = await service.GetCurrentHandicapAsync(member.Id, CancellationToken.None);

        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual("Course rating is missing for one or more rounds", result.ErrorMessage);
        Assert.IsNull(result.CurrentHandicap);
    }

    private static async Task<MemberShipInfo> CreateMemberWithGenderAsync(
        AppDbContext db,
        UserManager<ClubBaistUser> userManager,
        string email,
        Gender gender)
    {
        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", $"Shareholder-{Guid.NewGuid():N}");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, email, "Test", "Member");

        member.User.Gender = gender;
        await db.SaveChangesAsync();
        return member;
    }

    private static async Task AddRoundsWithUniformScoresAsync(
        AppDbContext db,
        MemberShipInfo member,
        int roundCount,
        GolfRound.TeeColor teeColor)
    {
        for (var i = 1; i <= roundCount; i++)
        {
            await AddRoundAsync(db, member, teeColor, i, CreateScores(5, 18));
        }
    }

    private static async Task AddRoundAsync(
        AppDbContext db,
        MemberShipInfo member,
        GolfRound.TeeColor teeColor,
        int index,
        List<uint?> scores)
    {
        var start = DateTime.SpecifyKind(DateTime.Now.AddDays(-index).AddMinutes(index), DateTimeKind.Unspecified);
        var slot = await Domain2TestData.CreateSlotAtAsync(db, start);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        db.GolfRounds.Add(new GolfRound
        {
            TeeTimeBookingId = booking.Id,
            TeeTimeBooking = booking,
            MembershipId = member.Id,
            Member = member,
            SelectedTeeColor = teeColor,
            Scores = scores,
            SubmittedAt = DateTime.SpecifyKind(DateTime.Now.AddDays(-index), DateTimeKind.Unspecified),
            ActingUserId = member.UserId.ToString()
        });

        await db.SaveChangesAsync();
    }

    private static List<uint?> CreateScores(uint value, int holes)
    {
        var scores = Enumerable.Repeat<uint?>(value, holes).ToList();
        return scores;
    }
}