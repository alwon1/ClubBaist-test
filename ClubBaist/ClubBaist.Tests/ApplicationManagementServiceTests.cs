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

        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

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
            Sponsor1MemberId: sponsor1Id,
            Sponsor2MemberId: sponsor2Id,
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

    [TestMethod]
    public async Task GetActionableApplicationsAsync_ReturnsOnlyActionableStatuses()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        await ResetDatabaseAsync(dbContext);

        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        var statuses = new[]
        {
            ApplicationStatus.Submitted,
            ApplicationStatus.OnHold,
            ApplicationStatus.Waitlisted,
            ApplicationStatus.Accepted,
            ApplicationStatus.Denied
        };

        foreach (var status in statuses)
        {
            var userId = await CreateIdentityUserAsync(userManager);
            dbContext.MembershipApplications.Add(CreateApplication(userId, status, submittedAt, sponsor1Id, sponsor2Id));
            submittedAt = submittedAt.AddMinutes(1);
        }

        await dbContext.SaveChangesAsync();

        var result = await applicationService.GetActionableApplicationsAsync();

        Assert.HasCount(3, result);
        Assert.IsTrue(result.All(item =>
            item.CurrentStatus is ApplicationStatus.Submitted or ApplicationStatus.OnHold or ApplicationStatus.Waitlisted));
        Assert.IsFalse(result.Any(item =>
            item.CurrentStatus is ApplicationStatus.Accepted or ApplicationStatus.Denied));
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

    private static async Task<int> CreateIdentityUserAsync(UserManager<IdentityUser<int>> userManager)
    {
        var userId = await CreateUniquePositiveIntUserIdAsync(userManager);
        var user = new IdentityUser<int>
        {
            Id = userId,
            UserName = $"user-{userId}",
            Email = $"user-{userId}@example.com"
        };

        var createResult = await userManager.CreateAsync(user);
        Assert.IsTrue(createResult.Succeeded, string.Join(",", createResult.Errors.Select(error => error.Description)));

        return userId;
    }

    private static async Task<int> CreateSponsorMemberAsync(
        UserManager<IdentityUser<int>> userManager,
        TestApplicationDbContext dbContext)
    {
        var userId = await CreateIdentityUserAsync(userManager);
        dbContext.MemberAccounts.Add(new MemberAccount<int>
        {
            ApplicationUserId = userId,
            MemberNumber = $"M-{userId}",
            FirstName = "Sponsor",
            LastName = "Member",
            DateOfBirth = new DateTime(1975, 1, 1),
            Email = $"sponsor-{userId}@example.com",
            Phone = "555-0200",
            Address = "1 Sponsor Lane",
            PostalCode = "S1S1S1",
            MembershipCategory = MembershipCategory.Social,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static MembershipApplication<int> CreateApplication(
        int userId,
        ApplicationStatus status,
        DateTime submittedAt,
        int sponsor1Id,
        int sponsor2Id)
    {
        return new MembershipApplication<int>
        {
            ApplicationId = Guid.NewGuid(),
            ApplicationUserId = userId,
            CurrentStatus = status,
            SubmittedAt = submittedAt,
            LastStatusChangedAt = submittedAt,
            FirstName = "Seed",
            LastName = "Applicant",
            Occupation = "Tester",
            CompanyName = "ClubBaist",
            Address = "100 Testing Ave",
            PostalCode = "T2T2T2",
            Phone = "555-0199",
            Email = "seed@example.com",
            DateOfBirth = new DateTime(1992, 3, 1),
            RequestedMembershipCategory = MembershipCategory.Social,
            Sponsor1MemberId = sponsor1Id,
            Sponsor2MemberId = sponsor2Id
        };
    }

    private static async Task ResetDatabaseAsync(TestApplicationDbContext dbContext)
    {
        await dbContext.ApplicationStatusHistories.ExecuteDeleteAsync();
        await dbContext.MemberAccounts.ExecuteDeleteAsync();
        await dbContext.MembershipApplications.ExecuteDeleteAsync();
        await dbContext.Users.ExecuteDeleteAsync();
        await dbContext.Roles.ExecuteDeleteAsync();
    }
}