namespace ClubBaist.Domain;

public class MemberAccount<TKey> where TKey : IEquatable<TKey>
{
    public int MemberAccountId { get; set; }
    public TKey ApplicationUserId { get; set; } = default!;
    public ApplicationUser? ApplicationUser { get; set; }
    public int MemberNumber { get; set; }

    public DateTime DateOfBirth { get; set; }

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
        DateTime dateOfBirth,
        string address,
        string postalCode,
        MembershipCategory membershipCategory,
        DateTime updatedAt,
        string? alternatePhone = null)
    {
        DateOfBirth = dateOfBirth;
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

    public void AttachApplicationUser(ApplicationUser applicationUser)
    {
        ApplicationUser = applicationUser ?? throw new ArgumentNullException(nameof(applicationUser));
        ApplicationUserId = (TKey)(object)applicationUser.Id;
    }

    private static string RequireText(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", paramName)
            : value.Trim();
    }
}
