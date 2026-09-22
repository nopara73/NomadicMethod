namespace NomadicMethod.Models;

internal sealed class LegacyWorkoutState
{
    public int Version { get; set; } = 4;

    public Dictionary<string, string> SelectedExercises { get; set; } = [];

    public Dictionary<string, ExerciseOutcome> Outcomes { get; set; } = [];

    public string? PendingRestMuscleGroup { get; set; }

    public long PendingRestEndsAtUnixMilliseconds { get; set; }

    public bool PendingRestKept { get; set; }

    public int PendingScoreExerciseId { get; set; }

    public int PendingScoreValue { get; set; }

    public int LastWorkoutMinutes { get; set; } = 10;

    public int ActiveWorkoutMinutes { get; set; }

    public bool WorkoutCompleted { get; set; }

    public bool CompletionAcknowledged { get; set; }
}
