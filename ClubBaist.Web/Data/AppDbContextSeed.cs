using System.Text.Json;
using System.Text.Json.Serialization;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Services2;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ClubBaist.Web.Data;

internal static class AppDbContextSeed
{
    private const string DefaultPassword = "Pass@word1";

    // ReferenceHandler.Preserve resolves $id/$ref within the document so nav properties
    // on later objects point to the same C# instances as earlier objects — no manual
    // key-lookup dictionaries needed. JsonStringEnumConverter lets us use enum member
    // names ("Submitted", "Monday", etc.) instead of numeric values.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static SeedData LoadSeedData()
    {
        using var stream = typeof(AppDbContextSeed).Assembly
            .GetManifestResourceStream("ClubBaist.Web.Data.SeedData.seed.jsonc")
            ?? throw new InvalidOperationException(
                "Embedded resource 'ClubBaist.Web.Data.SeedData.seed.jsonc' not found. " +
                "Ensure the file is marked as EmbeddedResource in ClubBaist.Web.csproj.");

        return JsonSerializer.Deserialize<SeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException("seed.jsonc deserialised to null.");
    }

    public static void Seed(AppDbContext db, bool storeCreated)
    {
        if (!storeCreated) return;
        SeedAsync(db, storeCreated, CancellationToken.None).GetAwaiter().GetResult();
    }

    public static async Task SeedAsync(AppDbContext db, bool storeCreated, CancellationToken cancellationToken)
    {
        if (!storeCreated) return;

        var userManager = db.GetService<UserManager<ClubBaistUser>>();
        var seed = LoadSeedData();

        // Insertion order mirrors FK dependency order documented in seed.jsonc.
        // Each step saves immediately so the next step can read DB-assigned IDs.

        // Step 1: Roles — no FK dependencies
        await SeedRolesAsync(db, seed, cancellationToken);

        // Step 2: Membership levels (with tee-time availability windows) — no FK dependencies
        await SeedMembershipLevelsAsync(db, seed, cancellationToken);

        // Step 3: Users — depends on roles (AspNetUserRoles)
        await SeedUsersAsync(userManager, seed, cancellationToken);

        // Step 4: Memberships — depends on users (UserId FK) and levels (MembershipLevelId FK)
        await SeedMembershipsAsync(db, seed, cancellationToken);

        // Step 5: Applications — depends on levels (RequestedMembershipLevelId FK)
        //                        and members (Sponsor1MemberId / Sponsor2MemberId FKs)
        await SeedApplicationsAsync(db, seed, cancellationToken);

        // Step 6: Season and tee time slots — generated algorithmically; not in seed.jsonc
        await SeedCurrentSeasonAsync(db, cancellationToken);
    }

    private static async Task SeedRolesAsync(AppDbContext db, SeedData seed, CancellationToken cancellationToken)
    {
        db.Roles.AddRange(seed.Roles);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedMembershipLevelsAsync(AppDbContext db, SeedData seed, CancellationToken cancellationToken)
    {
        db.MembershipLevels.AddRange(seed.MembershipLevels);
        await db.SaveChangesAsync(cancellationToken);
        // MembershipLevel.Id values are now DB-assigned.
        // $ref-linked objects (members, applications) reference the same C# instances,
        // so they automatically see the updated Id values.
    }

    private static async Task SeedUsersAsync(UserManager<ClubBaistUser> userManager, SeedData seed, CancellationToken cancellationToken)
    {
        foreach (var entry in seed.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfFailed(await userManager.CreateAsync(entry.User, DefaultPassword), $"create user '{entry.User.Email}'");
            ThrowIfFailed(await userManager.AddToRoleAsync(entry.User, entry.Role), $"assign role '{entry.Role}' to '{entry.User.Email}'");
            // entry.User.Id is now set by Identity.
            // $ref-linked MemberShipInfo objects reference the same C# instance,
            // so they automatically see the updated Id.
        }
    }

    private static async Task SeedMembershipsAsync(AppDbContext db, SeedData seed, CancellationToken cancellationToken)
    {
        // Scalar FK properties must be set explicitly — EF needs them alongside nav properties.
        // By this point, User.Id and MembershipLevel.Id are set (Steps 3 & 2 saved).
        foreach (var member in seed.Members)
        {
            member.UserId = member.User.Id;
            member.MembershipLevelId = member.MembershipLevel.Id;
        }

        db.MemberShips.AddRange(seed.Members);
        await db.SaveChangesAsync(cancellationToken);
        // MemberShipInfo.Id values are now DB-assigned; used for sponsor FKs in Step 5.
    }

    private static async Task SeedApplicationsAsync(AppDbContext db, SeedData seed, CancellationToken cancellationToken)
    {
        foreach (var app in seed.Applications)
        {
            // All three nav properties are set via $ref; derive scalar FKs from them.
            app.RequestedMembershipLevelId = app.RequestedMembershipLevel.Id;
            app.Sponsor1MemberId = app.Sponsor1Member!.Id;
            app.Sponsor2MemberId = app.Sponsor2Member!.Id;
        }

        db.MembershipApplications.AddRange(seed.Applications);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCurrentSeasonAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var season = new Season
        {
            Name = $"{today.Year} Season",
            StartDate = new DateOnly(today.Year, 1, 1),
            EndDate = new DateOnly(today.Year, 12, 31)
        };

        db.Seasons.Add(season);
        await db.SaveChangesAsync(cancellationToken);

        var slots = SeasonService2.GenerateSlots(season, OperatingHours.AllDaysDefault()).ToList();
        await db.TeeTimeSlots.AddRangeAsync(slots, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (result.Succeeded) return;
        var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
        throw new InvalidOperationException($"Failed to {action}: {errors}");
    }

    // Wrapper for the entire seed.jsonc document.
    private sealed record SeedData(
        List<IdentityRole<Guid>> Roles,
        List<MembershipLevel> MembershipLevels,
        List<UserSeedEntry> Users,
        List<MemberShipInfo> Members,
        List<MembershipApplication> Applications
    );

    // ClubBaistUser has no Role property (Identity roles are a separate relationship),
    // so we pair the user with the role name for UserManager.AddToRoleAsync.
    private sealed record UserSeedEntry(ClubBaistUser User, string Role);
}
