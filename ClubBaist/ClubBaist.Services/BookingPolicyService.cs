using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class BookingPolicyService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly SeasonService<TKey> _seasonService;

    public BookingPolicyService(
        IApplicationDbContext<TKey> dbContext,
        SeasonService<TKey> seasonService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _seasonService = seasonService ?? throw new ArgumentNullException(nameof(seasonService));
    }

    public async Task<ServiceResult<BookingPolicyDecision>> EvaluateCreateBookingAsync(
        BookingRequest bookingRequest,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluateBookingIntentAsync(bookingRequest, cancellationToken);
        return ServiceResult<BookingPolicyDecision>.Success(evaluation);
    }

    public async Task<ServiceResult<BookingPolicyDecision>> EvaluateUpdateBookingAsync(
        BookingRequest bookingRequest,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<(ReservationDecisionCodes Code, string Reason)>();

        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ReservationId == bookingId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Active)
        {
            failures.Add((
                ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE,
                "Booking does not exist or is no longer active."));
        }
        else if (reservation.BookingMemberAccountId != bookingRequest.MemberId)
        {
            failures.Add((
                ReservationDecisionCodes.BOOKING_FORBIDDEN,
                "Requesting member is not permitted to maintain this booking."));
        }

        var intentEvaluation = await EvaluateBookingIntentAsync(bookingRequest, cancellationToken);
        if (!intentEvaluation.Allowed)
        {
            failures.AddRange(intentEvaluation.Reasons.Select(reason => (intentEvaluation.DecisionCode, reason)));
        }

        return ServiceResult<BookingPolicyDecision>.Success(BuildDecision(failures, intentEvaluation.PolicyApplied));
    }

    public async Task<ServiceResult<BookingPolicyDecision>> EvaluateCancelBookingAsync(
        BookingCancellation bookingCancellation,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<(ReservationDecisionCodes Code, string Reason)>();

        var reservation = await _dbContext.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ReservationId == bookingCancellation.BookingId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Active)
        {
            failures.Add((
                ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE,
                "Booking does not exist or is no longer active."));
        }
        else if (reservation.BookingMemberAccountId != bookingCancellation.MemberId)
        {
            failures.Add((
                ReservationDecisionCodes.BOOKING_FORBIDDEN,
                "Requesting member is not permitted to maintain this booking."));
        }

        return ServiceResult<BookingPolicyDecision>.Success(BuildDecision(failures));
    }

    public async Task<ServiceResult<BookingPolicy>> GetPolicyForDateAsync(
        DateOnly playDate,
        CancellationToken cancellationToken = default)
    {
        var seasonResult = await _seasonService.GetSeasonForDateAsync(playDate, cancellationToken);
        if (!seasonResult.IsSuccess)
        {
            return ServiceResult<BookingPolicy>.Conflict("SEASON_UNAVAILABLE", "Unable to resolve booking policy for date.");
        }

        if (seasonResult.Value is null)
        {
            return ServiceResult<BookingPolicy>.ValidationFailed(["Play date is outside an active season."]);
        }

        return ServiceResult<BookingPolicy>.Success(new BookingPolicy(
            seasonResult.Value.SeasonId,
            1,
            SlotOccupancy.MaxCapacity));
    }

    private async Task<BookingPolicyDecision> EvaluateBookingIntentAsync(
        BookingRequest bookingRequest,
        CancellationToken cancellationToken)
    {
        var failures = new List<(ReservationDecisionCodes Code, string Reason)>();

        var policyResult = await GetPolicyForDateAsync(bookingRequest.PlayDate, cancellationToken);
        BookingPolicy? policy = null;

        if (policyResult.IsSuccess)
        {
            policy = policyResult.Value;
        }
        else
        {
            failures.Add((
                ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION,
                "Play date is outside an active season window."));
        }

        var playerCount = bookingRequest.PlayerMemberAccountIds.Distinct().Count();
        if (playerCount is < 1 or > SlotOccupancy.MaxCapacity)
        {
            failures.Add((
                ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE,
                $"Player count must be between 1 and {SlotOccupancy.MaxCapacity}."));
        }

        return BuildDecision(failures, policy);
    }

    private static BookingPolicyDecision BuildDecision(
        IReadOnlyList<(ReservationDecisionCodes Code, string Reason)> failures,
        BookingPolicy? policyApplied = null)
    {
        if (failures.Count == 0)
        {
            return new BookingPolicyDecision(
                true,
                ReservationDecisionCodes.BOOKING_ALLOWED,
                ["Booking request satisfies all policy rules."],
                policyApplied);
        }

        return new BookingPolicyDecision(
            false,
            failures[0].Code,
            failures.Select(item => item.Reason).ToList(),
            policyApplied);
    }
}
