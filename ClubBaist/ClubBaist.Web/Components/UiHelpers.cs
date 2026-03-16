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

    public static string GetSlotCapacityClass(int remainingCapacity) => remainingCapacity switch
    {
        BookingConstants.MaxPlayersPerSlot => "slot-row-open",
        > 0 => "slot-row-partial",
        _ => "slot-row-full"
    };

    public static string GetRoleBadgeClass(string role) => role switch
    {
        AppRoles.Admin => "bg-danger",
        AppRoles.MembershipCommittee => "bg-warning text-dark",
        AppRoles.Member => "bg-success",
        _ => "bg-secondary"
    };
}
