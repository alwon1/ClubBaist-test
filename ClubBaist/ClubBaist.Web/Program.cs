using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities;
using ClubBaist.Services2;
using ClubBaist.Services2.Membership;
using ClubBaist.Services2.Membership.Applications;
using ClubBaist.Web.Components;
using ClubBaist.Web.Components.Account;

namespace ClubBaist.Web;

public class Program
{
    public static void Main(string[] args)
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
        builder.Services.AddTeeTimeBookingServices2();
        // Authorization policies
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AppRoles.Admin, policy => policy.RequireRole(AppRoles.Admin))
            .AddPolicy(AppRoles.MembershipCommittee, policy => policy.RequireRole(AppRoles.Admin, AppRoles.MembershipCommittee))
            .AddPolicy(AppRoles.Member, policy => policy.RequireRole(AppRoles.Member));

        var app = builder.Build();

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
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        app.Run();
    }
}
