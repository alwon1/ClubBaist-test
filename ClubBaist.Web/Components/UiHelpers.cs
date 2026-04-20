using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities.Membership;

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

    public static string GetStandingTeeTimeStatusBadgeClass(StandingTeeTimeStatus status) => status switch
    {
        StandingTeeTimeStatus.Draft => "bg-secondary",
        StandingTeeTimeStatus.Approved => "bg-success",
        StandingTeeTimeStatus.Allocated => "bg-primary",
        StandingTeeTimeStatus.Unallocated => "bg-warning text-dark",
        StandingTeeTimeStatus.Cancelled => "bg-danger",
        StandingTeeTimeStatus.Denied => "bg-danger",
        _ => "bg-secondary"
    };
}
