using ClubBaist.Domain;
using ClubBaist.Services;

namespace ClubBaist.Tests;

[TestClass]
public sealed class BookingPolicyServiceTests
{
    [TestMethod]
    public void BuildDecision_NoFailures_ReturnsAllowedDecision()
    {
        var service = new BookingPolicyService();

        var result = service.BuildDecision([]);

        Assert.IsTrue(result.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_ALLOWED, result.DecisionCode);
        Assert.IsEmpty(result.Reasons);
    }

    [TestMethod]
    public void BuildDecision_MultipleFailures_SelectsHighestPrecedenceDecisionCode()
    {
        var service = new BookingPolicyService();
        var failures = new[]
        {
            new BookingRuleFailure(ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE, "Player count must be between 1 and 4."),
            new BookingRuleFailure(ReservationDecisionCodes.BOOKING_FORBIDDEN, "Member is not allowed to maintain this booking."),
            new BookingRuleFailure(ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION, "Requested play date is outside the booking window."),
        };

        var result = service.BuildDecision(failures);

        Assert.IsFalse(result.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_FORBIDDEN, result.DecisionCode);
        CollectionAssert.AreEquivalent(failures.Select(item => item.Reason).ToList(), result.Reasons.ToList());
    }

    [TestMethod]
    public void BuildDecision_MultipleFailures_IncludingNotFound_SelectsNotFoundFirst()
    {
        var service = new BookingPolicyService();
        var failures = new[]
        {
            new BookingRuleFailure(ReservationDecisionCodes.BOOKING_FORBIDDEN, "Member is not allowed to maintain this booking."),
            new BookingRuleFailure(ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE, "Booking was not found or is not active."),
            new BookingRuleFailure(ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE, "Player count must be between 1 and 4."),
        };

        var result = service.BuildDecision(failures);

        Assert.IsFalse(result.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE, result.DecisionCode);
        Assert.HasCount(3, result.Reasons);
    }

    [TestMethod]
    public void DeniedDecision_WithoutReasons_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            BookingPolicyDecision.Denied(ReservationDecisionCodes.BOOKING_FORBIDDEN, []));
    }
}
