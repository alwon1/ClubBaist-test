using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services2.Membership;

public class MembershipLevelService(IAppDbContext2 db)
{
    public async Task<bool> CreateMembershipLevelAsync(string name, string shortCode)
    {
        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var trans = await db.BeginTransactionAsync(isolationLevel: System.Data.IsolationLevel.Snapshot);
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
        });
    }

    public async Task<bool> UpdateMembershipLevelAsync(MembershipLevel membershipLevel) =>
        await UpdateMembershipLevelAsync(membershipLevel.Id, membershipLevel.Name, membershipLevel.ShortCode);

    public async Task<bool> UpdateMembershipLevelAsync(int id, string name, string shortCode)
    {
        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var trans = await db.BeginTransactionAsync(isolationLevel: System.Data.IsolationLevel.Snapshot);
            try
            {
                var membershipLevel = await db.MembershipLevels.FindAsync(id);
                if (membershipLevel == null)
                {
                    await trans.RollbackAsync();
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
        });
    }
}
