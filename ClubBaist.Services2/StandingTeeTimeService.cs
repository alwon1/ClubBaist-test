using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2;

public class StandingTeeTimeService(IAppDbContext2 db, ILogger<StandingTeeTimeService> logger)
{
    /// <summary>
    /// Submits a standing tee time request for a shareholder member.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public async Task<string?> SubmitRequestAsync(StandingTeeTime request)
    {
        if (request.AdditionalParticipants.Count != 3)
        {
            logger.LogWarning(
                "Standing tee time request rejected for member {MemberId}: must be a foursome (got {Count} additional participants)",
                request.BookingMemberId, request.AdditionalParticipants.Count);
            return "A standing tee time request must be for a foursome — please add exactly 3 additional players.";
        }

        var participantIds = request.AdditionalParticipants.Select(p => p.Id).ToList();

        if (participantIds.Distinct().Count() != participantIds.Count)
            return "Player list contains duplicate members.";

        if (participantIds.Contains(request.BookingMemberId))
            return "The booking member cannot also be listed as an additional player.";

        if (request.EndDate <= request.StartDate)
            return "End date must be after the start date.";

        var hasActiveRequest = await db.StandingTeeTimes
            .AnyAsync(s => s.BookingMemberId == request.BookingMemberId
                && s.Status != StandingTeeTimeStatus.Cancelled
                && s.Status != StandingTeeTimeStatus.Denied);

        if (hasActiveRequest)
        {
            logger.LogWarning(
                "Standing tee time request rejected for member {MemberId}: already has an active request",
                request.BookingMemberId);
            return "You already have an active standing tee time request. Please cancel it before submitting a new one.";
        }

        db.StandingTeeTimes.Add(request);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Standing tee time request {RequestId} submitted for member {MemberId} ({DayOfWeek} at {Time})",
            request.Id, request.BookingMemberId, request.RequestedDayOfWeek, request.RequestedTime);

        return null;
    }

    /// <summary>
    /// Gets all standing tee time requests for a member, newest first.
    /// </summary>
    public Task<List<StandingTeeTime>> GetMemberRequestsAsync(int memberId) =>
        db.StandingTeeTimes
            .Where(s => s.BookingMemberId == memberId)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync();

    /// <summary>
    /// Cancels a standing tee time request on behalf of the owning member.
    /// Returns false if the request is not found, already cancelled, or already denied.
    /// </summary>
    public async Task<bool> CancelRequestAsync(int requestId, int memberId)
    {
        var request = await db.StandingTeeTimes
            .FirstOrDefaultAsync(s => s.Id == requestId && s.BookingMemberId == memberId);

        if (request is null)
        {
            logger.LogWarning(
                "Cancel failed: standing tee time {RequestId} not found for member {MemberId}",
                requestId, memberId);
            return false;
        }

        if (request.Status is StandingTeeTimeStatus.Cancelled or StandingTeeTimeStatus.Denied)
            return false;

        request.Status = StandingTeeTimeStatus.Cancelled;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Standing tee time {RequestId} cancelled by member {MemberId}",
            requestId, memberId);
        return true;
    }
}
