using ClubBaist.Domain;

namespace ClubBaist.Services;

public sealed class BookingPolicyService
{
    private static readonly IReadOnlyDictionary<ReservationDecisionCodes, int> DecisionCodePrecedence =
        new Dictionary<ReservationDecisionCodes, int>
        {
            [ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE] = 0,
            [ReservationDecisionCodes.BOOKING_FORBIDDEN] = 1,
            [ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION] = 2,
            [ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE] = 3,
        };

    public BookingPolicyDecision BuildDecision(IEnumerable<BookingRuleFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        var failedRules = failures.ToList();
        if (failedRules.Count == 0)
        {
            return BookingPolicyDecision.CreateAllowed();
        }

        var reasons = failedRules
            .Select(item => item.Reason)
            .ToList();

        var decisionCode = failedRules
            .Select(item => item.DecisionCode)
            .OrderBy(GetDecisionCodeRank)
            .First();

        return BookingPolicyDecision.Denied(decisionCode, reasons);
    }

    private static int GetDecisionCodeRank(ReservationDecisionCodes code) =>
        DecisionCodePrecedence.TryGetValue(code, out var rank)
            ? rank
            : int.MaxValue;
}

public sealed record BookingRuleFailure(ReservationDecisionCodes DecisionCode, string Reason)
{
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("A failure reason is required.", nameof(Reason))
        : Reason.Trim();
}

public sealed record BookingPolicyDecision
{
    public bool Allowed { get; }
    public ReservationDecisionCodes DecisionCode { get; }
    public IReadOnlyList<string> Reasons { get; }

    private BookingPolicyDecision(
        bool allowed,
        ReservationDecisionCodes decisionCode,
        IReadOnlyList<string> reasons)
    {
        if (!allowed)
        {
            if (reasons is null || reasons.Count == 0 || reasons.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Denied booking decisions must include at least one non-empty reason.", nameof(reasons));
            }
        }

        Allowed = allowed;
        DecisionCode = decisionCode;
        Reasons = reasons;
    }

    public static BookingPolicyDecision CreateAllowed() =>
        new(true, ReservationDecisionCodes.BOOKING_ALLOWED, []);

    public static BookingPolicyDecision Denied(
        ReservationDecisionCodes decisionCode,
        IReadOnlyList<string> reasons)
    {
        if (decisionCode == ReservationDecisionCodes.BOOKING_ALLOWED)
        {
            throw new ArgumentException("Denied booking decisions cannot use the BOOKING_ALLOWED decision code.", nameof(decisionCode));
        }

        return new BookingPolicyDecision(false, decisionCode, reasons);
    }
}
    private const int MinPlayersPerBooking = 1;
    private const int MaxPlayersPerBooking = SlotOccupancy.MaxCapacity;

    public ReservationDecisionCodes EvaluateCreatePolicy(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EvaluatePolicy(request);
    }

    public ReservationDecisionCodes EvaluateUpdatePolicy(BookingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EvaluatePolicy(request);
    }

    private static ReservationDecisionCodes EvaluatePolicy(BookingRequest request)
    {
        var participantCount = request.PlayerMemberAccountIds.Count;
        if (participantCount < MinPlayersPerBooking || participantCount > MaxPlayersPerBooking)
        {
            return ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE;
        }

        if (request.PlayDate < DateOnly.FromDateTime(request.RequestedAt.Date))
        {
            return ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION;
        }

        return ReservationDecisionCodes.BOOKING_ALLOWED;
    }
}

public sealed record BookingRequest(
    Guid MemberId,
    DateOnly PlayDate,
    TimeOnly TeeTime,
    DateTimeOffset RequestedAt,
    IReadOnlyList<Guid> PlayerMemberAccountIds);
