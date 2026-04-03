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
        var currentLocalTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        return query.Select(p => p.SpotsRemaining < 0 ? p :
            p.Slot.Start < currentLocalTime
                ? new TeeTimeEvaluation(p.Slot, -5, "Cannot book a tee time in the past")
                : p);
    }
}
