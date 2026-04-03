namespace ClubBaist.Domain2;

/// <summary>
/// Identity role name constants shared across domain, services, and web layers.
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string MembershipCommittee = "MembershipCommittee";
    public const string Member = "Member";

    /// <summary>Admin or MembershipCommittee (comma-separated for Blazor Roles attribute).</summary>
    public const string AdminOrCommittee = $"{Admin},{MembershipCommittee}";

    /// <summary>Admin or Member (comma-separated for Blazor Roles attribute).</summary>
    public const string AdminOrMember = $"{Admin},{Member}";
}
