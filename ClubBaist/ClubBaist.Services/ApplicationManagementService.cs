using ClubBaist.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class ApplicationManagementService<TKey> where TKey : IEquatable<TKey>
{
    private static readonly ApplicationStatus[] ActionableStatuses =
    [
        ApplicationStatus.Submitted,
        ApplicationStatus.OnHold,
        ApplicationStatus.Waitlisted
    ];

    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly UserManager<IdentityUser<TKey>> _userManager;
    private readonly MemberManagementService<TKey> _memberManagementService;

    public ApplicationManagementService(
        IApplicationDbContext<TKey> dbContext,
        UserManager<IdentityUser<TKey>> userManager,
        MemberManagementService<TKey> memberManagementService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _memberManagementService = memberManagementService ?? throw new ArgumentNullException(nameof(memberManagementService));
    }

    public async Task<MembershipApplication<TKey>> SubmitApplicationAsync(
        SubmitApplicationRequest<TKey> submitRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submitRequest);
        EnsureRequiredKey(submitRequest.ApplicationUserId, nameof(submitRequest.ApplicationUserId));
        EnsureRequiredKey(submitRequest.Sponsor1MemberId, nameof(submitRequest.Sponsor1MemberId));
        EnsureRequiredKey(submitRequest.Sponsor2MemberId, nameof(submitRequest.Sponsor2MemberId));

        await EnsureIdentityUserExistsAsync(submitRequest.ApplicationUserId, cancellationToken);

        var submittedAt = submitRequest.SubmittedAt ?? DateTime.UtcNow;

        var membershipApplication = MembershipApplication<TKey>.Submit(
            submitRequest.ApplicationUserId,
            submitRequest.FirstName,
            submitRequest.LastName,
            submitRequest.Occupation,
            submitRequest.CompanyName,
            submitRequest.Address,
            submitRequest.PostalCode,
            submitRequest.Phone,
            submitRequest.Email,
            submitRequest.DateOfBirth,
            submitRequest.RequestedMembershipCategory,
            submitRequest.Sponsor1MemberId,
            submitRequest.Sponsor2MemberId,
            submittedAt,
            submitRequest.AlternatePhone,
            submitRequest.ApplicationId);

        _dbContext.MembershipApplications.Add(membershipApplication);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return membershipApplication;
    }

    public async Task<IReadOnlyList<MembershipApplication<TKey>>> GetActionableApplicationsAsync(
        ActionableApplicationFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ActionableApplicationFilter();

        IQueryable<MembershipApplication<TKey>> query = _dbContext.MembershipApplications.AsNoTracking();

        query = query.Where(application => ActionableStatuses.Contains(application.CurrentStatus));

        if (filter.Status is not null)
        {
            query = query.Where(application => application.CurrentStatus == filter.Status.Value);
        }

        if (filter.SubmittedFrom is not null)
        {
            query = query.Where(application => application.SubmittedAt >= filter.SubmittedFrom.Value);
        }

        if (filter.SubmittedTo is not null)
        {
            query = query.Where(application => application.SubmittedAt <= filter.SubmittedTo.Value);
        }

        query = query.OrderBy(application => application.SubmittedAt);

        if (filter.PageNumber is not null && filter.PageSize is not null && filter.PageNumber > 0 && filter.PageSize > 0)
        {
            var skip = (filter.PageNumber.Value - 1) * filter.PageSize.Value;
            query = query.Skip(skip).Take(filter.PageSize.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<ChangeApplicationStatusResult<TKey>> ChangeApplicationStatusAsync(
        Guid applicationId,
        ApplicationStatus newStatus,
        TKey changedByUserId,
        DateTime changedAt,
        CancellationToken cancellationToken = default)
    {
        EnsureRequiredKey(changedByUserId, nameof(changedByUserId));
        await EnsureIdentityUserExistsAsync(changedByUserId, cancellationToken);

        var application = await _dbContext.MembershipApplications
            .FirstOrDefaultAsync(item => item.ApplicationId == applicationId, cancellationToken);

        if (application is null)
        {
            throw new KeyNotFoundException($"Membership application '{applicationId}' was not found.");
        }

        var history = application.ChangeStatus(newStatus, changedByUserId, changedAt);
        _dbContext.ApplicationStatusHistories.Add(history);

        CreateMemberResult? memberCreationResult = null;
        if (newStatus == ApplicationStatus.Accepted)
        {
            var createMemberRequest = new CreateMemberRequest<TKey>(
                application.ApplicationUserId,
                application.FirstName,
                application.LastName,
                application.DateOfBirth,
                application.Email,
                application.Phone,
                application.Address,
                application.PostalCode,
                application.RequestedMembershipCategory,
                true,
                application.AlternatePhone,
                changedAt);

            memberCreationResult = await _memberManagementService.CreateMemberAsync(
                createMemberRequest,
                cancellationToken);
        }
        else
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ChangeApplicationStatusResult<TKey>(
            application.ApplicationId,
            application.ApplicationUserId,
            application.CurrentStatus,
            application.LastStatusChangedAt,
            memberCreationResult);
    }

    public async Task<ApplicationStatusHistory<TKey>> RecordStatusHistoryAsync(
        Guid applicationId,
        ApplicationStatus fromStatus,
        ApplicationStatus toStatus,
        TKey changedByUserId,
        DateTime changedAt,
        CancellationToken cancellationToken = default)
    {
        EnsureRequiredKey(changedByUserId, nameof(changedByUserId));

        var history = new ApplicationStatusHistory<TKey>
        {
            MembershipApplicationId = applicationId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ChangedByUserId = changedByUserId,
            ChangedAt = changedAt
        };

        _dbContext.ApplicationStatusHistories.Add(history);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return history;
    }

    private async Task EnsureIdentityUserExistsAsync(TKey userId, CancellationToken cancellationToken)
    {
        var exists = await _userManager.Users.AnyAsync(
            user => user.Id!.Equals(userId),
            cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("The linked application user does not exist.");
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

public sealed record SubmitApplicationRequest<TKey>(
    TKey ApplicationUserId,
    string FirstName,
    string LastName,
    string Occupation,
    string CompanyName,
    string Address,
    string PostalCode,
    string Phone,
    string Email,
    DateTime DateOfBirth,
    MembershipCategory RequestedMembershipCategory,
    TKey Sponsor1MemberId,
    TKey Sponsor2MemberId,
    string? AlternatePhone = null,
    DateTime? SubmittedAt = null,
    Guid? ApplicationId = null)
    where TKey : IEquatable<TKey>;

public sealed record ActionableApplicationFilter(
    ApplicationStatus? Status = null,
    DateTime? SubmittedFrom = null,
    DateTime? SubmittedTo = null,
    int? PageNumber = null,
    int? PageSize = null);

public sealed record ChangeApplicationStatusResult<TKey>(
    Guid ApplicationId,
    TKey ApplicationUserId,
    ApplicationStatus CurrentStatus,
    DateTime LastStatusChangedAt,
    CreateMemberResult? MemberCreationResult)
    where TKey : IEquatable<TKey>;