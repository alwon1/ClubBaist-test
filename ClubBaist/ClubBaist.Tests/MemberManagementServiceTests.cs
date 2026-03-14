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

        var memberService = provider.GetRequiredService<MemberManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<int>(
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

        var memberService = provider.GetRequiredService<MemberManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        var userId = await CreateIdentityUserAsync(userManager);
        var createdByUserId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<int>(
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

        var result = await memberService.CreateMemberAsync(request, createdByUserId);

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

        var memberService = provider.GetRequiredService<MemberManagementService<int>>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<int>>>();

        var userId = await CreateIdentityUserAsync(userManager);

        var request = new CreateMemberRequest<int>(
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

    private static async Task<int> CreateIdentityUserAsync(UserManager<IdentityUser<int>> userManager)
    {
        while (true)
        {
            var candidate = Random.Shared.Next(1, int.MaxValue);
            var exists = await userManager.Users.AnyAsync(user => user.Id == candidate);
            if (exists)
            {
                continue;
            }

            var user = new IdentityUser<int>
            {
                Id = candidate,
                UserName = $"user-{candidate}",
                Email = $"user-{candidate}@example.com"
            };

            var createResult = await userManager.CreateAsync(user);
            Assert.IsTrue(createResult.Succeeded, string.Join(",", createResult.Errors.Select(error => error.Description)));

            return candidate;
        }
    }
}
