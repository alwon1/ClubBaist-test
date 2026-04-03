using System;
using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2;

public class BookingService(IEnumerable<IBookingRule> rules, AppDbContext db, ILogger<BookingService> logger)
{
    public async Task<bool> CreateBooking(TeeTimeBooking request)
    {
        await db.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot);
        try
        {
            var evaluation = await db.TeeTimeSlots.Evaluate(rules, request).FirstOrDefaultAsync();

            if (evaluation.SpotsRemaining < 0)
            {
                logger.LogWarning("Booking rejected for member {MemberId} on slot {SlotStart}: {Reason}",
                    request.BookingMemberId, request.TeeTimeSlotStart, evaluation.RejectionReason);
                return false;
            }

            db.TeeTimeBookings.Add(request);
            logger.LogInformation("Booking created for member {MemberId} on slot {SlotStart}",
                request.BookingMemberId, request.TeeTimeSlotStart);
            return await db.SaveChangesAsync() > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating booking for member {MemberId} on slot {SlotStart}",
                request.BookingMemberId, request.TeeTimeSlotStart);
            await db.Database.RollbackTransactionAsync();
            return false;
        }
    }

    public async Task<bool> CancelBookingAsync(int bookingId)
    {
        var booking = await db.TeeTimeBookings.FindAsync(bookingId);
        if (booking is null)
        {
            logger.LogWarning("Cancel requested for non-existent booking {BookingId}", bookingId);
            return false;
        }

        db.TeeTimeBookings.Remove(booking);
        var saved = await db.SaveChangesAsync() > 0;
        if (saved)
            logger.LogInformation("Booking {BookingId} cancelled", bookingId);
        return saved;
    }

    public async Task<bool> UpdateBookingAsync(int bookingId, IEnumerable<MemberShipInfo> additionalParticipants)
    {
        var participants = additionalParticipants.ToList();
        if (participants.Count > 3)
        {
            logger.LogWarning("UpdateBooking rejected: {Count} additional participants exceeds max of 3", participants.Count);
            return false;
        }

        var booking = await db.TeeTimeBookings
            .Include(b => b.AdditionalParticipants)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking is null)
        {
            logger.LogWarning("Update requested for non-existent booking {BookingId}", bookingId);
            return false;
        }

        booking.AdditionalParticipants.Clear();
        booking.AdditionalParticipants.AddRange(participants);
        return await db.SaveChangesAsync() > 0;
    }
}

