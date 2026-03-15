namespace ClubBaist.Domain;

public sealed record BookingCancellation(
    Guid BookingId,
    Guid MemberId,
    DateTimeOffset RequestedAt);

