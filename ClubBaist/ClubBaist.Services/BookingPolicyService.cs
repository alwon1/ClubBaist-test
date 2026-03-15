using ClubBaist.Domain;

namespace ClubBaist.Services;

public sealed class BookingPolicyService
{
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
