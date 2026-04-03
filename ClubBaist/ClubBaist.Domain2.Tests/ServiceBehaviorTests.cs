using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Services2;
using ClubBaist.Services2.Membership;
using ClubBaist.Services2.Membership.Applications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class MembershipApplicationServiceTests
{
    [TestMethod]
    public async Task SubmitMembershipApplicationAsync_PersistsApplication_WithSubmittedStatus()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "applicant@test.com", firstName: "Jane", lastName: "Doe");

        var result = await applicationService.SubmitMembershipApplicationAsync(application);

        Assert.IsTrue(result);

        var persisted = await db.MembershipApplications.AsNoTracking().SingleAsync(item => item.Email == "applicant@test.com");
        Assert.AreEqual(ApplicationStatus.Submitted, persisted.Status);
        Assert.AreEqual("Jane", persisted.FirstName);
        Assert.AreEqual("Doe", persisted.LastName);
        Assert.AreEqual(associate.Id, persisted.RequestedMembershipLevelId);
    }

    [TestMethod]
    public async Task SubmitMembershipApplicationAsync_DuplicateActiveApplication_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var first = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "duplicate@test.com");
        var second = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "duplicate@test.com", firstName: "Second");

        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(first));
        Assert.IsFalse(await applicationService.SubmitMembershipApplicationAsync(second));

        Assert.AreEqual(1, await db.MembershipApplications.CountAsync(item => item.Email == "duplicate@test.com"));
    }

    [TestMethod]
    public async Task ApproveMembershipApplicationAsync_CreatesUserAndMembership_AndMarksAccepted()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "approved@test.com", firstName: "Alex", lastName: "Approved");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));

        var persisted = await db.MembershipApplications.SingleAsync(item => item.Email == "approved@test.com");
        var approved = await applicationService.ApproveMembershipApplicationAsync(persisted.Id, associate.Id);

        Assert.IsTrue(approved);

        var updatedApplication = await db.MembershipApplications.AsNoTracking().SingleAsync(item => item.Id == persisted.Id);
        var createdUser = await userManager.FindByEmailAsync("approved@test.com");
        var membership = await db.MemberShips
            .Include(item => item.MembershipLevel)
            .Include(item => item.User)
            .SingleAsync(item => item.User.Email == "approved@test.com");

        Assert.AreEqual(ApplicationStatus.Accepted, updatedApplication.Status);
        Assert.IsNotNull(createdUser);
        Assert.AreEqual(associate.Id, membership.MembershipLevel.Id);
    }

    [TestMethod]
    public async Task SetApplicationStatusAsync_NonTerminalStatus_UpdatesApplication()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var applicationService = provider.GetRequiredService<MembershipApplicationService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var associate = await Domain2TestData.CreateMembershipLevelAsync(db, "AS", "Associate");
        var sponsor1 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor1@test.com", "Sponsor", "One");
        var sponsor2 = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "sponsor2@test.com", "Sponsor", "Two");

        var application = Domain2TestData.CreateApplication(associate, sponsor1.Id, sponsor2.Id, email: "onhold@test.com");
        Assert.IsTrue(await applicationService.SubmitMembershipApplicationAsync(application));

        var persisted = await db.MembershipApplications.SingleAsync(item => item.Email == "onhold@test.com");
        var updated = await applicationService.SetApplicationStatusAsync(persisted.Id, ApplicationStatus.OnHold);

        Assert.IsTrue(updated);
        Assert.AreEqual(ApplicationStatus.OnHold, (await db.MembershipApplications.AsNoTracking().SingleAsync(item => item.Id == persisted.Id)).Status);
        Assert.IsFalse(await applicationService.SetApplicationStatusAsync(persisted.Id, ApplicationStatus.Accepted));
    }
}

[TestClass]
public class MembershipServiceTests
{
    [TestMethod]
    public async Task SetMembershipLevelForUserAsync_ExistingMembership_UpdatesLevel()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var membershipService = provider.GetRequiredService<MembershipService>();

        var bronze = await Domain2TestData.CreateMembershipLevelAsync(db, "BR", "Bronze");
        var silver = await Domain2TestData.CreateMembershipLevelAsync(db, "SV", "Silver");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, bronze, "member@test.com", "Member", "User");

        var result = await membershipService.SetMembershipLevelForUserAsync(member.User, silver.Id);
        var updatedLevel = await membershipService.GetMembershipLevelForUserAsync(member.User.Id);

        Assert.IsTrue(result);
        Assert.IsNotNull(updatedLevel);
        Assert.AreEqual(silver.Id, updatedLevel.Id);
    }
}

[TestClass]
public class SeasonValidationRuleCoverageTests
{
    [TestMethod]
    public void SlotWithinSeason_AllowsBooking()
    {
        var date = new DateTime(2026, 6, 15, 10, 0, 0);
        var season = new Season { Name = "2026 Season", StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 9, 30) };
        var slot = new TeeTimeSlot { Start = date, SeasonId = season.Id };

        var result = new SeasonValidationRule(new[] { season }.AsQueryable())
            .Evaluate(Builders.Seed(slot), Builders.Level())
            .Single();

        Assert.IsNull(result.RejectionReason);
    }

    [TestMethod]
    public void SlotOutsideSeason_RejectsWithNegativeFour()
    {
        var season = new Season { Name = "2026 Season", StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 9, 30) };
        var slot = new TeeTimeSlot { Start = new DateTime(2026, 1, 15, 10, 0, 0), SeasonId = season.Id };

        var result = new SeasonValidationRule(new[] { season }.AsQueryable())
            .Evaluate(Builders.Seed(slot), Builders.Level())
            .Single();

        Assert.AreEqual(-4, result.SpotsRemaining);
        StringAssert.Contains(result.RejectionReason, "outside");
    }
}

[TestClass]
public class BookingServiceTests
{
    [TestMethod]
    public async Task UpdateBookingAsync_ExistingBooking_ReplacesAdditionalParticipants()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var bookingService = provider.GetRequiredService<BookingService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bookingMember = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker@test.com", "Booker", "User");
        var firstParticipant = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "player1@test.com", "Player", "One");
        var secondParticipant = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "player2@test.com", "Player", "Two");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(
            seasonService,
            db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(8, 0));

        var booking = new TeeTimeBooking
        {
            TeeTimeSlotStart = slot.Start,
            TeeTimeSlot = slot,
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            AdditionalParticipants = new List<MemberShipInfo> { firstParticipant }
        };
        db.TeeTimeBookings.Add(booking);
        await db.SaveChangesAsync();

        var updated = await bookingService.UpdateBookingAsync(booking.Id, [secondParticipant]);
        var persisted = await db.TeeTimeBookings
            .Include(item => item.AdditionalParticipants)
            .SingleAsync(item => item.Id == booking.Id);

        Assert.IsTrue(updated);
        Assert.HasCount(1, persisted.AdditionalParticipants);
        Assert.AreEqual(secondParticipant.Id, persisted.AdditionalParticipants[0].Id);
    }

    [TestMethod]
    public async Task CreateBooking_OutsideSeason_ReturnsFalse()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var bookingService = provider.GetRequiredService<BookingService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker@test.com", "Booker", "User");
        var season = new Season
        {
            Name = "2026 Season",
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2026, 9, 30)
        };
        db.Seasons.Add(season);
        await db.SaveChangesAsync();

        var offSeason = new TeeTimeSlot
        {
            Start = new DateTime(2026, 1, 15, 10, 0, 0),
            Duration = TimeSpan.FromMinutes(7),
            SeasonId = season.Id,
            Season = season
        };
        db.TeeTimeSlots.Add(offSeason);
        await db.SaveChangesAsync();

        var booking = new TeeTimeBooking
        {
            TeeTimeSlotStart = offSeason.Start,
            TeeTimeSlot = offSeason,
            BookingMemberId = member.Id,
            BookingMember = member
        };

        var created = await bookingService.CreateBooking(booking);

        Assert.IsFalse(created);
    }

    [TestMethod]
    public async Task CancelBookingAsync_ExistingBooking_RemovesBooking()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();
        var bookingService = provider.GetRequiredService<BookingService>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var member = await Domain2TestData.CreateMemberAsync(userManager, db, shareholder, "booker@test.com", "Booker", "User");
        var (_, slot) = await Domain2TestData.CreateSeasonAndSlotAsync(
            seasonService,
            db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            new TimeOnly(8, 0));

        var booking = new TeeTimeBooking
        {
            TeeTimeSlotStart = slot.Start,
            TeeTimeSlot = slot,
            BookingMemberId = member.Id,
            BookingMember = member
        };
        db.TeeTimeBookings.Add(booking);
        await db.SaveChangesAsync();

        Assert.IsTrue(await bookingService.CancelBookingAsync(booking.Id));
        Assert.HasCount(0, await db.TeeTimeBookings.ToListAsync());
    }
}