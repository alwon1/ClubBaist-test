using ClubBaist.Domain2;
using ClubBaist.Domain2.Entities.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubBaist.Services2.Scoring;

public sealed class CourseHoleReferenceService(IAppDbContext2 db, ILogger<CourseHoleReferenceService> logger)
{
    private static readonly IReadOnlyList<CourseHoleReference> FallbackHoles =
    [
        new(1, 4, 1), new(2, 5, 2), new(3, 3, 3), new(4, 4, 4), new(5, 4, 5), new(6, 4, 6),
        new(7, 4, 7), new(8, 5, 8), new(9, 4, 9), new(10, 4, 10), new(11, 4, 11), new(12, 3, 12),
        new(13, 5, 13), new(14, 4, 14), new(15, 4, 15), new(16, 3, 16), new(17, 5, 17), new(18, 4, 18)
    ];

    public async Task<IReadOnlyList<CourseHoleReference>> GetHoleReferencesAsync(
        GolfRound.TeeColor teeColor,
        CancellationToken ct = default)
    {
        var holes = await db.CourseHoles
            .Where(h => h.TeeColor == teeColor)
            .OrderBy(h => h.HoleNumber)
            .Select(h => new CourseHoleReference(h.HoleNumber, h.Par, h.StrokeIndex))
            .AsNoTracking()
            .ToListAsync(ct);

        if (holes.Count == 18)
        {
            return holes;
        }

        logger.LogWarning(
            "Course hole reference data incomplete for tee {TeeColor}. Expected 18 rows, found {Count}. Using fallback values.",
            teeColor,
            holes.Count);

        return FallbackHoles;
    }
}

public sealed record CourseHoleReference(int HoleNumber, int Par, int StrokeIndex);