namespace ClubBaist.Domain;

public enum ReservationDecisionCodes
{
    BOOKING_ALLOWED = 0,
    BOOKING_WINDOW_VIOLATION = 1,
    PLAYER_COUNT_OUT_OF_RANGE = 2,
    BOOKING_NOT_FOUND_OR_NOT_ACTIVE = 3,
    BOOKING_FORBIDDEN = 4
}
