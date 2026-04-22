using ClubBaist.Domain2;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Web.Data;

internal sealed class DatabaseInitializerService(IServiceProvider services) : IHostedLifecycleService
{
    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(cancellationToken);
            await db.EnsureSqlServerSnapshotIsolationAsync();
            return;
        }

        var storeCreated = await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.EnsureSqlServerSnapshotIsolationAsync();
        await AppDbContextSeed.SeedAsync(scope.ServiceProvider, db, storeCreated, cancellationToken);
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
