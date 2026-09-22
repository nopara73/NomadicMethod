namespace NomadicMethod.Models;

public enum WorkoutSessionStatus
{
    InProgress,
    Completed,
    Interrupted,
}

public enum WorkoutSelectionChangeKind
{
    Shuffle,
}

public sealed class WorkoutSessionLog
{
    public long SessionId { get; set; }

    public long StartedAtUnixMilliseconds { get; set; }

    public long EndedAtUnixMilliseconds { get; set; }

    public int WorkoutMinutes { get; set; }

    public WorkoutModifiers Modifiers { get; set; }

    public bool IsLightDay { get; set; }

    // Freeze the automatic gate for this workout, not the evidence or the next
    // workout. Null preserves the behavior of logs made before this field existed.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? AutomaticLightRequiredAtStart { get; set; }

    public WorkoutSessionStatus Status { get; set; } =
        WorkoutSessionStatus.InProgress;

    public bool StartedBeforeLogging { get; set; }

    public int[] KeptExerciseIdsAtStart { get; set; } = [];

    public Dictionary<string, int[]>
        KeptExerciseRootIdsBySelectionGroupIdAtStart { get; set; } = [];

    public List<WorkoutSelectionSnapshot> InitialSelections { get; set; } = [];

    public List<WorkoutSelectionChangeLog> SelectionChanges { get; set; } = [];

    public List<WorkoutModifierChangeLog> ModifierChanges { get; set; } = [];

    public List<WorkoutDurationChangeLog> DurationChanges { get; set; } = [];

    public List<WorkoutBlockLog> Blocks { get; set; } = [];

    public List<WorkoutDecisionLog> Decisions { get; set; } = [];
}

public sealed class WorkoutDurationChangeLog
{
    public long ChangedAtUnixMilliseconds { get; set; }
    public int PreviousMinutes { get; set; }
    public int NewMinutes { get; set; }
    public List<WorkoutSelectionSnapshot> PlannedSelections { get; set; } = [];
}

public sealed class WorkoutModifierChangeLog
{
    public long ChangedAtUnixMilliseconds { get; set; }

    public WorkoutModifiers PreviousModifiers { get; set; }

    public WorkoutModifiers NewModifiers { get; set; }

    public string ProtectedSelectionGroupId { get; set; } = string.Empty;

    // This is the complete post-change plan, not only a list of differences,
    // so historical sessions remain reconstructable when atomic sequences
    // merge or split anatomical slots.
    public List<WorkoutSelectionSnapshot> PlannedSelections { get; set; } = [];
}

public sealed class WorkoutSelectionSnapshot
{
    public string SelectionGroupId { get; set; } = string.Empty;

    public string[] CoveredWorkoutGroupIds { get; set; } = [];

    public int RootExerciseId { get; set; }

    public string RootExerciseName { get; set; } = string.Empty;

    public int SelectionScoreAtStart { get; set; }

    public int SequenceBlockCount { get; set; }

    public int SetCount { get; set; }

    public bool WasKeptAtWorkoutStart { get; set; }
}

public sealed class WorkoutSelectionChangeLog
{
    public WorkoutSelectionChangeKind Kind { get; set; }

    public long ChangedAtUnixMilliseconds { get; set; }

    public string SelectionGroupId { get; set; } = string.Empty;

    public WorkoutExercisePhase ExercisePhase { get; set; }

    public int RejectedRootExerciseId { get; set; }

    public string RejectedRootExerciseName { get; set; } = string.Empty;

    public int RejectedSelectionScoreBeforeChange { get; set; }

    public bool RejectedSelectionWasKeptAtWorkoutStart { get; set; }

    public int ReplacementRootExerciseId { get; set; }

    public string ReplacementRootExerciseName { get; set; } = string.Empty;

    public int ReplacementSelectionScore { get; set; }
}

public sealed class WorkoutBlockLog
{
    public long CompletedAtUnixMilliseconds { get; set; }

    public string WorkoutGroupId { get; set; } = string.Empty;

    public string SelectionGroupId { get; set; } = string.Empty;

    public int Order { get; set; }

    public int RootExerciseId { get; set; }

    public string RootExerciseName { get; set; } = string.Empty;

    public int ExerciseId { get; set; }

    public string ExerciseName { get; set; } = string.Empty;

    public int SequenceBlockNumber { get; set; }

    public int SequenceBlockCount { get; set; }

    public int SetNumber { get; set; }

    public int SetCount { get; set; }

    public ExerciseSequenceSideCue SideCue { get; set; }

    public ExerciseSequenceDirectionCue DirectionCue { get; set; }

    public bool MirrorMedia { get; set; }

    public ExerciseSequenceMediaSegment MediaSegment { get; set; }

    public int MuscularDemand { get; set; }

    public CanonicalMuscleGroup PrimaryCanonicalGroup { get; set; }

    public CanonicalMuscleGroup[] SecondaryCanonicalGroups { get; set; } = [];

    public bool WasSequenceKeptAtWorkoutStart { get; set; }
}

public sealed class WorkoutDecisionLog
{
    public long DecidedAtUnixMilliseconds { get; set; }

    public string SelectionGroupId { get; set; } = string.Empty;

    public WorkoutExercisePhase ExercisePhase { get; set; }

    public int RootExerciseId { get; set; }

    public string RootExerciseName { get; set; } = string.Empty;

    public int[] SequenceExerciseIds { get; set; } = [];

    public ExerciseOutcome Outcome { get; set; }

    public int SelectionScoreBeforeDecision { get; set; }

    public int CompletedBlockCount { get; set; }

    public int PlannedBlockCount { get; set; }

    public bool WasKeptAtWorkoutStart { get; set; }
}
