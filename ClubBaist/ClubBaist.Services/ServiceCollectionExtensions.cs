using ClubBaist.Domain;
using ClubBaist.Services.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all ClubBaist domain services, booking rules, and supporting singletons.
    /// The caller is responsible for registering the DbContext and Identity services.
    /// </summary>
    public static IServiceCollection AddClubBaistServices<TKey>(this IServiceCollection services)
        where TKey : IEquatable<TKey>
    {
        services.AddScoped<MemberManagementService<TKey>>();
        services.AddScoped<ApplicationManagementService<TKey>>();
        services.AddScoped<TeeTimeBookingService<TKey>>();
        services.AddScoped<StandingTeeTimeService<TKey>>();
        services.AddScoped<ClubEventService<TKey>>();

        // Booking rules (order matters — first -1 result short-circuits evaluation)
        services.AddScoped<IBookingRule, BookingWindowRule>();
        services.AddScoped<IBookingRule, ClubEventBlockingRule<TKey>>();
        services.AddScoped<IBookingRule, SlotCapacityRule<TKey>>();
        services.AddScoped<IBookingRule, MembershipTimeRestrictionRule>();
        services.AddScoped<IBookingRule, MemberConflictRule<TKey>>();

        // Schedule service
        services.AddSingleton<IScheduleTimeService, DefaultScheduleTimeService>();

        // Live availability notifications
        services.AddSingleton<AvailabilityUpdateService>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="ISeasonService"/> as a singleton loaded from the database.
    /// Must be called after DbContext registration.
    /// </summary>
    public static IServiceCollection AddSeasonService<TDbContext, TKey>(this IServiceCollection services)
        where TDbContext : IApplicationDbContext<TKey>
        where TKey : IEquatable<TKey>
    {
        services.AddSingleton<ISeasonService>(provider =>
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var seasons = db.Seasons
                .AsNoTracking()
                .Where(s => s.SeasonStatus == SeasonStatus.Active || s.SeasonStatus == SeasonStatus.Planned)
                .ToList();
            return new SeasonService(seasons);
        });

        return services;
    }
}
