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

        var applicationService = provider.GetRequiredService<ApplicationManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var request = new SubmitApplicationRequest<Guid>(
            NewUserEmail: "applicant@test.com",
            NewUserPassword: "Password123!",
            FirstName: "Jane",
            LastName: "Doe",
            Occupation: "Engineer",
            CompanyName: "Acme Corp",
            Address: "123 Main St",
            PostalCode: "T1T1T1",
            Phone: "555-0100",
            DateOfBirth: new DateTime(1990, 5, 20),
            RequestedMembershipCategory: MembershipCategory.Social,
            Sponsor1MemberId: sponsor1Id,
            Sponsor2MemberId: sponsor2Id,
            AlternatePhone: "555-0101",
            SubmittedAt: submittedAt);

        var result = await applicationService.SubmitApplicationAsync(request);

        Assert.AreNotEqual(Guid.Empty, result.MembershipApplication.ApplicationId);
        Assert.AreEqual(ApplicationStatus.Submitted, result.MembershipApplication.CurrentStatus);

        var persisted = await dbContext.MembershipApplications
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationId == result.MembershipApplication.ApplicationId);

        Assert.AreEqual(ApplicationStatus.Submitted, persisted.CurrentStatus);
        Assert.AreEqual(result.ApplicationUser.Id, persisted.ApplicationUserId);
        Assert.AreEqual("Jane", persisted.FirstName);
        Assert.AreEqual("Doe", persisted.LastName);
        Assert.AreEqual(submittedAt, persisted.SubmittedAt);
    }

    [TestMethod]
    public async Task GetActionableApplicationsAsync_ReturnsOnlyActionableStatuses()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

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

        var applicationService = provider.GetRequiredService<ApplicationManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        await ResetDatabaseAsync(dbContext);

        var changedByUserId = await CreateIdentityUserAsync(userManager);
        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);
        var submitRequest = new SubmitApplicationRequest<Guid>(
            NewUserEmail: "applicant@test.com",
            NewUserPassword: "Password123!",
            FirstName: "Alex",
            LastName: "Applicant",
            Occupation: "Analyst",
            CompanyName: "ClubBaist",
            Address: "500 Main St",
            PostalCode: "T3T3T3",
            Phone: "555-0300",
            DateOfBirth: new DateTime(1991, 4, 10),
            RequestedMembershipCategory: MembershipCategory.Social,
            Sponsor1MemberId: sponsor1Id,
            Sponsor2MemberId: sponsor2Id,
            SubmittedAt: submittedAt);

        var submittedApplication = await applicationService.SubmitApplicationAsync(submitRequest);

        var changedAt = new DateTime(2026, 2, 16, 10, 15, 0, DateTimeKind.Utc);
        var result = await applicationService.ChangeApplicationStatusAsync(
            submittedApplication.MembershipApplication.ApplicationId,
            ApplicationStatus.OnHold,
            changedByUserId,
            changedAt);

        Assert.AreEqual(submittedApplication.MembershipApplication.ApplicationId, result.ApplicationId);
        Assert.AreEqual(ApplicationStatus.OnHold, result.CurrentStatus);
        Assert.AreEqual(changedAt, result.LastStatusChangedAt);
        Assert.IsNull(result.MemberCreationResult);

        var persistedApplication = await dbContext.MembershipApplications
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationId == submittedApplication.MembershipApplication.ApplicationId);

        Assert.AreEqual(ApplicationStatus.OnHold, persistedApplication.CurrentStatus);
        Assert.AreEqual(changedAt, persistedApplication.LastStatusChangedAt);

        var historyEntries = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(item => item.MembershipApplicationId == submittedApplication.MembershipApplication.ApplicationId)
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

        var applicationService = provider.GetRequiredService<ApplicationManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        await ResetDatabaseAsync(dbContext);

        var changedByUserId = await CreateIdentityUserAsync(userManager);
        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 2, 20, 9, 0, 0, DateTimeKind.Utc);
        var submitted = await applicationService.SubmitApplicationAsync(
            new SubmitApplicationRequest<Guid>(
                NewUserEmail: "applicant@test.com",
                NewUserPassword: "Password123!",
                FirstName: "Sam",
                LastName: "Submitter",
                Occupation: "Coordinator",
                CompanyName: "ClubBaist",
                Address: "900 Service Rd",
                PostalCode: "T4T4T4",
                Phone: "555-0400",
                DateOfBirth: new DateTime(1993, 7, 12),
                RequestedMembershipCategory: MembershipCategory.Social,
                Sponsor1MemberId: sponsor1Id,
                Sponsor2MemberId: sponsor2Id,
                SubmittedAt: submittedAt));

        var changedAt = new DateTime(2026, 2, 21, 11, 45, 0, DateTimeKind.Utc);
        var history = await applicationService.RecordStatusHistoryAsync(
            submitted.MembershipApplication.ApplicationId,
            ApplicationStatus.Submitted,
            ApplicationStatus.Waitlisted,
            changedByUserId,
            changedAt);

        Assert.AreEqual(submitted.MembershipApplication.ApplicationId, history.MembershipApplicationId);
        Assert.AreEqual(ApplicationStatus.Submitted, history.FromStatus);
        Assert.AreEqual(ApplicationStatus.Waitlisted, history.ToStatus);
        Assert.AreEqual(changedByUserId, history.ChangedByUserId);
        Assert.AreEqual(changedAt, history.ChangedAt);

        var persistedHistory = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(item => item.MembershipApplicationId == submitted.MembershipApplication.ApplicationId)
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

        var applicationService = provider.GetRequiredService<ApplicationManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        await ResetDatabaseAsync(dbContext);

        var changedByUserId = await CreateIdentityUserAsync(userManager);
        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submittedAt = new DateTime(2026, 2, 22, 9, 0, 0, DateTimeKind.Utc);
        var submitted = await applicationService.SubmitApplicationAsync(
            new SubmitApplicationRequest<Guid>(
                NewUserEmail: "applicant@test.com",
                NewUserPassword: "Password123!",
                FirstName: "Taylor",
                LastName: "Transition",
                Occupation: "Planner",
                CompanyName: "ClubBaist",
                Address: "1200 Process Blvd",
                PostalCode: "T5T5T5",
                Phone: "555-0500",
                DateOfBirth: new DateTime(1990, 9, 5),
                RequestedMembershipCategory: MembershipCategory.Social,
                Sponsor1MemberId: sponsor1Id,
                Sponsor2MemberId: sponsor2Id,
                SubmittedAt: submittedAt));

        var acceptedAt = new DateTime(2026, 2, 23, 10, 0, 0, DateTimeKind.Utc);
        var acceptedResult = await applicationService.ChangeApplicationStatusAsync(
            submitted.MembershipApplication.ApplicationId,
            ApplicationStatus.Accepted,
            changedByUserId,
            acceptedAt);

        Assert.AreEqual(ApplicationStatus.Accepted, acceptedResult.CurrentStatus);

        var invalidChangedAt = acceptedAt.AddHours(2);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await applicationService.ChangeApplicationStatusAsync(
                submitted.MembershipApplication.ApplicationId,
                ApplicationStatus.OnHold,
                changedByUserId,
                invalidChangedAt));

        var persistedApplication = await dbContext.MembershipApplications
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationId == submitted.MembershipApplication.ApplicationId);

        Assert.AreEqual(ApplicationStatus.Accepted, persistedApplication.CurrentStatus);
        Assert.AreEqual(acceptedAt, persistedApplication.LastStatusChangedAt);

        var historyEntries = await dbContext.ApplicationStatusHistories
            .AsNoTracking()
            .Where(item => item.MembershipApplicationId == submitted.MembershipApplication.ApplicationId)
            .OrderBy(item => item.ChangedAt)
            .ToListAsync();

        Assert.HasCount(1, historyEntries);
        Assert.AreEqual(ApplicationStatus.Submitted, historyEntries[0].FromStatus);
        Assert.AreEqual(ApplicationStatus.Accepted, historyEntries[0].ToStatus);
    }

    [TestMethod]
    public async Task RecordStatusHistoryAsync_UnknownApplication_ThrowsKeyNotFoundException()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var changedByUserId = await CreateIdentityUserAsync(userManager);
        var nonExistentApplicationId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await applicationService.RecordStatusHistoryAsync(
                nonExistentApplicationId,
                ApplicationStatus.Submitted,
                ApplicationStatus.OnHold,
                changedByUserId,
                DateTime.UtcNow));
    }

    [TestMethod]
    public async Task RecordStatusHistoryAsync_UnknownChangedByUser_ThrowsInvalidOperationException()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var applicationService = provider.GetRequiredService<ApplicationManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var sponsor1Id = await CreateSponsorMemberAsync(userManager, dbContext);
        var sponsor2Id = await CreateSponsorMemberAsync(userManager, dbContext);

        var submitted = await applicationService.SubmitApplicationAsync(
            new SubmitApplicationRequest<Guid>(
                NewUserEmail: "applicant@test.com",
                NewUserPassword: "Password123!",
                FirstName: "Robin",
                LastName: "History",
                Occupation: "Tester",
                CompanyName: "ClubBaist",
                Address: "300 History Rd",
                PostalCode: "T6T6T6",
                Phone: "555-0600",
                DateOfBirth: new DateTime(1994, 2, 28),
                RequestedMembershipCategory: MembershipCategory.Social,
                Sponsor1MemberId: sponsor1Id,
                Sponsor2MemberId: sponsor2Id,
                SubmittedAt: DateTime.UtcNow));

        var nonExistentUserId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await applicationService.RecordStatusHistoryAsync(
                submitted.MembershipApplication.ApplicationId,
                ApplicationStatus.Submitted,
                ApplicationStatus.OnHold,
                nonExistentUserId,
                DateTime.UtcNow));
    }

    private static Task<Guid> CreateIdentityUserAsync(UserManager<ApplicationUser> userManager) =>
        TestDataFactory.CreateIdentityUserAsync(userManager);

    private static Task<int> CreateSponsorMemberAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext) =>
        TestDataFactory.CreateMemberAsync(userManager, dbContext);

    private static MembershipApplication<Guid> CreateApplication(
        Guid userId,
        ApplicationStatus status,
        DateTime submittedAt,
        int sponsor1Id,
        int sponsor2Id)
    {
        var application = MembershipApplication<Guid>.Submit(
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

    private static async Task ResetDatabaseAsync(ApplicationDbContext dbContext)
    {
        await dbContext.ApplicationStatusHistories.ExecuteDeleteAsync();
        await dbContext.MemberAccounts.ExecuteDeleteAsync();
        await dbContext.MembershipApplications.ExecuteDeleteAsync();
        await dbContext.UserRoles.ExecuteDeleteAsync();
        await dbContext.Users.ExecuteDeleteAsync();
    }
}
