namespace ClubBaist.Domain;

public record TeeTimeSlot(
    DateOnly SlotDate,
    TimeOnly SlotTime,
    Guid BookingMemberAccountId,
    List<Guid> PlayerMemberAccountIds);
