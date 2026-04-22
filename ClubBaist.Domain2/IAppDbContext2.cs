using System.Data;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Domain2.Entities.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubBaist.Domain2;

/// <summary>
/// Abstraction over AppDbContext used by ClubBaist.Services2.
/// Enables testing without a real database.
/// </summary>
public interface IAppDbContext2
{
    /// <summary>Pre-populated slot rows — one row per valid tee time, generated on season creation. FK target for TeeTimeBooking.</summary>
    DbSet<TeeTimeSlot> TeeTimeSlots { get; }

    DbSet<TeeTimeBooking> TeeTimeBookings { get; }
    DbSet<MemberShipInfo> MemberShips { get; }
    DbSet<MembershipLevel> MembershipLevels { get; }
    DbSet<MembershipApplication> MembershipApplications { get; }
    DbSet<MembershipLevelTeeTimeAvailability> MembershipLevelTeeTimeAvailabilities { get; }
    DbSet<SpecialEvent> SpecialEvents { get; }
    DbSet<Season> Seasons { get; }
    DbSet<StandingTeeTime> StandingTeeTimes { get; }

    /// <summary>Submitted golf rounds, one per tee time booking per member.</summary>
    DbSet<GolfRound> GolfRounds { get; }

    /// <summary>Reference course and slope ratings by tee color and gender.</summary>
    DbSet<CourseRating> CourseRatings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    IExecutionStrategy CreateExecutionStrategy();
}
