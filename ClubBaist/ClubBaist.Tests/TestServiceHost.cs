using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddDbContext<TestApplicationDbContext>(options => options.UseSqlite(connection));

        services.AddIdentityCore<IdentityUser<int>>()
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<TestApplicationDbContext>();

        services.AddScoped<IApplicationDbContext<int>>(provider => provider.GetRequiredService<TestApplicationDbContext>());
        services.AddScoped<MemberManagementService<int>>();
        services.AddScoped<ApplicationManagementService<int>>();
        services.AddScoped<SeasonService<int>>();
        services.AddScoped<BookingPolicyService<int>>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        using (var initScope = provider.CreateScope())
        {
            initScope.ServiceProvider.GetRequiredService<TestApplicationDbContext>().Database.EnsureCreated();
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
