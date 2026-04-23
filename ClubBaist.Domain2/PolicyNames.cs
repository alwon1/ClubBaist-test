namespace ClubBaist.Domain2;

/// <summary>
/// Named authorization policy constants. Reference these in [Authorize(Policy = ...)] attributes.
/// All policies are registered in Program.cs.
/// </summary>
public static class PolicyNames
{
    /// <summary>Requires the Admin role.</summary>
    public const string Admin = "Policy.Admin";

    /// <summary>Requires Admin or MembershipCommittee role.</summary>
    public const string AdminOrCommittee = "Policy.AdminOrCommittee";

    /// <summary>Requires Admin or Clerk role.</summary>
    public const string AdminOrClerk = "Policy.AdminOrClerk";

    /// <summary>Requires Admin or ProShopStaff role.</summary>
    public const string AdminOrProShop = "Policy.AdminOrProShop";

    /// <summary>Requires the Member role (any tier).</summary>
    public const string MemberAny = "Policy.MemberAny";

    /// <summary>Requires the Member role and the standing-tee-time.book permission claim.</summary>
    public const string MemberWithStandingBooking = "Policy.MemberWithStandingBooking";

    /// <summary>Requires the Member role and the shareholder membership-fact claim.</summary>
    public const string ShareholderMember = "Policy.ShareholderMember";
}
