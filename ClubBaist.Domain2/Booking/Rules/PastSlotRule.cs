namespace ClubBaist.Domain2;

/// <summary>
/// Rejects any slot whose start time is in the past relative to the current local
/// wall-clock time used to generate slot start values.
/// Applies only to booking attempts; availability queries are not filtered so the UI
/// can still display historical slots as reference.
/// </summary>
public class PastSlotRule : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null)
    {
        var nowLocal = DateTime.Now;
        var nowUtc = DateTime.UtcNow;
        var nowUnspecified = DateTime.SpecifyKind(nowLocal, DateTimeKind.Unspecified);

        return query.Select(p => p.SpotsRemaining < 0 ? p :
            IsPastSlot(p.Slot.Start, nowLocal, nowUtc, nowUnspecified)
                ? new TeeTimeEvaluation(p.Slot, -5, "Cannot book a tee time in the past")
                : p);
    }

    private static bool IsPastSlot(DateTime slotStart, DateTime nowLocal, DateTime nowUtc, DateTime nowUnspecified) =>
        slotStart.Kind switch
        {
            DateTimeKind.Utc => slotStart < nowUtc,
            DateTimeKind.Local => slotStart < nowLocal,
            _ => slotStart < nowUnspecified
        };
}
