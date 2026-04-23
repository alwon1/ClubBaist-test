using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain.Entities.Scoring;

/// <summary>
/// Represents a completed 18-hole golf round submitted by a member.
/// One round may be recorded per tee time booking per member (enforced by the composite unique index).
/// </summary>
[Index(nameof(TeeTimeBookingId), nameof(MembershipId), IsUnique = true)]
public class GolfRound
{
    /// <summary>
    /// The tee colour a member plays from when recording a round.
    /// Used as a lookup key for course rating and slope rating (UC-PS-03).
    /// </summary>
    public enum TeeColor { Red = 0, White = 1, Blue = 2 }

    /// <summary>Auto-generated surrogate key.</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; init; }

    /// <summary>FK to the tee time booking this round was played in.</summary>
    [Required]
    [ForeignKey(nameof(TeeTimeBooking))]
    public int TeeTimeBookingId { get; init; }

    /// <summary>Navigation to the tee time booking. One-way — no back-navigation added to TeeTimeBooking.</summary>
    [Required]
    public required TeeTimeBooking TeeTimeBooking { get; init; }

    /// <summary>FK to the member who played this round (must be the primary booker).</summary>
    [Required]
    [ForeignKey(nameof(Member))]
    public int MembershipId { get; init; }

    /// <summary>Navigation to the member who submitted this round.</summary>
    [Required]
    public required MemberShipInfo Member { get; init; }

    /// <summary>The tee colour the member selected for this round.</summary>
    [Required]
    public TeeColor SelectedTeeColor { get; init; }

    /// <summary>
    /// Hole-by-hole scores for all 18 holes. Initialised to 18 nulls; all elements must be
    /// non-null and in the range 1–20 before the round is persisted. Stored as a JSON column.
    /// </summary>
    public List<uint?> Scores { get; init; } = Enumerable.Repeat<uint?>(null, 18).ToList();

    /// <summary>
    /// Local server time when the round was submitted. Set by the service — never supplied by the client.
    /// Stored as <see cref="DateTimeKind.Unspecified"/> (single-timezone club).
    /// </summary>
    [Required]
    public DateTime SubmittedAt { get; init; }

    /// <summary>
    /// ASP.NET Identity user ID of the person who submitted the round (the member or an authorised clerk).
    /// Always taken from the authenticated session — never from a client-supplied value.
    /// </summary>
    [Required]
    [MaxLength(450)]
    public required string ActingUserId { get; init; }
}
