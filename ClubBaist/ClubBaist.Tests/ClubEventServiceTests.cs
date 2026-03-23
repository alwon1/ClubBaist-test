using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class ClubEventServiceTests
{
    private static readonly DateOnly EventDate = new(2026, 6, 15);
    private static readonly TimeOnly StartTime = new(10, 0);
    private static readonly TimeOnly EndTime = new(12, 0);

    [TestMethod]
    public async Task CreateAsync_ValidTimeWindow_CreatesEvent()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var clubEvent = await service.CreateAsync("Tournament", EventDate, StartTime, EndTime, "Annual event");

        Assert.IsNotNull(clubEvent);
        Assert.AreEqual("Tournament", clubEvent.Name);
        Assert.AreEqual(StartTime, clubEvent.StartTime);
        Assert.AreEqual(EndTime, clubEvent.EndTime);
    }

    [TestMethod]
    public async Task CreateAsync_EndTimeBeforeStartTime_ThrowsArgumentException()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.CreateAsync("Bad Event", EventDate, EndTime, StartTime));
    }

    [TestMethod]
    public async Task CreateAsync_EndTimeEqualToStartTime_ThrowsArgumentException()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.CreateAsync("Bad Event", EventDate, StartTime, StartTime));
    }

    [TestMethod]
    public async Task UpdateAsync_ValidTimeWindow_UpdatesEvent()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var clubEvent = await service.CreateAsync("Original", EventDate, StartTime, EndTime);
        var newEnd = new TimeOnly(14, 0);

        var result = await service.UpdateAsync(clubEvent.ClubEventId, "Updated", EventDate, StartTime, newEnd, "New desc");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task UpdateAsync_EndTimeBeforeStartTime_ThrowsArgumentException()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var clubEvent = await service.CreateAsync("Original", EventDate, StartTime, EndTime);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.UpdateAsync(clubEvent.ClubEventId, "Updated", EventDate, EndTime, StartTime));
    }

    [TestMethod]
    public async Task UpdateAsync_EndTimeEqualToStartTime_ThrowsArgumentException()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var clubEvent = await service.CreateAsync("Original", EventDate, StartTime, EndTime);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => service.UpdateAsync(clubEvent.ClubEventId, "Updated", EventDate, StartTime, StartTime));
    }

    [TestMethod]
    public async Task UpdateAsync_NonExistentEvent_ReturnsFalse()
    {
        using var scope = TestServiceHost.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ClubEventService<Guid>>();

        var result = await service.UpdateAsync(Guid.NewGuid(), "Ghost", EventDate, StartTime, EndTime);

        Assert.IsFalse(result);
    }
}
