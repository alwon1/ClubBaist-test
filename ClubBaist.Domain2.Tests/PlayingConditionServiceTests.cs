using ClubBaist.Services2.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class PlayingConditionServiceTests
{
    [TestMethod]
    public async Task UpsertAsync_NewDate_CreatesRecord()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PlayingConditionService>();

        var date = DateOnly.FromDateTime(DateTime.Today);
        var result = await service.UpsertAsync(date, 1.0m, "user-1", "Windy day");
        var stored = await service.GetByDateAsync(date);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(stored);
        Assert.AreEqual(1.0m, stored.Adjustment);
        Assert.AreEqual("Windy day", stored.Notes);
    }

    [TestMethod]
    public async Task UpsertAsync_SameDate_UpdatesRecord()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PlayingConditionService>();

        var date = DateOnly.FromDateTime(DateTime.Today);
        await service.UpsertAsync(date, 0.5m, "user-1", "Morning");
        var updateResult = await service.UpsertAsync(date, 1.5m, "user-2", "Afternoon weather shift");
        var stored = await service.GetByDateAsync(date);

        Assert.IsTrue(updateResult.Success);
        Assert.IsNotNull(stored);
        Assert.AreEqual(1.5m, stored.Adjustment);
        Assert.AreEqual("Afternoon weather shift", stored.Notes);
        Assert.AreEqual("user-2", stored.EnteredByUserId);
    }

    [TestMethod]
    public async Task UpsertAsync_InvalidRange_ReturnsError()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PlayingConditionService>();

        var result = await service.UpsertAsync(DateOnly.FromDateTime(DateTime.Today), 3.5m, "user-1", null);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("PCC must be between -1.0 and 3.0.", result.Error);
    }
}