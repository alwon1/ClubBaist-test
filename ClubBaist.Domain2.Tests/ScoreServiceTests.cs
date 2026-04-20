using System.Data;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Domain2.Entities.Scoring;
using ClubBaist.Services2;
using ClubBaist.Services2.Scoring;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class ScoreServiceTests
{
    // --- helpers ---

    private static IReadOnlyList<uint?> ValidScores(uint value = 5) =>
        Enumerable.Repeat<uint?>(value, 18).ToList().AsReadOnly();

    private static TimeSpan MinDuration(int playerCount) => playerCount switch
    {
        1 => TimeSpan.FromHours(2),
        2 => TimeSpan.FromHours(2.5),
        3 => TimeSpan.FromHours(3),
        _ => TimeSpan.FromHours(3.5)
    };

    /// <summary>
    /// Creates a slot in the past, far enough that the real system clock is past the time-lock.
    /// Uses a date from 2 days ago so even 3.5h locks are well elapsed.
    /// </summary>
    private static async Task<(Season season, TeeTimeSlot slot)> PastSlotAsync(
        SeasonService2 seasonService, AppDbContext db)
    {
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-2));
        return await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db, date, new TimeOnly(8, 0));
    }

    // --- injectable clock ---

    private sealed class FixedClock(DateTime fixedNow) : IScoreClock
    {
        public DateTime Now => fixedNow;
    }

    // --- throwing DbContext for T-27 ---

    private sealed class ThrowingOnSaveDbContext(AppDbContext inner) : IAppDbContext2
    {
        public DbSet<TeeTimeSlot> TeeTimeSlots => inner.TeeTimeSlots;
        public DbSet<TeeTimeBooking> TeeTimeBookings => inner.TeeTimeBookings;
        public DbSet<MemberShipInfo> MemberShips => inner.MemberShips;
        public DbSet<MembershipLevel> MembershipLevels => inner.MembershipLevels;
        public DbSet<MembershipApplication> MembershipApplications => inner.MembershipApplications;
        public DbSet<MembershipLevelTeeTimeAvailability> MembershipLevelTeeTimeAvailabilities => inner.MembershipLevelTeeTimeAvailabilities;
        public DbSet<SpecialEvent> SpecialEvents => inner.SpecialEvents;
        public DbSet<Season> Seasons => inner.Seasons;
        public DbSet<StandingTeeTime> StandingTeeTimes => inner.StandingTeeTimes;
        public DbSet<GolfRound> GolfRounds => inner.GolfRounds;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated save failure");

        public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default) =>
            inner.BeginTransactionAsync(isolationLevel, cancellationToken);

        public IExecutionStrategy CreateExecutionStrategy() => inner.CreateExecutionStrategy();
    }

    // =========================================================================
    // E1 — GetEligibleBookingsAsync
    // =========================================================================

    [TestMethod]
    public async Task T01_GetEligible_NoPastBookings_ReturnsEmpty()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task T02_GetEligible_OnePlayerBooking1HourAgo_ReturnedEmpty_InsideLock()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), new TimeOnly(8, 0));
        await Domain2TestData.CreateBookingAsync(db, member, slot);

        // Fixed clock: only 1 hour after slot start — inside the 2h lock
        var fakeClock = new FixedClock(DateTime.SpecifyKind(slot.Start.AddHours(1), DateTimeKind.Unspecified));
        var svc = new ScoreService(provider.GetRequiredService<IAppDbContext2>(),
            provider.GetRequiredService<ILogger<ScoreService>>(), fakeClock);

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task T03_GetEligible_OnePlayerBookingExactly2HoursAgo_Included()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), new TimeOnly(8, 0));
        await Domain2TestData.CreateBookingAsync(db, member, slot);

        // Fixed clock: exactly 2h after slot start — lock just elapsed
        var fakeClock = new FixedClock(DateTime.SpecifyKind(slot.Start.AddHours(2), DateTimeKind.Unspecified));
        var svc = new ScoreService(provider.GetRequiredService<IAppDbContext2>(),
            provider.GetRequiredService<ILogger<ScoreService>>(), fakeClock);

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(slot.Start, result[0].TeeTimeSlotStart);
    }

    [TestMethod]
    public async Task T04_GetEligible_AlreadyScoredBooking_ExcludedFromResults()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        // Submit a round to mark it as scored
        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores()),
            "acting-user");

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task T05_GetEligible_TwoEligibleOnePastLock_ReturnsTwoEligible()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");

        // Two slots from 2 days ago (both past the lock with real clock)
        var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-2));
        var (_, slot1) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db, date, new TimeOnly(8, 0));
        var (_, slot2) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), new TimeOnly(10, 0));

        // Third slot from yesterday — use fixed clock to put it inside the lock
        var (_, slot3) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), new TimeOnly(8, 0));

        await Domain2TestData.CreateBookingAsync(db, member, slot1);
        await Domain2TestData.CreateBookingAsync(db, member, slot2);
        await Domain2TestData.CreateBookingAsync(db, member, slot3);

        var fakeClock = new FixedClock(DateTime.SpecifyKind(slot3.Start.AddHours(1), DateTimeKind.Unspecified));
        var svc = new ScoreService(provider.GetRequiredService<IAppDbContext2>(),
            provider.GetRequiredService<ILogger<ScoreService>>(), fakeClock);

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(2, result.Count);
        Assert.IsFalse(result.Any(b => b.TeeTimeSlotStart == slot3.Start));
    }

    [TestMethod]
    public async Task T06_GetEligible_TwoPlayerExactly2h30mElapsed_Included()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var extra = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m2@t.com", "C", "D");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), new TimeOnly(8, 0));
        await Domain2TestData.CreateBookingAsync(db, member, slot, [extra]);

        var fakeClock = new FixedClock(DateTime.SpecifyKind(slot.Start.AddHours(2.5), DateTimeKind.Unspecified));
        var svc = new ScoreService(provider.GetRequiredService<IAppDbContext2>(),
            provider.GetRequiredService<ILogger<ScoreService>>(), fakeClock);

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(2, result[0].ParticipantCount);
    }

    [TestMethod]
    public async Task T07_GetEligible_ThreePlayerExactly3hElapsed_Included()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var extra1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m2@t.com", "C", "D");
        var extra2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m3@t.com", "E", "F");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), new TimeOnly(8, 0));
        await Domain2TestData.CreateBookingAsync(db, member, slot, [extra1, extra2]);

        var fakeClock = new FixedClock(DateTime.SpecifyKind(slot.Start.AddHours(3), DateTimeKind.Unspecified));
        var svc = new ScoreService(provider.GetRequiredService<IAppDbContext2>(),
            provider.GetRequiredService<ILogger<ScoreService>>(), fakeClock);

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(3, result[0].ParticipantCount);
    }

    [TestMethod]
    public async Task T08_GetEligible_FourPlayerExactly3h30mElapsed_Included()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var extra1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m2@t.com", "C", "D");
        var extra2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m3@t.com", "E", "F");
        var extra3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m4@t.com", "G", "H");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), new TimeOnly(8, 0));
        await Domain2TestData.CreateBookingAsync(db, member, slot, [extra1, extra2, extra3]);

        var fakeClock = new FixedClock(DateTime.SpecifyKind(slot.Start.AddHours(3.5), DateTimeKind.Unspecified));
        var svc = new ScoreService(provider.GetRequiredService<IAppDbContext2>(),
            provider.GetRequiredService<ILogger<ScoreService>>(), fakeClock);

        var result = await svc.GetEligibleBookingsAsync(member.Id);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(4, result[0].ParticipantCount);
    }

    [TestMethod]
    public async Task T09_GetEligible_MemberIsOnlyAdditionalParticipant_NotInEligibleList()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var primaryBooker = await Domain2TestData.CreateMemberAsync(userManager, db, level, "booker@t.com", "A", "B");
        var secondaryMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "secondary@t.com", "C", "D");
        var (_, slot) = await PastSlotAsync(seasonService, db);

        // secondaryMember is only an additional participant — not the primary booker
        await Domain2TestData.CreateBookingAsync(db, primaryBooker, slot, [secondaryMember]);

        var result = await svc.GetEligibleBookingsAsync(secondaryMember.Id);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task T10_GetEligible_TwoMembers_NoLeakage()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var member2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m2@t.com", "C", "D");

        var (_, slot1) = await PastSlotAsync(seasonService, db);
        var (_, slot2) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), new TimeOnly(10, 0));

        await Domain2TestData.CreateBookingAsync(db, member1, slot1);
        await Domain2TestData.CreateBookingAsync(db, member2, slot2);

        var result1 = await svc.GetEligibleBookingsAsync(member1.Id);
        var result2 = await svc.GetEligibleBookingsAsync(member2.Id);

        Assert.AreEqual(1, result1.Count);
        Assert.AreEqual(slot1.Start, result1[0].TeeTimeSlotStart);
        Assert.AreEqual(1, result2.Count);
        Assert.AreEqual(slot2.Start, result2[0].TeeTimeSlotStart);
    }

    // =========================================================================
    // E2 — SubmitRoundAsync — happy path
    // =========================================================================

    [TestMethod]
    public async Task T11_Submit_ValidRequest_SucceedsAndPersistsRound()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores()),
            "acting-user-id");

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.AreEqual(1, await db.GolfRounds.CountAsync(r => r.TeeTimeBookingId == booking.Id));
    }

    [TestMethod]
    public async Task T12_Submit_SubmittedAt_IsUnspecifiedKindAndNotDefault()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores()),
            "acting-user-id");

        var round = await db.GolfRounds.AsNoTracking().SingleAsync(r => r.TeeTimeBookingId == booking.Id);
        Assert.AreNotEqual(default(DateTime), round.SubmittedAt);
        Assert.AreEqual(DateTimeKind.Unspecified, round.SubmittedAt.Kind);
    }

    [TestMethod]
    public async Task T13_Submit_ActingUserId_MatchesParameter()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        const string actingUserId = "clerk-abc-123";
        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.Blue, ValidScores()),
            actingUserId);

        var round = await db.GolfRounds.AsNoTracking().SingleAsync(r => r.TeeTimeBookingId == booking.Id);
        Assert.AreEqual(actingUserId, round.ActingUserId);
    }

    [TestMethod]
    public async Task T14_Submit_AfterSuccess_BookingNoLongerEligible()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.Red, ValidScores()),
            "user");

        var eligible = await svc.GetEligibleBookingsAsync(member.Id);
        Assert.IsFalse(eligible.Any(b => b.BookingId == booking.Id));
    }

    // =========================================================================
    // E3 — SubmitRoundAsync — validation failures
    // =========================================================================

    [TestMethod]
    public async Task T15_Submit_UnknownMemberId_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, 99999, GolfRound.TeeColor.White, ValidScores()),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T16_Submit_BookingOwnedByDifferentMember_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var booker = await Domain2TestData.CreateMemberAsync(userManager, db, level, "booker@t.com", "A", "B");
        var otherMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "other@t.com", "C", "D");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, booker, slot);

        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, otherMember.Id, GolfRound.TeeColor.White, ValidScores()),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T17_Submit_BookingInsideTimeLock_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), new TimeOnly(8, 0));
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        // 1h after slot start — inside the 2h lock
        var fakeClock = new FixedClock(DateTime.SpecifyKind(slot.Start.AddHours(1), DateTimeKind.Unspecified));
        var svc = new ScoreService(provider.GetRequiredService<IAppDbContext2>(),
            provider.GetRequiredService<ILogger<ScoreService>>(), fakeClock);

        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores()),
            "user");

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T18_Submit_DuplicateSequential_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);
        var request = new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores());

        var first = await svc.SubmitRoundAsync(request, "user");
        Assert.IsTrue(first.Success);

        var second = await svc.SubmitRoundAsync(request, "user");

        Assert.IsFalse(second.Success);
        Assert.AreEqual(1, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T19_Submit_17Scores_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var scores = Enumerable.Repeat<uint?>(5, 17).ToList().AsReadOnly();
        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, scores),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T20_Submit_19Scores_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var scores = Enumerable.Repeat<uint?>(5, 19).ToList().AsReadOnly();
        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, scores),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T21_Submit_OneNullScore_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var scores = Enumerable.Repeat<uint?>(5, 18).ToList();
        scores[9] = null;
        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, scores.AsReadOnly()),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T22_Submit_OneScoreIsZero_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var scores = Enumerable.Repeat<uint?>(5, 18).ToList();
        scores[0] = 0;
        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, scores.AsReadOnly()),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T23_Submit_OneScoreIs21_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var scores = Enumerable.Repeat<uint?>(5, 18).ToList();
        scores[17] = 21;
        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, scores.AsReadOnly()),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T24_Submit_AllScores20_Succeeds()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores(20)),
            "user");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, await db.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T25_Submit_AllScores1_Succeeds()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores(1)),
            "user");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, await db.GolfRounds.CountAsync());
    }

    // =========================================================================
    // E4 — Concurrency
    // =========================================================================

    [TestMethod]
    public async Task T26_Submit_ConcurrentDuplicates_SecondFails()
    {
        await using var host = await Domain2TestHost.CreateAsync();

        // Seed shared data in a temporary scope
        int bookingId, memberId;
        {
            await using var seedScope = host.CreateScope();
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ClubBaistUser>>();
            var seasonService = seedScope.ServiceProvider.GetRequiredService<SeasonService2>();

            var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
            var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
            var (_, slot) = await PastSlotAsync(seasonService, db);
            var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);
            bookingId = booking.Id;
            memberId = member.Id;
        }

        var request = new SubmitRoundRequest(bookingId, memberId, GolfRound.TeeColor.White, ValidScores());

        // Scope 1: commits successfully
        await using var scope1 = host.CreateScope();
        var svc1 = new ScoreService(
            scope1.ServiceProvider.GetRequiredService<IAppDbContext2>(),
            scope1.ServiceProvider.GetRequiredService<ILogger<ScoreService>>(),
            scope1.ServiceProvider.GetRequiredService<IScoreClock>());
        var result1 = await svc1.SubmitRoundAsync(request, "user-1");
        Assert.IsTrue(result1.Success);

        // Scope 2: hits the unique index constraint
        await using var scope2 = host.CreateScope();
        var svc2 = new ScoreService(
            scope2.ServiceProvider.GetRequiredService<IAppDbContext2>(),
            scope2.ServiceProvider.GetRequiredService<ILogger<ScoreService>>(),
            scope2.ServiceProvider.GetRequiredService<IScoreClock>());
        var result2 = await svc2.SubmitRoundAsync(request, "user-2");

        Assert.IsFalse(result2.Success);
        Assert.IsNotNull(result2.ErrorMessage);
        Assert.IsTrue(result2.ErrorMessage!.Contains("already submitted", StringComparison.OrdinalIgnoreCase));

        // Only one round stored
        await using var verifyScope = host.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await verifyDb.GolfRounds.CountAsync());
    }

    [TestMethod]
    public async Task T27_Submit_SaveChangesThrows_RollsBackAndReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var (_, slot) = await PastSlotAsync(seasonService, db);
        var booking = await Domain2TestData.CreateBookingAsync(db, member, slot);

        // Use a throwing DbContext wrapper; real strategy still handles the transaction
        var throwingDb = new ThrowingOnSaveDbContext(db);
        var svc = new ScoreService(throwingDb,
            provider.GetRequiredService<ILogger<ScoreService>>(),
            provider.GetRequiredService<IScoreClock>());

        var result = await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking.Id, member.Id, GolfRound.TeeColor.White, ValidScores()),
            "user");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, await db.GolfRounds.CountAsync());
    }

    // =========================================================================
    // E5 — GetRoundsByMemberAsync
    // =========================================================================

    [TestMethod]
    public async Task T28_GetRounds_NoRounds_ReturnsEmpty()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");

        var result = await svc.GetRoundsByMemberAsync(member.Id);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task T29_GetRounds_TwoRounds_ReturnedOrderedBySubmittedAtDescending()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");

        var (_, slot1) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), new TimeOnly(8, 0));
        var (_, slot2) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), new TimeOnly(10, 0));

        var booking1 = await Domain2TestData.CreateBookingAsync(db, member, slot1);
        var booking2 = await Domain2TestData.CreateBookingAsync(db, member, slot2);

        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking1.Id, member.Id, GolfRound.TeeColor.Red, ValidScores(4)),
            "user");
        await Task.Delay(10); // ensure distinct SubmittedAt
        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking2.Id, member.Id, GolfRound.TeeColor.White, ValidScores(5)),
            "user");

        var rounds = await svc.GetRoundsByMemberAsync(member.Id);

        Assert.AreEqual(2, rounds.Count);
        // Ordered by SubmittedAt descending — most recent first
        Assert.IsTrue(rounds[0].SubmittedAt >= rounds[1].SubmittedAt);
        Assert.AreEqual(booking2.Id, rounds[0].TeeTimeBookingId);
    }

    [TestMethod]
    public async Task T30_GetRounds_TwoMembers_NoLeakage()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var svc = provider.GetRequiredService<ScoreService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@t.com", "A", "B");
        var member2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m2@t.com", "C", "D");

        var (_, slot1) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-3)), new TimeOnly(8, 0));
        var (_, slot2) = await Domain2TestData.CreateSeasonAndSlotAsync(seasonService, db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(-2)), new TimeOnly(10, 0));

        var booking1 = await Domain2TestData.CreateBookingAsync(db, member1, slot1);
        var booking2 = await Domain2TestData.CreateBookingAsync(db, member2, slot2);

        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking1.Id, member1.Id, GolfRound.TeeColor.White, ValidScores(4)),
            "user");
        await svc.SubmitRoundAsync(
            new SubmitRoundRequest(booking2.Id, member2.Id, GolfRound.TeeColor.Blue, ValidScores(6)),
            "user");

        var rounds1 = await svc.GetRoundsByMemberAsync(member1.Id);
        var rounds2 = await svc.GetRoundsByMemberAsync(member2.Id);

        Assert.AreEqual(1, rounds1.Count);
        Assert.AreEqual(booking1.Id, rounds1[0].TeeTimeBookingId);
        Assert.AreEqual(1, rounds2.Count);
        Assert.AreEqual(booking2.Id, rounds2[0].TeeTimeBookingId);
    }
}
