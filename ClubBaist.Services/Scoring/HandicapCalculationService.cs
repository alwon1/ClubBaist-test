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

        var differentialCount = GetDifferentialCountForRoundCount(validDifferentials.Count);
        var selectedDifferentials = validDifferentials
            .OrderBy(d => d.Differential)
            .Take(differentialCount)
            .ToList();

        var averageDifferential = selectedDifferentials.Average(d => d.Differential);
        var currentHandicap = TruncateToTenths(averageDifferential);
        var lastUpdated = selectedDifferentials.Max(d => d.SubmittedAt);

        var result = new HandicapResult
        {
            CurrentHandicap = currentHandicap,
            RoundCount = validDifferentials.Count,
            DifferentialCount = differentialCount,
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
        var raw = ((adjustedGross - courseRating) + pccAdjustment) * 113m / slopeRating;
        return decimal.Round(raw, 1, MidpointRounding.AwayFromZero);
    }

    private static int GetDifferentialCountForRoundCount(int roundCount)
    {
        if (roundCount <= 0)
        {
            return 0;
        }

        if (roundCount < 5)
        {
            return roundCount;
        }

        if (roundCount <= 6)
        {
            return 1;
        }

        if (roundCount <= 8)
        {
            return 2;
        }

        if (roundCount <= 10)
        {
            return 3;
        }

        if (roundCount <= 12)
        {
            return 4;
        }

        if (roundCount <= 14)
        {
            return 5;
        }

        if (roundCount <= 16)
        {
            return 6;
        }

        if (roundCount <= 19)
        {
            return 7;
        }

        return 8;
    }

    private static decimal TruncateToTenths(decimal value) =>
        decimal.Truncate(value * 10m) / 10m;
}