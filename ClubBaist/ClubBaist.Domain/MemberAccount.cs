using Microsoft.AspNetCore.Identity;

namespace ClubBaist.Domain;

public class MemberAccount<TKey> where TKey : IEquatable<TKey>
{
    public Guid MemberAccountId { get; set; } = Guid.NewGuid();
    public TKey ApplicationUserId { get; set; } = default!;
    public IdentityUser<TKey>? ApplicationUser { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string FirstName
    {
        get;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", nameof(FirstName));
            }
            field = value.Trim();
        }
    } = string.Empty;

    public string LastName
    {
        get;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", nameof(LastName));
            }
            field = value.Trim();
        }
    } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    public string Email
    {
        get;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", nameof(Email));
            }
            field = value.Trim();
        }
    } = string.Empty;

    public string Phone
    {
        get;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", nameof(Phone));
            }
            field = value.Trim();
        }
    } = string.Empty;

    public string? AlternatePhone
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public string Address
    {
        get;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", nameof(Address));
            }
            field = value.Trim();
        }
    } = string.Empty;

    public string PostalCode
    {
        get;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value is required.", nameof(PostalCode));
            }
            field = value.Trim();
        }
    } = string.Empty;

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

}