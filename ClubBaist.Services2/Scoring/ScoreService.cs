using System.Data;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities.Scoring;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2.Scoring;

/// <summary>
/// Coordinates end-to-end score submission workflow for UC-PS-01.
/// Retrieves eligible bookings, validates and persists <see cref="GolfRound"/>,
/// and returns a member's submitted rounds.
/// </summary>
public class ScoreService(IAppDbContext2 db, ILogger<ScoreService> logger, IScoreClock clock)
{
    /// <summary>
    /// Returns tee time bookings for <paramref name="memberId"/> that are eligible for score entry.
    /// Eligible means the minimum round-completion window has elapsed and no score has been recorded yet.
    /// </summary>
    public async Task<IReadOnlyList<EligibleBooking>> GetEligibleBookingsAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.Now;

        var bookings = await db.TeeTimeBookings
            .Include(b => b.AdditionalParticipants)
            .Where(b => b.BookingMemberId == memberId && b.TeeTimeSlotStart < now)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (bookings.Count == 0)
            return [];

        var bookingIds = bookings.Select(b => b.Id).ToList();
        var scoredIds = await db.GolfRounds
            .Where(r => bookingIds.Contains(r.TeeTimeBookingId))
            .Select(r => r.TeeTimeBookingId)
            .ToListAsync(cancellationToken);

        return bookings
            .Where(b => !scoredIds.Contains(b.Id))
            .Where(b => now >= b.TeeTimeSlotStart + MinDuration(b.ParticipantCount))
            .OrderByDescending(b => b.TeeTimeSlotStart)
            .Select(b => new EligibleBooking(b.Id, b.TeeTimeSlotStart, b.ParticipantCount))
            .ToList();
    }

    /// <summary>
    /// Validates and persists a golf round for the given request.
    /// Runs under Snapshot isolation to guard against concurrent duplicate submissions.
    /// </summary>
    /// <param name="request">The score submission request.</param>
    /// <param name="actingUserId">
    /// ASP.NET Identity user ID of the authenticated submitter (member or clerk).
    /// Always taken from the server-side auth session — never from client input.
    /// </param>
    public async Task<ScoreSubmissionResult> SubmitRoundAsync(
        SubmitRoundRequest request,
        string actingUserId,
        CancellationToken cancellationToken = default)
    {
        // --- Step 1: Member exists ---
        var memberExists = await db.MemberShips
            .AnyAsync(m => m.Id == request.MembershipId, cancellationToken);
        if (!memberExists)
        {
            logger.LogWarning("SubmitRound rejected: member {MemberId} not found", request.MembershipId);
            return new ScoreSubmissionResult(false, "Member not found");
        }

        // --- Step 2: Booking exists and belongs to member ---
        var booking = await db.TeeTimeBookings
            .Include(b => b.AdditionalParticipants)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking is null || booking.BookingMemberId != request.MembershipId)
        {
            logger.LogWarning("SubmitRound rejected: booking {BookingId} not found or not owned by member {MemberId}",
                request.BookingId, request.MembershipId);
            return new ScoreSubmissionResult(false, "Booking not found or not owned by member");
        }

        // --- Step 3: Time-lock has elapsed ---
        var now = clock.Now;
        if (now < booking.TeeTimeSlotStart + MinDuration(booking.ParticipantCount))
        {
            logger.LogWarning("SubmitRound rejected: booking {BookingId} is inside the time-lock window", request.BookingId);
            return new ScoreSubmissionResult(false, "Round not yet eligible — minimum completion time has not elapsed");
        }

        // --- Step 4: No existing round (pre-check before transaction) ---
        var alreadyScoredPreCheck = await db.GolfRounds
            .AnyAsync(r => r.TeeTimeBookingId == request.BookingId, cancellationToken);
        if (alreadyScoredPreCheck)
        {
            logger.LogWarning("SubmitRound rejected: score already exists for booking {BookingId}", request.BookingId);
            return new ScoreSubmissionResult(false, "Score already submitted for this booking");
        }

        // --- Step 5: Exactly 18 scores, all non-null ---
        if (request.Scores.Count != 18 || request.Scores.Any(s => !s.HasValue))
        {
            logger.LogWarning("SubmitRound rejected: incomplete scorecard for booking {BookingId}", request.BookingId);
            return new ScoreSubmissionResult(false, "Incomplete scorecard — all 18 hole scores are required");
        }

        // --- Step 6: All scores in range 1–20 ---
        for (var i = 0; i < 18; i++)
        {
            if (request.Scores[i]!.Value < 1 || request.Scores[i]!.Value > 20)
            {
                logger.LogWarning("SubmitRound rejected: hole {Hole} score {Score} out of range for booking {BookingId}",
                    i + 1, request.Scores[i], request.BookingId);
                return new ScoreSubmissionResult(false, $"Score out of range on hole {i + 1} — valid range is 1 to 20");
            }
        }

        // --- Transaction: snapshot isolation with concurrency guard ---
        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken);
            try
            {
                // Re-check inside transaction (concurrency guard)
                var alreadyScored = await db.GolfRounds
                    .AnyAsync(r => r.TeeTimeBookingId == request.BookingId, cancellationToken);
                if (alreadyScored)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    logger.LogWarning("SubmitRound concurrency guard: duplicate detected for booking {BookingId}", request.BookingId);
                    return new ScoreSubmissionResult(false, "Score already submitted for this booking and member");
                }

                // Load fresh navigation objects for entity construction (AsNoTracking so change tracker stays clean)
                var bookingForRound = await db.TeeTimeBookings
                    .AsNoTracking()
                    .FirstAsync(b => b.Id == request.BookingId, cancellationToken);
                var member = await db.MemberShips
                    .AsNoTracking()
                    .FirstAsync(m => m.Id == request.MembershipId, cancellationToken);

                var round = new GolfRound
                {
                    TeeTimeBookingId = request.BookingId,
                    TeeTimeBooking = bookingForRound,
                    MembershipId = request.MembershipId,
                    Member = member,
                    SelectedTeeColor = request.TeeColor,
                    Scores = request.Scores.ToList(),
                    SubmittedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                    ActingUserId = actingUserId
                };

                db.GolfRounds.Add(round);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "Golf round submitted: BookingId={BookingId} MemberId={MemberId} ActingUser={ActingUserId}",
                    request.BookingId, request.MembershipId, actingUserId);
                return new ScoreSubmissionResult(true);
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning("SubmitRound unique index violation: BookingId={BookingId} MemberId={MemberId}",
                    request.BookingId, request.MembershipId);
                return new ScoreSubmissionResult(false, "Score already submitted for this booking and member");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SubmitRound failed: BookingId={BookingId} MemberId={MemberId}",
                    request.BookingId, request.MembershipId);
                await transaction.RollbackAsync(cancellationToken);
                return new ScoreSubmissionResult(false, "An error occurred while submitting the score");
            }
        });
    }

    /// <summary>
    /// Returns all golf rounds submitted by <paramref name="memberId"/>, ordered by submission time descending.
    /// </summary>
    public async Task<IReadOnlyList<GolfRound>> GetRoundsByMemberAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        return await db.GolfRounds
            .Where(r => r.MembershipId == memberId)
            .Include(r => r.TeeTimeBooking)
            .OrderByDescending(r => r.SubmittedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    private static TimeSpan MinDuration(int playerCount) => playerCount switch
    {
        1 => TimeSpan.FromHours(2),
        2 => TimeSpan.FromHours(2.5),
        3 => TimeSpan.FromHours(3),
        _ => TimeSpan.FromHours(3.5)
    };

    private static bool IsUniqueIndexViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
}
