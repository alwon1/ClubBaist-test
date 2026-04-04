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

    public async Task<bool> ApproveMembershipApplicationAsync(int applicationId, int membershipLevelId)
    {
        var strategy = db.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot);
            try
            {
                var application = await db.MembershipApplications.FindAsync(applicationId);
                if (application is null)
                {
                    logger.LogWarning("Approve requested for non-existent application {ApplicationId}", applicationId);
                    await transaction.RollbackAsync();
                    return false;
                }

                if (application.Status is ApplicationStatus.Accepted or ApplicationStatus.Denied)
                {
                    logger.LogWarning("Application {ApplicationId} is already {Status}; cannot approve.", applicationId, application.Status);
                    await transaction.RollbackAsync();
                    return false;
                }

                var membershipLevel = await db.MembershipLevels.FindAsync(membershipLevelId);
                if (membershipLevel is null)
                {
                    logger.LogWarning("MembershipLevel {LevelId} not found; cannot approve application {ApplicationId}.", membershipLevelId, applicationId);
                    await transaction.RollbackAsync();
                    return false;
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

                var result = await userManager.CreateAsync(user, "ChangeMe123!");
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        logger.LogError("User creation failed for {Email}: {Code} - {Description}", application.Email, error.Code, error.Description);
                    }

                    await transaction.RollbackAsync();
                    return false;
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
                        return false;
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
                    return false;
                }

                db.MemberShips.Add(new MemberShipInfo { User = user, MembershipLevel = membershipLevel });
                application.Status = ApplicationStatus.Accepted;

                var saved = await db.SaveChangesAsync() > 0;
                if (!saved)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error approving membership application {ApplicationId}", applicationId);
                await transaction.RollbackAsync();
                throw;
            }
        });
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

