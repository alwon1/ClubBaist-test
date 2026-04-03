using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2.Membership.Applications;

public class MembershipApplicationService(AppDbContext db, UserManager<ClubBaistUser> userManager, ILogger<MembershipApplicationService> logger)
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
        await db.BeginTransactionAsync(System.Data.IsolationLevel.Snapshot);
        try
        {
            var application = await db.MembershipApplications.FindAsync(applicationId);
            if (application is null)
            {
                logger.LogWarning("Approve requested for non-existent application {ApplicationId}", applicationId);
                return false;
            }

            if (application.Status is ApplicationStatus.Accepted or ApplicationStatus.Denied)
            {
                logger.LogWarning("Application {ApplicationId} is already {Status}; cannot approve.", applicationId, application.Status);
                return false;
            }

            var membershipLevel = await db.MembershipLevels.FindAsync(membershipLevelId);
            if (membershipLevel is null)
            {
                logger.LogWarning("MembershipLevel {LevelId} not found; cannot approve application {ApplicationId}.", membershipLevelId, applicationId);
                await db.Database.RollbackTransactionAsync();
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
                    logger.LogError("User creation failed for {Email}: {Code} - {Description}", application.Email, error.Code, error.Description);
                await db.Database.RollbackTransactionAsync();
                return false;
            }

            db.MemberShips.Add(new MemberShipInfo { User = user, MembershipLevel = membershipLevel });
            application.Status = ApplicationStatus.Accepted;

            await db.SaveChangesAsync();
            await db.Database.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await db.Database.RollbackTransactionAsync();
            throw;
        }
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

