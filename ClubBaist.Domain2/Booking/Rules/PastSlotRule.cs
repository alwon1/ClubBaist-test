namespace ClubBaist.Domain2;

/// <summary>
/// Rejects any slot whose start time is in the past relative to the current UTC time.
/// Applies only to booking attempts; availability queries are not filtered so the UI
/// can still display historical slots as reference.
/// </summary>
public class PastSlotRule : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) =>
        query.Select(p => p.SpotsRemaining < 0 ? p :
            p.Slot.Start < DateTime.UtcNow
                ? new TeeTimeEvaluation(p.Slot, -5, "Cannot book a tee time in the past")
                : p);
}
