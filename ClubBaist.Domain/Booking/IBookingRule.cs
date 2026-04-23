namespace ClubBaist.Domain;

public interface IBookingRule
{
    /// <summary>Evaluate for a specific booking attempt (create or update).</summary>
    IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null) => query;

    /// <summary>Evaluate availability for a membership level (no specific member).</summary>
    IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MembershipLevel membershipLevel) => query;

    /// <summary>Evaluate availability for a specific member (e.g. highlight duplicate slots in UI).</summary>
    IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member) => query;
}

public static class BookingRuleExtensions
{
    /// <summary>Single-slot booking validation — filters to the target slot then applies all rules.</summary>
    public static IQueryable<TeeTimeEvaluation> Evaluate(
        this IQueryable<TeeTimeSlot> slots, IEnumerable<IBookingRule> rules, TeeTimeBooking booking,int? excludeBookingId=null) =>
        rules.Aggregate(
            slots.Where(s => s.Start == booking.TeeTimeSlotStart)
                 .Select(s => new TeeTimeEvaluation(s, int.MaxValue, null)),
            (query, rule) => rule.Evaluate(query, booking, excludeBookingId));
    /// <summary>Range availability query for a membership level — runs across all slots in the source.</summary>
    public static IQueryable<TeeTimeEvaluation> Evaluate(
        this IQueryable<TeeTimeSlot> slots, IEnumerable<IBookingRule> rules, MembershipLevel membershipLevel) =>
        rules.Aggregate(
            slots.Select(s => new TeeTimeEvaluation(s, int.MaxValue, null)),
            (query, rule) => rule.Evaluate(query, membershipLevel));

    /// <summary>Range availability query for a specific member — includes duplicate-booking detection.</summary>
    public static IQueryable<TeeTimeEvaluation> Evaluate(
        this IQueryable<TeeTimeSlot> slots, IEnumerable<IBookingRule> rules, MemberShipInfo member) =>
        rules.Aggregate(
            slots.Select(s => new TeeTimeEvaluation(s, int.MaxValue, null)),
            (query, rule) => rule.Evaluate(query, member));
}
