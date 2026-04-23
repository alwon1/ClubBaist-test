using ClubBaist.Domain;
using ClubBaist.Domain.Entities;
using ClubBaist.Domain.Entities.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services.Scoring;

public sealed class HandicapCalculationService(AppDbContext db, ILogger<HandicapCalculationService> logger)
{
    public async Task<HandicapResult> GetCurrentHandicapAsync(int memberId, CancellationToken ct)
    {
        var member = await db.MemberShips
            .AsNoTracking()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);

        if (member is null)
        {
            logger.LogWarning("Handicap calculation rejected: member {MemberId} was not found", memberId);
            return CreateUnavailableResult("Member not found");
        }

        if (member.User.Gender is null)
        {
            logger.LogWarning("Handicap calculation rejected: member {MemberId} has no gender set", memberId);
            return CreateUnavailableResult("Member profile is incomplete");
        }

        var memberGender = member.User.Gender.Value;
        var rounds = await db.GolfRounds
            .Where(r => r.MembershipId == memberId)
            .OrderByDescending(r => r.SubmittedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        if (rounds.Count == 0)
        {
            logger.LogInformation("Handicap calculation: member {MemberId} has no rounds", memberId);
            return CreateUnavailableResult("No submitted rounds");
        }

        var courseRatings = await db.CourseRatings
            .AsNoTracking()
            .ToDictionaryAsync(r => (r.TeeColor, r.Gender), ct);

        var validDifferentials = new List<(decimal Differential, DateTime SubmittedAt)>();

        foreach (var round in rounds)
        {
            if (!IsComplete18HoleRound(round.Scores))
            {
                logger.LogWarning(
                    "Skipping round {RoundId} for member {MemberId}: scorecard does not contain 18 completed hole scores",
                    round.Id,
                    memberId);
                continue;
            }

            if (!courseRatings.TryGetValue((round.SelectedTeeColor, memberGender), out var courseRating))
            {
                logger.LogWarning(
                    "Skipping round {RoundId} for member {MemberId}: course rating missing for tee {TeeColor} and gender {Gender}",
                    round.Id,
                    memberId,
                    round.SelectedTeeColor,
                    memberGender);
                return CreateUnavailableResult("Course rating is missing for one or more rounds");
            }

            var adjustedGross = CalculateAdjustedGrossScore(round.Scores);
            var pccAdjustment = ResolvePlayingConditionsAdjustment(round);
            var differential = CalculateHandicapDifferential(adjustedGross, courseRating.Rating, courseRating.SlopeRating, pccAdjustment);

            validDifferentials.Add((differential, round.SubmittedAt));

            if (validDifferentials.Count == 20)
            {
                break;
            }

            logger.LogInformation(
                "Round {RoundId} included for member {MemberId}. AdjustedGross={AdjustedGross}, CourseRating={CourseRating}, Slope={Slope}, PCC={Pcc}, Differential={Differential}",
                round.Id,
                memberId,
                adjustedGross,
                courseRating.Rating,
                courseRating.SlopeRating,
                pccAdjustment,
                differential);
        }

        if (validDifferentials.Count == 0)
        {
            logger.LogWarning("Handicap calculation unavailable: all rounds were invalid for member {MemberId}", memberId);
            return CreateUnavailableResult("No valid rounds available for handicap calculation");
        }

        var selectionRule = GetDifferentialSelectionRule(validDifferentials.Count);
        var selectedDifferentials = validDifferentials
            .OrderBy(d => d.Differential)
            .Take(selectionRule.DifferentialCount)
            .ToList();

        var averageDifferential = selectedDifferentials.Average(d => d.Differential);
        var currentHandicap = RoundHandicapIndex(averageDifferential + selectionRule.Adjustment);
        var lastUpdated = selectedDifferentials.Max(d => d.SubmittedAt);

        var result = new HandicapResult
        {
            CurrentHandicap = currentHandicap,
            RoundCount = validDifferentials.Count,
            DifferentialCount = selectionRule.DifferentialCount,
            LastUpdated = lastUpdated,
            IsProvisional = validDifferentials.Count < 20,
            IsAvailable = true
        };

        logger.LogInformation(
            "Handicap calculation completed for member {MemberId}. RoundCount={RoundCount}, DifferentialCount={DifferentialCount}, Handicap={Handicap}, IsProvisional={IsProvisional}",
            memberId,
            result.RoundCount,
            result.DifferentialCount,
            result.CurrentHandicap,
            result.IsProvisional);

        return result;
    }

    private static HandicapResult CreateUnavailableResult(string reason) =>
        new()
        {
            CurrentHandicap = null,
            RoundCount = 0,
            DifferentialCount = 0,
            LastUpdated = null,
            IsProvisional = true,
            IsAvailable = false,
            ErrorMessage = reason
        };

    private static bool IsComplete18HoleRound(IReadOnlyList<uint?> scores) =>
        scores.Count == 18 && scores.All(s => s.HasValue);

    private static decimal CalculateAdjustedGrossScore(IReadOnlyList<uint?> scores)
    {
        // Placeholder for full per-hole cap logic when detailed hole handicap context is introduced.
        // For now, use the submitted gross score as adjusted gross.
        return scores.Sum(score => Convert.ToDecimal(score!.Value));
    }

    private static decimal ResolvePlayingConditionsAdjustment(GolfRound round)
    {
        // Contract point for future PCC integration. Returning zero keeps caller contract unchanged.
        _ = round;
        return 0m;
    }

    private static decimal CalculateHandicapDifferential(
        decimal adjustedGross,
        decimal courseRating,
        int slopeRating,
        decimal pccAdjustment)
    {
        var raw = (adjustedGross - courseRating - pccAdjustment) * 113m / slopeRating;
        return RoundDifferential(raw);
    }

    private static (int DifferentialCount, decimal Adjustment) GetDifferentialSelectionRule(int roundCount)
    {
        if (roundCount <= 0)
        {
            return (0, 0m);
        }

        if (roundCount <= 2)
        {
            return (roundCount, 0m);
        }

        if (roundCount == 3)
        {
            return (1, -2.0m);
        }

        if (roundCount == 4)
        {
            return (1, -1.0m);
        }

        if (roundCount == 5)
        {
            return (1, 0m);
        }

        if (roundCount <= 6)
        {
            return (2, -1.0m);
        }

        if (roundCount <= 8)
        {
            return (2, 0m);
        }

        if (roundCount <= 10)
        {
            return (3, 0m);
        }

        if (roundCount <= 12)
        {
            return (4, 0m);
        }

        if (roundCount <= 14)
        {
            return (5, 0m);
        }

        if (roundCount <= 16)
        {
            return (6, 0m);
        }

        if (roundCount <= 19)
        {
            return (7, 0m);
        }

        return (8, 0m);
    }

    private static decimal RoundHandicapIndex(decimal value) =>
        decimal.Round(value, 1, MidpointRounding.AwayFromZero);

    private static decimal RoundDifferential(decimal value)
    {
        if (value >= 0m)
        {
            return decimal.Round(value, 1, MidpointRounding.AwayFromZero);
        }

        var absolute = decimal.Abs(value);
        var scaled = absolute * 10m;
        var whole = decimal.Truncate(scaled);
        var fraction = scaled - whole;

        if (fraction == 0.5m)
        {
            return -(whole / 10m);
        }

        return -decimal.Round(absolute, 1, MidpointRounding.AwayFromZero);
    }
}