using System.Numerics;

namespace NomadicMethod.Services;

internal sealed record WorkoutVarietyDeficiency(int GroupIndex, int DistinctMovementCount, int RequiredCount);

// Count complete, feasible movement families, after the caller applies anatomy,
// physical settings and every member's training-zone eligibility. A side, set,
// direction or alias never buys another choice.
internal static class WorkoutVarietyPolicy
{
    public const int RequiredMovementChoices = 5;
    public const string UnavailableMessage = "Some muscle groups have fewer than five options for these settings and your training zone.";

    public static IReadOnlyList<WorkoutVarietyDeficiency> FindDeficiencies(
        int groupCount,
        int workoutMinutes,
        IReadOnlyList<AtomicSequenceCandidate> sourceCandidates,
        int required = RequiredMovementChoices,
        CancellationToken cancellationToken = default,
        ulong groupsToValidate = ulong.MaxValue)
    {
        if (groupCount < 1 || groupCount > 63 || workoutMinutes < 1 || required < 1)
            throw new ArgumentOutOfRangeException(nameof(groupCount));

        AtomicSequenceCandidate[] candidates = sourceCandidates.Select(candidate => candidate with
        {
            UtilitiesByGroup = new BigInteger[groupCount],
        }).ToArray();
        var families = candidates.GroupBy(candidate => candidate.MovementId).ToArray();
        // Families with identical complete placement/cost sets are interchangeable
        // in a feasibility witness. Cache only that structural equivalence, never
        // anatomical names, ratings, preferences, or a guessed average block cost.
        var signatures = families.ToDictionary(family => family.Key, family => string.Join('|', family
            .Select(candidate => $"{candidate.CoverageMask}:{candidate.BlockCount}")
            .Distinct().Order(StringComparer.Ordinal)));
        var feasible = new Dictionary<string, bool>(StringComparer.Ordinal);
        var deficits = new List<WorkoutVarietyDeficiency>();
        for (int index = 0; index < groupCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ulong bit = 1UL << index;
            if ((groupsToValidate & bit) == 0) continue;
            int count = 0;
            foreach (var family in families)
            {
                bool selectable = false;
                foreach (AtomicSequenceCandidate pinned in family.Where(candidate => (candidate.CoverageMask & bit) != 0))
                {
                    string key = $"{signatures[family.Key]}@{pinned.CoverageMask}:{pinned.BlockCount}";
                    if (!feasible.TryGetValue(key, out bool fits))
                    {
                        AtomicSequenceCandidate[] remaining = candidates.Where(candidate =>
                            candidate.MovementId != pinned.MovementId &&
                            (candidate.CoverageMask & pinned.CoverageMask) == 0).Prepend(pinned).ToArray();
                        fits = AtomicSequenceLineupSolver.Solve(groupCount, workoutMinutes, remaining, cancellationToken) is not null;
                        feasible[key] = fits;
                    }
                    if (fits) { selectable = true; break; }
                }
                if (selectable && ++count >= required) break;
            }
            if (count < required) deficits.Add(new(index, count, required));
        }
        return deficits;
    }
}
