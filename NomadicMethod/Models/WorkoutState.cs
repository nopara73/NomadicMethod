namespace NomadicMethod.Models;

public sealed class WorkoutState
{
    // Live permission-checked context. Never backed up with workout preferences
    // or persisted as the next session's physical setup.
    [System.Text.Json.Serialization.JsonIgnore]
    public OuraRecoverySnapshot? OuraRecovery { get; set; }

    public int Version { get; set; } = 30;

    public int CatalogRevision { get; set; }

    public Dictionary<string, int> SelectedExerciseIds { get; set; } = [];

    public Dictionary<string, ExerciseOutcome> Outcomes { get; set; } = [];

    public HashSet<int> LastKeptExerciseIds { get; set; } = [];

    // Preferences are keyed by the stable logical selection slot (for example,
    // r10.upper-limbs), never by modifier profile. Values are sequence-root IDs:
    // one Keep/reject decision therefore remains one preference even when the
    // selected sequence contains several exercise blocks or repeated sets.
    public Dictionary<string, HashSet<int>>
        KeptExerciseRootIdsBySelectionGroupId { get; set; } = [];

    // Version 22 migration input only. Downvotes stopped following anatomical
    // selection slots in version 23; retain this field so those votes can be
    // migrated without losing the user's feedback.
    public Dictionary<string, Dictionary<int, int>>
        ExerciseScoreAdjustmentsBySelectionGroupId { get; set; } = [];

    // New downvotes follow the sequence root only within the workout phase in
    // which the user rejected it. The immutable catalog score remains the
    // baseline for feedback recorded before phase-scoped persistence existed.
    public Dictionary<WorkoutExercisePhase, Dictionary<int, int>>
        ExerciseScoreAdjustmentsByPhase { get; set; } = [];

    public Dictionary<string, long>
        LastHardWorkUnixMillisecondsByPrimaryMuscle { get; set; } = [];

    public Dictionary<string, long>
        LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle { get; set; } = [];

    // A one-time migration can recover contiguous pre-logging training days
    // from legacy hard-work timestamps. Keep those timestamps separate from
    // WorkoutHistory: they prove the day occurred, but cannot reconstruct a
    // truthful block-by-block session log.
    public HashSet<long> LegacyCompletedTrainingDayUnixMilliseconds { get; set; } = [];

    public long NextWorkoutSessionId { get; set; } = 1;

    public WorkoutSessionLog? ActiveWorkoutSession { get; set; }

    public List<WorkoutSessionLog> WorkoutHistory { get; set; } = [];

    public HashSet<int> NextWorkoutExcludedExerciseIds { get; set; } = [];

    public HashSet<string> ActiveExtraSetSelectionGroupIds { get; set; } = [];

    public Dictionary<string, int> ActiveSetCountsBySelectionGroupId { get; set; } = [];

    // The active schedule keeps a stable logical order even when unfinished
    // selections are rebuilt after a mid-workout equipment change.
    public List<string> ActiveSelectionGroupOrder { get; set; } = [];

    // A duration edit retains completed/current slots at their original
    // resolution and replans only the remaining slots. Null is the ordinary
    // duration-derived plan, including every pre-version-30 workout.
    public List<string>? ActiveDurationSelectionGroupIds { get; set; }

    // Preserve legacy short-workout round IDs across the 30-minute boundary.
    public HashSet<string> ActiveSimpleRoundSelectionGroupIds { get; set; } = [];

    // Modifier changes may make an already completed anatomical slot
    // unavailable for the new profile. Retain only those completed slots in
    // the active schedule so elapsed work still counts toward its duration;
    // an unfinished unavailable slot is replanned away immediately.
    public HashSet<string> ActiveModifierRetainedSelectionGroupIds { get; set; } = [];

    // Unfinished current work is replanned normally for every modifier change.
    // This exception exists only for a block already completed and resting, so
    // recorded work is not rewritten retroactively.
    public string? ActiveModifierProtectedSelectionGroupId { get; set; }

    // Version 16 migration inputs only. Atomic sequences replace both legacy
    // direction-partner allocation and split-side timing in version 17; the
    // fields remain readable so in-progress upgrades can still be recovered.
    public Dictionary<string, int> ActiveDirectionPartnerExerciseIds { get; set; } = [];

    public HashSet<string> ActiveFullSideRoundIds { get; set; } = [];

    public string? PendingMovementGroupId { get; set; }

    public long PendingMovementMillisecondsRemaining { get; set; }

    public long PendingMovementEndsAtUnixMilliseconds { get; set; }

    public bool PendingMovementPausedByUser { get; set; }

    public string? PendingRestGroupId { get; set; }

    public long PendingRestEndsAtUnixMilliseconds { get; set; }

    public long PendingRestMillisecondsRemaining { get; set; }

    public bool PendingRestPausedByUser { get; set; }

    public bool PendingRestKept { get; set; }

    public int PendingScoreExerciseId { get; set; }

    public int PendingScoreValue { get; set; }

    public Dictionary<int, int> PendingScoreUpdates { get; set; } = [];

    public int LastWorkoutMinutes { get; set; } = 10;

    public WorkoutModifiers LastWorkoutModifiers { get; set; } =
        WorkoutModifiers.UpperBodyClothing |
        WorkoutModifiers.HardFloor |
        WorkoutModifiers.Silence;

    public int ActiveWorkoutMinutes { get; set; }

    public WorkoutModifiers ActiveWorkoutModifiers { get; set; } =
        WorkoutModifiers.None;

    // Kept for backward-compatible state and log serialization. In version 28+
    // it is always derived from the session-scoped Light modifier in
    // ActiveWorkoutModifiers.
    public bool ActiveWorkoutIsLightDay { get; set; }

    public bool WorkoutCompleted { get; set; }

    public bool CompletionAcknowledged { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, string> LegacySelectedExerciseNames { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, ExerciseOutcome> LegacyOutcomes { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public string? LegacyPendingRestGroup { get; set; }
}
