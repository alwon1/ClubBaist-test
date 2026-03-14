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

    [TestMethod]
    public async Task ChangeApplicationStatusAsync_ValidTransition_UpdatesStatusAndCreatesHistory()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        await ResetDatabaseAsync(dbContext);

        var applicantUserId = await CreateIdentityUserAsync(userManager);
        var changedByUserId = await CreateIdentityUserAsync(userManager);
        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);
        var submitRequest = new SubmitApplicationRequest<int>(
            ApplicationUserId: applicantUserId,
            FirstName: "Alex",
            LastName: "Applicant",
            Occupation: "Analyst",
            CompanyName: "ClubBaist",
            Address: "500 Main St",
            PostalCode: "T3T3T3",
            Phone: "555-0300",
            Email: "alex.applicant@example.com",
            DateOfBirth: new DateTime(1991, 4, 10),
            RequestedMembershipCategory: MembershipCategory.Social,
            Sponsor1MemberId: sponsor1Id,
            Sponsor2MemberId: sponsor2Id,
            SubmittedAt: submittedAt);

        var submittedApplication = await applicationService.SubmitApplicationAsync(submitRequest, applicantUserId);

        var changedAt = new DateTime(2026, 2, 16, 10, 15, 0, DateTimeKind.Utc);
        var result = await applicationService.ChangeApplicationStatusAsync(
            submittedApplication.ApplicationId,
            ApplicationStatus.OnHold,
            changedByUserId,
            changedAt);

        Assert.AreEqual(submittedApplication.ApplicationId, result.ApplicationId);
        Assert.AreEqual(ApplicationStatus.OnHold, result.CurrentStatus);
        Assert.AreEqual(changedAt, result.LastStatusChangedAt);
        Assert.IsNull(result.MemberCreationResult);

        var persistedApplication = await dbContext.MembershipApplications
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationId == submittedApplication.ApplicationId);

        Assert.AreEqual(ApplicationStatus.OnHold, persistedApplication.CurrentStatus);
        Assert.AreEqual(changedAt, persistedApplication.LastStatusChangedAt);

        var historyEntries = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(item => item.MembershipApplicationId == submittedApplication.ApplicationId)
            .ToListAsync();

        Assert.HasCount(1, historyEntries);
        var history = historyEntries.Single();
        Assert.AreEqual(ApplicationStatus.Submitted, history.FromStatus);
        Assert.AreEqual(ApplicationStatus.OnHold, history.ToStatus);
        Assert.AreEqual(changedByUserId, history.ChangedByUserId);
        Assert.AreEqual(changedAt, history.ChangedAt);
    }

    [TestMethod]
    public async Task RecordStatusHistoryAsync_PersistsHistoryEntry_WithExpectedValues()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        await ResetDatabaseAsync(dbContext);

        var applicantUserId = await CreateIdentityUserAsync(userManager);
        var changedByUserId = await CreateIdentityUserAsync(userManager);
        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc);
        var submitted = await applicationService.SubmitApplicationAsync(
            new SubmitApplicationRequest<int>(
                ApplicationUserId: applicantUserId,
                FirstName: "Sam",
                LastName: "Submitter",
                Occupation: "Coordinator",
                CompanyName: "ClubBaist",
                Address: "900 Service Rd",
                PostalCode: "T4T4T4",
                Phone: "555-0400",
                Email: "sam.submitter@example.com",
                DateOfBirth: new DateTime(1993, 7, 12),
                RequestedMembershipCategory: MembershipCategory.Social,
                Sponsor1MemberId: sponsor1Id,
                Sponsor2MemberId: sponsor2Id,
                SubmittedAt: submittedAt),
            applicantUserId);

        var changedAt = new DateTime(2026, 2, 21, 11, 45, 0, DateTimeKind.Utc);
        var history = await applicationService.RecordStatusHistoryAsync(
            submitted.ApplicationId,
            ApplicationStatus.Submitted,
            ApplicationStatus.Waitlisted,
            changedByUserId,
            changedAt);

        Assert.AreEqual(submitted.ApplicationId, history.MembershipApplicationId);
        Assert.AreEqual(ApplicationStatus.Submitted, history.FromStatus);
        Assert.AreEqual(ApplicationStatus.Waitlisted, history.ToStatus);
        Assert.AreEqual(changedByUserId, history.ChangedByUserId);
        Assert.AreEqual(changedAt, history.ChangedAt);

        var persistedHistory = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(item => item.MembershipApplicationId == submitted.ApplicationId)
            .ToListAsync();

        Assert.HasCount(1, persistedHistory);
        var persisted = persistedHistory.Single();
        Assert.AreEqual(history.ApplicationStatusHistoryId, persisted.ApplicationStatusHistoryId);
        Assert.AreEqual(ApplicationStatus.Submitted, persisted.FromStatus);
        Assert.AreEqual(ApplicationStatus.Waitlisted, persisted.ToStatus);
        Assert.AreEqual(changedByUserId, persisted.ChangedByUserId);
        Assert.AreEqual(changedAt, persisted.ChangedAt);
    }

    [TestMethod]
    public async Task ChangeApplicationStatusAsync_InvalidTransition_ThrowsAndKeepsPersistedState()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        await ResetDatabaseAsync(dbContext);

        var applicantUserId = await CreateIdentityUserAsync(userManager);
        var changedByUserId = await CreateIdentityUserAsync(userManager);
        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 2, 22, 9, 0, 0, DateTimeKind.Utc);
        var submitted = await applicationService.SubmitApplicationAsync(
            new SubmitApplicationRequest<int>(
                ApplicationUserId: applicantUserId,
                FirstName: "Taylor",
                LastName: "Transition",
                Occupation: "Planner",
                CompanyName: "ClubBaist",
                Address: "1200 Process Blvd",
                PostalCode: "T5T5T5",
                Phone: "555-0500",
                Email: "taylor.transition@example.com",
                DateOfBirth: new DateTime(1990, 9, 5),
                RequestedMembershipCategory: MembershipCategory.Social,
                Sponsor1MemberId: sponsor1Id,
                Sponsor2MemberId: sponsor2Id,
                SubmittedAt: submittedAt),
            applicantUserId);

        var acceptedAt = new DateTime(2026, 2, 23, 10, 0, 0, DateTimeKind.Utc);
        var acceptedResult = await applicationService.ChangeApplicationStatusAsync(
            submitted.ApplicationId,
            ApplicationStatus.Accepted,
            changedByUserId,
            acceptedAt);

        Assert.AreEqual(ApplicationStatus.Accepted, acceptedResult.CurrentStatus);

        var invalidChangedAt = acceptedAt.AddHours(2);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await applicationService.ChangeApplicationStatusAsync(
                submitted.ApplicationId,
                ApplicationStatus.OnHold,
                changedByUserId,
                invalidChangedAt));

        var persistedApplication = await dbContext.MembershipApplications
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationId == submitted.ApplicationId);

        Assert.AreEqual(ApplicationStatus.Accepted, persistedApplication.CurrentStatus);
        Assert.AreEqual(acceptedAt, persistedApplication.LastStatusChangedAt);

        var historyEntries = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(item => item.MembershipApplicationId == submitted.ApplicationId)
            .OrderBy(item => item.ChangedAt)
            .ToListAsync();

        Assert.HasCount(1, historyEntries);
        Assert.AreEqual(ApplicationStatus.Submitted, historyEntries[0].FromStatus);
        Assert.AreEqual(ApplicationStatus.Accepted, historyEntries[0].ToStatus);
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
        var application = MembershipApplication<int>.Submit(
            userId,
            "Seed",
            "Applicant",
            "Tester",
            "ClubBaist",
            "100 Testing Ave",
            "T2T2T2",
            "555-0199",
            "seed@example.com",
            new DateTime(1992, 3, 1),
            MembershipCategory.Social,
            sponsor1Id,
            sponsor2Id,
            submittedAt);

        if (status != ApplicationStatus.Submitted)
        {
            application.ChangeStatus(status, userId, submittedAt);
        }

        return application;
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