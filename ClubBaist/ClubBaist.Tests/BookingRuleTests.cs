using ClubBaist.Domain;
using ClubBaist.Services;
using ClubBaist.Services.Rules;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class BookingRuleTests
{
    #region SlotCapacityRule

    [TestMethod]
    public async Task SlotCapacityRule_UnderCapacity_ReturnsPositiveRemaining()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var rule = new SlotCapacityRule<Guid>(dbContext);

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);
        var bookerId = 1;

        var slot = new TeeTimeSlot(date, time, bookerId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(3, result); // 4 max - 1 booker = 3 remaining
    }

    [TestMethod]
    public async Task SlotCapacityRule_AtCapacity_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);

        // Pre-fill the slot with 4 players
        dbContext.Reservations.Add(new Reservation
        {
            SlotDate = date,
            SlotTime = time,
            BookingMemberAccountId = 1,
            PlayerMemberAccountIds = [2, 3, 4]
        });
        await dbContext.SaveChangesAsync();

        var rule = new SlotCapacityRule<Guid>(dbContext);
        var newBookerId = 5;
        var slot = new TeeTimeSlot(date, time, newBookerId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.IsLessThan(0, result, "Should return negative when over capacity");
    }

    [TestMethod]
    public async Task SlotCapacityRule_WithPrecomputedOccupancy_UsesPrecomputedValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var rule = new SlotCapacityRule<Guid>(dbContext);

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), 0, []);
        var context = new BookingEvaluationContext(null, PrecomputedOccupancy: 2);

        var result = await rule.EvaluateAsync(slot, context);

        // Availability query (Guid.Empty booker) → requested = 0, so remaining = 4 - 2 = 2
        Assert.AreEqual(2, result);
    }

    #endregion

    #region BookingWindowRule

    [TestMethod]
    public async Task BookingWindowRule_DateWithinSeason_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        // Seed an active season
        dbContext.Seasons.Add(new Season
        {
            SeasonId = Guid.NewGuid(),
            Name = $"Rule Test Season {Guid.NewGuid():N}",
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2026, 9, 30),
            SeasonStatus = SeasonStatus.Active
        });
        await dbContext.SaveChangesAsync();

        // Re-resolve ISeasonService so it picks up the seeded season
        var seasonService = provider.GetRequiredService<ISeasonService>();
        var rule = new BookingWindowRule(seasonService);

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result);
    }

    [TestMethod]
    public async Task BookingWindowRule_DateOutsideSeason_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        // Seed an active season
        dbContext.Seasons.Add(new Season
        {
            SeasonId = Guid.NewGuid(),
            Name = $"Rule Test Season {Guid.NewGuid():N}",
            StartDate = new DateOnly(2026, 4, 1),
            EndDate = new DateOnly(2026, 9, 30),
            SeasonStatus = SeasonStatus.Active
        });
        await dbContext.SaveChangesAsync();

        var seasonService = provider.GetRequiredService<ISeasonService>();
        var rule = new BookingWindowRule(seasonService);

        // January is outside the season
        var slot = new TeeTimeSlot(new DateOnly(2026, 1, 15), new TimeOnly(10, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(-1, result);
    }

    #endregion

    #region MembershipTimeRestrictionRule

    [TestMethod]
    [DataRow(MembershipCategory.Shareholder, "Monday", "16:00", true, DisplayName = "Gold weekday 4PM - allowed")]
    [DataRow(MembershipCategory.Associate, "Saturday", "08:00", true, DisplayName = "Gold weekend 8AM - allowed")]
    [DataRow(MembershipCategory.ShareholderSpouse, "Monday", "10:00", true, DisplayName = "Silver weekday 10AM - allowed (before 3PM)")]
    [DataRow(MembershipCategory.ShareholderSpouse, "Monday", "16:00", false, DisplayName = "Silver weekday 4PM - restricted (3PM-5:30PM)")]
    [DataRow(MembershipCategory.ShareholderSpouse, "Monday", "18:00", true, DisplayName = "Silver weekday 6PM - allowed (after 5:30PM)")]
    [DataRow(MembershipCategory.ShareholderSpouse, "Saturday", "11:00", true, DisplayName = "Silver weekend 11AM - allowed")]
    [DataRow(MembershipCategory.ShareholderSpouse, "Saturday", "10:00", false, DisplayName = "Silver weekend 10AM - restricted (before 11AM)")]
    [DataRow(MembershipCategory.Junior, "Monday", "10:00", true, DisplayName = "Bronze weekday 10AM - allowed (before 3PM)")]
    [DataRow(MembershipCategory.Junior, "Monday", "16:00", false, DisplayName = "Bronze weekday 4PM - restricted (3PM-6PM)")]
    [DataRow(MembershipCategory.Junior, "Monday", "18:00", true, DisplayName = "Bronze weekday 6PM - allowed")]
    [DataRow(MembershipCategory.Junior, "Saturday", "13:00", true, DisplayName = "Bronze weekend 1PM - allowed")]
    [DataRow(MembershipCategory.Junior, "Saturday", "12:00", false, DisplayName = "Bronze weekend 12PM - restricted (before 1PM)")]
    [DataRow(MembershipCategory.Social, "Monday", "10:00", false, DisplayName = "Social - no golf privileges")]
    public async Task MembershipTimeRestrictionRule_EnforcesRestrictions(
        MembershipCategory category,
        string dayOfWeek,
        string timeStr,
        bool expectedAllowed)
    {
        var rule = new MembershipTimeRestrictionRule();

        // Pick a date that matches the day of week within the 2026 season
        var date = GetDateForDayOfWeek(dayOfWeek);
        var time = TimeOnly.Parse(timeStr);

        var slot = new TeeTimeSlot(date, time, 1, []);
        var context = new BookingEvaluationContext(category);

        var result = await rule.EvaluateAsync(slot, context);

        if (expectedAllowed)
            Assert.AreEqual(int.MaxValue, result, $"{category} should be allowed on {dayOfWeek} at {timeStr}");
        else
            Assert.AreEqual(-1, result, $"{category} should be restricted on {dayOfWeek} at {timeStr}");
    }

    [TestMethod]
    public async Task MembershipTimeRestrictionRule_NullCategory_ReturnsMaxValue()
    {
        var rule = new MembershipTimeRestrictionRule();

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), 0, []);
        var context = new BookingEvaluationContext(null);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Null category (availability query) should pass");
    }

    #endregion

    #region MemberConflictRule

    [TestMethod]
    public async Task MemberConflictRule_NoExistingReservations_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rule = new MemberConflictRule<Guid>(dbContext);

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result);
    }

    [TestMethod]
    public async Task MemberConflictRule_BookingMemberAlreadyBooked_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);
        var memberId = 1;

        dbContext.Reservations.Add(new Reservation
        {
            SlotDate = date,
            SlotTime = time,
            BookingMemberAccountId = memberId,
            PlayerMemberAccountIds = []
        });
        await dbContext.SaveChangesAsync();

        var rule = new MemberConflictRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(date, time, memberId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(-1, result, "Booking member already in this slot should be blocked");
    }

    [TestMethod]
    public async Task MemberConflictRule_PlayerAlreadyBookedAsPlayer_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);
        var existingBooker = 1;
        var conflictingPlayer = 2;

        dbContext.Reservations.Add(new Reservation
        {
            SlotDate = date,
            SlotTime = time,
            BookingMemberAccountId = existingBooker,
            PlayerMemberAccountIds = [conflictingPlayer]
        });
        await dbContext.SaveChangesAsync();

        var rule = new MemberConflictRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(date, time, 3, [conflictingPlayer]);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(-1, result, "Player already in this slot should be blocked");
    }

    [TestMethod]
    public async Task MemberConflictRule_PlayerAlreadyBookedAsBookingMember_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);
        var existingBooker = 1;

        dbContext.Reservations.Add(new Reservation
        {
            SlotDate = date,
            SlotTime = time,
            BookingMemberAccountId = existingBooker,
            PlayerMemberAccountIds = []
        });
        await dbContext.SaveChangesAsync();

        var rule = new MemberConflictRule<Guid>(dbContext);
        // Try to add existingBooker as a player in a new reservation
        var slot = new TeeTimeSlot(date, time, 2, [existingBooker]);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(-1, result, "Existing booking member added as a player should be blocked");
    }

    [TestMethod]
    public async Task MemberConflictRule_AvailabilityQuery_GuidEmpty_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);

        dbContext.Reservations.Add(new Reservation
        {
            SlotDate = date,
            SlotTime = time,
            BookingMemberAccountId = 1,
            PlayerMemberAccountIds = []
        });
        await dbContext.SaveChangesAsync();

        var rule = new MemberConflictRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(date, time, 0, []);
        var context = new BookingEvaluationContext(null, PrecomputedOccupancy: 1);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Availability queries (Guid.Empty) should skip conflict check");
    }

    [TestMethod]
    public async Task MemberConflictRule_UpdateExcludesOwnReservation_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);
        var bookerId = 1;
        var reservationId = Guid.NewGuid();

        dbContext.Reservations.Add(new Reservation
        {
            ReservationId = reservationId,
            SlotDate = date,
            SlotTime = time,
            BookingMemberAccountId = bookerId,
            PlayerMemberAccountIds = []
        });
        await dbContext.SaveChangesAsync();

        var rule = new MemberConflictRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(date, time, bookerId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder, ExcludeReservationId: reservationId);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Updating own reservation should not conflict with itself");
    }

    [TestMethod]
    public async Task MemberConflictRule_CancelledReservation_NotConsideredConflict()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var time = new TimeOnly(10, 0);
        var memberId = 1;

        dbContext.Reservations.Add(new Reservation
        {
            SlotDate = date,
            SlotTime = time,
            BookingMemberAccountId = memberId,
            PlayerMemberAccountIds = [],
            IsCancelled = true
        });
        await dbContext.SaveChangesAsync();

        var rule = new MemberConflictRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(date, time, memberId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Cancelled reservations should not block re-booking");
    }

    [TestMethod]
    public async Task MemberConflictRule_DifferentTimeSlot_NoConflict()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var date = new DateOnly(2026, 6, 15);
        var memberId = 1;

        dbContext.Reservations.Add(new Reservation
        {
            SlotDate = date,
            SlotTime = new TimeOnly(10, 0),
            BookingMemberAccountId = memberId,
            PlayerMemberAccountIds = []
        });
        await dbContext.SaveChangesAsync();

        var rule = new MemberConflictRule<Guid>(dbContext);
        // Same member, different time — should be fine
        var slot = new TeeTimeSlot(date, new TimeOnly(11, 0), memberId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Same member at a different time slot should not conflict");
    }

    #endregion

    #region ClubEventBlockingRule

    [TestMethod]
    public async Task ClubEventBlockingRule_NoEventOnDate_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rule = new ClubEventBlockingRule<Guid>(dbContext);

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "No event on date should allow booking");
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotWithinEventWindow_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var eventDate = new DateOnly(2026, 6, 15);
        dbContext.ClubEvents.Add(new ClubEvent
        {
            Name = "Club Championship",
            EventDate = eventDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(14, 0)
        });
        await dbContext.SaveChangesAsync();

        var rule = new ClubEventBlockingRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(eventDate, new TimeOnly(10, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(-1, result, "Slot within event window should be blocked");
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotBeforeEvent_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var eventDate = new DateOnly(2026, 6, 15);
        dbContext.ClubEvents.Add(new ClubEvent
        {
            Name = "Afternoon Event",
            EventDate = eventDate,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0)
        });
        await dbContext.SaveChangesAsync();

        var rule = new ClubEventBlockingRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(eventDate, new TimeOnly(10, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Slot before event window should not be blocked");
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotAfterEvent_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var eventDate = new DateOnly(2026, 6, 15);
        dbContext.ClubEvents.Add(new ClubEvent
        {
            Name = "Morning Event",
            EventDate = eventDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(11, 0)
        });
        await dbContext.SaveChangesAsync();

        var rule = new ClubEventBlockingRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(eventDate, new TimeOnly(14, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Slot after event window should not be blocked");
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_EventOnDifferentDate_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        dbContext.ClubEvents.Add(new ClubEvent
        {
            Name = "Other Day Event",
            EventDate = new DateOnly(2026, 6, 16),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(18, 0)
        });
        await dbContext.SaveChangesAsync();

        var rule = new ClubEventBlockingRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Event on a different date should not block this slot");
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotAtExactStartTime_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var eventDate = new DateOnly(2026, 6, 15);
        var startTime = new TimeOnly(9, 0);
        dbContext.ClubEvents.Add(new ClubEvent
        {
            Name = "Start Boundary Event",
            EventDate = eventDate,
            StartTime = startTime,
            EndTime = new TimeOnly(12, 0)
        });
        await dbContext.SaveChangesAsync();

        var rule = new ClubEventBlockingRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(eventDate, startTime, 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(-1, result, "Slot exactly at event start time should be blocked");
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotAtExactEndTime_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var eventDate = new DateOnly(2026, 6, 15);
        var endTime = new TimeOnly(12, 0);
        dbContext.ClubEvents.Add(new ClubEvent
        {
            Name = "End Boundary Event",
            EventDate = eventDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = endTime
        });
        await dbContext.SaveChangesAsync();

        var rule = new ClubEventBlockingRule<Guid>(dbContext);
        var slot = new TeeTimeSlot(eventDate, endTime, 1, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(-1, result, "Slot exactly at event end time should be blocked");
    }

    #endregion

    /// <summary>
    /// Returns a DateOnly in June 2026 matching the specified day of week.
    /// </summary>
    private static DateOnly GetDateForDayOfWeek(string dayOfWeek)
    {
        var target = Enum.Parse<DayOfWeek>(dayOfWeek);
        var date = new DateOnly(2026, 6, 1); // June 1, 2026 is a Monday

        while (date.DayOfWeek != target)
            date = date.AddDays(1);

        return date;
    }
}
