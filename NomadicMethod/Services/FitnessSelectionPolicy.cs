namespace NomadicMethod.Services;

/// <summary>Shared training levels with an estimated training zone; unknown profiles bootstrap normally.</summary>
public static class FitnessSelectionPolicy
{
    public const string UnavailableMessage = "No complete workout fits your current training zone with these settings.";

    public static int[][] Zones(IReadOnlyList<FitnessProfileAxis> profile) =>
        profile.Select(axis => axis.Mean > 0 ? axis.BestLevels ?? [] : []).ToArray();

    public static bool IsEligible(int[][]? levels, IReadOnlyList<int[]> zones) =>
        !zones.Any(zone => zone.Length > 0) || levels is not null &&
        levels.Select((set, dimension) => set.Length > 0 && dimension < zones.Count &&
            set.Any(level => zones[dimension].Contains(level))).Any(matches => matches);

    // Every member must qualify, so a matching side cannot admit an unrated or
    // out-of-zone member through a complete atomic sequence.
    public static bool IsSequenceEligible(IEnumerable<int[][]?> members, IReadOnlyList<int[]> zones) =>
        members.All(levels => IsEligible(levels, zones));

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
