namespace ClubBaist.Domain;

public static class MembershipApplicationExtensions
{
    public static ApplicationStatusHistory<TKey> RecordStatusChange<TKey>(
        this MembershipApplication<TKey> membershipApplication,
        ApplicationStatus newStatus,
        TKey changedByUserId,
        DateTime changedAt)
        where TKey : IEquatable<TKey>
    {
        if (membershipApplication is null)
        {
            throw new ArgumentNullException(nameof(membershipApplication));
        }

        return new ApplicationStatusHistory<TKey>
        {
            MembershipApplicationId = membershipApplication.ApplicationId,
            FromStatus = membershipApplication.CurrentStatus,
            ToStatus = newStatus,
            ChangedByUserId = changedByUserId,
            ChangedAt = changedAt
        };
    }
}