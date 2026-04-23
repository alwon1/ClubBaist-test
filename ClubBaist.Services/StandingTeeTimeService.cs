using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services;

public class StandingTeeTimeService(AppDbContext db, ILogger<StandingTeeTimeService> logger)
{
    public async Task<IReadOnlyList<StandingTeeTime>> GetAllAsync() =>
        await db.StandingTeeTimes
            .AsNoTracking()
            .OrderByDescending(s => s.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<StandingTeeTime>> GetForMemberAsync(int memberId) =>
        await db.StandingTeeTimes
            .AsNoTracking()
            .Where(s => s.BookingMemberId == memberId)
            .OrderByDescending(s => s.Id)
            .ToListAsync();

    /// <summary>
    /// Submits a new standing tee time request in Draft status.
    /// A member may only have one active (non-cancelled, non-denied) request.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> SubmitRequestAsync(StandingTeeTime request)
    {
        if (request.AdditionalParticipants.Count != 3)
            return (false, "A standing tee time request requires exactly 3 additional players (foursome).");

        if (request.EndDate <= request.StartDate)
            return (false, "End date must be after start date.");

        var participantIds = request.AdditionalParticipants.Select(p => p.Id).ToList();

        if (participantIds.Contains(request.BookingMemberId))
            return (false, "The booking member cannot also be listed as an additional player.");

        if (participantIds.Count != participantIds.Distinct().Count())
            return (false, "Duplicate players are not allowed.");

        var hasActive = await db.StandingTeeTimes.AnyAsync(s =>
            s.BookingMemberId == request.BookingMemberId &&
            s.Status != StandingTeeTimeStatus.Cancelled &&
            s.Status != StandingTeeTimeStatus.Denied);

        if (hasActive)
        {
            logger.LogWarning("Member {MemberId} already has an active standing tee time request.", request.BookingMemberId);
            return (false, "You already have an active standing tee time request.");
        }

        db.StandingTeeTimes.Add(request);
        var saved = await db.SaveChangesAsync() > 0;
        if (!saved)
        {
            logger.LogWarning("Standing tee time save returned no changes for member {MemberId}.", request.BookingMemberId);
            return (false, "Unable to save the request. Please try again.");
        }

        logger.LogInformation("Standing tee time request created for member {MemberId}.", request.BookingMemberId);
        return (true, null);
    }

    /// <summary>
    /// Approves a Draft standing tee time request.
    /// </summary>
    public async Task<bool> ApproveAsync(int id, TimeOnly approvedTime, int? priorityNumber)
    {
        if (priorityNumber.HasValue && priorityNumber.Value < 1)
        {
            logger.LogWarning("Approve rejected for standing tee time {Id}: priority number {Priority} is less than 1.", id, priorityNumber.Value);
            return false;
        }

        var request = await db.StandingTeeTimes.FindAsync(id);
        if (request is null)
        {
            logger.LogWarning("Approve requested for non-existent standing tee time {Id}.", id);
            return false;
        }

        if (request.Status != StandingTeeTimeStatus.Draft)
        {
            logger.LogWarning("Standing tee time {Id} is in status {Status}; can only approve Draft requests.", id, request.Status);
            return false;
        }

        request.ApprovedTime = approvedTime;
        request.PriorityNumber = priorityNumber;
        request.Status = StandingTeeTimeStatus.Approved;

        var saved = await db.SaveChangesAsync() > 0;
        if (saved)
            logger.LogInformation("Standing tee time {Id} approved with time {ApprovedTime}.", id, approvedTime);
        return saved;
    }

    /// <summary>
    /// Denies a Draft standing tee time request.
    /// </summary>
    public async Task<bool> DenyAsync(int id)
    {
        var request = await db.StandingTeeTimes.FindAsync(id);
        if (request is null)
        {
            logger.LogWarning("Deny requested for non-existent standing tee time {Id}.", id);
            return false;
        }

        if (request.Status != StandingTeeTimeStatus.Draft)
        {
            logger.LogWarning("Standing tee time {Id} is in status {Status}; can only deny Draft requests.", id, request.Status);
            return false;
        }

        request.Status = StandingTeeTimeStatus.Denied;
        var saved = await db.SaveChangesAsync() > 0;
        if (saved)
            logger.LogInformation("Standing tee time {Id} denied.", id);
        return saved;
    }

    /// <summary>
    /// Cancels a standing tee time request. Members may only cancel their own requests.
    /// </summary>
    public async Task<bool> CancelAsync(int id, int requestingMemberId)
    {
        var request = await db.StandingTeeTimes.FindAsync(id);
        if (request is null)
        {
            logger.LogWarning("Cancel requested for non-existent standing tee time {Id}.", id);
            return false;
        }

        if (request.BookingMemberId != requestingMemberId)
        {
            logger.LogWarning("Member {MemberId} attempted to cancel standing tee time {Id} belonging to member {OwnerId}.",
                requestingMemberId, id, request.BookingMemberId);
            return false;
        }

        if (request.Status is StandingTeeTimeStatus.Cancelled or StandingTeeTimeStatus.Denied)
        {
            logger.LogWarning("Standing tee time {Id} is already {Status}; cannot cancel.", id, request.Status);
            return false;
        }

        request.Status = StandingTeeTimeStatus.Cancelled;
        var saved = await db.SaveChangesAsync() > 0;
        if (saved)
            logger.LogInformation("Standing tee time {Id} cancelled by member {MemberId}.", id, requestingMemberId);
        return saved;
    }
}
