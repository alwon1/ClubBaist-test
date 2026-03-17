using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class MemberManagementServiceTests
{
    [TestMethod]
    public async Task CreateMemberAsync_ValidRequest_PersistsMemberAccount()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "Jane",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 5, 20),
            Phone: "555-0100",
            Address: "123 Main St",
            PostalCode: "T1T1T1",
            MembershipCategory: MembershipCategory.Social);

        var result = await memberService.CreateMemberAsync(request);

        Assert.AreNotEqual(0, result.MemberAccountId);
        Assert.IsGreaterThanOrEqualTo(10000, result.MemberNumber);

        var persisted = await dbContext.MemberAccounts
            .AsNoTracking()
            .SingleAsync(item => item.MemberAccountId == result.MemberAccountId);

        Assert.AreEqual(userId, persisted.ApplicationUserId);

        var identityUser = await userManager.FindByIdAsync(userId.ToString());
        Assert.AreEqual("Jane", identityUser!.FirstName);
        Assert.AreEqual("Doe", identityUser.LastName);
        Assert.AreEqual("555-0100", identityUser.Phone);
        Assert.AreEqual("123 Main St", persisted.Address);
        Assert.AreEqual("T1T1T1", persisted.PostalCode);
    }

    [TestMethod]
    public async Task CreateMemberAsync_FieldsWithSurroundingWhitespace_TrimsBeforePersisting()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "  Jane  ",
            LastName: "  Doe  ",
            DateOfBirth: new DateTime(1990, 5, 20),
            Phone: "  555-0100  ",
            Address: "  123 Main St  ",
            PostalCode: "  T1T1T1  ",
            MembershipCategory: MembershipCategory.Social,
            AlternatePhone: "  555-0199  ");

        var result = await memberService.CreateMemberAsync(request);

        var persisted = await dbContext.MemberAccounts
            .AsNoTracking()
            .SingleAsync(item => item.MemberAccountId == result.MemberAccountId);

        var identityUser = await userManager.FindByIdAsync(userId.ToString());
        Assert.AreEqual("Jane", identityUser!.FirstName);
        Assert.AreEqual("Doe", identityUser.LastName);
        Assert.AreEqual("555-0100", identityUser.Phone);
        Assert.AreEqual("123 Main St", persisted.Address);
        Assert.AreEqual("T1T1T1", persisted.PostalCode);
        Assert.AreEqual("555-0199", persisted.AlternatePhone);
    }

    [TestMethod]
    [DataRow("", "LastName", "555-0000", "1 St", "A1A1A1", "FirstName")]
    [DataRow("   ", "LastName", "555-0000", "1 St", "A1A1A1", "FirstName")]
    [DataRow("FirstName", "", "555-0000", "1 St", "A1A1A1", "LastName")]
    [DataRow("FirstName", "   ", "555-0000", "1 St", "A1A1A1", "LastName")]
    [DataRow("FirstName", "LastName", "", "1 St", "A1A1A1", "Phone")]
    [DataRow("FirstName", "LastName", "   ", "1 St", "A1A1A1", "Phone")]
    [DataRow("FirstName", "LastName", "555-0000", "", "A1A1A1", "Address")]
    [DataRow("FirstName", "LastName", "555-0000", "   ", "A1A1A1", "Address")]
    [DataRow("FirstName", "LastName", "555-0000", "1 St", "", "PostalCode")]
    [DataRow("FirstName", "LastName", "555-0000", "1 St", "   ", "PostalCode")]
    public async Task CreateMemberAsync_InvalidProfileField_ThrowsArgumentException(
        string firstName,
        string lastName,
        string phone,
        string address,
        string postalCode,
        string expectedParamFragment)
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: firstName,
            LastName: lastName,
            DateOfBirth: new DateTime(1990, 5, 20),
            Phone: phone,
            Address: address,
            PostalCode: postalCode,
            MembershipCategory: MembershipCategory.Social);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await memberService.CreateMemberAsync(request));

        StringAssert.Contains(ex.ParamName, expectedParamFragment);
    }

    [TestMethod]
    public async Task CreateMemberAsync_DuplicateApplicationUserId_ThrowsInvalidOperationException()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var userId = await CreateIdentityUserAsync(userManager);

        var firstRequest = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "Jane",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 5, 20),
            Phone: "555-0100",
            Address: "123 Main St",
            PostalCode: "T1T1T1",
            MembershipCategory: MembershipCategory.Social);

        await memberService.CreateMemberAsync(firstRequest);

        var duplicateRequest = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "Janet",
            LastName: "Doe",
            DateOfBirth: new DateTime(1991, 1, 10),
            Phone: "555-0110",
            Address: "124 Main St",
            PostalCode: "T2T2T2",
            MembershipCategory: MembershipCategory.Associate);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await memberService.CreateMemberAsync(duplicateRequest));

        StringAssert.Contains(ex.Message, "already exists");
    }

    [TestMethod]
    public async Task CreateMemberAsync_ValidRequest_AssignsMemberRole()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await roleManager.CreateAsync(new IdentityRole<Guid> { Name = AppRoles.Member });

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "Jane",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 5, 20),
            Phone: "555-0100",
            Address: "123 Main St",
            PostalCode: "T1T1T1",
            MembershipCategory: MembershipCategory.Social);

        await memberService.CreateMemberAsync(request);

        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.IsNotNull(user);
        Assert.IsTrue(await userManager.IsInRoleAsync(user, AppRoles.Member));
    }

    [TestMethod]
    public async Task CreateMemberAsync_MultipleMembersCreated_AssignsSequentialMemberNumbers()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var userId1 = await CreateIdentityUserAsync(userManager);
        var userId2 = await CreateIdentityUserAsync(userManager);

        var request1 = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId1,
            FirstName: "First",
            LastName: "Member",
            DateOfBirth: new DateTime(1990, 1, 1),
            Phone: "555-0001",
            Address: "1 Main St",
            PostalCode: "T1T1T1",
            MembershipCategory: MembershipCategory.Social);

        var request2 = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId2,
            FirstName: "Second",
            LastName: "Member",
            DateOfBirth: new DateTime(1991, 2, 2),
            Phone: "555-0002",
            Address: "2 Main St",
            PostalCode: "T2T2T2",
            MembershipCategory: MembershipCategory.Social);

        var result1 = await memberService.CreateMemberAsync(request1);
        var result2 = await memberService.CreateMemberAsync(request2);

        Assert.AreEqual(result1.MemberNumber + 1, result2.MemberNumber);
    }

    private static Task<Guid> CreateIdentityUserAsync(UserManager<ApplicationUser> userManager) =>
        TestDataFactory.CreateIdentityUserAsync(userManager);
}
