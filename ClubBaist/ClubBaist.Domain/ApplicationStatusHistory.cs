using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class ApplicationStatusHistory<TKey> where TKey : IEquatable<TKey>
{
    public Guid ApplicationStatusHistoryId { get; set; } = Guid.NewGuid();
    public Guid MembershipApplicationId { get; set; }
    public MembershipApplication<TKey>? MembershipApplication { get; set; }
    public ApplicationStatus FromStatus
    {
        get;
        set
        {
            if (value == ToStatus)
            {
                throw new ArgumentException("FromStatus and ToStatus must be different for a transition record.", nameof(FromStatus));
            }
            field = value;
        }
    }

    public ApplicationStatus ToStatus
    {
        get;
        set
        {
            if (value == FromStatus)
            {
                throw new ArgumentException("FromStatus and ToStatus must be different for a transition record.", nameof(ToStatus));
            }
            field = value;
        }
    }

    public TKey ChangedByUserId { get; set; } = default!;
    public IdentityUser<TKey>? ChangedByUser { get; set; }
    public DateTime ChangedAt { get; set; }

    public ApplicationStatusHistory()
    {
    }

    public ApplicationStatusHistory(
        Guid membershipApplicationId,
        ApplicationStatus fromStatus,
        ApplicationStatus toStatus,
        TKey changedByUserId,
        DateTime changedAt,
        Guid? applicationStatusHistoryId = null)
    {
        ApplicationStatusHistoryId = applicationStatusHistoryId ?? Guid.NewGuid();
        MembershipApplicationId = membershipApplicationId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ChangedByUserId = changedByUserId;
        ChangedAt = changedAt;
    }

    public void AttachMembershipApplication(MembershipApplication<TKey> membershipApplication)
    {
        MembershipApplication = membershipApplication ?? throw new ArgumentNullException(nameof(membershipApplication));
        MembershipApplicationId = membershipApplication.ApplicationId;
    }

    public void AttachChangedByUser(IdentityUser<TKey> changedByUser)
    {
        ChangedByUser = changedByUser ?? throw new ArgumentNullException(nameof(changedByUser));
        ChangedByUserId = changedByUser.Id;
    }
}