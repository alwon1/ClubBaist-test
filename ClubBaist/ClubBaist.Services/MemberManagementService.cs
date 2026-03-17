using ClubBaist.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class MemberManagementService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public MemberManagementService(
        IApplicationDbContext<TKey> dbContext,
        UserManager<ApplicationUser> userManager)
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
            DateOfBirth = createMemberRequest.DateOfBirth,
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

        // Update ApplicationUser with member's name and phone
        var identityUser = await _userManager.FindByIdAsync(createMemberRequest.ApplicationUserId.ToString()!);
        if (identityUser is not null)
        {
            identityUser.FirstName = createMemberRequest.FirstName;
            identityUser.LastName = createMemberRequest.LastName;
            identityUser.Phone = createMemberRequest.Phone;
            await _userManager.UpdateAsync(identityUser);

            if (!await _userManager.IsInRoleAsync(identityUser, AppRoles.Member))
            {
                await _userManager.AddToRoleAsync(identityUser, AppRoles.Member);
            }
        }

        return new CreateMemberResult(memberAccount.MemberAccountId, memberAccount.MemberNumber, memberAccount.CreatedAt);
    }

    public async Task<MemberAccount<TKey>> UpdateMemberAsync(
        UpdateMemberRequest<TKey> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var member = await _dbContext.MemberAccounts
            .FirstOrDefaultAsync(m => m.MemberAccountId == request.MemberAccountId, cancellationToken)
            ?? throw new InvalidOperationException("Member not found.");

        var now = DateTime.UtcNow;
        member.UpdateProfile(
            request.DateOfBirth,
            request.Address,
            request.PostalCode,
            request.MembershipCategory,
            now,
            request.AlternatePhone);

        member.SetActive(request.IsActive, now);

        // Update ApplicationUser with name and phone
        var identityUser = await _userManager.FindByIdAsync(member.ApplicationUserId.ToString()!);
        if (identityUser is not null)
        {
            identityUser.FirstName = request.FirstName;
            identityUser.LastName = request.LastName;
            identityUser.Phone = request.Phone;
            await _userManager.UpdateAsync(identityUser);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return member;
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
            user => user.Id.Equals(applicationUserId),
            cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("The linked application user does not exist.");
        }
    }

    private async Task<int> GenerateUniqueMemberNumberAsync(CancellationToken cancellationToken)
    {
        var maxNumber = await _dbContext.MemberAccounts
            .AsNoTracking()
            .MaxAsync(m => (int?)m.MemberNumber, cancellationToken) ?? 9999;

        return maxNumber + 1;
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
    string Phone,
    string Address,
    string PostalCode,
    MembershipCategory MembershipCategory,
    bool IsActive = true,
    string? AlternatePhone = null,
    DateTime? CreatedAt = null)
    where TKey : IEquatable<TKey>;

public sealed record CreateMemberResult(
    int MemberAccountId,
    int MemberNumber,
    DateTime CreatedAt);

public sealed record UpdateMemberRequest<TKey>(
    int MemberAccountId,
    string FirstName,
    string LastName,
    DateTime DateOfBirth,
    string Phone,
    string Address,
    string PostalCode,
    MembershipCategory MembershipCategory,
    bool IsActive,
    string? AlternatePhone = null)
    where TKey : IEquatable<TKey>;
