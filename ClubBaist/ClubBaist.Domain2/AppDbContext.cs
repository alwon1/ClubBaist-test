using System.Data;
using ClubBaist.Domain2.Entities.Membership;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubBaist.Domain2;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TeeTimeSlot> TeeTimeSlots { get; set; }
    public DbSet<TeeTimeBooking> TeeTimeBookings { get; set; }
    public DbSet<MemberShipInfo> MemberShips { get; set; }
    public DbSet<MembershipLevel> MembershipLevels { get; set; }
    public DbSet<MembershipApplication> MembershipApplications { get; set; }
    public DbSet<MembershipLevelTeeTimeAvailability> MembershipLevelTeeTimeAvailabilities { get; set; }
    public DbSet<SpecialEvent> SpecialEvents { get; set; }
    public DbSet<Season> Seasons { get; set; }

    public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(isolationLevel, cancellationToken);

    public IExecutionStrategy CreateExecutionStrategy() =>
        Database.CreateExecutionStrategy();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TeeTimeBooking>(entity =>
        {
            entity.HasMany(b => b.AdditionalParticipants)
                  .WithMany()
                  .UsingEntity(j => j.ToTable("BookingAdditionalParticipant"));

            entity.Property(b => b.ParticipantCount)
                  .HasComputedColumnSql(
                      "(1 + (SELECT COUNT(*) FROM [BookingAdditionalParticipant] WHERE [TeeTimeBookingId] = [Id]))",
                      stored: true);
        });

        modelBuilder.Entity<MembershipApplication>()
            .HasOne(a => a.RequestedMembershipLevel)
            .WithMany()
            .HasForeignKey(a => a.RequestedMembershipLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
