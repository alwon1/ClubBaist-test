using ClubBaist.Domain;

namespace ClubBaist.Services;

public class BookingPolicyService<TKey> where TKey : IEquatable<TKey>
{
    private readonly AvailabilityService<TKey> _availabilityService;
    private readonly IReadOnlyList<IBookingPolicyRule<TKey>> _bookingPolicyRules;

    public BookingPolicyService(
        AvailabilityService<TKey> availabilityService,
        IEnumerable<IBookingPolicyRule<TKey>> bookingPolicyRules)
    {
        _availabilityService = availabilityService ?? throw new ArgumentNullException(nameof(availabilityService));
        _bookingPolicyRules = bookingPolicyRules?.ToList()
            ?? throw new ArgumentNullException(nameof(bookingPolicyRules));
    }

    public async Task<ServiceResult<BookingPolicyDecision>> EvaluateCreateBookingAsync(
        BookingRequest bookingRequest,
        CancellationToken cancellationToken = default)
    {
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

        foreach (var policyRule in _bookingPolicyRules)
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

    public Task<ServiceResult<BookingPolicyDecision>> EvaluateCancelBookingAsync(
        BookingCancellation bookingCancellation,
        CancellationToken cancellationToken = default) =>
        _availabilityService.EvaluateCancelBookingAsync(bookingCancellation, cancellationToken);

    public Task<ServiceResult<BookingPolicy>> GetPolicyForDateAsync(
        DateOnly playDate,
        CancellationToken cancellationToken = default) =>
        _availabilityService.GetPolicyForDateAsync(playDate, cancellationToken);
}
