using ClubBaist.Domain;

namespace ClubBaist.Services;

public class DefaultScheduleTimeService : IScheduleTimeService
{
    private static readonly TimeOnly OpeningTime = new(7, 0);
    private static readonly TimeOnly ClosingTime = new(19, 0);

    public IReadOnlyList<TimeOnly> GetScheduleTimes(DateOnly date)
    {
        var times = new List<TimeOnly>();
        var current = OpeningTime;
        var addSeven = true;

        while (current < ClosingTime)
        {
            times.Add(current);
            current = current.AddMinutes(addSeven ? 7 : 8);
            addSeven = !addSeven;
        }

        return times;
    }
}
