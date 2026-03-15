using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain;

public interface IApplicationDbContext<TKey> where TKey : IEquatable<TKey>
{
    DbSet<MembershipApplication<TKey>> MembershipApplications { get; }
    DbSet<MemberAccount<TKey>> MemberAccounts { get; }
    DbSet<ApplicationStatusHistory<TKey>> ApplicationStatusHistories { get; }
    DbSet<Season> Seasons { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<ReservationPlayer> ReservationPlayers { get; }
    DbSet<SlotOccupancy> SlotOccupancies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
