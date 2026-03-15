using ClubBaist.Domain;
using ClubBaist.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClubBaist.Tests;

[TestClass]
public sealed class BookingPolicyServiceTests
{
    [TestMethod]
    public async Task EvaluateUpdateBookingAsync_BookingMissing_ReturnsPersistedStateFailure()
    {
        using var scope = TestServiceHost.CreateScope();
        var policyService = scope.ServiceProvider.GetRequiredService<BookingPolicyService<int>>();

        var request = BuildBookingRequest();

        var result = await policyService.EvaluateUpdateBookingAsync(request, Guid.NewGuid());

        Assert.AreEqual(ServiceResultStatus.Success, result.Status);
        Assert.IsFalse(result.Value!.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE, result.Value.DecisionCode);
        CollectionAssert.Contains(result.Value.Reasons.ToList(), "Booking does not exist or is no longer active.");
    }

    [TestMethod]
    public async Task EvaluateUpdateBookingAsync_UsesCreatePipelineForIntentReasonAggregation()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var policyService = provider.GetRequiredService<BookingPolicyService<int>>();

        var ownerId = Guid.NewGuid();
        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookingMemberAccountId = ownerId,
            SlotDate = new DateOnly(2027, 1, 10),
            SlotTime = new TimeOnly(8, 0),
            Status = ReservationStatus.Active
        };
        dbContext.Reservations.Add(reservation);
        dbContext.Entry(reservation).Property("IdempotencyKey").CurrentValue = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync();

        var request = BuildBookingRequest(ownerId, new DateOnly(2029, 1, 1),
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]);

        var createResult = await policyService.EvaluateCreateBookingAsync(request);
        var updateResult = await policyService.EvaluateUpdateBookingAsync(request, dbContext.Reservations.Single().ReservationId);

        Assert.IsFalse(createResult.Value!.Allowed);
        Assert.IsFalse(updateResult.Value!.Allowed);
        CollectionAssert.AreEquivalent(
            createResult.Value.Reasons.ToList(),
            updateResult.Value.Reasons.ToList());
        Assert.AreEqual(createResult.Value.DecisionCode, updateResult.Value.DecisionCode);
    }

    [TestMethod]
    public async Task EvaluateUpdateBookingAsync_WhenActorNotOwner_ReturnsForbidden()
    {
        using var scope = TestServiceHost.CreateScope();
        var provider = scope.ServiceProvider;
        var dbContext = provider.GetRequiredService<TestApplicationDbContext>();
        var seasonService = provider.GetRequiredService<SeasonService<int>>();
        var policyService = provider.GetRequiredService<BookingPolicyService<int>>();

        await seasonService.CreateSeasonAsync("Summer", new DateOnly(2027, 6, 1), new DateOnly(2027, 8, 31));

        var reservation = new Reservation
        {
            ReservationId = Guid.NewGuid(),
            BookingMemberAccountId = Guid.NewGuid(),
            SlotDate = new DateOnly(2027, 6, 15),
            SlotTime = new TimeOnly(9, 0),
            Status = ReservationStatus.Active
        };
        dbContext.Reservations.Add(reservation);
        dbContext.Entry(reservation).Property("IdempotencyKey").CurrentValue = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync();

        var request = BuildBookingRequest(Guid.NewGuid(), new DateOnly(2027, 6, 20), [Guid.NewGuid()]);

        var result = await policyService.EvaluateUpdateBookingAsync(request, reservation.ReservationId);

        Assert.IsFalse(result.Value!.Allowed);
        Assert.AreEqual(ReservationDecisionCodes.BOOKING_FORBIDDEN, result.Value.DecisionCode);
        CollectionAssert.Contains(result.Value.Reasons.ToList(), "Requesting member is not permitted to maintain this booking.");
    }

    private static BookingRequest BuildBookingRequest(
        Guid? memberId = null,
        DateOnly? playDate = null,
        IReadOnlyList<Guid>? playerIds = null)
    {
        var actor = memberId ?? Guid.NewGuid();
        var players = playerIds ?? [actor];

        return new BookingRequest(
            actor,
            playDate ?? new DateOnly(2027, 6, 15),
            new TimeOnly(9, 0),
            DateTimeOffset.UtcNow,
            players);
    }
}
