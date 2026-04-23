using ClubBaist.Domain2;
using ClubBaist.Services2.Membership;
using ClubBaist.Services2.Scoring;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Services2;

public static class ServiceCollectionExtensions2
{
    /// <summary>
    /// Registers TeeTimeBookingService2 and all Domain2 booking rules.
    ///
    /// Prerequisites: the caller must register IAppDbContext2 (typically via
    /// services.AddDbContext&lt;AppDbContext&gt;() and services.AddScoped&lt;IAppDbContext2, AppDbContext&gt;()).
    /// </summary>
    public static IServiceCollection AddTeeTimeBookingServices2(this IServiceCollection services)
    {
        // Rules that need IQueryable<T> pulled from the scoped DbContext
        services.AddScoped<IBookingRule>(_ => new PastSlotRule());

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<IAppDbContext2>();
            return new SpecialEventBlockingRule(db.SpecialEvents);
        });

        // Explicitly denies Social (Copper/CP) members — no golf privileges.
        services.AddScoped<IBookingRule>(_ => new SocialMemberNoGolfRule());

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<IAppDbContext2>();
            return new MembershipLevelAvailabilityRule(db.MembershipLevelTeeTimeAvailabilities);
        });

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<IAppDbContext2>();
            return new MaxParticipantsRule(db.TeeTimeBookings);
        });

        services.AddScoped<IBookingRule>(sp =>
        {
            var db = sp.GetRequiredService<IAppDbContext2>();
            return new DuplicateBookingRule(db.TeeTimeBookings);
        });

        services.AddScoped<BookingService>();
        services.AddScoped<SeasonService2>();
        services.AddScoped<StandingTeeTimeService>();

        services.AddScoped<ScoreService>();
        services.AddScoped<HandicapCalculationService>();
        services.AddScoped<PlayingConditionService>();
        services.AddScoped<RoundScoreDerivationService>();
        services.AddScoped<CourseHoleReferenceService>();

        services.AddScoped<IMemberClaimSynchroniser, MemberClaimSynchroniser>();

        return services;
    }
}
