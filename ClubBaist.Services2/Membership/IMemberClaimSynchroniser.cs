using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;

namespace ClubBaist.Services2.Membership;

/// <summary>
/// Synchronises ASP.NET Identity claims for a user to reflect their current membership level.
/// Call this whenever a member's membership level changes (approval, level edit, annual renewal).
/// </summary>
public interface IMemberClaimSynchroniser
{
    /// <summary>
    /// Evaluates <paramref name="newLevel"/> and updates the user's Identity claims to match:
    /// <list type="bullet">
    ///   <item>Shareholder level → grant <c>standing-tee-time.book</c> and <c>clubbaist.membership=shareholder</c>; revoke <c>clubbaist.membership=copper-tier</c>.</item>
    ///   <item>Copper (Social) level → grant <c>clubbaist.membership=copper-tier</c>; revoke standing-tee-time and shareholder claims.</item>
    ///   <item>All other levels → revoke all three claims.</item>
    /// </list>
    /// </summary>
    /// <returns><c>true</c> if all claim operations succeeded; <c>false</c> if any operation failed (errors are logged).</returns>
    Task<bool> SynchroniseAsync(ClubBaistUser user, MembershipLevel newLevel, CancellationToken ct = default);
}
