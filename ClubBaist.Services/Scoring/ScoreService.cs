using System.Data;
using ClubBaist.Domain;
using ClubBaist.Domain.Entities.Scoring;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services.Scoring;

public class ScoreService(AppDbContext db, ILogger<ScoreService> logger)
{
    public async Task<List<TeeTimeBooking>> GetEligibleBookingsAsync(
        int memberId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

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
            .ToList();
    }

    public async Task<(bool Success, string? Error)> SubmitRoundAsync(
        int bookingId, int membershipId, GolfRound.TeeColor teeColor,
        IReadOnlyList<uint?> scores, string actingUserId,
        CancellationToken cancellationToken = default)
    {
        var memberExists = await db.MemberShips.AnyAsync(m => m.Id == membershipId, cancellationToken);
        if (!memberExists)
        {
            logger.LogWarning("SubmitRound rejected: member {MemberId} not found", membershipId);
            return (false, "Member not found");
        }

        var booking = await db.TeeTimeBookings
            .Include(b => b.AdditionalParticipants)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

        if (booking is null || booking.BookingMemberId != membershipId)
        {
            logger.LogWarning("SubmitRound rejected: booking {BookingId} not found or not owned by member {MemberId}",
                bookingId, membershipId);
            return (false, "Booking not found or not owned by member");
        }

        var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        if (now < booking.TeeTimeSlotStart + MinDuration(booking.ParticipantCount))
        {
            logger.LogWarning("SubmitRound rejected: booking {BookingId} is inside the time-lock window", bookingId);
            return (false, "Round not yet eligible — minimum completion time has not elapsed");
        }

        if (await db.GolfRounds.AnyAsync(r => r.TeeTimeBookingId == bookingId && r.MembershipId == membershipId, cancellationToken))
        {
            logger.LogWarning("SubmitRound rejected: score already exists for booking {BookingId} member {MemberId}", bookingId, membershipId);
            return (false, "Score already submitted for this booking");
        }

        if (scores.Count != 18 || scores.Any(s => !s.HasValue))
        {
            logger.LogWarning("SubmitRound rejected: incomplete scorecard for booking {BookingId}", bookingId);
            return (false, "Incomplete scorecard — all 18 hole scores are required");
        }

        for (var i = 0; i < 18; i++)
        {
            if (scores[i]!.Value < 1 || scores[i]!.Value > 20)
            {
                logger.LogWarning("SubmitRound rejected: hole {Hole} score {Score} out of range for booking {BookingId}",
                    i + 1, scores[i], bookingId);
                return (false, $"Score out of range on hole {i + 1} — valid range is 1 to 20");
            }
        }

        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Snapshot, cancellationToken);
            try
            {
                if (await db.GolfRounds.AnyAsync(r => r.TeeTimeBookingId == bookingId && r.MembershipId == membershipId, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    logger.LogWarning("SubmitRound concurrency guard: duplicate for booking {BookingId} member {MemberId}", bookingId, membershipId);
                    return (false, "Score already submitted for this booking and member");
                }

                var round = new GolfRound
                {
                    TeeTimeBookingId = bookingId,
                    TeeTimeBooking = null!,
                    MembershipId = membershipId,
                    Member = null!,
                    SelectedTeeColor = teeColor,
                    Scores = scores.ToList(),
                    SubmittedAt = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                    ActingUserId = actingUserId
                };

                db.GolfRounds.Add(round);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation("Golf round submitted: BookingId={BookingId} MemberId={MemberId} ActingUser={ActingUserId}",
                    bookingId, membershipId, actingUserId);
                return (true, (string?)null);
            }
            catch (DbUpdateException ex) when (IsUniqueIndexViolation(ex))
            {
                await transaction.RollbackAsync(cancellationToken);
                logger.LogWarning("SubmitRound unique index violation: BookingId={BookingId} MemberId={MemberId}",
                    bookingId, membershipId);
                return (false, "Score already submitted for this booking and member");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SubmitRound failed: BookingId={BookingId} MemberId={MemberId}",
                    bookingId, membershipId);
                await transaction.RollbackAsync(cancellationToken);
                return (false, "An error occurred while submitting the score");
            }
        });
    }

    public async Task<IReadOnlyList<GolfRound>> GetRoundsByMemberAsync(
        int memberId, CancellationToken cancellationToken = default)
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
