using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubBaist.Domain;

public interface IApplicationDbContext<TKey> where TKey : IEquatable<TKey>
{
    DbSet<MembershipApplication<TKey>> MembershipApplications { get; }
    DbSet<MemberAccount<TKey>> MemberAccounts { get; }
    DbSet<ApplicationStatusHistory<TKey>> ApplicationStatusHistories { get; }
    DbSet<Season> Seasons { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<StandingTeeTime> StandingTeeTimes { get; }
    DbSet<ClubEvent> ClubEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    IExecutionStrategy CreateExecutionStrategy();
}
