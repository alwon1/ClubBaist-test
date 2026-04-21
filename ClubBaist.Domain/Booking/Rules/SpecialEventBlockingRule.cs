namespace ClubBaist.Domain;

public class SpecialEventBlockingRule(IQueryable<SpecialEvent> specialEvents) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) =>
        Filter(query);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) =>
        Filter(query);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) =>
        Filter(query);

    private IQueryable<TeeTimeEvaluation> Filter(IQueryable<TeeTimeEvaluation> query) =>
        query
            .Select(p => new
            {
                p.Slot,
                p.SpotsRemaining,
                p.RejectionReason,
                EventName = specialEvents
                    .Where(e => e.Start <= p.Slot.Start && e.End > p.Slot.Start)
                    .Select(e => e.Name)
                    .FirstOrDefault()
            })
            .Select(x => new TeeTimeEvaluation(
                x.Slot,
                x.SpotsRemaining < 0
                    ? x.SpotsRemaining
                    : x.EventName == null
                        ? x.SpotsRemaining
                        : -3,
                x.SpotsRemaining < 0
                    ? x.RejectionReason
                    : x.EventName == null
                        ? x.RejectionReason
                        : "This time is blocked by the special event: " + x.EventName));
}
