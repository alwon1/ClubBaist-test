using ClubBaist.Domain;

namespace ClubBaist.Services.Rules;

public class BookingWindowRule : IBookingRule
{
    private readonly ISeasonService _seasonService;

    public BookingWindowRule(ISeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    public Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var allowed = _seasonService.GetSeasonForDate(slot.SlotDate) is not null;
        return Task.FromResult(allowed ? int.MaxValue : 0);
    }
}
