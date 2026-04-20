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
    // Helper
    // -----------------------------------------------------------------------

    private static StandingTeeTime BuildRequest(
        MemberShipInfo bookingMember,
        List<MemberShipInfo> additionalParticipants,
        DateOnly? startDate = null,
        DateOnly? endDate = null) =>
        new()
        {
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(8, 0),
            ToleranceMinutes = 30,
            StartDate = startDate ?? new DateOnly(2026, 4, 1),
            EndDate = endDate ?? new DateOnly(2026, 9, 30),
            AdditionalParticipants = additionalParticipants
        };
}
