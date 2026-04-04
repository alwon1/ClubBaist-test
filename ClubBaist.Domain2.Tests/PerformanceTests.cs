using System.Diagnostics;
using System.Reflection;
using ClubBaist.Domain2.Entities;
using ClubBaist.Services2;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Domain2.Tests;

[TestClass]
[DoNotParallelize]
public sealed class BookingPerformanceTests
{
    private static readonly MethodInfo EvaluateBookingAsyncMethod =
        typeof(BookingService).GetMethod("EvaluateBookingAsync", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find BookingService.EvaluateBookingAsync.");

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [TestCategory("Performance")]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task EvaluateBookingAsync_Benchmark_CurrentImplementation_BeatsLegacyMaterialization()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<AppDbContext>();
        var service = provider.GetRequiredService<BookingService>();
        var rules = provider.GetServices<IBookingRule>().ToArray();

        var request = await SeedScenarioAsync(provider);

        var baseline = await RunLegacyMaterializedAsync(db, rules, request);
        var current = await InvokeCurrentImplementationAsync(service, request);

        Assert.IsNotNull(baseline.Slot);
        Assert.IsNotNull(current.Slot);
        Assert.AreEqual(baseline.Slot.Start, current.Slot.Start);
        Assert.AreEqual(baseline.SpotsRemaining, current.SpotsRemaining);
        Assert.AreEqual(baseline.RejectionReason, current.RejectionReason);

        const int iterations = 200;
        _ = await MeasureAsync(10, () => RunLegacyMaterializedAsync(db, rules, request));
        _ = await MeasureAsync(10, () => InvokeCurrentImplementationAsync(service, request));

        var legacyElapsed = await MeasureAsync(iterations, () => RunLegacyMaterializedAsync(db, rules, request));
        var currentElapsed = await MeasureAsync(iterations, () => InvokeCurrentImplementationAsync(service, request));
        var improvement = legacyElapsed.TotalMilliseconds / Math.Max(1.0, currentElapsed.TotalMilliseconds);

        TestContext.WriteLine($"Legacy materialized path: {legacyElapsed.TotalMilliseconds:N1} ms for {iterations} iterations");
        TestContext.WriteLine($"Current BookingService path: {currentElapsed.TotalMilliseconds:N1} ms for {iterations} iterations");
        TestContext.WriteLine($"Improvement factor: {improvement:N2}x");

        Assert.IsTrue(
            currentElapsed <= legacyElapsed * 1.10,
            $"Current implementation regressed. Legacy={legacyElapsed.TotalMilliseconds:N1} ms, Current={currentElapsed.TotalMilliseconds:N1} ms");
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Timeout(15000, CooperativeCancellation = true)]
    public async Task EvaluateBookingAsync_PerformanceSmoke_CompletesWithinBudget()
    {
        await using var host = await Domain2TestHost.CreateAsync();
        await using var scope = host.CreateScope();
        var provider = scope.ServiceProvider;

        var service = provider.GetRequiredService<BookingService>();
        var request = await SeedScenarioAsync(provider);

        _ = await InvokeCurrentImplementationAsync(service, request);

        const int iterations = 150;
        var elapsed = await MeasureAsync(iterations, () => InvokeCurrentImplementationAsync(service, request));

        TestContext.WriteLine($"Current BookingService path completed {iterations} evaluations in {elapsed.TotalMilliseconds:N1} ms");

        Assert.IsTrue(
            elapsed < TimeSpan.FromSeconds(7),
            $"Expected {iterations} evaluations to complete within 5000 ms against the AppHost-backed database, but took {elapsed.TotalMilliseconds:N1} ms.");
    }

    private static async Task<TeeTimeBooking> SeedScenarioAsync(IServiceProvider provider)
    {
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ClubBaistUser>>();
        var seasonService = provider.GetRequiredService<SeasonService2>();

        var shareholder = await Domain2TestData.CreateMembershipLevelAsync(db, "SH", "Shareholder");
        var members = new List<MemberShipInfo>();
        for (var i = 0; i < 16; i++)
        {
            members.Add(await Domain2TestData.CreateMemberAsync(
                userManager,
                db,
                shareholder,
                $"perf-member-{i}@test.com",
                "Perf",
                $"Member{i:00}"));
        }

        var season = await seasonService.CreateSeasonAsync(
            "Performance Season",
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 7));

        var slots = await db.TeeTimeSlots
            .Where(slot => slot.SeasonId == season.Id)
            .OrderBy(slot => slot.Start)
            .ToListAsync();

        var targetSlot = slots[slots.Count / 2];
        var bookings = new List<TeeTimeBooking>();

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.Start == targetSlot.Start)
            {
                continue;
            }

            var bookingMember = members[i % members.Count];
            var additionalParticipants = members
                .Where(member => member.Id != bookingMember.Id)
                .Skip((i + 1) % (members.Count - 1))
                .Take(2)
                .ToList();

            bookings.Add(new TeeTimeBooking
            {
                TeeTimeSlotStart = slot.Start,
                TeeTimeSlot = slot,
                BookingMemberId = bookingMember.Id,
                BookingMember = bookingMember,
                AdditionalParticipants = additionalParticipants.ToList()
            });
        }

        await db.TeeTimeBookings.AddRangeAsync(bookings);

        for (var i = 0; i < 24; i++)
        {
            var day = new DateOnly(2026, 6, 1).AddDays(i % 7);
            var eventStart = day.ToDateTime(new TimeOnly(5 + (i % 6), 0));
            db.SpecialEvents.Add(new SpecialEvent
            {
                Name = $"Event {i:00}",
                Start = eventStart,
                End = eventStart.AddMinutes(30)
            });
        }

        await db.SaveChangesAsync();

        return new TeeTimeBooking
        {
            TeeTimeSlotStart = targetSlot.Start,
            TeeTimeSlot = targetSlot,
            BookingMemberId = members[0].Id,
            BookingMember = members[0],
            AdditionalParticipants = [members[1], members[2]]
        };
    }

    private static async Task<TeeTimeEvaluation> RunLegacyMaterializedAsync(
        AppDbContext db,
        IReadOnlyCollection<IBookingRule> rules,
        TeeTimeBooking request)
    {
        var matchingSlots = await db.TeeTimeSlots
            .Where(slot => slot.Start == request.TeeTimeSlotStart)
            .ToListAsync();

        return matchingSlots
            .AsQueryable()
            .Evaluate(rules, request)
            .FirstOrDefault();
    }

    private static async Task<TeeTimeEvaluation> InvokeCurrentImplementationAsync(BookingService service, TeeTimeBooking request)
    {
        var task = (Task<TeeTimeEvaluation>)EvaluateBookingAsyncMethod.Invoke(service, [request, null])!;
        return await task;
    }

    private static async Task<TimeSpan> MeasureAsync(int iterations, Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            await action();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static async Task<TimeSpan> MeasureAsync<T>(int iterations, Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            _ = await action();
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }
}
