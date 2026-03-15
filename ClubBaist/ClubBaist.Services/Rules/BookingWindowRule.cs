using ClubBaist.Domain;

namespace ClubBaist.Services.Rules;

public class BookingWindowRule : IBookingRule
{
    public Task<int> EvaluateAsync(TeeTimeSlot slot, BookingEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var season = context.ActiveSeason;

        var allowed = season is not null
            && season.StartDate <= slot.SlotDate
            && season.EndDate >= slot.SlotDate;

        return Task.FromResult(allowed ? int.MaxValue : 0);
    }
}
