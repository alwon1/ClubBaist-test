using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class ClubEventServiceTests
{
    private static readonly DateOnly EventDate = new(2026, 7, 4);
    private static readonly TimeOnly StartTime = new(8, 0);
    private static readonly TimeOnly EndTime = new(17, 0);

    [TestMethod]
    public async Task CreateAsync_NotifiesAvailabilityForEventDate()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var clubEventService = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        DateOnly? notifiedDate = null;
        availabilityUpdates.AvailabilityChanged += d => notifiedDate = d;

        await clubEventService.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        Assert.AreEqual(EventDate, notifiedDate, "CreateAsync should notify availability for the event date");
    }

    [TestMethod]
    public async Task UpdateAsync_SameDate_NotifiesAvailabilityOnce()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var clubEventService = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var clubEvent = await clubEventService.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += d => notifiedDates.Add(d);

        await clubEventService.UpdateAsync(clubEvent.ClubEventId, "Tournament Updated", EventDate, StartTime, EndTime);

        Assert.HasCount(1, notifiedDates, "UpdateAsync with same date should notify exactly once");
        Assert.AreEqual(EventDate, notifiedDates[0]);
    }

    [TestMethod]
    public async Task UpdateAsync_DateChanged_NotifiesBothOldAndNewDate()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var clubEventService = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var clubEvent = await clubEventService.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += d => notifiedDates.Add(d);

        var newDate = EventDate.AddDays(7);
        await clubEventService.UpdateAsync(clubEvent.ClubEventId, "Tournament", newDate, StartTime, EndTime);

        Assert.HasCount(2, notifiedDates, "UpdateAsync with date change should notify both old and new date");
        CollectionAssert.Contains(notifiedDates, newDate);
        CollectionAssert.Contains(notifiedDates, EventDate);
    }

    [TestMethod]
    public async Task DeleteAsync_NotifiesAvailabilityForEventDate()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var clubEventService = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var clubEvent = await clubEventService.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        DateOnly? notifiedDate = null;
        availabilityUpdates.AvailabilityChanged += d => notifiedDate = d;

        var deleted = await clubEventService.DeleteAsync(clubEvent.ClubEventId);

        Assert.IsTrue(deleted);
        Assert.AreEqual(EventDate, notifiedDate, "DeleteAsync should notify availability for the event date");
    }

    [TestMethod]
    public async Task DeleteAsync_NotFound_DoesNotNotify()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var clubEventService = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var notified = false;
        availabilityUpdates.AvailabilityChanged += _ => notified = true;

        var deleted = await clubEventService.DeleteAsync(Guid.NewGuid());

        Assert.IsFalse(deleted);
        Assert.IsFalse(notified, "DeleteAsync should not notify when event is not found");
    }
}
