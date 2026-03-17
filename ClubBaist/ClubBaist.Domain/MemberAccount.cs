using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class MemberAccount<TKey> where TKey : IEquatable<TKey>
{
    public int MemberAccountId { get; set; }
    public TKey ApplicationUserId { get; set; } = default!;
    public IdentityUser<TKey>? ApplicationUser { get; set; }
    public int MemberNumber { get; set; }
    public string FirstName
    {
        get;
        set => field = RequireText(value, nameof(FirstName));
    } = string.Empty;

    public string LastName
    {
        get;
        set => field = RequireText(value, nameof(LastName));
    } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    public string Email
    {
        get;
        set => field = RequireText(value, nameof(Email));
    } = string.Empty;

    public string Phone
    {
        get;
        set => field = RequireText(value, nameof(Phone));
    } = string.Empty;

    public string? AlternatePhone
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string Address
    {
        get;
        set => field = RequireText(value, nameof(Address));
    } = string.Empty;

    public string PostalCode
    {
        get;
        set => field = RequireText(value, nameof(PostalCode));
    } = string.Empty;

    public MembershipCategory MembershipCategory { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; private set; }

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
        FirstName = firstName;
        LastName = lastName;
        DateOfBirth = dateOfBirth;
        Email = email;
        Phone = phone;
        AlternatePhone = alternatePhone;
        Address = address;
        PostalCode = postalCode;
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

}