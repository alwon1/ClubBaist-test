using ClubBaist.Domain.Entities;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain.Tests;

[TestClass]
public class StandingTeeTimePhaseOneTests
{
    [TestMethod]
    public void StandingTeeTime_TracksBookingMemberParticipantsAndDefaults()
    {
        var shareholderLevel = new MembershipLevel
        {
            Id = 1,
            Name = "Shareholder",
            ShortCode = "SH"
        };

        var guestLevel = new MembershipLevel
        {
            Id = 2,
            Name = "Bronze",
            ShortCode = "BR"
        };

        var bookingMember = new MemberShipInfo
        {
            Id = 1001,
            User = null!,
            MembershipLevel = shareholderLevel
        };

        var additionalParticipant = new MemberShipInfo
        {
            Id = 1002,
            User = null!,
            MembershipLevel = guestLevel
        };

        var standing = new StandingTeeTime
        {
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            RequestedDayOfWeek = DayOfWeek.Saturday,
            RequestedTime = new TimeOnly(8, 0),
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2026, 9, 30),
            Status = StandingTeeTimeStatus.Approved,
            PriorityNumber = 1,
            ApprovedTime = new TimeOnly(8, 7)
        };

        standing.AdditionalParticipants.Add(additionalParticipant);

        Assert.AreEqual(2, standing.ParticipantCount);
        Assert.AreEqual(30, standing.ToleranceMinutes);
        Assert.AreEqual(AppRoles.ClaimTypes.Permission, AppRoles.Claims.StandingTeeTimeBooking.Type);
        Assert.AreEqual(AppRoles.Permissions.BookStandingTeeTime, AppRoles.Claims.StandingTeeTimeBooking.Value);
        Assert.AreEqual("BR", standing.AdditionalParticipants.Single().MembershipLevel.ShortCode);
    }

    [TestMethod]
    public async Task AppDbContext_PersistsStandingTeeTime_AndGeneratedBookingLink()
    {
        await using var host = await DomainTestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService>();

        var shareholder = await DomainTestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var bronze = await DomainTestData.CreateMembershipLevelAsync(db, "BR", "Bronze");
        var bookingMember = await DomainTestData.CreateMemberAsync(userManager, db, shareholder, "standing@test.com", "Standing", "Member");
        var participant = await DomainTestData.CreateMemberAsync(userManager, db, bronze, "guest@test.com", "Guest", "Player");
        var (_, slot) = await DomainTestData.CreateSeasonAndSlotAsync(
            seasonService,
            db,
            DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
            new TimeOnly(8, 0));

        var standing = new StandingTeeTime
        {
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            RequestedDayOfWeek = slot.Start.DayOfWeek,
            RequestedTime = TimeOnly.FromDateTime(slot.Start),
            StartDate = DateOnly.FromDateTime(slot.Start),
            EndDate = DateOnly.FromDateTime(slot.Start.AddMonths(1)),
            Status = StandingTeeTimeStatus.Approved,
            PriorityNumber = 5,
            ApprovedTime = TimeOnly.FromDateTime(slot.Start)
        };
        standing.AdditionalParticipants.Add(participant);

        db.StandingTeeTimes.Add(standing);
        await db.SaveChangesAsync();

        var generatedBooking = new TeeTimeBooking
        {
            TeeTimeSlotStart = slot.Start,
            TeeTimeSlot = slot,
            BookingMemberId = bookingMember.Id,
            BookingMember = bookingMember,
            StandingTeeTimeId = standing.Id,
            StandingTeeTime = standing,
            AdditionalParticipants = [participant]
        };

        db.TeeTimeBookings.Add(generatedBooking);
        await db.SaveChangesAsync();

        var persisted = await db.StandingTeeTimes
            .Include(item => item.BookingMember)
                .ThenInclude(item => item.MembershipLevel)
            .Include(item => item.AdditionalParticipants)
            .SingleAsync(item => item.Id == standing.Id);
        var linkedBookings = await db.TeeTimeBookings
            .Where(item => item.StandingTeeTimeId == standing.Id)
            .ToListAsync();

        Assert.AreEqual("SH", persisted.BookingMember.MembershipLevel.ShortCode);
        Assert.HasCount(1, linkedBookings);
        Assert.AreEqual(generatedBooking.Id, linkedBookings[0].Id);
        Assert.HasCount(1, persisted.AdditionalParticipants);
        Assert.AreEqual(participant.Id, persisted.AdditionalParticipants[0].Id);
    }
}
