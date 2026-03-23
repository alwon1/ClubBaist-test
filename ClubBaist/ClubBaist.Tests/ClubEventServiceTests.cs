using ClubBaist.Domain;
using ClubBaist.Services;
using ClubBaist.Services.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class ClubEventServiceTests
{
    private static readonly DateOnly EventDate = new(2026, 7, 4);
    private static readonly TimeOnly StartTime = new(9, 0);
    private static readonly TimeOnly EndTime = new(12, 0);

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CreateAsync_ValidEvent_PersistsToDatabase()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var created = await service.CreateAsync("Club Championship", EventDate, StartTime, EndTime, "Annual event");

        Assert.AreNotEqual(Guid.Empty, created.ClubEventId);
        Assert.AreEqual("Club Championship", created.Name);
        Assert.AreEqual(EventDate, created.EventDate);
        Assert.AreEqual(StartTime, created.StartTime);
        Assert.AreEqual(EndTime, created.EndTime);
        Assert.AreEqual("Annual event", created.Description);

        var stored = await db.ClubEvents.FindAsync(created.ClubEventId);
        Assert.IsNotNull(stored);
        Assert.AreEqual("Club Championship", stored.Name);
    }

    [TestMethod]
    public async Task CreateAsync_NullDescription_PersistsWithNullDescription()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var created = await service.CreateAsync("Ladies Day", EventDate, StartTime, EndTime);

        var stored = await db.ClubEvents.FindAsync(created.ClubEventId);
        Assert.IsNotNull(stored);
        Assert.IsNull(stored.Description);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAsync_ExistingEvent_UpdatesAllFields()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var created = await service.CreateAsync("Old Name", EventDate, StartTime, EndTime);

        var newDate = EventDate.AddDays(1);
        var newStart = new TimeOnly(10, 0);
        var newEnd = new TimeOnly(14, 0);

        var result = await service.UpdateAsync(created.ClubEventId, "New Name", newDate, newStart, newEnd, "Updated desc");

        Assert.IsTrue(result);

        var stored = await db.ClubEvents.FindAsync(created.ClubEventId);
        Assert.IsNotNull(stored);
        Assert.AreEqual("New Name", stored.Name);
        Assert.AreEqual(newDate, stored.EventDate);
        Assert.AreEqual(newStart, stored.StartTime);
        Assert.AreEqual(newEnd, stored.EndTime);
        Assert.AreEqual("Updated desc", stored.Description);
    }

    [TestMethod]
    public async Task UpdateAsync_NonExistentEvent_ReturnsFalse()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var result = await service.UpdateAsync(Guid.NewGuid(), "X", EventDate, StartTime, EndTime);

        Assert.IsFalse(result);
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DeleteAsync_ExistingEvent_RemovesFromDatabase()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var created = await service.CreateAsync("Delete Me", EventDate, StartTime, EndTime);

        var result = await service.DeleteAsync(created.ClubEventId);

        Assert.IsTrue(result);
        var stored = await db.ClubEvents.FindAsync(created.ClubEventId);
        Assert.IsNull(stored);
    }

    [TestMethod]
    public async Task DeleteAsync_NonExistentEvent_ReturnsFalse()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.IsFalse(result);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GetAllAsync_MultipleEvents_ReturnsOrderedByDateThenTime()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var later = EventDate.AddDays(1);
        await service.CreateAsync("Afternoon", EventDate, new TimeOnly(14, 0), new TimeOnly(17, 0));
        await service.CreateAsync("Morning", EventDate, new TimeOnly(8, 0), new TimeOnly(11, 0));
        await service.CreateAsync("Next Day", later, new TimeOnly(9, 0), new TimeOnly(12, 0));

        var all = await service.GetAllAsync();

        Assert.HasCount(3, all);
        Assert.AreEqual("Morning", all[0].Name);
        Assert.AreEqual("Afternoon", all[1].Name);
        Assert.AreEqual("Next Day", all[2].Name);
    }

    [TestMethod]
    public async Task GetAllAsync_NoEvents_ReturnsEmptyList()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var all = await service.GetAllAsync();

        Assert.HasCount(0, all);
    }

    // ── GetByDateRangeAsync ──────────────────────────────────────────────────

    [TestMethod]
    public async Task GetByDateRangeAsync_EventsWithinRange_ReturnsOnlyMatching()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var inRange1 = new DateOnly(2026, 7, 5);
        var inRange2 = new DateOnly(2026, 7, 10);
        var before = new DateOnly(2026, 7, 1);
        var after = new DateOnly(2026, 7, 20);

        await service.CreateAsync("Before", before, StartTime, EndTime);
        await service.CreateAsync("InRange1", inRange1, StartTime, EndTime);
        await service.CreateAsync("InRange2", inRange2, StartTime, EndTime);
        await service.CreateAsync("After", after, StartTime, EndTime);

        var results = await service.GetByDateRangeAsync(inRange1, inRange2);

        Assert.HasCount(2, results);
        Assert.IsTrue(results.All(e => e.EventDate >= inRange1 && e.EventDate <= inRange2));
    }

    [TestMethod]
    public async Task GetByDateRangeAsync_IncludesBoundaryDates()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var from = new DateOnly(2026, 8, 1);
        var to = new DateOnly(2026, 8, 31);

        await service.CreateAsync("First Day", from, StartTime, EndTime);
        await service.CreateAsync("Last Day", to, StartTime, EndTime);

        var results = await service.GetByDateRangeAsync(from, to);

        Assert.HasCount(2, results);
    }

    [TestMethod]
    public async Task GetByDateRangeAsync_NoEventsInRange_ReturnsEmpty()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        await service.CreateAsync("Outside", new DateOnly(2026, 5, 1), StartTime, EndTime);

        var results = await service.GetByDateRangeAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        Assert.HasCount(0, results);
    }

    // ── ClubEventBlockingRule ────────────────────────────────────────────────

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotDuringEvent_ReturnsNegative()
    {
        using var scope = TestServiceHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rule = scope.ServiceProvider.GetRequiredService<IEnumerable<IBookingRule>>()
            .OfType<ClubEventBlockingRule<Guid>>()
            .Single();

        db.ClubEvents.Add(new ClubEvent
        {
            Name = "Blocked Event",
            EventDate = EventDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(13, 0)
        });
        await db.SaveChangesAsync();

        // Slot time falls within the event window
        var slot = new TeeTimeSlot(EventDate, new TimeOnly(10, 0), 1, []);
        var result = await rule.EvaluateAsync(slot, new BookingEvaluationContext(null));

        Assert.AreEqual(-1, result);
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotOutsideEvent_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rule = scope.ServiceProvider.GetRequiredService<IEnumerable<IBookingRule>>()
            .OfType<ClubEventBlockingRule<Guid>>()
            .Single();

        db.ClubEvents.Add(new ClubEvent
        {
            Name = "Morning Event",
            EventDate = EventDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(11, 0)
        });
        await db.SaveChangesAsync();

        // Slot time is after the event window
        var slot = new TeeTimeSlot(EventDate, new TimeOnly(14, 0), 1, []);
        var result = await rule.EvaluateAsync(slot, new BookingEvaluationContext(null));

        Assert.AreEqual(int.MaxValue, result);
    }

    [TestMethod]
    public async Task ClubEventBlockingRule_SlotOnDifferentDate_ReturnsMaxValue()
    {
        using var scope = TestServiceHost.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rule = scope.ServiceProvider.GetRequiredService<IEnumerable<IBookingRule>>()
            .OfType<ClubEventBlockingRule<Guid>>()
            .Single();

        db.ClubEvents.Add(new ClubEvent
        {
            Name = "Event",
            EventDate = EventDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(18, 0)
        });
        await db.SaveChangesAsync();

        // Slot is on a completely different date
        var slot = new TeeTimeSlot(EventDate.AddDays(1), new TimeOnly(10, 0), 1, []);
        var result = await rule.EvaluateAsync(slot, new BookingEvaluationContext(null));

        Assert.AreEqual(int.MaxValue, result);
    }
}
