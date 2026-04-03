using System;
using ClubBaist.Domain2;
using Microsoft.IdentityModel.Tokens;

namespace ClubBaist.Services2.Membership;

public class MembershipLevelService(AppDbContext db)
{
    public async Task<bool> CreateMembershipLevelAsync(string name, string shortCode)
    {
        var trans = await db.BeginTransactionAsync(isolationLevel: System.Data.IsolationLevel.Snapshot);
        try
        {
            var membershipLevel = new MembershipLevel
            {
                Name = name,
                ShortCode = shortCode
            };
            db.MembershipLevels.Add(membershipLevel);
            await db.SaveChangesAsync();
            await trans.CommitAsync();
            return true;
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateMembershipLevelAsync(MembershipLevel membershipLevel) => await UpdateMembershipLevelAsync(membershipLevel.Id, membershipLevel.Name, membershipLevel.ShortCode);
    public async Task<bool> UpdateMembershipLevelAsync(int id, string name, string shortCode)
    {
        var trans = await db.BeginTransactionAsync(isolationLevel: System.Data.IsolationLevel.Snapshot);
        try
        {
            var membershipLevel = await db.MembershipLevels.FindAsync(id);
            if (membershipLevel == null)
            {
                return false;
            }
            membershipLevel.Name = name;
            membershipLevel.ShortCode = shortCode;
            await db.SaveChangesAsync();
            await trans.CommitAsync();
            return true;
        }
        catch
        {
            await trans.RollbackAsync();
            throw;
        }
    }
}
