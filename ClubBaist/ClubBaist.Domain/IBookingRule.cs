namespace ClubBaist.Domain;

/// <summary>
/// Pre-fetched data supplied by the booking service once per call.
/// <see cref="MemberCategory"/> is null for availability queries where no
/// specific member is being evaluated.
/// <para>
/// Option A – context prefetch: populate <see cref="BlockedEventsByDate"/> once for
/// the entire date range before the slot loop; <see cref="ClubEventBlockingRule{TKey}"/>
/// will use that data instead of hitting the database.
/// </para>
/// <para>
/// Option B – the rule's own per-date lazy cache: if <see cref="BlockedEventsByDate"/>
/// is null the rule loads and caches events per date within the scoped instance,
/// so each date causes at most one DB query regardless of how many slot times it has.
/// </para>
/// </summary>
public sealed record BookingEvaluationContext(
    MembershipCategory? MemberCategory,
    Guid? ExcludeReservationId = null,
    int? PrecomputedOccupancy = null,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<ClubEvent>>? BlockedEventsByDate = null);

public interface IBookingRule
{
    Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default);
}
