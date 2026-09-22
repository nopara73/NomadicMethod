using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutCoveragePolicy
{
    public const int MinimumCoveragePercent = 50;

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

        return (group.CanonicalGroups.Count * MinimumCoveragePercent + 99) / 100;
    }

    public static bool IsSelectable(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        return GetCanonicalCoverage(exercise, group) >=
            GetRequiredCanonicalCoverage(group) || IsRegionalCompound(exercise, group);
    }

    public static bool IsRegionalCompound(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);

        if (exercise.Mode != ExerciseMode.Repetition ||
            exercise.Presentation != ExercisePresentation.Motion)
        {
            return false;
        }

        // Fine anatomical subdivisions are not equally sized pieces of a broad
        // workout. A shoulder-to-elbow movement remains compound even though
        // the upper-limb bucket also contains separate hand, face and neck leaves.
        // Both joints must receive reviewed training, with a proximal main target.
        if (group.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAbductors) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAdductorsAndExtensors) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.RotatorCuff) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.ElbowFlexors) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.ElbowExtensors))
        {
            return (IsShoulderTarget(exercise.PrimaryCanonicalGroup) ||
                    IsElbowTarget(exercise.PrimaryCanonicalGroup)) &&
                (exercise.Trains(CanonicalMuscleGroup.ShoulderAbductors) ||
                    exercise.Trains(CanonicalMuscleGroup.ShoulderAdductorsAndExtensors) ||
                    exercise.Trains(CanonicalMuscleGroup.RotatorCuff) ||
                    exercise.Trains(CanonicalMuscleGroup.ScapularGirdle) ||
                    exercise.Trains(CanonicalMuscleGroup.Chest)) &&
                (exercise.Trains(CanonicalMuscleGroup.ElbowFlexors) ||
                    exercise.Trains(CanonicalMuscleGroup.ElbowExtensors));
        }

        // A broad torso round can directly train its front and back without
        // adding an incidental breathing, pelvic-floor or stabilization claim.
        if (group.CanonicalGroups.Contains(CanonicalMuscleGroup.SpinalExtensors) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.DeepAndIntersegmentalBack) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.AbdominalWall) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.Chest) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.BreathingMuscles) &&
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.PelvicFloorAndPerineum))
        {
            return (IsAnteriorTrunkTarget(exercise.PrimaryCanonicalGroup) ||
                    IsPosteriorTrunkTarget(exercise.PrimaryCanonicalGroup)) &&
                (exercise.Trains(CanonicalMuscleGroup.AbdominalWall) ||
                    exercise.Trains(CanonicalMuscleGroup.Chest)) &&
                (exercise.Trains(CanonicalMuscleGroup.SpinalExtensors) ||
                    exercise.Trains(CanonicalMuscleGroup.DeepAndIntersegmentalBack));
        }

        return false;
    }

    private static bool IsShoulderTarget(CanonicalMuscleGroup muscle) => muscle is
        CanonicalMuscleGroup.ShoulderAbductors or
        CanonicalMuscleGroup.ShoulderAdductorsAndExtensors or
        CanonicalMuscleGroup.RotatorCuff or
        CanonicalMuscleGroup.ScapularGirdle or
        CanonicalMuscleGroup.Chest;

    private static bool IsElbowTarget(CanonicalMuscleGroup muscle) => muscle is
        CanonicalMuscleGroup.ElbowFlexors or CanonicalMuscleGroup.ElbowExtensors;

    private static bool IsAnteriorTrunkTarget(CanonicalMuscleGroup muscle) => muscle is
        CanonicalMuscleGroup.AbdominalWall or CanonicalMuscleGroup.Chest;

    private static bool IsPosteriorTrunkTarget(CanonicalMuscleGroup muscle) => muscle is
        CanonicalMuscleGroup.SpinalExtensors or CanonicalMuscleGroup.DeepAndIntersegmentalBack;

    public static bool IsPrimaryForGroup(Exercise exercise, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);
        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup);
    }
}
