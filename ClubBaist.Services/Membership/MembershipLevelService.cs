using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services.Membership;

public class MembershipLevelService(AppDbContext db)
{
    public async Task<bool> CreateMembershipLevelAsync(
        string name,
        string shortCode,
        MemberType memberType = MemberType.Associate,
        decimal annualFee = 0,
        int? minimumAge = null,
        int? maximumAge = null)
    {
        if (annualFee < 0)
            throw new ArgumentOutOfRangeException(nameof(annualFee), "Annual fee cannot be negative.");
        if (minimumAge.HasValue && maximumAge.HasValue && minimumAge.Value > maximumAge.Value)
            throw new ArgumentException("Minimum age cannot be greater than maximum age.", nameof(minimumAge));

        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var trans = await db.BeginTransactionAsync(isolationLevel: System.Data.IsolationLevel.Snapshot);
            try
            {
                var membershipLevel = new MembershipLevel
                {
                    Name = name,
                    ShortCode = shortCode,
                    MemberType = memberType,
                    AnnualFee = annualFee,
                    MinimumAge = minimumAge,
                    MaximumAge = maximumAge
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
        await UpdateMembershipLevelAsync(
            membershipLevel.Id,
            membershipLevel.Name,
            membershipLevel.ShortCode,
            membershipLevel.MemberType,
            membershipLevel.AnnualFee,
            membershipLevel.MinimumAge,
            membershipLevel.MaximumAge);

    public async Task<bool> UpdateMembershipLevelAsync(
        int id,
        string name,
        string shortCode,
        MemberType memberType = MemberType.Associate,
        decimal annualFee = 0,
        int? minimumAge = null,
        int? maximumAge = null)
    {
        if (annualFee < 0)
            throw new ArgumentOutOfRangeException(nameof(annualFee), "Annual fee cannot be negative.");
        if (minimumAge.HasValue && maximumAge.HasValue && minimumAge.Value > maximumAge.Value)
            throw new ArgumentException("Minimum age cannot be greater than maximum age.", nameof(minimumAge));

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
                membershipLevel.MemberType = memberType;
                membershipLevel.AnnualFee = annualFee;
                membershipLevel.MinimumAge = minimumAge;
                membershipLevel.MaximumAge = maximumAge;
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
