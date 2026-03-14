using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

public static class TestServiceHost
{
    private static SqliteConnection? _connection;
    private static ServiceProvider? _serviceProvider;

    public static void Initialize()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddDbContext<TestApplicationDbContext>(options => options.UseSqlite(_connection));

        services.AddIdentityCore<IdentityUser<int>>()
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<TestApplicationDbContext>();

        services.AddScoped<IApplicationDbContext<int>>(provider => provider.GetRequiredService<TestApplicationDbContext>());
        services.AddScoped<MemberManagementService<int>>();
        services.AddScoped<ApplicationManagementService<int>>();

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestApplicationDbContext>();
        dbContext.Database.EnsureCreated();
    }

    public static void Cleanup()
    {
        _serviceProvider?.Dispose();

        if (_connection is not null)
        {
            _connection.Close();
            _connection.Dispose();
        }
    }

    public static IServiceScope CreateScope()
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("Test service provider is not initialized.");
        }

        return _serviceProvider.CreateScope();
    }
}