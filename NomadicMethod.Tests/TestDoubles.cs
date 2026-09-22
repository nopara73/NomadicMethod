using NomadicMethod.Data;
using NomadicMethod.Models;

namespace NomadicMethod.Tests;

internal sealed class FakeExerciseDatabase : IExerciseDatabase
{
    private readonly Dictionary<int, int> _persistedScores;

    public FakeExerciseDatabase(IReadOnlyList<Exercise> exercises)
    {
        Exercises = exercises;
        _persistedScores = exercises.ToDictionary(exercise => exercise.Id, exercise => exercise.Score);
    }

    public IReadOnlyList<Exercise> Exercises { get; }

    public List<(int ExerciseId, int Score)> Updates { get; } = [];

    public int PersistedScore(int exerciseId) => _persistedScores[exerciseId];

    public void UpdateScore(Exercise exercise)
    {
        _persistedScores[exercise.Id] = exercise.Score;
        Updates.Add((exercise.Id, exercise.Score));
    }

    public void Dispose()
    {
    }
}

internal sealed class FakeWorkoutStateStore : IWorkoutStateStore
{
    private WorkoutState _storedState = new();

    public int SaveCalls { get; private set; }

    public int DeferredSaveCalls { get; private set; }

    public WorkoutState Load() => Clone(_storedState);

    public void Save(WorkoutState state)
    {
        _storedState = Clone(state);
        SaveCalls++;
    }

    public void SaveDeferred(WorkoutState state)
    {
        _storedState = Clone(state);
        DeferredSaveCalls++;
    }

    private static WorkoutState Clone(WorkoutState state)
    {
        return new WorkoutState
        {
            Version = state.Version,
            CatalogRevision = state.CatalogRevision,
            SelectedExerciseIds = new Dictionary<string, int>(
                state.SelectedExerciseIds,
                StringComparer.Ordinal),
            Outcomes = new Dictionary<string, ExerciseOutcome>(
                state.Outcomes,
                StringComparer.Ordinal),
            LastKeptExerciseIds = new HashSet<int>(state.LastKeptExerciseIds),
            KeptExerciseRootIdsBySelectionGroupId = state
                .KeptExerciseRootIdsBySelectionGroupId
                .ToDictionary(
                    entry => entry.Key,
                    entry => new HashSet<int>(entry.Value),
                    StringComparer.Ordinal),
            ExerciseScoreAdjustmentsBySelectionGroupId = state
                .ExerciseScoreAdjustmentsBySelectionGroupId
                .ToDictionary(
                    entry => entry.Key,
                    entry => new Dictionary<int, int>(entry.Value),
                    StringComparer.Ordinal),
            ExerciseScoreAdjustmentsByPhase = state
                .ExerciseScoreAdjustmentsByPhase
                .ToDictionary(
                    entry => entry.Key,
                    entry => new Dictionary<int, int>(entry.Value)),
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>(
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    StringComparer.Ordinal),
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>(
                    state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
                    StringComparer.Ordinal),
            LegacyCompletedTrainingDayUnixMilliseconds = new HashSet<long>(
                state.LegacyCompletedTrainingDayUnixMilliseconds),
            NextWorkoutSessionId = state.NextWorkoutSessionId,
            ActiveWorkoutSession = state.ActiveWorkoutSession is null
                ? null
                : Clone(state.ActiveWorkoutSession),
            WorkoutHistory = state.WorkoutHistory
                .Select(Clone)
                .ToList(),
            NextWorkoutExcludedExerciseIds = new HashSet<int>(
                state.NextWorkoutExcludedExerciseIds),
            ActiveExtraSetSelectionGroupIds = new HashSet<string>(
                state.ActiveExtraSetSelectionGroupIds,
                StringComparer.Ordinal),
            ActiveSetCountsBySelectionGroupId = new Dictionary<string, int>(
                state.ActiveSetCountsBySelectionGroupId,
                StringComparer.Ordinal),
            ActiveSelectionGroupOrder = [.. state.ActiveSelectionGroupOrder],
            ActiveDurationSelectionGroupIds = state.ActiveDurationSelectionGroupIds?.ToList(),
            ActiveSimpleRoundSelectionGroupIds = [.. state.ActiveSimpleRoundSelectionGroupIds],
            ActiveModifierProtectedSelectionGroupId =
                state.ActiveModifierProtectedSelectionGroupId,
            ActiveDirectionPartnerExerciseIds = new Dictionary<string, int>(
                state.ActiveDirectionPartnerExerciseIds,
                StringComparer.Ordinal),
            ActiveFullSideRoundIds = new HashSet<string>(
                state.ActiveFullSideRoundIds,
                StringComparer.Ordinal),
            PendingMovementGroupId = state.PendingMovementGroupId,
            PendingMovementMillisecondsRemaining =
                state.PendingMovementMillisecondsRemaining,
            PendingMovementEndsAtUnixMilliseconds =
                state.PendingMovementEndsAtUnixMilliseconds,
            PendingMovementPausedByUser = state.PendingMovementPausedByUser,
            PendingRestGroupId = state.PendingRestGroupId,
            PendingRestEndsAtUnixMilliseconds = state.PendingRestEndsAtUnixMilliseconds,
            PendingRestMillisecondsRemaining =
                state.PendingRestMillisecondsRemaining,
            PendingRestPausedByUser = state.PendingRestPausedByUser,
            PendingRestKept = state.PendingRestKept,
            PendingScoreExerciseId = state.PendingScoreExerciseId,
            PendingScoreValue = state.PendingScoreValue,
            PendingScoreUpdates = new Dictionary<int, int>(
                state.PendingScoreUpdates),
            LastWorkoutMinutes = state.LastWorkoutMinutes,
            LastWorkoutModifiers = state.LastWorkoutModifiers,
            ActiveWorkoutMinutes = state.ActiveWorkoutMinutes,
            ActiveWorkoutModifiers = state.ActiveWorkoutModifiers,
            ActiveWorkoutIsLightDay = state.ActiveWorkoutIsLightDay,
            WorkoutCompleted = state.WorkoutCompleted,
            CompletionAcknowledged = state.CompletionAcknowledged,
            LegacySelectedExerciseNames = new Dictionary<string, string>(
                state.LegacySelectedExerciseNames,
                StringComparer.Ordinal),
            LegacyOutcomes = new Dictionary<string, ExerciseOutcome>(
                state.LegacyOutcomes,
                StringComparer.Ordinal),
            LegacyPendingRestGroup = state.LegacyPendingRestGroup,
        };
    }

    private static WorkoutSessionLog Clone(WorkoutSessionLog session)
    {
        return new WorkoutSessionLog
        {
            SessionId = session.SessionId,
            StartedAtUnixMilliseconds = session.StartedAtUnixMilliseconds,
            EndedAtUnixMilliseconds = session.EndedAtUnixMilliseconds,
            WorkoutMinutes = session.WorkoutMinutes,
            Modifiers = session.Modifiers,
            IsLightDay = session.IsLightDay,
            AutomaticLightRequiredAtStart = session.AutomaticLightRequiredAtStart,
            Status = session.Status,
            StartedBeforeLogging = session.StartedBeforeLogging,
            KeptExerciseIdsAtStart = [.. session.KeptExerciseIdsAtStart],
            KeptExerciseRootIdsBySelectionGroupIdAtStart = session
                .KeptExerciseRootIdsBySelectionGroupIdAtStart
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.ToArray(),
                    StringComparer.Ordinal),
            InitialSelections = session.InitialSelections
                .Select(selection => new WorkoutSelectionSnapshot
                {
                    SelectionGroupId = selection.SelectionGroupId,
                    CoveredWorkoutGroupIds = [.. selection.CoveredWorkoutGroupIds],
                    RootExerciseId = selection.RootExerciseId,
                    RootExerciseName = selection.RootExerciseName,
                    SelectionScoreAtStart = selection.SelectionScoreAtStart,
                    SequenceBlockCount = selection.SequenceBlockCount,
                    SetCount = selection.SetCount,
                    WasKeptAtWorkoutStart = selection.WasKeptAtWorkoutStart,
                })
                .ToList(),
            SelectionChanges = session.SelectionChanges
                .Select(change => new WorkoutSelectionChangeLog
                {
                    Kind = change.Kind,
                    ChangedAtUnixMilliseconds = change.ChangedAtUnixMilliseconds,
                    SelectionGroupId = change.SelectionGroupId,
                    ExercisePhase = change.ExercisePhase,
                    RejectedRootExerciseId = change.RejectedRootExerciseId,
                    RejectedRootExerciseName = change.RejectedRootExerciseName,
                    RejectedSelectionScoreBeforeChange =
                        change.RejectedSelectionScoreBeforeChange,
                    RejectedSelectionWasKeptAtWorkoutStart =
                        change.RejectedSelectionWasKeptAtWorkoutStart,
                    ReplacementRootExerciseId = change.ReplacementRootExerciseId,
                    ReplacementRootExerciseName = change.ReplacementRootExerciseName,
                    ReplacementSelectionScore = change.ReplacementSelectionScore,
                })
                .ToList(),
            ModifierChanges = session.ModifierChanges
                .Select(change => new WorkoutModifierChangeLog
                {
                    ChangedAtUnixMilliseconds = change.ChangedAtUnixMilliseconds,
                    PreviousModifiers = change.PreviousModifiers,
                    NewModifiers = change.NewModifiers,
                    ProtectedSelectionGroupId =
                        change.ProtectedSelectionGroupId,
                    PlannedSelections = change.PlannedSelections
                        .Select(selection => new WorkoutSelectionSnapshot
                        {
                            SelectionGroupId = selection.SelectionGroupId,
                            CoveredWorkoutGroupIds =
                                [.. selection.CoveredWorkoutGroupIds],
                            RootExerciseId = selection.RootExerciseId,
                            RootExerciseName = selection.RootExerciseName,
                            SelectionScoreAtStart =
                                selection.SelectionScoreAtStart,
                            SequenceBlockCount = selection.SequenceBlockCount,
                            SetCount = selection.SetCount,
                            WasKeptAtWorkoutStart =
                                selection.WasKeptAtWorkoutStart,
                        })
                        .ToList(),
                })
                .ToList(),
            DurationChanges = session.DurationChanges.Select(change => new WorkoutDurationChangeLog
            {
                ChangedAtUnixMilliseconds = change.ChangedAtUnixMilliseconds,
                PreviousMinutes = change.PreviousMinutes,
                NewMinutes = change.NewMinutes,
                PlannedSelections = change.PlannedSelections.Select(selection => new WorkoutSelectionSnapshot
                {
                    SelectionGroupId = selection.SelectionGroupId,
                    CoveredWorkoutGroupIds = [.. selection.CoveredWorkoutGroupIds],
                    RootExerciseId = selection.RootExerciseId,
                    RootExerciseName = selection.RootExerciseName,
                    SelectionScoreAtStart = selection.SelectionScoreAtStart,
                    SequenceBlockCount = selection.SequenceBlockCount,
                    SetCount = selection.SetCount,
                    WasKeptAtWorkoutStart = selection.WasKeptAtWorkoutStart,
                }).ToList(),
            }).ToList(),
            Blocks = session.Blocks
                .Select(block => new WorkoutBlockLog
                {
                    CompletedAtUnixMilliseconds = block.CompletedAtUnixMilliseconds,
                    WorkoutGroupId = block.WorkoutGroupId,
                    SelectionGroupId = block.SelectionGroupId,
                    Order = block.Order,
                    RootExerciseId = block.RootExerciseId,
                    RootExerciseName = block.RootExerciseName,
                    ExerciseId = block.ExerciseId,
                    ExerciseName = block.ExerciseName,
                    SequenceBlockNumber = block.SequenceBlockNumber,
                    SequenceBlockCount = block.SequenceBlockCount,
                    SetNumber = block.SetNumber,
                    SetCount = block.SetCount,
                    SideCue = block.SideCue,
                    DirectionCue = block.DirectionCue,
                    MirrorMedia = block.MirrorMedia,
                    MediaSegment = block.MediaSegment,
                    MuscularDemand = block.MuscularDemand,
                    PrimaryCanonicalGroup = block.PrimaryCanonicalGroup,
                    SecondaryCanonicalGroups = [.. block.SecondaryCanonicalGroups],
                    WasSequenceKeptAtWorkoutStart =
                        block.WasSequenceKeptAtWorkoutStart,
                })
                .ToList(),
            Decisions = session.Decisions
                .Select(decision => new WorkoutDecisionLog
                {
                    DecidedAtUnixMilliseconds = decision.DecidedAtUnixMilliseconds,
                    SelectionGroupId = decision.SelectionGroupId,
                    ExercisePhase = decision.ExercisePhase,
                    RootExerciseId = decision.RootExerciseId,
                    RootExerciseName = decision.RootExerciseName,
                    SequenceExerciseIds = [.. decision.SequenceExerciseIds],
                    Outcome = decision.Outcome,
                    SelectionScoreBeforeDecision =
                        decision.SelectionScoreBeforeDecision,
                    CompletedBlockCount = decision.CompletedBlockCount,
                    PlannedBlockCount = decision.PlannedBlockCount,
                    WasKeptAtWorkoutStart = decision.WasKeptAtWorkoutStart,
                })
                .ToList(),
        };
    }
}

internal static class ScoreJournalProtocol
{
    public static void Stage(
        WorkoutState state,
        Exercise exercise,
        IWorkoutStateStore stateStore)
    {
        state.PendingScoreExerciseId = exercise.Id;
        state.PendingScoreValue = exercise.Score;
        stateStore.Save(state);
    }

    public static void Recover(
        WorkoutState state,
        IWorkoutStateStore stateStore,
        IExerciseDatabase database)
    {
        if (state.PendingScoreExerciseId <= 0)
        {
            return;
        }

        Exercise? exercise = database.Exercises.SingleOrDefault(candidate =>
            candidate.Id == state.PendingScoreExerciseId);
        if (exercise is not null)
        {
            exercise.Score = state.PendingScoreValue;
            database.UpdateScore(exercise);
        }

        state.PendingScoreExerciseId = 0;
        state.PendingScoreValue = 0;
        stateStore.Save(state);
    }
}
