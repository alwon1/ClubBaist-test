namespace ClubBaist.Domain2;

/// <summary>
/// Rejects any slot whose SeasonId does not correspond to a season whose date range covers the slot's start time.
/// This acts as a booking-window guard — slots outside any active season cannot be booked.
/// </summary>
public class SeasonValidationRule(IQueryable<Season> seasons) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) =>
        Filter(query);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) =>
        Filter(query);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) =>
        Filter(query);

    private IQueryable<TeeTimeEvaluation> Filter(IQueryable<TeeTimeEvaluation> query) =>
        query.Select(p => p.SpotsRemaining < 0 ? p :
            seasons.Any(s => s.Id == p.Slot.SeasonId
                          && s.StartDate <= DateOnly.FromDateTime(p.Slot.Start)
                          && s.EndDate >= DateOnly.FromDateTime(p.Slot.Start))
                ? p
                : new TeeTimeEvaluation(p.Slot, -4, "Tee time is outside of an active season"));
}
