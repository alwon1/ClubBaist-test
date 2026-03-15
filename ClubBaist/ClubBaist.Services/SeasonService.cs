using ClubBaist.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClubBaist.Services;

public class SeasonService<TKey> where TKey : IEquatable<TKey>
{
    public const string SeasonOverlapConflictCode = "SEASON_OVERLAP";
    public const string SeasonAlreadyClosedConflictCode = "SEASON_ALREADY_CLOSED";
    public const string SeasonDuplicateNameConflictCode = "SEASON_DUPLICATE_NAME";

    private readonly IApplicationDbContext<TKey> _dbContext;

    public SeasonService(IApplicationDbContext<TKey> dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<ServiceResult<Season>> CreateSeasonAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidateSeasonInput(name, startDate, endDate);
        if (validationErrors.Count > 0)
        {
            return ServiceResult<Season>.ValidationFailed(validationErrors);
        }

        var trimmedName = name.Trim();

        var nameAlreadyExists = await _dbContext.Seasons
            .AnyAsync(season => season.Name == trimmedName, cancellationToken);

        if (nameAlreadyExists)
        {
            return ServiceResult<Season>.Conflict(
                SeasonDuplicateNameConflictCode,
                "A season with this name already exists.");
        }

        var overlapsExistingSeason = await _dbContext.Seasons
            .AnyAsync(
                season => startDate <= season.EndDate && endDate >= season.StartDate,
                cancellationToken);

        if (overlapsExistingSeason)
        {
            return ServiceResult<Season>.Conflict(
                SeasonOverlapConflictCode,
                "Season dates overlap an existing season.");
        }

        var season = new Season
        {
            Name = trimmedName,
            StartDate = startDate,
            EndDate = endDate,
            SeasonStatus = SeasonStatus.Planned
        };

        _dbContext.Seasons.Add(season);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Season>.Success(season);
    }

    public async Task<ServiceResult<Season?>> GetSeasonForDateAsync(
        DateOnly playDate,
        CancellationToken cancellationToken = default)
    {
        var season = await _dbContext.Seasons
            .AsNoTracking()
            .Where(item => item.SeasonStatus != SeasonStatus.Closed)
            .Where(item => item.StartDate <= playDate && item.EndDate >= playDate)
            .OrderBy(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        return ServiceResult<Season?>.Success(season);
    }

    public Task<ServiceResult<Season?>> GetCurrentSeasonAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        return GetSeasonForDateAsync(today, cancellationToken);
    }

    public async Task<ServiceResult<Season>> CloseSeasonAsync(
        Guid seasonId,
        DateOnly closedOn,
        CancellationToken cancellationToken = default)
    {
        var season = await _dbContext.Seasons
            .FirstOrDefaultAsync(item => item.SeasonId == seasonId, cancellationToken);

        if (season is null)
        {
            return ServiceResult<Season>.ValidationFailed(["Season was not found."]);
        }

        if (season.SeasonStatus == SeasonStatus.Closed)
        {
            return ServiceResult<Season>.Conflict(
                SeasonAlreadyClosedConflictCode,
                "Season is already closed.");
        }

        season.SeasonStatus = SeasonStatus.Closed;

        if (closedOn < season.EndDate)
        {
            season.EndDate = closedOn;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<Season>.Success(season);
    }

    private static List<string> ValidateSeasonInput(string name, DateOnly startDate, DateOnly endDate)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Name is required.");
        }

        if (startDate > endDate)
        {
            errors.Add("Start date must be on or before end date.");
        }

        return errors;
    }
}

public enum ServiceResultStatus
{
    Success = 0,
    Validation = 1,
    Conflict = 2
}

public sealed record ServiceResult<T>(
    ServiceResultStatus Status,
    T? Value = default,
    IReadOnlyList<string>? ValidationErrors = null,
    string? ConflictCode = null,
    string? ConflictMessage = null)
{
    public bool IsSuccess => Status == ServiceResultStatus.Success;

    public static ServiceResult<T> Success(T value) =>
        new(ServiceResultStatus.Success, value);

    public static ServiceResult<T> ValidationFailed(IReadOnlyList<string> errors) =>
        new(ServiceResultStatus.Validation, default, errors);

    public static ServiceResult<T> Conflict(string code, string message) =>
        new(ServiceResultStatus.Conflict, default, null, code, message);
}
