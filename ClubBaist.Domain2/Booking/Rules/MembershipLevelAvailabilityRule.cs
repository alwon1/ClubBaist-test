namespace ClubBaist.Domain2;

public class MembershipLevelAvailabilityRule(IQueryable<MembershipLevelTeeTimeAvailability> availabilities) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) =>
        Evaluate(query, booking.BookingMember.MembershipLevel);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) =>
        query.Select(p => p.SpotsRemaining < 0 ? p :
            availabilities.Any(a =>
                    a.MembershipLevel.Id == membershipLevel.Id &&
                    a.DayOfWeek == p.Slot.Start.DayOfWeek &&
                    a.StartTime <= TimeOnly.FromDateTime(p.Slot.Start) &&
                    a.EndTime >= TimeOnly.FromDateTime(p.Slot.Start))
                ? p
                : new TeeTimeEvaluation(p.Slot, -1, $"Not available to {membershipLevel.Name} members at this time"));

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) =>
        Evaluate(query, member.MembershipLevel);
}
