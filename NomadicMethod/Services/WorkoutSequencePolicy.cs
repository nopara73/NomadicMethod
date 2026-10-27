using NomadicMethod.Models;
using System.Numerics;

namespace NomadicMethod.Services;

public static class WorkoutSequencePolicy
{
    public static bool CanUseSingleDirection(Exercise exercise) =>
        exercise.DirectionSequence != ExerciseDirectionSequence.None &&
        !exercise.SideSequence.UsesTimedSides() &&
        exercise.SequenceBlocks.Length == 2 &&
        exercise.SequenceBlocks.All(block => block.ExerciseId == exercise.Id &&
            block.SideCue == ExerciseSequenceSideCue.None &&
            block.DirectionCue != ExerciseSequenceDirectionCue.None) &&
        exercise.SequenceBlocks[0].DirectionCue != exercise.SequenceBlocks[1].DirectionCue;

    public static int GetBlockCount(Exercise exercise, int minutes) =>
        minutes is > 0 and < 30 && CanUseSingleDirection(exercise)
            ? 1 : exercise.SequenceBlocks.Length;

    public static Exercise[] GetMembers(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);

        if (root.SequenceBlocks.Length == 0)
        {
            return [];
        }

        var members = new List<Exercise>();
        var seenIds = new HashSet<int>();
        foreach (ExerciseSequenceBlock block in root.SequenceBlocks)
        {
            if (!exercisesById.TryGetValue(block.ExerciseId, out Exercise? member))
            {
                return [];
            }
            if (seenIds.Add(member.Id))
            {
                members.Add(member);
            }
        }

        return members.ToArray();
    }

    public static WorkoutGroup[] GetPrimaryCoverageGroups(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);
        ArgumentNullException.ThrowIfNull(groups);

        if (root.SequenceBlocks.Length == 0)
        {
            return [];
        }

        var coveredGroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExerciseSequenceBlock block in root.SequenceBlocks)
        {
            if (!exercisesById.TryGetValue(block.ExerciseId, out Exercise? member))
            {
                return [];
            }

            WorkoutGroup[] primaryGroups = groups
                .Where(group => group.CanonicalGroups.Contains(
                    member.PrimaryCanonicalGroup))
                .ToArray();
            if (primaryGroups.Length != 1)
            {
                return [];
            }
            coveredGroupIds.Add(primaryGroups[0].Id);
        }

        return groups
            .Where(group => coveredGroupIds.Contains(group.Id))
            .OrderBy(group => group.Order)
            .ToArray();
    }

    public static int GetCanonicalCoverage(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Exercise[] members = GetMembers(root, exercisesById);
        return group.CanonicalGroups.Count(canonicalGroup =>
            members.Any(member => member.Trains(canonicalGroup)));
    }

    // Start with the feasible, preference-ranked lineup. Credit each selected
    // complete movement everywhere it trains before retaining another selection.
    // This keeps the exact matcher bounded; it does not enumerate anatomical subsets.
    public static IReadOnlyDictionary<string, int> ConsolidateCoverage(
        IReadOnlyDictionary<string, int> lineup,
        IReadOnlyList<WorkoutGroup> groups,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyDictionary<int, BigInteger> priorities,
        int minutes,
        Func<IReadOnlyDictionary<string, int>, MuscleBalanceEvaluation?>? evaluate = null,
        IReadOnlySet<int>? protectedRootIds = null)
    {
        var choices = lineup.Values.Distinct().Select(id => new
        {
            Root = exercisesById[id],
            Coverage = groups.Where(g => IsSelectable(exercisesById[id], exercisesById, g)).ToArray(),
            Priority = priorities.GetValueOrDefault(id),
            Order = groups.Where(g => lineup[g.Id] == id).Min(g => g.Order),
        }).OrderByDescending(c => c.Priority).ThenByDescending(c => c.Coverage.Length)
          .ThenBy(c => c.Order).ToArray();
        if (evaluate is not null)
        {
            IReadOnlyDictionary<string, int> current = lineup;
            MuscleBalanceEvaluation? balance = evaluate(current);
            while (balance is not null)
            {
                IReadOnlyDictionary<string, int>? best = null;
                MuscleBalanceEvaluation? bestBalance = null;
                foreach (int redundant in current.Values.Distinct())
                {
                    if (protectedRootIds?.Contains(redundant) == true) continue;
                    string[] reassigned = current.Where(e => e.Value == redundant).Select(e => e.Key).ToArray();
                    // Transfer coverage to the best retained preference; do not
                    // enumerate every equivalent anatomical ownership permutation.
                    var owner = choices.FirstOrDefault(c => c.Root.Id != redundant &&
                        current.Values.Contains(c.Root.Id) && c.Priority >= priorities.GetValueOrDefault(redundant) &&
                        reassigned.All(id => c.Coverage.Any(g => g.Id == id)));
                    if (owner is null) continue;
                    var candidate = new Dictionary<string, int>(current);
                    foreach (string id in reassigned) candidate[id] = owner.Root.Id;
                    MuscleBalanceEvaluation? candidateBalance = evaluate(candidate);
                    if (candidateBalance is null || WorkoutMuscleBalancePolicy.CompareWorkoutLoad(candidateBalance, balance) < 0 ||
                        bestBalance is not null && WorkoutMuscleBalancePolicy.CompareWorkoutLoad(candidateBalance, bestBalance) <= 0) continue;
                    best = candidate;
                    bestBalance = candidateBalance;
                }
                if (best is null) break;
                current = best;
                balance = bestBalance;
            }
            return current;
        }
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var costs = new List<int>();
        foreach (var choice in choices)
        {
            WorkoutGroup[] uncovered = choice.Coverage.Where(g => !result.ContainsKey(g.Id)).ToArray();
            if (uncovered.Length == 0) continue;
            foreach (WorkoutGroup group in uncovered) result[group.Id] = choice.Root.Id;
            costs.Add(choice.Root.SequenceBlocks.Length);
        }
        // Never shorten a sequence or lose exact duration when consolidation
        // removes the only sequence length that can fill the remaining budget.
        int remaining = minutes - costs.Sum();
        if (result.Count != groups.Count || remaining < 0) return lineup;
        var fillable = new bool[remaining + 1];
        fillable[0] = true;
        for (int value = 1; value <= remaining; value++)
            fillable[value] = costs.Any(cost => cost <= value && fillable[value - cost]);
        return fillable[remaining] ? result : lineup;
    }

    public static WorkoutGroup[][] GetPlacementOptions(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutGroup> groups,
        bool includeSharedCoverage = false)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);
        ArgumentNullException.ThrowIfNull(groups);
        if (root.SequenceBlocks.Length == 0)
        {
            return [];
        }

        WorkoutGroup[] eligibleAnchors = groups
            .Where(group => IsSelectable(root, exercisesById, group))
            .ToArray();
        WorkoutGroup[] primaryGroups = GetPrimaryCoverageGroups(
            root,
            exercisesById,
            groups);
        bool canClaimMultiplePrimarySlots = primaryGroups.Length > 1 &&
            primaryGroups.All(primaryGroup => eligibleAnchors.Any(anchor =>
                anchor.Id == primaryGroup.Id));

        IEnumerable<WorkoutGroup[]> options = eligibleAnchors
            .Select(anchor => canClaimMultiplePrimarySlots &&
                    primaryGroups.Any(primaryGroup =>
                        primaryGroup.Id == anchor.Id)
                ? primaryGroups
                : [anchor]);
        if (includeSharedCoverage && eligibleAnchors.Length > 1)
            options = options.Prepend(eligibleAnchors);
        return options.DistinctBy(option => string.Join(
                '|',
                option.OrderBy(group => group.Order).Select(group => group.Id)))
            .Select(option => option.OrderBy(group => group.Order).ToArray())
            .ToArray();
    }

    public static bool IsSelectable(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutGroup group)
    {
        Exercise[] members = GetMembers(root, exercisesById);
        return members.Length > 0 &&
            group.CanonicalGroups.Count(muscle => members.Any(member => member.Trains(muscle))) >=
                WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group);
    }
}
