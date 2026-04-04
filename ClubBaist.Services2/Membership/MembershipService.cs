using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services2;

public class MembershipService(IAppDbContext2 db)
{
    public Task<MembershipLevel?> GetMembershipLevelForUserAsync(ClubBaistUser user) =>
        GetMembershipLevelForUserAsync(user.Id);

    public async Task<MembershipLevel?> GetMembershipLevelForUserAsync(Guid userId) =>
        await db.MemberShips
            .Where(x => x.UserId == userId)
            .Select(x => x.MembershipLevel)
            .AsNoTracking()
            .FirstOrDefaultAsync();

    public async Task<bool> SetMembershipLevelForUserAsync(ClubBaistUser user, int membershipLevelId)
    {
        var membershipLevel = await db.MembershipLevels.FindAsync(membershipLevelId);
        if (membershipLevel is null)
            return false;

        var membership = await db.MemberShips.FirstOrDefaultAsync(x => x.UserId == user.Id);
        if (membership is null)
        {
            return false;
        }

        membership.MembershipLevelId = membershipLevelId;
        await db.SaveChangesAsync();
        return true;
    }
}
