using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class MemberAccount<TKey> where TKey : IEquatable<TKey>
{
    public Guid MemberAccountId { get; set; } = Guid.NewGuid();
    public TKey ApplicationUserId { get; set; } = default!;
    public IdentityUser<TKey>? ApplicationUser { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public MembershipCategory MembershipCategory { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public MemberAccount()
    {
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