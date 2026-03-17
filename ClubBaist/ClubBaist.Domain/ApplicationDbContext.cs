using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubBaist.Domain;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext<Guid>
{
    public DbSet<MembershipApplication<Guid>> MembershipApplications => Set<MembershipApplication<Guid>>();
    public DbSet<MemberAccount<Guid>> MemberAccounts => Set<MemberAccount<Guid>>();
    public DbSet<ApplicationStatusHistory<Guid>> ApplicationStatusHistories => Set<ApplicationStatusHistory<Guid>>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    public IExecutionStrategy CreateExecutionStrategy() =>
        Database.CreateExecutionStrategy();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.LastName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Phone).IsRequired();
        });

        builder.Entity<MembershipApplication<Guid>>(entity =>
        {
            entity.HasKey(a => a.ApplicationId);

            entity.HasOne(a => a.ApplicationUser)
                .WithMany()
                .HasForeignKey(a => a.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MemberAccount<Guid>>(entity =>
        {
            entity.HasKey(m => m.MemberAccountId);
            entity.Property(m => m.MemberAccountId)
                .ValueGeneratedOnAdd()
                .HasAnnotation("SqlServer:Identity", "1000, 1");
            entity.HasIndex(m => m.MemberNumber).IsUnique();
            entity.HasIndex(m => m.ApplicationUserId).IsUnique();

            entity.HasOne(m => m.ApplicationUser)
                .WithMany()
                .HasForeignKey(m => m.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationStatusHistory<Guid>>(entity =>
        {
            entity.HasKey(h => h.ApplicationStatusHistoryId);

            entity.HasOne(h => h.MembershipApplication)
                .WithMany(a => a.StatusHistory)
                .HasForeignKey(h => h.MembershipApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Season>(entity =>
        {
            entity.HasKey(s => s.SeasonId);
            entity.Property(s => s.Name).IsRequired();
            entity.HasIndex(s => s.Name).IsUnique();
            entity.HasIndex(s => new { s.StartDate, s.EndDate });
        });

        builder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.ReservationId);
            entity.HasIndex(r => new { r.BookingMemberAccountId, r.SlotDate, r.SlotTime });
            entity.PrimitiveCollection(r => r.PlayerMemberAccountIds);
        });
    }
}
