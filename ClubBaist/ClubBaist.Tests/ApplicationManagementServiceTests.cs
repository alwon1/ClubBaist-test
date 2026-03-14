using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class ApplicationManagementServiceTests
{
    [TestMethod]
    public async Task SubmitApplicationAsync_PersistsApplication_WithSubmittedStatus()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        var userId = await CreateUniquePositiveIntUserIdAsync(userManager);
        var identityUser = new IdentityUser<int>
        {
            Id = userId,
            UserName = $"applicant-{userId}",
            Email = $"applicant-{userId}@example.com"
        };

        var userCreateResult = await userManager.CreateAsync(identityUser);
        Assert.IsTrue(
            userCreateResult.Succeeded,
            string.Join(",", userCreateResult.Errors.Select(error => error.Description)));

        var submittedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var request = new SubmitApplicationRequest<int>(
            ApplicationUserId: userId,
            FirstName: "Jane",
            LastName: "Doe",
            Occupation: "Engineer",
            CompanyName: "Acme Corp",
            Address: "123 Main St",
            PostalCode: "T1T1T1",
            Phone: "555-0100",
            Email: "jane.doe@example.com",
            DateOfBirth: new DateTime(1990, 5, 20),
            RequestedMembershipCategory: MembershipCategory.Social,
            Sponsor1MemberId: Random.Shared.Next(1, int.MaxValue),
            Sponsor2MemberId: Random.Shared.Next(1, int.MaxValue),
            AlternatePhone: "555-0101",
            SubmittedAt: submittedAt);

        var result = await applicationService.SubmitApplicationAsync(request, userId);

        Assert.AreNotEqual(Guid.Empty, result.ApplicationId);
        Assert.AreEqual(ApplicationStatus.Submitted, result.CurrentStatus);
        Assert.AreEqual(userId, result.ApplicationUserId);

        var persisted = await dbContext.MembershipApplications
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationId == result.ApplicationId);

        Assert.AreEqual(ApplicationStatus.Submitted, persisted.CurrentStatus);
        Assert.AreEqual(userId, persisted.ApplicationUserId);
        Assert.AreEqual("Jane", persisted.FirstName);
        Assert.AreEqual("Doe", persisted.LastName);
        Assert.AreEqual(submittedAt, persisted.SubmittedAt);
    }

    private static async Task<int> CreateUniquePositiveIntUserIdAsync(UserManager<IdentityUser<int>> userManager)
    {
        while (true)
        {
            var candidate = Random.Shared.Next(1, int.MaxValue);
            var exists = await userManager.Users.AnyAsync(user => user.Id == candidate);

            if (!exists)
            {
                return candidate;
            }
        }
    }
}