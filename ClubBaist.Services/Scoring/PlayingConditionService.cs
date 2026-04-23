using ClubBaist.Domain;
using ClubBaist.Domain.Entities.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services.Scoring;

public sealed class PlayingConditionService(AppDbContext db, ILogger<PlayingConditionService> logger)
{
    public async Task<PlayingConditionAdjustment?> GetByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        return await db.PlayingConditionAdjustments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.EffectiveDate == date, ct);
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(
        DateOnly date,
        decimal adjustment,
        string enteredByUserId,
        string? notes,
        CancellationToken ct = default)
    {
        if (adjustment < -1.0m || adjustment > 3.0m)
        {
            return (false, "PCC must be between -1.0 and 3.0.");
        }

        var normalized = decimal.Round(adjustment, 1, MidpointRounding.AwayFromZero);
        var existing = await db.PlayingConditionAdjustments
            .FirstOrDefaultAsync(p => p.EffectiveDate == date, ct);

        if (existing is null)
        {
            db.PlayingConditionAdjustments.Add(new PlayingConditionAdjustment
            {
                EffectiveDate = date,
                Adjustment = normalized,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                EnteredByUserId = enteredByUserId,
                UpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
            });
        }
        else
        {
            existing.Adjustment = normalized;
            existing.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            existing.EnteredByUserId = enteredByUserId;
            existing.UpdatedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "PCC upserted for {Date}. Adjustment={Adjustment} by {UserId}",
            date,
            normalized,
            enteredByUserId);

        return (true, null);
    }
}