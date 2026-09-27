using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutCoveragePolicy
{
    public static int GetCanonicalCoverage(
        Exercise exercise,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        return group.CanonicalGroups.Count(exercise.Trains);
    }

    public static int GetRequiredCanonicalCoverage(WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (group.CanonicalGroups.Count == 0)
        {
            throw new ArgumentException(
                "A workout group must contain at least one canonical group.",
                nameof(group));
        }

        // Restore the original trained-muscle membership rule. A group offers
        // alternative targets; one exercise need not train half its subdivisions.
        return 1;
    }

    public static bool IsSelectable(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        return GetCanonicalCoverage(exercise, group) >=
            GetRequiredCanonicalCoverage(group);
    }

    public static bool IsPrimaryForGroup(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);
        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup);
    }
}
