namespace ClubBaist.Domain2;

/// <summary>
/// Checks that adding the incoming booking does not exceed <paramref name="maxParticipants"/> per slot.
/// Injects <see cref="IQueryable{TeeTimeBooking}"/> so the capacity check runs as a DB-side correlated
/// sub-query, which correctly handles the ExcludeBookingId case during updates.
/// The MembershipLevel overload uses the pre-aggregated BookedSpots from the view for read-only queries.
/// </summary>
public class MaxParticipantsRule(IQueryable<TeeTimeBooking> bookings, int maxParticipants = 4) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null)
    {
        // Use AdditionalParticipants.Count + 1 so this works for unsaved bookings
        // (the DB-computed ParticipantCount column is 0 before the row is saved).
        var incoming = 1 + booking.AdditionalParticipants.Count;
        return query
            .Select(p => new { p, existing = bookings.Where(b => b.TeeTimeSlotStart == p.Slot.Start && (excludeBookingId == null || b.Id != excludeBookingId)).Sum(b => 1 + b.AdditionalParticipants.Count) })
            .Select(x => x.p.SpotsRemaining < 0 ? x.p :
                x.existing + incoming > maxParticipants
                    ? new TeeTimeEvaluation(x.p.Slot, 0, "Tee time is full")
                    : new TeeTimeEvaluation(x.p.Slot, maxParticipants - x.existing, x.p.RejectionReason));
    }

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) =>
        query
            .Select(p => new { p, existing = bookings.Where(b => b.TeeTimeSlotStart == p.Slot.Start).Sum(b => 1 + b.AdditionalParticipants.Count) })
            .Select(x => x.p.SpotsRemaining < 0 ? x.p :
                x.existing >= maxParticipants
                    ? new TeeTimeEvaluation(x.p.Slot, 0, "Tee time is full")
                    : new TeeTimeEvaluation(x.p.Slot, maxParticipants - x.existing, x.p.RejectionReason));

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) =>
        Evaluate(query, member.MembershipLevel);
}
