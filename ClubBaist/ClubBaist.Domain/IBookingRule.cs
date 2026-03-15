namespace ClubBaist.Domain;

public interface IBookingRule
{
    Task<int> EvaluateAsync(TeeTimeSlot slot, CancellationToken cancellationToken = default);
}
