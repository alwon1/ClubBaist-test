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
    public async Task GetBookedSlotsWithMembersForRange_ReturnsCorrectPerDayStructure()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var day1 = SeasonDate;             // Monday 2026-06-15
        var day2 = SeasonDate.AddDays(1);  // Tuesday 2026-06-16

        var booker1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var booker2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var player = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        await bookingService.CreateReservationAsync(new TeeTimeSlot(day1, SlotTime, booker1, [player]));
        await bookingService.CreateReservationAsync(new TeeTimeSlot(day2, SlotTime, booker2, []));

        var result = await bookingService.GetBookedSlotsWithMembersForRangeAsync([day1, day2]);

        // Both dates should be present
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.ContainsKey(day1));
        Assert.IsTrue(result.ContainsKey(day2));

        // day1: 1 reservation with booker1 + player (2 players → 2 remaining)
        var day1Slot = result[day1].FirstOrDefault(s => s.Time == SlotTime);
        Assert.IsNotNull(day1Slot);
        Assert.AreEqual(1, day1Slot.Reservations.Count);
        Assert.AreEqual(booker1, day1Slot.Reservations[0].BookingMember.MemberAccountId);
        Assert.AreEqual(1, day1Slot.Reservations[0].Players.Count);
        Assert.AreEqual(player, day1Slot.Reservations[0].Players[0].MemberAccountId);
        Assert.AreEqual(2, day1Slot.RemainingCapacity); // 4 max - 2 booked = 2

        // day2: 1 reservation with booker2 alone (3 remaining)
        var day2Slot = result[day2].FirstOrDefault(s => s.Time == SlotTime);
        Assert.IsNotNull(day2Slot);
        Assert.AreEqual(1, day2Slot.Reservations.Count);
        Assert.AreEqual(booker2, day2Slot.Reservations[0].BookingMember.MemberAccountId);
        Assert.AreEqual(0, day2Slot.Reservations[0].Players.Count);
        Assert.AreEqual(3, day2Slot.RemainingCapacity); // 4 max - 1 booked = 3
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembersForRange_EmptyDates_ReturnsEmptyDictionary()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();
        var result = await bookingService.GetBookedSlotsWithMembersForRangeAsync([]);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_NullCategory_UserCanBookIsAlwaysTrue()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        // Restricted weekday time; with null category the flag should still be true
        var restrictedTime = new TimeOnly(16, 0); // Mon 4PM
        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(SeasonDate, memberCategory: null);
        var slot = bookedSlots.FirstOrDefault(s => s.Time == restrictedTime);

        Assert.IsNotNull(slot);
        Assert.IsTrue(slot.UserCanBook, "UserCanBook should be true when no memberCategory is provided");
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_ShareholderCategory_UnrestrictedTime_UserCanBookIsTrue()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        // Shareholder (Gold) can book at any time
        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(
            SeasonDate, memberCategory: MembershipCategory.Shareholder);
        var slot = bookedSlots.FirstOrDefault(s => s.Time == new TimeOnly(16, 0)); // Mon 4PM

        Assert.IsNotNull(slot);
        Assert.IsTrue(slot.UserCanBook, "Shareholder (Gold) members should be able to book at 4PM on a weekday");
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_RestrictedCategory_RestrictedTime_UserCanBookIsFalse()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        // ShareholderSpouse (Silver) cannot book Mon-Fri 3PM-5:30PM
        var restrictedTime = new TimeOnly(16, 0); // Mon 4PM
        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(
            SeasonDate, memberCategory: MembershipCategory.ShareholderSpouse);
        var slot = bookedSlots.FirstOrDefault(s => s.Time == restrictedTime);

        Assert.IsNotNull(slot);
        Assert.IsFalse(slot.UserCanBook, "Silver members should be restricted on weekdays between 3PM and 5:30PM");
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_RestrictedCategory_AllowedTime_UserCanBookIsTrue()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        // ShareholderSpouse (Silver) CAN book before 3PM on weekdays
        var allowedTime = new TimeOnly(10, 0); // Mon 10AM
        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(
            SeasonDate, memberCategory: MembershipCategory.ShareholderSpouse);
        var slot = bookedSlots.FirstOrDefault(s => s.Time == allowedTime);

        Assert.IsNotNull(slot);
        Assert.IsTrue(slot.UserCanBook, "Silver members should be allowed to book before 3PM on a weekday");
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembersForRange_UserCanBook_VariesBySlotTimeAndCategory()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        // SeasonDate is a Monday; pass two days
        var day1 = SeasonDate;
        var day2 = SeasonDate.AddDays(1); // Tuesday

        // ShareholderSpouse (Silver): restricted Mon-Fri 3PM-5:30PM
        var result = await bookingService.GetBookedSlotsWithMembersForRangeAsync(
            [day1, day2], memberCategory: MembershipCategory.ShareholderSpouse);

        // Allowed time: 10AM
        var allowedSlotDay1 = result[day1].FirstOrDefault(s => s.Time == new TimeOnly(10, 0));
        Assert.IsNotNull(allowedSlotDay1);
        Assert.IsTrue(allowedSlotDay1.UserCanBook, "Silver member should be allowed at 10AM on Monday");

        // Restricted time: 4PM on Monday (weekday 3PM-5:30PM restriction)
        var restrictedSlotDay1 = result[day1].FirstOrDefault(s => s.Time == new TimeOnly(16, 0));
        Assert.IsNotNull(restrictedSlotDay1);
        Assert.IsFalse(restrictedSlotDay1.UserCanBook, "Silver member should be restricted at 4PM on Monday");

        // Tuesday restricted time: 4PM should also be false
        var restrictedSlotDay2 = result[day2].FirstOrDefault(s => s.Time == new TimeOnly(16, 0));
        Assert.IsNotNull(restrictedSlotDay2);
        Assert.IsFalse(restrictedSlotDay2.UserCanBook, "Silver member should be restricted at 4PM on Tuesday");
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_MemberAlreadyBooked_UserCanBookIsFalse()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var booker = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Book the slot for the member
        await bookingService.CreateReservationAsync(new TeeTimeSlot(SeasonDate, SlotTime, booker, []));

        // Query with the same member's ID — conflict rule should deny
        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(SeasonDate, MembershipCategory.Shareholder, booker);
        var targetSlot = bookedSlots.FirstOrDefault(s => s.Time == SlotTime);

        Assert.IsNotNull(targetSlot);
        Assert.IsFalse(targetSlot.UserCanBook, "UserCanBook should be false when the member is already booked in the slot");
    }

    [TestMethod]
    public async Task GetBookedSlotsWithMembers_SlotFull_UserCanBookIsFalse()
    {
        using var scope = CreateScopeWithSeason();
        var provider = scope.ServiceProvider;

        var bookingService = provider.GetRequiredService<TeeTimeBookingService<Guid>>();

        var member1 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member2 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member3 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var member4 = await CreateMemberAsync(provider, MembershipCategory.Shareholder);

        // Fill the slot to capacity
        await bookingService.CreateReservationAsync(new TeeTimeSlot(SeasonDate, SlotTime, member1, [member2]));
        await bookingService.CreateReservationAsync(new TeeTimeSlot(SeasonDate, SlotTime, member3, [member4]));

        // A new member tries to check availability — slot is full
        var newMember = await CreateMemberAsync(provider, MembershipCategory.Shareholder);
        var bookedSlots = await bookingService.GetBookedSlotsWithMembersAsync(SeasonDate, MembershipCategory.Shareholder, newMember);
        var targetSlot = bookedSlots.FirstOrDefault(s => s.Time == SlotTime);

        Assert.IsNotNull(targetSlot);
        Assert.IsFalse(targetSlot.UserCanBook, "UserCanBook should be false when the slot is full");
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

    private static Task<int> CreateMemberAsync(
        IServiceProvider provider,
        MembershipCategory category) =>
        TestDataFactory.CreateMemberAsync(
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<ApplicationDbContext>(),
            category);
}
