namespace NomadicMethod.Services;

/// <summary>Exhaust +1 choices before current and progressively lower levels.</summary>
public static class FitnessSelectionPolicy
{
    public const int UnknownTier = 6;
    public const int TierCount = 10;

    // Ascending tiers: +1, current, -1, -2, -3, -4, unknown, +2, +3, +4.
    // Unknowns and larger jumps remain fallbacks for complete coverage. A range
    // supports only its explicit members; it does not imply a harder execution.
    public static int Tier(int[][]? levels, IReadOnlyList<int> roundedProfile)
    {
        int tier = int.MaxValue;
        if (levels is not null)
        {
            for (int dimension = 0; dimension < Math.Min(levels.Length, roundedProfile.Count); dimension++)
            {
                int target = roundedProfile[dimension];
                if (target <= 0) continue;
                foreach (int level in levels[dimension])
                {
                    int difference = level - target;
                    int candidateTier = difference == 1 ? 0 : difference <= 0
                        ? 1 - difference : 5 + difference;
                    tier = Math.Min(tier, candidateTier);
                }
            }
        }
        return tier == int.MaxValue ? UnknownTier : tier;
    }

    public static int SequenceTier(IEnumerable<int[][]?> members, IReadOnlyList<int> roundedProfile) =>
        members.Select(levels => Tier(levels, roundedProfile)).Where(tier => tier != UnknownTier)
            .DefaultIfEmpty(UnknownTier).Min();

    // One uniform draw per family, reused across its tiers and placements.
    // Variant draws break ties inside a family, never buy it extra chances.
    public static T[] Order<T>(
        IEnumerable<T> candidates,
        Func<T, int> movementId,
        Func<T, int> tier,
        Func<double> random)
    {
        return candidates.Select((item, index) =>
            (Item: item, Index: index, Tier: tier(item)))
            .GroupBy(entry => movementId(entry.Item))
            .SelectMany(family =>
            {
                var variants = family.ToArray();
                double familyKey = Draw(random);
                return variants.Select(entry => (Entry: entry, FamilyKey: familyKey,
                    FamilyIndex: variants[0].Index,
                    VariantKey: variants.Length == 1 ? 0 : Draw(random))).ToArray();
            })
            .OrderBy(entry => entry.Entry.Tier)
            .ThenBy(entry => entry.FamilyKey)
            .ThenBy(entry => entry.FamilyIndex)
            .ThenBy(entry => entry.VariantKey)
            .ThenBy(entry => entry.Entry.Index)
            .Select(entry => entry.Entry.Item).ToArray();
    }

    private static double Draw(Func<double> random)
    {
        double unit = random();
        if (!double.IsFinite(unit) || unit < 0 || unit >= 1)
            throw new ArgumentOutOfRangeException(nameof(random));
        return unit;
    }
}
