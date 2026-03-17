using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class ServiceSetupTests
{
    [TestMethod]
    public async Task SqliteInMemoryAndDependencyInjectionAreConfigured()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        var userManager = provider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var memberManagementService = provider.GetRequiredService<MemberManagementService<Guid>>();
        var applicationManagementService = provider.GetRequiredService<ApplicationManagementService<Guid>>();

        Assert.IsNotNull(dbContext);
        Assert.IsNotNull(userManager);
        Assert.IsNotNull(memberManagementService);
        Assert.IsNotNull(applicationManagementService);

        var user = new IdentityUser<Guid>
        {
            UserName = $"user-{Guid.NewGuid():N}",
            Email = "setup-test@clubbaist.local"
        };

        var identityResult = await userManager.CreateAsync(user);
        Assert.IsTrue(identityResult.Succeeded, string.Join(",", identityResult.Errors.Select(error => error.Description)));

        var createMemberRequest = new CreateMemberRequest<Guid>(
            user.Id,
            "Test",
            "Member",
            new DateTime(1990, 1, 1),
            "setup-test@clubbaist.local",
            "780-555-0101",
            "123 Main St",
            "T0T0T0",
            ClubBaist.Domain.MembershipCategory.Social);

        var createMemberResult = await memberManagementService.CreateMemberAsync(createMemberRequest);

        Assert.AreNotEqual(0, createMemberResult.MemberAccountId);
        Assert.IsTrue(await dbContext.MemberAccounts.AnyAsync(member => member.MemberAccountId == createMemberResult.MemberAccountId));
    }
}
