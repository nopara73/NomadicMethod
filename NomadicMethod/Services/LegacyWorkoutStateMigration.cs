using NomadicMethod.Models;

namespace NomadicMethod.Services;

internal static class LegacyWorkoutStateMigration
{
    public static WorkoutState Migrate(LegacyWorkoutState legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);

        return new WorkoutState
        {
            Version = legacy.Version,
            LegacySelectedExerciseNames = legacy.SelectedExercises ?? [],
            LegacyOutcomes = legacy.Outcomes ?? [],
            LegacyPendingRestGroup = legacy.PendingRestMuscleGroup,
            PendingRestEndsAtUnixMilliseconds =
                legacy.PendingRestEndsAtUnixMilliseconds,
            PendingRestKept = legacy.PendingRestKept,
            PendingScoreExerciseId = legacy.PendingScoreExerciseId,
            PendingScoreValue = legacy.PendingScoreValue,
            LastWorkoutMinutes = legacy.LastWorkoutMinutes,
            ActiveWorkoutMinutes = legacy.ActiveWorkoutMinutes,
            WorkoutCompleted = legacy.WorkoutCompleted,
            CompletionAcknowledged = legacy.CompletionAcknowledged,
        };
    }
}
