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
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        logger.LogInformation("Creating database schema...");
        await db.Database.EnsureDeletedAsync(stoppingToken);
        await db.Database.EnsureCreatedAsync(stoppingToken);

        await SeedRolesAsync(roleManager, stoppingToken);
        await SeedUsersAsync(userManager, db, stoppingToken);
        await SeedSeasonAsync(db, stoppingToken);
        await SeedApplicationsAsync(userManager, db, stoppingToken);

        logger.LogInformation("Seeding complete. Stopping seeder.");
        lifetime.StopApplication();
    }

    private async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager, CancellationToken ct)
    {
        string[] roles = [AppRoles.Admin, AppRoles.MembershipCommittee, AppRoles.Member];
        foreach (var role in roles)
        {
            ct.ThrowIfCancellationRequested();
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = role });
                logger.LogInformation("Created role: {Role}", role);
            }
        }
    }

    private async Task SeedUsersAsync(UserManager<ApplicationUser> userManager, ApplicationDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Admin
        await CreateUserWithRoleAsync(userManager, "admin@clubbaist.com", AppRoles.Admin, ct);

        // Membership Committee
        await CreateUserWithRoleAsync(userManager, "committee@clubbaist.com", AppRoles.MembershipCommittee, ct);

        // Shareholder members (Gold) - need at least 3 for sponsorship
        var sh1 = await CreateUserWithRoleAsync(userManager, "shareholder1@clubbaist.com", AppRoles.Member, ct);
        var sh2 = await CreateUserWithRoleAsync(userManager, "shareholder2@clubbaist.com", AppRoles.Member, ct);
        var sh3 = await CreateUserWithRoleAsync(userManager, "shareholder3@clubbaist.com", AppRoles.Member, ct);

        // Silver member
        var silver = await CreateUserWithRoleAsync(userManager, "silver@clubbaist.com", AppRoles.Member, ct);

        // Bronze member
        var bronze = await CreateUserWithRoleAsync(userManager, "bronze@clubbaist.com", AppRoles.Member, ct);

        // Create MemberAccounts if they don't exist yet
        if (!await db.MemberAccounts.AnyAsync(ct))
        {
            var memberNumber = 10000;

            db.MemberAccounts.AddRange(
                CreateMember(sh1.Id, memberNumber++, "Alice", "Shareholder", "shareholder1@clubbaist.com", MembershipCategory.Shareholder, now),
                CreateMember(sh2.Id, memberNumber++, "Bob", "Shareholder", "shareholder2@clubbaist.com", MembershipCategory.Shareholder, now),
                CreateMember(sh3.Id, memberNumber++, "Carol", "Shareholder", "shareholder3@clubbaist.com", MembershipCategory.Shareholder, now),
                CreateMember(silver.Id, memberNumber++, "Diana", "Silver", "silver@clubbaist.com", MembershipCategory.ShareholderSpouse, now),
                CreateMember(bronze.Id, memberNumber++, "Evan", "Bronze", "bronze@clubbaist.com", MembershipCategory.Junior, now)
            );

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} member accounts", 5);
        }
    }

    private async Task<ApplicationUser> CreateUserWithRoleAsync(
        UserManager<ApplicationUser> userManager, string email, string role, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Seed",
                LastName = "User",
                Phone = "(403) 555-0000"
            };
            var result = await userManager.CreateAsync(user, DefaultPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to create user {Email}: {Errors}", email, errors);
                throw new InvalidOperationException($"Failed to create user {email}: {errors}");
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
        Guid userId, int memberNumber, string firstName, string lastName,
        string email, MembershipCategory category, DateTime now)
    {
        return new MemberAccount<Guid>
        {
            ApplicationUserId = userId,
            MemberNumber = memberNumber,
            DateOfBirth = new DateTime(1985, 1, 15),
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

    private async Task SeedApplicationsAsync(
        UserManager<ApplicationUser> userManager, ApplicationDbContext db, CancellationToken ct)
    {
        if (await db.MembershipApplications.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;

        // Retrieve existing sponsor member IDs
        var sponsors = await db.MemberAccounts
            .OrderBy(m => m.MemberNumber)
            .Take(2)
            .Select(m => m.MemberAccountId)
            .ToListAsync(ct);

        if (sponsors.Count < 2)
        {
            logger.LogWarning("Not enough member accounts to seed applications; skipping.");
            return;
        }

        var sponsor1Id = sponsors[0];
        var sponsor2Id = sponsors[1];

        // Retrieve the committee user (used as the actor for status transitions)
        var committeeUser = await userManager.FindByEmailAsync("committee@clubbaist.com")
            ?? throw new InvalidOperationException("Committee user not found.");

        // Create identity users for applicants
        var applicants = new[]
        {
            ("frank.pending@example.com",  "Frank",  "Pending",   new DateTime(1990, 3, 22)),
            ("grace.onhold@example.com",   "Grace",  "OnHold",    new DateTime(1988, 7, 14)),
            ("henry.waitlist@example.com", "Henry",  "Waitlist",  new DateTime(1995, 11, 5)),
            ("iris.submitted@example.com", "Iris",   "Submitted", new DateTime(1992, 6, 30)),
            ("jack.waitlist@example.com",  "Jack",   "Waitlist",  new DateTime(1985, 9, 18)),
        };

        var applicantUsers = new List<ApplicationUser>();
        foreach (var (email, firstName, lastName, _) in applicants)
        {
            ct.ThrowIfCancellationRequested();
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    Phone = "(403) 555-0000"
                };
                var result = await userManager.CreateAsync(user, DefaultPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create applicant user {email}: {errors}");
                }
            }
            applicantUsers.Add(user);
        }

        // Build and save applications
        var applications = new List<MembershipApplication<Guid>>();
        for (var i = 0; i < applicants.Length; i++)
        {
            var (email, firstName, lastName, dob) = applicants[i];
            var userId = applicantUsers[i].Id;
            var submittedAt = now.AddDays(-(applicants.Length - i) * 3);

            var app = MembershipApplication<Guid>.Submit(
                applicationUserId: userId,
                firstName: firstName,
                lastName: lastName,
                occupation: "Software Developer",
                companyName: "Acme Corp",
                address: "456 Fairway Lane",
                postalCode: "T2P 2B2",
                phone: "403-555-0200",
                email: email,
                dateOfBirth: dob,
                requestedMembershipCategory: MembershipCategory.Associate,
                sponsor1MemberId: sponsor1Id,
                sponsor2MemberId: sponsor2Id,
                submittedAt: submittedAt);

            applications.Add(app);
        }

        db.MembershipApplications.AddRange(applications);


        // Advance statuses for some applications
        // applications[1] → OnHold
        applications[1].ChangeStatus(ApplicationStatus.OnHold, committeeUser.Id, now.AddDays(-8));

        // applications[2] → Waitlisted
        applications[2].ChangeStatus(ApplicationStatus.Waitlisted, committeeUser.Id, now.AddDays(-6));

        // applications[4] → OnHold then Waitlisted
        applications[4].ChangeStatus(ApplicationStatus.OnHold, committeeUser.Id, now.AddDays(-10));
        applications[4].ChangeStatus(ApplicationStatus.Waitlisted, committeeUser.Id, now.AddDays(-5));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} in-progress membership applications", applications.Count);
    }
}
