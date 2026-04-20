using ClubBaist.Domain2.Entities.Scoring;

namespace ClubBaist.Services2.Scoring;

/// <summary>A tee time booking that is eligible for score entry by the member who booked it.</summary>
public record EligibleBooking(
    /// <summary>ID of the <see cref="ClubBaist.Domain2.TeeTimeBooking"/>.</summary>
    int BookingId,
    /// <summary>Scheduled start time of the tee time slot.</summary>
    DateTime TeeTimeSlotStart,
    /// <summary>Total number of players in the group (primary booker + additional participants).</summary>
    int ParticipantCount);

/// <summary>
/// Request to submit a completed round. All fields except <see cref="Scores"/> are validated
/// server-side. <see cref="Scores"/> must contain exactly 18 non-null values in the range 1–20.
/// </summary>
public record SubmitRoundRequest(
    /// <summary>ID of the <see cref="ClubBaist.Domain2.TeeTimeBooking"/> for this round.</summary>
    int BookingId,
    /// <summary>ID of the <see cref="ClubBaist.Domain2.MemberShipInfo"/> submitting the round.</summary>
    int MembershipId,
    /// <summary>Tee colour the member played from.</summary>
    GolfRound.TeeColor TeeColor,
    /// <summary>Hole-by-hole scores — exactly 18 elements, all non-null, each in 1–20.</summary>
    IReadOnlyList<uint?> Scores);

/// <summary>Result returned by <see cref="ScoreService.SubmitRoundAsync"/>.</summary>
public record ScoreSubmissionResult(
    /// <summary><c>true</c> if the round was persisted successfully.</summary>
    bool Success,
    /// <summary>Human-readable error message when <see cref="Success"/> is <c>false</c>.</summary>
    string? ErrorMessage = null);
