using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class BookingPolicyService<TKey> where TKey : IEquatable<TKey>
{
    private readonly IApplicationDbContext<TKey> _dbContext;
    private readonly IReadOnlyList<IBookingPolicyRule<TKey>> _rules;

    public BookingPolicyService(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

        _rules =
        [
            new SeasonWindowBookingPolicyRule<TKey>(),
            new PlayerCountRangeBookingPolicyRule<TKey>(),
            new BookingMemberActiveBookingPolicyRule<TKey>(),
            new MembershipCategoryTimeWindowBookingPolicyRule<TKey>()
        ];
    }

    public async Task<BookingPolicyDecision> EvaluateCreateBookingAsync(
        BookingPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = new BookingPolicyRuleContext<TKey>(_dbContext, request);
        var failures = new List<BookingPolicyRuleFailure>();

        foreach (var rule in _rules)
        {
            var result = await rule.EvaluateAsync(context, cancellationToken);
            if (result is not null)
            {
                failures.Add(result);
            }
        }

        if (failures.Count == 0)
        {
            return new BookingPolicyDecision(true, ReservationDecisionCodes.BOOKING_ALLOWED, []);
        }

        var primaryDecisionCode = DeterminePrimaryDecisionCode(failures);
        var reasons = failures
            .Select(failure => $"{failure.DecisionCode}: {failure.Reason}")
            .ToArray();

        return new BookingPolicyDecision(false, primaryDecisionCode, reasons);
    }

    private static ReservationDecisionCodes DeterminePrimaryDecisionCode(
        IReadOnlyCollection<BookingPolicyRuleFailure> failures)
    {
        var decisionPriority = new Dictionary<ReservationDecisionCodes, int>
        {
            [ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION] = 0,
            [ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE] = 1,
            [ReservationDecisionCodes.BOOKING_FORBIDDEN] = 2,
            [ReservationDecisionCodes.BOOKING_NOT_FOUND_OR_NOT_ACTIVE] = 3,
            [ReservationDecisionCodes.BOOKING_ALLOWED] = 4
        };

        return failures
            .OrderBy(failure => decisionPriority[failure.DecisionCode])
            .ThenBy(failure => failure.Reason, StringComparer.Ordinal)
            .Select(failure => failure.DecisionCode)
            .First();
    }
}

public sealed record BookingPolicyRequest(
    Guid BookingMemberAccountId,
    DateOnly PlayDate,
    TimeOnly TeeTime,
    IReadOnlyCollection<Guid> PlayerMemberAccountIds);

public sealed record BookingPolicyDecision(
    bool Allowed,
    ReservationDecisionCodes DecisionCode,
    IReadOnlyList<string> Reasons);

internal interface IBookingPolicyRule<TKey> where TKey : IEquatable<TKey>
{
    Task<BookingPolicyRuleFailure?> EvaluateAsync(
        BookingPolicyRuleContext<TKey> context,
        CancellationToken cancellationToken);
}

internal sealed record BookingPolicyRuleFailure(
    ReservationDecisionCodes DecisionCode,
    string Reason);

internal sealed class BookingPolicyRuleContext<TKey> where TKey : IEquatable<TKey>
{
    private MemberAccount<TKey>? _bookingMember;
    private Dictionary<Guid, MemberAccount<TKey>>? _players;

    public BookingPolicyRuleContext(IApplicationDbContext<TKey> dbContext, BookingPolicyRequest request)
    {
        DbContext = dbContext;
        Request = request;
    }

    public IApplicationDbContext<TKey> DbContext { get; }
    public BookingPolicyRequest Request { get; }

    public async Task<MemberAccount<TKey>?> GetBookingMemberAsync(CancellationToken cancellationToken)
    {
        if (_bookingMember is not null)
        {
            return _bookingMember;
        }

        _bookingMember = await DbContext.MemberAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                member => member.MemberAccountId == Request.BookingMemberAccountId,
                cancellationToken);

        return _bookingMember;
    }

    public async Task<IReadOnlyDictionary<Guid, MemberAccount<TKey>>> GetPlayersByIdAsync(CancellationToken cancellationToken)
    {
        if (_players is not null)
        {
            return _players;
        }

        var requestedPlayerIds = Request.PlayerMemberAccountIds
            .Distinct()
            .ToArray();

        var players = await DbContext.MemberAccounts
            .AsNoTracking()
            .Where(member => requestedPlayerIds.Contains(member.MemberAccountId))
            .ToListAsync(cancellationToken);

        _players = players.ToDictionary(player => player.MemberAccountId, player => player);

        return _players;
    }
}

internal sealed class SeasonWindowBookingPolicyRule<TKey> : IBookingPolicyRule<TKey> where TKey : IEquatable<TKey>
{
    public async Task<BookingPolicyRuleFailure?> EvaluateAsync(
        BookingPolicyRuleContext<TKey> context,
        CancellationToken cancellationToken)
    {
        var isInSeason = await context.DbContext.Seasons
            .AsNoTracking()
            .AnyAsync(
                season => season.SeasonStatus != SeasonStatus.Closed
                    && season.StartDate <= context.Request.PlayDate
                    && season.EndDate >= context.Request.PlayDate,
                cancellationToken);

        return isInSeason
            ? null
            : new BookingPolicyRuleFailure(
                ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION,
                "Requested play date is outside the active season window.");
    }
}

internal sealed class PlayerCountRangeBookingPolicyRule<TKey> : IBookingPolicyRule<TKey> where TKey : IEquatable<TKey>
{
    private const int MinPlayers = 1;
    private const int MaxPlayers = SlotOccupancy.MaxCapacity;

    public Task<BookingPolicyRuleFailure?> EvaluateAsync(
        BookingPolicyRuleContext<TKey> context,
        CancellationToken cancellationToken)
    {
        var playerCount = context.Request.PlayerMemberAccountIds.Count;
        if (playerCount >= MinPlayers && playerCount <= MaxPlayers)
        {
            return Task.FromResult<BookingPolicyRuleFailure?>(null);
        }

        return Task.FromResult<BookingPolicyRuleFailure?>(
            new BookingPolicyRuleFailure(
                ReservationDecisionCodes.PLAYER_COUNT_OUT_OF_RANGE,
                $"Player count must be between {MinPlayers} and {MaxPlayers}."));
    }
}

internal sealed class BookingMemberActiveBookingPolicyRule<TKey> : IBookingPolicyRule<TKey> where TKey : IEquatable<TKey>
{
    public async Task<BookingPolicyRuleFailure?> EvaluateAsync(
        BookingPolicyRuleContext<TKey> context,
        CancellationToken cancellationToken)
    {
        var bookingMember = await context.GetBookingMemberAsync(cancellationToken);

        if (bookingMember is null)
        {
            return new BookingPolicyRuleFailure(
                ReservationDecisionCodes.BOOKING_FORBIDDEN,
                "Booking member account was not found.");
        }

        return bookingMember.IsActive
            ? null
            : new BookingPolicyRuleFailure(
                ReservationDecisionCodes.BOOKING_FORBIDDEN,
                "Booking member account is inactive.");
    }
}

internal sealed class MembershipCategoryTimeWindowBookingPolicyRule<TKey> : IBookingPolicyRule<TKey> where TKey : IEquatable<TKey>
{
    public async Task<BookingPolicyRuleFailure?> EvaluateAsync(
        BookingPolicyRuleContext<TKey> context,
        CancellationToken cancellationToken)
    {
        // Business rule choice: validate the tee-time window for every named player in the reservation.
        var playersById = await context.GetPlayersByIdAsync(cancellationToken);

        foreach (var playerId in context.Request.PlayerMemberAccountIds.Distinct())
        {
            if (!playersById.TryGetValue(playerId, out var player))
            {
                return new BookingPolicyRuleFailure(
                    ReservationDecisionCodes.BOOKING_FORBIDDEN,
                    $"Player member account '{playerId}' was not found.");
            }

            var (start, end) = GetWindow(player.MembershipCategory);
            var teeTime = context.Request.TeeTime;
            var isAllowed = teeTime >= start && teeTime <= end;
            if (!isAllowed)
            {
                return new BookingPolicyRuleFailure(
                    ReservationDecisionCodes.BOOKING_WINDOW_VIOLATION,
                    $"Membership category '{player.MembershipCategory}' allows tee times from {start:HH\\:mm} to {end:HH\\:mm}.");
            }
        }

        return null;
    }

    private static (TimeOnly Start, TimeOnly End) GetWindow(MembershipCategory category)
    {
        return category switch
        {
            MembershipCategory.PeeWee => (new TimeOnly(12, 0), new TimeOnly(16, 0)),
            MembershipCategory.Junior => (new TimeOnly(10, 0), new TimeOnly(17, 0)),
            MembershipCategory.Intermediate => (new TimeOnly(7, 0), new TimeOnly(18, 0)),
            MembershipCategory.Social => (new TimeOnly(13, 0), new TimeOnly(17, 0)),
            _ => (new TimeOnly(6, 0), new TimeOnly(20, 0))
        };
    }
}
