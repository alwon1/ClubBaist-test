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

        var hasActiveForDay = await db.StandingTeeTimes.AnyAsync(s =>
            s.BookingMemberId == request.BookingMemberId &&
            s.RequestedDayOfWeek == request.RequestedDayOfWeek &&
            s.Status != StandingTeeTimeStatus.Cancelled &&
            s.Status != StandingTeeTimeStatus.Denied);

        if (hasActiveForDay)
        {
            logger.LogWarning("Member {MemberId} already has an active standing tee time request for {DayOfWeek}.",
                request.BookingMemberId, request.RequestedDayOfWeek);
            return (false, $"You already have an active standing tee time request for {request.RequestedDayOfWeek}.");
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

    /// <summary>
    /// Runs the weekly standing tee time allocation for a specific date.
    /// Iterates approved requests in priority order (lower PriorityNumber first, nulls last)
    /// and creates <see cref="TeeTimeBooking"/> records for available slots on the target date.
    /// </summary>
    /// <param name="targetDate">The specific date to allocate standing tee times for.</param>
    /// <param name="maxParticipantsPerSlot">Maximum total participants allowed per slot (default 4).</param>
    /// <returns>A summary of allocated, unallocated, and skipped requests.</returns>
    public async Task<AllocationRunResult> RunWeeklyAllocationAsync(DateOnly targetDate, int maxParticipantsPerSlot = 4)
    {
        var dayStart = DateTime.SpecifyKind(targetDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var dayEnd = DateTime.SpecifyKind(targetDate.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Unspecified);

        // Load eligible requests: Approved, Allocated, or Unallocated, covering this date and day of week.
        var requests = await db.StandingTeeTimes
            .Where(s =>
                s.RequestedDayOfWeek == targetDate.DayOfWeek &&
                s.StartDate <= targetDate &&
                s.EndDate >= targetDate &&
                (s.Status == StandingTeeTimeStatus.Approved ||
                 s.Status == StandingTeeTimeStatus.Allocated ||
                 s.Status == StandingTeeTimeStatus.Unallocated))
            .OrderBy(s => s.PriorityNumber == null ? int.MaxValue : s.PriorityNumber)
            .ThenBy(s => s.Id)
            .ToListAsync();

        if (requests.Count == 0)
        {
            logger.LogInformation("Weekly allocation for {TargetDate}: no eligible requests found.", targetDate);
            return new AllocationRunResult(0, 0, 0);
        }

        // Load all tee time slots for the target day (tracked so they can be used as navigation properties).
        var allDaySlots = await db.TeeTimeSlots
            .Where(s => s.Start >= dayStart && s.Start <= dayEnd)
            .OrderBy(s => s.Start)
            .ToListAsync();

        // Load special events overlapping the target day.
        var daySpecialEvents = await db.SpecialEvents
            .AsNoTracking()
            .Where(e => e.Start <= dayEnd && e.End > dayStart)
            .ToListAsync();

        var blockedSlotStarts = allDaySlots
            .Where(slot => daySpecialEvents.Any(e => slot.Start >= e.Start && slot.Start < e.End))
            .Select(slot => slot.Start)
            .ToHashSet();

        // Load existing bookings for the target day to determine capacity and detect duplicates.
        var existingBookings = await db.TeeTimeBookings
            .AsNoTracking()
            .Where(b => b.TeeTimeSlotStart >= dayStart && b.TeeTimeSlotStart <= dayEnd)
            .Select(b => new
            {
                b.TeeTimeSlotStart,
                b.BookingMemberId,
                b.StandingTeeTimeId,
                ParticipantCount = 1 + b.AdditionalParticipants.Count
            })
            .ToListAsync();

        // Remaining capacity per slot, updated as this run allocates requests.
        var slotCapacity = allDaySlots.ToDictionary(
            s => s.Start,
            s => maxParticipantsPerSlot - existingBookings
                .Where(b => b.TeeTimeSlotStart == s.Start)
                .Sum(b => b.ParticipantCount));

        // Member+slot pairs already booked (to avoid duplicate bookings on re-run or conflict).
        var memberSlotBooked = existingBookings
            .Select(b => (b.TeeTimeSlotStart, b.BookingMemberId))
            .ToHashSet();

        // Standing request IDs that already have a booking on this date (idempotent re-run support).
        var alreadyAllocatedStandingIds = existingBookings
            .Where(b => b.StandingTeeTimeId.HasValue)
            .Select(b => b.StandingTeeTimeId!.Value)
            .ToHashSet();

        // Slots claimed by higher-priority requests in this run.
        var claimedThisRun = new HashSet<DateTime>();

        int allocated = 0, unallocated = 0, skipped = 0;

        foreach (var request in requests)
        {
            // Skip requests with no approved time.
            if (!request.ApprovedTime.HasValue)
            {
                skipped++;
                continue;
            }

            // Skip if this standing request already has a booking for this date (idempotent).
            if (alreadyAllocatedStandingIds.Contains(request.Id))
            {
                skipped++;
                continue;
            }

            var approvedTimeSpan = request.ApprovedTime.Value.ToTimeSpan();
            var toleranceSpan = TimeSpan.FromMinutes(request.ToleranceMinutes);

            var minTimeSpan = approvedTimeSpan - toleranceSpan;
            var maxTimeSpan = approvedTimeSpan + toleranceSpan;

            // Clamp to same-day bounds.
            if (minTimeSpan < TimeSpan.Zero) minTimeSpan = TimeSpan.Zero;
            if (maxTimeSpan >= TimeSpan.FromHours(24)) maxTimeSpan = TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1);

            int partySize = 1 + request.AdditionalParticipants.Count;

            // Find the best available slot: within tolerance, not blocked by a special event,
            // not already claimed this run, sufficient remaining capacity, and closest to approved time.
            var candidateSlot = allDaySlots
                .Where(s =>
                    s.Start.TimeOfDay >= minTimeSpan &&
                    s.Start.TimeOfDay <= maxTimeSpan &&
                    !blockedSlotStarts.Contains(s.Start) &&
                    !claimedThisRun.Contains(s.Start) &&
                    !memberSlotBooked.Contains((s.Start, request.BookingMemberId)) &&
                    slotCapacity.GetValueOrDefault(s.Start, 0) >= partySize)
                .OrderBy(s => Math.Abs((s.Start.TimeOfDay - approvedTimeSpan).TotalMinutes))
                .FirstOrDefault();

            if (candidateSlot is not null)
            {
                var booking = new TeeTimeBooking
                {
                    TeeTimeSlotStart = candidateSlot.Start,
                    TeeTimeSlot = candidateSlot,
                    BookingMemberId = request.BookingMemberId,
                    BookingMember = request.BookingMember,
                    StandingTeeTimeId = request.Id,
                    StandingTeeTime = request,
                    AdditionalParticipants = request.AdditionalParticipants.ToList()
                };

                db.TeeTimeBookings.Add(booking);

                claimedThisRun.Add(candidateSlot.Start);
                slotCapacity[candidateSlot.Start] -= partySize;
                memberSlotBooked.Add((candidateSlot.Start, request.BookingMemberId));

                request.Status = StandingTeeTimeStatus.Allocated;
                allocated++;
            }
            else
            {
                request.Status = StandingTeeTimeStatus.Unallocated;
                unallocated++;
            }
        }

        if (allocated + unallocated > 0)
            await db.SaveChangesAsync();

        logger.LogInformation(
            "Weekly allocation for {TargetDate}: {Allocated} allocated, {Unallocated} unallocated, {Skipped} skipped.",
            targetDate, allocated, unallocated, skipped);

        return new AllocationRunResult(allocated, unallocated, skipped);
    }
}

/// <summary>
/// Summary of results from a weekly standing tee time allocation run.
/// </summary>
/// <param name="Allocated">Number of requests that were successfully allocated to a tee time slot.</param>
/// <param name="Unallocated">Number of requests for which no suitable slot was available.</param>
/// <param name="Skipped">Number of requests skipped (no approved time set, or already allocated for this date).</param>
public record AllocationRunResult(int Allocated, int Unallocated, int Skipped);
