using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2;

public class StandingTeeTimeService(IAppDbContext2 db, ILogger<StandingTeeTimeService> logger)
{
    public async Task<IReadOnlyList<StandingTeeTime>> GetAllAsync() =>
        await db.StandingTeeTimes
            .AsNoTracking()
            .OrderByDescending(s => s.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<StandingTeeTime>> GetForMemberAsync(int memberId) =>
        await db.StandingTeeTimes
            .AsNoTracking()
            .Where(s => s.BookingMemberId == memberId && s.Status != StandingTeeTimeStatus.Cancelled)
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

        var request = await db.StandingTeeTimes
            .Include(s => s.BookingMember)
            .Include(s => s.AdditionalParticipants)
            .FirstOrDefaultAsync(s => s.Id == id);
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

        var (createdCount, skippedCount) = await GenerateBookingsForApprovedRequestAsync(request, approvedTime);
        request.Status = createdCount > 0
            ? StandingTeeTimeStatus.Allocated
            : StandingTeeTimeStatus.Unallocated;

        var saved = await db.SaveChangesAsync() > 0;
        if (saved)
            logger.LogInformation(
                "Standing tee time {Id} approved with time {ApprovedTime}. Generated {CreatedCount} booking(s), skipped {SkippedCount} occurrence(s).",
                id,
                approvedTime,
                createdCount,
                skippedCount);
        return saved;
    }

    private async Task<(int CreatedCount, int SkippedCount)> GenerateBookingsForApprovedRequestAsync(
        StandingTeeTime request,
        TimeOnly approvedTime)
    {
        var additionalParticipantIds = request.AdditionalParticipants.Select(p => p.Id).ToList();

        var participants = await db.MemberShips
            .Where(m => additionalParticipantIds.Contains(m.Id))
            .ToListAsync();

        if (participants.Count != additionalParticipantIds.Count)
        {
            logger.LogWarning(
                "Standing tee time {Id} approval could not resolve all additional participants. Expected {Expected}, resolved {Actual}.",
                request.Id,
                additionalParticipantIds.Count,
                participants.Count);
            return (0, 0);
        }

        var bookingMember = await db.MemberShips.FirstOrDefaultAsync(m => m.Id == request.BookingMemberId);
        if (bookingMember is null)
        {
            logger.LogWarning("Standing tee time {Id} approval failed to resolve booking member {MemberId}.", request.Id, request.BookingMemberId);
            return (0, 0);
        }

        var startDate = request.StartDate > DateOnly.FromDateTime(DateTime.Today)
            ? request.StartDate
            : DateOnly.FromDateTime(DateTime.Today);

        if (startDate > request.EndDate)
        {
            return (0, 0);
        }

        var occurrenceDates = EnumerateOccurrenceDates(startDate, request.EndDate, request.RequestedDayOfWeek).ToList();
        var createdCount = 0;
        var skippedCount = 0;

        foreach (var date in occurrenceDates)
        {
            var requestedDateTime = DateTime.SpecifyKind(date.ToDateTime(approvedTime), DateTimeKind.Unspecified);
            var windowStart = requestedDateTime.AddMinutes(-request.ToleranceMinutes);
            var windowEnd = requestedDateTime.AddMinutes(request.ToleranceMinutes);

            var candidateSlots = await db.TeeTimeSlots
                .Where(s => s.Start >= windowStart && s.Start <= windowEnd)
                .ToListAsync();

            var orderedSlots = candidateSlots
                .OrderBy(slot => Math.Abs((slot.Start - requestedDateTime).TotalMinutes))
                .ThenBy(slot => slot.Start)
                .ToList();

            var allocatedSlot = await FindAllocatableSlotAsync(
                orderedSlots,
                request,
                additionalParticipantIds.ToHashSet());

            if (allocatedSlot is null)
            {
                skippedCount++;
                continue;
            }

            db.TeeTimeBookings.Add(new TeeTimeBooking
            {
                TeeTimeSlotStart = allocatedSlot.Start,
                TeeTimeSlot = allocatedSlot,
                BookingMemberId = bookingMember.Id,
                BookingMember = bookingMember,
                StandingTeeTimeId = request.Id,
                StandingTeeTime = request,
                AdditionalParticipants = [.. participants]
            });

            createdCount++;
        }

        return (createdCount, skippedCount);
    }

    private async Task<TeeTimeSlot?> FindAllocatableSlotAsync(
        IReadOnlyList<TeeTimeSlot> orderedSlots,
        StandingTeeTime request,
        ISet<int> additionalParticipantIds)
    {
        foreach (var slot in orderedSlots)
        {
            var bookingsAtSlot = await db.TeeTimeBookings
                .Include(b => b.AdditionalParticipants)
                .Where(b => b.TeeTimeSlotStart == slot.Start)
                .ToListAsync();

            var totalBookedSpots = bookingsAtSlot.Sum(b => b.ParticipantCount);
            if (totalBookedSpots + request.ParticipantCount > 4)
            {
                continue;
            }

            var existingParticipantIds = bookingsAtSlot
                .SelectMany(b => b.ParticipantIds)
                .ToHashSet();

            if (existingParticipantIds.Contains(request.BookingMemberId))
            {
                continue;
            }

            if (additionalParticipantIds.Any(existingParticipantIds.Contains))
            {
                continue;
            }

            return slot;
        }

        return null;
    }

    private static IEnumerable<DateOnly> EnumerateOccurrenceDates(DateOnly startDate, DateOnly endDate, DayOfWeek dayOfWeek)
    {
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek == dayOfWeek)
            {
                yield return date;
            }
        }
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
