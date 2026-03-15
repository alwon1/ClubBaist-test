using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class AvailabilityService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly SeasonService<TKey> _seasonService;
    private readonly IReadOnlyList<IAvailabilityPolicyRule<TKey>> _availabilityPolicyRules;

    public AvailabilityService(
        IApplicationDbContext<TKey> dbContext,
        SeasonService<TKey> seasonService,
        IEnumerable<IAvailabilityPolicyRule<TKey>> availabilityPolicyRules)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _seasonService = seasonService ?? throw new ArgumentNullException(nameof(seasonService));
        _availabilityPolicyRules = availabilityPolicyRules?.ToList()
            ?? throw new ArgumentNullException(nameof(availabilityPolicyRules));
    }

    public Task<ServiceResult<BookingPolicyDecision>> EvaluateCreateBookingAsync(
        BookingRequest bookingRequest,
        CancellationToken cancellationToken = default) =>
        EvaluateCreateCoreAsync(bookingRequest, cancellationToken);

    public async Task<ServiceResult<IReadOnlyList<BookingPolicyDecision>>> EvaluateCreateBookingsAsync(
        IReadOnlyList<BookingRequest> bookingRequests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookingRequests);

        var decisions = new List<BookingPolicyDecision>(bookingRequests.Count);

        foreach (var bookingRequest in bookingRequests)
        {
            var evaluation = await EvaluateCreateCoreAsync(bookingRequest, cancellationToken);

            if (!evaluation.IsSuccess)
            {
                return evaluation.Status == ServiceResultStatus.Validation
                    ? ServiceResult<IReadOnlyList<BookingPolicyDecision>>.ValidationFailed(evaluation.ValidationErrors!)
                    : ServiceResult<IReadOnlyList<BookingPolicyDecision>>.Conflict(evaluation.ConflictCode!, evaluation.ConflictMessage!);
            }

            decisions.Add(evaluation.Value!);
        }

        return ServiceResult<IReadOnlyList<BookingPolicyDecision>>.Success(decisions);
    }

    public async Task<ServiceResult<BookingPolicyDecision>> EvaluateCancelBookingAsync(
        BookingCancellation bookingCancellation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookingCancellation);

        if (bookingCancellation.BookingId == Guid.Empty || bookingCancellation.MemberId == Guid.Empty)
        {
            return ServiceResult<BookingPolicyDecision>.ValidationFailed(["BookingId and MemberId are required."]);
        }

        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ReservationId == bookingCancellation.BookingId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Active)
        {
            return ServiceResult<BookingPolicyDecision>.Success(Deny(
                ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE,
                "Booking was not found or is not active."));
        }

        if (reservation.BookingMemberAccountId != bookingCancellation.MemberId)
        {
            return ServiceResult<BookingPolicyDecision>.Success(Deny(
                ReservationDecisionCodes.BOOKING_FORBIDDEN,
                "Requesting member is not allowed to cancel this booking."));
        }

        return ServiceResult<BookingPolicyDecision>.Success(new BookingPolicyDecision(
            true,
            ReservationDecisionCodes.BOOKING_ALLOWED.ToString(),
            ["Cancellation is allowed in Phase 1 with no time-based cutoff."]));
    }

    public async Task<ServiceResult<BookingPolicy>> GetPolicyForDateAsync(
        DateOnly playDate,
        CancellationToken cancellationToken = default)
    {
        var seasonResult = await _seasonService.GetSeasonForDateAsync(playDate, cancellationToken);
        if (!seasonResult.IsSuccess)
        {
            return seasonResult.Status == ServiceResultStatus.Validation
                ? ServiceResult<BookingPolicy>.ValidationFailed(seasonResult.ValidationErrors!)
                : ServiceResult<BookingPolicy>.Conflict(seasonResult.ConflictCode!, seasonResult.ConflictMessage!);
        }

        if (seasonResult.Value is null)
        {
            return ServiceResult<BookingPolicy>.Conflict(
                ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION.ToString(),
                "Requested play date is outside an active season window.");
        }

        return ServiceResult<BookingPolicy>.Success(new BookingPolicy(seasonResult.Value.SeasonId, 1, 4));
    }

    private async Task<ServiceResult<BookingPolicyDecision>> EvaluateCreateCoreAsync(
        BookingRequest bookingRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bookingRequest);

        if (bookingRequest.MemberId == Guid.Empty)
        {
            return ServiceResult<BookingPolicyDecision>.ValidationFailed(["MemberId is required."]);
        }

        var policyResult = await GetPolicyForDateAsync(bookingRequest.PlayDate, cancellationToken);
        if (!policyResult.IsSuccess)
        {
            return policyResult.Status == ServiceResultStatus.Validation
                ? ServiceResult<BookingPolicyDecision>.ValidationFailed(policyResult.ValidationErrors!)
                : ServiceResult<BookingPolicyDecision>.Conflict(policyResult.ConflictCode!, policyResult.ConflictMessage!);
        }

        var policy = policyResult.Value!;
        var failures = new List<BookingPolicyRuleResult>();

        foreach (var policyRule in _availabilityPolicyRules)
        {
            var failure = await policyRule.EvaluateAsync(bookingRequest, policy, cancellationToken);
            if (failure is not null)
            {
                failures.Add(failure);
            }
        }

        if (failures.Count > 0)
        {
            return ServiceResult<BookingPolicyDecision>.Success(new BookingPolicyDecision(
                false,
                failures[0].DecisionCode.ToString(),
                failures.Select(failure => failure.Reason).ToList(),
                policy));
        }

        return ServiceResult<BookingPolicyDecision>.Success(new BookingPolicyDecision(
            true,
            ReservationDecisionCodes.BOOKING_ALLOWED.ToString(),
            ["Booking request passed all Phase 1 policy checks."],
            policy));
    }

    private static BookingPolicyDecision Deny(ReservationDecisionCodes code, string reason, BookingPolicy? policyApplied = null) =>
        new(false, code.ToString(), [reason], policyApplied);
}

public sealed record BookingRequest(
    Guid MemberId,
    DateOnly PlayDate,
    TimeOnly TeeTime,
    DateTimeOffset RequestedAt,
    int PlayerCount);

public sealed record BookingCancellation(
    Guid BookingId,
    Guid MemberId,
    DateTimeOffset RequestedAt);

public sealed record BookingPolicyDecision(
    bool Allowed,
    string DecisionCode,
    IReadOnlyList<string> Reasons,
    BookingPolicy? PolicyApplied = null);

public sealed record BookingPolicy(
    Guid SeasonId,
    int MinPlayers,
    int MaxPlayers);
