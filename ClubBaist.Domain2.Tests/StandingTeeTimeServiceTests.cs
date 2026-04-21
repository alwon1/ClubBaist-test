using ClubBaist.Domain2.Entities;
using ClubBaist.Services2;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class StandingTeeTimeServiceTests
{
    // -----------------------------------------------------------------------
    // SubmitRequestAsync
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task SubmitRequestAsync_ValidRequest_Succeeds()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var request = BuildRequest(bookingMember, [p1, p2, p3]);
        var (success, error) = await service.SubmitRequestAsync(request);

        Assert.IsTrue(success);
        Assert.IsNull(error);

        var persisted = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Draft, persisted.Status);
    }

    [TestMethod]
    public async Task SubmitRequestAsync_SecondActiveRequest_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var first = BuildRequest(bookingMember, [p1, p2, p3]);
        await service.SubmitRequestAsync(first);

        // second request for same member should fail
        var second = BuildRequest(bookingMember, [p1, p2, p3]);
        var (success, error) = await service.SubmitRequestAsync(second);

        Assert.IsFalse(success);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public async Task SubmitRequestAsync_EndDateBeforeStartDate_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var request = BuildRequest(bookingMember, [p1, p2, p3],
            startDate: new DateOnly(2026, 9, 1),
            endDate: new DateOnly(2026, 4, 1));   // end before start

        var (success, error) = await service.SubmitRequestAsync(request);

        Assert.IsFalse(success);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public async Task SubmitRequestAsync_FewerThanThreePlayers_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");

        var request = BuildRequest(bookingMember, [p1, p2]);   // only 2 players

        var (success, error) = await service.SubmitRequestAsync(request);

        Assert.IsFalse(success);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public async Task SubmitRequestAsync_BookingMemberInParticipants_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");

        // booking member is also listed as participant
        var request = BuildRequest(bookingMember, [p1, p2, bookingMember]);

        var (success, error) = await service.SubmitRequestAsync(request);

        Assert.IsFalse(success);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public async Task SubmitRequestAsync_DuplicateParticipants_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");

        // p1 appears twice
        var request = BuildRequest(bookingMember, [p1, p1, p2]);

        var (success, error) = await service.SubmitRequestAsync(request);

        Assert.IsFalse(success);
        Assert.IsNotNull(error);
    }

    // -----------------------------------------------------------------------
    // ApproveAsync / DenyAsync
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task ApproveAsync_DraftRequest_SetsApprovedStatusAndTime()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);

        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);
        var approvedTime = new TimeOnly(8, 15);
        var result = await service.ApproveAsync(standing.Id, approvedTime, priorityNumber: 2);

        Assert.IsTrue(result);
        var updated = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == standing.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Approved, updated.Status);
        Assert.AreEqual(approvedTime, updated.ApprovedTime);
        Assert.AreEqual(2, updated.PriorityNumber);
    }

    [TestMethod]
    public async Task ApproveAsync_NonDraftRequest_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);
        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);

        // first approve succeeds
        await service.ApproveAsync(standing.Id, new TimeOnly(8, 15), null);

        // second approve should fail because status is no longer Draft
        var result = await service.ApproveAsync(standing.Id, new TimeOnly(9, 0), null);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ApproveAsync_InvalidPriorityNumber_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);
        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);

        var result = await service.ApproveAsync(standing.Id, new TimeOnly(8, 15), priorityNumber: 0);

        Assert.IsFalse(result);
        var unchanged = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == standing.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Draft, unchanged.Status);
    }

    [TestMethod]
    public async Task DenyAsync_DraftRequest_SetsDeniedStatus()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);
        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);

        var result = await service.DenyAsync(standing.Id);

        Assert.IsTrue(result);
        var updated = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == standing.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Denied, updated.Status);
    }

    [TestMethod]
    public async Task DenyAsync_NonDraftRequest_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);
        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);

        await service.ApproveAsync(standing.Id, new TimeOnly(8, 0), null);

        // denying an already-approved request should fail
        var result = await service.DenyAsync(standing.Id);
        Assert.IsFalse(result);
    }

    // -----------------------------------------------------------------------
    // CancelAsync
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task CancelAsync_OwnRequest_SetsCancelledStatus()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);
        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);

        var result = await service.CancelAsync(standing.Id, bookingMember.Id);

        Assert.IsTrue(result);
        var updated = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == standing.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Cancelled, updated.Status);
    }

    [TestMethod]
    public async Task CancelAsync_WrongMember_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var other = await Domain2TestData.CreateMemberAsync(userManager, db, level, "other@test.com", "Other", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);
        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);

        // a different member tries to cancel
        var result = await service.CancelAsync(standing.Id, other.Id);

        Assert.IsFalse(result);
        var updated = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == standing.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Draft, updated.Status);
    }

    [TestMethod]
    public async Task CancelAsync_AlreadyCancelledRequest_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var (success, _) = await service.SubmitRequestAsync(BuildRequest(bookingMember, [p1, p2, p3]));
        Assert.IsTrue(success);
        var standing = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.BookingMemberId == bookingMember.Id);

        await service.CancelAsync(standing.Id, bookingMember.Id);

        // cancelling an already-cancelled request should fail
        var result = await service.CancelAsync(standing.Id, bookingMember.Id);
        Assert.IsFalse(result);
    }

    // -----------------------------------------------------------------------
    // Q17 – Relaxed one-per-day-of-week rule
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task SubmitRequestAsync_SecondRequest_DifferentDayOfWeek_Succeeds()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        // First request for Saturday – should succeed.
        var saturday = BuildRequest(bookingMember, [p1, p2, p3], dayOfWeek: DayOfWeek.Saturday);
        var (firstSuccess, _) = await service.SubmitRequestAsync(saturday);
        Assert.IsTrue(firstSuccess);

        // Second request for Thursday (different day) – should also succeed under the relaxed rule.
        var thursday = BuildRequest(bookingMember, [p1, p2, p3], dayOfWeek: DayOfWeek.Thursday);
        var (secondSuccess, secondError) = await service.SubmitRequestAsync(thursday);

        Assert.IsTrue(secondSuccess);
        Assert.IsNull(secondError);

        var allRequests = await db.StandingTeeTimes.AsNoTracking()
            .Where(s => s.BookingMemberId == bookingMember.Id)
            .ToListAsync();
        Assert.HasCount(2, allRequests);
    }

    [TestMethod]
    public async Task SubmitRequestAsync_SecondRequest_SameDayOfWeek_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        // Submit first Saturday request.
        var first = BuildRequest(bookingMember, [p1, p2, p3], dayOfWeek: DayOfWeek.Saturday);
        await service.SubmitRequestAsync(first);

        // Submit second Saturday request – same day, should fail.
        var second = BuildRequest(bookingMember, [p1, p2, p3], dayOfWeek: DayOfWeek.Saturday);
        var (success, error) = await service.SubmitRequestAsync(second);

        Assert.IsFalse(success);
        Assert.IsNotNull(error);
    }

    // -----------------------------------------------------------------------
    // Q16 – Weekly allocation engine
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task RunWeeklyAllocationAsync_ApprovedRequest_CreatesBookingAndSetsAllocated()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        var slotTime = new TimeOnly(8, 0);
        var slot = await Domain2TestData.CreateSlotAtAsync(db, targetDate.ToDateTime(slotTime));

        var standing = new StandingTeeTime
        {
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            RequestedDayOfWeek = targetDate.DayOfWeek,
            RequestedTime = slotTime,
            ToleranceMinutes = 30,
            StartDate = targetDate.AddDays(-1),
            EndDate = targetDate.AddDays(1),
            Status = StandingTeeTimeStatus.Approved,
            ApprovedTime = slotTime,
            PriorityNumber = 1,
            AdditionalParticipants = [p1, p2, p3]
        };
        db.StandingTeeTimes.Add(standing);
        await db.SaveChangesAsync();

        var result = await service.RunWeeklyAllocationAsync(targetDate);

        Assert.AreEqual(1, result.Allocated);
        Assert.AreEqual(0, result.Unallocated);
        Assert.AreEqual(0, result.Skipped);

        var updated = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == standing.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Allocated, updated.Status);

        var bookings = await db.TeeTimeBookings
            .Where(b => b.StandingTeeTimeId == standing.Id)
            .ToListAsync();
        Assert.HasCount(1, bookings);
        Assert.AreEqual(slot.Start, bookings[0].TeeTimeSlotStart);
        Assert.AreEqual(bookingMember.Id, bookings[0].BookingMemberId);
    }

    [TestMethod]
    public async Task RunWeeklyAllocationAsync_NoSlotAvailable_MarksUnallocated()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

        // Create a standing request but NO tee time slots for the target date.
        var standing = new StandingTeeTime
        {
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            RequestedDayOfWeek = targetDate.DayOfWeek,
            RequestedTime = new TimeOnly(8, 0),
            ToleranceMinutes = 30,
            StartDate = targetDate.AddDays(-1),
            EndDate = targetDate.AddDays(1),
            Status = StandingTeeTimeStatus.Approved,
            ApprovedTime = new TimeOnly(8, 0),
            AdditionalParticipants = [p1, p2, p3]
        };
        db.StandingTeeTimes.Add(standing);
        await db.SaveChangesAsync();

        var result = await service.RunWeeklyAllocationAsync(targetDate);

        Assert.AreEqual(0, result.Allocated);
        Assert.AreEqual(1, result.Unallocated);
        Assert.AreEqual(0, result.Skipped);

        var updated = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == standing.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Unallocated, updated.Status);

        var bookings = await db.TeeTimeBookings
            .Where(b => b.StandingTeeTimeId == standing.Id)
            .ToListAsync();
        Assert.IsEmpty(bookings);
    }

    [TestMethod]
    public async Task RunWeeklyAllocationAsync_RespectsAllocationPriority()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m1@test.com", "Member", "One");
        var member2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "m2@test.com", "Member", "Two");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");
        var p4 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p4@test.com", "Player", "Four");
        var p5 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p5@test.com", "Player", "Five");
        var p6 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p6@test.com", "Player", "Six");

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        var slotTime = new TimeOnly(8, 0);

        // Create exactly one slot that both requests want.
        var slot = await Domain2TestData.CreateSlotAtAsync(db, targetDate.ToDateTime(slotTime));

        // Higher priority (lower number) = member1.
        var highPriority = new StandingTeeTime
        {
            BookingMemberId = member1.Id,
            BookingMember = member1,
            RequestedDayOfWeek = targetDate.DayOfWeek,
            RequestedTime = slotTime,
            ToleranceMinutes = 0,
            StartDate = targetDate.AddDays(-1),
            EndDate = targetDate.AddDays(1),
            Status = StandingTeeTimeStatus.Approved,
            ApprovedTime = slotTime,
            PriorityNumber = 1,
            AdditionalParticipants = [p1, p2, p3]
        };

        var lowPriority = new StandingTeeTime
        {
            BookingMemberId = member2.Id,
            BookingMember = member2,
            RequestedDayOfWeek = targetDate.DayOfWeek,
            RequestedTime = slotTime,
            ToleranceMinutes = 0,
            StartDate = targetDate.AddDays(-1),
            EndDate = targetDate.AddDays(1),
            Status = StandingTeeTimeStatus.Approved,
            ApprovedTime = slotTime,
            PriorityNumber = 2,
            AdditionalParticipants = [p4, p5, p6]
        };

        db.StandingTeeTimes.AddRange(highPriority, lowPriority);
        await db.SaveChangesAsync();

        var result = await service.RunWeeklyAllocationAsync(targetDate);

        Assert.AreEqual(1, result.Allocated);
        Assert.AreEqual(1, result.Unallocated);

        var updatedHigh = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == highPriority.Id);
        var updatedLow = await db.StandingTeeTimes.AsNoTracking().SingleAsync(s => s.Id == lowPriority.Id);

        Assert.AreEqual(StandingTeeTimeStatus.Allocated, updatedHigh.Status);
        Assert.AreEqual(StandingTeeTimeStatus.Unallocated, updatedLow.Status);

        var highBookings = await db.TeeTimeBookings.Where(b => b.StandingTeeTimeId == highPriority.Id).ToListAsync();
        var lowBookings = await db.TeeTimeBookings.Where(b => b.StandingTeeTimeId == lowPriority.Id).ToListAsync();
        Assert.HasCount(1, highBookings);
        Assert.IsEmpty(lowBookings);
    }

    [TestMethod]
    public async Task RunWeeklyAllocationAsync_IsIdempotent_WhenRerunOnSameDate()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, level, "bm@test.com", "Booking", "Member");
        var p1 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p1@test.com", "Player", "One");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p2@test.com", "Player", "Two");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "p3@test.com", "Player", "Three");

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        var slotTime = new TimeOnly(8, 0);
        await Domain2TestData.CreateSlotAtAsync(db, targetDate.ToDateTime(slotTime));

        var standing = new StandingTeeTime
        {
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            RequestedDayOfWeek = targetDate.DayOfWeek,
            RequestedTime = slotTime,
            ToleranceMinutes = 30,
            StartDate = targetDate.AddDays(-1),
            EndDate = targetDate.AddDays(1),
            Status = StandingTeeTimeStatus.Approved,
            ApprovedTime = slotTime,
            AdditionalParticipants = [p1, p2, p3]
        };
        db.StandingTeeTimes.Add(standing);
        await db.SaveChangesAsync();

        // First run: should allocate.
        var first = await service.RunWeeklyAllocationAsync(targetDate);
        Assert.AreEqual(1, first.Allocated);

        // Second run on the same date: should skip the already-allocated request.
        var second = await service.RunWeeklyAllocationAsync(targetDate);
        Assert.AreEqual(0, second.Allocated);
        Assert.AreEqual(0, second.Unallocated);
        Assert.AreEqual(1, second.Skipped);

        // Only one booking should exist.
        var bookings = await db.TeeTimeBookings.Where(b => b.StandingTeeTimeId == standing.Id).ToListAsync();
        Assert.HasCount(1, bookings);
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static StandingTeeTime BuildRequest(
        MemberShipInfo bookingMember,
        List<MemberShipInfo> additionalParticipants,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        DayOfWeek dayOfWeek = DayOfWeek.Saturday) =>
        new()
        {
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            RequestedDayOfWeek = dayOfWeek,
            RequestedTime = new TimeOnly(8, 0),
            ToleranceMinutes = 30,
            StartDate = startDate ?? new DateOnly(2026, 4, 1),
            EndDate = endDate ?? new DateOnly(2026, 9, 30),
            AdditionalParticipants = additionalParticipants
        };
}
