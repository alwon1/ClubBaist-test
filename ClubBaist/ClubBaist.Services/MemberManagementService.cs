using ClubBaist.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class MemberManagementService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly UserManager<IdentityUser<TKey>> _userManager;

    public MemberManagementService(
        IApplicationDbContext<TKey> dbContext,
        UserManager<IdentityUser<TKey>> userManager)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<CreateMemberResult> CreateMemberAsync(
        CreateMemberRequest<TKey> createMemberRequest,
        TKey createdByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createMemberRequest);
        EnsureRequiredKey(createMemberRequest.ApplicationUserId, nameof(createMemberRequest.ApplicationUserId));
        EnsureRequiredKey(createdByUserId, nameof(createdByUserId));

        await EnsureIdentityUserExistsAsync(createMemberRequest.ApplicationUserId, cancellationToken);

        var hasExistingMember = await _dbContext.MemberAccounts
            .AnyAsync(
                member => EqualityComparer<TKey>.Default.Equals(member.ApplicationUserId, createMemberRequest.ApplicationUserId),
                cancellationToken);

        if (hasExistingMember)
        {
            throw new InvalidOperationException("A member account already exists for this application user.");
        }

        var createdAt = createMemberRequest.CreatedAt ?? DateTime.UtcNow;
        var memberNumber = await GenerateUniqueMemberNumberAsync(cancellationToken);

        var memberAccount = new MemberAccount<TKey>(
            createMemberRequest.ApplicationUserId,
            memberNumber,
            createMemberRequest.FirstName,
            createMemberRequest.LastName,
            createMemberRequest.DateOfBirth,
            createMemberRequest.Email,
            createMemberRequest.Phone,
            createMemberRequest.Address,
            createMemberRequest.PostalCode,
            createMemberRequest.MembershipCategory,
            createdAt,
            createMemberRequest.IsActive,
            createMemberRequest.AlternatePhone);

        _dbContext.MemberAccounts.Add(memberAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateMemberResult(memberAccount.MemberAccountId, memberAccount.MemberNumber, memberAccount.CreatedAt);
    }

    private async Task EnsureIdentityUserExistsAsync(TKey applicationUserId, CancellationToken cancellationToken)
    {
        var exists = await _userManager.Users.AnyAsync(
            user => EqualityComparer<TKey>.Default.Equals(user.Id, applicationUserId),
            cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("The linked application user does not exist.");
        }
    }

    private async Task<string> GenerateUniqueMemberNumberAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var candidate = $"MBR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
            var exists = await _dbContext.MemberAccounts.AnyAsync(
                member => member.MemberNumber == candidate,
                cancellationToken);

            if (!exists)
            {
                return candidate;
            }
        }
    }

    private static void EnsureRequiredKey(TKey key, string paramName)
    {
        if (key is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (key is string text && string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Value is required.", paramName);
        }
    }
}

public sealed record CreateMemberRequest<TKey>(
    TKey ApplicationUserId,
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string Email,
    string Phone,
    string Address,
    string PostalCode,
    MembershipCategory MembershipCategory,
    bool IsActive = true,
    string? AlternatePhone = null,
    DateTime? CreatedAt = null)
    where TKey : IEquatable<TKey>;

public sealed record CreateMemberResult(
    Guid MemberAccountId,
    string MemberNumber,
    DateTime CreatedAt);
