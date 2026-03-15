using ClubBaist.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Tests;

public sealed class TestApplicationDbContext
    : IdentityDbContext<IdentityUser<int>, IdentityRole<int>, int>, IApplicationDbContext<int>
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MembershipApplication<int>> MembershipApplications => Set<MembershipApplication<int>>();
    public DbSet<MemberAccount<int>> MemberAccounts => Set<MemberAccount<int>>();
    public DbSet<ApplicationStatusHistory<int>> ApplicationStatusHistories => Set<ApplicationStatusHistory<int>>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MembershipApplication<int>>(entity =>
        {
            entity.HasKey(application => application.ApplicationId);

            entity.HasOne(application => application.ApplicationUser)
                .WithMany()
                .HasForeignKey(application => application.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MemberAccount<int>>(entity =>
        {
            entity.HasKey(member => member.MemberAccountId);
            entity.HasIndex(member => member.MemberNumber).IsUnique();
            entity.HasIndex(member => member.ApplicationUserId).IsUnique();

            entity.HasOne(member => member.ApplicationUser)
                .WithMany()
                .HasForeignKey(member => member.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationStatusHistory<int>>(entity =>
        {
            entity.HasKey(history => history.ApplicationStatusHistoryId);

            entity.HasOne(history => history.MembershipApplication)
                .WithMany(application => application.StatusHistory)
                .HasForeignKey(history => history.MembershipApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(history => history.ChangedByUser)
                .WithMany()
                .HasForeignKey(history => history.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Season>(entity =>
        {
            entity.HasKey(season => season.SeasonId);
            entity.Property(season => season.Name)
                .IsRequired();
            entity.HasIndex(season => season.Name)
                .IsUnique();
            entity.HasIndex(season => new { season.StartDate, season.EndDate });
        });

        builder.Entity<Reservation>(entity =>
        {
            entity.HasKey(reservation => reservation.ReservationId);

            entity.HasIndex(reservation => new
            {
                reservation.BookingMemberAccountId,
                reservation.SlotDate,
                reservation.SlotTime
            });

            entity.PrimitiveCollection(reservation => reservation.PlayerMemberAccountIds);
        });
    }
}
