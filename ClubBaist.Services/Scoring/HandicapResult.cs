namespace ClubBaist.Services.Scoring;

public sealed record HandicapResult
{
    public decimal? CurrentHandicap { get; init; }
    public int RoundCount { get; init; }
    public int DifferentialCount { get; init; }
    public DateTime? LastUpdated { get; init; }
    public bool IsProvisional { get; init; }
    public bool IsAvailable { get; init; }
    public string? ErrorMessage { get; init; }
}