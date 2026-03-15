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
        var bookerId = Guid.NewGuid();

        var slot = new TeeTimeSlot(date, time, bookerId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(3, result); // 4 max - 1 booker = 3 remaining
    }

    [TestMethod]
    public async Task SlotCapacityRule_AtCapacity_ReturnsZero()
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
            BookingMemberAccountId = Guid.NewGuid(),
            PlayerMemberAccountIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]
        });
        await dbContext.SaveChangesAsync();

        var rule = new SlotCapacityRule<Guid>(dbContext);
        var newBookerId = Guid.NewGuid();
        var slot = new TeeTimeSlot(date, time, newBookerId, []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.IsTrue(result < 0, "Should return negative when over capacity");
    }

    [TestMethod]
    public async Task SlotCapacityRule_WithPrecomputedOccupancy_UsesPrecomputedValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var rule = new SlotCapacityRule<Guid>(dbContext);

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), Guid.Empty, []);
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

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), Guid.NewGuid(), []);
        var context = new BookingEvaluationContext(MembershipCategory.Shareholder);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result);
    }

    [TestMethod]
    public async Task BookingWindowRule_DateOutsideSeason_ReturnsZero()
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
        var slot = new TeeTimeSlot(new DateOnly(2026, 1, 15), new TimeOnly(10, 0), Guid.NewGuid(), []);
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

        var slot = new TeeTimeSlot(date, time, Guid.NewGuid(), []);
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

        var slot = new TeeTimeSlot(new DateOnly(2026, 6, 15), new TimeOnly(10, 0), Guid.Empty, []);
        var context = new BookingEvaluationContext(null);

        var result = await rule.EvaluateAsync(slot, context);

        Assert.AreEqual(int.MaxValue, result, "Null category (availability query) should pass");
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
