namespace ClubBaist.Domain;

public interface IScheduleTimeService
{
    IReadOnlyList<TimeOnly> GetScheduleTimes(DateOnly date);
}
