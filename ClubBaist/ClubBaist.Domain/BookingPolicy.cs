namespace ClubBaist.Domain;

public sealed record BookingPolicy(
    Guid SeasonId,
    int MinPlayers,
    int MaxPlayers);

