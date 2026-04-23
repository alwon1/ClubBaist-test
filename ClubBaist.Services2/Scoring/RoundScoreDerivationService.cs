namespace ClubBaist.Services2.Scoring;

public sealed class RoundScoreDerivationService
{
    public RoundScoreView Compute(IReadOnlyList<uint?> scores)
    {
        if (scores is null || scores.Count == 0)
        {
            return new RoundScoreView(0, 0, false, "No scores provided");
        }

        var rawTotal = scores.Where(s => s.HasValue).Sum(s => Convert.ToInt32(s!.Value));
        var isComplete = scores.Count == 18 && scores.All(s => s.HasValue);

        // Phase 1 behavior: derive adjusted value from raw input without persisting it.
        // Hole-level WHS adjustments (NDB/net par/most likely) are added in the next phase.
        var adjustedTotal = rawTotal;
        var basis = isComplete
            ? "Derived from raw totals (hole-level WHS adjustments pending)"
            : "Partial raw totals (incomplete scorecard)";

        return new RoundScoreView(rawTotal, adjustedTotal, isComplete, basis);
    }

    public RoundScoreView ComputeWithWhsHoleAdjustments(
        IReadOnlyList<HoleScoreInput> holes,
        int courseHandicap,
        bool hasEstablishedHandicap)
    {
        if (holes is null || holes.Count == 0)
        {
            return new RoundScoreView(0, 0, false, "No hole inputs provided", HoleBreakdown: []);
        }

        var derivations = new List<HoleScoreDerivation>(holes.Count);

        foreach (var hole in holes.OrderBy(h => h.HoleNumber))
        {
            var strokesReceived = hasEstablishedHandicap
                ? CalculateStrokesReceived(courseHandicap, hole.StrokeIndex)
                : 0;
            var strokesGivenBack = hasEstablishedHandicap
                ? CalculateStrokesGivenBack(courseHandicap, hole.StrokeIndex)
                : 0;

            var ndb = hole.Par + 2 + strokesReceived - strokesGivenBack;
            var netPar = hole.Par + strokesReceived - strokesGivenBack;
            var rawScore = hole.RawScore ?? 0;

            int adjustedScore;
            string ruleApplied;

            if (!hasEstablishedHandicap)
            {
                var candidate = ResolveActualOrMostLikely(hole);
                adjustedScore = Math.Min(candidate, hole.Par + 5);
                ruleApplied = "Initial handicap cap (Par + 5)";
            }
            else
            {
                switch (hole.State)
                {
                    case HolePlayState.NotPlayed:
                        adjustedScore = netPar;
                        ruleApplied = "Net Par";
                        break;
                    case HolePlayState.StartedNotHoledOut:
                        var mostLikely = ResolveMostLikely(hole);
                        adjustedScore = Math.Min(mostLikely, ndb);
                        ruleApplied = "Most Likely capped by NDB";
                        break;
                    default:
                        adjustedScore = Math.Min(rawScore, ndb);
                        ruleApplied = "NDB cap";
                        break;
                }
            }

            derivations.Add(new HoleScoreDerivation(
                hole.HoleNumber,
                rawScore,
                adjustedScore,
                ruleApplied,
                hole.Par,
                strokesReceived,
                strokesGivenBack,
                ndb,
                netPar));
        }

        var rawTotal = derivations.Sum(d => d.RawScore);
        var adjustedTotal = derivations.Sum(d => d.AdjustedScore);
        var isComplete = holes.Count == 18;

        return new RoundScoreView(
            rawTotal,
            adjustedTotal,
            isComplete,
            "Derived using WHS hole adjustment rules",
            HoleBreakdown: derivations,
            UsedWhsHoleRules: true);
    }

    private static int ResolveActualOrMostLikely(HoleScoreInput hole)
    {
        if (hole.State == HolePlayState.StartedNotHoledOut)
        {
            return ResolveMostLikely(hole);
        }

        if (hole.State == HolePlayState.NotPlayed)
        {
            return hole.Par;
        }

        return hole.RawScore ?? 0;
    }

    private static int ResolveMostLikely(HoleScoreInput hole) =>
        hole.MostLikelyScore ?? (hole.RawScore ?? 0);

    private static int CalculateStrokesReceived(int courseHandicap, int strokeIndex)
    {
        if (courseHandicap <= 0)
        {
            return 0;
        }

        var cycles = courseHandicap / 18;
        var remainder = courseHandicap % 18;
        return cycles + (strokeIndex <= remainder ? 1 : 0);
    }

    private static int CalculateStrokesGivenBack(int courseHandicap, int strokeIndex)
    {
        if (courseHandicap >= 0)
        {
            return 0;
        }

        var absolute = Math.Abs(courseHandicap);
        var cycles = absolute / 18;
        var remainder = absolute % 18;
        var reverseStrokeIndex = 19 - strokeIndex;
        return cycles + (reverseStrokeIndex <= remainder ? 1 : 0);
    }
}

public sealed record RoundScoreView(
    int RawTotal,
    int AdjustedTotal,
    bool IsComplete,
    string Basis,
    IReadOnlyList<HoleScoreDerivation>? HoleBreakdown = null,
    bool UsedWhsHoleRules = false);

public sealed record HoleScoreInput(
    int HoleNumber,
    int Par,
    int StrokeIndex,
    int? RawScore,
    HolePlayState State,
    int? MostLikelyScore = null);

public enum HolePlayState
{
    Completed = 0,
    NotPlayed = 1,
    StartedNotHoledOut = 2
}

public sealed record HoleScoreDerivation(
    int HoleNumber,
    int RawScore,
    int AdjustedScore,
    string RuleApplied,
    int Par,
    int StrokesReceived,
    int StrokesGivenBack,
    int NetDoubleBogeyCap,
    int NetPar);