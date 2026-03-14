using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class MemberAccount<TKey> where TKey : IEquatable<TKey>
{
    public Guid MemberAccountId { get; private set; }
    public TKey ApplicationUserId { get; private set; }
    public IdentityUser<TKey>? ApplicationUser { get; private set; }
    public string MemberNumber { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    public string Email { get; private set; }
    public string Phone { get; private set; }
    public string? AlternatePhone { get; private set; }
    public string Address { get; private set; }
    public string PostalCode { get; private set; }
    public MembershipCategory MembershipCategory { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public MemberAccount(
        TKey applicationUserId,
        string memberNumber,
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        string email,
        string phone,
        string address,
        string postalCode,
        MembershipCategory membershipCategory,
        DateTime createdAt,
        bool isActive = true,
        string? alternatePhone = null,
        Guid? memberAccountId = null)
    {
        MemberAccountId = memberAccountId ?? Guid.NewGuid();
        ApplicationUserId = applicationUserId;
        MemberNumber = RequireText(memberNumber, nameof(memberNumber));
        FirstName = RequireText(firstName, nameof(firstName));
        LastName = RequireText(lastName, nameof(lastName));
        DateOfBirth = dateOfBirth;
        Email = RequireText(email, nameof(email));
        Phone = RequireText(phone, nameof(phone));
        AlternatePhone = NormalizeOptionalText(alternatePhone);
        Address = RequireText(address, nameof(address));
        PostalCode = RequireText(postalCode, nameof(postalCode));
        MembershipCategory = membershipCategory;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public void UpdateProfile(
        string firstName,
        string lastName,
        DateTime dateOfBirth,
        string email,
        string phone,
        string address,
        string postalCode,
        MembershipCategory membershipCategory,
        DateTime updatedAt,
        string? alternatePhone = null)
    {
        FirstName = RequireText(firstName, nameof(firstName));
        LastName = RequireText(lastName, nameof(lastName));
        DateOfBirth = dateOfBirth;
        Email = RequireText(email, nameof(email));
        Phone = RequireText(phone, nameof(phone));
        AlternatePhone = NormalizeOptionalText(alternatePhone);
        Address = RequireText(address, nameof(address));
        PostalCode = RequireText(postalCode, nameof(postalCode));
        MembershipCategory = membershipCategory;
        UpdatedAt = updatedAt;
    }

    public void SetActive(bool isActive, DateTime updatedAt)
    {
        IsActive = isActive;
        UpdatedAt = updatedAt;
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