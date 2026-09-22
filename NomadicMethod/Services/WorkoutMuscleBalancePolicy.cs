using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutMuscleBalancePolicy
{
    // Eighth-units represent the complete 0.125-granularity workload table
    // exactly, without floating-point comparisons or persisted derived scores.
    public const int MinimumPrimaryLoadEighthUnits = 2;

    public const int MinimumSecondaryLoadEighthUnits = 1;

    public const int ModeratePrimaryLoadEighthUnits = 4;

    public const int ModerateSecondaryLoadEighthUnits = 2;

    public const int HardPrimaryLoadEighthUnits = 8;

    public const int HardSecondaryLoadEighthUnits = 4;

    public const int MinimumBalancedShareNumerator = 1;

    public const int MinimumBalancedShareDenominator = 4;

    public const int MaximumRebalancePasses = 30;

    public static IReadOnlyDictionary<CanonicalMuscleGroup, int>
        CalculateCanonicalLoadEighthUnits(
            IEnumerable<Exercise> scheduledExercises)
    {
        ArgumentNullException.ThrowIfNull(scheduledExercises);

        var result = new Dictionary<CanonicalMuscleGroup, int>();
        foreach (Exercise exercise in scheduledExercises)
        {
            ArgumentNullException.ThrowIfNull(exercise);
            AddExerciseLoad(result, exercise, setCount: 1);
        }

        return result;
    }

    public static void AddExerciseLoad(
        IDictionary<CanonicalMuscleGroup, int> loadEighthUnits,
        Exercise exercise,
        int setCount)
    {
        ArgumentNullException.ThrowIfNull(loadEighthUnits);
        ArgumentNullException.ThrowIfNull(exercise);
        if (setCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(setCount),
                setCount,
                "Set count must be positive.");
        }

        int primaryLoad = GetPrimaryLoadEighthUnits(exercise) * setCount;
        loadEighthUnits.TryGetValue(
            exercise.PrimaryCanonicalGroup,
            out int existingPrimaryLoad);
        loadEighthUnits[exercise.PrimaryCanonicalGroup] =
            existingPrimaryLoad + primaryLoad;

        int secondaryLoad = GetSecondaryLoadEighthUnits(exercise) * setCount;
        foreach (CanonicalMuscleGroup secondary in
                 exercise.SecondaryCanonicalGroups.Distinct())
        {
            loadEighthUnits.TryGetValue(secondary, out int existingSecondaryLoad);
            loadEighthUnits[secondary] = existingSecondaryLoad + secondaryLoad;
        }
    }

    public static int GetPrimaryLoadEighthUnits(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return exercise.MuscularDemand switch
        {
            Exercise.MinimumMuscularDemand => MinimumPrimaryLoadEighthUnits,
            Exercise.ModerateMuscularDemand => ModeratePrimaryLoadEighthUnits,
            Exercise.MaximumMuscularDemand => HardPrimaryLoadEighthUnits,
            _ => throw new ArgumentOutOfRangeException(
                nameof(exercise.MuscularDemand),
                exercise.MuscularDemand,
                "Muscular demand must be between 0 and 2."),
        };
    }

    public static int GetSecondaryLoadEighthUnits(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return exercise.MuscularDemand switch
        {
            Exercise.MinimumMuscularDemand => MinimumSecondaryLoadEighthUnits,
            Exercise.ModerateMuscularDemand => ModerateSecondaryLoadEighthUnits,
            Exercise.MaximumMuscularDemand => HardSecondaryLoadEighthUnits,
            _ => throw new ArgumentOutOfRangeException(
                nameof(exercise.MuscularDemand),
                exercise.MuscularDemand,
                "Muscular demand must be between 0 and 2."),
        };
    }

    public static MuscleBalanceEvaluation Evaluate(
        IReadOnlyDictionary<CanonicalMuscleGroup, int>
            canonicalLoadEighthUnits)
    {
        ArgumentNullException.ThrowIfNull(canonicalLoadEighthUnits);

        MuscleResolutionBalance[] resolutions = MassGroupingTaxonomy
            .SupportedMinutes
            .Select(minutes =>
            {
                WorkoutResolution resolution =
                    MassGroupingTaxonomy.GetResolution(minutes);
                Dictionary<string, int> groupLoads = resolution.Groups
                    .ToDictionary(
                        group => group.Id,
                        group => group.CanonicalGroups.Sum(canonicalGroup =>
                            canonicalLoadEighthUnits.GetValueOrDefault(
                                canonicalGroup)),
                        StringComparer.Ordinal);
                return new MuscleResolutionBalance(
                    minutes,
                    groupLoads,
                    groupLoads.Values.Min(),
                    groupLoads.Values.Max());
            })
            .ToArray();
        return new MuscleBalanceEvaluation(resolutions);
    }

    public static int Compare(
        MuscleBalanceEvaluation left,
        MuscleBalanceEvaluation right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Resolutions.Count != right.Resolutions.Count)
        {
            throw new ArgumentException(
                "Muscle-balance evaluations must cover the same resolutions.");
        }

        MuscleResolutionBalance[] leftOrdered = left.Resolutions
            .OrderBy(balance => balance, ResolutionShareComparer.Instance)
            .ThenBy(balance => balance.Minutes)
            .ToArray();
        MuscleResolutionBalance[] rightOrdered = right.Resolutions
            .OrderBy(balance => balance, ResolutionShareComparer.Instance)
            .ThenBy(balance => balance.Minutes)
            .ToArray();
        for (int index = 0; index < leftOrdered.Length; index++)
        {
            int comparison = CompareShare(
                leftOrdered[index],
                rightOrdered[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static int CompareShare(
        MuscleResolutionBalance left,
        MuscleResolutionBalance right)
    {
        (int leftNumerator, int leftDenominator) = left.ShareFraction;
        (int rightNumerator, int rightDenominator) = right.ShareFraction;
        return ((long)leftNumerator * rightDenominator)
            .CompareTo((long)rightNumerator * leftDenominator);
    }

    private sealed class ResolutionShareComparer :
        IComparer<MuscleResolutionBalance>
    {
        public static ResolutionShareComparer Instance { get; } = new();

        public int Compare(
            MuscleResolutionBalance? left,
            MuscleResolutionBalance? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left is null)
            {
                return -1;
            }
            if (right is null)
            {
                return 1;
            }

            return CompareShare(left, right);
        }
    }
}

public sealed record MuscleResolutionBalance(
    int Minutes,
    IReadOnlyDictionary<string, int> LoadEighthUnitsByGroupId,
    int WeakestLoadEighthUnits,
    int StrongestLoadEighthUnits)
{
    public (int Numerator, int Denominator) ShareFraction =>
        StrongestLoadEighthUnits == 0
            ? (1, 1)
            : (WeakestLoadEighthUnits, StrongestLoadEighthUnits);

    public bool IsBalanced =>
        StrongestLoadEighthUnits == 0 ||
        (long)WeakestLoadEighthUnits *
            WorkoutMuscleBalancePolicy.MinimumBalancedShareDenominator >=
        (long)StrongestLoadEighthUnits *
            WorkoutMuscleBalancePolicy.MinimumBalancedShareNumerator;
}

public sealed record MuscleBalanceEvaluation(
    IReadOnlyList<MuscleResolutionBalance> Resolutions)
{
    public bool IsBalanced => Resolutions.All(resolution => resolution.IsBalanced);
}
