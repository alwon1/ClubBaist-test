using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Rules;

public class MembershipTimeRestrictionRule<TKey> : IBookingRule where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;

    public MembershipTimeRestrictionRule(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> EvaluateAsync(TeeTimeSlot slot, CancellationToken cancellationToken = default)
    {
        var member = await _dbContext.MemberAccounts
            .FirstOrDefaultAsync(m => m.MemberAccountId == slot.BookingMemberAccountId, cancellationToken);

        if (member is null)
            return 0;

        return IsAllowed(member.MembershipCategory, slot.SlotDate, slot.SlotTime)
            ? int.MaxValue
            : 0;
    }

    private static bool IsAllowed(MembershipCategory category, DateOnly date, TimeOnly time)
    {
        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        return category switch
        {
            // Gold: anytime
            MembershipCategory.Shareholder or MembershipCategory.Associate => true,

            // Silver: Mon-Fri before 3PM or after 5:30PM; Weekends after 11AM
            MembershipCategory.ShareholderSpouse or MembershipCategory.AssociateSpouse =>
                isWeekend
                    ? time >= new TimeOnly(11, 0)
                    : time < new TimeOnly(15, 0) || time >= new TimeOnly(17, 30),

            // Bronze: Mon-Fri before 3PM or after 6PM; Weekends after 1PM
            MembershipCategory.PeeWee or MembershipCategory.Junior or MembershipCategory.Intermediate =>
                isWeekend
                    ? time >= new TimeOnly(13, 0)
                    : time < new TimeOnly(15, 0) || time >= new TimeOnly(18, 0),

            // Social: no golf privileges
            MembershipCategory.Social => false,

            _ => false
        };
    }
}
