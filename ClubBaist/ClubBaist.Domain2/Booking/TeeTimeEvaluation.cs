namespace ClubBaist.Domain2;

public record struct TeeTimeEvaluation(TeeTimeSlot Slot, int SpotsRemaining, string? RejectionReason);
