using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Services2;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ClubBaist.Web.Data;

internal static class AppDbContextSeed
{
    private const string DefaultPassword = "Pass@word1";

    private static readonly SeedMembershipLevel[] MembershipLevels =
    [
        new("SH", "Shareholder"),
        new("SV", "Silver"),
        new("BR", "Bronze"),
        new("AS", "Associate")
    ];

    private static readonly SeedUser[] Users =
    [
        new("admin@clubbaist.com", AppRoles.Admin, "Seed", "Admin", null),
        new("committee@clubbaist.com", AppRoles.MembershipCommittee, "Seed", "Committee", null),
        new("shareholder1@clubbaist.com", AppRoles.Member, "Alice", "Shareholder", "SH"),
        new("shareholder2@clubbaist.com", AppRoles.Member, "Bob", "Shareholder", "SH"),
        new("shareholder3@clubbaist.com", AppRoles.Member, "Carol", "Shareholder", "SH"),
        new("silver@clubbaist.com", AppRoles.Member, "Diana", "Silver", "SV"),
        new("bronze@clubbaist.com", AppRoles.Member, "Evan", "Bronze", "BR")
    ];

    private static readonly SeedApplication[] Applications =
    [
        new("frank.pending@example.com", "Frank", "Pending", "Software Developer", "Acme Corp", "456 Fairway Lane", "T2P 2B2", "403-555-0200", new DateTime(1990, 3, 22), "AS", ApplicationStatus.Submitted),
        new("grace.onhold@example.com", "Grace", "OnHold", "Project Manager", "Northwind Ltd", "789 Eagle Crest", "T2P 3C3", "403-555-0201", new DateTime(1988, 7, 14), "SV", ApplicationStatus.OnHold),
        new("henry.waitlist@example.com", "Henry", "Waitlist", "Civil Engineer", "Prairie Systems", "321 Bunker Road", "T2P 4D4", "403-555-0202", new DateTime(1995, 11, 5), "AS", ApplicationStatus.Waitlisted),
        new("iris.submitted@example.com", "Iris", "Submitted", "Designer", "ClubBaist Studio", "654 Green View", "T2P 5E5", "403-555-0203", new DateTime(1992, 6, 30), "BR", ApplicationStatus.Submitted),
        new("jack.waitlist@example.com", "Jack", "Waitlist", "Accountant", "Fairway Finance", "987 Sunset Terrace", "T2P 6F6", "403-555-0204", new DateTime(1985, 9, 18), "SV", ApplicationStatus.Waitlisted)
    ];

    public static void Seed(AppDbContext db, bool storeCreated)
    {
        if (!storeCreated)
        {
            return;
        }

        SeedAsync(db, storeCreated, CancellationToken.None).GetAwaiter().GetResult();
    }

    public static async Task SeedAsync(AppDbContext db, bool storeCreated, CancellationToken cancellationToken)
    {
        if (!storeCreated)
        {
            return;
        }

        var roleManager = db.GetService<RoleManager<IdentityRole<Guid>>>();
        var userManager = db.GetService<UserManager<ClubBaistUser>>();

        await SeedRolesAsync(roleManager);
        var levelsByCode = await SeedMembershipLevelsAsync(db, cancellationToken);
        var usersByEmail = await SeedUsersAsync(userManager, cancellationToken);
        var sponsors = await SeedMembershipsAsync(db, usersByEmail, levelsByCode, cancellationToken);

        await SeedApplicationsAsync(db, levelsByCode, sponsors, cancellationToken);
        await SeedCurrentSeasonAsync(db, cancellationToken);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in new[] { AppRoles.Admin, AppRoles.MembershipCommittee, AppRoles.Member })
        {
            ThrowIfFailed(
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName }),
                $"create role '{roleName}'");
        }
    }

    private static async Task<Dictionary<string, MembershipLevel>> SeedMembershipLevelsAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var levels = MembershipLevels.Select(seedLevel =>
        {
            var level = new MembershipLevel
            {
                ShortCode = seedLevel.ShortCode,
                Name = seedLevel.Name
            };

            AddAvailabilities(level);

            return level;
        }).ToList();

        db.MembershipLevels.AddRange(levels);
        await db.SaveChangesAsync(cancellationToken);

        return levels.ToDictionary(level => level.ShortCode, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddAvailabilities(MembershipLevel level)
    {
        void AddAvailability(DayOfWeek day, TimeOnly startTime, TimeOnly endTime)
        {
            level.Availabilities.Add(new MembershipLevelTeeTimeAvailability
            {
                MembershipLevel = level,
                DayOfWeek = day,
                StartTime = startTime,
                EndTime = endTime
            });
        }

        switch (level.ShortCode.ToUpperInvariant())
        {
            case "SV": // Silver: weekdays two windows, weekends after 11 AM
                foreach (var day in Weekdays)
                {
                    AddAvailability(day, new TimeOnly(7, 0), new TimeOnly(15, 0));
                    AddAvailability(day, new TimeOnly(17, 30), new TimeOnly(19, 0));
                }
                foreach (var day in WeekendDays)
                {
                    AddAvailability(day, new TimeOnly(11, 0), new TimeOnly(19, 0));
                }
                break;

            case "BR": // Bronze: weekdays two windows, weekends after 1 PM
                foreach (var day in Weekdays)
                {
                    AddAvailability(day, new TimeOnly(7, 0), new TimeOnly(15, 0));
                    AddAvailability(day, new TimeOnly(18, 0), new TimeOnly(19, 0));
                }
                foreach (var day in WeekendDays)
                {
                    AddAvailability(day, new TimeOnly(13, 0), new TimeOnly(19, 0));
                }
                break;

            default: // Gold (SH, AS) and any other level: 7 AM–7 PM all days
                foreach (var day in Enum.GetValues<DayOfWeek>())
                {
                    AddAvailability(day, new TimeOnly(7, 0), new TimeOnly(19, 0));
                }
                break;
        }
    }

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static readonly DayOfWeek[] WeekendDays =
        [DayOfWeek.Saturday, DayOfWeek.Sunday];

    private static async Task<Dictionary<string, ClubBaistUser>> SeedUsersAsync(
        UserManager<ClubBaistUser> userManager,
        CancellationToken cancellationToken)
    {
        var usersByEmail = new Dictionary<string, ClubBaistUser>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedUser in Users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = new ClubBaistUser
            {
                UserName = seedUser.Email,
                Email = seedUser.Email,
                EmailConfirmed = true,
                FirstName = seedUser.FirstName,
                LastName = seedUser.LastName,
                DateOfBirth = new DateTime(1985, 1, 15),
                PhoneNumber = "(403) 555-0000",
                AlternatePhoneNumber = "(403) 555-0001",
                AddressLine1 = "123 Golf Drive",
                City = "Calgary",
                Province = "AB",
                PostalCode = "T2P 1A1"
            };

            ThrowIfFailed(await userManager.CreateAsync(user, DefaultPassword), $"create user '{seedUser.Email}'");
            ThrowIfFailed(await userManager.AddToRoleAsync(user, seedUser.Role), $"assign role '{seedUser.Role}' to '{seedUser.Email}'");

            if (seedUser.MembershipLevelShortCode == "SH")
            {
                ThrowIfFailed(
                    await userManager.AddClaimAsync(user, AppRoles.Claims.StandingTeeTimeBooking),
                    $"add standing tee time claim to '{seedUser.Email}'");
            }

            usersByEmail.Add(seedUser.Email, user);
        }

        return usersByEmail;
    }

    private static async Task<List<MemberShipInfo>> SeedMembershipsAsync(
        AppDbContext db,
        IReadOnlyDictionary<string, ClubBaistUser> usersByEmail,
        IReadOnlyDictionary<string, MembershipLevel> levelsByCode,
        CancellationToken cancellationToken)
    {
        var memberships = Users
            .Where(user => user.MembershipLevelShortCode is not null)
            .Select(user =>
            {
                var dbUser = usersByEmail[user.Email];
                var level = levelsByCode[user.MembershipLevelShortCode!];

                return new MemberShipInfo
                {
                    User = dbUser,
                    UserId = dbUser.Id,
                    MembershipLevel = level,
                    MembershipLevelId = level.Id
                };
            })
            .ToList();

        db.MemberShips.AddRange(memberships);
        await db.SaveChangesAsync(cancellationToken);

        return memberships;
    }

    private static async Task SeedApplicationsAsync(
        AppDbContext db,
        IReadOnlyDictionary<string, MembershipLevel> levelsByCode,
        IReadOnlyList<MemberShipInfo> sponsors,
        CancellationToken cancellationToken)
    {
        db.MembershipApplications.AddRange(
            Applications.Select(seedApplication =>
            {
                var requestedLevel = levelsByCode[seedApplication.RequestedMembershipLevelShortCode];

                return new MembershipApplication
                {
                    FirstName = seedApplication.FirstName,
                    LastName = seedApplication.LastName,
                    Occupation = seedApplication.Occupation,
                    CompanyName = seedApplication.CompanyName,
                    Address = seedApplication.Address,
                    PostalCode = seedApplication.PostalCode,
                    Phone = seedApplication.Phone,
                    Email = seedApplication.Email,
                    DateOfBirth = seedApplication.DateOfBirth,
                    Sponsor1MemberId = sponsors[0].Id,
                    Sponsor2MemberId = sponsors[1].Id,
                    RequestedMembershipLevelId = requestedLevel.Id,
                    RequestedMembershipLevel = requestedLevel,
                    Status = seedApplication.Status
                };
            }));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCurrentSeasonAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var season = new Season
        {
            Name = $"{today.Year} Season",
            StartDate = new DateOnly(today.Year, 1, 1),
            EndDate = new DateOnly(today.Year, 12, 31)
        };

        db.Seasons.Add(season);
        await db.SaveChangesAsync(cancellationToken);

        var slots = SeasonService2.GenerateSlots(season, OperatingHours.AllDaysDefault()).ToList();
        await db.TeeTimeSlots.AddRangeAsync(slots, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(", ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
        throw new InvalidOperationException($"Failed to {action}: {errors}");
    }

    private sealed record SeedMembershipLevel(string ShortCode, string Name);

    private sealed record SeedUser(
        string Email,
        string Role,
        string FirstName,
        string LastName,
        string? MembershipLevelShortCode);

    private sealed record SeedApplication(
        string Email,
        string FirstName,
        string LastName,
        string Occupation,
        string CompanyName,
        string Address,
        string PostalCode,
        string Phone,
        DateTime DateOfBirth,
        string RequestedMembershipLevelShortCode,
        ApplicationStatus Status);
}