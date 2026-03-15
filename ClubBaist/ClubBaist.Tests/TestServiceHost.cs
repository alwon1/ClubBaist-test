using ClubBaist.Domain;
using ClubBaist.Services;
using ClubBaist.Services.Rules;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace ClubBaist.Tests;

public static class TestServiceHost
{
    /// <summary>
    /// Creates a service scope backed by its own isolated in-memory SQLite database.
    /// Each call returns a completely independent context, making tests safe to run in parallel.
    /// Disposing the returned scope also disposes the underlying provider and connection.
    /// </summary>
    public static IServiceScope CreateScope()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));

        services.AddIdentityCore<IdentityUser<Guid>>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<IApplicationDbContext<Guid>>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<MemberManagementService<Guid>>();
        services.AddScoped<ApplicationManagementService<Guid>>();
        services.AddScoped<TeeTimeBookingService<Guid>>();

        // Booking rules
        services.AddScoped<IBookingRule, BookingWindowRule>();
        services.AddScoped<IBookingRule, SlotCapacityRule<Guid>>();
        services.AddScoped<IBookingRule, MembershipTimeRestrictionRule>();

        // Schedule service
        services.AddSingleton<IScheduleTimeService, DefaultScheduleTimeService>();

        // SeasonService is a singleton loaded once from DB on first resolution.
        // The DB is guaranteed to exist before any test scope resolves it.
        services.AddSingleton<ISeasonService>(provider =>
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seasons = db.Seasons
                .Where(s => s.SeasonStatus == SeasonStatus.Active || s.SeasonStatus == SeasonStatus.Planned)
                .ToList();
            return new SeasonService(seasons);
        });

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        using (var initScope = provider.CreateScope())
        {
            initScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        return new TestScope(provider.CreateScope(), provider, connection);
    }

    private sealed class TestScope : IServiceScope
    {
        private readonly IServiceScope _inner;
        private readonly ServiceProvider _provider;
        private readonly SqliteConnection _connection;

        public TestScope(IServiceScope inner, ServiceProvider provider, SqliteConnection connection)
        {
            _inner = inner;
            _provider = provider;
            _connection = connection;
        }

        public IServiceProvider ServiceProvider => _inner.ServiceProvider;

        public void Dispose()
        {
            _inner.Dispose();
            _provider.Dispose();
            _connection.Close();
            _connection.Dispose();
        }
    }
}
