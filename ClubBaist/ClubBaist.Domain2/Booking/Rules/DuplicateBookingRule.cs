namespace ClubBaist.Domain2;

/// <summary>
/// Prevents a member or any of their additional participants from being booked into a slot
/// that falls within <see cref="ConflictWindowHours"/> hours of an existing booking for any participant.
/// Also exposes a MemberShipInfo overload used by the UI availability view to flag conflicting slots.
/// </summary>
public class DuplicateBookingRule(IQueryable<TeeTimeBooking> bookings, double conflictWindowHours = 2.0) : IBookingRule
{
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, TeeTimeBooking booking, int? excludeBookingId = null)
    {
        var participantIds = booking.Participants.Select(p => p.Id).ToList();
        return query.Select(p => p.SpotsRemaining < 0 ? p :
            bookings.Any(b => (excludeBookingId == null || b.Id != excludeBookingId)
                           && b.TeeTimeSlotStart > p.Slot.Start.AddHours(-conflictWindowHours)
                           && b.TeeTimeSlotStart < p.Slot.Start.AddHours(conflictWindowHours)
                           && (participantIds.Contains(b.BookingMemberId)
                               || b.AdditionalParticipants.Any(m => participantIds.Contains(m.Id))))
                ? new TeeTimeEvaluation(p.Slot, -2, $"One or more participants have a booking within {conflictWindowHours} hours of this tee time")
                : p);
    }

    /// <summary>
    /// Per-member availability overload: marks slots where the member has a booking within the conflict window.
    /// SpotsRemaining is left unchanged so the slot still shows capacity; the RejectionReason
    /// signals the UI that this member cannot book again.
    /// </summary>
    public IQueryable<TeeTimeEvaluation> Evaluate(IQueryable<TeeTimeEvaluation> query, MemberShipInfo member)
    {
        var memberId = member.Id;
        return query.Select(p => p.SpotsRemaining < 0 ? p :
            bookings.Any(b => b.TeeTimeSlotStart > p.Slot.Start.AddHours(-conflictWindowHours)
                           && b.TeeTimeSlotStart < p.Slot.Start.AddHours(conflictWindowHours)
                           && (b.BookingMemberId == memberId
                               || b.AdditionalParticipants.Any(m => m.Id == memberId)))
                ? new TeeTimeEvaluation(p.Slot, p.SpotsRemaining, $"You have a booking within {conflictWindowHours} hours of this tee time")
                : p);
    }
}

