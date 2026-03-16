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
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "Jane",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 5, 20),
            Email: "jane.doe@example.com",
            Phone: "555-0100",
            Address: "123 Main St",
            PostalCode: "T1T1T1",
            MembershipCategory: MembershipCategory.Social);

        var result = await memberService.CreateMemberAsync(request);

        Assert.AreNotEqual(Guid.Empty, result.MemberAccountId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.MemberNumber));

        var persisted = await dbContext.MemberAccounts
            .AsNoTracking()
            .SingleAsync(item => item.MemberAccountId == result.MemberAccountId);

        Assert.AreEqual(userId, persisted.ApplicationUserId);
        Assert.AreEqual("Jane", persisted.FirstName);
        Assert.AreEqual("Doe", persisted.LastName);
        Assert.AreEqual("jane.doe@example.com", persisted.Email);
        Assert.AreEqual("555-0100", persisted.Phone);
        Assert.AreEqual("123 Main St", persisted.Address);
        Assert.AreEqual("T1T1T1", persisted.PostalCode);
    }

    [TestMethod]
    public async Task CreateMemberAsync_FieldsWithSurroundingWhitespace_TrimsBeforePersisting()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "  Jane  ",
            LastName: "  Doe  ",
            DateOfBirth: new DateTime(1990, 5, 20),
            Email: "  jane.doe@example.com  ",
            Phone: "  555-0100  ",
            Address: "  123 Main St  ",
            PostalCode: "  T1T1T1  ",
            MembershipCategory: MembershipCategory.Social,
            AlternatePhone: "  555-0199  ");

        var result = await memberService.CreateMemberAsync(request);

        var persisted = await dbContext.MemberAccounts
            .AsNoTracking()
            .SingleAsync(item => item.MemberAccountId == result.MemberAccountId);

        Assert.AreEqual("Jane", persisted.FirstName);
        Assert.AreEqual("Doe", persisted.LastName);
        Assert.AreEqual("jane.doe@example.com", persisted.Email);
        Assert.AreEqual("555-0100", persisted.Phone);
        Assert.AreEqual("123 Main St", persisted.Address);
        Assert.AreEqual("T1T1T1", persisted.PostalCode);
        Assert.AreEqual("555-0199", persisted.AlternatePhone);
    }

    [TestMethod]
    [DataRow("", "LastName", "email@example.com", "555-0000", "1 St", "A1A1A1", "FirstName")]
    [DataRow("   ", "LastName", "email@example.com", "555-0000", "1 St", "A1A1A1", "FirstName")]
    [DataRow("FirstName", "", "email@example.com", "555-0000", "1 St", "A1A1A1", "LastName")]
    [DataRow("FirstName", "   ", "email@example.com", "555-0000", "1 St", "A1A1A1", "LastName")]
    [DataRow("FirstName", "LastName", "", "555-0000", "1 St", "A1A1A1", "Email")]
    [DataRow("FirstName", "LastName", "   ", "555-0000", "1 St", "A1A1A1", "Email")]
    [DataRow("FirstName", "LastName", "email@example.com", "", "1 St", "A1A1A1", "Phone")]
    [DataRow("FirstName", "LastName", "email@example.com", "   ", "1 St", "A1A1A1", "Phone")]
    [DataRow("FirstName", "LastName", "email@example.com", "555-0000", "", "A1A1A1", "Address")]
    [DataRow("FirstName", "LastName", "email@example.com", "555-0000", "   ", "A1A1A1", "Address")]
    [DataRow("FirstName", "LastName", "email@example.com", "555-0000", "1 St", "", "PostalCode")]
    [DataRow("FirstName", "LastName", "email@example.com", "555-0000", "1 St", "   ", "PostalCode")]
    public async Task CreateMemberAsync_InvalidProfileField_ThrowsArgumentException(
        string firstName,
        string lastName,
        string email,
        string phone,
        string address,
        string postalCode,
        string expectedParamFragment)
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var memberService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: firstName,
            LastName: lastName,
            DateOfBirth: new DateTime(1990, 5, 20),
            Email: email,
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
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<Guid>>>();

        var userId = await CreateIdentityUserAsync(userManager);

        var firstRequest = new CreateMemberRequest<Guid>(
            ApplicationUserId: userId,
            FirstName: "Jane",
            LastName: "Doe",
            DateOfBirth: new DateTime(1990, 5, 20),
            Email: "jane.doe@example.com",
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
            Email: "janet.doe@example.com",
            Phone: "555-0110",
            Address: "124 Main St",
            PostalCode: "T2T2T2",
            MembershipCategory: MembershipCategory.Associate);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await memberService.CreateMemberAsync(duplicateRequest));

        StringAssert.Contains(ex.Message, "already exists");
    }

    private static Task<Guid> CreateIdentityUserAsync(UserManager<IdentityUser<Guid>> userManager) =>
        TestDataFactory.CreateIdentityUserAsync(userManager);
}
