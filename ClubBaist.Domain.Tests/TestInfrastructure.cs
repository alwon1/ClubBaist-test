using Aspire.Hosting;
using Aspire.Hosting.Testing;
using ClubBaist.Domain.Entities;
using ClubBaist.Domain.Entities.Membership;
using ClubBaist.Services;
using ClubBaist.Services.Membership;
using ClubBaist.Services.Membership.Applications;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain.Tests;

internal sealed class DomainTestHost : IAsyncDisposable
{
    private static readonly SemaphoreSlim AppLock = new(1, 1);
    private static DistributedApplication? distributedApp;

    private DomainTestHost(ServiceProvider services)
    {
        Services = services;
    }

    public ServiceProvider Services { get; }

    public static async Task<DomainTestHost> CreateAsync()
    {
        var baseConnectionString = await GetConnectionStringAsync();
        var sqlBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"clubbaist-domain-tests-{Guid.NewGuid():N}",
            TrustServerCertificate = true
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(sqlBuilder.ConnectionString, sql => sql.EnableRetryOnFailure()));

        services.AddIdentityCore<ClubBaistUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddTeeTimeBookingServices();
        services.AddScoped<MembershipApplicationService>();
        services.AddScoped<MembershipService>();
        services.AddScoped<MembershipLevelService>();

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
            await db.EnsureSqlServerSnapshotIsolationAsync();

            if (!await db.Roles.AnyAsync())
            {
                db.Roles.AddRange(
                    new IdentityRole<Guid> { Name = AppRoles.Admin, NormalizedName = AppRoles.Admin.ToUpperInvariant() },
                    new IdentityRole<Guid> { Name = AppRoles.MembershipCommittee, NormalizedName = AppRoles.MembershipCommittee.ToUpperInvariant() },
                    new IdentityRole<Guid> { Name = AppRoles.Member, NormalizedName = AppRoles.Member.ToUpperInvariant() },
                    new IdentityRole<Guid> { Name = AppRoles.Shareholder, NormalizedName = AppRoles.Shareholder.ToUpperInvariant() });
                await db.SaveChangesAsync();
            }
        }

        return new DomainTestHost(provider);
    }

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public async ValueTask DisposeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await Services.DisposeAsync();
    }

    private static async Task<string> GetConnectionStringAsync()
    {
        if (distributedApp is null)
        {
            await AppLock.WaitAsync();
            try
            {
                if (distributedApp is null)
                {
                    var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ClubBaist_AppHost>();
                    distributedApp = await builder.BuildAsync();
                    await distributedApp.StartAsync();
                }
            }
            finally
            {
                AppLock.Release();
            }
        }

        return await distributedApp.GetConnectionStringAsync("clubbaist")
            ?? throw new InvalidOperationException("The AppHost did not provide a connection string for 'clubbaist'.");
    }
}

internal static class DomainTestData
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
        SeasonService seasonService,
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

    /// <summary>
    /// Creates a single tee-time slot at the exact <paramref name="slotStart"/> time,
    /// bypassing operating-hours constraints. Use this when a test needs a slot at a
    /// specific wall-clock time that may fall outside the standard 07:00–19:00 window.
    /// </summary>
    public static async Task<TeeTimeSlot> CreateSlotAtAsync(AppDbContext db, DateTime slotStart)
    {
        var date = DateOnly.FromDateTime(slotStart);
        var season = new Season
        {
            Name = $"Spot-Season-{slotStart:HHmmss-fffff}",
            StartDate = date,
            EndDate = date
        };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();

        var slot = new TeeTimeSlot
        {
            Start = DateTime.SpecifyKind(slotStart, DateTimeKind.Unspecified),
            Duration = TimeSpan.FromMinutes(7),
            SeasonId = season.Id
        };
        db.TeeTimeSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot;
    }

    public static async Task<TeeTimeBooking> CreateBookingAsync(
        AppDbContext db,
        MemberShipInfo bookingMember,
        TeeTimeSlot slot,
        List<MemberShipInfo>? additionalParticipants = null)
    {
        var booking = new TeeTimeBooking
        {
            TeeTimeSlotStart = slot.Start,
            TeeTimeSlot = slot,
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            AdditionalParticipants = additionalParticipants ?? []
        };
        db.TeeTimeBookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }
}