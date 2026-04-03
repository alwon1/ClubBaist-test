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
        query
            .Select(p => new { p, eventName = specialEvents.Where(e => e.Start <= p.Slot.Start && e.End > p.Slot.Start).Select(e => e.Name).FirstOrDefault() })
            .Select(x => x.p.SpotsRemaining < 0 ? x.p :
                x.eventName == null
                    ? x.p
                    : new TeeTimeEvaluation(x.p.Slot, -3, "This time is blocked by the special event: " + x.eventName));
}
