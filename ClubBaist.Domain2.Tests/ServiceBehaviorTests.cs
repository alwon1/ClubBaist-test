using System.Data;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Domain2.Entities.Scoring;
using ClubBaist.Services2;
using ClubBaist.Services2.Membership;
using ClubBaist.Services2.Membership.Applications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class MembershipApplicationServiceTests
{
    [TestMethod]
    public async Task SubmitMembershipApplicationAsync_PersistsApplication_WithSubmittedStatus()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "applicant@test.com", firstName: "Jane", lastName: "Doe");

        var result = await applicationService.SubmitMembershipApplicationAsync(application);

        Assert.IsTrue(result);

        var persisted = await db.MembershipApplications.AsNoTracking().SingleAsync(item => item.Email == "applicant@test.com");
        Assert.AreEqual(ApplicationStatus.Submitted, persisted.Status);
        Assert.AreEqual("Jane", persisted.FirstName);
        Assert.AreEqual("Doe", persisted.LastName);
        Assert.AreEqual(associate.Id, persisted.RequestedMembershipLevelId);
    }

    [TestMethod]
    public async Task SubmitMembershipApplicationAsync_DuplicateActiveApplication_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var first = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "duplicate@test.com");
        var second = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "duplicate@test.com", firstName: "Second");

        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(first));
        Assert.IsFalse(await applicationService.SubmitMembershipApplicationAsync(second));

        Assert.AreEqual(1, await db.MembershipApplications.CountAsync(item => item.Email == "duplicate@test.com"));
    }

    [TestMethod]
    public async Task ApproveMembershipApplicationAsync_CreatesUserAndMembership_AndMarksAccepted()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "approved@test.com", firstName: "Alex", lastName: "Approved");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));

        var persisted = await db.MembershipApplications.SingleAsync(item => item.Email == "approved@test.com");
        var approved = await applicationService.ApproveMembershipApplicationAsync(persisted.Id, associate.Id);

        Assert.IsTrue(approved.Success);
        Assert.IsNotNull(approved.GeneratedPassword);

        // Verify password strength policy
        var pwd = approved.GeneratedPassword!;
        Assert.AreEqual(16, pwd.Length, "Generated password must be 16 characters.");
        Assert.IsTrue(pwd.Any(char.IsUpper), "Generated password must contain at least one uppercase letter.");
        Assert.IsTrue(pwd.Any(char.IsLower), "Generated password must contain at least one lowercase letter.");
        Assert.IsTrue(pwd.Any(char.IsDigit), "Generated password must contain at least one digit.");
        Assert.IsTrue(pwd.Any(c => "!@#$%&*?".Contains(c)), "Generated password must contain at least one special character.");

        var updatedApplication = await db.MembershipApplications.AsNoTracking().SingleAsync(item => item.Id == persisted.Id);
        var createdUser = await userManager.FindByEmailAsync("approved@test.com");
        var membership = await db.MemberShips
            .Include(item => item.MembershipLevel)
            .Include(item => item.User)
            .SingleAsync(item => item.User.Email == "approved@test.com");

        Assert.AreEqual(ApplicationStatus.Accepted, updatedApplication.Status);
        Assert.IsNotNull(createdUser);
        Assert.AreEqual(associate.Id, membership.MembershipLevel.Id);
    }

    [TestMethod]
    public async Task SetApplicationStatusAsync_NonTerminalStatus_UpdatesApplication()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "onhold@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));

        var persisted = await db.MembershipApplications.SingleAsync(item => item.Email == "onhold@test.com");
        var updated = await applicationService.SetApplicationStatusAsync(persisted.Id, ApplicationStatus.OnHold);

        Assert.IsTrue(updated);
        Assert.AreEqual(ApplicationStatus.OnHold, (await db.MembershipApplications.AsNoTracking().SingleAsync(item => item.Id == persisted.Id)).Status);
        Assert.IsFalse(await applicationService.SetApplicationStatusAsync(persisted.Id, ApplicationStatus.Accepted));
    }

    [TestMethod]
    public async Task ConfirmSponsorEndorsementAsync_WhenSponsorMatches_RecordsConfirmation()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "endorse@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));

        var persisted = await db.MembershipApplications.SingleAsync(item => item.Email == "endorse@test.com");

        Assert.IsTrue(await applicationService.ConfirmSponsorEndorsementAsync(persisted.Id, sponsor1.Id));
        Assert.IsTrue(await applicationService.ConfirmSponsorEndorsementAsync(persisted.Id, sponsor2.Id));

        var updated = await db.MembershipApplications.AsNoTracking().SingleAsync(item => item.Id == persisted.Id);
        Assert.IsTrue(updated.Sponsor1EndorsementConfirmed);
        Assert.IsTrue(updated.Sponsor2EndorsementConfirmed);
        Assert.IsTrue(updated.Sponsor1EndorsementConfirmedAt.HasValue);
        Assert.IsTrue(updated.Sponsor2EndorsementConfirmedAt.HasValue);
    }

    [TestMethod]
    public async Task ConfirmSponsorEndorsementAsync_WhenMemberIsNotSponsor_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");
        var unrelated = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "unrelated@test.com", "Unrelated", "Member");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "notsponsor@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));

        var persisted = await db.MembershipApplications.SingleAsync(item => item.Email == "notsponsor@test.com");
        Assert.IsFalse(await applicationService.ConfirmSponsorEndorsementAsync(persisted.Id, unrelated.Id));
    }
}

[TestClass]
public class MemberClaimSynchroniserTests
{
    [TestMethod]
    public async Task ApproveMembershipApplicationAsync_ShareholderLevel_GrantsShareholderAndBookingClaims()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholderLevel = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder", MemberType.Shareholder);
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(shareholderLevel, sponsor1.Id, sponsor2.Id, email: "sh-applicant@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));
        var persisted = await db.MembershipApplications.SingleAsync(a => a.Email == "sh-applicant@test.com");

        var result = await applicationService.ApproveMembershipApplicationAsync(persisted.Id, shareholderLevel.Id);

        Assert.IsTrue(result.Success);
        var createdUser = await userManager.FindByEmailAsync("sh-applicant@test.com");
        Assert.IsNotNull(createdUser);
        var claims = await userManager.GetClaimsAsync(createdUser);

        Assert.IsTrue(claims.Any(c => c.Type == AppRoles.ClaimTypes.Permission && c.Value == AppRoles.Permissions.BookStandingTeeTime),
            "Shareholder should have the standing-tee-time.book permission claim.");
        Assert.IsTrue(claims.Any(c => c.Type == AppRoles.ClaimTypes.MembershipFact && c.Value == AppRoles.MembershipFacts.Shareholder),
            "Shareholder should have the clubbaist.membership=shareholder claim.");
        Assert.IsFalse(claims.Any(c => c.Type == AppRoles.ClaimTypes.MembershipFact && c.Value == AppRoles.MembershipFacts.CopperTier),
            "Shareholder should NOT have the copper-tier claim.");
    }

    [TestMethod]
    public async Task ApproveMembershipApplicationAsync_CopperLevel_GrantsCopperTierClaimOnly()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholderLevel = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder", MemberType.Shareholder);
        var copperLevel = await Domain2TestData.CreateMembershipLevelAsync(db, "CP", "Social", MemberType.Associate, openingHour: 7, closingHour: 19);
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(copperLevel, sponsor1.Id, sponsor2.Id, email: "cp-applicant@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));
        var persisted = await db.MembershipApplications.SingleAsync(a => a.Email == "cp-applicant@test.com");

        var result = await applicationService.ApproveMembershipApplicationAsync(persisted.Id, copperLevel.Id);

        Assert.IsTrue(result.Success);
        var createdUser = await userManager.FindByEmailAsync("cp-applicant@test.com");
        Assert.IsNotNull(createdUser);
        var claims = await userManager.GetClaimsAsync(createdUser);

        Assert.IsTrue(claims.Any(c => c.Type == AppRoles.ClaimTypes.MembershipFact && c.Value == AppRoles.MembershipFacts.CopperTier),
            "Copper-tier member should have the copper-tier claim.");
        Assert.IsFalse(claims.Any(c => c.Type == AppRoles.ClaimTypes.Permission && c.Value == AppRoles.Permissions.BookStandingTeeTime),
            "Copper-tier member should NOT have the standing-tee-time.book claim.");
        Assert.IsFalse(claims.Any(c => c.Type == AppRoles.ClaimTypes.MembershipFact && c.Value == AppRoles.MembershipFacts.Shareholder),
            "Copper-tier member should NOT have the shareholder claim.");
    }

    [TestMethod]
    public async Task ApproveMembershipApplicationAsync_AssociateLevel_GrantsNoClaims()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholderLevel = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder", MemberType.Shareholder);
        var associateLevel = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associateLevel, sponsor1.Id, sponsor2.Id, email: "as-applicant@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));
        var persisted = await db.MembershipApplications.SingleAsync(a => a.Email == "as-applicant@test.com");

        var result = await applicationService.ApproveMembershipApplicationAsync(persisted.Id, associateLevel.Id);

        Assert.IsTrue(result.Success);
        var createdUser = await userManager.FindByEmailAsync("as-applicant@test.com");
        Assert.IsNotNull(createdUser);
        var claims = await userManager.GetClaimsAsync(createdUser);

        Assert.IsFalse(claims.Any(c => c.Type == AppRoles.ClaimTypes.Permission && c.Value == AppRoles.Permissions.BookStandingTeeTime),
            "Associate member should NOT have the standing-tee-time.book claim.");
        Assert.IsFalse(claims.Any(c => c.Type == AppRoles.ClaimTypes.MembershipFact),
            "Associate member should have no membership-fact claims.");
    }

    [TestMethod]
    public async Task ApproveMembershipApplicationAsync_ShareholderSpouseLevel_GrantsStandingTeeTimeClaim()
    {
        // Shareholder Spouse (SS) has MemberType.Shareholder — claims should be granted by MemberType, not ShortCode.
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholderLevel = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder", MemberType.Shareholder);
        var shareholderSpouseLevel = await Domain2TestData.CreateMembershipLevelAsync(db, "SS", "Shareholder Spouse", MemberType.Shareholder);
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholderLevel, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(shareholderSpouseLevel, sponsor1.Id, sponsor2.Id, email: "ss-applicant@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));
        var persisted = await db.MembershipApplications.SingleAsync(a => a.Email == "ss-applicant@test.com");

        var result = await applicationService.ApproveMembershipApplicationAsync(persisted.Id, shareholderSpouseLevel.Id);

        Assert.IsTrue(result.Success);
        var createdUser = await userManager.FindByEmailAsync("ss-applicant@test.com");
        Assert.IsNotNull(createdUser);
        var claims = await userManager.GetClaimsAsync(createdUser);

        Assert.IsTrue(claims.Any(c => c.Type == AppRoles.ClaimTypes.Permission && c.Value == AppRoles.Permissions.BookStandingTeeTime),
            "Shareholder Spouse should have the standing-tee-time.book permission claim.");
        Assert.IsTrue(claims.Any(c => c.Type == AppRoles.ClaimTypes.MembershipFact && c.Value == AppRoles.MembershipFacts.Shareholder),
            "Shareholder Spouse should have the clubbaist.membership=shareholder claim.");
        Assert.IsFalse(claims.Any(c => c.Type == AppRoles.ClaimTypes.MembershipFact && c.Value == AppRoles.MembershipFacts.CopperTier),
            "Shareholder Spouse should NOT have the copper-tier claim.");
    }
}

[TestClass]
public class MembershipServiceTests
{
    [TestMethod]
    public async Task SetMembershipLevelForUserAsync_ExistingMembership_UpdatesLevel()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var membershipService = provider.GetRequiredService<MembershipService>();

        var bronze = await Domain2TestData.CreateMembershipLevelAsync(db, "BR", "Bronze");
        var silver = await Domain2TestData.CreateMembershipLevelAsync(db, "SV", "Silver");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, bronze, "member@test.com", "Member", "User");

        var result = await membershipService.SetMembershipLevelForUserAsync(member.User, silver.Id);
        var updatedLevel = await membershipService.GetMembershipLevelForUserAsync(member.User.Id);

        Assert.IsTrue(result);
        Assert.IsNotNull(updatedLevel);
        Assert.AreEqual(silver.Id, updatedLevel.Id);
    }
}

[TestClass]
public class MembershipLevelServiceTests
{
    [TestMethod]
    public async Task CreateMembershipLevelAsync_PersistsMembershipLevel()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var service = provider.GetRequiredService<MembershipLevelService>();

        var created = await service.CreateMembershipLevelAsync("Intermediate", "INT");
        var persisted = await db.MembershipLevels.AsNoTracking().SingleAsync(level => level.ShortCode == "INT");

        Assert.IsTrue(created);
        Assert.AreEqual("Intermediate", persisted.Name);
        Assert.AreEqual("INT", persisted.ShortCode);
    }

    [TestMethod]
    public async Task UpdateMembershipLevelAsync_WithEntityOverload_UpdatesValues()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var service = provider.GetRequiredService<MembershipLevelService>();

        var original = await Domain2TestData.CreateMembershipLevelAsync(db, "JR", "Junior");
        var updatedLevel = new MembershipLevel
        {
            Id = original.Id,
            Name = "Senior",
            ShortCode = "SR"
        };

        var updated = await service.UpdateMembershipLevelAsync(updatedLevel);
        var persisted = await db.MembershipLevels.AsNoTracking().SingleAsync(level => level.Id == original.Id);

        Assert.IsTrue(updated);
        Assert.AreEqual("Senior", persisted.Name);
        Assert.AreEqual("SR", persisted.ShortCode);
    }

    [TestMethod]
    public async Task UpdateMembershipLevelAsync_MissingMembershipLevel_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var service = provider.GetRequiredService<MembershipLevelService>();

        var updated = await service.UpdateMembershipLevelAsync(9999, "Missing", "MS");

        Assert.IsFalse(updated);
    }

    [TestMethod]
    public async Task CreateMembershipLevelAsync_WhenSaveFails_Rethrows()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var service = new MembershipLevelService(new ThrowingAppDbContext2(db, throwOnSave: true));

        try
        {
            await service.CreateMembershipLevelAsync("Failing Level", "FL");
            Assert.Fail("Expected InvalidOperationException to be thrown.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public async Task UpdateMembershipLevelAsync_WhenSaveFails_Rethrows()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var original = await Domain2TestData.CreateMembershipLevelAsync(db, "TMP", "Temp");
        var service = new MembershipLevelService(new ThrowingAppDbContext2(db, throwOnSave: true));

        try
        {
            await service.UpdateMembershipLevelAsync(original.Id, "Updated Temp", "UT");
            Assert.Fail("Expected InvalidOperationException to be thrown.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed class ThrowingAppDbContext2(AppDbContext inner, bool throwOnSave) : IAppDbContext2
    {
        public DbSet<TeeTimeSlot> TeeTimeSlots => inner.TeeTimeSlots;
        public DbSet<TeeTimeBooking> TeeTimeBookings => inner.TeeTimeBookings;
        public DbSet<MemberShipInfo> MemberShips => inner.MemberShips;
        public DbSet<MembershipLevel> MembershipLevels => inner.MembershipLevels;
        public DbSet<MembershipApplication> MembershipApplications => inner.MembershipApplications;
        public DbSet<MembershipLevelTeeTimeAvailability> MembershipLevelTeeTimeAvailabilities => inner.MembershipLevelTeeTimeAvailabilities;
        public DbSet<SpecialEvent> SpecialEvents => inner.SpecialEvents;
        public DbSet<Season> Seasons => inner.Seasons;
        public DbSet<StandingTeeTime> StandingTeeTimes => inner.StandingTeeTimes;
        public DbSet<GolfRound> GolfRounds => inner.GolfRounds;
        public DbSet<CourseRating> CourseRatings => inner.CourseRatings;
        public DbSet<CourseHole> CourseHoles => inner.CourseHoles;
        public DbSet<PlayingConditionAdjustment> PlayingConditionAdjustments => inner.PlayingConditionAdjustments;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throwOnSave
                ? throw new InvalidOperationException("Simulated save failure")
                : inner.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default) =>
            inner.BeginTransactionAsync(isolationLevel, cancellationToken);

        public IExecutionStrategy CreateExecutionStrategy() => inner.CreateExecutionStrategy();
    }
}

[TestClass]
public class BookingServiceTests
{
    [TestMethod]
    public async Task UpdateBookingAsync_ExistingBooking_ReplacesAdditionalParticipants()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var bookingService = provider.GetRequiredService<BookingService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker@test.com", "Booker", "User");
        var firstParticipant = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "player1@test.com", "Player", "One");
        var secondParticipant = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "player2@test.com", "Player", "Two");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(
            seasonService,
            db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(8, 0));

        var booking = new TeeTimeBooking
        {
            TeeTimeSlotStart = slot.Start,
            TeeTimeSlot = slot,
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            AdditionalParticipants = [firstParticipant]
        };
        db.TeeTimeBookings.Add(booking);
        await db.SaveChangesAsync();

        var updated = await bookingService.UpdateBookingAsync(booking.Id, [secondParticipant]);
        var persisted = await db.TeeTimeBookings
            .Include(item => item.AdditionalParticipants)
            .SingleAsync(item => item.Id == booking.Id);

        Assert.IsTrue(updated);
        Assert.HasCount(1, persisted.AdditionalParticipants);
        Assert.AreEqual(secondParticipant.Id, persisted.AdditionalParticipants[0].Id);
    }

    [TestMethod]
    public async Task UpdateBookingAsync_WhenParticipantHasNearbyBooking_ReturnsFalse_AndLeavesExistingParticipants()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var bookingService = provider.GetRequiredService<BookingService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker@test.com", "Booker", "User");
        var existingParticipant = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "player1@test.com", "Player", "One");
        var conflictingParticipant = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "player2@test.com", "Player", "Two");
        var secondBookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker2@test.com", "Booker", "Two");

        var (_, firstSlot) = await Domain2TestData.CreateSeasonAndSlotAsync(
            seasonService,
            db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(8, 0));

        var nearbySlot = await db.TeeTimeSlots
            .Where(item => item.SeasonId == firstSlot.SeasonId && item.Start > firstSlot.Start)
            .OrderBy(item => item.Start)
            .FirstAsync();

        var originalBooking = new TeeTimeBooking
        {
            TeeTimeSlotStart = firstSlot.Start,
            TeeTimeSlot = firstSlot,
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            AdditionalParticipants = [existingParticipant]
        };

        var nearbyBooking = new TeeTimeBooking
        {
            TeeTimeSlotStart = nearbySlot.Start,
            TeeTimeSlot = nearbySlot,
            BookingMemberId = secondBookingMember.Id,
            BookingMember = secondBookingMember,
            AdditionalParticipants = [conflictingParticipant]
        };

        db.TeeTimeBookings.AddRange(originalBooking, nearbyBooking);
        await db.SaveChangesAsync();

        var updated = await bookingService.UpdateBookingAsync(originalBooking.Id, [conflictingParticipant]);
        var persisted = await db.TeeTimeBookings
            .Include(item => item.AdditionalParticipants)
            .SingleAsync(item => item.Id == originalBooking.Id);

        Assert.IsFalse(updated);
        Assert.HasCount(1, persisted.AdditionalParticipants);
        Assert.AreEqual(existingParticipant.Id, persisted.AdditionalParticipants[0].Id);
    }

    [TestMethod]
    public async Task CreateBooking_OutsideSeason_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var bookingService = provider.GetRequiredService<BookingService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker@test.com", "Booker", "User");
        var season = new Season
        {
            Name = "2026 Season",
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2026, 9, 30)
        };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();

        var offSeason = new TeeTimeSlot
        {
            Start = new DateTime(2026, 1, 15, 10, 0, 0),
            Duration = TimeSpan.FromMinutes(7),
            SeasonId = season.Id,
            Season = season
        };
        db.TeeTimeSlots.Add(offSeason);
        await db.SaveChangesAsync();

        var booking = new TeeTimeBooking
        {
            TeeTimeSlotStart = offSeason.Start,
            TeeTimeSlot = offSeason,
            BookingMemberId = member.Id,
            BookingMember = member
        };

        var created = await bookingService.CreateBookingAsync(booking);

        Assert.IsFalse(created);
    }

    [TestMethod]
    public async Task CancelBookingAsync_ExistingBooking_RemovesBooking()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var bookingService = provider.GetRequiredService<BookingService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker@test.com", "Booker", "User");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(
            seasonService,
            db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(8, 0));

        var booking = new TeeTimeBooking
        {
            TeeTimeSlotStart = slot.Start,
            TeeTimeSlot = slot,
            BookingMemberId = member.Id,
            BookingMember = member
        };
        slot.Bookings.Add(booking);
        await db.SaveChangesAsync();

        Assert.IsTrue(await bookingService.CancelBookingAsync(booking.Id));
        Assert.HasCount(0, await db.TeeTimeBookings.ToListAsync());
    }
}