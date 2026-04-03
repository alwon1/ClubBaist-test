using System;
using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2;

public class BookingService(IEnumerable<IBookingRule> rules, IAppDbContext2 db, ILogger<BookingService> logger)
{
    public async Task<bool> CreateBookingAsync(TeeTimeBooking request)
    {
        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot);
            try
            {
                var evaluation = await EvaluateBookingAsync(request);

                if (evaluation.Slot is null)
                {
                    logger.LogWarning("Booking rejected for member {MemberId} on slot {SlotStart}: slot was not found",
                        request.BookingMemberId, request.TeeTimeSlotStart);
                    await transaction.RollbackAsync();
                    return false;
                }

                if (evaluation.SpotsRemaining < 0)
                {
                    logger.LogWarning("Booking rejected for member {MemberId} on slot {SlotStart}: {Reason}",
                        request.BookingMemberId, request.TeeTimeSlotStart, evaluation.RejectionReason);
                    await transaction.RollbackAsync();
                    return false;
                }

                db.TeeTimeBookings.Add(request);
                var saved = await db.SaveChangesAsync() > 0;
                if (!saved)
                {
                    logger.LogWarning("Booking save returned no changes for member {MemberId} on slot {SlotStart}",
                        request.BookingMemberId, request.TeeTimeSlotStart);
                    await transaction.RollbackAsync();
                    return false;
                }

                await transaction.CommitAsync();
                logger.LogInformation("Booking created for member {MemberId} on slot {SlotStart}",
                    request.BookingMemberId, request.TeeTimeSlotStart);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating booking for member {MemberId} on slot {SlotStart}",
                    request.BookingMemberId, request.TeeTimeSlotStart);
                await transaction.RollbackAsync();
                return false;
            }
        });
    }

    public async Task<bool> CancelBookingAsync(int bookingId)
    {
        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot);
            try
            {
                var booking = await db.TeeTimeBookings.FindAsync(bookingId);
                if (booking is null)
                {
                    logger.LogWarning("Cancel requested for non-existent booking {BookingId}", bookingId);
                    await transaction.RollbackAsync();
                    return false;
                }

                db.TeeTimeBookings.Remove(booking);
                var saved = await db.SaveChangesAsync() > 0;
                if (!saved)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                await transaction.CommitAsync();
                logger.LogInformation("Booking {BookingId} cancelled", bookingId);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cancelling booking {BookingId}", bookingId);
                await transaction.RollbackAsync();
                return false;
            }
        });
    }

    public async Task<bool> UpdateBookingAsync(int bookingId, IEnumerable<MemberShipInfo> additionalParticipants)
    {
        var requestedParticipants = additionalParticipants.ToList();
        if (requestedParticipants.Count > 3)
        {
            logger.LogWarning("UpdateBooking rejected: {Count} additional participants exceeds max of 3", requestedParticipants.Count);
            return false;
        }

        var requestedParticipantIds = requestedParticipants.Select(p => p.Id).ToList();
        if (requestedParticipantIds.Count != requestedParticipantIds.Distinct().Count())
        {
            logger.LogWarning("UpdateBooking rejected for booking {BookingId}: duplicate participants were supplied", bookingId);
            return false;
        }

        try
        {
            var booking = await db.TeeTimeBookings
                .Include(b => b.TeeTimeSlot)
                .Include(b => b.BookingMember)
                    .ThenInclude(m => m.MembershipLevel)
                .Include(b => b.AdditionalParticipants)
                    .ThenInclude(m => m.MembershipLevel)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking is null)
            {
                logger.LogWarning("Update requested for non-existent booking {BookingId}", bookingId);
                return false;
            }

            if (requestedParticipantIds.Contains(booking.BookingMemberId))
            {
                logger.LogWarning("UpdateBooking rejected for booking {BookingId}: booking member cannot also be an additional participant", bookingId);
                return false;
            }

            var participants = requestedParticipantIds.Count == 0
                ? []
                : await db.MemberShips
                    .Include(m => m.MembershipLevel)
                    .Where(m => requestedParticipantIds.Contains(m.Id))
                    .ToListAsync();

            if (participants.Count != requestedParticipantIds.Count)
            {
                logger.LogWarning("UpdateBooking rejected for booking {BookingId}: one or more participants were not found", bookingId);
                return false;
            }

            var proposedBooking = new TeeTimeBooking
            {
                Id = booking.Id,
                TeeTimeSlotStart = booking.TeeTimeSlotStart,
                TeeTimeSlot = booking.TeeTimeSlot,
                BookingMemberId = booking.BookingMemberId,
                BookingMember = booking.BookingMember,
                AdditionalParticipants = participants
            };

            var evaluation = await EvaluateBookingAsync(proposedBooking, booking.Id);

            if (evaluation.Slot is null)
            {
                logger.LogWarning("UpdateBooking rejected for booking {BookingId}: slot was not found", bookingId);
                return false;
            }

            if (evaluation.SpotsRemaining < 0)
            {
                logger.LogWarning("UpdateBooking rejected for booking {BookingId}: {Reason}", bookingId, evaluation.RejectionReason);
                return false;
            }

            booking.AdditionalParticipants.Clear();
            booking.AdditionalParticipants.AddRange(participants);

            await db.SaveChangesAsync();
            logger.LogInformation("Booking {BookingId} updated with {ParticipantCount} additional participants", bookingId, participants.Count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating booking {BookingId}", bookingId);
            return false;
        }
    }

    private async Task<TeeTimeEvaluation> EvaluateBookingAsync(TeeTimeBooking request, int? excludeBookingId = null)
    {
        var slot = await db.TeeTimeSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Start == request.TeeTimeSlotStart);

        if (slot is null)
        {
            return default;
        }

        return new[] { slot }
            .AsQueryable()
            .Evaluate(rules, request, excludeBookingId)
            .FirstOrDefault();
    }
}

