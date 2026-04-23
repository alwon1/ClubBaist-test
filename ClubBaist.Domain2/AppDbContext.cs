using System.Data;
using System.Text.Json;
using ClubBaist.Domain2.Entities;
using ClubBaist.Domain2.Entities.Membership;
using ClubBaist.Domain2.Entities.Scoring;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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
    public DbSet<GolfRound> GolfRounds { get; set; }
    public DbSet<CourseRating> CourseRatings { get; set; }
    public DbSet<CourseHole> CourseHoles { get; set; }

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

            entity.Navigation(r => r.TeeTimeBooking).AutoInclude();
            entity.Navigation(r => r.Member).AutoInclude();
        });

        modelBuilder.Entity<CourseRating>(entity =>
        {
            entity.Property(r => r.Rating).HasPrecision(4, 1);

            entity.HasData(
                new CourseRating
                {
                    Id = 1,
                    TeeColor = GolfRound.TeeColor.Red,
                    Gender = Gender.Male,
                    Rating = 66.2m,
                    SlopeRating = 116,
                    Source = "Club BAIST"
                },
                new CourseRating
                {
                    Id = 2,
                    TeeColor = GolfRound.TeeColor.Red,
                    Gender = Gender.Female,
                    Rating = 71.0m,
                    SlopeRating = 125,
                    Source = "Club BAIST"
                },
                new CourseRating
                {
                    Id = 3,
                    TeeColor = GolfRound.TeeColor.White,
                    Gender = Gender.Male,
                    Rating = 68.8m,
                    SlopeRating = 123,
                    Source = "Club BAIST"
                },
                new CourseRating
                {
                    Id = 4,
                    TeeColor = GolfRound.TeeColor.White,
                    Gender = Gender.Female,
                    Rating = 75.0m,
                    SlopeRating = 133,
                    Source = "Club BAIST"
                },
                new CourseRating
                {
                    Id = 5,
                    TeeColor = GolfRound.TeeColor.Blue,
                    Gender = Gender.Male,
                    Rating = 70.9m,
                    SlopeRating = 127,
                    Source = "Club BAIST"
                },
                new CourseRating
                {
                    Id = 6,
                    TeeColor = GolfRound.TeeColor.Blue,
                    Gender = Gender.Female,
                    Rating = 76.6m,
                    SlopeRating = 138,
                    Source = "Club BAIST"
                });
        });

        modelBuilder.Entity<CourseHole>(entity =>
        {
            var parByHole = new[] { 4, 5, 3, 4, 4, 4, 4, 5, 4, 4, 4, 3, 5, 4, 4, 3, 5, 4 };
            var strokeIndexByHole = Enumerable.Range(1, 18).ToArray();
            var seededHoles = new List<CourseHole>();
            var nextId = 1;

            foreach (var teeColor in Enum.GetValues<GolfRound.TeeColor>())
            {
                for (var i = 0; i < 18; i++)
                {
                    seededHoles.Add(new CourseHole
                    {
                        Id = nextId++,
                        TeeColor = teeColor,
                        HoleNumber = i + 1,
                        Par = parByHole[i],
                        StrokeIndex = strokeIndexByHole[i],
                        Source = "Club BAIST"
                    });
                }
            }

            entity.HasData(seededHoles);
        });
    }
}
