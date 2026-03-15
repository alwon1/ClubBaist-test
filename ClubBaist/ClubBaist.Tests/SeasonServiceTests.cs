using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class SeasonServiceTests
{
    [TestMethod]
    public async Task CreateSeasonAsync_InvalidDateRange_ReturnsValidationFailure()
    {
        using var scope = TestServiceHost.CreateScope();
        var seasonService = scope.ServiceProvider.GetRequiredService<SeasonService<int>>();

        var result = await seasonService.CreateSeasonAsync(
            "Summer",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 8, 31));

        Assert.AreEqual(ServiceResultStatus.Validation, result.Status);
        CollectionAssert.Contains(result.ValidationErrors!.ToList(), "Start date must be on or before end date.");
    }

    [TestMethod]
    public async Task CreateSeasonAsync_OverlappingRange_ReturnsConflict()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var seasonService = provider.GetRequiredService<SeasonService<int>>();

        await seasonService.CreateSeasonAsync(
            "Summer",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 8, 31));

        var result = await seasonService.CreateSeasonAsync(
            "Late Summer",
            new DateOnly(2026, 8, 15),
            new DateOnly(2026, 9, 30));

        Assert.AreEqual(ServiceResultStatus.Conflict, result.Status);
        Assert.AreEqual(SeasonService<int>.SeasonOverlapConflictCode, result.ConflictCode);
    }

    [TestMethod]
    public async Task GetSeasonForDateAsync_DateInsideOpenSeason_ReturnsSeason()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var seasonService = provider.GetRequiredService<SeasonService<int>>();

        var createResult = await seasonService.CreateSeasonAsync(
            "Summer",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 8, 31));

        var getResult = await seasonService.GetSeasonForDateAsync(new DateOnly(2026, 7, 15));

        Assert.AreEqual(ServiceResultStatus.Success, getResult.Status);
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(createResult.Value!.SeasonId, getResult.Value.SeasonId);
    }

    [TestMethod]
    public async Task GetCurrentSeasonAsync_UsesProvidedDate()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var seasonService = provider.GetRequiredService<SeasonService<int>>();

        var createResult = await seasonService.CreateSeasonAsync(
            "Fall",
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 10, 31));

        var currentResult = await seasonService.GetCurrentSeasonAsync(new DateOnly(2026, 9, 10));

        Assert.AreEqual(ServiceResultStatus.Success, currentResult.Status);
        Assert.AreEqual(createResult.Value!.SeasonId, currentResult.Value!.SeasonId);
    }

    [TestMethod]
    public async Task CloseSeasonAsync_AlreadyClosed_ReturnsConflict()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var seasonService = provider.GetRequiredService<SeasonService<int>>();

        var createResult = await seasonService.CreateSeasonAsync(
            "Winter",
            new DateOnly(2026, 11, 1),
            new DateOnly(2027, 2, 28));

        var firstClose = await seasonService.CloseSeasonAsync(
            createResult.Value!.SeasonId,
            new DateOnly(2027, 1, 31));

        Assert.AreEqual(ServiceResultStatus.Success, firstClose.Status);

        var secondClose = await seasonService.CloseSeasonAsync(
            createResult.Value.SeasonId,
            new DateOnly(2027, 2, 1));

        Assert.AreEqual(ServiceResultStatus.Conflict, secondClose.Status);
        Assert.AreEqual(SeasonService<int>.SeasonAlreadyClosedConflictCode, secondClose.ConflictCode);
    }

    [TestMethod]
    public async Task CloseSeasonAsync_ClosesAndTruncatesEndDate()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;

        var seasonService = provider.GetRequiredService<SeasonService<int>>();
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();

        var createResult = await seasonService.CreateSeasonAsync(
            "Spring",
            new DateOnly(2027, 3, 1),
            new DateOnly(2027, 5, 31));

        var closeResult = await seasonService.CloseSeasonAsync(
            createResult.Value!.SeasonId,
            new DateOnly(2027, 4, 15));

        Assert.AreEqual(ServiceResultStatus.Success, closeResult.Status);
        Assert.AreEqual(SeasonStatus.Closed, closeResult.Value!.SeasonStatus);
        Assert.AreEqual(new DateOnly(2027, 4, 15), closeResult.Value.EndDate);

        var persisted = await dbContext.Seasons.AsNoTracking()
            .SingleAsync(item => item.SeasonId == createResult.Value.SeasonId);

        Assert.AreEqual(SeasonStatus.Closed, persisted.SeasonStatus);
        Assert.AreEqual(new DateOnly(2027, 4, 15), persisted.EndDate);
    }
}
