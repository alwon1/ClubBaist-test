using ClubBaist.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createMemberRequest);
        EnsureRequiredKey(createMemberRequest.ApplicationUserId, nameof(createMemberRequest.ApplicationUserId));
        EnsureRequiredText(createMemberRequest.FirstName, nameof(createMemberRequest.FirstName));
        EnsureRequiredText(createMemberRequest.LastName, nameof(createMemberRequest.LastName));
        EnsureRequiredText(createMemberRequest.Email, nameof(createMemberRequest.Email));
        EnsureRequiredText(createMemberRequest.Phone, nameof(createMemberRequest.Phone));
        EnsureRequiredText(createMemberRequest.Address, nameof(createMemberRequest.Address));
        EnsureRequiredText(createMemberRequest.PostalCode, nameof(createMemberRequest.PostalCode));

        await EnsureIdentityUserExistsAsync(createMemberRequest.ApplicationUserId, cancellationToken);

        var createdAt = createMemberRequest.CreatedAt ?? DateTime.UtcNow;
        var memberNumber = await GenerateUniqueMemberNumberAsync(cancellationToken);

        var memberAccount = new MemberAccount<TKey>
        {
            ApplicationUserId = createMemberRequest.ApplicationUserId,
            MemberNumber = memberNumber,
            FirstName = createMemberRequest.FirstName,
            LastName = createMemberRequest.LastName,
            DateOfBirth = createMemberRequest.DateOfBirth,
            Email = createMemberRequest.Email,
            Phone = createMemberRequest.Phone,
            AlternatePhone = createMemberRequest.AlternatePhone,
            Address = createMemberRequest.Address,
            PostalCode = createMemberRequest.PostalCode,
            MembershipCategory = createMemberRequest.MembershipCategory,
            IsActive = createMemberRequest.IsActive,
            CreatedAt = createdAt
        };

        _dbContext.MemberAccounts.Add(memberAccount);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (await HasExistingMemberAccountAsync(createMemberRequest.ApplicationUserId, cancellationToken))
            {
                throw new InvalidOperationException("A member account already exists for this application user.", ex);
            }

            throw;
        }

        return new CreateMemberResult(memberAccount.MemberAccountId, memberAccount.MemberNumber, memberAccount.CreatedAt);
    }

    private Task<bool> HasExistingMemberAccountAsync(TKey applicationUserId, CancellationToken cancellationToken)
    {
        return _dbContext.MemberAccounts.AnyAsync(
            member => member.ApplicationUserId!.Equals(applicationUserId),
            cancellationToken);
    }

    private async Task EnsureIdentityUserExistsAsync(TKey applicationUserId, CancellationToken cancellationToken)
    {
        var exists = await _userManager.Users.AnyAsync(
            user => user.Id!.Equals(applicationUserId),
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

        if (typeof(TKey).IsValueType && EqualityComparer<TKey>.Default.Equals(key, default!))
        {
            throw new ArgumentException("Value is required.", paramName);
        }
    }

    private static void EnsureRequiredText(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
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
