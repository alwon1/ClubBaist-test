using System.Security.Claims;

namespace ClubBaist.Domain;

/// <summary>
/// Identity role name constants shared across domain, services, and web layers.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string MembershipCommittee = "MembershipCommittee";
    public const string Member = "Member";
    public const string Clerk = "Clerk";
    public const string ProShopStaff = "ProShopStaff";

    /// <summary>Admin or MembershipCommittee (comma-separated for Blazor Roles attribute).</summary>
    public const string AdminOrCommittee = $"{Admin},{MembershipCommittee}";

    /// <summary>Admin or Member (comma-separated for Blazor Roles attribute).</summary>
    public const string AdminOrMember = $"{Admin},{Member}";

    /// <summary>Admin or Clerk (comma-separated for Blazor Roles attribute).</summary>
    public const string AdminOrClerk = $"{Admin},{Clerk}";

    /// <summary>Admin or ProShopStaff (comma-separated for Blazor Roles attribute).</summary>
    public const string AdminOrProShopStaff = $"{Admin},{ProShopStaff}";

    public static class ClaimTypes
    {
        public const string Permission = "clubbaist.permission";
    }

    public static class Permissions
    {
        public const string BookStandingTeeTime = "standing-tee-time.book";
    }

    public static class Claims
    {
        public static Claim StandingTeeTimeBooking { get; } =
            new(ClaimTypes.Permission, Permissions.BookStandingTeeTime);
    }
}
