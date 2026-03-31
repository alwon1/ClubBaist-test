namespace ClubBaist.Domain;

public record TeeTimeSlot(
    DateOnly SlotDate,
    TimeOnly SlotTime,
    int BookingMemberAccountId,
    List<int> PlayerMemberAccountIds);
