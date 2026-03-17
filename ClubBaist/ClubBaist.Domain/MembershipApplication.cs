using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class MembershipApplication<TKey> where TKey : IEquatable<TKey>
{
    private readonly List<ApplicationStatusHistory<TKey>> _statusHistory = [];

    public Guid ApplicationId { get; private set; } = Guid.NewGuid();
    public TKey ApplicationUserId { get; private set; } = default!;
    public IdentityUser<TKey>? ApplicationUser { get; private set; }
    public ApplicationStatus CurrentStatus { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime LastStatusChangedAt { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Occupation { get; private set; } = string.Empty;
    public string CompanyName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? AlternatePhone { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public DateTime DateOfBirth { get; private set; }
    public MembershipCategory RequestedMembershipCategory { get; private set; }
    public int Sponsor1MemberId { get; private set; }
    public int Sponsor2MemberId { get; private set; }

    public IReadOnlyList<ApplicationStatusHistory<TKey>> StatusHistory => _statusHistory;

    public MembershipApplication()
    {
    }

    public static MembershipApplication<TKey> Submit(
        TKey applicationUserId,
        string firstName,
        string lastName,
        string occupation,
        string companyName,
        string address,
        string postalCode,
        string phone,
        string email,
        DateTime dateOfBirth,
        MembershipCategory requestedMembershipCategory,
        int sponsor1MemberId,
        int sponsor2MemberId,
        DateTime submittedAt,
        string? alternatePhone = null,
        Guid? applicationId = null)
    {
        var membershipApplication = new MembershipApplication<TKey>
        {
            ApplicationUserId = applicationUserId,
            FirstName = RequireText(firstName, nameof(firstName)),
            LastName = RequireText(lastName, nameof(lastName)),
            Occupation = RequireText(occupation, nameof(occupation)),
            CompanyName = RequireText(companyName, nameof(companyName)),
            Address = RequireText(address, nameof(address)),
            PostalCode = RequireText(postalCode, nameof(postalCode)),
            Phone = RequireText(phone, nameof(phone)),
            Email = RequireText(email, nameof(email)),
            AlternatePhone = NormalizeOptionalText(alternatePhone),
            DateOfBirth = dateOfBirth,
            RequestedMembershipCategory = requestedMembershipCategory,
            Sponsor1MemberId = sponsor1MemberId,
            Sponsor2MemberId = sponsor2MemberId,
            CurrentStatus = ApplicationStatus.Submitted,
            SubmittedAt = submittedAt,
            LastStatusChangedAt = submittedAt
        };

        if (applicationId.HasValue)
        {
            membershipApplication.ApplicationId = applicationId.Value;
        }

        return membershipApplication;
    }

    public bool CanTransitionTo(ApplicationStatus newStatus)
    {
        return CurrentStatus switch
        {
            ApplicationStatus.Submitted =>
                newStatus is ApplicationStatus.OnHold or ApplicationStatus.Waitlisted or ApplicationStatus.Accepted or ApplicationStatus.Denied,
            ApplicationStatus.OnHold =>
                newStatus is ApplicationStatus.OnHold or ApplicationStatus.Waitlisted or ApplicationStatus.Accepted or ApplicationStatus.Denied,
            ApplicationStatus.Waitlisted =>
                newStatus is ApplicationStatus.Waitlisted or ApplicationStatus.OnHold or ApplicationStatus.Accepted or ApplicationStatus.Denied,
            ApplicationStatus.Accepted => false,
            ApplicationStatus.Denied => false,
            _ => false
        };
    }

    public ApplicationStatusHistory<TKey> ChangeStatus(ApplicationStatus newStatus, TKey changedByUserId, DateTime changedAt)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new InvalidOperationException($"Status transition from {CurrentStatus} to {newStatus} is not allowed.");
        }

        if (CurrentStatus == newStatus)
        {
            throw new InvalidOperationException("A status transition must change the current status.");
        }

        var history = this.RecordStatusChange(newStatus, changedByUserId, changedAt);
        _statusHistory.Add(history);

        CurrentStatus = newStatus;
        LastStatusChangedAt = changedAt;

        return history;
    }

    public void AttachApplicationUser(IdentityUser<TKey> applicationUser)
    {
        ApplicationUser = applicationUser ?? throw new ArgumentNullException(nameof(applicationUser));
        ApplicationUserId = applicationUser.Id;
    }

    private static string RequireText(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", paramName)
            : value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}