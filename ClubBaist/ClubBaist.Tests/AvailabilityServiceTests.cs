using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class AvailabilityServiceTests
{
    [TestMethod]
    public async Task EvaluateCreateBookingAsync_AllowsValidShareholderRequest()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var seasonService = provider.GetRequiredService<SeasonService<int>>();
        var availabilityService = provider.GetRequiredService<AvailabilityService<int>>();

        await seasonService.CreateSeasonAsync("Summer 2029", new DateOnly(2029, 6, 1), new DateOnly(2029, 8, 31));

        dbContext.Users.Add(new IdentityUser<int> { Id = 3001, UserName = "availability-user", Email = "availability@clubbaist.local" });
        var member = new MemberAccount<int>
        {
            ApplicationUserId = 3001,
            MemberNumber = "MBR-3001",
            FirstName = "Valid",
            LastName = "Member",
            DateOfBirth = new DateTime(1988, 1, 1),
            Email = "availability@clubbaist.local",
            Phone = "780-555-0303",
            Address = "300 Main St",
            PostalCode = "T2T2T2",
            MembershipCategory = MembershipCategory.Shareholder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.MemberAccounts.Add(member);
        await dbContext.SaveChangesAsync();

        var result = await availabilityService.EvaluateCreateBookingAsync(new BookingRequest(
            member.MemberAccountId,
            new DateOnly(2029, 7, 12),
            new TimeOnly(6, 15),
            DateTimeOffset.UtcNow,
            4));

        Assert.AreEqual(ServiceResultStatus.Success, result.Status);
        Assert.IsTrue(result.Value!.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_ALLOWED.ToString(), result.Value.DecisionCode);
    }

    [TestMethod]
    public async Task EvaluateCreateBookingsAsync_ReturnsDecisionPerRequestedSlot()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var seasonService = provider.GetRequiredService<SeasonService<int>>();
        var availabilityService = provider.GetRequiredService<AvailabilityService<int>>();

        await seasonService.CreateSeasonAsync("Summer 2031", new DateOnly(2031, 6, 1), new DateOnly(2031, 8, 31));

        dbContext.Users.Add(new IdentityUser<int> { Id = 4001, UserName = "batch-user", Email = "batch@clubbaist.local" });
        var member = new MemberAccount<int>
        {
            ApplicationUserId = 4001,
            MemberNumber = "MBR-4001",
            FirstName = "Batch",
            LastName = "Member",
            DateOfBirth = new DateTime(1987, 5, 1),
            Email = "batch@clubbaist.local",
            Phone = "780-555-0404",
            Address = "400 Main St",
            PostalCode = "T4T4T4",
            MembershipCategory = MembershipCategory.Shareholder,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.MemberAccounts.Add(member);
        await dbContext.SaveChangesAsync();

        var requests = new List<BookingRequest>
        {
            new(member.MemberAccountId, new DateOnly(2031, 7, 10), new TimeOnly(6, 0), DateTimeOffset.UtcNow, 2),
            new(member.MemberAccountId, new DateOnly(2031, 7, 10), new TimeOnly(8, 0), DateTimeOffset.UtcNow, 5)
        };

        var result = await availabilityService.EvaluateCreateBookingsAsync(requests);

        Assert.AreEqual(ServiceResultStatus.Success, result.Status);
        Assert.AreEqual(2, result.Value!.Count);
        Assert.IsTrue(result.Value[0].Allowed);
        Assert.IsFalse(result.Value[1].Allowed);
        Assert.AreEqual(ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE.ToString(), result.Value[1].DecisionCode);
    }

    [TestMethod]
    public async Task BookingPolicyService_DelegatesToAvailabilityServiceForSharedChecks()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var bookingPolicyService = provider.GetRequiredService<BookingPolicyService<int>>();
        var availabilityService = provider.GetRequiredService<AvailabilityService<int>>();

        var policyViaBooking = await bookingPolicyService.GetPolicyForDateAsync(new DateOnly(2040, 1, 1));
        var policyViaAvailability = await availabilityService.GetPolicyForDateAsync(new DateOnly(2040, 1, 1));

        Assert.AreEqual(policyViaAvailability.Status, policyViaBooking.Status);
        Assert.AreEqual(policyViaAvailability.ConflictCode, policyViaBooking.ConflictCode);
        Assert.AreEqual(policyViaAvailability.ConflictMessage, policyViaBooking.ConflictMessage);
    }
}
