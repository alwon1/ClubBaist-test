using System;
using System.ClientModel;
using System.Security.Claims;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Diagnostics;
namespace ClubBaist.Services2;

public class MembershipService(AppDbContext db)
{
    public async Task<MembershipLevel?> GetMembershipLevelForUserAsync(ClubBaistUser user) => await GetMembershipLevelForUserAsync(user.Id);
    public async Task<MembershipLevel?> GetMembershipLevelForUserAsync(Guid userId) =>
             await db.MemberShips
                 .Where(x => x.User.Id == userId)
                 .Select(x => x.MembershipLevel).AsNoTracking().FirstOrDefaultAsync();
    public async Task<bool> SetMembershipLevelForUserAsync(ClubBaistUser user, int membershipLevelId)
    {
        var membershipLevel = db.MembershipLevels.Find(membershipLevelId);
        Guard.IsNotNull(membershipLevel);
        var membership = db.MemberShips.FirstOrDefault(x => x.User.Id == user.Id);
        if (membership == null)
        {
            return false;
        }
        membership.MembershipLevel = membershipLevel;
        await db.SaveChangesAsync();
        return true;
    }
}
