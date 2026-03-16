using ClubBaist.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Tests;

/// <summary>
/// Shared test data creation helpers to avoid duplication across test classes.
/// </summary>
public static class TestDataFactory
{
    public static async Task<Guid> CreateIdentityUserAsync(UserManager<IdentityUser<Guid>> userManager)
    {
        var userId = Guid.NewGuid();
        var user = new IdentityUser<Guid>
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            Email = $"user-{userId:N}@example.com"
        };

        var result = await userManager.CreateAsync(user);
        Assert.IsTrue(result.Succeeded, string.Join(",", result.Errors.Select(e => e.Description)));

        return userId;
    }

    public static async Task<Guid> CreateMemberAsync(
        UserManager<IdentityUser<Guid>> userManager,
        ApplicationDbContext dbContext,
        MembershipCategory category = MembershipCategory.Social)
    {
        var userId = await CreateIdentityUserAsync(userManager);
        var nextMemberNumber = (await dbContext.MemberAccounts.AsNoTracking().MaxAsync(m => (int?)m.MemberNumber) ?? 9999) + 1;

        var memberAccountId = Guid.NewGuid();
        dbContext.MemberAccounts.Add(new MemberAccount<Guid>
        {
            MemberAccountId = memberAccountId,
            ApplicationUserId = userId,
            MemberNumber = nextMemberNumber,
            FirstName = "Test",
            LastName = "Member",
            DateOfBirth = new DateTime(1985, 1, 15),
            Email = $"member-{userId:N}@example.com",
            Phone = "555-0000",
            Address = "1 Test St",
            PostalCode = "T0T0T0",
            MembershipCategory = category,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return memberAccountId;
    }
}
