namespace NomadicMethod.Services;

/// <summary>Soft fitness preferences used only for otherwise tied choices.</summary>
public static class FitnessSelectionPolicy
{
    public static double Weight(int[][]? levels, IReadOnlyList<int> roundedProfile)
    {
        int distance = int.MaxValue;
        if (levels is not null)
        {
            for (int dimension = 0; dimension < levels.Length; dimension++)
            {
                int target = roundedProfile[dimension];
                if (target <= 0) continue;
                foreach (int level in levels[dimension])
                    distance = Math.Min(distance, Math.Abs(level - target));
            }
        }
        return distance == int.MaxValue ? 1d : Math.Pow(2, -distance);
    }

    // One family draw, then weighted variant draws. Repeated identities cannot
    // buy extra chances for their movement in the atomic lineup solver.
    public static T[] Order<T>(
        IEnumerable<T> candidates,
        Func<T, int> movementId,
        Func<T, double> weight,
        Func<double> random)
    {
        var families = candidates.Select((item, index) =>
            (Item: item, Index: index, Weight: weight(item)))
            .GroupBy(entry => movementId(entry.Item))
            .Select(family =>
            {
                var variants = family.ToArray();
                double key = Key(variants.Max(entry => entry.Weight), random);
                var ordered = variants.Length == 1 ? variants :
                    variants.Select(entry => (Entry: entry, Key: Key(entry.Weight, random)))
                        .OrderBy(entry => entry.Key)
                        .ThenBy(entry => entry.Entry.Index)
                        .Select(entry => entry.Entry).ToArray();
                return (Key: key, Index: variants[0].Index, Variants: ordered);
            })
            .OrderBy(family => family.Key)
            .ThenBy(family => family.Index);
        return families.SelectMany(family => family.Variants)
            .Select(entry => entry.Item).ToArray();
    }

    private static double Key(double weight, Func<double> random)
    {
        double unit = random();
        if (!double.IsFinite(weight) || weight <= 0 ||
            !double.IsFinite(unit) || unit < 0 || unit >= 1)
            throw new ArgumentOutOfRangeException(nameof(random));
        // Random supplies [0,1); 1-unit gives U in (0,1], avoiding log(0).
        return -Math.Log(1 - unit) / weight;
    }
}
