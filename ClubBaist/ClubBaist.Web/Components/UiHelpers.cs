using ClubBaist.Domain;
using ClubBaist.Services;

namespace ClubBaist.Web.Components;

public static class UiHelpers
{
    public static string GetStatusBadgeClass(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Submitted => "bg-primary",
        ApplicationStatus.OnHold => "bg-warning text-dark",
        ApplicationStatus.Waitlisted => "bg-info text-dark",
        ApplicationStatus.Accepted => "bg-success",
        ApplicationStatus.Denied => "bg-danger",
        _ => "bg-secondary"
    };

    public static string GetSlotStatusClass(int remainingCapacity, bool userCanBook) =>
        remainingCapacity == 0 ? "slot-row-full" :
        userCanBook ? "slot-row-open" :
        "slot-row-restricted";

    public static string GetRoleBadgeClass(string role) => role switch
    {
        AppRoles.Admin => "bg-danger",
        AppRoles.MembershipCommittee => "bg-warning text-dark",
        AppRoles.Member => "bg-success",
        _ => "bg-secondary"
    };

    public static string GetSeasonStatusBadgeClass(SeasonStatus status) => status switch
    {
        SeasonStatus.Active => "bg-success",
        SeasonStatus.Planned => "bg-primary",
        SeasonStatus.Closed => "bg-secondary",
        _ => "bg-secondary"
    };
}
