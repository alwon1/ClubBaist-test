using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2.Membership.Applications;

public class MembershipApplicationService(
    IAppDbContext2 db,
    UserManager<ClubBaistUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<MembershipApplicationService> logger)
{
    public IQueryable<MembershipApplication> GetMembershipApplications() => db.MembershipApplications.AsNoTracking();

    public async Task<MembershipApplication?> GetMembershipApplicationByIdAsync(int applicationId) =>
        await db.MembershipApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId);

    public async Task<bool> SubmitMembershipApplicationAsync(MembershipApplication application)
    {
        if (await db.MemberShips.AnyAsync(m => m.User.Email == application.Email))
        {
            logger.LogWarning("Application submitted for {Email} who is already a member.", application.Email);
            return false;
        }

        if (await db.MembershipApplications.AnyAsync(a => a.Email == application.Email && a.Status != ApplicationStatus.Denied))
        {
            logger.LogWarning("An active application already exists for {Email}.", application.Email);
            return false;
        }

        db.MembershipApplications.Add(application);
        return await db.SaveChangesAsync() > 0;
    }

    public async Task<(bool Success, string? GeneratedPassword)> ApproveMembershipApplicationAsync(int applicationId, int membershipLevelId)
    {
        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<(bool Success, string? GeneratedPassword)>(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot);
            try
            {
                var application = await db.MembershipApplications.FindAsync(applicationId);
                if (application is null)
                {
                    logger.LogWarning("Approve requested for non-existent application {ApplicationId}", applicationId);
                    await transaction.RollbackAsync();
                    return (false, null);
                }

                if (application.Status is ApplicationStatus.Accepted or ApplicationStatus.Denied)
                {
                    logger.LogWarning("Application {ApplicationId} is already {Status}; cannot approve.", applicationId, application.Status);
                    await transaction.RollbackAsync();
                    return (false, null);
                }

                var membershipLevel = await db.MembershipLevels.FindAsync(membershipLevelId);
                if (membershipLevel is null)
                {
                    logger.LogWarning("MembershipLevel {LevelId} not found; cannot approve application {ApplicationId}.", membershipLevelId, applicationId);
                    await transaction.RollbackAsync();
                    return (false, null);
                }

                var user = new ClubBaistUser
                {
                    UserName = application.Email,
                    Email = application.Email,
                    FirstName = application.FirstName,
                    LastName = application.LastName,
                    DateOfBirth = application.DateOfBirth,
                    PhoneNumber = application.Phone,
                    AlternatePhoneNumber = application.AlternatePhone,
                    AddressLine1 = application.Address,
                    PostalCode = application.PostalCode,
                    City = "Unknown",
                    Province = "Unknown",
                    EmailConfirmed = true,
                };

                var generatedPassword = GenerateSecurePassword();
                var result = await userManager.CreateAsync(user, generatedPassword);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        logger.LogError("User creation failed for {Email}: {Code} - {Description}", application.Email, error.Code, error.Description);
                    }

                    await transaction.RollbackAsync();
                    return (false, null);
                }

                if (!await roleManager.RoleExistsAsync(AppRoles.Member))
                {
                    var createRoleResult = await roleManager.CreateAsync(new IdentityRole<Guid> { Name = AppRoles.Member });
                    if (!createRoleResult.Succeeded)
                    {
                        foreach (var error in createRoleResult.Errors)
                        {
                            logger.LogError("Role creation failed for {Role}: {Code} - {Description}", AppRoles.Member, error.Code, error.Description);
                        }

                        await transaction.RollbackAsync();
                        return (false, null);
                    }
                }

                var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Member);
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        logger.LogError("Role assignment failed for {Email}: {Code} - {Description}", application.Email, error.Code, error.Description);
                    }

                    await transaction.RollbackAsync();
                    return (false, null);
                }

                db.MemberShips.Add(new MemberShipInfo { User = user, MembershipLevel = membershipLevel });
                application.Status = ApplicationStatus.Accepted;

                var saved = await db.SaveChangesAsync() > 0;
                if (!saved)
                {
                    await transaction.RollbackAsync();
                    return (false, null);
                }

                await transaction.CommitAsync();
                return (true, generatedPassword);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error approving membership application {ApplicationId}", applicationId);
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private static string GenerateSecurePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%&*?";
        const string all = upper + lower + digits + special;

        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[16];
        rng.GetBytes(bytes);

        // Guarantee at least one character from each required set
        var chars = new char[16];
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];
        for (var i = 4; i < 16; i++)
            chars[i] = all[bytes[i] % all.Length];

        // Fisher-Yates shuffle
        rng.GetBytes(bytes);
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = bytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    public async Task<bool> DenyApplicationAsync(int applicationId)
    {
        var application = await db.MembershipApplications.FindAsync(applicationId);
        if (application is null)
            return false;

        if (application.Status is ApplicationStatus.Accepted or ApplicationStatus.Denied)
        {
            logger.LogWarning("Application {ApplicationId} is already {Status}; cannot deny.", applicationId, application.Status);
            return false;
        }

        application.Status = ApplicationStatus.Denied;
        return await db.SaveChangesAsync() > 0;
    }

    public async Task<bool> SetApplicationStatusAsync(int applicationId, ApplicationStatus status)
    {
        if (status is ApplicationStatus.Accepted or ApplicationStatus.Denied)
        {
            logger.LogWarning("Use ApproveMembershipApplicationAsync or DenyApplicationAsync to set terminal statuses.");
            return false;
        }

        var application = await db.MembershipApplications.FindAsync(applicationId);
        if (application is null)
            return false;

        application.Status = status;
        return await db.SaveChangesAsync() > 0;
    }
}

