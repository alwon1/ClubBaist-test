using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using ClubBaist.Domain;
using ClubBaist.Services;
using ClubBaist.Services.Rules;
using ClubBaist.Web.Components;
using ClubBaist.Web.Components.Account;
using Microsoft.EntityFrameworkCore;

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
        builder.Services.AddScoped<ApplicationManagementService<Guid>>();
        builder.Services.AddScoped<MemberManagementService<Guid>>();
        builder.Services.AddScoped<TeeTimeBookingService<Guid>>();

        // Booking rules
        builder.Services.AddScoped<IBookingRule, BookingWindowRule>();
        builder.Services.AddScoped<IBookingRule, SlotCapacityRule<Guid>>();
        builder.Services.AddScoped<IBookingRule, MembershipTimeRestrictionRule>();

        // Schedule & season services
        builder.Services.AddSingleton<IScheduleTimeService, DefaultScheduleTimeService>();
        builder.Services.AddSingleton<ISeasonService>(sp =>
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seasons = db.Seasons
                .Where(s => s.SeasonStatus == SeasonStatus.Active || s.SeasonStatus == SeasonStatus.Planned)
                .ToList();
            return new SeasonService(seasons);
        });

        // Authorization policies
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin"))
            .AddPolicy("MembershipCommittee", policy => policy.RequireRole("Admin", "MembershipCommittee"))
            .AddPolicy("Member", policy => policy.RequireRole("Member"));

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
