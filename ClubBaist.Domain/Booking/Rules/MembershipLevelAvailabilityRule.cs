namespace ClubBaist.Domain;

public class MembershipLevelAvailabilityRule(IQueryable<MembershipLevelTeeTimeAvailability> availabilities) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) =>
        Evaluate(query, booking.BookingMember.MembershipLevel);

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) =>
        query
            .Select(p => new
            {
                p.Slot,
                p.SpotsRemaining,
                p.RejectionReason,
                IsAvailable = availabilities.Any(a =>
                    a.MembershipLevel.Id == membershipLevel.Id &&
                    a.DayOfWeek == p.Slot.Start.DayOfWeek &&
                    a.StartTime <= TimeOnly.FromDateTime(p.Slot.Start) &&
                    a.EndTime >= TimeOnly.FromDateTime(p.Slot.Start))
            })
            .Select(x => new TeeTimeEvaluation(
                x.Slot,
                x.SpotsRemaining < 0
                    ? x.SpotsRemaining
                    : x.IsAvailable
                        ? x.SpotsRemaining
                        : -1,
                x.SpotsRemaining < 0
                    ? x.RejectionReason
                    : x.IsAvailable
                        ? x.RejectionReason
                        : $"Not available to {membershipLevel.Name} members at this time"));

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) =>
        Evaluate(query, member.MembershipLevel);
}
