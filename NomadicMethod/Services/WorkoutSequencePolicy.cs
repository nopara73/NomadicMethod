using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutSequencePolicy
{
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

    public static WorkoutGroup[][] GetPlacementOptions(
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

        return eligibleAnchors
            .Select(anchor => canClaimMultiplePrimarySlots &&
                    primaryGroups.Any(primaryGroup =>
                        primaryGroup.Id == anchor.Id)
                ? primaryGroups
                : [anchor])
            .DistinctBy(option => string.Join(
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
            (group.CanonicalGroups.Count(muscle => members.Any(member => member.Trains(muscle))) >=
                WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group) ||
                members.All(member => WorkoutCoveragePolicy.IsRegionalCompound(member, group)));
    }
}
