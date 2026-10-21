using NomadicMethod.Models;

namespace NomadicMethod.Services;

public readonly record struct ReactionCueTiming(
    int MinimumMoveMilliseconds,
    int MaximumMoveMilliseconds,
    int MinimumFreezeMilliseconds,
    int MaximumFreezeMilliseconds);

public static class ReactionCuePolicy
{
    public const int IntroducedCatalogRevision = 87;

    private static readonly ReactionCueTiming[] Timings =
    [
        new(5_500, 8_000, 1_600, 2_200),
        new(4_000, 6_500, 1_200, 1_800),
        new(3_000, 5_000, 900, 1_400),
        new(2_000, 3_500, 700, 1_100),
        new(1_200, 2_500, 450, 850),
    ];

    public static bool IsEnabled(Exercise exercise, int activeCatalogRevision) =>
        activeCatalogRevision >= IntroducedCatalogRevision &&
        exercise.ReactionCueLevel is >= 1 and <= 5;

    public static ReactionCueTiming GetTiming(int level)
    {
        if (level is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        return Timings[level - 1];
    }

    public static int GetNextDurationMilliseconds(
        int level,
        bool moving,
        double randomUnitInterval)
    {
        if (randomUnitInterval is < 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(randomUnitInterval));
        }

        ReactionCueTiming timing = GetTiming(level);
        int minimum = moving
            ? timing.MinimumMoveMilliseconds
            : timing.MinimumFreezeMilliseconds;
        int maximum = moving
            ? timing.MaximumMoveMilliseconds
            : timing.MaximumFreezeMilliseconds;
        return minimum + (int)Math.Floor(
            randomUnitInterval * (maximum - minimum + 1));
    }
}
