namespace ClubBaist.Domain;

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
        var currentLocalTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        return query.Select(p => new TeeTimeEvaluation(
            p.Slot,
            p.SpotsRemaining < 0
                ? p.SpotsRemaining
                : p.Slot.Start < currentLocalTime
                    ? -5
                    : p.SpotsRemaining,
            p.SpotsRemaining < 0
                ? p.RejectionReason
                : p.Slot.Start < currentLocalTime
                    ? "Cannot book a tee time in the past"
                    : p.RejectionReason));
    }
}
