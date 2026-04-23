namespace ClubBaist.Services.Scoring;

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
}

public sealed record RoundScoreView(
    int RawTotal,
    int AdjustedTotal,
    bool IsComplete,
    string Basis);
