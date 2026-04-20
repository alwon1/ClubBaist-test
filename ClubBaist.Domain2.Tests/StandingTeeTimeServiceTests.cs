using ClubBaist.Domain2.Entities;
using ClubBaist.Services2;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class StandingTeeTimeServiceTests
{
    [TestMethod]
    public async Task SubmitRequestAsync_Success_WhenValidFoursome()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var booking = await Domain2TestData.CreateMemberAsync(userManager, db, level, "s1@test.com", "Alice", "Shareholder");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "s2@test.com", "Bob", "Player");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "s3@test.com", "Carol", "Player");
        var p4 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "s4@test.com", "Dave", "Player");

        var request = new StandingTeeTime
        {
            BookingMemberId = booking.Id,
            BookingMember = booking,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(9, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [p2, p3, p4]
        };

        var error = await service.SubmitRequestAsync(request);

        Assert.IsNull(error);
        Assert.AreNotEqual(0, request.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Draft, request.Status);
    }

    [TestMethod]
    public async Task SubmitRequestAsync_ReturnsError_WhenNotFoursome()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var booking = await Domain2TestData.CreateMemberAsync(userManager, db, level, "ns1@test.com", "Alice", "Shareholder");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "ns2@test.com", "Bob", "Player");

        var request = new StandingTeeTime
        {
            BookingMemberId = booking.Id,
            BookingMember = booking,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(9, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [p2]   // only 1 additional — not a foursome
        };

        var error = await service.SubmitRequestAsync(request);

        Assert.IsNotNull(error);
        StringAssert.Contains(error, "foursome");
    }

    [TestMethod]
    public async Task SubmitRequestAsync_ReturnsError_WhenDuplicateActiveRequest()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var booking = await Domain2TestData.CreateMemberAsync(userManager, db, level, "dup1@test.com", "Alice", "Shareholder");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "dup2@test.com", "Bob", "Player");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "dup3@test.com", "Carol", "Player");
        var p4 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "dup4@test.com", "Dave", "Player");

        var firstRequest = new StandingTeeTime
        {
            BookingMemberId = booking.Id,
            BookingMember = booking,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(9, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [p2, p3, p4]
        };

        await service.SubmitRequestAsync(firstRequest);

        var secondRequest = new StandingTeeTime
        {
            BookingMemberId = booking.Id,
            BookingMember = booking,
            RequestedDayOfWeek = DayOfWeek.Sunday,
            RequestedTime = new TimeOnly(10, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [p2, p3, p4]
        };

        var error = await service.SubmitRequestAsync(secondRequest);

        Assert.IsNotNull(error);
        StringAssert.Contains(error, "active standing tee time request");
    }

    [TestMethod]
    public async Task CancelRequestAsync_Success_WhenOwnerCancels()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var booking = await Domain2TestData.CreateMemberAsync(userManager, db, level, "c1@test.com", "Alice", "Shareholder");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "c2@test.com", "Bob", "Player");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "c3@test.com", "Carol", "Player");
        var p4 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "c4@test.com", "Dave", "Player");

        var request = new StandingTeeTime
        {
            BookingMemberId = booking.Id,
            BookingMember = booking,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(9, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [p2, p3, p4]
        };

        await service.SubmitRequestAsync(request);

        var result = await service.CancelRequestAsync(request.Id, booking.Id);

        Assert.IsTrue(result);

        var updated = await db.StandingTeeTimes.FindAsync(request.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Cancelled, updated!.Status);
    }

    [TestMethod]
    public async Task CancelRequestAsync_ReturnsFalse_WhenMemberMismatch()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var booking = await Domain2TestData.CreateMemberAsync(userManager, db, level, "mm1@test.com", "Alice", "Shareholder");
        var other = await Domain2TestData.CreateMemberAsync(userManager, db, level, "mm0@test.com", "Other", "Member");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "mm2@test.com", "Bob", "Player");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "mm3@test.com", "Carol", "Player");
        var p4 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "mm4@test.com", "Dave", "Player");

        var request = new StandingTeeTime
        {
            BookingMemberId = booking.Id,
            BookingMember = booking,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(9, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [p2, p3, p4]
        };

        await service.SubmitRequestAsync(request);

        // Another member tries to cancel — should fail
        var result = await service.CancelRequestAsync(request.Id, other.Id);

        Assert.IsFalse(result);
        var standing = await db.StandingTeeTimes.FindAsync(request.Id);
        Assert.AreEqual(StandingTeeTimeStatus.Draft, standing!.Status);
    }

    [TestMethod]
    public async Task GetMemberRequestsAsync_ReturnsOnlyOwnRequests()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var service = provider.GetRequiredService<StandingTeeTimeService>();

        var level = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var memberA = await Domain2TestData.CreateMemberAsync(userManager, db, level, "ga1@test.com", "Alice", "A");
        var memberB = await Domain2TestData.CreateMemberAsync(userManager, db, level, "gb1@test.com", "Bob", "B");
        var p2 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "gp2@test.com", "P2", "Player");
        var p3 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "gp3@test.com", "P3", "Player");
        var p4 = await Domain2TestData.CreateMemberAsync(userManager, db, level, "gp4@test.com", "P4", "Player");

        var requestA = new StandingTeeTime
        {
            BookingMemberId = memberA.Id,
            BookingMember = memberA,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(9, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [memberB, p2, p3]
        };

        var requestB = new StandingTeeTime
        {
            BookingMemberId = memberB.Id,
            BookingMember = memberB,
            RequestedDayOfWeek = DayOfWeek.Sunday,
            RequestedTime = new TimeOnly(10, 0),
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today).AddMonths(6),
            AdditionalParticipants = [memberA, p2, p4]
        };

        await service.SubmitRequestAsync(requestA);
        await service.SubmitRequestAsync(requestB);

        var resultsA = await service.GetMemberRequestsAsync(memberA.Id);
        var resultsB = await service.GetMemberRequestsAsync(memberB.Id);

        Assert.HasCount(1, resultsA);
        Assert.AreEqual(memberA.Id, resultsA[0].BookingMemberId);

        Assert.HasCount(1, resultsB);
        Assert.AreEqual(memberB.Id, resultsB[0].BookingMemberId);
    }
}
