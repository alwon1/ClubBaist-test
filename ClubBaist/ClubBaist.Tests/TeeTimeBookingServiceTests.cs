using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class TeeTimeBookingServiceTests
{
    // All tee time tests use a date within the seeded active season (April–September 2026).
    private static readonly DateOnly SeasonDate = new(2026, 6, 15); // Monday
    private static readonly TimeOnly SlotTime = new(10, 0);

    [TestMethod]
    public async Task CreateReservation_ValidSlot_ReturnsRemainingCapacity()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        var slot = new TeeTimeSlot(SeasonDate, SlotTime, memberId, []);
        var remaining = await bookingService.CreateReservationAsync(slot);

        Assert.IsGreaterThanOrEqualTo(0, remaining, "CreateReservation should succeed for a valid slot");
        Assert.AreEqual(3, remaining); // 4 max - 1 player = 3 remaining
    }

    [TestMethod]
    public async Task CreateReservation_SlotFull_ReturnsNegative()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        // Fill the slot with 4 players across reservations
        var member1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member3 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member4 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Book 2 reservations of 2 players each to fill the slot
        var slot1 = new TeeTimeSlot(SeasonDate, SlotTime, member1, [member2]);
        await bookingService.CreateReservationAsync(slot1);

        var slot2 = new TeeTimeSlot(SeasonDate, SlotTime, member3, [member4]);
        await bookingService.CreateReservationAsync(slot2);

        // Try to book a 5th player
        var member5 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var slot3 = new TeeTimeSlot(SeasonDate, SlotTime, member5, []);
        var result = await bookingService.CreateReservationAsync(slot3);

        Assert.AreEqual(-1, result, "Should fail when slot is full");
    }

    [TestMethod]
    public async Task CreateReservation_OutsideSeason_ReturnsNegative()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // January is outside the April–September season
        var outsideDate = new DateOnly(2026, 1, 15);
        var slot = new TeeTimeSlot(outsideDate, SlotTime, memberId, []);
        var result = await bookingService.CreateReservationAsync(slot);

        Assert.AreEqual(-1, result, "Should fail outside active season");
    }

    [TestMethod]
    public async Task CreateReservation_SilverMember_RestrictedTime_ReturnsNegative()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var memberId = await CreateMemberAsync(provider, MembershipCategory.ShareholderSpouse);

        // Monday at 4:00 PM — Silver members can't book Mon-Fri 3PM-5:30PM
        var restrictedTime = new TimeOnly(16, 0);
        var slot = new TeeTimeSlot(SeasonDate, restrictedTime, memberId, []);
        var result = await bookingService.CreateReservationAsync(slot);

        Assert.AreEqual(-1, result, "Silver members should be restricted at this time on weekdays");
    }

    [TestMethod]
    public async Task CreateReservation_GoldMember_AnyTime_Succeeds()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Monday at 4:00 PM — Gold members can book anytime
        var slot = new TeeTimeSlot(SeasonDate, new TimeOnly(16, 0), memberId, []);
        var result = await bookingService.CreateReservationAsync(slot);

        Assert.IsGreaterThanOrEqualTo(0, result, "Gold members should be able to book anytime");
    }

    [TestMethod]
    public async Task UpdateReservation_ModifiesPlayerList()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var bookerId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player3 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        var slot = new TeeTimeSlot(SeasonDate, SlotTime, bookerId, [player2]);
        await bookingService.CreateReservationAsync(slot);

        var reservation = await dbContext.Reservations
            .FirstAsync(r => r.BookingMemberAccountId == bookerId && !r.IsCancelled);

        // Update to replace player2 with player3
        var result = await bookingService.UpdateReservationAsync(reservation.ReservationId, [player3]);

        Assert.IsGreaterThanOrEqualTo(0, result, "Update should succeed");

        var updated = await dbContext.Reservations
            .AsNoTracking()
            .FirstAsync(r => r.ReservationId == reservation.ReservationId);

        Assert.HasCount(1, updated.PlayerMemberAccountIds);
        Assert.Contains(player3, updated.PlayerMemberAccountIds);
        Assert.DoesNotContain(player2, updated.PlayerMemberAccountIds);
    }

    [TestMethod]
    public async Task CancelReservation_SetsIsCancelled()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        var slot = new TeeTimeSlot(SeasonDate, SlotTime, memberId, []);
        await bookingService.CreateReservationAsync(slot);

        var reservation = await dbContext.Reservations
            .FirstAsync(r => r.BookingMemberAccountId == memberId && !r.IsCancelled);

        var result = await bookingService.CancelReservationAsync(reservation.ReservationId);

        Assert.IsTrue(result, "Cancel should return true");

        var cancelled = await dbContext.Reservations
            .AsNoTracking()
            .FirstAsync(r => r.ReservationId == reservation.ReservationId);

        Assert.IsTrue(cancelled.IsCancelled);
    }

    [TestMethod]
    public async Task GetMemberReservations_ReturnsOnlyThatMembersBookings()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var member1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Member1 books a slot
        var slot1 = new TeeTimeSlot(SeasonDate, SlotTime, member1, []);
        await bookingService.CreateReservationAsync(slot1);

        // Member2 books a different slot
        var slot2 = new TeeTimeSlot(SeasonDate, new TimeOnly(11, 0), member2, []);
        await bookingService.CreateReservationAsync(slot2);

        var member1Reservations = await bookingService.GetMemberReservationsAsync(member1);
        var member2Reservations = await bookingService.GetMemberReservationsAsync(member2);

        Assert.HasCount(1, member1Reservations);
        Assert.AreEqual(member1, member1Reservations[0].BookingMemberAccountId);

        Assert.HasCount(1, member2Reservations);
        Assert.AreEqual(member2, member2Reservations[0].BookingMemberAccountId);
    }

    [TestMethod]
    public async Task CreateReservation_BookingMemberAlreadyBookedAtSlot_ReturnsNegative()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // First booking succeeds
        var slot1 = new TeeTimeSlot(SeasonDate, SlotTime, memberId, []);
        var first = await bookingService.CreateReservationAsync(slot1);
        Assert.IsGreaterThanOrEqualTo(0, first, "First booking should succeed");

        // Second booking at the same slot with the same member should fail
        var slot2 = new TeeTimeSlot(SeasonDate, SlotTime, memberId, []);
        var result = await bookingService.CreateReservationAsync(slot2);

        Assert.AreEqual(-1, result, "Booking member already in this slot should be rejected");
    }

    [TestMethod]
    public async Task CreateReservation_PlayerAlreadyBookedAtSlot_ReturnsNegative()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var booker1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var booker2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var sharedPlayer = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Book the shared player in the first reservation
        var slot1 = new TeeTimeSlot(SeasonDate, SlotTime, booker1, [sharedPlayer]);
        var first = await bookingService.CreateReservationAsync(slot1);
        Assert.IsGreaterThanOrEqualTo(0, first, "First booking should succeed");

        // Attempt to add the same player to a second reservation in the same slot
        var slot2 = new TeeTimeSlot(SeasonDate, SlotTime, booker2, [sharedPlayer]);
        var result = await bookingService.CreateReservationAsync(slot2);

        Assert.AreEqual(-1, result, "Player already in this slot should be rejected");
    }

    [TestMethod]
    public async Task UpdateReservation_NewPlayerAlreadyBooked_ReturnsNegative()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var booker1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var booker2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var takenPlayer = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // First reservation claims takenPlayer
        var slot1 = new TeeTimeSlot(SeasonDate, SlotTime, booker1, [takenPlayer]);
        await bookingService.CreateReservationAsync(slot1);

        // Second reservation with booker2, no extra players yet
        var slot2 = new TeeTimeSlot(SeasonDate, SlotTime, booker2, []);
        await bookingService.CreateReservationAsync(slot2);

        var reservation2 = await dbContext.Reservations
            .FirstAsync(r => r.BookingMemberAccountId == booker2 && !r.IsCancelled);

        // Try to add takenPlayer to the second reservation
        var result = await bookingService.UpdateReservationAsync(reservation2.ReservationId, [takenPlayer]);

        Assert.AreEqual(-1, result, "Updating to include a player already booked in the slot should fail");
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_ReturnsCorrectReservationGroups()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var booker = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        var slot = new TeeTimeSlot(SeasonDate, SlotTime, booker, [player]);
        await bookingService.CreateReservationAsync(slot);

        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(SeasonDate);
        var targetSlot = bookedSlots.FirstOrDefault(s => s.Time == SlotTime);

        Assert.IsNotNull(targetSlot);
        Assert.HasCount(1, targetSlot.Reservations);

        var reservation = targetSlot.Reservations[0];
        Assert.AreEqual(booker, reservation.BookingMember.MemberAccountId);
        Assert.HasCount(1, reservation.Players);
        Assert.AreEqual(player, reservation.Players[0].MemberAccountId);
        Assert.AreEqual(2, targetSlot.RemainingCapacity); // 4 max - 2 booked = 2
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_EmptySlot_ShowsFullCapacity()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(SeasonDate);
        var targetSlot = bookedSlots.FirstOrDefault(s => s.Time == SlotTime);

        Assert.IsNotNull(targetSlot);
        Assert.IsEmpty(targetSlot.Reservations);
        Assert.AreEqual(BookingConstants.MaxPlayersPerSlot, targetSlot.RemainingCapacity);
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_MultipleReservationsInSlot_GroupedSeparately()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var booker1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var booker2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        await bookingService.CreateReservationAsync(new TeeTimeSlot(SeasonDate, SlotTime, booker1, []));
        await bookingService.CreateReservationAsync(new TeeTimeSlot(SeasonDate, SlotTime, booker2, []));

        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(SeasonDate);
        var targetSlot = bookedSlots.FirstOrDefault(s => s.Time == SlotTime);

        Assert.IsNotNull(targetSlot);
        Assert.HasCount(2, targetSlot.Reservations, "Each booking should be its own reservation group");
        Assert.AreEqual(2, targetSlot.RemainingCapacity); // 4 max - 2 booked = 2

        var bookerIds = targetSlot.Reservations.Select(r => r.BookingMember.MemberAccountId).ToHashSet();
        Assert.Contains(booker1, bookerIds);
        Assert.Contains(booker2, bookerIds);
    }

    [TestMethod]
    public async Task GetAvailability_ReturnsCorrectRemainingCapacity()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var memberId = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Book a slot with 2 players
        var player2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var slot = new TeeTimeSlot(SeasonDate, SlotTime, memberId, [player2]);
        await bookingService.CreateReservationAsync(slot);

        var availability = await bookingService.GetAvailabilityAsync(SeasonDate, SeasonDate);

        Assert.HasCount(1, availability);
        var dayAvail = availability[0];
        Assert.AreEqual(SeasonDate, dayAvail.Date);

        var slotAvail = dayAvail.Slots.FirstOrDefault(s => s.Time == SlotTime);
        Assert.IsNotNull(slotAvail);
        Assert.AreEqual(2, slotAvail.RemainingCapacity); // 4 max - 2 booked = 2 remaining
    }

    /// <summary>
    /// Creates a test scope with an active season seeded in the database.
    /// </summary>
    private static IServiceScope CreateScopeWithSeason()
    {
        var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.Seasons.Add(new Season
        {
            SeasonId = Guid.NewGuid(),
            Name = $"Test Season {Guid.NewGuid():N}",
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2026, 9, 30),
            SeasonStatus = SeasonStatus.Active
        });
        dbContext.SaveChanges();

        return scope;
    }

    private static Task<Guid> CreateMemberAsync(
        IServiceProvider provider,
        MembershipCategory category) =>
        TestDataFactory.CreateMemberAsync(
            provider.GetRequiredService<UserManager<IdentityUser<Guid>>>(),
            provider.GetRequiredService<ApplicationDbContext>(),
            category);
}
