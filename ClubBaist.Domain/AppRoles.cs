namespace ClubBaist.Domain;

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
