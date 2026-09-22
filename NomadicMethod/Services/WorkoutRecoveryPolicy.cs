using NomadicMethod.Models;

namespace NomadicMethod.Services;

public enum HardExerciseRotationStatus
{
    RecoveringHard,
    Neutral,
    FreshHard,
}

public static class WorkoutRecoveryPolicy
{
    public const int ModerateMuscularDemand = Exercise.ModerateMuscularDemand;

    public const int HardMuscularDemand = Exercise.MaximumMuscularDemand;

    public const long ModerateRecoveryWindowMilliseconds =
        18L * 60L * 60L * 1000L;

    public const long HardRecoveryWindowMilliseconds =
        36L * 60L * 60L * 1000L;

    public static bool IsModerateExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.MuscularDemand == ModerateMuscularDemand;
    }

    public static bool IsHardExercise(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.MuscularDemand == HardMuscularDemand;
    }

    public static long GetLastHardWorkUnixMilliseconds(
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle)
    {
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);
        return GetLastWorkUnixMilliseconds(
            lastHardWorkByPrimaryMuscle,
            primaryMuscle);
    }

    public static long GetLastMeaningfulWorkUnixMilliseconds(
        IReadOnlyDictionary<string, long> lastMeaningfulWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle)
    {
        ArgumentNullException.ThrowIfNull(lastMeaningfulWorkByPrimaryMuscle);
        return GetLastWorkUnixMilliseconds(
            lastMeaningfulWorkByPrimaryMuscle,
            primaryMuscle);
    }

    public static bool IsPrimaryMuscleRecovering(
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle,
        long nowUnixMilliseconds)
    {
        return IsPrimaryMuscleWithinRecoveryWindow(
            lastHardWorkByPrimaryMuscle,
            primaryMuscle,
            nowUnixMilliseconds,
            HardRecoveryWindowMilliseconds);
    }

    public static bool IsPrimaryMuscleInModerateRecovery(
        IReadOnlyDictionary<string, long> lastMeaningfulWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(lastMeaningfulWorkByPrimaryMuscle);
        return IsPrimaryMuscleWithinRecoveryWindow(
            lastMeaningfulWorkByPrimaryMuscle,
            primaryMuscle,
            nowUnixMilliseconds,
            ModerateRecoveryWindowMilliseconds);
    }

    public static bool IsModerateExerciseRecovering(
        Exercise exercise,
        IReadOnlyDictionary<string, long> lastMeaningfulWorkByPrimaryMuscle,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(lastMeaningfulWorkByPrimaryMuscle);
        return IsModerateExercise(exercise) &&
            IsPrimaryMuscleInModerateRecovery(
                lastMeaningfulWorkByPrimaryMuscle,
                exercise.PrimaryCanonicalGroup,
                nowUnixMilliseconds);
    }

    public static HardExerciseRotationStatus GetRotationStatus(
        Exercise exercise,
        WorkoutGroup group,
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);

        if (!IsHardExercise(exercise))
        {
            return HardExerciseRotationStatus.Neutral;
        }
        if (IsPrimaryMuscleRecovering(
                lastHardWorkByPrimaryMuscle,
                exercise.PrimaryCanonicalGroup,
                nowUnixMilliseconds))
        {
            return HardExerciseRotationStatus.RecoveringHard;
        }

        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup)
            ? HardExerciseRotationStatus.FreshHard
            : HardExerciseRotationStatus.Neutral;
    }

    public static void RecordCompletedMuscularWork(
        IDictionary<string, long> lastMeaningfulWorkByPrimaryMuscle,
        IDictionary<string, long> lastHardWorkByPrimaryMuscle,
        Exercise exercise,
        long completedAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(lastMeaningfulWorkByPrimaryMuscle);
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);
        ArgumentNullException.ThrowIfNull(exercise);

        if (completedAtUnixMilliseconds <= 0 ||
            (!IsModerateExercise(exercise) && !IsHardExercise(exercise)))
        {
            return;
        }

        string primaryMuscle = exercise.PrimaryCanonicalGroup.ToString();
        RecordCompletion(
            lastMeaningfulWorkByPrimaryMuscle,
            primaryMuscle,
            completedAtUnixMilliseconds);
        if (IsHardExercise(exercise))
        {
            RecordCompletion(
                lastHardWorkByPrimaryMuscle,
                primaryMuscle,
                completedAtUnixMilliseconds);
        }
    }

    private static long GetLastWorkUnixMilliseconds(
        IReadOnlyDictionary<string, long> lastWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle) =>
        lastWorkByPrimaryMuscle.GetValueOrDefault(primaryMuscle.ToString());

    private static bool IsPrimaryMuscleWithinRecoveryWindow(
        IReadOnlyDictionary<string, long> lastWorkByPrimaryMuscle,
        CanonicalMuscleGroup primaryMuscle,
        long nowUnixMilliseconds,
        long recoveryWindowMilliseconds)
    {
        long lastWorkUnixMilliseconds = GetLastWorkUnixMilliseconds(
            lastWorkByPrimaryMuscle,
            primaryMuscle);
        if (lastWorkUnixMilliseconds <= 0)
        {
            return false;
        }

        return nowUnixMilliseconds - lastWorkUnixMilliseconds <
            recoveryWindowMilliseconds;
    }

    private static void RecordCompletion(
        IDictionary<string, long> history,
        string primaryMuscle,
        long completedAtUnixMilliseconds)
    {
        _ = history.TryGetValue(primaryMuscle, out long previousCompletion);
        history[primaryMuscle] = Math.Max(
            completedAtUnixMilliseconds,
            previousCompletion);
    }
}
