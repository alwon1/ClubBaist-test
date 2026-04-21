using System.Data;
using System.Text.Json;
using ClubBaist.Domain.Entities;
using ClubBaist.Domain.Entities.Membership;
using ClubBaist.Domain.Entities.Scoring;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubBaist.Domain;

public class AppDbContext : IdentityDbContext<ClubBaistUser, IdentityRole<Guid>, Guid>
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
    public DbSet<GolfRound> GolfRounds { get; set; }

    public Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(isolationLevel, cancellationToken);

    public IExecutionStrategy CreateExecutionStrategy() =>
        Database.CreateExecutionStrategy();

    public async Task EnsureSqlServerSnapshotIsolationAsync(CancellationToken cancellationToken = default)
    {
        if (!Database.IsSqlServer())
        {
            return;
        }

        var connectionString = Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var databaseName = Database.GetDbConnection().Database;
        var escapedDatabaseName = databaseName.Replace("]", "]]" );
        var sqlBuilder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(sqlBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var commandText = $"""
            IF DB_ID(N'{databaseName.Replace("'", "''")}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{escapedDatabaseName}] SET ALLOW_SNAPSHOT_ISOLATION ON;
                ALTER DATABASE [{escapedDatabaseName}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
            END
            """;

        await using var command = new SqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

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

            entity.HasMany(booking => booking.AdditionalParticipants)
                .WithMany()
                .UsingEntity(join => join.ToTable("BookingAdditionalParticipant"));
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

        modelBuilder.Entity<GolfRound>(entity =>
        {
            entity.HasOne(r => r.TeeTimeBooking)
                .WithMany()
                .HasForeignKey(r => r.TeeTimeBookingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Member)
                .WithMany()
                .HasForeignKey(r => r.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(r => r.Scores)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<uint?>>(v, (JsonSerializerOptions?)null)
                         ?? Enumerable.Repeat<uint?>(null, 18).ToList(),
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<uint?>>(
                        (a, b) => a != null && b != null && a.SequenceEqual(b),
                        v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                        v => v.ToList()))
                .HasColumnType("nvarchar(max)");
        });
    }
}
