using ClubBaist.Domain;
using ClubBaist.Services.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers BookingService and all Domain booking rules.
    ///
    /// Prerequisites: the caller must register AppDbContext (typically via
    /// services.AddDbContext&lt;AppDbContext&gt;()).
    /// </summary>
    public static IServiceCollection AddTeeTimeBookingServices(this IServiceCollection services)
    {
        // Rules that need IQueryable<T> pulled from the scoped DbContext
        services.AddScoped<IBookingRule>(_ => new PastSlotRule());

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return new SpecialEventBlockingRule(db.SpecialEvents);
        });

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return new MembershipLevelAvailabilityRule(db.MembershipLevelTeeTimeAvailabilities);
        });

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return new MaxParticipantsRule(db.TeeTimeBookings);
        });

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            return new DuplicateBookingRule(db.TeeTimeBookings);
        });

        services.AddScoped<BookingService>();
        services.AddScoped<SeasonService>();
        services.AddScoped<StandingTeeTimeService>();

        services.AddScoped<ScoreService>();
        services.AddScoped<HandicapCalculationService>();
        services.AddScoped<RoundScoreDerivationService>();

        return services;
    }
}
