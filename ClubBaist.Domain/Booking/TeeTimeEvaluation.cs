namespace ClubBaist.Domain;

public record struct TeeTimeEvaluation(TeeTimeSlot Slot, int SpotsRemaining, string? RejectionReason);
