namespace ClubBaist.Domain2;

/// <summary>
/// Explicitly denies tee time bookings and availability queries for Social (Copper) members.
/// Social members have no golf privileges per the membership tier structure in BusinessProblem.md.
/// Short code: "CP".
/// </summary>
public class SocialMemberNoGolfRule : IBookingRule
{
    internal const string SocialShortCode = "CP";
    internal const string RejectionReason = "Social members do not have golf privileges.";

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) =>
        booking.BookingMember.MembershipLevel.ShortCode.Equals(SocialShortCode, StringComparison.OrdinalIgnoreCase)
            ? Deny(query)
            : query;

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) =>
        membershipLevel.ShortCode.Equals(SocialShortCode, StringComparison.OrdinalIgnoreCase)
            ? Deny(query)
            : query;

    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) =>
        member.MembershipLevel.ShortCode.Equals(SocialShortCode, StringComparison.OrdinalIgnoreCase)
            ? Deny(query)
            : query;

    private static IQueryable<TeeTimeEvaluation> Deny(IQueryable<TeeTimeEvaluation> query) =>
        query.Select(e => new TeeTimeEvaluation(
            e.Slot,
            e.SpotsRemaining < 0 ? e.SpotsRemaining : -1,
            e.SpotsRemaining < 0 ? e.RejectionReason : RejectionReason));
}
