using System.Data;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubBaist.Domain2;

public class AppDbContext : IdentityDbContext<ClubBaistUser, IdentityRole<Guid>, Guid>, IAppDbContext2
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
    public DbSet<StandingTeeTime> StandingTeeTimes { get; set; }

    public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(isolationLevel, cancellationToken);

    public IExecutionStrategy CreateExecutionStrategy() =>
        Database.CreateExecutionStrategy();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TeeTimeBooking>(entity =>
        {
            entity.HasOne(booking => booking.BookingMember)
                .WithMany()
                .HasForeignKey(booking => booking.BookingMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(booking => booking.StandingTeeTime)
                .WithMany()
                .HasForeignKey(booking => booking.StandingTeeTimeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.OwnsMany(booking => booking.AdditionalParticipants, navbuilder =>
            {
                navbuilder.WithOwner().HasForeignKey("TeeTimeBookingId");
                navbuilder.Property(participant => participant.Id)
                    .HasColumnName("MemberShipInfoId")
                    .ValueGeneratedNever();
                navbuilder.HasKey("TeeTimeBookingId", nameof(BookingParticipant.Id));
                navbuilder.ToTable("BookingAdditionalParticipant");
            });
        });

        modelBuilder.Entity<StandingTeeTime>(entity =>
        {
            entity.HasOne(standing => standing.BookingMember)
                .WithMany()
                .HasForeignKey(standing => standing.BookingMemberId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(standing => standing.AdditionalParticipants)
                .WithMany()
                .UsingEntity(join => join.ToTable("StandingTeeTimeAdditionalParticipant"));
            entity.Navigation(standing => standing.BookingMember).AutoInclude();
            entity.Navigation(standing => standing.AdditionalParticipants).AutoInclude();
        });

        modelBuilder.Entity<MemberShipInfo>()
            .HasOne(member => member.User)
            .WithOne()
            .HasForeignKey<MemberShipInfo>(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MemberShipInfo>()
            .Navigation(member => member.User)
            .AutoInclude();

        modelBuilder.Entity<MemberShipInfo>()
            .Navigation(member => member.MembershipLevel)
            .AutoInclude();

        modelBuilder.Entity<MembershipApplication>()
            .HasOne(a => a.RequestedMembershipLevel)
            .WithMany()
            .HasForeignKey(a => a.RequestedMembershipLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
