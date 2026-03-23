using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class ClubEventServiceTests
{
    private static readonly DateOnly EventDate = new(2026, 7, 4);
    private static readonly DateOnly AnotherDate = new(2026, 8, 15);
    private static readonly TimeOnly StartTime = new(9, 0);
    private static readonly TimeOnly EndTime = new(17, 0);

    [TestMethod]
    public async Task CreateAsync_NotifiesAvailabilityForEventDate()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var service = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += notifiedDates.Add;

        await service.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        CollectionAssert.Contains(notifiedDates, EventDate);
    }

    [TestMethod]
    public async Task UpdateAsync_SameDate_NotifiesOnce()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var service = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var clubEvent = await service.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += notifiedDates.Add;

        var result = await service.UpdateAsync(clubEvent.ClubEventId, "Updated Tournament", EventDate, StartTime, EndTime);

        Assert.IsTrue(result);
        Assert.HasCount(1, notifiedDates);
        CollectionAssert.Contains(notifiedDates, EventDate);
    }

    [TestMethod]
    public async Task UpdateAsync_DateChanged_NotifiesBothDates()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var service = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var clubEvent = await service.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += notifiedDates.Add;

        var result = await service.UpdateAsync(clubEvent.ClubEventId, "Tournament", AnotherDate, StartTime, EndTime);

        Assert.IsTrue(result);
        CollectionAssert.Contains(notifiedDates, AnotherDate);
        CollectionAssert.Contains(notifiedDates, EventDate);
    }

    [TestMethod]
    public async Task DeleteAsync_NotifiesAvailabilityForEventDate()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var service = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var clubEvent = await service.CreateAsync("Tournament", EventDate, StartTime, EndTime);

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += notifiedDates.Add;

        var result = await service.DeleteAsync(clubEvent.ClubEventId);

        Assert.IsTrue(result);
        CollectionAssert.Contains(notifiedDates, EventDate);
    }

    [TestMethod]
    public async Task UpdateAsync_NotFound_ReturnsFalseWithNoNotification()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var service = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += notifiedDates.Add;

        var result = await service.UpdateAsync(Guid.NewGuid(), "Nonexistent", EventDate, StartTime, EndTime);

        Assert.IsFalse(result);
        Assert.IsEmpty(notifiedDates);
    }

    [TestMethod]
    public async Task DeleteAsync_NotFound_ReturnsFalseWithNoNotification()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var service = provider.GetRequiredService<ClubEventService<Guid>>();
        var availabilityUpdates = provider.GetRequiredService<AvailabilityUpdateService>();

        var notifiedDates = new List<DateOnly>();
        availabilityUpdates.AvailabilityChanged += notifiedDates.Add;

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.IsFalse(result);
        Assert.IsEmpty(notifiedDates);
    }
}
