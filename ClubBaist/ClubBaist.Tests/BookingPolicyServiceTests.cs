using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class BookingPolicyServiceTests
{
    [TestMethod]
    public async Task EvaluateCreateBookingAsync_UsesBookingMemberMembershipWindow()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var seasonService = provider.GetRequiredService<SeasonService<int>>();
        var bookingPolicyService = provider.GetRequiredService<BookingPolicyService<int>>();

        var season = await seasonService.CreateSeasonAsync(
            "Summer 2027",
            new DateOnly(2027, 6, 1),
            new DateOnly(2027, 8, 31));

        var bookingUser = new IdentityUser<int> { Id = 1001, UserName = "restricted-user", Email = "restricted@clubbaist.local" };
        dbContext.Users.Add(bookingUser);

        var restrictedBookingMember = new MemberAccount<int>
        {
            ApplicationUserId = 1001,
            MemberNumber = "MBR-0001",
            FirstName = "Restricted",
            LastName = "Member",
            DateOfBirth = new DateTime(1990, 1, 1),
            Email = "restricted@clubbaist.local",
            Phone = "780-555-0101",
            Address = "100 Main St",
            PostalCode = "T0T0T0",
            MembershipCategory = MembershipCategory.Social,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.MemberAccounts.Add(restrictedBookingMember);
        await dbContext.SaveChangesAsync();

        var request = new BookingRequest(
            restrictedBookingMember.MemberAccountId,
            new DateOnly(2027, 7, 15),
            new TimeOnly(7, 0),
            DateTimeOffset.UtcNow,
            1);

        var decisionResult = await bookingPolicyService.EvaluateCreateBookingAsync(request);

        Assert.AreEqual(ServiceResultStatus.Success, decisionResult.Status);
        Assert.IsNotNull(decisionResult.Value);
        Assert.IsFalse(decisionResult.Value.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION.ToString(), decisionResult.Value.DecisionCode);
        Assert.IsTrue(decisionResult.Value.Reasons.Count > 0);
        Assert.IsNotNull(decisionResult.Value.PolicyApplied);
        Assert.AreEqual(season.Value!.SeasonId, decisionResult.Value.PolicyApplied.SeasonId);
    }

    [TestMethod]
    public async Task EvaluateCancelBookingAsync_AllowsOwnerWithoutCutoff()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var bookingPolicyService = provider.GetRequiredService<BookingPolicyService<int>>();

        var bookingMemberId = Guid.NewGuid();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookingMemberAccountId = bookingMemberId,
            SlotDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
            SlotTime = new TimeOnly(6, 0),
            Status = ReservationStatus.Active
        };

        dbContext.Reservations.Add(reservation);
        dbContext.Entry(reservation).Property("IdempotencyKey").CurrentValue = $"idem-{Guid.NewGuid():N}";
        await dbContext.SaveChangesAsync();

        var cancelRequest = new BookingCancellation(
            reservation.ReservationId,
            bookingMemberId,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var decisionResult = await bookingPolicyService.EvaluateCancelBookingAsync(cancelRequest);

        Assert.AreEqual(ServiceResultStatus.Success, decisionResult.Status);
        Assert.IsTrue(decisionResult.Value!.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_ALLOWED.ToString(), decisionResult.Value.DecisionCode);
    }

    [TestMethod]
    public async Task EvaluateCancelBookingAsync_FailureContainsReasonAndSupportedCode()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var bookingPolicyService = provider.GetRequiredService<BookingPolicyService<int>>();

        var ownerMemberId = Guid.NewGuid();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookingMemberAccountId = ownerMemberId,
            SlotDate = new DateOnly(2027, 7, 20),
            SlotTime = new TimeOnly(9, 0),
            Status = ReservationStatus.Active
        };

        dbContext.Reservations.Add(reservation);
        dbContext.Entry(reservation).Property("IdempotencyKey").CurrentValue = $"idem-{Guid.NewGuid():N}";
        await dbContext.SaveChangesAsync();

        var cancelRequest = new BookingCancellation(
            reservation.ReservationId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var decisionResult = await bookingPolicyService.EvaluateCancelBookingAsync(cancelRequest);

        Assert.AreEqual(ServiceResultStatus.Success, decisionResult.Status);
        Assert.IsFalse(decisionResult.Value!.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_FORBIDDEN.ToString(), decisionResult.Value.DecisionCode);
        Assert.IsTrue(decisionResult.Value.Reasons.Count >= 1);
    }

    [TestMethod]
    public async Task GetPolicyForDateAsync_OutsideSeason_ReturnsBookingWindowConflict()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var bookingPolicyService = provider.GetRequiredService<BookingPolicyService<int>>();

        var result = await bookingPolicyService.GetPolicyForDateAsync(new DateOnly(2030, 1, 1));

        Assert.AreEqual(ServiceResultStatus.Conflict, result.Status);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION.ToString(), result.ConflictCode);
    }

    [TestMethod]
    public async Task EvaluateCreateBookingAsync_PlayerCountOutOfRange_ReturnsDeniedDecision()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var seasonService = provider.GetRequiredService<SeasonService<int>>();
        var bookingPolicyService = provider.GetRequiredService<BookingPolicyService<int>>();

        await seasonService.CreateSeasonAsync(
            "Summer 2028",
            new DateOnly(2028, 6, 1),
            new DateOnly(2028, 8, 31));

        var bookingUser = new IdentityUser<int> { Id = 2001, UserName = "priority-user", Email = "priority@clubbaist.local" };
        dbContext.Users.Add(bookingUser);

        var priorityMember = new MemberAccount<int>
        {
            ApplicationUserId = 2001,
            MemberNumber = "MBR-2001",
            FirstName = "Priority",
            LastName = "Member",
            DateOfBirth = new DateTime(1991, 2, 3),
            Email = "priority@clubbaist.local",
            Phone = "780-555-0202",
            Address = "200 Main St",
            PostalCode = "T1T1T1",
            MembershipCategory = MembershipCategory.Shareholder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.MemberAccounts.Add(priorityMember);
        await dbContext.SaveChangesAsync();

        var request = new BookingRequest(
            priorityMember.MemberAccountId,
            new DateOnly(2028, 7, 10),
            new TimeOnly(8, 0),
            DateTimeOffset.UtcNow,
            5);

        var result = await bookingPolicyService.EvaluateCreateBookingAsync(request);

        Assert.AreEqual(ServiceResultStatus.Success, result.Status);
        Assert.IsFalse(result.Value!.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE.ToString(), result.Value.DecisionCode);
        Assert.IsTrue(result.Value.Reasons.Count > 0);
    }
}
