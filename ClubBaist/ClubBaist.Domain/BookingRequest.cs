namespace ClubBaist.Domain;

public sealed record BookingRequest(
    Guid MemberId,
    DateOnly PlayDate,
    TimeOnly TeeTime,
    DateTimeOffset RequestedAt,
    IReadOnlyList<Guid> PlayerMemberAccountIds);

