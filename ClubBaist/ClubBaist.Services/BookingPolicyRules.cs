using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public interface IBookingPolicyRule<TKey> where TKey : IEquatable<TKey>
{
    Task<BookingPolicyRuleResult?> EvaluateAsync(
        BookingRequest bookingRequest,
        BookingPolicy bookingPolicy,
        CancellationToken cancellationToken = default);
}

public interface IAvailabilityPolicyRule<TKey> where TKey : IEquatable<TKey>
{
    Task<BookingPolicyRuleResult?> EvaluateAsync(
        BookingRequest bookingRequest,
        BookingPolicy bookingPolicy,
        CancellationToken cancellationToken = default);
}

public sealed record BookingPolicyRuleResult(ReservationDecisionCodes DecisionCode, string Reason);

public sealed class PlayerCountPolicyRule<TKey> : IBookingPolicyRule<TKey>, IAvailabilityPolicyRule<TKey>
    where TKey : IEquatable<TKey>
{
    public Task<BookingPolicyRuleResult?> EvaluateAsync(
        BookingRequest bookingRequest,
        BookingPolicy bookingPolicy,
        CancellationToken cancellationToken = default)
    {
        if (bookingRequest.PlayerCount < bookingPolicy.MinPlayers || bookingRequest.PlayerCount > bookingPolicy.MaxPlayers)
        {
            return Task.FromResult<BookingPolicyRuleResult?>(new BookingPolicyRuleResult(
                ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE,
                $"Player count must be between {bookingPolicy.MinPlayers} and {bookingPolicy.MaxPlayers}."));
        }

        return Task.FromResult<BookingPolicyRuleResult?>(null);
    }
}

public sealed class MemberEligibilityPolicyRule<TKey> : IBookingPolicyRule<TKey>, IAvailabilityPolicyRule<TKey>
    where TKey : IEquatable<TKey>
{
    private static readonly TimeOnly EarliestPriorityTeeTime = new(6, 0);
    private static readonly TimeOnly EarliestStandardTeeTime = new(7, 0);
    private static readonly TimeOnly EarliestRestrictedTeeTime = new(8, 0);

    private readonly IApplicationDbContext<TKey> _dbContext;

    public MemberEligibilityPolicyRule(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<BookingPolicyRuleResult?> EvaluateAsync(
        BookingRequest bookingRequest,
        BookingPolicy bookingPolicy,
        CancellationToken cancellationToken = default)
    {
        var bookingMember = await _dbContext.MemberAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(member => member.MemberAccountId == bookingRequest.MemberId, cancellationToken);

        if (bookingMember is null || !bookingMember.IsActive)
        {
            return new BookingPolicyRuleResult(
                ReservationDecisionCodes.BOOKING_FORBIDDEN,
                "Booking member was not found or is inactive.");
        }

        if (bookingRequest.TeeTime < GetEarliestAllowedTeeTime(bookingMember.MembershipCategory))
        {
            return new BookingPolicyRuleResult(
                ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION,
                $"Requested tee time {bookingRequest.TeeTime:HH\\:mm} is outside the booking member time window for {bookingMember.MembershipCategory} membership.");
        }

        return null;
    }

    private static TimeOnly GetEarliestAllowedTeeTime(MembershipCategory membershipCategory) => membershipCategory switch
    {
        MembershipCategory.Shareholder or MembershipCategory.ShareholderSpouse => EarliestPriorityTeeTime,
        MembershipCategory.Associate or MembershipCategory.AssociateSpouse => EarliestStandardTeeTime,
        _ => EarliestRestrictedTeeTime
    };
}
