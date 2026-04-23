using System.Security.Claims;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2.Membership;

/// <summary>
/// Implements <see cref="IMemberClaimSynchroniser"/> using ASP.NET Identity's UserManager.
/// </summary>
public sealed class MemberClaimSynchroniser(
    UserManager<ClubBaistUser> userManager,
    ILogger<MemberClaimSynchroniser> logger) : IMemberClaimSynchroniser
{
    public async Task SynchroniseAsync(ClubBaistUser user, MembershipLevel newLevel, CancellationToken ct = default)
    {
        var existingClaims = await userManager.GetClaimsAsync(user);

        bool isShareholder = newLevel.MemberType == MemberType.Shareholder;
        bool isCopper = newLevel.ShortCode.Equals("CP", StringComparison.OrdinalIgnoreCase);

        await SyncClaimAsync(user, existingClaims, AppRoles.Claims.StandingTeeTimeBooking, isShareholder);
        await SyncClaimAsync(user, existingClaims, AppRoles.Claims.ShareholderStatus, isShareholder);
        await SyncClaimAsync(user, existingClaims, AppRoles.Claims.CopperTierStatus, isCopper);
    }

    private async Task SyncClaimAsync(
        ClubBaistUser user,
        IList<Claim> existingClaims,
        Claim targetClaim,
        bool shouldHave)
    {
        bool hasClaim = existingClaims.Any(c => c.Type == targetClaim.Type && c.Value == targetClaim.Value);

        if (shouldHave && !hasClaim)
        {
            var result = await userManager.AddClaimAsync(user, targetClaim);
            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Failed to add claim {ClaimType}={ClaimValue} for user {UserId}: {Errors}",
                    targetClaim.Type, targetClaim.Value, user.Id,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
        else if (!shouldHave && hasClaim)
        {
            // RemoveClaimAsync removes all matching type+value entries, eliminating any duplicates.
            var result = await userManager.RemoveClaimAsync(user, targetClaim);
            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Failed to remove claim {ClaimType}={ClaimValue} for user {UserId}: {Errors}",
                    targetClaim.Type, targetClaim.Value, user.Id,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
