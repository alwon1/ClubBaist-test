namespace ClubBaist.Services2.Scoring;

/// <summary>
/// Abstracts the current local server time to enable deterministic time-lock boundary tests.
/// </summary>
public interface IScoreClock
{
    /// <summary>
    /// Returns the current local server time as <see cref="DateTimeKind.Unspecified"/>,
    /// consistent with how the club stores all timestamps (single-timezone operation).
    /// </summary>
    DateTime Now { get; }
}

/// <summary>
/// Production implementation that returns <c>DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)</c>.
/// </summary>
internal sealed class SystemScoreClock : IScoreClock
{
    /// <inheritdoc />
    public DateTime Now => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
}
