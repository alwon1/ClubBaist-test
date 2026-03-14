using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class ApplicationStatusHistory<TKey> where TKey : IEquatable<TKey>
{
    public Guid ApplicationStatusHistoryId { get; private set; }
    public Guid MembershipApplicationId { get; private set; }
    public MembershipApplication<TKey>? MembershipApplication { get; private set; }
    public ApplicationStatus FromStatus { get; private set; }
    public ApplicationStatus ToStatus { get; private set; }
    public TKey ChangedByUserId { get; private set; }
    public IdentityUser<TKey>? ChangedByUser { get; private set; }
    public DateTime ChangedAt { get; private set; }

    public ApplicationStatusHistory(
        Guid membershipApplicationId,
        ApplicationStatus fromStatus,
        ApplicationStatus toStatus,
        TKey changedByUserId,
        DateTime changedAt,
        Guid? applicationStatusHistoryId = null)
    {
        if (fromStatus == toStatus)
        {
            throw new ArgumentException("FromStatus and ToStatus must be different for a transition record.", nameof(toStatus));
        }

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