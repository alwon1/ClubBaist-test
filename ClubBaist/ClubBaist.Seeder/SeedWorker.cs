using ClubBaist.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Seeder;

public class SeedWorker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    ILogger<SeedWorker> logger) : BackgroundService
{
    private const string DefaultPassword = "Pass@word1";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        logger.LogInformation("Creating database schema...");
        await db.Database.EnsureCreatedAsync(stoppingToken);

        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(userManager, db);
        await SeedSeasonAsync(db, stoppingToken);

        logger.LogInformation("Seeding complete. Stopping seeder.");
        lifetime.StopApplication();
    }

    private async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        string[] roles = ["Admin", "MembershipCommittee", "Member"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
                logger.LogInformation("Created role: {Role}", role);
            }
        }
    }

    private async Task SeedUsersAsync(UserManager<IdentityUser<Guid>> userManager, ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;

        // Admin
        await CreateUserWithRoleAsync(userManager, "admin@clubbaist.com", "Admin");

        // Membership Committee
        await CreateUserWithRoleAsync(userManager, "committee@clubbaist.com", "MembershipCommittee");

        // Shareholder members (Gold) - need at least 3 for sponsorship
        var sh1 = await CreateUserWithRoleAsync(userManager, "shareholder1@clubbaist.com", "Member");
        var sh2 = await CreateUserWithRoleAsync(userManager, "shareholder2@clubbaist.com", "Member");
        var sh3 = await CreateUserWithRoleAsync(userManager, "shareholder3@clubbaist.com", "Member");

        // Silver member
        var silver = await CreateUserWithRoleAsync(userManager, "silver@clubbaist.com", "Member");

        // Bronze member
        var bronze = await CreateUserWithRoleAsync(userManager, "bronze@clubbaist.com", "Member");

        // Create MemberAccounts if they don't exist yet
        if (!await db.MemberAccounts.AnyAsync())
        {
            var memberNumber = 1000;

            db.MemberAccounts.AddRange(
                CreateMember(sh1!.Id, $"SH-{memberNumber++}", "Alice", "Shareholder", "shareholder1@clubbaist.com", MembershipCategory.Shareholder, now),
                CreateMember(sh2!.Id, $"SH-{memberNumber++}", "Bob", "Shareholder", "shareholder2@clubbaist.com", MembershipCategory.Shareholder, now),
                CreateMember(sh3!.Id, $"SH-{memberNumber++}", "Carol", "Shareholder", "shareholder3@clubbaist.com", MembershipCategory.Shareholder, now),
                CreateMember(silver!.Id, $"SH-{memberNumber++}", "Diana", "Silver", "silver@clubbaist.com", MembershipCategory.ShareholderSpouse, now),
                CreateMember(bronze!.Id, $"SH-{memberNumber++}", "Evan", "Bronze", "bronze@clubbaist.com", MembershipCategory.Junior, now)
            );

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} member accounts", 5);
        }
    }

    private async Task<IdentityUser<Guid>?> CreateUserWithRoleAsync(
        UserManager<IdentityUser<Guid>> userManager, string email, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser<Guid>
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, DefaultPassword);
            if (!result.Succeeded)
            {
                logger.LogError("Failed to create user {Email}: {Errors}",
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return null;
            }
            logger.LogInformation("Created user: {Email}", email);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static MemberAccount<Guid> CreateMember(
        Guid userId, string memberNumber, string firstName, string lastName,
        string email, MembershipCategory category, DateTime now)
    {
        return new MemberAccount<Guid>
        {
            ApplicationUserId = userId,
            MemberNumber = memberNumber,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateTime(1985, 1, 15),
            Email = email,
            Phone = "403-555-0100",
            Address = "123 Golf Drive",
            PostalCode = "T2P 1A1",
            MembershipCategory = category,
            IsActive = true,
            CreatedAt = now,
        };
    }

    private async Task SeedSeasonAsync(ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Seasons.AnyAsync(ct))
        {
            db.Seasons.Add(new Season
            {
                SeasonId = Guid.NewGuid(),
                Name = "2026 Season",
                StartDate = new DateOnly(2026, 4, 1),
                EndDate = new DateOnly(2026, 9, 30),
                SeasonStatus = SeasonStatus.Active
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded active season: 2026");
        }
    }
}
