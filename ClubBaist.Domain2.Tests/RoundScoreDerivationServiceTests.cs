using ClubBaist.Services2.Scoring;

namespace ClubBaist.Domain2.Tests;

[TestClass]
public class RoundScoreDerivationServiceTests
{
    private readonly RoundScoreDerivationService service = new();

    [TestMethod]
    public void Compute_RawOnly_ReturnsRawAndAdjustedAsEqual()
    {
        var scores = Enumerable.Repeat<uint?>(5, 18).ToList();

        var result = service.Compute(scores);

        Assert.AreEqual(90, result.RawTotal);
        Assert.AreEqual(90, result.AdjustedTotal);
        Assert.IsFalse(result.UsedWhsHoleRules);
    }

    [TestMethod]
    public void ComputeWithWhsHoleAdjustments_Established_CompletedHoleIsCappedByNdb()
    {
        var holes = new List<HoleScoreInput>
        {
            new(1, 4, 1, 10, HolePlayState.Completed)
        };

        var result = service.ComputeWithWhsHoleAdjustments(holes, courseHandicap: 18, hasEstablishedHandicap: true);

        Assert.IsTrue(result.UsedWhsHoleRules);
        Assert.AreEqual(10, result.RawTotal);
        Assert.AreEqual(7, result.AdjustedTotal);
        Assert.AreEqual("NDB cap", result.HoleBreakdown![0].RuleApplied);
    }

    [TestMethod]
    public void ComputeWithWhsHoleAdjustments_NotEstablished_UsesParPlusFiveCap()
    {
        var holes = new List<HoleScoreInput>
        {
            new(1, 4, 1, 12, HolePlayState.Completed)
        };

        var result = service.ComputeWithWhsHoleAdjustments(holes, courseHandicap: 0, hasEstablishedHandicap: false);

        Assert.AreEqual(12, result.RawTotal);
        Assert.AreEqual(9, result.AdjustedTotal);
        Assert.AreEqual("Initial handicap cap (Par + 5)", result.HoleBreakdown![0].RuleApplied);
    }

    [TestMethod]
    public void ComputeWithWhsHoleAdjustments_NotPlayed_UsesNetPar()
    {
        var holes = new List<HoleScoreInput>
        {
            new(1, 4, 1, null, HolePlayState.NotPlayed)
        };

        var result = service.ComputeWithWhsHoleAdjustments(holes, courseHandicap: 18, hasEstablishedHandicap: true);

        Assert.AreEqual(0, result.RawTotal);
        Assert.AreEqual(5, result.AdjustedTotal);
        Assert.AreEqual("Net Par", result.HoleBreakdown![0].RuleApplied);
    }

    [TestMethod]
    public void ComputeWithWhsHoleAdjustments_StartedNotHoledOut_UsesMostLikelyCappedByNdb()
    {
        var holes = new List<HoleScoreInput>
        {
            new(1, 4, 1, null, HolePlayState.StartedNotHoledOut, MostLikelyScore: 9)
        };

        var result = service.ComputeWithWhsHoleAdjustments(holes, courseHandicap: 18, hasEstablishedHandicap: true);

        Assert.AreEqual(0, result.RawTotal);
        Assert.AreEqual(7, result.AdjustedTotal);
        Assert.AreEqual("Most Likely capped by NDB", result.HoleBreakdown![0].RuleApplied);
    }

    [TestMethod]
    public void ComputeWithWhsHoleAdjustments_PlusHandicap_GivesBackStrokesFromHardestToEasiestReverse()
    {
        var holes = new List<HoleScoreInput>
        {
            new(1, 4, 18, 7, HolePlayState.Completed),
            new(2, 4, 1, 7, HolePlayState.Completed)
        };

        var result = service.ComputeWithWhsHoleAdjustments(holes, courseHandicap: -1, hasEstablishedHandicap: true);

        Assert.AreEqual(14, result.RawTotal);
        Assert.AreEqual(11, result.AdjustedTotal);
        Assert.AreEqual(1, result.HoleBreakdown![0].StrokesGivenBack);
        Assert.AreEqual(0, result.HoleBreakdown[1].StrokesGivenBack);
    }
}