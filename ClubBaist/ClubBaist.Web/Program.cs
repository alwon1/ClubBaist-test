using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using ClubBaist.Domain;
using ClubBaist.Services;
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

        builder.AddSqlServerDbContext<ApplicationDbContext>("clubbaist");
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddIdentityCore<IdentityUser<Guid>>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<IdentityUser<Guid>>, IdentityNoOpEmailSender>();

        // Domain services
        builder.Services.AddScoped<IApplicationDbContext<Guid>>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddClubBaistServices<Guid>();
        builder.Services.AddSeasonService<ApplicationDbContext>();

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
