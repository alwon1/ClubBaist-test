using ClubBaist.Domain.Entities.Scoring;
using ClubBaist.Services.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain.Tests;

[TestClass]
public class CourseHoleReferenceServiceTests
{
    [TestMethod]
    public async Task GetHoleReferencesAsync_ReturnsEighteenHolesForEachTee()
    {
        await using var host = await DomainTestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<CourseHoleReferenceService>();

        foreach (var tee in Enum.GetValues<GolfRound.TeeColor>())
        {
            var holes = await service.GetHoleReferencesAsync(tee);

            Assert.HasCount(18, holes);
            Assert.AreEqual(1, holes.First().HoleNumber);
            Assert.AreEqual(18, holes.Last().HoleNumber);
        }
    }
}