namespace ClubBaist.Domain;

/// <summary>
/// Pre-fetched data supplied by the booking service once per call so rules
/// don't need their own database access for shared lookups.
/// <see cref="MemberCategory"/> is null for availability queries where no
/// specific member is being evaluated.
/// </summary>
public sealed record BookingEvaluationContext(
    Season? ActiveSeason,
    MembershipCategory? MemberCategory);

public interface IBookingRule
{
    Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default);
}
