using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Services2;
using ClubBaist.Services2.Membership;
using ClubBaist.Services2.Membership.Applications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

internal sealed class Domain2TestHost : IAsyncDisposable
{
    private Domain2TestHost(ServiceProvider services)
    {
        Services = services;
    }

    public ServiceProvider Services { get; }

    public static async Task<Domain2TestHost> CreateAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase($"clubbaist-domain2-tests-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddIdentityCore<ClubBaistUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IAppDbContext2>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddTeeTimeBookingServices2();
        services.AddScoped<MembershipApplicationService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<MembershipLevelService>();

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new Domain2TestHost(provider);
    }

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
    }
}

internal static class Domain2TestData
{
    public static async Task<MembershipLevel> CreateMembershipLevelAsync(
        AppDbContext db,
        string shortCode,
        string name,
        int openingHour = 7,
        int closingHour = 19)
    {
        var membershipLevel = new MembershipLevel
        {
            ShortCode = shortCode,
            Name = name
        };

        foreach (var dayOfWeek in Enum.GetValues<DayOfWeek>())
        {
            membershipLevel.Availabilities.Add(new MembershipLevelTeeTimeAvailability
            {
                MembershipLevel = membershipLevel,
                DayOfWeek = dayOfWeek,
                StartTime = new TimeOnly(openingHour, 0),
                EndTime = new TimeOnly(closingHour, 0)
            });
        }

        db.MembershipLevels.Add(membershipLevel);
        await db.SaveChangesAsync();
        return membershipLevel;
    }

    public static async Task<MemberShipInfo> CreateMemberAsync(
        UserManager<ClubBaistUser> userManager,
        AppDbContext db,
        MembershipLevel membershipLevel,
        string email,
        string firstName,
        string lastName)
    {
        var user = new ClubBaistUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = new DateTime(1985, 1, 15),
            PhoneNumber = "403-555-0100",
            AddressLine1 = "123 Golf Drive",
            City = "Calgary",
            Province = "AB",
            PostalCode = "T2P 1A1"
        };

        var createResult = await userManager.CreateAsync(user, "Pass@word1");
        Assert.IsTrue(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(error => error.Description)));

        var membership = new MemberShipInfo
        {
            User = user,
            MembershipLevel = membershipLevel
        };

        db.MemberShips.Add(membership);
        await db.SaveChangesAsync();
        return membership;
    }

    public static MembershipApplication CreateApplication(
        MembershipLevel requestedMembershipLevel,
        int sponsor1MemberId,
        int sponsor2MemberId,
        string email = "applicant@example.com",
        string firstName = "Alex",
        string lastName = "Applicant") =>
        new()
        {
            FirstName = firstName,
            LastName = lastName,
            Occupation = "Engineer",
            CompanyName = "ClubBaist",
            Address = "500 Main St",
            PostalCode = "T2P 1A1",
            Phone = "403-555-0200",
            AlternatePhone = "403-555-0201",
            Email = email,
            DateOfBirth = new DateTime(1990, 5, 20),
            Sponsor1MemberId = sponsor1MemberId,
            Sponsor2MemberId = sponsor2MemberId,
            RequestedMembershipLevelId = requestedMembershipLevel.Id,
            RequestedMembershipLevel = requestedMembershipLevel,
            Status = ApplicationStatus.Submitted
        };

    public static async Task<(Season season, TeeTimeSlot slot)> CreateSeasonAndSlotAsync(
        SeasonService2 seasonService,
        AppDbContext db,
        DateOnly date,
        TimeOnly earliestTime)
    {
        var season = await seasonService.CreateSeasonAsync($"Test Season {date:yyyyMMdd}", date, date);
        var slotStart = date.ToDateTime(earliestTime);
        var slot = await db.TeeTimeSlots
            .Where(item => item.SeasonId == season.Id && item.Start >= slotStart)
            .OrderBy(item => item.Start)
            .FirstAsync();

        return (season, slot);
    }
}