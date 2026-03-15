namespace ClubBaist.Domain;

public sealed record BookingPolicyDecision(
    bool Allowed,
    ReservationDecisionCodes DecisionCode,
    IReadOnlyList<string> Reasons,
    BookingPolicy? PolicyApplied = null);

