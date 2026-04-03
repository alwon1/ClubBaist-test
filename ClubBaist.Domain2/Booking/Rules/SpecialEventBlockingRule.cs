namespace ClubBaist.Domain2;

public class SpecialEventBlockingRule(IQueryable<SpecialEvent> specialEvents) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) =>
        Filter(query);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) =>
        Filter(query);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) =>
        Filter(query);

    private IQueryable<TeeTimeEvaluation> Filter(IQueryable<TeeTimeEvaluation> query) =>
        query.Select(p => p.SpotsRemaining < 0 ? p :
            specialEvents.Any(e => e.Start <= p.Slot.Start && e.End > p.Slot.Start)
                ? new TeeTimeEvaluation(p.Slot, -3,
                    "This time is blocked by the special event: " + specialEvents
                        .Where(e => e.Start <= p.Slot.Start && e.End > p.Slot.Start)
                        .Select(e => e.Name)
                        .FirstOrDefault())
                : p);
}
