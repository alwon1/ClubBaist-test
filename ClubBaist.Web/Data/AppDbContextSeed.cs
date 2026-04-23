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
        // Gold tier
        new("SH", "Shareholder", MemberType.Shareholder, 3000m),
        new("AS", "Associate", MemberType.Associate, 4500m),
        // Silver tier
        new("SS", "Shareholder Spouse", MemberType.Shareholder, 2000m),
        new("SV", "Associate Spouse", MemberType.Associate, 2500m),
        // Bronze tier
        new("PW", "Pee Wee", MemberType.Associate, 250m),
        new("JR", "Junior", MemberType.Associate, 500m),
        new("BR", "Intermediate", MemberType.Associate, 1000m),
        // Copper tier — Social members have no golf privileges
        new("CP", "Social", MemberType.Associate, 100m),
    ];

    private static readonly SeedUser[] Users =
    [
        new("admin@clubbaist.com", AppRoles.Admin, "Seed", "Admin", null, Gender.Male),
        new("committee@clubbaist.com", AppRoles.MembershipCommittee, "Seed", "Committee", null, Gender.Female),
        new("clerk@clubbaist.com", AppRoles.Clerk, "Seed", "Clerk", null, null),
        new("proshop@clubbaist.com", AppRoles.ProShopStaff, "Seed", "ProShop", null, null),
        new("shareholder1@clubbaist.com", AppRoles.Member, "Alice", "Shareholder", "SH", Gender.Female),
        new("shareholder2@clubbaist.com", AppRoles.Member, "Bob", "Shareholder", "SH", Gender.Male),
        new("shareholder3@clubbaist.com", AppRoles.Member, "Carol", "Shareholder", "SH", Gender.Female),
        new("silver@clubbaist.com", AppRoles.Member, "Diana", "Silver", "SV", Gender.Female),
        new("bronze@clubbaist.com", AppRoles.Member, "Evan", "Bronze", "BR", Gender.Male),
        new("copper@clubbaist.com", AppRoles.Member, "Fiona", "Copper", "CP", Gender.Female)
    ];

    private static readonly SeedApplication[] Applications =
    [
        new("frank.pending@example.com", "Frank", "Pending", "Software Developer", "Acme Corp", "456 Fairway Lane", "T2P 2B2", "403-555-0200", new DateTime(1990, 3, 22), "AS", ApplicationStatus.Submitted),
        new("grace.onhold@example.com", "Grace", "OnHold", "Project Manager", "Northwind Ltd", "789 Eagle Crest", "T2P 3C3", "403-555-0201", new DateTime(1988, 7, 14), "SV", ApplicationStatus.OnHold),
        new("henry.waitlist@example.com", "Henry", "Waitlist", "Civil Engineer", "Prairie Systems", "321 Bunker Road", "T2P 4D4", "403-555-0202", new DateTime(1995, 11, 5), "AS", ApplicationStatus.Waitlisted),
        new("iris.submitted@example.com", "Iris", "Submitted", "Designer", "ClubBaist Studio", "654 Green View", "T2P 5E5", "403-555-0203", new DateTime(1992, 6, 30), "BR", ApplicationStatus.Submitted),
        new("jack.waitlist@example.com", "Jack", "Waitlist", "Accountant", "Fairway Finance", "987 Sunset Terrace", "T2P 6F6", "403-555-0204", new DateTime(1985, 9, 18), "SV", ApplicationStatus.Waitlisted)
    ];

    public static async Task SeedAsync(IServiceProvider serviceProvider, AppDbContext db, bool storeCreated, CancellationToken cancellationToken)
    {
        if (!storeCreated)
        {
            return;
        }

        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ClubBaistUser>>();

        await SeedRolesAsync(roleManager);
        var levelsByCode = await SeedMembershipLevelsAsync(db, cancellationToken);
        var usersByEmail = await SeedUsersAsync(userManager, cancellationToken);
        var sponsors = await SeedMembershipsAsync(db, usersByEmail, levelsByCode, cancellationToken);

        await SeedApplicationsAsync(db, levelsByCode, sponsors, cancellationToken);
        await SeedCurrentSeasonAsync(db, cancellationToken);
        await SeedPastBookingsAsync(db, cancellationToken);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in new[] { AppRoles.Admin, AppRoles.MembershipCommittee, AppRoles.Member, AppRoles.Clerk, AppRoles.ProShopStaff })
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
                Name = seedLevel.Name,
                MemberType = seedLevel.MemberType,
                AnnualFee = seedLevel.AnnualFee
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
            case "SS": // Silver: Shareholder Spouse
            case "SV": // Silver: Associate Spouse
                // Silver members: weekdays two windows, weekends after 11 AM
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

            case "PW": // Bronze: Pee Wee
            case "JR": // Bronze: Junior
            case "BR": // Bronze: Intermediate
                // Bronze members: weekdays two windows, weekends after 1 PM
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

            case "CP": // Copper: Social — no golf privileges; intentionally no availability windows
                break;

            default: // Gold (SH, AS): full access, 7 AM–7 PM all days
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
                Gender = seedUser.Gender,
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
        // Start one month before today so past slots exist for score-entry testing.
        // End Oct 15 — reflects a typical Edmonton-area golf season close.
        var season = new Season
        {
            Name = $"{today.Year} Season",
            StartDate = today.AddMonths(-1),
            EndDate = new DateOnly(today.Year, 10, 15)
        };

        db.Seasons.Add(season);
        await db.SaveChangesAsync(cancellationToken);

        var slots = SeasonService2.GenerateSlots(season, OperatingHours.AllDaysDefault()).ToList();
        await db.TeeTimeSlots.AddRangeAsync(slots, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPastBookingsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var logger = db.GetService<ILoggerFactory>().CreateLogger(nameof(AppDbContextSeed));

        // Member IDs assigned sequentially by EF during seeding:
        //   1 = Alice Shareholder, 2 = Bob Shareholder, 3 = Carol Shareholder,
        //   4 = Diana Silver (Associate Spouse), 5 = Evan Bronze (Intermediate),
        //   6 = Fiona Copper (Social)
        var alice = await db.MemberShips.FirstOrDefaultAsync(m => m.Id == 1, cancellationToken);
        var diana = await db.MemberShips.FirstOrDefaultAsync(m => m.Id == 4, cancellationToken);

        if (alice is null || diana is null)
        {
            logger.LogWarning("SeedPastBookingsAsync: expected member IDs not found — skipping past booking seed.");
            return;
        }

        // Slot times are on the hour, operating hours 07:00–18:00.
        // Clamp an hour value to valid operating hours and return a DateTimeKind.Unspecified DateTime
        // suitable for matching TeeTimeSlot.Start.
        static DateTime SlotTime(DateTime date, int hour) =>
            DateTime.SpecifyKind(date.Date.AddHours(Math.Clamp(hour, 7, 18)), DateTimeKind.Unspecified);

        var now = DateTime.Now;
        var today = now.Date;

        // Today, more than 2 hours ago (3 h back; clamped to 07:00)
        var oldTodayStart = SlotTime(today, now.Hour - 3);

        // Within the last 2 hours (1 h back; clamped to 07:00; must be later than oldTodayStart)
        var recentStart = SlotTime(today, now.Hour - 1);
        if (recentStart <= oldTodayStart)
            recentStart = SlotTime(today, oldTodayStart.Hour + 1);

        // Near future (1 h ahead; if past 18:00 operating limit, use tomorrow 08:00)
        var futureHour = now.Hour + 1;
        var futureStart = futureHour <= 18
            ? SlotTime(today, futureHour)
            : DateTime.SpecifyKind(today.AddDays(1).AddHours(8), DateTimeKind.Unspecified);

        // Booking 1 — today, more than 2 hours ago — Alice solo
        var slot1 = await db.TeeTimeSlots.FirstOrDefaultAsync(s => s.Start == oldTodayStart, cancellationToken);
        if (slot1 is null)
            logger.LogWarning("SeedPastBookingsAsync: slot {Start} not found — skipping.", oldTodayStart);
        else
            db.TeeTimeBookings.Add(new TeeTimeBooking
            {
                TeeTimeSlotStart = slot1.Start,
                TeeTimeSlot = slot1,
                BookingMemberId = alice.Id,
                BookingMember = alice,
                AdditionalParticipants = []
            });

        // Booking 2 — today, within the last 2 hours — Alice + Diana
        var slot2 = await db.TeeTimeSlots.FirstOrDefaultAsync(s => s.Start == recentStart, cancellationToken);
        if (slot2 is null)
            logger.LogWarning("SeedPastBookingsAsync: slot {Start} not found — skipping.", recentStart);
        else
            db.TeeTimeBookings.Add(new TeeTimeBooking
            {
                TeeTimeSlotStart = slot2.Start,
                TeeTimeSlot = slot2,
                BookingMemberId = alice.Id,
                BookingMember = alice,
                AdditionalParticipants = [diana]
            });

        // Booking 3 — near future — Diana solo
        var slot3 = await db.TeeTimeSlots.FirstOrDefaultAsync(s => s.Start == futureStart, cancellationToken);
        if (slot3 is null)
            logger.LogWarning("SeedPastBookingsAsync: slot {Start} not found — skipping.", futureStart);
        else
            db.TeeTimeBookings.Add(new TeeTimeBooking
            {
                TeeTimeSlotStart = slot3.Start,
                TeeTimeSlot = slot3,
                BookingMemberId = diana.Id,
                BookingMember = diana,
                AdditionalParticipants = []
            });

        // Booking 4 — yesterday at 08:00 — Alice solo (second eligible booking for TC-SCORE-005/006/007)
        var yesterday8 = DateTime.SpecifyKind(today.AddDays(-1).AddHours(8), DateTimeKind.Unspecified);
        var slot4 = await db.TeeTimeSlots.FirstOrDefaultAsync(s => s.Start == yesterday8, cancellationToken);
        if (slot4 is null)
            logger.LogWarning("SeedPastBookingsAsync: slot {Start} not found — skipping.", yesterday8);
        else
            db.TeeTimeBookings.Add(new TeeTimeBooking
            {
                TeeTimeSlotStart = slot4.Start,
                TeeTimeSlot = slot4,
                BookingMemberId = alice.Id,
                BookingMember = alice,
                AdditionalParticipants = []
            });

        // Booking 5 — yesterday at 09:00 — Diana solo (eligible booking for TC-SCORE-009 staff scoring)
        var yesterday9 = DateTime.SpecifyKind(today.AddDays(-1).AddHours(9), DateTimeKind.Unspecified);
        var slot5 = await db.TeeTimeSlots.FirstOrDefaultAsync(s => s.Start == yesterday9, cancellationToken);
        if (slot5 is null)
            logger.LogWarning("SeedPastBookingsAsync: slot {Start} not found — skipping.", yesterday9);
        else
            db.TeeTimeBookings.Add(new TeeTimeBooking
            {
                TeeTimeSlotStart = slot5.Start,
                TeeTimeSlot = slot5,
                BookingMemberId = diana.Id,
                BookingMember = diana,
                AdditionalParticipants = []
            });

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

    private sealed record SeedMembershipLevel(string ShortCode, string Name, MemberType MemberType, decimal AnnualFee);

    private sealed record SeedUser(
        string Email,
        string Role,
        string FirstName,
        string LastName,
        string? MembershipLevelShortCode,
        Gender? Gender);

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