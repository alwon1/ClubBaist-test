using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Domain;

public interface IApplicationDbContext<TKey> where TKey : IEquatable<TKey>
{
    DbSet<MembershipApplication<TKey>> MembershipApplications { get; }
    DbSet<MemberAccount<TKey>> MemberAccounts { get; }
    DbSet<ApplicationStatusHistory<TKey>> ApplicationStatusHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}