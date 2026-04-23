using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Services2;
using ClubBaist.Services2.Membership;
using ClubBaist.Services2.Membership.Applications;
using ClubBaist.Web.Components;
using ClubBaist.Web.Components.Account;
using ClubBaist.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        builder.AddSqlServerDbContext<AppDbContext>("clubbaist");
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<ClubBaistUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ClubBaistUser>, IdentityNoOpEmailSender>();

        // Domain services
        builder.Services.AddScoped<IAppDbContext2>(sp => sp.GetRequiredService<AppDbContext>());
        builder.Services.AddTeeTimeBookingServices2();
        builder.Services.AddScoped<MembershipApplicationService>();
        builder.Services.AddScoped<MembershipService>();
        builder.Services.AddScoped<MembershipLevelService>();
        // Authorization policies
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppRoles.Admin, policy => policy.RequireRole(AppRoles.Admin))
            .AddPolicy(AppRoles.MembershipCommittee, policy => policy.RequireRole(AppRoles.Admin, AppRoles.MembershipCommittee))
            .AddPolicy(AppRoles.Member, policy => policy.RequireRole(AppRoles.Member))
            .AddPolicy(AppRoles.Permissions.BookStandingTeeTime, policy =>
                policy.RequireRole(AppRoles.Member)
                      .RequireClaim(AppRoles.ClaimTypes.Permission, AppRoles.Permissions.BookStandingTeeTime))
            // Named policies — use [Authorize(Policy = PolicyNames.Xyz)] on new pages
            .AddPolicy(PolicyNames.Admin, policy => policy.RequireRole(AppRoles.Admin))
            .AddPolicy(PolicyNames.AdminOrCommittee, policy => policy.RequireRole(AppRoles.Admin, AppRoles.MembershipCommittee))
            .AddPolicy(PolicyNames.AdminOrClerk, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Clerk))
            .AddPolicy(PolicyNames.AdminOrProShop, policy => policy.RequireRole(AppRoles.Admin, AppRoles.ProShopStaff))
            .AddPolicy(PolicyNames.MemberAny, policy => policy.RequireRole(AppRoles.Member))
            .AddPolicy(PolicyNames.MemberWithStandingBooking, policy =>
                policy.RequireRole(AppRoles.Member)
                      .RequireClaim(AppRoles.ClaimTypes.Permission, AppRoles.Permissions.BookStandingTeeTime))
            .AddPolicy(PolicyNames.ShareholderMember, policy =>
                policy.RequireRole(AppRoles.Member)
                      .RequireClaim(AppRoles.ClaimTypes.MembershipFact, AppRoles.MembershipFacts.Shareholder));

        var app = builder.Build();

        await DatabaseInitializerService.InitializeAsync(app.Services, app.Lifetime.ApplicationStopping);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        await app.RunAsync();
    }
}
