using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Tests;

[TestClass]
public sealed class BookingPolicyServiceTests
{
    [TestMethod]
    public async Task EvaluateCreateBookingAsync_AllRulesPass_ReturnsAllowedDecision()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var policyService = provider.GetRequiredService<BookingPolicyService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();

        var member = await CreateMemberAsync(userManager, MembershipCategory.Shareholder, isActive: true);

        dbContext.MemberAccounts.Add(member);
        dbContext.Seasons.Add(new Season
        {
            Name = "Summer",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 9, 30),
            SeasonStatus = SeasonStatus.Active
        });

        await dbContext.SaveChangesAsync();

        var decision = await policyService.EvaluateCreateBookingAsync(new BookingPolicyRequest(
            member.MemberAccountId,
            new DateOnly(2026, 6, 15),
            new TimeOnly(8, 30),
            [member.MemberAccountId]));

        Assert.IsTrue(decision.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_ALLOWED, decision.DecisionCode);
        Assert.IsEmpty(decision.Reasons);
    }

    [TestMethod]
    public async Task EvaluateCreateBookingAsync_MultipleFailures_ReturnsDeterministicPrimaryCodeAndAllReasons()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var policyService = provider.GetRequiredService<BookingPolicyService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();

        dbContext.MemberAccounts.Add(await CreateMemberAsync(userManager, MembershipCategory.Junior, isActive: false));
        await dbContext.SaveChangesAsync();

        var decision = await policyService.EvaluateCreateBookingAsync(new BookingPolicyRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 12, 20),
            new TimeOnly(7, 0),
            []));

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION, decision.DecisionCode);
        CollectionAssert.AreEquivalent(
            new[]
            {
            $"{ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION}: Requested play date is outside the active season window.",
            $"{ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE}: Player count must be between 1 and 4.",
            $"{ReservationDecisionCodes.BOOKING_FORBIDDEN}: Booking member account was not found."
            },
            decision.Reasons.ToArray());
    }

    [TestMethod]
    public async Task EvaluateCreateBookingAsync_PlayerCategoryTimeWindowViolation_IsCaptured()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var policyService = provider.GetRequiredService<BookingPolicyService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();

        var bookingMember = await CreateMemberAsync(userManager, MembershipCategory.Shareholder, isActive: true);
        var socialPlayer = await CreateMemberAsync(userManager, MembershipCategory.Social, isActive: true);

        dbContext.MemberAccounts.AddRange(bookingMember, socialPlayer);
        dbContext.Seasons.Add(new Season
        {
            Name = "Summer",
            StartDate = new DateOnly(2026, 5, 1),
            EndDate = new DateOnly(2026, 9, 30),
            SeasonStatus = SeasonStatus.Active
        });

        await dbContext.SaveChangesAsync();

        var decision = await policyService.EvaluateCreateBookingAsync(new BookingPolicyRequest(
            bookingMember.MemberAccountId,
            new DateOnly(2026, 6, 15),
            new TimeOnly(9, 30),
            [bookingMember.MemberAccountId, socialPlayer.MemberAccountId]));

        Assert.IsFalse(decision.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION, decision.DecisionCode);
        Assert.IsTrue(decision.Reasons.Any(
            reason => reason.Contains("Membership category 'Social' allows tee times", StringComparison.Ordinal)));
    }

    private static async Task<MemberAccount<int>> CreateMemberAsync(
        UserManager<IdentityUser<int>> userManager,
        MembershipCategory category,
        bool isActive)
    {
        var userId = await CreateIdentityUserAsync(userManager);

        return new MemberAccount<int>
        {
            ApplicationUserId = userId,
            MemberNumber = $"M-{Guid.NewGuid():N}"[..10],
            FirstName = "Test",
            LastName = "Member",
            DateOfBirth = new DateTime(1990, 1, 1),
            Email = $"test-{Guid.NewGuid():N}@example.com",
            Phone = "555-0100",
            Address = "123 Test Street",
            PostalCode = "T3S7A1",
            MembershipCategory = category,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task<int> CreateIdentityUserAsync(UserManager<IdentityUser<int>> userManager)
    {
        while (true)
        {
            var candidateId = Random.Shared.Next(1, int.MaxValue);
            if (await userManager.Users.AnyAsync(user => user.Id == candidateId))
            {
                continue;
            }

            var user = new IdentityUser<int>
            {
                Id = candidateId,
                UserName = $"user-{candidateId}",
                Email = $"user-{candidateId}@example.com"
            };

            var createResult = await userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                return candidateId;
            }
        }
    }
}
