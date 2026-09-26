using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed partial class ExerciseSessionService
{
    public const int MinimumWorkoutMinutes = 3;
    public const int MaximumWorkoutMinutes = 90;
    public const int DefaultWorkoutMinutes = 10;
    public const WorkoutModifiers DefaultWorkoutModifiers =
        WorkoutModifiers.UpperBodyClothing |
        WorkoutModifiers.HardFloor |
        WorkoutModifiers.Silence;

    private const int CurrentStateVersion = 30;
    private const int DominantLightModeStateVersion = 29;
    private const int ExplicitLightModeStateVersion = 28;
    private const int ImplicitUpperBodyClothingStateVersion = 27;
    private const int LegacyTrainingDayInferenceStateVersion = 25;
    private const int PersistedLightDayStateVersion = 24;
    private const int PhaseScopedDownvoteStateVersion = 23;
    private const int SlotScopedPreferenceStateVersion = 22;
    private const long RestDurationMilliseconds = 15_000L;
    private const int ImplicitHardFloorStateVersion = 21;
    private const int ExplicitMirrorEquipmentStateVersion = 12;
    private const int ImplicitSilenceStateVersion = 8;
    private const int LegacyLineupStateVersion = 7;
    private const string SelectionProfilePrefix = "p";
    private const char SelectionProfileSeparator = '|';
    private static readonly IReadOnlyList<int> WorkoutMinutes =
        Array.AsReadOnly([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);

    private static readonly IReadOnlyDictionary<string, WorkoutGroup> KnownWorkoutGroups =
        MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .ToDictionary(group => group.Id, StringComparer.Ordinal);
    private readonly IReadOnlyList<Exercise> _exercises;
    private readonly IReadOnlyDictionary<int, Exercise> _exercisesById;
    private readonly IReadOnlyDictionary<int, Exercise> _sequenceRootByExerciseId;
    private readonly IReadOnlyDictionary<int, Exercise[]> _sequenceExercisesByRootId;
    private readonly Dictionary<string, WorkoutGroup[][]>
        _sequencePlacementOptionsCache = new(StringComparer.Ordinal);
    private readonly Random _random;
    private readonly Func<DateTimeOffset> _utcNowProvider;
    private readonly TimeZoneInfo _localTimeZone;

    public ExerciseSessionService(
        IReadOnlyList<Exercise> exercises,
        Random? random = null,
        Func<DateTimeOffset>? utcNowProvider = null,
        TimeZoneInfo? localTimeZone = null)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        _exercises = exercises;
        _exercisesById = exercises.ToDictionary(exercise => exercise.Id);
        var sequenceRootByExerciseId = new Dictionary<int, Exercise>();
        foreach (Exercise root in exercises.Where(exercise =>
                     exercise.SequenceBlocks.Length > 0))
        {
            foreach (int memberId in root.SequenceBlocks
                         .Select(block => block.ExerciseId)
                         .Distinct())
            {
                if (!_exercisesById.ContainsKey(memberId) ||
                    !sequenceRootByExerciseId.TryAdd(memberId, root))
                {
                    throw new ArgumentException(
                        $"Exercise {memberId} has an invalid sequence owner.",
                        nameof(exercises));
                }
            }
        }
        if (sequenceRootByExerciseId.Count != exercises.Count)
        {
            throw new ArgumentException(
                "Every exercise must belong to exactly one sequence.",
                nameof(exercises));
        }
        _sequenceRootByExerciseId = sequenceRootByExerciseId;
        _sequenceExercisesByRootId = exercises
            .Where(exercise => exercise.SequenceBlocks.Length > 0)
            .ToDictionary(
                root => root.Id,
                root => root.SequenceBlocks
                    .Select(block => _exercisesById[block.ExerciseId])
                    .DistinctBy(member => member.Id)
                    .ToArray());
        _random = random ?? Random.Shared;
        _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
    }

    public static IReadOnlyList<int> SupportedWorkoutMinutes =>
        WorkoutMinutes;

    public WorkoutModifiers GetDefaultWorkoutModifiers(
        WorkoutState state,
        long? nowUnixMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        WorkoutModifiers setup = WorkoutModifierPolicy.GetPersistentSetupModifiers(state.LastWorkoutModifiers);
        return IsLightDayDue(state, nowUnixMilliseconds ?? GetCurrentUnixTimeMilliseconds())
            ? setup | WorkoutModifiers.Light : setup;
    }

    public int GetWorkoutsUntilLightDay(
        WorkoutState state,
        int prospectiveWorkoutMinutes,
        long? nowUnixMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            state.WorkoutHistory,
            prospectiveWorkoutMinutes,
            nowUnixMilliseconds ?? GetCurrentUnixTimeMilliseconds(),
            _localTimeZone,
            state.LegacyCompletedTrainingDayUnixMilliseconds);
    }

    public bool IsAutomaticLightDayDue(
        WorkoutState state,
        long? nowUnixMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return IsLightDayDue(
            state,
            nowUnixMilliseconds ?? GetCurrentUnixTimeMilliseconds());
    }

    public WorkoutRecoveryLightStatus GetRecoveryLightStatus(
        WorkoutState state,
        WorkoutModifiers modifiers,
        long? nowUnixMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        WorkoutModifiers physicalProfile = WorkoutModifierPolicy.Normalize(
            modifiers & ~WorkoutModifiers.Light);
        Exercise[] selectableExercises = _exercises
            .Where(root => root.SequenceBlocks.Length > 0)
            .Where(root => GetSequenceExercises(root).All(member =>
                IsCompatibleWithModifiers(member, physicalProfile)))
            .SelectMany(GetSequenceExercises)
            .DistinctBy(exercise => exercise.Id)
            .ToArray();

        return WorkoutRecoveryLightPolicy.Evaluate(
            selectableExercises,
            state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
            state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
            nowUnixMilliseconds ?? GetCurrentUnixTimeMilliseconds());
    }

    public void Initialize(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int loadedStateVersion = state.Version;
        NormalizeCollections(state);
        NormalizeWorkoutHistory(state);
        long currentUnixTimeMilliseconds = GetCurrentUnixTimeMilliseconds();
        if (loadedStateVersion < LegacyTrainingDayInferenceStateVersion)
        {
            MigrateLegacyCompletedTrainingDays(
                state,
                currentUnixTimeMilliseconds);
        }
        bool shouldMigratePreparedLightDay =
            loadedStateVersion < PersistedLightDayStateVersion &&
            state.ActiveWorkoutSession is null &&
            IsValidWorkoutMinutes(state.ActiveWorkoutMinutes) &&
            state.Outcomes.Count == 0 &&
            !state.WorkoutCompleted &&
            !state.CompletionAcknowledged &&
            state.PendingMovementGroupId is null &&
            state.PendingRestGroupId is null &&
            IsLightDayDue(state, currentUnixTimeMilliseconds);
        bool requiresSlotPreferenceMigration =
            state.Version < SlotScopedPreferenceStateVersion;
        if (requiresSlotPreferenceMigration)
        {
            // Capture concrete saved-slot and workout-log evidence before a
            // catalog migration is allowed to discard stale lineup entries.
            MigrateSlotScopedPreferences(state);
        }
        if (state.Version < PhaseScopedDownvoteStateVersion)
        {
            if (state.Version < SlotScopedPreferenceStateVersion)
            {
                // Older releases have only the global score baseline, so no
                // historical rejection can be truthfully assigned a phase.
                state.ExerciseScoreAdjustmentsBySelectionGroupId.Clear();
            }
            // This also restores historical Keeps that an older rejection
            // removed. With phase-local feedback, that Keep still applies in
            // the other two phases.
            MigratePhaseScopedDownvotes(state);
        }
        LegacyActiveProgressSnapshot? atomicSequenceMigration =
            state.Version is 16 or 17 && state.ActiveWorkoutMinutes > 0
                ? CaptureLegacyActiveProgress(state)
                : null;
        CatalogMigrationRules.ReconcileWorkoutState(state, _exercisesById);
        bool migratedLegacyState = state.Version < LegacyLineupStateVersion ||
            state.LegacySelectedExerciseNames.Count > 0;
        if (migratedLegacyState)
        {
            MigrateLegacyLineups(state);
            if (requiresSlotPreferenceMigration)
            {
                // Very old name-keyed lineups become concrete only after the
                // legacy lineup migration above.
                MigrateSlotScopedPreferences(state);
            }
        }

        if (state.Version < ImplicitSilenceStateVersion)
        {
            MigrateImplicitSilenceModifier(state);
        }

        if (state.Version < ExplicitMirrorEquipmentStateVersion)
        {
            MigrateExplicitMirrorEquipment(state);
        }

        if (state.Version < ImplicitHardFloorStateVersion)
        {
            MigrateImplicitHardFloorModifier(state);
        }

        if (state.Version < ImplicitUpperBodyClothingStateVersion)
        {
            MigrateImplicitUpperBodyClothingModifier(state);
        }

        if (state.Version < ExplicitLightModeStateVersion)
        {
            MigrateExplicitLightMode(state);
        }

        bool shouldMigrateDominantLightLineup =
            loadedStateVersion < DominantLightModeStateVersion &&
            state.ActiveWorkoutMinutes > 0 &&
            !state.WorkoutCompleted &&
            !state.CompletionAcknowledged &&
            state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light);
        bool shouldMigrateActiveLightLineup =
            shouldMigrateDominantLightLineup &&
            (state.ActiveWorkoutSession is not null ||
             state.Outcomes.Count > 0 ||
             state.PendingMovementGroupId is not null ||
             state.PendingRestGroupId is not null);
        bool shouldMigratePreparedDominantLightLineup =
            shouldMigrateDominantLightLineup &&
            !shouldMigrateActiveLightLineup;

        state.Version = CurrentStateVersion;
        state.LastWorkoutMinutes = NormalizeLastWorkoutMinutes(state.LastWorkoutMinutes);
        state.LastWorkoutModifiers = WorkoutModifierPolicy
            .GetPersistentSetupModifiers(
                state.LastWorkoutModifiers);
        state.ActiveWorkoutModifiers = NormalizeWorkoutModifiers(
            state.ActiveWorkoutModifiers);
        NormalizeSavedLineups(state);
        NormalizeSlotPreferences(state);
        if (shouldMigratePreparedLightDay)
        {
            // Older builds may have persisted an unstarted background plan.
            // Re-evaluate that plan from its already persisted session history
            // so installing the update on day four takes effect immediately.
            EnableLightModeForExistingActiveWorkout(state);
            CarrySlotPreferencesForward(state);
        }
        state.ActiveWorkoutIsLightDay = state.ActiveWorkoutModifiers
            .HasFlag(WorkoutModifiers.Light);

        if (migratedLegacyState && state.ActiveWorkoutMinutes > 0)
        {
            if (state.WorkoutCompleted && state.CompletionAcknowledged)
            {
                ResetToDurationSelection(state);
                ClearLegacyMigrationState(state);
            }

            return;
        }

        if (state.ActiveWorkoutMinutes == 0)
        {
            FinalizeActiveWorkoutSession(
                state,
                WorkoutSessionStatus.Interrupted);
            ResetToDurationSelection(state);
            ClearLegacyMigrationState(state);
            return;
        }

        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            FinalizeActiveWorkoutSession(
                state,
                WorkoutSessionStatus.Interrupted);
            ResetToDurationSelection(state);
            ClearLegacyMigrationState(state);
            return;
        }

        // Only a valid, resumable rest may preserve a below-threshold active
        // selection. Clear stale checkpoints before lineup arbitration.
        NormalizePendingRest(state);
        NormalizeActiveModifierRetainedSelectionGroups(state);
        NormalizeActiveModifierTransitionProtection(state);
        if (shouldMigrateActiveLightLineup)
        {
            MigrateActiveLightLineup(state);
        }
        else
        {
            RepairActiveLineup(
                state,
                preserveCurrentSelections:
                    !shouldMigratePreparedLightDay &&
                    !shouldMigratePreparedDominantLightLineup);
            NormalizeActiveLongWorkoutAllocation(state);
        }
        if ((shouldMigratePreparedLightDay ||
             shouldMigratePreparedDominantLightLineup) &&
            !shouldMigrateActiveLightLineup)
        {
            RebalanceNewExercisesByMuscleBalance(state);
            SetActiveLongWorkoutAllocation(state);
        }
        if (atomicSequenceMigration is not null)
        {
            MigrateLegacyActiveProgress(state, atomicSequenceMigration);
        }
        NormalizeOutcomes(state);
        NormalizeCompletionState(state);
        NormalizePendingRest(state);
        NormalizePendingMovement(state);
        NormalizeCompletionState(state);

        if (state.WorkoutCompleted)
        {
            FinalizeActiveWorkoutSession(
                state,
                WorkoutSessionStatus.Completed);
        }
        else
        {
            EnsureActiveWorkoutSession(state, startedBeforeLogging: true);
        }

        if (state.WorkoutCompleted && state.CompletionAcknowledged)
        {
            FinalizeCurrentWorkout(state);
        }
    }

    public void StartWorkout(
        WorkoutState state,
        int minutes,
        WorkoutModifiers modifiers = DefaultWorkoutModifiers)
    {
        PrepareWorkout(state, minutes, modifiers);
        ActivatePreparedWorkout(state);
    }

    public void PrepareWorkout(
        WorkoutState state,
        int minutes,
        WorkoutModifiers modifiers = DefaultWorkoutModifiers)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!IsValidWorkoutMinutes(minutes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutes),
                minutes,
                "Workout duration must be one of 3, 5, 7, 10, 15, 20, 30, 45, 60, or 90 minutes.");
        }

        if (state.ActiveWorkoutMinutes != 0)
        {
            throw new InvalidOperationException("A workout is already active.");
        }

        int loadedStateVersion = state.Version;
        NormalizeCollections(state);
        NormalizeWorkoutHistory(state);
        NormalizeSlotPreferences(state);
        long workoutStartedAtUnixMilliseconds =
            GetCurrentUnixTimeMilliseconds();
        if (loadedStateVersion < LegacyTrainingDayInferenceStateVersion)
        {
            MigrateLegacyCompletedTrainingDays(
                state,
                workoutStartedAtUnixMilliseconds);
        }
        FinalizeActiveWorkoutSession(
            state,
            WorkoutSessionStatus.Interrupted,
            workoutStartedAtUnixMilliseconds);
        state.Version = CurrentStateVersion;
        modifiers = NormalizeWorkoutModifiers(modifiers);
        if (IsLightDayDue(state, workoutStartedAtUnixMilliseconds))
        {
            modifiers |= WorkoutModifiers.Light;
        }
        state.LastWorkoutMinutes = minutes;
        state.LastWorkoutModifiers = WorkoutModifierPolicy
            .GetPersistentSetupModifiers(modifiers);
        state.ActiveWorkoutMinutes = minutes;
        state.ActiveWorkoutModifiers = modifiers;
        state.ActiveWorkoutIsLightDay = modifiers.HasFlag(
            WorkoutModifiers.Light);
        state.ActiveSelectionGroupOrder.Clear();
        state.ActiveDurationSelectionGroupIds = null;
        state.ActiveSimpleRoundSelectionGroupIds.Clear();
        state.ActiveModifierRetainedSelectionGroupIds.Clear();
        state.ActiveModifierProtectedSelectionGroupId = null;
        state.Outcomes.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        ClearPendingMovement(state);
        ClearPendingRest(state);
        ClearLegacyMigrationState(state);
        // Shuffle exclusions are scoped to the workout in which the shuffle
        // happened. Persistent rejection feedback is stored by workout phase.
        state.NextWorkoutExcludedExerciseIds.Clear();
        PrepareAdaptiveLineup(state);
    }

    public void ActivatePreparedWorkout(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes) ||
            state.ActiveWorkoutSession is not null ||
            state.Outcomes.Count != 0 ||
            state.WorkoutCompleted ||
            state.CompletionAcknowledged ||
            state.PendingMovementGroupId is not null ||
            state.PendingRestGroupId is not null)
        {
            throw new InvalidOperationException(
                "The workout state does not contain an activatable prepared workout.");
        }

        long workoutStartedAtUnixMilliseconds =
            GetCurrentUnixTimeMilliseconds();
        if (!state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light) &&
            IsLightDayDue(state, workoutStartedAtUnixMilliseconds))
        {
            EnableLightModeForExistingActiveWorkout(state);
            RepairActiveLineup(state, preserveCurrentSelections: false);
            RebalanceNewExercisesByMuscleBalance(state);
            SetActiveLongWorkoutAllocation(state);
            ReconcileLineupWithScheduledPhases(state);
        }
        int[] keptExerciseIdsAtStart = state.LastKeptExerciseIds
            .Order()
            .ToArray();
        CreateActiveWorkoutSession(
            state,
            workoutStartedAtUnixMilliseconds,
            keptExerciseIdsAtStart,
            startedBeforeLogging: false);
    }

    public void ReconfigureActiveWorkout(
        WorkoutState state,
        WorkoutModifiers modifiers,
        string currentWorkoutGroupId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentWorkoutGroupId);
        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes) ||
            state.WorkoutCompleted ||
            state.CompletionAcknowledged)
        {
            throw new InvalidOperationException(
                "Only an in-progress workout can be reconfigured.");
        }

        WorkoutGroup[] priorActiveRounds = GetActiveGroups(state).ToArray();
        WorkoutGroup currentRound = priorActiveRounds.SingleOrDefault(round =>
                round.Id == currentWorkoutGroupId)
            ?? throw new InvalidOperationException(
                "The displayed workout block is no longer active.");
        if (GetNextGroup(state)?.Id != currentRound.Id)
        {
            throw new InvalidOperationException(
                "Only the currently displayed workout block can be replanned.");
        }

        WorkoutModifiers previousModifiers = state.ActiveWorkoutModifiers;
        WorkoutModifiers lastModifiersBefore = state.LastWorkoutModifiers;
        bool previousIsLightDay = state.ActiveWorkoutIsLightDay;
        modifiers = NormalizeWorkoutModifiers(modifiers);
        long reconfiguredAtUnixMilliseconds =
            GetCurrentUnixTimeMilliseconds();
        if (IsLightDayDue(state, reconfiguredAtUnixMilliseconds))
        {
            modifiers |= WorkoutModifiers.Light;
        }
        bool targetIsLightDay = modifiers.HasFlag(WorkoutModifiers.Light);
        if (modifiers == previousModifiers)
        {
            state.LastWorkoutModifiers = WorkoutModifierPolicy
                .GetPersistentSetupModifiers(modifiers);
            state.ActiveWorkoutIsLightDay = targetIsLightDay;
            return;
        }

        SelectedSequencePlacement[] priorPlacements =
            GetSelectedSequencePlacements(state);
        SelectedSequencePlacement[] priorOrderedPlacements =
            GetScheduleOrderedPlacements(state, priorPlacements);
        SelectedSequencePlacement currentPlacement = priorPlacements.Single(
            placement => placement.Anchor.Id == currentRound.SelectionKey);
        bool preserveCompletedCurrentSelection =
            GetPendingRestGroup(state)?.Id == currentRound.Id;
        HashSet<string> lockedSelectionGroupIds = priorActiveRounds
            .Where(round => state.Outcomes.ContainsKey(round.Id))
            .Select(round => round.SelectionKey)
            .ToHashSet(StringComparer.Ordinal);
        if (preserveCompletedCurrentSelection)
        {
            lockedSelectionGroupIds.Add(currentRound.SelectionKey);
        }
        else
        {
            // The unfinished current slot must be chosen normally for the
            // target profile. This lets both restrictive changes and newly
            // enabled equipment preferences replace it. Earlier completed
            // blocks remain in the durable workout log.
            lockedSelectionGroupIds.Remove(currentRound.SelectionKey);
        }
        SelectedSequencePlacement[] lockedPlacements = priorPlacements
            .Where(placement => lockedSelectionGroupIds.Contains(
                placement.Anchor.Id))
            .ToArray();
        Dictionary<string, int> lockedExerciseIdsByGroup = lockedPlacements
            .SelectMany(placement => placement.CoveredGroups.Select(group =>
                (GroupId: group.Id, RootId: placement.Root.Id)))
            .ToDictionary(
                entry => entry.GroupId,
                entry => entry.RootId,
                StringComparer.Ordinal);
        Dictionary<string, int> lockedSetCountsBySelectionGroupId =
            lockedPlacements.ToDictionary(
                placement => placement.Anchor.Id,
                placement => state.ActiveSetCountsBySelectionGroupId
                    .GetValueOrDefault(placement.Anchor.Id, 1),
                StringComparer.Ordinal);
        HashSet<string> protectedBaseGroupIds =
            lockedExerciseIdsByGroup.Keys.ToHashSet(StringComparer.Ordinal);
        WorkoutGroup[] selectionGroups = GetSelectionGroups(
                state,
                modifiers,
                protectedBaseGroupIds)
            .ToArray();
        HashSet<string> retainedUnavailableSelectionGroupIds = selectionGroups
            .Where(group => !WorkoutModifierPolicy.IsSelectionGroupAvailable(
                group,
                modifiers))
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        bool currentSelectionGroupAvailable = selectionGroups.Any(group =>
            group.Id == currentRound.SelectionKey);

        IReadOnlyDictionary<string, int> replannedLineup =
            ChooseBestDistinctLineup(
                state,
                selectionGroups,
                modifiers,
                currentExerciseIds: lockedExerciseIdsByGroup,
                allowSavedSelectionException: true,
                modifierTransitionProtectedGroupIds: protectedBaseGroupIds,
                reservedRepeatMinutes: lockedPlacements.Sum(p =>
                    (lockedSetCountsBySelectionGroupId[p.Anchor.Id] - 1) * p.Root.SequenceBlocks.Length));

        var selectedExerciseIdsBefore = new Dictionary<string, int>(
            state.SelectedExerciseIds,
            StringComparer.Ordinal);
        var setCountsBefore = new Dictionary<string, int>(
            state.ActiveSetCountsBySelectionGroupId,
            StringComparer.Ordinal);
        var extraSetGroupsBefore = new HashSet<string>(
            state.ActiveExtraSetSelectionGroupIds,
            StringComparer.Ordinal);
        var selectionOrderBefore = state.ActiveSelectionGroupOrder.ToList();
        var retainedSelectionGroupsBefore = new HashSet<string>(
            state.ActiveModifierRetainedSelectionGroupIds,
            StringComparer.Ordinal);
        var outcomesBefore = new Dictionary<string, ExerciseOutcome>(
            state.Outcomes,
            StringComparer.Ordinal);
        string? protectedSelectionBefore =
            state.ActiveModifierProtectedSelectionGroupId;
        string? pendingMovementGroupBefore = state.PendingMovementGroupId;
        long pendingMovementRemainingBefore =
            state.PendingMovementMillisecondsRemaining;
        long pendingMovementEndsAtBefore =
            state.PendingMovementEndsAtUnixMilliseconds;
        bool pendingMovementPausedBefore = state.PendingMovementPausedByUser;
        try
        {
            state.LastWorkoutModifiers = WorkoutModifierPolicy
                .GetPersistentSetupModifiers(modifiers);
            state.ActiveWorkoutModifiers = modifiers;
            state.ActiveWorkoutIsLightDay = targetIsLightDay;
            state.ActiveModifierRetainedSelectionGroupIds =
                retainedUnavailableSelectionGroupIds;
            state.ActiveModifierProtectedSelectionGroupId =
                preserveCompletedCurrentSelection
                    ? currentRound.SelectionKey
                    : null;
            ApplyDistinctLineup(
                state,
                selectionGroups,
                replannedLineup,
                clearChangedProgress: false);
            UpdateSelectionOrderAfterReconfiguration(
                state,
                priorOrderedPlacements);
            RebalanceNewExercisesByMuscleBalance(
                state,
                lockedSelectionGroupIds);
            UpdateSelectionOrderAfterReconfiguration(
                state,
                priorOrderedPlacements);
            ApplyLongWorkoutAllocation(
                state,
                ChooseLongWorkoutAllocation(
                    state,
                    lockedSelectionGroupIds));

            WorkoutGroup[] replannedRounds = GetActiveGroups(state).ToArray();
            SelectedSequencePlacement? replannedCurrentPlacement =
                GetSelectedSequencePlacements(state).SingleOrDefault(placement =>
                    placement.CoveredGroups.Any(group =>
                        group.Id == currentRound.SelectionKey));
            bool currentSelectionChanged =
                replannedCurrentPlacement is null ||
                replannedCurrentPlacement.Root.Id != currentPlacement.Root.Id;
            if (currentSelectionChanged)
            {
                // Round IDs describe positions, not exercise identities. A
                // replacement must not inherit completed flags from the old
                // movement at those positions; its actual work stays logged.
                foreach (WorkoutGroup priorRound in priorActiveRounds.Where(round =>
                             round.SelectionKey == currentRound.SelectionKey))
                {
                    state.Outcomes.Remove(priorRound.Id);
                }

                // Partial time belongs to the movement that was removed. The
                // replacement returns in Ready state with its full timer; no
                // score or Keep is changed.
                ClearPendingMovement(state);
                state.ActiveModifierProtectedSelectionGroupId = null;
            }

            WorkoutGroup? replannedNextRound = GetNextGroup(state);
            bool changedLockedSelection = lockedExerciseIdsByGroup.Any(entry =>
                state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        entry.Key,
                        state.ActiveWorkoutModifiers)) != entry.Value);
            bool changedLockedSetCount =
                lockedSetCountsBySelectionGroupId.Any(entry =>
                    state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(
                        entry.Key,
                        1) != entry.Value);
            if (changedLockedSelection ||
                changedLockedSetCount ||
                state.Outcomes.Keys.Any(outcomeGroupId =>
                    replannedRounds.All(round => round.Id != outcomeGroupId)) ||
                (preserveCompletedCurrentSelection &&
                    replannedNextRound?.Id != currentRound.Id) ||
                (!preserveCompletedCurrentSelection &&
                    currentSelectionGroupAvailable &&
                    replannedNextRound?.SelectionKey !=
                        currentRound.SelectionKey) ||
                (!currentSelectionChanged &&
                    replannedNextRound?.Id != currentRound.Id) ||
                (!currentSelectionChanged &&
                    pendingMovementGroupBefore is not null &&
                    state.PendingMovementGroupId != pendingMovementGroupBefore) ||
                (preserveCompletedCurrentSelection &&
                    state.PendingMovementGroupId is not null &&
                    state.PendingMovementGroupId != currentRound.Id) ||
                (preserveCompletedCurrentSelection &&
                    state.PendingRestGroupId is not null &&
                    state.PendingRestGroupId != currentRound.Id) ||
                (currentSelectionChanged &&
                    state.PendingMovementGroupId is not null))
            {
                throw new InvalidOperationException(
                    "The modifier change could not preserve completed work or " +
                    $"replan the current exercise safely. Selection changed: {changedLockedSelection}; " +
                    $"sets changed: {changedLockedSetCount}; current: {currentRound.Id}; " +
                    $"next: {replannedNextRound?.Id}; replacement: {currentSelectionChanged}; " +
                    $"orphan outcomes: {string.Join(',', state.Outcomes.Keys.Where(id => replannedRounds.All(r => r.Id != id)))}.");
            }

            WorkoutSessionLog session = EnsureActiveWorkoutSession(
                state,
                startedBeforeLogging: true);
            session.ModifierChanges.Add(new WorkoutModifierChangeLog
            {
                ChangedAtUnixMilliseconds = GetCurrentUnixTimeMilliseconds(),
                PreviousModifiers = previousModifiers,
                NewModifiers = modifiers,
                ProtectedSelectionGroupId = preserveCompletedCurrentSelection
                    ? currentRound.SelectionKey
                    : string.Empty,
                PlannedSelections = CreateCurrentSelectionSnapshots(
                    state,
                    session),
            });
        }
        catch
        {
            state.SelectedExerciseIds = selectedExerciseIdsBefore;
            state.ActiveSetCountsBySelectionGroupId = setCountsBefore;
            state.ActiveExtraSetSelectionGroupIds = extraSetGroupsBefore;
            state.ActiveSelectionGroupOrder = selectionOrderBefore;
            state.ActiveModifierRetainedSelectionGroupIds =
                retainedSelectionGroupsBefore;
            state.Outcomes = outcomesBefore;
            state.ActiveModifierProtectedSelectionGroupId =
                protectedSelectionBefore;
            state.PendingMovementGroupId = pendingMovementGroupBefore;
            state.PendingMovementMillisecondsRemaining =
                pendingMovementRemainingBefore;
            state.PendingMovementEndsAtUnixMilliseconds =
                pendingMovementEndsAtBefore;
            state.PendingMovementPausedByUser = pendingMovementPausedBefore;
            state.ActiveWorkoutModifiers = previousModifiers;
            state.LastWorkoutModifiers = lastModifiersBefore;
            state.ActiveWorkoutIsLightDay = previousIsLightDay;
            throw;
        }
    }

    public IReadOnlyList<WorkoutGroup> GetActiveGroups(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return IsValidWorkoutMinutes(state.ActiveWorkoutMinutes)
            ? CreateWorkoutSchedule(
                state,
                GetEffectiveSetCounts(state))
            : [];
    }

    public Exercise GetSelectedExercise(WorkoutState state, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        if (group.ExerciseOverrideId > 0)
        {
            if (!_exercisesById.TryGetValue(
                    group.ExerciseOverrideId,
                    out Exercise? overrideExercise) ||
                !IsSequenceOverrideValid(
                    state,
                    overrideExercise,
                    group,
                    state.ActiveWorkoutModifiers))
            {
                throw new InvalidOperationException(
                    $"The linked sequence block for {group.DisplayName} is unavailable.");
            }

            return overrideExercise;
        }

        string selectionStorageKey = GetSelectionStorageKey(
            group.SelectionKey,
            state.ActiveWorkoutModifiers);
        if (!state.SelectedExerciseIds.TryGetValue(
                selectionStorageKey,
                out int exerciseId) ||
            !_exercisesById.TryGetValue(exerciseId, out Exercise? exercise) ||
            !IsSavedSelectionValid(
                state,
                exercise,
                group,
                state.ActiveWorkoutModifiers))
        {
            throw new InvalidOperationException(
                $"No eligible exercise is selected for {group.DisplayName}.");
        }

        return exercise;
    }

    public WorkoutGroup? GetNextGroup(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return GetActiveGroups(state)
            .FirstOrDefault(group => !state.Outcomes.ContainsKey(group.Id));
    }

    public bool IsIntermediateSequenceBlock(
        WorkoutState state,
        WorkoutGroup group)
    {
        return GetNextSequenceBlock(state, group) is not null;
    }

    public WorkoutGroup? GetNextSequenceBlock(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        IReadOnlyList<WorkoutGroup> activeGroups = GetActiveGroups(state);
        int groupIndex = activeGroups
            .Select((activeGroup, index) => (activeGroup, index))
            .Where(entry => entry.activeGroup.Id == group.Id)
            .Select(entry => entry.index)
            .DefaultIfEmpty(-1)
            .Single();
        if (groupIndex < 0 || groupIndex + 1 >= activeGroups.Count)
        {
            return null;
        }

        WorkoutGroup nextGroup = activeGroups[groupIndex + 1];
        return nextGroup.SelectionKey == group.SelectionKey
            ? nextGroup
            : null;
    }

    public bool IsSequenceContinuationBlock(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        IReadOnlyList<WorkoutGroup> activeGroups = GetActiveGroups(state);
        int groupIndex = activeGroups
            .Select((activeGroup, index) => (activeGroup, index))
            .Where(entry => entry.activeGroup.Id == group.Id)
            .Select(entry => entry.index)
            .DefaultIfEmpty(-1)
            .Single();
        return groupIndex > 0 &&
            activeGroups[groupIndex - 1].SelectionKey == group.SelectionKey;
    }

    public bool CanShuffleNextExercise(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        return GetNextGroup(state)?.Id == group.Id &&
            !IsSequenceContinuationBlock(state, group) &&
            GetCompatibleShuffleCandidates(state, group).Count > 0;
    }

    public ShuffledExerciseResult? ShuffleNextExercise(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        if (GetNextGroup(state)?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }
        if (IsSequenceContinuationBlock(state, group))
        {
            return null;
        }

        List<ShuffleCandidate> candidates =
            GetCompatibleShuffleCandidates(state, group);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light))
        {
            List<ShuffleCandidate> lightCandidates = candidates
                .Where(candidate => IsDemandZeroSequence(candidate.Exercise))
                .ToList();
            if (lightCandidates.Count > 0)
            {
                WorkoutExercisePhase phase = GetExercisePhase(group);
                int highestLightScore = lightCandidates.Max(candidate =>
                    GetSelectionScore(state, candidate.Exercise, phase));
                candidates = lightCandidates
                    .Where(candidate => GetSelectionScore(
                        state,
                        candidate.Exercise,
                        phase) == highestLightScore)
                    .ToList();
            }
        }

        Exercise rejectedExercise = GetSelectedExercise(state, group);
        Exercise rejectedRoot = GetSequenceRoot(rejectedExercise);
        Exercise[] scoreUpdates = GetSequenceExercises(rejectedRoot);
        int rejectedSelectionScore = GetSelectionScore(
            state,
            rejectedRoot,
            GetExercisePhase(group));

        Shuffle(candidates);
        ShuffleCandidate selected = candidates[0];

        foreach (WorkoutGroup coveredGroup in selected.CoveredGroups)
        {
            state.SelectedExerciseIds[GetSelectionStorageKey(
                coveredGroup.Id,
                state.ActiveWorkoutModifiers)] = selected.Exercise.Id;
        }
        RecordWorkoutSelectionChange(
            state,
            group.SelectionKey,
            GetExercisePhase(group),
            rejectedRoot,
            rejectedSelectionScore,
            selected.Exercise);
        ApplyShuffleRejection(
            state,
            group.SelectionKey,
            GetExercisePhase(group),
            rejectedRoot,
            scoreUpdates);
        ApplyLongWorkoutAllocation(state, selected.Allocation);
        if (state.ActiveModifierProtectedSelectionGroupId == group.SelectionKey)
        {
            state.ActiveModifierProtectedSelectionGroupId = null;
        }

        return new ShuffledExerciseResult(
            rejectedExercise,
            selected.Exercise,
            Array.AsReadOnly(scoreUpdates));
    }

    public bool IsFinalPendingGroup(WorkoutState state, WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        IReadOnlyList<WorkoutGroup> activeGroups = GetActiveGroups(state);
        WorkoutGroup? nextGroup = activeGroups
            .FirstOrDefault(activeGroup => !state.Outcomes.ContainsKey(activeGroup.Id));
        return nextGroup?.Id == group.Id &&
            state.Outcomes.Count == activeGroups.Count - 1;
    }

    public void BeginRest(
        WorkoutState state,
        WorkoutGroup group,
        long endsAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? nextGroup = GetNextGroup(state);
        if (nextGroup?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }
        if (endsAtUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(endsAtUnixMilliseconds));
        }

        ClearPendingMovement(state);
        state.PendingRestGroupId = group.Id;
        state.PendingRestEndsAtUnixMilliseconds = endsAtUnixMilliseconds;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
        state.PendingRestKept = false;
        long completedAtUnixMilliseconds = GetCurrentUnixTimeMilliseconds();
        RecordCompletedWorkoutBlock(
            state,
            group,
            completedAtUnixMilliseconds);
        WorkoutRecoveryPolicy.RecordCompletedMuscularWork(
            state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
            state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
            GetSelectedExercise(state, group),
            completedAtUnixMilliseconds);
    }

    public void BeginMovement(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining,
        long endsAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        ValidatePendingMovement(
            state,
            group,
            millisecondsRemaining,
            endsAtUnixMilliseconds,
            allowPausedDeadline: false);
        ClearPendingRest(state);
        state.PendingMovementGroupId = group.Id;
        state.PendingMovementMillisecondsRemaining = millisecondsRemaining;
        state.PendingMovementEndsAtUnixMilliseconds = endsAtUnixMilliseconds;
        state.PendingMovementPausedByUser = false;
    }

    public void PauseMovement(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining,
        bool pausedByUser)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        ValidatePendingMovement(
            state,
            group,
            millisecondsRemaining,
            endsAtUnixMilliseconds: 0,
            allowPausedDeadline: true);
        ClearPendingRest(state);
        state.PendingMovementGroupId = group.Id;
        state.PendingMovementMillisecondsRemaining = millisecondsRemaining;
        state.PendingMovementEndsAtUnixMilliseconds = 0;
        state.PendingMovementPausedByUser = pausedByUser;
    }

    public WorkoutGroup? GetPendingMovementGroup(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return GetValidPendingMovementGroup(state);
    }

    public WorkoutGroup? GetPendingRestGroup(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        WorkoutGroup? pendingGroup = GetValidPendingRestGroup(state);
        return pendingGroup is not null &&
            GetNextGroup(state)?.Id == pendingGroup.Id
                ? pendingGroup
                : null;
    }

    public bool KeepPendingRest(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        WorkoutGroup? pendingGroup = GetPendingRestGroup(state);
        if (pendingGroup is null ||
            IsIntermediateSequenceBlock(state, pendingGroup))
        {
            return false;
        }

        state.PendingRestKept = true;
        return true;
    }

    public void PauseRest(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? pendingGroup = GetPendingRestGroup(state);
        if (pendingGroup?.Id != group.Id || state.PendingRestPausedByUser)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} does not have a running rest.");
        }
        if (millisecondsRemaining <= 0 ||
            millisecondsRemaining > RestDurationMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(millisecondsRemaining));
        }

        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestMillisecondsRemaining = millisecondsRemaining;
        state.PendingRestPausedByUser = true;
    }

    public void ResumeRest(
        WorkoutState state,
        WorkoutGroup group,
        long endsAtUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? pendingGroup = GetPendingRestGroup(state);
        if (pendingGroup?.Id != group.Id || !state.PendingRestPausedByUser)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} does not have a paused rest.");
        }
        if (endsAtUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(endsAtUnixMilliseconds));
        }

        state.PendingRestEndsAtUnixMilliseconds = endsAtUnixMilliseconds;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
    }

    public long GetPendingRestMillisecondsRemaining(
        WorkoutState state,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }
        if (GetPendingRestGroup(state) is null)
        {
            return 0;
        }

        long remaining = state.PendingRestPausedByUser
            ? state.PendingRestMillisecondsRemaining
            : state.PendingRestEndsAtUnixMilliseconds - nowUnixMilliseconds;
        return Math.Clamp(remaining, 0L, RestDurationMilliseconds);
    }

    public long GetPendingMovementMillisecondsRemaining(
        WorkoutState state,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }

        WorkoutGroup? group = GetValidPendingMovementGroup(state);
        if (group is null)
        {
            return 0;
        }

        long remaining = state.PendingMovementMillisecondsRemaining;
        if (state.PendingMovementEndsAtUnixMilliseconds > nowUnixMilliseconds)
        {
            remaining = Math.Min(
                remaining,
                state.PendingMovementEndsAtUnixMilliseconds - nowUnixMilliseconds);
        }

        // An expired wall-clock deadline means the app stopped before its
        // lifecycle pause could checkpoint the monotonic countdown. Resume
        // conservatively from the last stored time instead of crediting an
        // exercise that may not have been performed while NomadicMethod was absent.
        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(state, group)) * 1_000L;
        return Math.Clamp(remaining, 1L, maximum);
    }

    public void ClearPendingMovement(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.PendingMovementGroupId = null;
        state.PendingMovementMillisecondsRemaining = 0;
        state.PendingMovementEndsAtUnixMilliseconds = 0;
        state.PendingMovementPausedByUser = false;
    }

    public Exercise RecordOutcome(
        WorkoutState state,
        WorkoutGroup group,
        bool keep) =>
        RecordOutcomeWithScoreUpdates(state, group, keep).Exercise;

    public RecordedWorkoutOutcome RecordOutcomeWithScoreUpdates(
        WorkoutState state,
        WorkoutGroup group,
        bool keep)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? nextGroup = GetNextGroup(state);
        if (nextGroup?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }
        if (!group.IsFinalSequenceRound)
        {
            throw new InvalidOperationException(
                "A sequence can only be rated after its final block.");
        }

        ClearPendingMovement(state);
        return ApplySequenceOutcome(state, group, keep);
    }

    public RecordedWorkoutOutcome RejectCurrentSequenceWithScoreUpdates(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        if (GetNextGroup(state)?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }

        WorkoutGroup[] sequenceRounds = GetActiveGroups(state)
            .Where(round => round.SelectionKey == group.SelectionKey)
            .OrderBy(round => round.Order)
            .ToArray();
        WorkoutGroup decisionRound = sequenceRounds[^1];
        foreach (WorkoutGroup round in sequenceRounds)
        {
            if (round.Id != decisionRound.Id)
            {
                state.Outcomes.TryAdd(round.Id, ExerciseOutcome.Neutral);
            }
        }

        ClearPendingMovement(state);
        ClearPendingRest(state);
        return ApplySequenceOutcome(
            state,
            decisionRound,
            keep: false,
            feedbackPhase: GetExercisePhase(group));
    }

    public void AdvanceSequence(
        WorkoutState state,
        WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(group);

        WorkoutGroup? nextGroup = GetNextGroup(state);
        if (nextGroup?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }
        if (!IsIntermediateSequenceBlock(state, group))
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not an intermediate sequence block.");
        }

        state.Outcomes[group.Id] = ExerciseOutcome.Neutral;
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
    }

    private RecordedWorkoutOutcome ApplySequenceOutcome(
        WorkoutState state,
        WorkoutGroup group,
        bool keep,
        WorkoutExercisePhase? feedbackPhase = null)
    {
        Exercise exercise = GetSelectedExercise(state, group);
        Exercise root = GetSequenceRoot(exercise);
        Exercise[] sequenceExercises = GetSequenceExercises(root);
        ExerciseOutcome outcome = keep ? ExerciseOutcome.Tick : ExerciseOutcome.X;
        WorkoutExercisePhase exercisePhase =
            feedbackPhase ?? GetExercisePhase(group);
        int selectionScoreBeforeDecision = GetSelectionScore(
            state,
            root,
            exercisePhase);
        if (!keep)
        {
            DownvoteSequenceInPhase(state, exercisePhase, root);
        }

        long decidedAtUnixMilliseconds = GetCurrentUnixTimeMilliseconds();
        RecordWorkoutDecision(
            state,
            group,
            root,
            outcome,
            selectionScoreBeforeDecision,
            exercisePhase,
            decidedAtUnixMilliseconds);
        state.Outcomes[group.Id] = outcome;
        if (state.ActiveModifierProtectedSelectionGroupId == group.SelectionKey &&
            GetActiveGroups(state)
                .Where(activeGroup =>
                    activeGroup.SelectionKey == group.SelectionKey)
                .All(activeGroup => state.Outcomes.ContainsKey(activeGroup.Id)))
        {
            state.ActiveModifierProtectedSelectionGroupId = null;
        }
        state.WorkoutCompleted = GetActiveGroups(state)
            .All(activeGroup => state.Outcomes.ContainsKey(activeGroup.Id));
        state.CompletionAcknowledged = false;
        if (state.WorkoutCompleted)
        {
            FinalizeActiveWorkoutSession(
                state,
                WorkoutSessionStatus.Completed,
                decidedAtUnixMilliseconds);
        }
        return new RecordedWorkoutOutcome(
            exercise,
            keep ? [] : Array.AsReadOnly(sequenceExercises));
    }

    private void ApplyShuffleRejection(
        WorkoutState state,
        string selectionGroupId,
        WorkoutExercisePhase phase,
        Exercise rejectedRoot,
        IReadOnlyList<Exercise> exercises)
    {
        HashSet<int> rejectedExerciseIds = exercises
            .Select(exercise => exercise.Id)
            .ToHashSet();
        DownvoteSequenceInPhase(state, phase, rejectedRoot);

        state.NextWorkoutExcludedExerciseIds.UnionWith(rejectedExerciseIds);
        RemoveSavedSequenceCopiesForSlot(
            state,
            selectionGroupId,
            rejectedRoot.Id);
    }

    public void AcknowledgeCompletion(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.WorkoutCompleted)
        {
            throw new InvalidOperationException("The workout is not complete.");
        }

        state.CompletionAcknowledged = true;
        FinalizeCurrentWorkout(state);
    }

    public Exercise? FinishInterruptedWorkout(WorkoutState state) =>
        FinishInterruptedWorkoutWithScoreUpdates(state).FirstOrDefault();

    public IReadOnlyList<Exercise> FinishInterruptedWorkoutWithScoreUpdates(
        WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.LegacySelectedExerciseNames.Count > 0)
        {
            Exercise? legacyPenalty = ResolveLegacyPendingRest(state);
            FinalizeActiveWorkoutSession(
                state,
                WorkoutSessionStatus.Interrupted);
            ResetToDurationSelection(state);
            ClearLegacyMigrationState(state);
            return legacyPenalty is null
                ? []
                : Array.AsReadOnly([legacyPenalty]);
        }

        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            FinalizeActiveWorkoutSession(
                state,
                WorkoutSessionStatus.Interrupted);
            ResetToDurationSelection(state);
            return [];
        }

        IReadOnlyList<Exercise> scoreUpdates = [];
        if (state.PendingRestGroupId is not null)
        {
            WorkoutGroup? pendingGroup = GetValidPendingRestGroup(state);
            if (pendingGroup is not null)
            {
                if (IsIntermediateSequenceBlock(state, pendingGroup))
                {
                    AdvanceSequence(state, pendingGroup);
                }
                else
                {
                    bool keep = state.PendingRestKept;
                    scoreUpdates = ApplySequenceOutcome(
                            state,
                            pendingGroup,
                            keep)
                        .ScoreUpdates;
                }
            }

            ClearPendingRest(state);
        }

        FinalizeCurrentWorkout(state);
        return scoreUpdates;
    }

    public (int Replaced, int Kept) GetOutcomeCounts(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        ExerciseOutcome[] decisionOutcomes = GetActiveGroups(state)
            .GroupBy(group => group.SelectionKey)
            .Select(rounds => rounds.OrderBy(round => round.Order).Last())
            .Where(round => state.Outcomes.ContainsKey(round.Id))
            .Select(round => state.Outcomes[round.Id])
            .ToArray();
        int replaced = decisionOutcomes.Count(outcome =>
            outcome == ExerciseOutcome.X);
        int kept = decisionOutcomes.Count(outcome =>
            outcome == ExerciseOutcome.Tick);
        return (replaced, kept);
    }

    public void ClearPendingRest(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.PendingRestGroupId = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
        state.PendingRestKept = false;
    }

    private void FinalizeCurrentWorkout(WorkoutState state)
    {
        // Closing a workout must not depend on finding a future lineup. Settle
        // its saved preferences only; PrepareWorkout arbitrates the next one
        // using that workout's duration, modifiers, phase scores and recovery.
        WorkoutGroup[] activeRounds = GetActiveGroups(state).ToArray();
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        var rejectedSelections = new List<(string GroupId, int RootId)>();
        foreach (WorkoutGroup selectionGroup in selectionGroups)
        {
            WorkoutGroup? decisionRound = activeRounds
                .Where(round => round.SelectionKey == selectionGroup.Id)
                .OrderBy(round => round.Order)
                .LastOrDefault();
            if (decisionRound is null ||
                !state.Outcomes.TryGetValue(
                    decisionRound.Id,
                    out ExerciseOutcome outcome))
            {
                continue;
            }

            int rootId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    selectionGroup.Id,
                    state.ActiveWorkoutModifiers));
            if (!_exercisesById.TryGetValue(rootId, out Exercise? root))
            {
                continue;
            }

            if (outcome == ExerciseOutcome.Tick)
            {
                KeepSequenceInSlot(state, selectionGroup.Id, root);
            }
            else if (outcome == ExerciseOutcome.X)
            {
                rejectedSelections.Add((selectionGroup.Id, root.Id));
            }
        }
        state.NextWorkoutExcludedExerciseIds.Clear();
        SyncLegacyKeptExerciseIds(state);
        foreach ((string groupId, int rootId) in rejectedSelections)
        {
            // Rejection is already a persisted phase-local downvote, not a
            // compatibility ban. Clear its cached slot without deleting Keeps.
            RemoveSavedSequenceCopiesForSlot(
                state,
                groupId,
                rootId);
        }
        FinalizeActiveWorkoutSession(
            state,
            state.WorkoutCompleted
                ? WorkoutSessionStatus.Completed
                : WorkoutSessionStatus.Interrupted);
        ResetToDurationSelection(state);
    }

    private Exercise? TryGetSelectedExercise(WorkoutState state, WorkoutGroup group)
    {
        try
        {
            return GetSelectedExercise(state, group);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private List<ShuffleCandidate> GetCompatibleShuffleCandidates(
        WorkoutState state,
        WorkoutGroup currentRound)
    {
        IReadOnlyList<WorkoutGroup> activeRounds = GetActiveGroups(state);
        if (activeRounds.Any(round =>
                round.SelectionKey == currentRound.SelectionKey &&
                state.Outcomes.ContainsKey(round.Id)) ||
            !IsLongWorkoutAllocationValid(state))
        {
            return [];
        }

        SelectedSequencePlacement? currentPlacement;
        try
        {
            currentPlacement = GetSelectedSequencePlacements(state)
                .SingleOrDefault(placement =>
                    placement.Anchor.Id == currentRound.SelectionKey);
        }
        catch (InvalidOperationException)
        {
            return [];
        }
        if (currentPlacement is null)
        {
            return [];
        }

        Exercise currentExercise = currentPlacement.Root;
        int currentExerciseId = currentExercise.Id;
        HashSet<string> coveredGroupIds = currentPlacement.CoveredGroups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<int> rejectedExerciseIds = GetSequenceExercises(currentExercise)
            .Select(exercise => exercise.Id)
            .ToHashSet();
        HashSet<string> startedSelectionGroupIds = activeRounds
            .Where(round => state.Outcomes.ContainsKey(round.Id))
            .Select(round => round.SelectionKey)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<int> unavailableExerciseIds = GetSelectionGroups(state)
            .Where(group => !coveredGroupIds.Contains(group.Id))
            .Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    group.Id,
                    state.ActiveWorkoutModifiers)))
            .Where(exerciseId => exerciseId > 0)
            .ToHashSet();
        HashSet<int> unavailableMovementIds = unavailableExerciseIds
            .Where(_exercisesById.ContainsKey)
            .Select(exerciseId => WorkoutModifierPolicy.GetSessionMovementId(
                _exercisesById[exerciseId]))
            .Append(WorkoutModifierPolicy.GetSessionMovementId(currentExercise))
            .ToHashSet();
        var candidates = new List<ShuffleCandidate>();
        foreach (Exercise exercise in _exercises.Where(exercise =>
                     exercise.Id != currentExerciseId &&
                     !state.NextWorkoutExcludedExerciseIds.Contains(exercise.Id) &&
                     !unavailableExerciseIds.Contains(exercise.Id) &&
                     !unavailableMovementIds.Contains(
                          WorkoutModifierPolicy.GetSessionMovementId(exercise)) &&
                     GetSequencePlacementOptions(
                             exercise,
                             GetSelectionGroups(state))
                         .Any(option => option.Select(group => group.Id)
                             .ToHashSet(StringComparer.Ordinal)
                             .SetEquals(coveredGroupIds)) &&
                     IsWorkoutSelectionCandidate(
                         state,
                         exercise,
                         currentPlacement.Anchor,
                         state.ActiveWorkoutModifiers)))
        {
            if (TryGetCompatibleShuffleAllocation(
                    state,
                    currentPlacement.CoveredGroups,
                    exercise,
                    startedSelectionGroupIds,
                    rejectedExerciseIds,
                    out LongWorkoutAllocation? allocation))
            {
                candidates.Add(new ShuffleCandidate(
                    exercise,
                    currentPlacement.CoveredGroups,
                    allocation));
            }
        }

        return candidates;
    }

    private bool TryGetCompatibleShuffleAllocation(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> coveredGroups,
        Exercise candidate,
        IReadOnlySet<string> startedSelectionGroupIds,
        IReadOnlySet<int> rejectedExerciseIds,
        [NotNullWhen(true)] out LongWorkoutAllocation? allocation)
    {
        Dictionary<string, int> previousExerciseIds = coveredGroups.ToDictionary(
            group => GetSelectionStorageKey(
                group.Id,
                state.ActiveWorkoutModifiers),
            group => state.SelectedExerciseIds[GetSelectionStorageKey(
                group.Id,
                state.ActiveWorkoutModifiers)],
            StringComparer.Ordinal);
        string selectionGroupId = coveredGroups
            .OrderBy(group => group.Order)
            .First()
            .Id;
        HashSet<int> priorSlotKeeps = GetKeptRootIdsForSlot(
            state,
            selectionGroupId)
            .ToHashSet();
        foreach (string selectionStorageKey in previousExerciseIds.Keys)
        {
            state.SelectedExerciseIds[selectionStorageKey] = candidate.Id;
        }
        GetKeptRootIdsForSlot(state, selectionGroupId)
            .Remove(GetSequenceRoot(_exercisesById[rejectedExerciseIds.First()]).Id);
        try
        {
            allocation = ChooseLongWorkoutAllocation(
                state,
                startedSelectionGroupIds);
            return true;
        }
        catch (InvalidOperationException)
        {
            allocation = null;
            return false;
        }
        finally
        {
            foreach ((string selectionStorageKey, int previousExerciseId) in
                     previousExerciseIds)
            {
                state.SelectedExerciseIds[selectionStorageKey] = previousExerciseId;
            }
            if (priorSlotKeeps.Count == 0)
            {
                state.KeptExerciseRootIdsBySelectionGroupId.Remove(
                    selectionGroupId);
            }
            else
            {
                state.KeptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
                    priorSlotKeeps;
            }
        }
    }

    private IReadOnlyDictionary<string, int> ChooseBestDistinctLineup(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers modifiers,
        IReadOnlyDictionary<string, int>? currentExerciseIds = null,
        bool allowSavedSelectionException = false,
        IReadOnlyDictionary<string, HashSet<int>>?
            carriedKeepRootIdsBySelectionGroupId = null,
        IReadOnlySet<string>? modifierTransitionProtectedGroupIds = null,
        IReadOnlyDictionary<string, WorkoutExercisePhase>?
            scheduledPhaseByGroupId = null,
        int reservedRepeatMinutes = 0)
    {
        if (groups.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        currentExerciseIds ??= new Dictionary<string, int>(StringComparer.Ordinal);
        carriedKeepRootIdsBySelectionGroupId ??=
            new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
        modifierTransitionProtectedGroupIds ??=
            new HashSet<string>(StringComparer.Ordinal);

        bool CalculateIsAllowed(Exercise exercise, WorkoutGroup group)
        {
            if (IsWorkoutSelectionCandidate(
                    state,
                    exercise,
                    group,
                    modifiers,
                    groups))
            {
                return true;
            }

            if (modifierTransitionProtectedGroupIds.Contains(group.Id) &&
                currentExerciseIds.GetValueOrDefault(group.Id) == exercise.Id)
            {
                return true;
            }

            return allowSavedSelectionException &&
                currentExerciseIds.GetValueOrDefault(group.Id) == exercise.Id &&
                IsSavedSelectionValid(
                    state,
                    exercise,
                    group,
                    modifiers,
                    groups);
        }

        var allowedGroupIdsByExerciseId = new Dictionary<int, HashSet<string>>();
        var candidates = new List<Exercise>();
        foreach (Exercise exercise in _exercises.Where(exercise =>
                     exercise.SequenceBlocks.Length > 0 &&
                     GetSequenceRoot(exercise).Id == exercise.Id))
        {
            HashSet<string> allowedGroupIds = groups
                .Where(group => CalculateIsAllowed(exercise, group))
                .Select(group => group.Id)
                .ToHashSet(StringComparer.Ordinal);
            if (allowedGroupIds.Count == 0)
            {
                continue;
            }

            candidates.Add(exercise);
            allowedGroupIdsByExerciseId[exercise.Id] = allowedGroupIds;
        }
        bool IsAllowed(Exercise exercise, WorkoutGroup group) =>
            allowedGroupIdsByExerciseId.GetValueOrDefault(exercise.Id)?
                .Contains(group.Id) == true;
        WorkoutExercisePhase GetSelectionPhase(WorkoutGroup group) =>
            scheduledPhaseByGroupId is not null &&
            scheduledPhaseByGroupId.TryGetValue(
                group.Id,
                out WorkoutExercisePhase scheduledPhase)
                ? scheduledPhase
                : GetProjectedSelectionPhase(state, group, groups.Count);
        Shuffle(candidates);

        int[] orderedScores = candidates
            .SelectMany(exercise => groups
                .Where(group => IsAllowed(exercise, group))
                .Select(group => GetSelectionScore(
                    state,
                    exercise,
                    GetSelectionPhase(group))))
            .Distinct()
            .Order()
            .ToArray();
        Dictionary<int, int> scoreRanks = orderedScores
            .Select((score, rank) => (score, rank))
            .ToDictionary(entry => entry.score, entry => entry.rank);
        Dictionary<string, int> highestScoreByGroup = groups.ToDictionary(
            group => group.Id,
            group => candidates
                .Where(exercise => IsAllowed(exercise, group))
                .Select(exercise => GetSelectionScore(
                    state,
                    exercise,
                    GetSelectionPhase(group)))
                .DefaultIfEmpty(int.MinValue)
                .Max(),
            StringComparer.Ordinal);
        long selectionTimeUnixMilliseconds = GetCurrentUnixTimeMilliseconds();
        Dictionary<long, int> freshHardMuscleRanks = candidates
            .SelectMany(exercise => groups
                .Where(group => IsAllowed(exercise, group))
                .Select(group => GetSequenceSelectionExerciseForGroup(
                    exercise,
                    group)))
            .Where(exercise =>
                WorkoutRecoveryPolicy.IsHardExercise(exercise) &&
                !WorkoutRecoveryPolicy.IsPrimaryMuscleRecovering(
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    exercise.PrimaryCanonicalGroup,
                    selectionTimeUnixMilliseconds))
            .Select(exercise =>
                WorkoutRecoveryPolicy.GetLastHardWorkUnixMilliseconds(
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    exercise.PrimaryCanonicalGroup))
            .Distinct()
            .OrderDescending()
            .Select((timestamp, rank) => (timestamp, rank))
            .ToDictionary(entry => entry.timestamp, entry => entry.rank);
        int maximumCoverage = groups.Max(group => group.CanonicalGroups.Count);
        // These are exact lexicographic assignment dimensions, not hardness
        // points. BigInteger keeps the ordering lossless for arbitrary saved
        // score histories without writing any derived value back to the user.
        BigInteger totalLowerPriorityRange =
            (BigInteger)groups.Count * maximumCoverage;
        BigInteger AddPriorityDimension(long maximumValue)
        {
            BigInteger weight = totalLowerPriorityRange + BigInteger.One;
            totalLowerPriorityRange +=
                (BigInteger)groups.Count * maximumValue * weight;
            return weight;
        }

        BigInteger primaryWeight = AddPriorityDimension(1L);
        BigInteger equipmentPreferenceWeight = AddPriorityDimension(2L);
        BigInteger currentSelectionWeight = AddPriorityDimension(1L);
        BigInteger hardMuscleAgeWeight = AddPriorityDimension(
            Math.Max(0, freshHardMuscleRanks.Count - 1));
        BigInteger moderateRecoveryAvoidanceWeight = AddPriorityDimension(1L);
        BigInteger hardRecoveryAvoidanceWeight = AddPriorityDimension(1L);
        BigInteger freshHardWeight = AddPriorityDimension(1L);
        BigInteger scoreWeight = AddPriorityDimension(
            Math.Max(0, orderedScores.Length - 1));
        BigInteger keptExerciseWeight = AddPriorityDimension(1L);
        BigInteger hardOpportunityWeight = AddPriorityDimension(1L);
        BigInteger lightDayOpportunityWeight = AddPriorityDimension(1L);
        BigInteger preservedActiveSelectionWeight = allowSavedSelectionException
            ? totalLowerPriorityRange + BigInteger.One
            : BigInteger.Zero;

        bool HasLightDayOpportunity(
            Exercise exercise,
            WorkoutGroup preferenceSlot)
        {
            return modifiers.HasFlag(WorkoutModifiers.Light) &&
                IsDemandZeroSequence(exercise);
        }

        BigInteger CalculateUtility(
            Exercise exercise,
            WorkoutGroup evaluationGroup,
            bool includeSlotPreference)
        {
            Exercise selectionExercise =
                GetSequenceSelectionExerciseForGroup(exercise, evaluationGroup);
            HardExerciseRotationStatus hardRotationStatus =
                WorkoutRecoveryPolicy.GetRotationStatus(
                    selectionExercise,
                    evaluationGroup,
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    selectionTimeUnixMilliseconds);
            bool isRecoveringModerate =
                WorkoutRecoveryPolicy.IsModerateExerciseRecovering(
                    selectionExercise,
                    state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
                    selectionTimeUnixMilliseconds);
            int hardMuscleAgeRank = hardRotationStatus ==
                    HardExerciseRotationStatus.FreshHard
                ? freshHardMuscleRanks.GetValueOrDefault(
                    WorkoutRecoveryPolicy.GetLastHardWorkUnixMilliseconds(
                        state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                        selectionExercise.PrimaryCanonicalGroup))
                : 0;
            bool isKept = includeSlotPreference &&
                (IsSequenceKept(
                    state,
                    evaluationGroup.Id,
                    exercise) ||
                 carriedKeepRootIdsBySelectionGroupId
                     .GetValueOrDefault(evaluationGroup.Id)?
                     .Contains(GetSequenceRoot(exercise).Id) == true);
            WorkoutExercisePhase phase = GetSelectionPhase(evaluationGroup);
            int selectionScore = includeSlotPreference
                ? GetSelectionScore(state, exercise, phase)
                : 0;
            bool isDownvotedInPhase = includeSlotPreference &&
                GetPhaseScoreAdjustment(state, exercise, phase) < 0;
            bool hasHardOpportunity = includeSlotPreference &&
                hardRotationStatus == HardExerciseRotationStatus.FreshHard &&
                !isDownvotedInPhase &&
                (isKept || selectionScore ==
                    highestScoreByGroup[evaluationGroup.Id]);
            bool hasContextualKeepPreference = includeSlotPreference &&
                isKept &&
                !isDownvotedInPhase &&
                hardRotationStatus != HardExerciseRotationStatus.RecoveringHard &&
                !isRecoveringModerate;
            bool isCurrentSelection = includeSlotPreference &&
                currentExerciseIds.GetValueOrDefault(evaluationGroup.Id) ==
                    exercise.Id;
            return
                (allowSavedSelectionException && isCurrentSelection
                    ? preservedActiveSelectionWeight
                    : BigInteger.Zero) +
                (includeSlotPreference && HasLightDayOpportunity(
                        exercise,
                        evaluationGroup)
                    ? lightDayOpportunityWeight
                    : BigInteger.Zero) +
                (hasHardOpportunity
                    ? hardOpportunityWeight
                    : BigInteger.Zero) +
                (hasContextualKeepPreference
                    ? keptExerciseWeight
                    : BigInteger.Zero) +
                (includeSlotPreference
                    ? scoreRanks[selectionScore] * scoreWeight
                    : BigInteger.Zero) +
                (hardRotationStatus != HardExerciseRotationStatus.RecoveringHard
                    ? hardRecoveryAvoidanceWeight
                    : BigInteger.Zero) +
                (!isRecoveringModerate
                    ? moderateRecoveryAvoidanceWeight
                    : BigInteger.Zero) +
                (hardRotationStatus == HardExerciseRotationStatus.FreshHard
                    ? freshHardWeight
                    : BigInteger.Zero) +
                hardMuscleAgeRank * hardMuscleAgeWeight +
                (isCurrentSelection
                    ? currentSelectionWeight
                    : BigInteger.Zero) +
                WorkoutModifierPolicy.GetEquipmentPreferenceCount(
                    selectionExercise,
                    modifiers) * equipmentPreferenceWeight +
                (WorkoutCoveragePolicy.IsPrimaryForGroup(
                        selectionExercise,
                        evaluationGroup)
                    ? primaryWeight
                    : BigInteger.Zero) +
                WorkoutSequencePolicy.GetCanonicalCoverage(
                    exercise,
                    _exercisesById,
                    evaluationGroup);
        }

        var allowed = new bool[groups.Count, candidates.Count];
        var baseUtilities = new BigInteger[groups.Count, candidates.Count];
        var anchorUtilities = new BigInteger[groups.Count, candidates.Count];
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            WorkoutGroup group = groups[groupIndex];
            for (int exerciseIndex = 0;
                 exerciseIndex < candidates.Count;
                 exerciseIndex++)
            {
                Exercise exercise = candidates[exerciseIndex];
                if (!IsAllowed(exercise, group))
                {
                    continue;
                }

                allowed[groupIndex, exerciseIndex] = true;
                baseUtilities[groupIndex, exerciseIndex] = CalculateUtility(
                    exercise,
                    group,
                    includeSlotPreference: false);
                anchorUtilities[groupIndex, exerciseIndex] = CalculateUtility(
                    exercise,
                    group,
                    includeSlotPreference: true);
            }
        }

        var atomicCandidates = new List<AtomicSequenceCandidate>();
        for (int candidateIndex = 0;
             candidateIndex < candidates.Count;
             candidateIndex++)
        {
            Exercise candidate = candidates[candidateIndex];
            var placementOptions = GetSequencePlacementOptions(
                    candidate,
                    groups)
                .ToList();
            if (allowSavedSelectionException)
            {
                foreach (int groupIndex in Enumerable.Range(0, groups.Count)
                             .Where(groupIndex =>
                                 currentExerciseIds.GetValueOrDefault(
                                     groups[groupIndex].Id) == candidate.Id &&
                                 allowed[groupIndex, candidateIndex] &&
                                 placementOptions.All(option =>
                                     option.All(candidateGroup =>
                                         candidateGroup.Id !=
                                            groups[groupIndex].Id))))
                {
                    placementOptions.Add([groups[groupIndex]]);
                }
            }

            foreach (WorkoutGroup[] placementGroups in placementOptions)
            {
                if (candidate.SequenceBlocks.Length +
                        (groups.Count - placementGroups.Length) >
                    state.ActiveWorkoutMinutes)
                {
                    // Even if every remaining slot used one block, this
                    // placement could not fit the requested duration.
                    continue;
                }
                ulong coverageMask = 0;
                bool placementAllowed = true;
                foreach (WorkoutGroup placementGroup in placementGroups)
                {
                    int groupIndex = groups
                        .Select((group, index) => (group, index))
                        .Single(entry => entry.group.Id == placementGroup.Id)
                        .index;
                    if (!allowed[groupIndex, candidateIndex])
                    {
                        placementAllowed = false;
                        break;
                    }
                    coverageMask |= 1UL << groupIndex;
                }
                if (!placementAllowed)
                {
                    continue;
                }

                WorkoutGroup preferenceSlot = placementGroups
                    .OrderBy(group => group.Order)
                    .First();
                int preferenceSlotIndex = groups
                    .Select((group, index) => (group, index))
                    .Single(entry => entry.group.Id == preferenceSlot.Id)
                    .index;
                var utilitiesByGroup = new BigInteger[groups.Count];
                for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
                {
                    if ((coverageMask & (1UL << groupIndex)) != 0)
                    {
                        utilitiesByGroup[groupIndex] = groupIndex ==
                                preferenceSlotIndex
                            ? anchorUtilities[groupIndex, candidateIndex]
                            : baseUtilities[groupIndex, candidateIndex];
                    }
                }
                if (HasLightDayOpportunity(candidate, preferenceSlot))
                {
                    // Reward every slot covered by an all-demand-zero atomic
                    // sequence. Counting only its anchor could tie it with one
                    // easy singleton plus non-easy work in the other slots.
                    for (int groupIndex = 0;
                         groupIndex < groups.Count;
                         groupIndex++)
                    {
                        if (groupIndex != preferenceSlotIndex &&
                            (coverageMask & (1UL << groupIndex)) != 0)
                        {
                            utilitiesByGroup[groupIndex] +=
                                lightDayOpportunityWeight;
                        }
                    }
                }
                atomicCandidates.Add(new AtomicSequenceCandidate(
                    candidate.Id,
                    WorkoutModifierPolicy.GetSessionMovementId(candidate),
                    coverageMask,
                    candidate.SequenceBlocks.Length,
                    utilitiesByGroup,
                    candidateIndex));
            }
        }

        AtomicSequenceLineup? solution = AtomicSequenceLineupSolver.Solve(
            groups.Count,
            state.ActiveWorkoutMinutes - reservedRepeatMinutes,
            atomicCandidates) ?? AtomicSequenceLineupSolver.SolveAllowingRepeatedMovements(
                groups.Count,
                state.ActiveWorkoutMinutes - reservedRepeatMinutes,
                atomicCandidates);
        if (solution is null)
        {
            int movementCount = atomicCandidates
                .Select(candidate => candidate.MovementId)
                .Distinct()
                .Count();
            throw new InvalidOperationException(CreateDistinctLineupException(groups, movementCount).Message +
                $" Budget: {state.ActiveWorkoutMinutes - reservedRepeatMinutes}; unavailable slots: " +
                string.Join(',', groups.Where((g, i) => !atomicCandidates.Any(c =>
                    (c.CoverageMask & (1UL << i)) != 0)).Select(g => g.Id)));
        }

        return solution.ExerciseIdByGroupIndex.ToDictionary(
            entry => groups[entry.Key].Id,
            entry => entry.Value,
            StringComparer.Ordinal);
    }

    private void RepairActiveLineup(
        WorkoutState state,
        bool preserveCurrentSelections = true)
    {
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        IReadOnlyList<WorkoutGroup> activeRounds;
        try
        {
            activeRounds = GetActiveGroups(state);
        }
        catch (InvalidOperationException)
        {
            activeRounds = [];
        }
        Dictionary<string, int> currentExerciseIds = selectionGroups
            .Select(group => new
            {
                group.Id,
                ExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers)),
            })
            .Where(entry => entry.ExerciseId != 0)
            .ToDictionary(
                entry => entry.Id,
                entry => entry.ExerciseId,
                StringComparer.Ordinal);
        IReadOnlyDictionary<string, int> repairedLineup = ChooseBestDistinctLineup(
            state,
            selectionGroups,
            state.ActiveWorkoutModifiers,
            currentExerciseIds: currentExerciseIds,
            allowSavedSelectionException: preserveCurrentSelections);
        ApplyDistinctLineup(
            state,
            selectionGroups,
            repairedLineup,
            clearChangedProgress: true,
            activeRounds);
    }

    private void NormalizeActiveModifierTransitionProtection(WorkoutState state)
    {
        string? protectedSelectionGroupId =
            state.ActiveModifierProtectedSelectionGroupId;
        if (string.IsNullOrWhiteSpace(protectedSelectionGroupId) ||
            PendingRestMatchesSelectionGroup(
                state,
                protectedSelectionGroupId))
        {
            return;
        }

        int rootId = state.SelectedExerciseIds.GetValueOrDefault(
            GetSelectionStorageKey(
                protectedSelectionGroupId,
                state.ActiveWorkoutModifiers));
        bool remainsCompatible =
            _exercisesById.TryGetValue(rootId, out Exercise? root) &&
            GetSequenceRoot(root).Id == root.Id &&
            GetSequenceExercises(root).All(member =>
                IsCompatibleWithModifiers(
                    member,
                    state.ActiveWorkoutModifiers));
        // Current work is no longer privileged merely because it is compatible.
        // Older builds persisted that one-way exception; remove it on upgrade.
        state.ActiveModifierProtectedSelectionGroupId = null;
        if (!remainsCompatible)
        {
            // An incompatible legacy movement cannot keep its partial timer.
            ClearPendingMovement(state);
        }
    }

    private void NormalizeActiveModifierRetainedSelectionGroups(
        WorkoutState state)
    {
        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            state.ActiveModifierRetainedSelectionGroupIds.Clear();
            return;
        }

        HashSet<int> completedRootIds = state.ActiveWorkoutSession
            ?.Decisions
            .Select(decision => decision.RootExerciseId)
            .Where(rootExerciseId => rootExerciseId > 0)
            .ToHashSet() ?? [];
        string? protectedSelectionGroupId =
            state.ActiveModifierProtectedSelectionGroupId;

        HashSet<string> validGroupIds = (state.ActiveDurationSelectionGroupIds ??
            GetBaseResolution(state.ActiveWorkoutMinutes).Groups.Select(group => group.Id).ToList())
            .ToHashSet(StringComparer.Ordinal);
        state.ActiveModifierRetainedSelectionGroupIds.RemoveWhere(groupId =>
            !validGroupIds.Contains(groupId) ||
            (!completedRootIds.Contains(state.SelectedExerciseIds.GetValueOrDefault(
                 GetSelectionStorageKey(
                     groupId,
                     state.ActiveWorkoutModifiers))) &&
             !(groupId == protectedSelectionGroupId &&
               PendingRestMatchesSelectionGroup(
                   state,
                   protectedSelectionGroupId))));
    }

    private void CarrySlotPreferencesForward(WorkoutState state)
    {
        if (state.KeptExerciseRootIdsBySelectionGroupId.Values.All(
                keptRootIds => keptRootIds.Count == 0))
        {
            return;
        }

        WorkoutGroup[] targetGroups = GetSelectionGroups(state).ToArray();
        IReadOnlyDictionary<string, HashSet<int>> carriedKeepRootIdsByGroup =
            BuildCrossResolutionKeepPreferences(state, targetGroups);
        Dictionary<string, int> currentExerciseIds = targetGroups
            .Select(group => new
            {
                group.Id,
                ExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers)),
            })
            .Where(entry => entry.ExerciseId != 0)
            .ToDictionary(
                entry => entry.Id,
                entry => entry.ExerciseId,
                StringComparer.Ordinal);
        IReadOnlyDictionary<string, int> carriedLineup = ChooseBestDistinctLineup(
            state,
            targetGroups,
            state.ActiveWorkoutModifiers,
            currentExerciseIds: currentExerciseIds,
            carriedKeepRootIdsBySelectionGroupId:
                carriedKeepRootIdsByGroup);
        ApplyDistinctLineup(
            state,
            targetGroups,
            carriedLineup,
            clearChangedProgress: false);

        bool addedCarriedKeep = false;
        foreach (SelectedSequencePlacement placement in
                 GetSelectedSequencePlacements(state))
        {
            if (carriedKeepRootIdsByGroup
                    .GetValueOrDefault(placement.Anchor.Id)?
                    .Contains(placement.Root.Id) != true)
            {
                continue;
            }

            addedCarriedKeep |= GetKeptRootIdsForSlot(
                    state,
                    placement.Anchor.Id)
                .Add(placement.Root.Id);
        }
        if (addedCarriedKeep)
        {
            SyncLegacyKeptExerciseIds(state);
        }
    }

    private IReadOnlyDictionary<string, HashSet<int>>
        BuildCrossResolutionKeepPreferences(
            WorkoutState state,
            IReadOnlyList<WorkoutGroup> targetGroups)
    {
        HashSet<string> targetGroupIds = targetGroups
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<int> rootsWithTargetResolutionKeeps = state
            .KeptExerciseRootIdsBySelectionGroupId
            .Where(entry => targetGroupIds.Contains(entry.Key))
            .SelectMany(entry => entry.Value)
            .ToHashSet();
        var carriedKeepRootIdsByGroup =
            new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        foreach ((string sourceGroupId, HashSet<int> keptRootIds) in
                 state.KeptExerciseRootIdsBySelectionGroupId)
        {
            if (targetGroupIds.Contains(sourceGroupId) ||
                !KnownWorkoutGroups.TryGetValue(
                    sourceGroupId,
                    out WorkoutGroup? sourceGroup))
            {
                continue;
            }

            foreach (int rootId in keptRootIds)
            {
                if (rootsWithTargetResolutionKeeps.Contains(rootId) ||
                    !_exercisesById.TryGetValue(rootId, out Exercise? root) ||
                    GetSequenceRoot(root).Id != rootId)
                {
                    continue;
                }

                Exercise sourceSelectionExercise =
                    GetSequenceSelectionExerciseForGroup(root, sourceGroup);
                WorkoutGroup targetPrimaryGroup = targetGroups.Single(group =>
                    group.CanonicalGroups.Contains(
                        sourceSelectionExercise.PrimaryCanonicalGroup));
                WorkoutGroup[]? mappedPlacement = GetSequencePlacementOptions(
                        root,
                        targetGroups)
                    .Where(option => option.Any(group =>
                        group.Id == targetPrimaryGroup.Id))
                    .OrderByDescending(option => option.Length)
                    .ThenBy(option => option.Min(group => group.Order))
                    .FirstOrDefault();
                if (mappedPlacement is null)
                {
                    continue;
                }

                WorkoutGroup targetAnchor = mappedPlacement
                    .OrderBy(group => group.Order)
                    .First();
                if (!carriedKeepRootIdsByGroup.TryGetValue(
                        targetAnchor.Id,
                        out HashSet<int>? targetKeeps))
                {
                    targetKeeps = [];
                    carriedKeepRootIdsByGroup[targetAnchor.Id] = targetKeeps;
                }
                targetKeeps.Add(rootId);
            }
        }

        return carriedKeepRootIdsByGroup;
    }

    private void ApplyDistinctLineup(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> groups,
        IReadOnlyDictionary<string, int> lineup,
        bool clearChangedProgress,
        IReadOnlyList<WorkoutGroup>? activeRounds = null)
    {
        activeRounds ??= clearChangedProgress
            ? GetActiveGroups(state)
            : [];
        foreach (WorkoutGroup group in groups)
        {
            string selectionStorageKey = GetSelectionStorageKey(
                group.Id,
                state.ActiveWorkoutModifiers);
            int previousExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
                selectionStorageKey);
            int nextExerciseId = lineup[group.Id];
            state.SelectedExerciseIds[selectionStorageKey] = nextExerciseId;
            if (!clearChangedProgress || previousExerciseId == nextExerciseId)
            {
                continue;
            }

            foreach (WorkoutGroup round in activeRounds.Where(round =>
                         round.SelectionKey == group.Id))
            {
                state.Outcomes.Remove(round.Id);
            }

            if (PendingRestMatchesSelectionGroup(state, group.Id))
            {
                ClearPendingRest(state);
            }
        }
    }

    private InvalidOperationException CreateDistinctLineupException(
        IReadOnlyList<WorkoutGroup> groups,
        int movementCount)
    {
        return new InvalidOperationException(
            $"No complete exercise lineup exists for the active workout profile " +
            $"across {groups.Count} groups and {movementCount} eligible session " +
            $"movements " +
            $"with at least {WorkoutCoveragePolicy.MinimumCoveragePercent}% coverage.");
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (int index = items.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private bool IsSavedSelectionValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers,
        IReadOnlyList<WorkoutGroup>? selectionGroups = null)
    {
        if (IsWorkoutSelectionCandidate(
                state,
                exercise,
                group,
                modifiers,
                selectionGroups))
        {
            return true;
        }

        if (IsModifierTransitionProtectedSelection(
                state,
                exercise,
                group,
                modifiers))
        {
            return true;
        }

        if (IsModifierTransitionRetainedCompletedSelection(
                state,
                exercise,
                group,
                modifiers))
        {
            return true;
        }

        if (!PendingRestMatchesSelectionGroup(state, group.SelectionKey) ||
            GetSequenceRoot(exercise).Id != exercise.Id)
        {
            return false;
        }

        return GetSequenceExercises(exercise).All(member =>
            IsCompatibleWithModifiers(member, modifiers) &&
            IsAssignedToGroup(member, group));
    }

    private bool IsSequenceOverrideValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (group.SequenceBlockIndex < 0 ||
            group.SequenceBlockIndex >= group.SequenceBlockCount)
        {
            return false;
        }

        string selectionStorageKey = GetSelectionStorageKey(
            group.SelectionKey,
            modifiers);
        return state.SelectedExerciseIds.TryGetValue(
            selectionStorageKey,
                out int rootExerciseId) &&
            _exercisesById.TryGetValue(rootExerciseId, out Exercise? root) &&
            root.SequenceBlocks.Length == group.SequenceBlockCount &&
            root.SequenceBlocks[group.SequenceBlockIndex].ExerciseId == exercise.Id &&
            root.SequenceBlocks[group.SequenceBlockIndex].SideCue ==
                group.SequenceSideCue &&
            root.SequenceBlocks[group.SequenceBlockIndex].DirectionCue ==
                group.SequenceDirectionCue &&
            root.SequenceBlocks[group.SequenceBlockIndex].MirrorMedia ==
                group.MirrorSequenceMedia &&
            root.SequenceBlocks[group.SequenceBlockIndex].MediaSegment ==
                group.SequenceMediaSegment &&
            (IsWorkoutSelectionCandidate(state, root, group, modifiers) ||
             IsModifierTransitionProtectedSelection(
                 state,
                 root,
                 group,
                 modifiers) ||
             IsModifierTransitionRetainedCompletedSelection(
                 state,
                 root,
                 group,
                 modifiers));
    }

    private bool IsModifierTransitionProtectedSelection(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        string? protectedSelectionGroupId =
            state.ActiveModifierProtectedSelectionGroupId;
        if (string.IsNullOrWhiteSpace(protectedSelectionGroupId) ||
            NormalizeWorkoutModifiers(modifiers) !=
                state.ActiveWorkoutModifiers)
        {
            return false;
        }

        Exercise root = GetSequenceRoot(exercise);
        if (state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    protectedSelectionGroupId,
                    modifiers)) != root.Id)
        {
            return false;
        }
        if (group.SelectionKey == protectedSelectionGroupId)
        {
            return true;
        }

        IReadOnlyList<WorkoutGroup> resolutionGroups = GetSelectionGroups(state);
        return GetSequencePlacementOptions(root, resolutionGroups).Any(option =>
            option.OrderBy(candidate => candidate.Order).First().Id ==
                protectedSelectionGroupId &&
            option.Any(candidate => candidate.Id == group.SelectionKey));
    }

    private bool IsModifierTransitionRetainedCompletedSelection(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (state.ActiveModifierRetainedSelectionGroupIds.Count == 0 ||
            NormalizeWorkoutModifiers(modifiers) !=
                state.ActiveWorkoutModifiers)
        {
            return false;
        }

        Exercise root = GetSequenceRoot(exercise);
        if (state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    group.SelectionKey,
                    modifiers)) != root.Id)
        {
            return false;
        }

        bool coversRetainedGroup = state.ActiveModifierRetainedSelectionGroupIds
            .Any(selectionGroupId => state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(selectionGroupId, modifiers)) == root.Id);
        return coversRetainedGroup &&
            state.ActiveWorkoutSession?.Decisions.Any(decision =>
                decision.RootExerciseId == root.Id) == true;
    }

    private bool IsWorkoutSelectionCandidate(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers,
        IReadOnlyList<WorkoutGroup>? selectionGroups = null)
    {
        if (exercise.SequenceBlocks.Length == 0 ||
            GetSequenceRoot(exercise).Id != exercise.Id)
        {
            return false;
        }

        IReadOnlyList<WorkoutGroup> resolutionGroups =
            selectionGroups ?? GetSelectionGroups(state);
        if (resolutionGroups.Count == 0)
        {
            resolutionGroups = GetResolutionGroupsForGroup(group);
        }
        WorkoutGroup[][] placementOptions = GetSequencePlacementOptions(
            exercise,
            resolutionGroups);
        return placementOptions.Any(option => option.Any(candidate =>
                candidate.CanonicalGroups.SetEquals(group.CanonicalGroups))) &&
            GetSequenceExercises(exercise).All(member =>
                IsCompatibleWithModifiers(member, modifiers));
    }

    private Exercise GetSequenceRoot(Exercise exercise)
    {
        return _sequenceRootByExerciseId.TryGetValue(
                exercise.Id,
                out Exercise? root)
            ? root
            : throw new InvalidOperationException(
                $"Exercise {exercise.Id} has no sequence root.");
    }

    private Exercise[] GetSequenceExercises(Exercise exercise)
    {
        Exercise root = GetSequenceRoot(exercise);
        return _sequenceExercisesByRootId[root.Id];
    }

    private bool IsDemandZeroSequence(Exercise exercise)
    {
        return GetSequenceExercises(exercise).All(member =>
            member.MuscularDemand == Exercise.MinimumMuscularDemand);
    }

    private Exercise GetSequenceSelectionExerciseForGroup(
        Exercise exercise,
        WorkoutGroup group)
    {
        Exercise root = GetSequenceRoot(exercise);
        Exercise[] primaryMembers = root.SequenceBlocks
            .Select(block => _exercisesById[block.ExerciseId])
            .Where(member => group.CanonicalGroups.Contains(
                member.PrimaryCanonicalGroup))
            .ToArray();
        return primaryMembers
            .OrderByDescending(member => member.MuscularDemand)
            .ThenByDescending(member => member.Id == root.Id)
            .FirstOrDefault() ??
            root;
    }

    private WorkoutGroup[][] GetSequencePlacementOptions(
        Exercise exercise,
        IReadOnlyList<WorkoutGroup> groups)
    {
        Exercise root = GetSequenceRoot(exercise);
        string cacheKey = $"{root.Id}:" + string.Join(
            '|',
            groups.Select(group => group.Id));
        if (_sequencePlacementOptionsCache.TryGetValue(
                cacheKey,
                out WorkoutGroup[][]? cached))
        {
            return cached;
        }

        WorkoutGroup[][] options = WorkoutSequencePolicy.GetPlacementOptions(
            root,
            _exercisesById,
            groups);
        _sequencePlacementOptionsCache[cacheKey] = options;
        return options;
    }

    private int GetSelectionScore(
        WorkoutState state,
        Exercise exercise,
        WorkoutExercisePhase phase)
    {
        Exercise root = GetSequenceRoot(exercise);
        int legacyScore = GetSequenceExercises(root)
            .Min(member => member.Score);
        return legacyScore + GetPhaseScoreAdjustment(state, root, phase);
    }

    private int GetPhaseScoreAdjustment(
        WorkoutState state,
        Exercise exercise,
        WorkoutExercisePhase phase)
    {
        if (phase == WorkoutExercisePhase.Unknown)
        {
            return 0;
        }

        return state.ExerciseScoreAdjustmentsByPhase
            .GetValueOrDefault(phase)?
            .GetValueOrDefault(GetSequenceRoot(exercise).Id) ?? 0;
    }

    private void DownvoteSequenceInPhase(
        WorkoutState state,
        WorkoutExercisePhase phase,
        Exercise exercise)
    {
        if (phase == WorkoutExercisePhase.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        Exercise root = GetSequenceRoot(exercise);
        if (!state.ExerciseScoreAdjustmentsByPhase.TryGetValue(
                phase,
                out Dictionary<int, int>? adjustments))
        {
            adjustments = [];
            state.ExerciseScoreAdjustmentsByPhase[phase] = adjustments;
        }

        adjustments[root.Id] = adjustments.GetValueOrDefault(root.Id) - 1;
    }

    private static WorkoutExercisePhase GetExercisePhase(WorkoutGroup group) =>
        WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(group.Order);

    private static WorkoutExercisePhase GetProjectedSelectionPhase(
        WorkoutState state,
        WorkoutGroup group,
        int selectionGroupCount)
    {
        if (selectionGroupCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectionGroupCount));
        }

        int workoutMinutes = Math.Max(selectionGroupCount, state.ActiveWorkoutMinutes);
        int projectedFinalBlockOrder = checked(
            (group.Order * workoutMinutes + selectionGroupCount - 1) /
            selectionGroupCount);
        return WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
            projectedFinalBlockOrder);
    }

    private long GetCurrentUnixTimeMilliseconds() =>
        _utcNowProvider().ToUnixTimeMilliseconds();

    private bool IsCompatibleWithModifiers(
        Exercise exercise,
        WorkoutModifiers modifiers)
    {
        return WorkoutModifierPolicy.IsCompatible(exercise, modifiers);
    }

    private bool PendingRestMatchesSelectionGroup(
        WorkoutState state,
        string selectionGroupId)
    {
        if (state.PendingRestGroupId is not string pendingRoundId)
        {
            return false;
        }

        return string.Equals(
                pendingRoundId,
                selectionGroupId,
                StringComparison.Ordinal) ||
            pendingRoundId.StartsWith(
                selectionGroupId + ".",
                StringComparison.Ordinal);
    }

    private static bool IsAssignedToGroup(Exercise exercise, WorkoutGroup group)
    {
        return group.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Any(group.CanonicalGroups.Contains);
    }

    private void MigrateLegacyLineups(WorkoutState state)
    {
        foreach ((string legacyGroup, string exerciseName) in
                 state.LegacySelectedExerciseNames)
        {
            bool pendingNoKeep = legacyGroup == state.LegacyPendingRestGroup &&
                !state.PendingRestKept;
            bool wasRejected = state.LegacyOutcomes.TryGetValue(
                    legacyGroup,
                    out ExerciseOutcome outcome) &&
                outcome == ExerciseOutcome.X;
            if (pendingNoKeep || wasRejected)
            {
                continue;
            }

            Exercise? exercise = _exercises.FirstOrDefault(candidate =>
                candidate.Name == exerciseName);
            if (exercise is null)
            {
                continue;
            }

            foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
            {
                WorkoutGroup group = MassGroupingTaxonomy.GetGroup(
                    minutes,
                    exercise.PrimaryCanonicalGroup);
                state.SelectedExerciseIds.TryAdd(group.Id, exercise.Id);
            }
        }

        state.Outcomes.Clear();
    }

    private void MigrateSlotScopedPreferences(WorkoutState state)
    {
        // Session logs are the only trustworthy historical evidence of the
        // slot in which a Keep occurred. Replay them in workout order so a
        // later decision changes only that same slot.
        IEnumerable<WorkoutSessionLog> sessions = state.WorkoutHistory
            .Append(state.ActiveWorkoutSession)
            .OfType<WorkoutSessionLog>()
            .OrderBy(session => session.StartedAtUnixMilliseconds)
            .ThenBy(session => session.SessionId);
        foreach (WorkoutSessionLog session in sessions)
        {
            foreach (WorkoutSelectionSnapshot selection in
                     session.InitialSelections.Where(selection =>
                         selection.WasKeptAtWorkoutStart))
            {
                if (_exercisesById.TryGetValue(
                        selection.RootExerciseId,
                        out Exercise? root))
                {
                    KeepSequenceInSlot(
                        state,
                        selection.SelectionGroupId,
                        root);
                }
            }

            foreach (WorkoutSelectionChangeLog change in
                     session.SelectionChanges.OrderBy(change =>
                         change.ChangedAtUnixMilliseconds))
            {
                if (_exercisesById.TryGetValue(
                        change.RejectedRootExerciseId,
                        out Exercise? rejectedRoot))
                {
                    RemoveSequenceKeep(
                        state,
                        change.SelectionGroupId,
                        rejectedRoot);
                }
            }

            foreach (WorkoutDecisionLog decision in session.Decisions
                         .OrderBy(decision =>
                             decision.DecidedAtUnixMilliseconds))
            {
                if (!_exercisesById.TryGetValue(
                        decision.RootExerciseId,
                        out Exercise? root))
                {
                    continue;
                }

                if (decision.Outcome == ExerciseOutcome.Tick)
                {
                    KeepSequenceInSlot(state, decision.SelectionGroupId, root);
                }
                else if (decision.Outcome == ExerciseOutcome.X)
                {
                    RemoveSequenceKeep(state, decision.SelectionGroupId, root);
                }
            }
        }

        // Pre-logging keeps have no historical slot record. A saved lineup is
        // still concrete evidence: map the complete legacy sequence Keep to
        // the anchor where that exact sequence is currently stored. Never fan
        // it out to every anatomically compatible slot.
        foreach (var savedPlacement in
                 state.SelectedExerciseIds
                     .Select(entry =>
                     {
                         bool parsed = TryParseSelectionStorageKey(
                             entry.Key,
                             out string selectionGroupId,
                             out WorkoutModifiers modifiers);
                         int rootId = _exercisesById.TryGetValue(
                                 entry.Value,
                                 out Exercise? selected)
                             ? GetSequenceRoot(selected).Id
                             : 0;
                         return new
                         {
                             parsed,
                             selectionGroupId,
                             modifiers,
                             rootId,
                         };
                     })
                     .Where(entry => entry.parsed && entry.rootId > 0 &&
                         KnownWorkoutGroups.ContainsKey(entry.selectionGroupId))
                     .Select(entry => (
                         entry.modifiers,
                         SelectionGroupId: entry.selectionGroupId,
                         RootId: entry.rootId))
                     .GroupBy(entry => (
                         entry.modifiers,
                         Resolution: entry.SelectionGroupId.Split('.')[0],
                         entry.RootId)))
        {
            Exercise root = _exercisesById[savedPlacement.Key.RootId];
            if (!GetSequenceExercises(root).All(member =>
                    state.LastKeptExerciseIds.Contains(member.Id)))
            {
                continue;
            }

            string anchorId = savedPlacement
                .Select(entry => entry.SelectionGroupId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(groupId => KnownWorkoutGroups[groupId].Order)
                .First();
            KeepSequenceInSlot(state, anchorId, root);
        }

        // The old global scores remain a read-only baseline because states
        // created before this migration did not record enough information to
        // assign every old vote truthfully. All new adjustments start empty
        // and are consequently exact and slot-local.
        state.ExerciseScoreAdjustmentsBySelectionGroupId.Clear();
    }

    private void MigratePhaseScopedDownvotes(WorkoutState state)
    {
        WorkoutSessionLog[] loggedSessions = state.WorkoutHistory
            .Append(state.ActiveWorkoutSession)
            .OfType<WorkoutSessionLog>()
            .ToArray();

        // Earlier preference models removed a slot Keep when that same slot
        // was rejected. Phase-local rejection no longer means "unkeep
        // everywhere", so restore every still-valid historical Keep before
        // replaying phase-provenance adjustments when they exist.
        foreach (WorkoutSessionLog session in loggedSessions)
        {
            foreach (WorkoutDecisionLog decision in session.Decisions.Where(
                         decision => decision.Outcome == ExerciseOutcome.Tick))
            {
                if (KnownWorkoutGroups.ContainsKey(decision.SelectionGroupId) &&
                    _exercisesById.TryGetValue(
                        decision.RootExerciseId,
                        out Exercise? keptRoot))
                {
                    KeepSequenceInSlot(
                        state,
                        decision.SelectionGroupId,
                        keptRoot);
                }
            }
        }

        LegacyPhaseDownvoteEvent[] loggedDownvotes = loggedSessions
            .SelectMany(session =>
                session.SelectionChanges.Select(change =>
                        new LegacyPhaseDownvoteEvent(
                            session.SessionId,
                            change.ChangedAtUnixMilliseconds,
                            change.SelectionGroupId,
                            change.RejectedRootExerciseId,
                            ResolveLoggedSelectionChangePhase(session, change)))
                    .Concat(session.Decisions
                        .Where(decision => decision.Outcome == ExerciseOutcome.X)
                        .Select(decision => new LegacyPhaseDownvoteEvent(
                            session.SessionId,
                            decision.DecidedAtUnixMilliseconds,
                            decision.SelectionGroupId,
                            decision.RootExerciseId,
                            ResolveLoggedDecisionPhase(session, decision)))))
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry.SessionId)
            .ToArray();

        foreach ((string selectionGroupId, Dictionary<int, int> adjustments) in
                 state.ExerciseScoreAdjustmentsBySelectionGroupId)
        {
            foreach ((int rootId, int adjustment) in adjustments)
            {
                int downvoteCount = Math.Max(0, -adjustment);
                if (downvoteCount == 0 ||
                    !_exercisesById.TryGetValue(rootId, out Exercise? root))
                {
                    continue;
                }

                LegacyPhaseDownvoteEvent[] matchingEvents = loggedDownvotes
                    .Where(entry =>
                        entry.SelectionGroupId == selectionGroupId &&
                        entry.RootExerciseId == rootId)
                    .TakeLast(downvoteCount)
                    .ToArray();
                foreach (LegacyPhaseDownvoteEvent entry in matchingEvents)
                {
                    DownvoteSequenceInPhase(state, entry.Phase, root);
                }

                WorkoutExercisePhase fallbackPhase =
                    GetLegacySelectionGroupPhase(selectionGroupId);
                for (int index = matchingEvents.Length;
                     index < downvoteCount;
                     index++)
                {
                    DownvoteSequenceInPhase(state, fallbackPhase, root);
                }
            }
        }

        state.ExerciseScoreAdjustmentsBySelectionGroupId.Clear();
    }

    private WorkoutExercisePhase ResolveLoggedSelectionChangePhase(
        WorkoutSessionLog session,
        WorkoutSelectionChangeLog change)
    {
        if (IsPersistableExercisePhase(change.ExercisePhase))
        {
            return change.ExercisePhase;
        }

        int lastCompletedOrder = session.Blocks
            .Where(block => change.ChangedAtUnixMilliseconds <= 0 ||
                block.CompletedAtUnixMilliseconds <=
                    change.ChangedAtUnixMilliseconds)
            .Select(block => block.Order)
            .DefaultIfEmpty(0)
            .Max();
        return lastCompletedOrder > 0
            ? WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                lastCompletedOrder + 1)
            : GetLegacySelectionGroupPhase(
                change.SelectionGroupId,
                session.WorkoutMinutes);
    }

    private WorkoutExercisePhase ResolveLoggedDecisionPhase(
        WorkoutSessionLog session,
        WorkoutDecisionLog decision)
    {
        if (IsPersistableExercisePhase(decision.ExercisePhase))
        {
            return decision.ExercisePhase;
        }

        int decisionOrder = session.Blocks
            .Where(block =>
                block.SelectionGroupId == decision.SelectionGroupId &&
                block.RootExerciseId == decision.RootExerciseId &&
                (decision.DecidedAtUnixMilliseconds <= 0 ||
                 block.CompletedAtUnixMilliseconds <=
                    decision.DecidedAtUnixMilliseconds))
            .Select(block => block.Order)
            .DefaultIfEmpty(0)
            .Max();
        if (decisionOrder <= 0)
        {
            decisionOrder = session.Blocks
                .Where(block => decision.DecidedAtUnixMilliseconds <= 0 ||
                    block.CompletedAtUnixMilliseconds <=
                        decision.DecidedAtUnixMilliseconds)
                .Select(block => block.Order)
                .DefaultIfEmpty(0)
                .Max();
        }

        return decisionOrder > 0
            ? WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(decisionOrder)
            : GetLegacySelectionGroupPhase(
                decision.SelectionGroupId,
                session.WorkoutMinutes);
    }

    private static WorkoutExercisePhase GetLegacySelectionGroupPhase(
        string selectionGroupId,
        int workoutMinutes = 0)
    {
        if (!KnownWorkoutGroups.TryGetValue(
                selectionGroupId,
                out WorkoutGroup? group))
        {
            return WorkoutExercisePhase.Warmup;
        }

        int resolutionGroupCount = GetResolutionGroupsForGroup(group).Count;
        int effectiveMinutes = IsValidWorkoutMinutes(workoutMinutes)
            ? workoutMinutes
            : resolutionGroupCount;
        int projectedFinalBlockOrder = checked(
            (group.Order * effectiveMinutes + resolutionGroupCount - 1) /
            resolutionGroupCount);
        return WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
            projectedFinalBlockOrder);
    }

    private static bool IsPersistableExercisePhase(WorkoutExercisePhase phase) =>
        phase is WorkoutExercisePhase.Warmup or
            WorkoutExercisePhase.PeakPerformance or
            WorkoutExercisePhase.Fatigued;

    private void RemoveSavedSequenceCopiesForSlot(
        WorkoutState state,
        string selectionGroupId,
        int rootId)
    {
        var matchingEntries = state.SelectedExerciseIds
            .Select(entry =>
            {
                bool parsed = TryParseSelectionStorageKey(
                    entry.Key,
                    out string storedSelectionGroupId,
                    out WorkoutModifiers modifiers);
                return new
                {
                    entry.Key,
                    entry.Value,
                    parsed,
                    storedSelectionGroupId,
                    modifiers,
                };
            })
            .Where(entry => entry.parsed && entry.Value == rootId &&
                KnownWorkoutGroups.ContainsKey(entry.storedSelectionGroupId))
            .ToArray()
            .GroupBy(entry => (
                entry.modifiers,
                Resolution: entry.storedSelectionGroupId.Split('.')[0]));
        foreach (var profileEntries in matchingEntries)
        {
            string anchorId = profileEntries
                .Select(entry => entry.storedSelectionGroupId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(groupId => KnownWorkoutGroups[groupId].Order)
                .First();
            if (anchorId != selectionGroupId)
            {
                continue;
            }

            foreach (string storageKey in profileEntries.Select(entry => entry.Key))
            {
                state.SelectedExerciseIds.Remove(storageKey);
            }
        }
    }

    private Exercise? ResolveLegacyPendingRest(WorkoutState state)
    {
        if (state.LegacyPendingRestGroup is null || state.PendingRestKept)
        {
            return null;
        }

        if (!state.LegacySelectedExerciseNames.TryGetValue(
                state.LegacyPendingRestGroup,
                out string? exerciseName))
        {
            return null;
        }

        Exercise? exercise = _exercises.FirstOrDefault(candidate =>
            candidate.Name == exerciseName);
        if (exercise is null)
        {
            return null;
        }

        int minutes = IsValidWorkoutMinutes(state.ActiveWorkoutMinutes)
            ? state.ActiveWorkoutMinutes
            : NormalizeLastWorkoutMinutes(state.LastWorkoutMinutes);
        string selectionGroupId = MassGroupingTaxonomy.GetGroup(
            minutes,
            exercise.PrimaryCanonicalGroup).Id;
        Exercise root = GetSequenceRoot(exercise);
        DownvoteSequenceInPhase(
            state,
            GetLegacySelectionGroupPhase(selectionGroupId, minutes),
            root);
        RemoveSavedSequenceCopiesForSlot(
            state,
            selectionGroupId,
            root.Id);
        return exercise;
    }

    private static void NormalizeCollections(WorkoutState state)
    {
        state.SelectedExerciseIds ??= [];
        state.Outcomes ??= [];
        state.LastKeptExerciseIds ??= [];
        state.KeptExerciseRootIdsBySelectionGroupId ??= [];
        state.ExerciseScoreAdjustmentsBySelectionGroupId ??= [];
        state.ExerciseScoreAdjustmentsByPhase ??= [];
        state.LastHardWorkUnixMillisecondsByPrimaryMuscle ??= [];
        state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle ??= [];
        state.LegacyCompletedTrainingDayUnixMilliseconds ??= [];
        state.WorkoutHistory ??= [];
        state.NextWorkoutExcludedExerciseIds ??= [];
        state.ActiveExtraSetSelectionGroupIds ??= [];
        state.ActiveSetCountsBySelectionGroupId ??= [];
        state.ActiveSelectionGroupOrder ??= [];
        state.ActiveSimpleRoundSelectionGroupIds ??= [];
        state.ActiveDurationSelectionGroupIds = state.ActiveDurationSelectionGroupIds?
            .Where(KnownWorkoutGroups.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
        state.ActiveModifierRetainedSelectionGroupIds ??= [];
        state.ActiveDirectionPartnerExerciseIds ??= [];
        state.ActiveFullSideRoundIds ??= [];
        state.PendingScoreUpdates ??= [];
        state.LegacySelectedExerciseNames ??= [];
        state.LegacyOutcomes ??= [];
    }

    private static void NormalizeWorkoutHistory(WorkoutState state)
    {
        state.WorkoutHistory = state.WorkoutHistory
            .OfType<WorkoutSessionLog>()
            .ToList();
        var usedSessionIds = new HashSet<long>();
        long nextSessionId = Math.Max(1L, state.NextWorkoutSessionId);
        foreach (WorkoutSessionLog session in state.WorkoutHistory)
        {
            NormalizeWorkoutSession(session);
            if (session.SessionId <= 0 || !usedSessionIds.Add(session.SessionId))
            {
                while (!usedSessionIds.Add(nextSessionId))
                {
                    nextSessionId++;
                }
                session.SessionId = nextSessionId++;
            }
            nextSessionId = Math.Max(nextSessionId, session.SessionId + 1L);
            if (session.Status == WorkoutSessionStatus.InProgress)
            {
                session.Status = WorkoutSessionStatus.Interrupted;
            }
        }

        if (state.ActiveWorkoutSession is { } activeSession)
        {
            NormalizeWorkoutSession(activeSession);
            if (activeSession.SessionId <= 0 ||
                !usedSessionIds.Add(activeSession.SessionId))
            {
                while (!usedSessionIds.Add(nextSessionId))
                {
                    nextSessionId++;
                }
                activeSession.SessionId = nextSessionId++;
            }
            nextSessionId = Math.Max(nextSessionId, activeSession.SessionId + 1L);
            activeSession.Status = WorkoutSessionStatus.InProgress;
            activeSession.EndedAtUnixMilliseconds = 0;
            state.ActiveWorkoutIsLightDay = activeSession.IsLightDay;
        }

        state.NextWorkoutSessionId = Math.Max(1L, nextSessionId);
    }

    private static void NormalizeWorkoutSession(WorkoutSessionLog session)
    {
        session.KeptExerciseIdsAtStart =
        [
            .. (session.KeptExerciseIdsAtStart ?? [])
                .Where(exerciseId => exerciseId > 0)
                .Distinct()
                .Order(),
        ];
        session.KeptExerciseRootIdsBySelectionGroupIdAtStart =
            (session.KeptExerciseRootIdsBySelectionGroupIdAtStart ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .ToDictionary(
                entry => entry.Key,
                entry => (entry.Value ?? [])
                    .Where(exerciseId => exerciseId > 0)
                    .Distinct()
                    .Order()
                    .ToArray(),
                StringComparer.Ordinal);
        session.InitialSelections = session.InitialSelections?
            .OfType<WorkoutSelectionSnapshot>()
            .ToList() ?? [];
        foreach (WorkoutSelectionSnapshot selection in session.InitialSelections)
        {
            selection.SelectionGroupId ??= string.Empty;
            selection.RootExerciseName ??= string.Empty;
            selection.CoveredWorkoutGroupIds = selection.CoveredWorkoutGroupIds?
                .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? [];
        }

        session.SelectionChanges = session.SelectionChanges?
            .OfType<WorkoutSelectionChangeLog>()
            .ToList() ?? [];
        foreach (WorkoutSelectionChangeLog change in session.SelectionChanges)
        {
            change.SelectionGroupId ??= string.Empty;
            change.RejectedRootExerciseName ??= string.Empty;
            change.ReplacementRootExerciseName ??= string.Empty;
            if (!IsPersistableExercisePhase(change.ExercisePhase))
            {
                change.ExercisePhase = WorkoutExercisePhase.Unknown;
            }
        }

        session.ModifierChanges = session.ModifierChanges?
            .OfType<WorkoutModifierChangeLog>()
            .ToList() ?? [];
        session.DurationChanges ??= [];
        foreach (WorkoutModifierChangeLog change in session.ModifierChanges)
        {
            change.PreviousModifiers = NormalizeWorkoutModifiers(
                change.PreviousModifiers);
            change.NewModifiers = NormalizeWorkoutModifiers(
                change.NewModifiers);
            change.ProtectedSelectionGroupId ??= string.Empty;
            change.PlannedSelections = change.PlannedSelections?
                .OfType<WorkoutSelectionSnapshot>()
                .ToList() ?? [];
            foreach (WorkoutSelectionSnapshot selection in
                     change.PlannedSelections)
            {
                selection.SelectionGroupId ??= string.Empty;
                selection.RootExerciseName ??= string.Empty;
                selection.CoveredWorkoutGroupIds =
                    selection.CoveredWorkoutGroupIds?
                        .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray() ?? [];
            }
        }

        session.Blocks = session.Blocks?
            .OfType<WorkoutBlockLog>()
            .ToList() ?? [];
        foreach (WorkoutBlockLog block in session.Blocks)
        {
            block.WorkoutGroupId ??= string.Empty;
            block.SelectionGroupId ??= string.Empty;
            block.RootExerciseName ??= string.Empty;
            block.ExerciseName ??= string.Empty;
            block.SecondaryCanonicalGroups ??= [];
        }

        session.Decisions = session.Decisions?
            .OfType<WorkoutDecisionLog>()
            .ToList() ?? [];
        foreach (WorkoutDecisionLog decision in session.Decisions)
        {
            decision.SelectionGroupId ??= string.Empty;
            decision.RootExerciseName ??= string.Empty;
            if (!IsPersistableExercisePhase(decision.ExercisePhase))
            {
                decision.ExercisePhase = WorkoutExercisePhase.Unknown;
            }
            decision.SequenceExerciseIds = decision.SequenceExerciseIds?
                .Where(exerciseId => exerciseId > 0)
                .Distinct()
                .ToArray() ?? [];
        }
    }

    private WorkoutSessionLog EnsureActiveWorkoutSession(
        WorkoutState state,
        bool startedBeforeLogging)
    {
        return state.ActiveWorkoutSession ?? CreateActiveWorkoutSession(
            state,
            GetCurrentUnixTimeMilliseconds(),
            state.LastKeptExerciseIds.Order().ToArray(),
            startedBeforeLogging);
    }

    private WorkoutSessionLog CreateActiveWorkoutSession(
        WorkoutState state,
        long startedAtUnixMilliseconds,
        IReadOnlyCollection<int> keptExerciseIdsAtStart,
        bool startedBeforeLogging)
    {
        if (!IsValidWorkoutMinutes(state.ActiveWorkoutMinutes))
        {
            throw new InvalidOperationException(
                "Cannot log a workout without a valid active duration.");
        }
        if (startedAtUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startedAtUnixMilliseconds));
        }
        if (state.ActiveWorkoutSession is not null)
        {
            return state.ActiveWorkoutSession;
        }

        long sessionId = Math.Max(1L, state.NextWorkoutSessionId);
        if (sessionId == long.MaxValue)
        {
            throw new InvalidOperationException("Workout session IDs are exhausted.");
        }
        state.NextWorkoutSessionId = sessionId + 1L;
        var session = new WorkoutSessionLog
        {
            SessionId = sessionId,
            StartedAtUnixMilliseconds = startedAtUnixMilliseconds,
            WorkoutMinutes = state.ActiveWorkoutMinutes,
            Modifiers = state.ActiveWorkoutModifiers,
            IsLightDay = state.ActiveWorkoutModifiers.HasFlag(
                WorkoutModifiers.Light),
            Status = WorkoutSessionStatus.InProgress,
            StartedBeforeLogging = startedBeforeLogging,
            AutomaticLightRequiredAtStart = startedBeforeLogging ? null :
                IsLightDayDue(state, startedAtUnixMilliseconds),
            KeptExerciseIdsAtStart = keptExerciseIdsAtStart
                .Where(exerciseId => exerciseId > 0)
                .Distinct()
                .Order()
                .ToArray(),
            KeptExerciseRootIdsBySelectionGroupIdAtStart = state
                .KeptExerciseRootIdsBySelectionGroupId
                .Where(entry => entry.Value.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.Order().ToArray(),
                    StringComparer.Ordinal),
        };
        IReadOnlyDictionary<string, int> setCounts = GetEffectiveSetCounts(state);
        SelectedSequencePlacement[] scheduleOrderedPlacements =
            GetScheduleOrderedPlacements(
                state,
                GetSelectedSequencePlacements(state));
        Dictionary<string, int> finalBlockOrderBySelectionGroupId =
            CreateWorkoutSchedule(state, setCounts)
                .GroupBy(group => group.SelectionKey)
                .ToDictionary(
                    groups => groups.Key,
                    groups => groups.Max(group => group.Order),
                    StringComparer.Ordinal);
        session.InitialSelections = scheduleOrderedPlacements
            .Select(placement => new WorkoutSelectionSnapshot
            {
                SelectionGroupId = placement.Anchor.Id,
                CoveredWorkoutGroupIds = placement.CoveredGroups
                    .OrderBy(group => group.Order)
                    .Select(group => group.Id)
                    .ToArray(),
                RootExerciseId = placement.Root.Id,
                RootExerciseName = placement.Root.Name,
                SelectionScoreAtStart = GetSelectionScore(
                    state,
                    placement.Root,
                    WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                        finalBlockOrderBySelectionGroupId[
                            placement.Anchor.Id])),
                SequenceBlockCount = placement.Root.SequenceBlocks.Length,
                SetCount = Math.Max(
                    1,
                    setCounts.GetValueOrDefault(placement.Anchor.Id, 1)),
                WasKeptAtWorkoutStart = IsSequenceKept(
                    state,
                    placement.Anchor.Id,
                    placement.Root),
            })
            .ToList();
        state.ActiveWorkoutSession = session;
        return session;
    }

    private void RecordWorkoutSelectionChange(
        WorkoutState state,
        string selectionGroupId,
        WorkoutExercisePhase phase,
        Exercise rejectedRoot,
        int rejectedSelectionScore,
        Exercise replacementRoot)
    {
        WorkoutSessionLog session = EnsureActiveWorkoutSession(
            state,
            startedBeforeLogging: true);
        session.SelectionChanges.Add(new WorkoutSelectionChangeLog
        {
            Kind = WorkoutSelectionChangeKind.Shuffle,
            ChangedAtUnixMilliseconds = GetCurrentUnixTimeMilliseconds(),
            SelectionGroupId = selectionGroupId,
            ExercisePhase = phase,
            RejectedRootExerciseId = rejectedRoot.Id,
            RejectedRootExerciseName = rejectedRoot.Name,
            RejectedSelectionScoreBeforeChange = rejectedSelectionScore,
            RejectedSelectionWasKeptAtWorkoutStart = WasSequenceKeptAtWorkoutStart(
                session,
                selectionGroupId,
                rejectedRoot),
            ReplacementRootExerciseId = replacementRoot.Id,
            ReplacementRootExerciseName = replacementRoot.Name,
            ReplacementSelectionScore = GetSelectionScore(
                state,
                replacementRoot,
                phase),
        });
    }

    private void RecordCompletedWorkoutBlock(
        WorkoutState state,
        WorkoutGroup group,
        long completedAtUnixMilliseconds)
    {
        WorkoutSessionLog session = EnsureActiveWorkoutSession(
            state,
            startedBeforeLogging: true);
        if (session.Blocks.Any(block =>
                string.Equals(
                    block.WorkoutGroupId,
                    group.Id,
                    StringComparison.Ordinal)))
        {
            return;
        }

        Exercise exercise = GetSelectedExercise(state, group);
        Exercise root = GetSequenceRoot(exercise);
        session.Blocks.Add(new WorkoutBlockLog
        {
            CompletedAtUnixMilliseconds = completedAtUnixMilliseconds,
            WorkoutGroupId = group.Id,
            SelectionGroupId = group.SelectionKey,
            Order = group.Order,
            RootExerciseId = root.Id,
            RootExerciseName = root.Name,
            ExerciseId = exercise.Id,
            ExerciseName = exercise.Name,
            SequenceBlockNumber = group.SequenceBlockIndex + 1,
            SequenceBlockCount = group.SequenceBlockCount,
            SetNumber = group.SetNumber,
            SetCount = group.SetCount,
            SideCue = group.SequenceSideCue,
            DirectionCue = group.SequenceDirectionCue,
            MirrorMedia = group.MirrorSequenceMedia,
            MediaSegment = group.SequenceMediaSegment,
            MuscularDemand = exercise.MuscularDemand,
            PrimaryCanonicalGroup = exercise.PrimaryCanonicalGroup,
            SecondaryCanonicalGroups = [.. exercise.SecondaryCanonicalGroups],
            WasSequenceKeptAtWorkoutStart = WasSequenceKeptAtWorkoutStart(
                session,
                group.SelectionKey,
                root),
        });
    }

    private void RecordWorkoutDecision(
        WorkoutState state,
        WorkoutGroup group,
        Exercise root,
        ExerciseOutcome outcome,
        int selectionScoreBeforeDecision,
        WorkoutExercisePhase exercisePhase,
        long decidedAtUnixMilliseconds)
    {
        WorkoutSessionLog session = EnsureActiveWorkoutSession(
            state,
            startedBeforeLogging: true);
        WorkoutDecisionLog? existing = session.Decisions.SingleOrDefault(decision =>
            string.Equals(
                decision.SelectionGroupId,
                group.SelectionKey,
                StringComparison.Ordinal));
        if (existing is not null)
        {
            if (existing.RootExerciseId != root.Id || existing.Outcome != outcome)
            {
                throw new InvalidOperationException(
                    $"Workout selection {group.SelectionKey} was decided twice.");
            }
            return;
        }

        session.Decisions.Add(new WorkoutDecisionLog
        {
            DecidedAtUnixMilliseconds = decidedAtUnixMilliseconds,
            SelectionGroupId = group.SelectionKey,
            ExercisePhase = exercisePhase,
            RootExerciseId = root.Id,
            RootExerciseName = root.Name,
            SequenceExerciseIds = GetSequenceExercises(root)
                .Select(exercise => exercise.Id)
                .Order()
                .ToArray(),
            Outcome = outcome,
            SelectionScoreBeforeDecision = selectionScoreBeforeDecision,
            CompletedBlockCount = session.Blocks.Count(block => string.Equals(
                block.SelectionGroupId,
                group.SelectionKey,
                StringComparison.Ordinal)),
            PlannedBlockCount = group.SequenceBlockCount * group.SetCount,
            WasKeptAtWorkoutStart = WasSequenceKeptAtWorkoutStart(
                session,
                group.SelectionKey,
                root),
        });
    }

    private bool WasSequenceKeptAtWorkoutStart(
        WorkoutSessionLog session,
        string selectionGroupId,
        Exercise root)
    {
        if (session.KeptExerciseRootIdsBySelectionGroupIdAtStart
                .GetValueOrDefault(selectionGroupId)?
                .Contains(root.Id) == true)
        {
            return true;
        }

        return session.InitialSelections.Any(selection =>
            selection.SelectionGroupId == selectionGroupId &&
            selection.RootExerciseId == root.Id &&
            selection.WasKeptAtWorkoutStart);
    }

    private List<WorkoutSelectionSnapshot> CreateCurrentSelectionSnapshots(
        WorkoutState state,
        WorkoutSessionLog session)
    {
        IReadOnlyDictionary<string, int> setCounts = GetEffectiveSetCounts(state);
        Dictionary<string, int> finalBlockOrderBySelectionGroupId =
            CreateWorkoutSchedule(state, setCounts)
                .GroupBy(group => group.SelectionKey)
                .ToDictionary(
                    groups => groups.Key,
                    groups => groups.Max(group => group.Order),
                    StringComparer.Ordinal);
        return GetScheduleOrderedPlacements(
                state,
                GetSelectedSequencePlacements(state))
            .Select(placement => new WorkoutSelectionSnapshot
            {
                SelectionGroupId = placement.Anchor.Id,
                CoveredWorkoutGroupIds = placement.CoveredGroups
                    .OrderBy(group => group.Order)
                    .Select(group => group.Id)
                    .ToArray(),
                RootExerciseId = placement.Root.Id,
                RootExerciseName = placement.Root.Name,
                SelectionScoreAtStart = GetSelectionScore(
                    state,
                    placement.Root,
                    WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                        finalBlockOrderBySelectionGroupId[
                            placement.Anchor.Id])),
                SequenceBlockCount = placement.Root.SequenceBlocks.Length,
                SetCount = Math.Max(
                    1,
                    setCounts.GetValueOrDefault(placement.Anchor.Id, 1)),
                WasKeptAtWorkoutStart = WasSequenceKeptAtWorkoutStart(
                    session,
                    placement.Anchor.Id,
                    placement.Root),
            })
            .ToList();
    }

    private void FinalizeActiveWorkoutSession(
        WorkoutState state,
        WorkoutSessionStatus status,
        long? endedAtUnixMilliseconds = null)
    {
        if (state.ActiveWorkoutSession is not { } session)
        {
            return;
        }
        if (status == WorkoutSessionStatus.InProgress)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        long endedAt = endedAtUnixMilliseconds ?? GetCurrentUnixTimeMilliseconds();
        session.EndedAtUnixMilliseconds = Math.Max(
            session.StartedAtUnixMilliseconds,
            endedAt);
        session.Status = status;
        int existingIndex = state.WorkoutHistory.FindIndex(candidate =>
            candidate.SessionId == session.SessionId);
        if (existingIndex >= 0)
        {
            state.WorkoutHistory[existingIndex] = session;
        }
        else
        {
            state.WorkoutHistory.Add(session);
        }
        state.ActiveWorkoutSession = null;
    }

    private void NormalizeActiveLongWorkoutAllocation(WorkoutState state)
    {
        if (!IsLongWorkoutAllocationValid(state))
        {
            SetActiveLongWorkoutAllocation(state);
        }
    }

    private bool IsLongWorkoutAllocationValid(WorkoutState state)
    {
        if (state.ActiveDirectionPartnerExerciseIds.Count != 0 ||
            state.ActiveFullSideRoundIds.Count != 0)
        {
            return false;
        }

        SelectedSequencePlacement[] placements =
            GetSelectedSequencePlacements(state);
        if (state.ActiveSetCountsBySelectionGroupId.Count !=
                placements.Length ||
            placements.Any(placement =>
                state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(
                    placement.Anchor.Id) < 1))
        {
            return false;
        }

        HashSet<string> expectedExtraSetGroups =
            state.ActiveSetCountsBySelectionGroupId
                .Where(entry => entry.Value > 1)
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);
        if (!state.ActiveExtraSetSelectionGroupIds.SetEquals(
                expectedExtraSetGroups))
        {
            return false;
        }

        try
        {
            IReadOnlyList<WorkoutGroup> rounds = CreateWorkoutSchedule(
                state,
                state.ActiveSetCountsBySelectionGroupId);
            // The allocation is an active-workout snapshot. Recomputing its
            // preference ranking after a Keep or downvote can move already
            // scheduled blocks and orphan their outcomes. Structural validity
            // is enough; new phase feedback applies when the next allocation
            // is created.
            return rounds.Count == state.ActiveWorkoutMinutes;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void NormalizeSlotPreferences(WorkoutState state)
    {
        foreach (string selectionGroupId in
                 state.KeptExerciseRootIdsBySelectionGroupId.Keys.ToArray())
        {
            if (!KnownWorkoutGroups.ContainsKey(selectionGroupId))
            {
                state.KeptExerciseRootIdsBySelectionGroupId.Remove(
                    selectionGroupId);
                continue;
            }

            HashSet<int> normalizedRoots = state
                .KeptExerciseRootIdsBySelectionGroupId[selectionGroupId]
                .Where(rootId => IsValidPreferenceRoot(
                    selectionGroupId,
                    rootId))
                .ToHashSet();
            if (normalizedRoots.Count == 0)
            {
                state.KeptExerciseRootIdsBySelectionGroupId.Remove(
                    selectionGroupId);
            }
            else
            {
                state.KeptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
                    normalizedRoots;
            }
        }

        foreach (string selectionGroupId in
                 state.ExerciseScoreAdjustmentsBySelectionGroupId.Keys.ToArray())
        {
            if (!KnownWorkoutGroups.ContainsKey(selectionGroupId))
            {
                state.ExerciseScoreAdjustmentsBySelectionGroupId.Remove(
                    selectionGroupId);
                continue;
            }

            Dictionary<int, int> normalizedAdjustments = state
                .ExerciseScoreAdjustmentsBySelectionGroupId[selectionGroupId]
                .Where(entry => entry.Value != 0 && IsValidPreferenceRoot(
                    selectionGroupId,
                    entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            if (normalizedAdjustments.Count == 0)
            {
                state.ExerciseScoreAdjustmentsBySelectionGroupId.Remove(
                    selectionGroupId);
            }
            else
            {
                state.ExerciseScoreAdjustmentsBySelectionGroupId[selectionGroupId] =
                    normalizedAdjustments;
            }
        }

        foreach (WorkoutExercisePhase phase in
                 state.ExerciseScoreAdjustmentsByPhase.Keys.ToArray())
        {
            if (!IsPersistableExercisePhase(phase))
            {
                state.ExerciseScoreAdjustmentsByPhase.Remove(phase);
                continue;
            }

            Dictionary<int, int> normalizedAdjustments = state
                .ExerciseScoreAdjustmentsByPhase[phase]
                .Where(entry => entry.Value < 0 && IsValidPreferenceRoot(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            if (normalizedAdjustments.Count == 0)
            {
                state.ExerciseScoreAdjustmentsByPhase.Remove(phase);
            }
            else
            {
                state.ExerciseScoreAdjustmentsByPhase[phase] =
                    normalizedAdjustments;
            }
        }

        SyncLegacyKeptExerciseIds(state);
        NormalizeWorkHistory(state);
        state.NextWorkoutExcludedExerciseIds.RemoveWhere(exerciseId =>
            !_exercisesById.ContainsKey(exerciseId));
        ExpandSequenceIds(state.NextWorkoutExcludedExerciseIds);
    }

    private bool IsValidPreferenceRoot(
        string selectionGroupId,
        int rootId)
    {
        if (!_exercisesById.TryGetValue(rootId, out Exercise? exercise) ||
            GetSequenceRoot(exercise).Id != rootId ||
            !KnownWorkoutGroups.TryGetValue(
                selectionGroupId,
                out WorkoutGroup? preferenceSlot))
        {
            return false;
        }

        IReadOnlyList<WorkoutGroup> resolutionGroups =
            GetResolutionGroupsForGroup(preferenceSlot);
        return GetSequencePlacementOptions(exercise, resolutionGroups)
            .Any(option => option
                .OrderBy(group => group.Order)
                .First()
                .Id == selectionGroupId);
    }

    private bool IsValidPreferenceRoot(int rootId) =>
        _exercisesById.TryGetValue(rootId, out Exercise? exercise) &&
        GetSequenceRoot(exercise).Id == rootId;

    private void RemovePartialSequenceIds(HashSet<int> exerciseIds)
    {
        HashSet<int> completeSequenceExerciseIds = exerciseIds
            .Select(exerciseId => _exercisesById[exerciseId])
            .Select(GetSequenceRoot)
            .DistinctBy(root => root.Id)
            .Where(root => GetSequenceExercises(root)
                .All(member => exerciseIds.Contains(member.Id)))
            .SelectMany(GetSequenceExercises)
            .Select(member => member.Id)
            .ToHashSet();
        exerciseIds.IntersectWith(completeSequenceExerciseIds);
    }

    private HashSet<int> GetKeptRootIdsForSlot(
        WorkoutState state,
        string selectionGroupId)
    {
        if (!state.KeptExerciseRootIdsBySelectionGroupId.TryGetValue(
                selectionGroupId,
                out HashSet<int>? keptRootIds))
        {
            keptRootIds = [];
            state.KeptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
                keptRootIds;
        }
        return keptRootIds;
    }

    private bool IsSequenceKept(
        WorkoutState state,
        string selectionGroupId,
        Exercise exercise) =>
        state.KeptExerciseRootIdsBySelectionGroupId
            .GetValueOrDefault(selectionGroupId)?
            .Contains(GetSequenceRoot(exercise).Id) == true;

    private void KeepSequenceInSlot(
        WorkoutState state,
        string selectionGroupId,
        Exercise exercise) =>
        GetKeptRootIdsForSlot(state, selectionGroupId)
            .Add(GetSequenceRoot(exercise).Id);

    private void RemoveSequenceKeep(
        WorkoutState state,
        string selectionGroupId,
        Exercise exercise)
    {
        if (!state.KeptExerciseRootIdsBySelectionGroupId.TryGetValue(
                selectionGroupId,
                out HashSet<int>? keptRootIds))
        {
            return;
        }

        keptRootIds.Remove(GetSequenceRoot(exercise).Id);
        if (keptRootIds.Count == 0)
        {
            state.KeptExerciseRootIdsBySelectionGroupId.Remove(selectionGroupId);
        }
    }

    private void SyncLegacyKeptExerciseIds(WorkoutState state)
    {
        state.LastKeptExerciseIds = state.KeptExerciseRootIdsBySelectionGroupId
            .Values
            .SelectMany(rootIds => rootIds)
            .Where(_exercisesById.ContainsKey)
            .SelectMany(rootId => GetSequenceExercises(_exercisesById[rootId]))
            .Select(exercise => exercise.Id)
            .ToHashSet();
    }

    private static void NormalizeWorkHistory(WorkoutState state)
    {
        state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
            NormalizeWorkHistory(
                state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);
        state.LastHardWorkUnixMillisecondsByPrimaryMuscle =
            NormalizeWorkHistory(
                state.LastHardWorkUnixMillisecondsByPrimaryMuscle);
    }

    private static Dictionary<string, long> NormalizeWorkHistory(
        IReadOnlyDictionary<string, long> history)
    {
        var normalized = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach ((string muscleKey, long completedAtUnixMilliseconds) in
                 history)
        {
            if (completedAtUnixMilliseconds <= 0 ||
                !Enum.TryParse(
                    muscleKey,
                    ignoreCase: true,
                    out CanonicalMuscleGroup primaryMuscle) ||
                !Enum.IsDefined(primaryMuscle))
            {
                continue;
            }

            string canonicalKey = primaryMuscle.ToString();
            normalized[canonicalKey] = Math.Max(
                completedAtUnixMilliseconds,
                normalized.GetValueOrDefault(canonicalKey));
        }

        return normalized;
    }

    private void ExpandSequenceIds(HashSet<int> exerciseIds)
    {
        foreach (int exerciseId in exerciseIds.ToArray())
        {
            if (_exercisesById.TryGetValue(exerciseId, out Exercise? exercise))
            {
                exerciseIds.UnionWith(GetSequenceExercises(exercise)
                    .Select(member => member.Id));
            }
        }
    }

    private void MigrateImplicitSilenceModifier(WorkoutState state)
    {
        foreach ((string selectionStorageKey, int exerciseId) in
                 state.SelectedExerciseIds.ToArray())
        {
            if (!TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out string selectionGroupId,
                    out WorkoutModifiers modifiers))
            {
                continue;
            }

            WorkoutModifiers quietProfile = NormalizeWorkoutModifiers(
                modifiers | WorkoutModifiers.Silence);
            state.SelectedExerciseIds.TryAdd(
                GetSelectionStorageKey(selectionGroupId, quietProfile),
                exerciseId);
        }

        state.LastWorkoutModifiers = NormalizeWorkoutModifiers(
            state.LastWorkoutModifiers | WorkoutModifiers.Silence);
        if (state.ActiveWorkoutMinutes > 0)
        {
            state.ActiveWorkoutModifiers = NormalizeWorkoutModifiers(
                state.ActiveWorkoutModifiers | WorkoutModifiers.Silence);
        }
    }

    private void MigrateExplicitMirrorEquipment(WorkoutState state)
    {
        foreach (string selectionStorageKey in
                 state.SelectedExerciseIds.Keys.ToArray())
        {
            if (TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out _,
                    out WorkoutModifiers modifiers) &&
                modifiers.HasFlag(WorkoutModifiers.Mirror))
            {
                state.SelectedExerciseIds.Remove(selectionStorageKey);
            }
        }

        // The former binary state did not record mirror height. Treating it as
        // compact or tall would silently claim equipment the user never chose.
        state.LastWorkoutModifiers = WorkoutModifierPolicy.WithMirrorEquipment(
            state.LastWorkoutModifiers,
            MirrorEquipment.None);
        state.ActiveWorkoutModifiers = WorkoutModifierPolicy.WithMirrorEquipment(
            state.ActiveWorkoutModifiers,
            MirrorEquipment.None);
    }

    private void MigrateImplicitHardFloorModifier(WorkoutState state)
    {
        foreach ((string selectionStorageKey, int exerciseId) in
                 state.SelectedExerciseIds.ToArray())
        {
            if (!TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out string selectionGroupId,
                    out WorkoutModifiers modifiers))
            {
                continue;
            }

            WorkoutModifiers hardFloorProfile = NormalizeWorkoutModifiers(
                modifiers | WorkoutModifiers.HardFloor);
            state.SelectedExerciseIds.TryAdd(
                GetSelectionStorageKey(selectionGroupId, hardFloorProfile),
                exerciseId);
        }

        state.LastWorkoutModifiers = NormalizeWorkoutModifiers(
            state.LastWorkoutModifiers | WorkoutModifiers.HardFloor);

        // Preserve the exact profile of a workout already in progress. The new
        // default takes effect when the user next reaches duration selection.
    }

    private void MigrateImplicitUpperBodyClothingModifier(WorkoutState state)
    {
        foreach ((string selectionStorageKey, int exerciseId) in
                 state.SelectedExerciseIds.ToArray())
        {
            if (!TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out string selectionGroupId,
                    out WorkoutModifiers modifiers))
            {
                continue;
            }

            WorkoutModifiers clothingProfile = NormalizeWorkoutModifiers(
                modifiers | WorkoutModifiers.UpperBodyClothing);
            state.SelectedExerciseIds.TryAdd(
                GetSelectionStorageKey(selectionGroupId, clothingProfile),
                exerciseId);
        }

        state.LastWorkoutModifiers = NormalizeWorkoutModifiers(
            state.LastWorkoutModifiers | WorkoutModifiers.UpperBodyClothing);

        // Preserve the exact profile and checkpoints of a workout already in
        // progress. The new default applies at the next duration selection.
    }

    private void MigrateExplicitLightMode(WorkoutState state)
    {
        state.LastWorkoutModifiers = WorkoutModifierPolicy
            .GetPersistentSetupModifiers(state.LastWorkoutModifiers);

        foreach (WorkoutSessionLog session in state.WorkoutHistory)
        {
            AddLightModifierToLegacyLightSession(session);
        }

        if (state.ActiveWorkoutSession is { } activeSession &&
            activeSession.IsLightDay)
        {
            AddLightModifierToLegacyLightSession(activeSession);
        }

        if (state.ActiveWorkoutMinutes > 0 && state.ActiveWorkoutIsLightDay)
        {
            EnableLightModeForExistingActiveWorkout(state);
        }
    }

    private void EnableLightModeForExistingActiveWorkout(
        WorkoutState state)
    {
        WorkoutModifiers previousProfile = NormalizeWorkoutModifiers(
            state.ActiveWorkoutModifiers & ~WorkoutModifiers.Light);
        WorkoutModifiers lightProfile = NormalizeWorkoutModifiers(
            previousProfile | WorkoutModifiers.Light);

        foreach ((string selectionStorageKey, int exerciseId) in
                 state.SelectedExerciseIds.ToArray())
        {
            if (!TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out string selectionGroupId,
                    out WorkoutModifiers storedProfile) ||
                storedProfile != previousProfile)
            {
                continue;
            }

            state.SelectedExerciseIds.TryAdd(
                GetSelectionStorageKey(selectionGroupId, lightProfile),
                exerciseId);
        }

        state.ActiveWorkoutModifiers = lightProfile;
        state.ActiveWorkoutIsLightDay = true;
        if (state.ActiveWorkoutSession is { } activeSession)
        {
            activeSession.IsLightDay = true;
            AddLightModifierToLegacyLightSession(activeSession);
        }
    }

    private void MigrateActiveLightLineup(WorkoutState state)
    {
        SelectedSequencePlacement[] priorPlacements =
            GetSelectedSequencePlacements(state);
        SelectedSequencePlacement[] priorOrderedPlacements =
            GetScheduleOrderedPlacements(state, priorPlacements);
        WorkoutGroup[] priorRounds = GetActiveGroups(state).ToArray();
        WorkoutGroup? currentRound = GetNextGroup(state);
        if (currentRound is null)
        {
            RepairActiveLineup(state);
            NormalizeActiveLongWorkoutAllocation(state);
            return;
        }

        WorkoutSessionLog session = EnsureActiveWorkoutSession(
            state,
            startedBeforeLogging: true);
        bool preserveCompletedCurrentSelection =
            GetPendingRestGroup(state)?.Id == currentRound.Id;
        HashSet<string> lockedSelectionGroupIds = session.Decisions
            .Select(decision => decision.SelectionGroupId)
            .Where(selectionGroupId => !string.IsNullOrWhiteSpace(
                selectionGroupId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (WorkoutGroup decidedRound in priorRounds.Where(round =>
                     state.Outcomes.TryGetValue(
                         round.Id,
                         out ExerciseOutcome outcome) &&
                     outcome is ExerciseOutcome.Tick or ExerciseOutcome.X))
        {
            lockedSelectionGroupIds.Add(decidedRound.SelectionKey);
        }
        if (preserveCompletedCurrentSelection)
        {
            lockedSelectionGroupIds.Add(currentRound.SelectionKey);
        }
        else
        {
            lockedSelectionGroupIds.Remove(currentRound.SelectionKey);
        }

        SelectedSequencePlacement[] lockedPlacements = priorPlacements
            .Where(placement => lockedSelectionGroupIds.Contains(
                placement.Anchor.Id))
            .ToArray();
        Dictionary<string, int> lockedExerciseIdsByGroup = lockedPlacements
            .SelectMany(placement => placement.CoveredGroups.Select(group =>
                (GroupId: group.Id, RootId: placement.Root.Id)))
            .ToDictionary(
                entry => entry.GroupId,
                entry => entry.RootId,
                StringComparer.Ordinal);
        Dictionary<string, int> lockedSetCountsBySelectionGroupId =
            lockedPlacements.ToDictionary(
                placement => placement.Anchor.Id,
                placement => state.ActiveSetCountsBySelectionGroupId
                    .GetValueOrDefault(placement.Anchor.Id, 1),
                StringComparer.Ordinal);
        HashSet<string> protectedBaseGroupIds =
            lockedExerciseIdsByGroup.Keys.ToHashSet(StringComparer.Ordinal);
        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();

        IReadOnlyDictionary<string, int> replannedLineup =
            ChooseBestDistinctLineup(
                state,
                selectionGroups,
                state.ActiveWorkoutModifiers,
                currentExerciseIds: lockedExerciseIdsByGroup,
                allowSavedSelectionException: true,
                modifierTransitionProtectedGroupIds: protectedBaseGroupIds);

        if (!preserveCompletedCurrentSelection)
        {
            foreach (WorkoutGroup currentSelectionRound in priorRounds.Where(
                         round => round.SelectionKey ==
                             currentRound.SelectionKey))
            {
                state.Outcomes.Remove(currentSelectionRound.Id);
            }
            ClearPendingMovement(state);
            ClearPendingRest(state);
            state.ActiveModifierProtectedSelectionGroupId = null;
        }

        ApplyDistinctLineup(
            state,
            selectionGroups,
            replannedLineup,
            clearChangedProgress: false);
        UpdateSelectionOrderAfterReconfiguration(
            state,
            priorOrderedPlacements);
        RebalanceNewExercisesByMuscleBalance(
            state,
            lockedSelectionGroupIds);
        UpdateSelectionOrderAfterReconfiguration(
            state,
            priorOrderedPlacements);
        ApplyLongWorkoutAllocation(
            state,
            ChooseLongWorkoutAllocation(
                state,
                lockedSelectionGroupIds));

        WorkoutGroup[] replannedRounds = GetActiveGroups(state).ToArray();
        bool changedLockedSelection = lockedExerciseIdsByGroup.Any(entry =>
            state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    entry.Key,
                    state.ActiveWorkoutModifiers)) != entry.Value);
        bool changedLockedSetCount =
            lockedSetCountsBySelectionGroupId.Any(entry =>
                state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(
                    entry.Key,
                    1) != entry.Value);
        WorkoutGroup? replannedNextRound = GetNextGroup(state);
        if (changedLockedSelection ||
            changedLockedSetCount ||
            state.Outcomes.Keys.Any(outcomeGroupId =>
                replannedRounds.All(round => round.Id != outcomeGroupId)) ||
            (preserveCompletedCurrentSelection &&
                replannedNextRound?.Id != currentRound.Id) ||
            (!preserveCompletedCurrentSelection &&
                replannedNextRound?.SelectionKey !=
                    currentRound.SelectionKey))
        {
            throw new InvalidOperationException(
                "The Light-mode upgrade could not preserve completed work.");
        }

        if (!preserveCompletedCurrentSelection)
        {
            if (replannedNextRound is null)
            {
                throw new InvalidOperationException(
                    "The Light-mode upgrade did not retain the current workout slot.");
            }
            long restartDurationMilliseconds =
                MovementPhaseSchedule.GetCountdownDurationSeconds(
                    includePreparation: !IsSequenceContinuationBlock(
                        state,
                        replannedNextRound)) * 1_000L;
            PauseMovement(
                state,
                replannedNextRound,
                restartDurationMilliseconds,
                pausedByUser: true);
        }

        session.ModifierChanges.Add(new WorkoutModifierChangeLog
        {
            ChangedAtUnixMilliseconds = GetCurrentUnixTimeMilliseconds(),
            PreviousModifiers = state.ActiveWorkoutModifiers,
            NewModifiers = state.ActiveWorkoutModifiers,
            ProtectedSelectionGroupId = preserveCompletedCurrentSelection
                ? currentRound.SelectionKey
                : string.Empty,
            PlannedSelections = CreateCurrentSelectionSnapshots(
                state,
                session),
        });
    }

    private static void AddLightModifierToLegacyLightSession(
        WorkoutSessionLog session)
    {
        if (!session.IsLightDay)
        {
            return;
        }

        session.Modifiers = NormalizeWorkoutModifiers(
            session.Modifiers | WorkoutModifiers.Light);
        foreach (WorkoutModifierChangeLog change in session.ModifierChanges)
        {
            change.PreviousModifiers = NormalizeWorkoutModifiers(
                change.PreviousModifiers | WorkoutModifiers.Light);
            change.NewModifiers = NormalizeWorkoutModifiers(
                change.NewModifiers | WorkoutModifiers.Light);
        }
    }

    private void NormalizeSavedLineups(WorkoutState state)
    {
        foreach (string selectionStorageKey in
                 state.SelectedExerciseIds.Keys.ToArray())
        {
            if (!TryParseSelectionStorageKey(
                    selectionStorageKey,
                    out string groupId,
                    out WorkoutModifiers modifiers) ||
                !KnownWorkoutGroups.TryGetValue(groupId, out WorkoutGroup? group) ||
                !_exercisesById.TryGetValue(
                    state.SelectedExerciseIds[selectionStorageKey],
                    out Exercise? exercise) ||
                !IsStoredLineupSelectionValid(state, exercise, group, modifiers))
            {
                state.SelectedExerciseIds.Remove(selectionStorageKey);
            }
        }
    }

    private void NormalizeOutcomes(WorkoutState state)
    {
        Dictionary<string, WorkoutGroup> activeGroups = GetActiveGroups(state)
            .ToDictionary(group => group.Id, StringComparer.Ordinal);

        foreach (string groupId in state.Outcomes.Keys
                     .Where(groupId => !activeGroups.ContainsKey(groupId))
                     .ToArray())
        {
            state.Outcomes.Remove(groupId);
        }

        foreach (string groupId in state.Outcomes
                     .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            WorkoutGroup group = activeGroups[groupId];
            if (IsIntermediateSequenceBlock(state, group))
            {
                continue;
            }

            state.Outcomes[groupId] = ExerciseOutcome.Tick;
        }
    }

    private void NormalizePendingRest(WorkoutState state)
    {
        if (state.PendingRestGroupId is string pendingGroupId &&
            HasValidPendingRestTiming(state) &&
            KnownWorkoutGroups.TryGetValue(
                pendingGroupId,
                out WorkoutGroup? pendingBaseGroup) &&
            state.SelectedExerciseIds.TryGetValue(
                GetSelectionStorageKey(
                    pendingBaseGroup.Id,
                    state.ActiveWorkoutModifiers),
                out int pendingRootId) &&
            _exercisesById.TryGetValue(pendingRootId, out Exercise? pendingRoot) &&
            IsSavedSelectionValid(
                state,
                pendingRoot,
                pendingBaseGroup,
                state.ActiveWorkoutModifiers))
        {
            return;
        }

        try
        {
            if (GetValidPendingRestGroup(state) is not null)
            {
                return;
            }
        }
        catch (InvalidOperationException)
        {
            // An obsolete schedule may be impossible to construct until its
            // lineup is repaired. Such a checkpoint cannot identify a valid
            // current block in the new schedule.
        }

        ClearPendingRest(state);
    }

    private static LegacyActiveProgressSnapshot CaptureLegacyActiveProgress(
        WorkoutState state) =>
        new(
            new Dictionary<string, ExerciseOutcome>(
                state.Outcomes,
                StringComparer.Ordinal),
            new Dictionary<string, int>(
                state.SelectedExerciseIds,
                StringComparer.Ordinal),
            new Dictionary<string, int>(
                state.ActiveDirectionPartnerExerciseIds,
                StringComparer.Ordinal),
            new HashSet<string>(
                state.ActiveFullSideRoundIds,
                StringComparer.Ordinal),
            state.PendingMovementGroupId,
            state.PendingMovementMillisecondsRemaining,
            state.PendingMovementEndsAtUnixMilliseconds,
            state.PendingMovementPausedByUser,
            state.PendingRestGroupId,
            state.PendingRestEndsAtUnixMilliseconds,
            state.PendingRestKept);

    private void MigrateLegacyActiveProgress(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot)
    {
        WorkoutGroup[] rounds = GetActiveGroups(state)
            .OrderBy(round => round.Order)
            .ToArray();
        state.Outcomes.Clear();
        ClearPendingMovement(state);
        ClearPendingRest(state);

        foreach (IGrouping<string, WorkoutGroup> sequence in rounds.GroupBy(
                     round => round.SelectionKey,
                     StringComparer.Ordinal))
        {
            if (!LegacySelectionMatchesCurrentSequence(
                    state,
                    snapshot,
                    sequence.Key))
            {
                continue;
            }

            KeyValuePair<string, ExerciseOutcome>[] oldOutcomes = snapshot.Outcomes
                .Where(entry => ResolveLegacySelectionKey(entry.Key) == sequence.Key)
                .ToArray();
            WorkoutGroup[] sequenceRounds = sequence
                .OrderBy(round => round.Order)
                .ToArray();
            ExerciseOutcome? decision = oldOutcomes
                .Where(entry => entry.Value is ExerciseOutcome.Tick or ExerciseOutcome.X)
                .Select(entry => (ExerciseOutcome?)entry.Value)
                .LastOrDefault();
            if (decision is not null)
            {
                foreach (WorkoutGroup round in sequenceRounds[..^1])
                {
                    state.Outcomes[round.Id] = ExerciseOutcome.Neutral;
                }
                state.Outcomes[sequenceRounds[^1].Id] = decision.Value;
                continue;
            }

            foreach (KeyValuePair<string, ExerciseOutcome> completed in oldOutcomes
                         .Where(entry => entry.Value == ExerciseOutcome.Neutral)
                         .OrderBy(entry => GetLegacyRoundOrdinal(entry.Key)))
            {
                foreach (WorkoutGroup round in ResolveLegacyRepresentedRounds(
                             state,
                             snapshot,
                             sequenceRounds,
                             completed.Key))
                {
                    if (!round.IsFinalSequenceRound)
                    {
                        state.Outcomes[round.Id] = ExerciseOutcome.Neutral;
                    }
                }
            }
        }

        string? legacyPendingRoundId =
            snapshot.PendingRestGroupId ?? snapshot.PendingMovementGroupId;
        string? pendingSelectionKey = ResolveLegacySelectionKey(legacyPendingRoundId);
        if (legacyPendingRoundId is null || pendingSelectionKey is null)
        {
            return;
        }
        if (!LegacySelectionMatchesCurrentSequence(
                state,
                snapshot,
                pendingSelectionKey))
        {
            return;
        }

        WorkoutGroup[] pendingSequenceRounds = rounds
            .Where(round => round.SelectionKey == pendingSelectionKey)
            .OrderBy(round => round.Order)
            .ToArray();
        WorkoutGroup[] representedRounds = ResolveLegacyRepresentedRounds(
            state,
            snapshot,
            pendingSequenceRounds,
            legacyPendingRoundId);
        if (representedRounds.Length == 0)
        {
            return;
        }

        if (snapshot.PendingRestGroupId is not null &&
            snapshot.PendingRestEndsAtUnixMilliseconds > 0)
        {
            WorkoutGroup pendingRestRound = representedRounds[^1];
            MarkSequenceRoundsBeforePending(
                state,
                pendingSequenceRounds,
                pendingRestRound);
            state.PendingRestGroupId = pendingRestRound.Id;
            state.PendingRestEndsAtUnixMilliseconds =
                snapshot.PendingRestEndsAtUnixMilliseconds;
            state.PendingRestKept = snapshot.PendingRestKept &&
                pendingRestRound.IsFinalSequenceRound;
            return;
        }

        if (snapshot.PendingMovementGroupId is null ||
            snapshot.PendingMovementMillisecondsRemaining <= 0)
        {
            return;
        }

        (int representedRoundIndex, long remainingMilliseconds) =
            MapLegacyMovementProgress(
                state,
                snapshot,
                legacyPendingRoundId,
                representedRounds.Length);
        WorkoutGroup pendingRound = representedRounds[Math.Clamp(
            representedRoundIndex,
            0,
            representedRounds.Length - 1)];
        MarkSequenceRoundsBeforePending(
            state,
            pendingSequenceRounds,
            pendingRound);
        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(
                state,
                pendingRound)) * 1_000L;
        state.PendingMovementGroupId = pendingRound.Id;
        state.PendingMovementMillisecondsRemaining = Math.Clamp(
            remainingMilliseconds,
            1L,
            maximum);
        state.PendingMovementEndsAtUnixMilliseconds =
            representedRounds.Length == 1 &&
                remainingMilliseconds ==
                    snapshot.PendingMovementMillisecondsRemaining
                ? snapshot.PendingMovementEndsAtUnixMilliseconds
                : 0;
        state.PendingMovementPausedByUser =
            snapshot.PendingMovementPausedByUser;
    }

    private bool LegacySelectionMatchesCurrentSequence(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot,
        string selectionKey)
    {
        int legacyExerciseId = ResolveLegacyMemberExerciseId(
            state,
            snapshot,
            selectionKey,
            selectionKey);
        int currentExerciseId = state.SelectedExerciseIds.GetValueOrDefault(
            GetSelectionStorageKey(selectionKey, state.ActiveWorkoutModifiers));
        return legacyExerciseId > 0 &&
            currentExerciseId > 0 &&
            _exercisesById.TryGetValue(legacyExerciseId, out Exercise? legacyExercise) &&
            _exercisesById.TryGetValue(currentExerciseId, out Exercise? currentExercise) &&
            GetSequenceRoot(legacyExercise).Id == GetSequenceRoot(currentExercise).Id;
    }

    private WorkoutGroup[] ResolveLegacyRepresentedRounds(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot,
        IReadOnlyList<WorkoutGroup> sequenceRounds,
        string legacyRoundId)
    {
        string? selectionKey = ResolveLegacySelectionKey(legacyRoundId);
        if (selectionKey is null)
        {
            return [];
        }

        int setNumber = GetLegacySetNumber(legacyRoundId);
        WorkoutGroup[] setRounds = sequenceRounds
            .Where(round => round.SetNumber == setNumber)
            .OrderBy(round => round.SequenceBlockIndex)
            .ToArray();
        if (setRounds.Length == 0)
        {
            return [];
        }

        int memberExerciseId = ResolveLegacyMemberExerciseId(
            state,
            snapshot,
            selectionKey,
            legacyRoundId);
        WorkoutGroup[] represented = memberExerciseId > 0
            ? setRounds
                .Where(round => round.ExerciseOverrideId == memberExerciseId ||
                    round.ExerciseOverrideId == 0 &&
                    GetSelectedExercise(state, round).Id == memberExerciseId)
                .ToArray()
            : [];
        return represented.Length > 0 ? represented : setRounds;
    }

    private int ResolveLegacyMemberExerciseId(
        WorkoutState state,
        LegacyActiveProgressSnapshot snapshot,
        string selectionKey,
        string legacyRoundId)
    {
        if (legacyRoundId.StartsWith(
                selectionKey + ".direction",
                StringComparison.Ordinal) &&
            snapshot.DirectionPartnerExerciseIds.TryGetValue(
                selectionKey,
                out int partnerExerciseId))
        {
            return partnerExerciseId;
        }

        string activeStorageKey = GetSelectionStorageKey(
            selectionKey,
            state.ActiveWorkoutModifiers);
        if (snapshot.SelectedExerciseIds.TryGetValue(
                activeStorageKey,
                out int selectedExerciseId))
        {
            return selectedExerciseId;
        }

        return snapshot.SelectedExerciseIds
            .Where(entry =>
            {
                if (!TryParseSelectionStorageKey(
                        entry.Key,
                        out string storedGroupId,
                        out WorkoutModifiers modifiers))
                {
                    return false;
                }
                return storedGroupId == selectionKey &&
                    WorkoutModifierPolicy.Normalize(modifiers) ==
                        state.ActiveWorkoutModifiers;
            })
            .Select(entry => entry.Value)
            .FirstOrDefault();
    }

    private (int RepresentedRoundIndex, long RemainingMilliseconds)
        MapLegacyMovementProgress(
            WorkoutState state,
            LegacyActiveProgressSnapshot snapshot,
            string legacyRoundId,
            int representedRoundCount)
    {
        long remaining = snapshot.PendingMovementMillisecondsRemaining;
        if (representedRoundCount < 2)
        {
            return (0, remaining);
        }

        string? selectionKey = ResolveLegacySelectionKey(legacyRoundId);
        int memberExerciseId = selectionKey is null
            ? 0
            : ResolveLegacyMemberExerciseId(
                state,
                snapshot,
                selectionKey,
                legacyRoundId);
        bool usedTimedPair = memberExerciseId > 0 &&
            _exercisesById.TryGetValue(memberExerciseId, out Exercise? member) &&
            (member.SideSequence.UsesTimedSides() ||
                member.DirectionSequence != ExerciseDirectionSequence.None);
        if (!usedTimedPair)
        {
            return (0, remaining);
        }

        if (snapshot.FullSideRoundIds.Contains(legacyRoundId))
        {
            return remaining switch
            {
                > 60_000L => (0, remaining - 60_000L),
                > 45_000L => (1, 45_000L),
                _ => (1, remaining),
            };
        }

        return remaining switch
        {
            > 25_000L => (0, remaining),
            > 20_000L => (1, 45_000L),
            _ => (1, remaining + 25_000L),
        };
    }

    private static void MarkSequenceRoundsBeforePending(
        WorkoutState state,
        IReadOnlyList<WorkoutGroup> sequenceRounds,
        WorkoutGroup pendingRound)
    {
        foreach (WorkoutGroup round in sequenceRounds.TakeWhile(round =>
                     round.Id != pendingRound.Id))
        {
            state.Outcomes.TryAdd(round.Id, ExerciseOutcome.Neutral);
        }
    }

    private static int GetLegacySetNumber(string roundId)
    {
        foreach (string marker in new[] { ".set", ".direction" })
        {
            int index = roundId.LastIndexOf(marker, StringComparison.Ordinal);
            if (index >= 0 &&
                int.TryParse(roundId.AsSpan(index + marker.Length), out int value) &&
                value > 0)
            {
                return value;
            }
        }

        return 1;
    }

    private static int GetLegacyRoundOrdinal(string roundId)
    {
        int setNumber = GetLegacySetNumber(roundId);
        int directionOffset = roundId.Contains(
            ".direction",
            StringComparison.Ordinal)
            ? 1
            : 0;
        return (setNumber - 1) * 2 + directionOffset;
    }

    private static string? ResolveLegacySelectionKey(string? roundId)
    {
        if (string.IsNullOrWhiteSpace(roundId))
        {
            return null;
        }

        return KnownWorkoutGroups.Keys
            .Where(groupId =>
                string.Equals(roundId, groupId, StringComparison.Ordinal) ||
                roundId.StartsWith(groupId + ".", StringComparison.Ordinal))
            .OrderByDescending(groupId => groupId.Length)
            .FirstOrDefault();
    }

    private void NormalizePendingMovement(WorkoutState state)
    {
        if (GetValidPendingMovementGroup(state) is null)
        {
            ClearPendingMovement(state);
            return;
        }

        // Movement and rest are mutually exclusive persisted phases. A valid
        // rest means movement already completed, so rest takes precedence.
        if (GetValidPendingRestGroup(state) is not null)
        {
            ClearPendingMovement(state);
        }
    }

    private WorkoutGroup? GetValidPendingMovementGroup(WorkoutState state)
    {
        if (state.PendingMovementGroupId is not string pendingGroupId ||
            state.PendingMovementMillisecondsRemaining <= 0 ||
            state.Outcomes.ContainsKey(pendingGroupId))
        {
            return null;
        }

        WorkoutGroup? pendingGroup = GetActiveGroups(state)
            .SingleOrDefault(group => group.Id == pendingGroupId);
        if (pendingGroup is null || GetNextGroup(state)?.Id != pendingGroup.Id)
        {
            return null;
        }

        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(state, pendingGroup)) *
            1_000L;
        if (state.PendingMovementMillisecondsRemaining > maximum ||
            state.PendingMovementEndsAtUnixMilliseconds < 0)
        {
            return null;
        }

        try
        {
            _ = GetSelectedExercise(state, pendingGroup);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return pendingGroup;
    }

    private void ValidatePendingMovement(
        WorkoutState state,
        WorkoutGroup group,
        long millisecondsRemaining,
        long endsAtUnixMilliseconds,
        bool allowPausedDeadline)
    {
        if (GetNextGroup(state)?.Id != group.Id)
        {
            throw new InvalidOperationException(
                $"{group.DisplayName} is not the next workout group.");
        }

        long maximum = MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !IsSequenceContinuationBlock(state, group)) * 1_000L;
        if (millisecondsRemaining <= 0 || millisecondsRemaining > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(millisecondsRemaining));
        }
        if ((!allowPausedDeadline && endsAtUnixMilliseconds <= 0) ||
            (allowPausedDeadline && endsAtUnixMilliseconds != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(endsAtUnixMilliseconds));
        }

        _ = GetSelectedExercise(state, group);
    }

    private WorkoutGroup? GetValidPendingRestGroup(WorkoutState state)
    {
        if (state.PendingRestGroupId is not string pendingGroupId ||
            !HasValidPendingRestTiming(state) ||
            state.Outcomes.ContainsKey(pendingGroupId))
        {
            return null;
        }

        WorkoutGroup? pendingGroup = GetActiveGroups(state)
            .SingleOrDefault(group => group.Id == pendingGroupId);
        if (pendingGroup is null)
        {
            return null;
        }

        try
        {
            _ = GetSelectedExercise(state, pendingGroup);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return pendingGroup;
    }

    private static bool HasValidPendingRestTiming(WorkoutState state) =>
        state.PendingRestPausedByUser
            ? state.PendingRestEndsAtUnixMilliseconds == 0 &&
                state.PendingRestMillisecondsRemaining > 0 &&
                state.PendingRestMillisecondsRemaining <= RestDurationMilliseconds
            : state.PendingRestEndsAtUnixMilliseconds > 0 &&
                state.PendingRestMillisecondsRemaining == 0;

    private void NormalizeCompletionState(WorkoutState state)
    {
        WorkoutGroup[] activeGroups = GetActiveGroups(state).ToArray();
        state.WorkoutCompleted = activeGroups.Length > 0 &&
            activeGroups.All(group => state.Outcomes.ContainsKey(group.Id));

        if (!state.WorkoutCompleted)
        {
            state.CompletionAcknowledged = false;
        }
    }

    private static void ClearLegacyMigrationState(WorkoutState state)
    {
        state.LegacySelectedExerciseNames.Clear();
        state.LegacyOutcomes.Clear();
        state.LegacyPendingRestGroup = null;
    }

    private static void ResetToDurationSelection(WorkoutState state)
    {
        state.ActiveDurationSelectionGroupIds = null;
        state.ActiveSimpleRoundSelectionGroupIds.Clear();
        state.ActiveWorkoutSession = null;
        state.ActiveWorkoutMinutes = 0;
        state.ActiveWorkoutModifiers = WorkoutModifiers.None;
        state.ActiveWorkoutIsLightDay = false;
        state.Outcomes.Clear();
        state.ActiveExtraSetSelectionGroupIds.Clear();
        state.ActiveSetCountsBySelectionGroupId.Clear();
        state.ActiveSelectionGroupOrder.Clear();
        state.ActiveModifierRetainedSelectionGroupIds.Clear();
        state.ActiveModifierProtectedSelectionGroupId = null;
        state.ActiveDirectionPartnerExerciseIds.Clear();
        state.ActiveFullSideRoundIds.Clear();
        state.WorkoutCompleted = false;
        state.CompletionAcknowledged = false;
        state.PendingMovementGroupId = null;
        state.PendingMovementMillisecondsRemaining = 0;
        state.PendingMovementEndsAtUnixMilliseconds = 0;
        state.PendingMovementPausedByUser = false;
        state.PendingRestGroupId = null;
        state.PendingRestEndsAtUnixMilliseconds = 0;
        state.PendingRestMillisecondsRemaining = 0;
        state.PendingRestPausedByUser = false;
        state.PendingRestKept = false;
    }

    public static int NormalizeLastWorkoutMinutes(int minutes)
    {
        return WorkoutMinutes
            .OrderBy(candidate => Math.Abs(candidate - minutes))
            .ThenByDescending(candidate => candidate)
            .First();
    }

    public static bool IsValidWorkoutMinutes(int minutes)
    {
        return WorkoutMinutes.Contains(minutes);
    }

    public static WorkoutModifiers NormalizeWorkoutModifiers(
        WorkoutModifiers modifiers)
    {
        return WorkoutModifierPolicy.Normalize(modifiers);
    }

    private bool IsLightDayDue(
        WorkoutState state,
        long nowUnixMilliseconds)
    {
        if (state.ActiveWorkoutSession is { Status: WorkoutSessionStatus.InProgress,
                AutomaticLightRequiredAtStart: bool required }) return required;
        bool cadenceDue = WorkoutLightDayPolicy.IsLightDayDue(
            state.WorkoutHistory,
            nowUnixMilliseconds,
            _localTimeZone,
            state.LegacyCompletedTrainingDayUnixMilliseconds);
        return OuraRecoveryPolicy.RequiresLight(cadenceDue,
            OuraRecoveryPolicy.Evaluate(state, nowUnixMilliseconds));
    }

    private void MigrateLegacyCompletedTrainingDays(
        WorkoutState state,
        long nowUnixMilliseconds)
    {
        foreach (long timestamp in WorkoutLightDayPolicy
                     .InferLegacyCompletedTrainingDays(
                         state.WorkoutHistory,
                         state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                         state.LegacyCompletedTrainingDayUnixMilliseconds,
                         nowUnixMilliseconds,
                         _localTimeZone))
        {
            state.LegacyCompletedTrainingDayUnixMilliseconds.Add(timestamp);
        }
    }

    private string GetSelectionStorageKey(
        string selectionGroupId,
        WorkoutModifiers modifiers)
    {
        WorkoutModifiers normalized = NormalizeWorkoutModifiers(modifiers);
        return normalized == WorkoutModifiers.None
            ? selectionGroupId
            : $"{SelectionProfilePrefix}{(int)normalized}" +
                $"{SelectionProfileSeparator}{selectionGroupId}";
    }

    private static bool TryParseSelectionStorageKey(
        string selectionStorageKey,
        out string selectionGroupId,
        out WorkoutModifiers modifiers)
    {
        int separatorIndex = selectionStorageKey.IndexOf(
            SelectionProfileSeparator);
        if (selectionStorageKey.StartsWith(
                SelectionProfilePrefix,
                StringComparison.Ordinal) &&
            separatorIndex > SelectionProfilePrefix.Length &&
            int.TryParse(
                selectionStorageKey.AsSpan(
                    SelectionProfilePrefix.Length,
                    separatorIndex - SelectionProfilePrefix.Length),
                out int modifierValue))
        {
            modifiers = NormalizeWorkoutModifiers(
                (WorkoutModifiers)modifierValue);
            selectionGroupId = selectionStorageKey[(separatorIndex + 1)..];
            return modifierValue > 0 &&
                (int)modifiers == modifierValue &&
                selectionGroupId.Length > 0;
        }

        selectionGroupId = selectionStorageKey;
        modifiers = WorkoutModifiers.None;
        return selectionGroupId.Length > 0;
    }

    private static IReadOnlyList<WorkoutGroup> GetSelectionGroups(
        WorkoutState state)
    {
        return GetSelectionGroups(
            state,
            state.ActiveWorkoutModifiers,
            state.ActiveModifierRetainedSelectionGroupIds);
    }

    private static IReadOnlyList<WorkoutGroup> GetSelectionGroups(
        WorkoutState state,
        WorkoutModifiers modifiers,
        IReadOnlySet<string>? retainedSelectionGroupIds) =>
        state.ActiveDurationSelectionGroupIds is { } groupIds
            ? groupIds.Select(id => KnownWorkoutGroups[id])
                .Where(group => WorkoutModifierPolicy.IsSelectionGroupAvailable(group, modifiers) ||
                    retainedSelectionGroupIds?.Contains(group.Id) == true).ToArray()
            : GetSelectionGroups(state.ActiveWorkoutMinutes, modifiers, retainedSelectionGroupIds);

    private static IReadOnlyList<WorkoutGroup> GetSelectionGroups(
        int workoutMinutes,
        WorkoutModifiers modifiers,
        IReadOnlySet<string>? retainedSelectionGroupIds = null)
    {
        retainedSelectionGroupIds ??=
            new HashSet<string>(StringComparer.Ordinal);
        return IsValidWorkoutMinutes(workoutMinutes)
            ? GetBaseResolution(workoutMinutes).Groups
                .Where(group =>
                    WorkoutModifierPolicy.IsSelectionGroupAvailable(
                        group,
                        modifiers) ||
                    retainedSelectionGroupIds.Contains(group.Id))
                .ToArray()
            : [];
    }

    private static WorkoutResolution GetBaseResolution(int workoutMinutes)
    {
        int resolutionMinutes = workoutMinutes > 30 ? 30 : workoutMinutes;
        return MassGroupingTaxonomy.GetResolution(resolutionMinutes);
    }

    private static int GetExtraMinuteCount(int workoutMinutes)
    {
        if (workoutMinutes <= 30)
        {
            return 0;
        }

        return workoutMinutes - GetBaseResolution(workoutMinutes).Groups.Count;
    }

    private SelectedSequencePlacement[] GetSelectedSequencePlacements(
        WorkoutState state)
    {
        IReadOnlyList<WorkoutGroup> selectionGroups = GetSelectionGroups(state);
        var selectedGroupsByRootId = new Dictionary<int, List<WorkoutGroup>>();
        foreach (WorkoutGroup group in selectionGroups)
        {
            int rootId = state.SelectedExerciseIds.GetValueOrDefault(
                GetSelectionStorageKey(
                    group.Id,
                    state.ActiveWorkoutModifiers));
            if (!_exercisesById.TryGetValue(rootId, out Exercise? root) ||
                root.SequenceBlocks.Length == 0 ||
                GetSequenceRoot(root).Id != root.Id)
            {
                throw new InvalidOperationException(
                    $"{group.DisplayName} has no selected sequence.");
            }
            if (!selectedGroupsByRootId.TryGetValue(
                    root.Id,
                    out List<WorkoutGroup>? selectedGroups))
            {
                selectedGroups = [];
                selectedGroupsByRootId[root.Id] = selectedGroups;
            }
            selectedGroups.Add(group);
        }

        var placements = new List<SelectedSequencePlacement>();
        foreach ((int rootId, List<WorkoutGroup> selectedGroups) in
                 selectedGroupsByRootId)
        {
            Exercise root = _exercisesById[rootId];
            var remainingGroupIds = selectedGroups.Select(group => group.Id)
                .ToHashSet(StringComparer.Ordinal);
            // A cross-primary sequence claims its complete primary placement.
            // The same root in another eligible slot is a separate complete
            // placement, with that slot's own round IDs, feedback and Keep.
            foreach (WorkoutGroup[] option in GetSequencePlacementOptions(root, selectionGroups))
            {
                if (!option.All(group => remainingGroupIds.Contains(group.Id)))
                    continue;
                placements.Add(new SelectedSequencePlacement(
                    root, option.OrderBy(group => group.Order).First(), option));
                remainingGroupIds.ExceptWith(option.Select(group => group.Id));
            }
            foreach (WorkoutGroup group in selectedGroups.Where(group =>
                         remainingGroupIds.Contains(group.Id)))
            {
                if (!(PendingRestMatchesSelectionGroup(state, group.Id) ||
                      state.Outcomes.ContainsKey(group.Id)) ||
                    !GetSequenceExercises(root).All(member =>
                        IsCompatibleWithModifiers(member, state.ActiveWorkoutModifiers) &&
                        IsAssignedToGroup(member, group)))
                {
                    throw new InvalidOperationException(
                        "The selected atomic sequence placements do not match " +
                        "their primary-muscle workout slots.");
                }
                placements.Add(new SelectedSequencePlacement(root, group, [group]));
            }
        }

        return placements
            .OrderBy(placement => placement.Anchor.Order)
            .ToArray();
    }

    private SelectedSequencePlacement[] GetScheduleOrderedPlacements(
        WorkoutState state,
        IEnumerable<SelectedSequencePlacement> placements)
    {
        SelectedSequencePlacement[] placementArray = placements.ToArray();
        string[] frozenSelectionGroupIds = state.ActiveSelectionGroupOrder
            .Where(selectionGroupId =>
                !string.IsNullOrWhiteSpace(selectionGroupId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (frozenSelectionGroupIds.Length == 0)
        {
            frozenSelectionGroupIds = state.ActiveWorkoutSession?
            .InitialSelections
            .Select(selection => selection.SelectionGroupId)
            .ToArray() ?? [];
        }
        if (frozenSelectionGroupIds.Length == placementArray.Length &&
            frozenSelectionGroupIds.Distinct(StringComparer.Ordinal).Count() ==
                placementArray.Length)
        {
            Dictionary<string, SelectedSequencePlacement> placementsByAnchor =
                placementArray.ToDictionary(
                    placement => placement.Anchor.Id,
                    StringComparer.Ordinal);
            if (frozenSelectionGroupIds.All(placementsByAnchor.ContainsKey))
            {
                return frozenSelectionGroupIds
                    .Select(selectionGroupId =>
                        placementsByAnchor[selectionGroupId])
                    .ToArray();
            }
        }

        return placementArray
            .OrderBy(placement =>
                WorkoutSchedulePolicy.GetMuscularDemandPriority(
                    WorkoutSchedulePolicy.GetSequenceMuscularDemand(
                        placement.Root,
                        _exercisesById)))
            .ThenBy(placement => placement.Anchor.Order)
            .ToArray();
    }

    private void UpdateSelectionOrderAfterReconfiguration(
        WorkoutState state,
        IReadOnlyList<SelectedSequencePlacement> priorOrderedPlacements)
    {
        Dictionary<string, int> priorRankByWorkoutGroupId =
            priorOrderedPlacements
                .SelectMany((placement, rank) =>
                    placement.CoveredGroups.Select(group =>
                        (group.Id, Rank: rank)))
                .ToDictionary(
                    entry => entry.Id,
                    entry => entry.Rank,
                    StringComparer.Ordinal);
        state.ActiveSelectionGroupOrder = GetSelectedSequencePlacements(state)
            .OrderBy(placement => placement.CoveredGroups.Min(group =>
                priorRankByWorkoutGroupId.GetValueOrDefault(
                    group.Id,
                    int.MaxValue)))
            .ThenBy(placement => placement.Anchor.Order)
            .Select(placement => placement.Anchor.Id)
            .ToList();
    }

    private LongWorkoutAllocation ChooseLongWorkoutAllocation(
        WorkoutState state,
        IReadOnlySet<string>? lockedSelectionGroupIds = null,
        IReadOnlyList<SelectedSequencePlacement>? selectedPlacements = null)
    {
        lockedSelectionGroupIds ??= new HashSet<string>(StringComparer.Ordinal);
        SelectedSequencePlacement[] rankedPlacements =
            (selectedPlacements ?? GetSelectedSequencePlacements(state))
            .OrderByDescending(placement =>
                GetSequenceExercises(placement.Root)
                    .Any(WorkoutRecoveryPolicy.IsHardExercise))
            .ThenByDescending(placement => IsSequenceKept(
                state,
                placement.Anchor.Id,
                placement.Root))
            .ThenByDescending(placement => placement.Anchor.Order)
            .ToArray();
        Dictionary<string, int> blockCostByGroup = rankedPlacements.ToDictionary(
            placement => placement.Anchor.Id,
            placement => placement.Root.SequenceBlocks.Length,
            StringComparer.Ordinal);
        Dictionary<string, int> setCounts = rankedPlacements.ToDictionary(
            placement => placement.Anchor.Id,
            _ => 1,
            StringComparer.Ordinal);
        int remainingMinutes = state.ActiveWorkoutMinutes -
            blockCostByGroup.Values.Sum();

        foreach (SelectedSequencePlacement placement in rankedPlacements.Where(
                     placement => lockedSelectionGroupIds.Contains(
                         placement.Anchor.Id)))
        {
            int lockedSetCount = state.ActiveSetCountsBySelectionGroupId
                .GetValueOrDefault(placement.Anchor.Id, 1);
            if (lockedSetCount < 1)
            {
                throw new InvalidOperationException(
                    $"The completed set allocation for " +
                    $"{placement.Anchor.DisplayName} is invalid.");
            }
            setCounts[placement.Anchor.Id] = lockedSetCount;
            remainingMinutes -=
                (lockedSetCount - 1) * blockCostByGroup[placement.Anchor.Id];
        }
        if (remainingMinutes < 0)
        {
            throw new InvalidOperationException(
                "The selected mandatory sequences exceed the workout duration.");
        }

        SelectedSequencePlacement[] repeatablePlacements = rankedPlacements
            .Where(placement => !lockedSelectionGroupIds.Contains(
                placement.Anchor.Id))
            .ToArray();
        int[] repeatableCosts = repeatablePlacements
            .Select(placement => blockCostByGroup[placement.Anchor.Id])
            .Distinct()
            .ToArray();
        SelectedSequencePlacement[] scheduleOrderedPlacements =
            GetScheduleOrderedPlacements(state, rankedPlacements);
        // Fillability depends only on the available sequence lengths. Build
        // this unbounded-knapsack table once rather than once per candidate
        // during every additional-set choice.
        var fillable = new bool[remainingMinutes + 1];
        fillable[0] = true;
        for (int value = 1; value <= remainingMinutes; value++)
        {
            fillable[value] = repeatableCosts.Any(cost =>
                cost <= value && fillable[value - cost]);
        }
        bool hasPhaseScoreAdjustments = state.ExerciseScoreAdjustmentsByPhase
            .Values
            .Any(adjustments => adjustments.Count > 0);

        while (remainingMinutes > 0)
        {
            // Compute every candidate's next-set phase in one prefix scan.
            // The former sort comparator rescanned the complete schedule for
            // both sides of every comparison.
            Dictionary<string, WorkoutExercisePhase>?
                phaseAfterAddingSetByGroupId = null;
            if (hasPhaseScoreAdjustments)
            {
                phaseAfterAddingSetByGroupId =
                    new Dictionary<string, WorkoutExercisePhase>(
                        StringComparer.Ordinal);
                int finalBlockOrder = 0;
                foreach (SelectedSequencePlacement placement in
                         scheduleOrderedPlacements)
                {
                    string groupId = placement.Anchor.Id;
                    int cost = blockCostByGroup[groupId];
                    finalBlockOrder += cost * setCounts[groupId];
                    phaseAfterAddingSetByGroupId[groupId] =
                        WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                            finalBlockOrder + cost);
                }
            }

            SelectedSequencePlacement? selectedPlacement = null;
            (int Score, int SetCount, bool Kept, bool OneBlock)? selectedRank =
                null;
            foreach (SelectedSequencePlacement placement in repeatablePlacements)
            {
                string groupId = placement.Anchor.Id;
                int cost = blockCostByGroup[groupId];
                if (cost > remainingMinutes ||
                    !fillable[remainingMinutes - cost])
                {
                    continue;
                }

                var rank = (
                    Score: hasPhaseScoreAdjustments
                        ? GetPhaseScoreAdjustment(
                            state,
                            placement.Root,
                            phaseAfterAddingSetByGroupId![groupId])
                        : 0,
                    SetCount: setCounts[groupId],
                    Kept: IsSequenceKept(state, groupId, placement.Root),
                    OneBlock: cost == 1);
                if (selectedRank is null ||
                    rank.Score > selectedRank.Value.Score ||
                    rank.Score == selectedRank.Value.Score &&
                    (rank.SetCount < selectedRank.Value.SetCount ||
                     rank.SetCount == selectedRank.Value.SetCount &&
                     (rank.Kept && !selectedRank.Value.Kept ||
                      rank.Kept == selectedRank.Value.Kept &&
                      rank.OneBlock && !selectedRank.Value.OneBlock)))
                {
                    selectedPlacement = placement;
                    selectedRank = rank;
                }
            }
            if (selectedPlacement is null)
            {
                throw new InvalidOperationException(
                    "The selected sequence lengths cannot fill the workout duration.");
            }

            setCounts[selectedPlacement.Anchor.Id]++;
            remainingMinutes -= blockCostByGroup[selectedPlacement.Anchor.Id];
        }

        HashSet<string> extraSetGroups = setCounts
            .Where(entry => entry.Value > 1)
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        return new LongWorkoutAllocation(extraSetGroups, setCounts);
    }

    private void RebalanceNewExercisesByMuscleBalance(
        WorkoutState state,
        IReadOnlySet<string>? lockedSelectionGroupIds = null)
    {
        lockedSelectionGroupIds ??=
            new HashSet<string>(StringComparer.Ordinal);
        WorkoutGroup[] groups = GetSelectionGroups(state).ToArray();
        if (groups.Length == 0)
        {
            return;
        }
        long selectionTimeUnixMilliseconds = GetCurrentUnixTimeMilliseconds();
        var allocationCache = new Dictionary<string, LongWorkoutAllocation?>(
            StringComparer.Ordinal);

        HashSet<int> savedKeptRootIds = state
            .KeptExerciseRootIdsBySelectionGroupId
            .Values
            .SelectMany(rootIds => rootIds)
            .ToHashSet();
        Exercise[] rebalanceRoots = _exercises
            .Where(exercise =>
                exercise.SequenceBlocks.Length > 0 &&
                GetSequenceRoot(exercise).Id == exercise.Id &&
                !savedKeptRootIds.Contains(exercise.Id) &&
                !state.NextWorkoutExcludedExerciseIds.Contains(exercise.Id) &&
                GetSequenceExercises(exercise).All(member =>
                    IsCompatibleWithModifiers(
                        member,
                        state.ActiveWorkoutModifiers)))
            .ToArray();
        Dictionary<int, WorkoutGroup[][]> placementOptionsByRootId =
            rebalanceRoots.ToDictionary(
                exercise => exercise.Id,
                exercise => GetSequencePlacementOptions(exercise, groups));
        var sequenceLoadByRootId =
            new Dictionary<int, IReadOnlyDictionary<CanonicalMuscleGroup, int>>();
        IReadOnlyDictionary<CanonicalMuscleGroup, int> GetSequenceLoad(
            Exercise root)
        {
            if (!sequenceLoadByRootId.TryGetValue(
                    root.Id,
                    out IReadOnlyDictionary<CanonicalMuscleGroup, int>? load))
            {
                load = WorkoutMuscleBalancePolicy
                    .CalculateCanonicalLoadEighthUnits(
                        GetSequenceExercises(root));
                sequenceLoadByRootId[root.Id] = load;
            }

            return load;
        }
        var selectionScoreByRootAndGroup =
            new Dictionary<(int RootId, string GroupId), int>();
        int GetCachedSelectionScore(Exercise root, string groupId)
        {
            var key = (root.Id, groupId);
            if (!selectionScoreByRootAndGroup.TryGetValue(key, out int score))
            {
                WorkoutGroup scoreGroup = KnownWorkoutGroups[groupId];
                score = GetSelectionScore(
                    state,
                    root,
                    GetProjectedSelectionPhase(state, scoreGroup, groups.Length));
                selectionScoreByRootAndGroup[key] = score;
            }

            return score;
        }
        var rebalanceCandidates = rebalanceRoots
            .Select(candidate => new
            {
                Candidate = candidate,
                MovementId = WorkoutModifierPolicy.GetSessionMovementId(candidate),
                Options = placementOptionsByRootId[candidate.Id]
                    .Where(option => option.All(group =>
                        WorkoutCoveragePolicy.IsSelectable(
                            GetSequenceSelectionExerciseForGroup(
                                candidate,
                                group),
                            group) &&
                        IsCompatibleWithModifiers(
                            GetSequenceSelectionExerciseForGroup(
                                candidate,
                                group),
                            state.ActiveWorkoutModifiers)))
                    .Select(option => new
                    {
                        Groups = option,
                        Anchor = option.OrderBy(group => group.Order).First(),
                    })
                    .Select(option => new
                    {
                        option.Groups,
                        option.Anchor,
                        AllocationBehaviorKey =
                            GetLongWorkoutAllocationPlacementKey(
                                state,
                                candidate,
                                option.Anchor),
                    })
                    .ToArray(),
            })
            .ToArray();
        var seenLineups = new HashSet<string>(StringComparer.Ordinal);
        for (int pass = 0;
             pass < WorkoutMuscleBalancePolicy.MaximumRebalancePasses;
             pass++)
        {
            string signature = string.Join(
                ',',
                groups.Select(group => state.SelectedExerciseIds.GetValueOrDefault(
                    GetSelectionStorageKey(
                        group.Id,
                        state.ActiveWorkoutModifiers))));
            if (!seenLineups.Add(signature))
            {
                break;
            }

            SelectedSequencePlacement[] currentPlacements;
            LongWorkoutAllocation currentAllocation;
            try
            {
                currentPlacements = GetSelectedSequencePlacements(state);
                currentAllocation = GetCachedLongWorkoutAllocation(
                    state,
                    currentPlacements,
                    allocationCache,
                    lockedSelectionGroupIds);
            }
            catch (InvalidOperationException)
            {
                break;
            }

            IReadOnlyDictionary<CanonicalMuscleGroup, int> currentCanonicalLoad =
                CalculateScheduledCanonicalLoadEighthUnits(
                    currentPlacements,
                    currentAllocation);
            MuscleBalanceEvaluation currentBalance =
                WorkoutMuscleBalancePolicy.Evaluate(currentCanonicalLoad);
            if (currentBalance.IsBalanced)
            {
                break;
            }

            HashSet<int> currentRootIds = currentPlacements
                .Select(placement => placement.Root.Id)
                .ToHashSet();
            HashSet<int> currentMovementIds = currentPlacements
                .Select(placement =>
                    WorkoutModifierPolicy.GetSessionMovementId(placement.Root))
                .ToHashSet();
            Dictionary<string, SelectedSequencePlacement> placementByGroupId =
                currentPlacements
                    .SelectMany(placement => placement.CoveredGroups.Select(group =>
                        (group.Id, Placement: placement)))
                    .ToDictionary(
                        entry => entry.Id,
                        entry => entry.Placement,
                        StringComparer.Ordinal);
            MuscleBalanceCandidate? bestAlternative = null;

            bool currentAllocationHasOneSetPerPlacement = currentAllocation
                .SetCountsBySelectionGroupId
                .Values
                .All(setCount => setCount == 1);
            var replacementAllocationByKey =
                new Dictionary<string, LongWorkoutAllocation?>(
                    StringComparer.Ordinal);
            foreach (var candidateMetadata in rebalanceCandidates)
            {
                Exercise candidate = candidateMetadata.Candidate;
                int candidateMovementId = candidateMetadata.MovementId;
                if (currentRootIds.Contains(candidate.Id) ||
                    currentMovementIds.Contains(candidateMovementId))
                {
                    continue;
                }

                foreach (var optionMetadata in candidateMetadata.Options)
                {
                    WorkoutGroup[] option = optionMetadata.Groups;
                    WorkoutGroup anchor = optionMetadata.Anchor;
                    SelectedSequencePlacement[] removedPlacements = option
                        .Select(group => placementByGroupId[group.Id])
                        .Distinct()
                        .ToArray();
                    if (removedPlacements.Sum(placement =>
                            placement.CoveredGroups.Count) != option.Length ||
                        removedPlacements.Any(placement =>
                            lockedSelectionGroupIds.Contains(
                                placement.Anchor.Id)) ||
                        removedPlacements.Any(placement => IsSequenceKept(
                            state,
                            placement.Anchor.Id,
                            placement.Root)) ||
                        removedPlacements.Any(placement =>
                            WorkoutModifierPolicy.GetSessionMovementId(
                                placement.Root) == candidateMovementId))
                    {
                        continue;
                    }

                    if (state.ActiveWorkoutModifiers.HasFlag(
                            WorkoutModifiers.Light) &&
                        removedPlacements.Any(placement =>
                            IsDemandZeroSequence(placement.Root)) &&
                        !IsDemandZeroSequence(candidate))
                    {
                        // Muscle balancing is subordinate to Light mode. It may
                        // improve one demand-zero lineup with another, but it
                        // cannot replace demand-zero work with harder work.
                        continue;
                    }

                    bool preservesScores = option.All(group =>
                    {
                        int displacedRootId = state.SelectedExerciseIds
                            .GetValueOrDefault(GetSelectionStorageKey(
                                group.Id,
                                state.ActiveWorkoutModifiers));
                        return _exercisesById.TryGetValue(
                                displacedRootId,
                                out Exercise? displacedRoot) &&
                            GetCachedSelectionScore(candidate, group.Id) >=
                            GetCachedSelectionScore(displacedRoot, group.Id);
                    });
                    if (!preservesScores)
                    {
                        continue;
                    }

                    MuscleBalanceEvaluation candidateBalance;
                    int removedBlockCount = removedPlacements.Sum(placement =>
                        placement.Root.SequenceBlocks.Length);
                    if (currentAllocationHasOneSetPerPlacement &&
                        candidate.SequenceBlocks.Length > removedBlockCount)
                    {
                        continue;
                    }
                    bool reusesCurrentAllocation =
                        currentAllocationHasOneSetPerPlacement &&
                            candidate.SequenceBlocks.Length == removedBlockCount ||
                        removedPlacements.Length == 1 &&
                            candidate.SequenceBlocks.Length ==
                                removedPlacements[0].Root.SequenceBlocks.Length &&
                            GetSequenceExercises(candidate).Any(
                                WorkoutRecoveryPolicy.IsHardExercise) ==
                            GetSequenceExercises(removedPlacements[0].Root).Any(
                                WorkoutRecoveryPolicy.IsHardExercise);
                    if (reusesCurrentAllocation)
                    {
                        var candidateCanonicalLoad =
                            new Dictionary<CanonicalMuscleGroup, int>(
                                currentCanonicalLoad);
                        foreach (SelectedSequencePlacement removed in
                                 removedPlacements)
                        {
                            foreach ((CanonicalMuscleGroup muscle, int load) in
                                     GetSequenceLoad(removed.Root))
                            {
                                int removedSetCount = currentAllocation
                                    .SetCountsBySelectionGroupId
                                    .GetValueOrDefault(removed.Anchor.Id, 1);
                                candidateCanonicalLoad[muscle] =
                                    candidateCanonicalLoad.GetValueOrDefault(
                                        muscle) - load * removedSetCount;
                            }
                        }
                        int candidateSetCount = removedPlacements.Length == 1
                            ? currentAllocation.SetCountsBySelectionGroupId
                                .GetValueOrDefault(
                                    removedPlacements[0].Anchor.Id,
                                    1)
                            : 1;
                        foreach ((CanonicalMuscleGroup muscle, int load) in
                                 GetSequenceLoad(candidate))
                        {
                            candidateCanonicalLoad[muscle] =
                                candidateCanonicalLoad.GetValueOrDefault(muscle) +
                                load * candidateSetCount;
                        }
                        candidateBalance = WorkoutMuscleBalancePolicy.Evaluate(
                            candidateCanonicalLoad);
                    }
                    else
                    {
                        HashSet<SelectedSequencePlacement> removedPlacementSet =
                            removedPlacements.ToHashSet();
                        string replacementAllocationKey =
                            string.Join(',', removedPlacements
                                .Select(placement => placement.Anchor.Id)
                                .Order(StringComparer.Ordinal)) +
                            ">" + optionMetadata.AllocationBehaviorKey;
                        if (!replacementAllocationByKey.TryGetValue(
                                replacementAllocationKey,
                                out LongWorkoutAllocation? candidateAllocation))
                        {
                            SelectedSequencePlacement[] candidatePlacements =
                                currentPlacements
                                    .Where(placement =>
                                        !removedPlacementSet.Contains(placement))
                                    .Append(new SelectedSequencePlacement(
                                        candidate,
                                        anchor,
                                        option))
                                    .OrderBy(placement => placement.Anchor.Order)
                                    .ToArray();
                            try
                            {
                                candidateAllocation =
                                    GetCachedLongWorkoutAllocation(
                                        state,
                                        candidatePlacements,
                                        allocationCache,
                                        lockedSelectionGroupIds);
                            }
                            catch (InvalidOperationException)
                            {
                                candidateAllocation = null;
                            }
                            replacementAllocationByKey[
                                replacementAllocationKey] = candidateAllocation;
                        }
                        if (candidateAllocation is null)
                        {
                            continue;
                        }

                        var candidateCanonicalLoad =
                            new Dictionary<CanonicalMuscleGroup, int>(
                                currentCanonicalLoad);
                        foreach (SelectedSequencePlacement placement in
                                 currentPlacements)
                        {
                            int currentSetCount = currentAllocation
                                .SetCountsBySelectionGroupId
                                .GetValueOrDefault(placement.Anchor.Id, 1);
                            int candidateSetCount = removedPlacementSet.Contains(
                                    placement)
                                ? 0
                                : candidateAllocation
                                    .SetCountsBySelectionGroupId
                                    .GetValueOrDefault(placement.Anchor.Id, 1);
                            int setCountDelta =
                                candidateSetCount - currentSetCount;
                            if (setCountDelta == 0)
                            {
                                continue;
                            }

                            foreach ((CanonicalMuscleGroup muscle, int load) in
                                     GetSequenceLoad(placement.Root))
                            {
                                candidateCanonicalLoad[muscle] =
                                    candidateCanonicalLoad.GetValueOrDefault(
                                        muscle) + load * setCountDelta;
                            }
                        }
                        int newCandidateSetCount = candidateAllocation
                            .SetCountsBySelectionGroupId
                            .GetValueOrDefault(anchor.Id, 1);
                        foreach ((CanonicalMuscleGroup muscle, int load) in
                                 GetSequenceLoad(candidate))
                        {
                            candidateCanonicalLoad[muscle] =
                                candidateCanonicalLoad.GetValueOrDefault(muscle) +
                                load * newCandidateSetCount;
                        }
                        candidateBalance = WorkoutMuscleBalancePolicy.Evaluate(
                            candidateCanonicalLoad);
                    }
                    if (WorkoutMuscleBalancePolicy.Compare(
                            candidateBalance,
                            currentBalance) <= 0)
                    {
                        continue;
                    }

                    MuscleBalanceCandidate alternative =
                        CreateMuscleBalanceCandidate(
                            state,
                            candidate,
                            anchor,
                            option,
                            candidateBalance,
                            selectionTimeUnixMilliseconds);
                    if (bestAlternative is null ||
                        IsPreferredMuscleBalanceCandidate(
                            alternative,
                            bestAlternative))
                    {
                        bestAlternative = alternative;
                    }
                }
            }

            if (bestAlternative is null)
            {
                break;
            }

            foreach (WorkoutGroup coveredGroup in bestAlternative.CoveredGroups)
            {
                state.SelectedExerciseIds[GetSelectionStorageKey(
                    coveredGroup.Id,
                    state.ActiveWorkoutModifiers)] = bestAlternative.ExerciseId;
            }
        }
    }

    private bool IsStoredLineupSelectionValid(
        WorkoutState state,
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers modifiers)
    {
        if (IsSavedSelectionValid(state, exercise, group, modifiers))
        {
            return true;
        }

        if (state.ActiveWorkoutMinutes != 0 ||
            exercise.SequenceBlocks.Length == 0 ||
            GetSequenceRoot(exercise).Id != exercise.Id)
        {
            return false;
        }

        IReadOnlyList<WorkoutGroup> resolutionGroups =
            GetResolutionGroupsForGroup(group);
        WorkoutGroup[][] placementOptions = GetSequencePlacementOptions(
            exercise,
            resolutionGroups);
        return placementOptions.Any(option => option.Any(candidate =>
                candidate.CanonicalGroups.SetEquals(group.CanonicalGroups))) &&
            GetSequenceExercises(exercise).All(member =>
                IsCompatibleWithModifiers(member, modifiers));
    }

    private static IReadOnlyList<WorkoutGroup> GetResolutionGroupsForGroup(
        WorkoutGroup group) =>
        MassGroupingTaxonomy.SupportedMinutes
            .Select(minutes => MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .Single(groups => groups.Any(candidate => candidate.Id == group.Id));

    private MuscleBalanceCandidate CreateMuscleBalanceCandidate(
        WorkoutState state,
        Exercise candidate,
        WorkoutGroup anchor,
        IReadOnlyList<WorkoutGroup> coveredGroups,
        MuscleBalanceEvaluation balance,
        long selectionTimeUnixMilliseconds)
    {
        Exercise selectionExercise =
            GetSequenceSelectionExerciseForGroup(candidate, anchor);
        HardExerciseRotationStatus rotationStatus =
            WorkoutRecoveryPolicy.GetRotationStatus(
                selectionExercise,
                anchor,
                state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                selectionTimeUnixMilliseconds);
        bool isRecoveringModerate =
            WorkoutRecoveryPolicy.IsModerateExerciseRecovering(
                selectionExercise,
                state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
                selectionTimeUnixMilliseconds);
        return new MuscleBalanceCandidate(
            candidate.Id,
            coveredGroups,
            balance,
            GetSelectionScore(
                state,
                candidate,
                GetProjectedSelectionPhase(
                    state,
                    anchor,
                    GetSelectionGroups(state).Count)),
            rotationStatus == HardExerciseRotationStatus.FreshHard,
            rotationStatus == HardExerciseRotationStatus.RecoveringHard,
            isRecoveringModerate,
            rotationStatus == HardExerciseRotationStatus.FreshHard
                ? WorkoutRecoveryPolicy.GetLastHardWorkUnixMilliseconds(
                    state.LastHardWorkUnixMillisecondsByPrimaryMuscle,
                    selectionExercise.PrimaryCanonicalGroup)
                : 0L,
            WorkoutModifierPolicy.GetEquipmentPreferenceCount(
                selectionExercise,
                state.ActiveWorkoutModifiers),
            WorkoutCoveragePolicy.IsPrimaryForGroup(selectionExercise, anchor),
            WorkoutSequencePolicy.GetCanonicalCoverage(
                candidate,
                _exercisesById,
                anchor));
    }

    private static bool IsPreferredMuscleBalanceCandidate(
        MuscleBalanceCandidate candidate,
        MuscleBalanceCandidate currentBest)
    {
        int balanceComparison = WorkoutMuscleBalancePolicy.Compare(
            candidate.Balance,
            currentBest.Balance);
        if (balanceComparison != 0)
        {
            return balanceComparison > 0;
        }
        if (candidate.RealScore != currentBest.RealScore)
        {
            return candidate.RealScore > currentBest.RealScore;
        }
        if (candidate.IsFreshHard != currentBest.IsFreshHard)
        {
            return candidate.IsFreshHard;
        }
        if (candidate.IsRecoveringHard != currentBest.IsRecoveringHard)
        {
            return !candidate.IsRecoveringHard;
        }
        if (candidate.IsRecoveringModerate != currentBest.IsRecoveringModerate)
        {
            return !candidate.IsRecoveringModerate;
        }
        if (candidate.LastHardWorkUnixMilliseconds !=
            currentBest.LastHardWorkUnixMilliseconds)
        {
            return candidate.LastHardWorkUnixMilliseconds <
                currentBest.LastHardWorkUnixMilliseconds;
        }
        if (candidate.EquipmentPreferenceCount !=
            currentBest.EquipmentPreferenceCount)
        {
            return candidate.EquipmentPreferenceCount >
                currentBest.EquipmentPreferenceCount;
        }
        if (candidate.IsPrimary != currentBest.IsPrimary)
        {
            return candidate.IsPrimary;
        }
        if (candidate.CanonicalCoverage != currentBest.CanonicalCoverage)
        {
            return candidate.CanonicalCoverage > currentBest.CanonicalCoverage;
        }

        return candidate.ExerciseId < currentBest.ExerciseId;
    }

    private LongWorkoutAllocation GetCachedLongWorkoutAllocation(
        WorkoutState state,
        IReadOnlyList<SelectedSequencePlacement> placements,
        IDictionary<string, LongWorkoutAllocation?> allocationCache,
        IReadOnlySet<string>? lockedSelectionGroupIds = null)
    {
        string signature = string.Join(
            '|',
            placements
                .OrderBy(placement => placement.Anchor.Order)
                .Select(placement => GetLongWorkoutAllocationPlacementKey(
                    state,
                    placement.Root,
                    placement.Anchor)));
        if (allocationCache.TryGetValue(
                signature,
                out LongWorkoutAllocation? cached))
        {
            return cached ?? throw new InvalidOperationException(
                "The selected sequence lengths cannot fill the workout duration.");
        }

        try
        {
            LongWorkoutAllocation allocation = ChooseLongWorkoutAllocation(
                state,
                lockedSelectionGroupIds,
                selectedPlacements: placements);
            allocationCache[signature] = allocation;
            return allocation;
        }
        catch (InvalidOperationException)
        {
            allocationCache[signature] = null;
            throw;
        }
    }

    private string GetLongWorkoutAllocationPlacementKey(
        WorkoutState state,
        Exercise root,
        WorkoutGroup anchor)
    {
        Exercise[] sequenceExercises = GetSequenceExercises(root);
        var keyParts = new List<object>
        {
            anchor.Id,
            root.SequenceBlocks.Length,
            sequenceExercises.Any(WorkoutRecoveryPolicy.IsHardExercise),
            IsSequenceKept(state, anchor.Id, root),
        };
        bool hasPhaseScoreAdjustments = state.ExerciseScoreAdjustmentsByPhase
            .Values
            .Any(adjustments => adjustments.Count > 0);
        if (hasPhaseScoreAdjustments)
        {
            // Schedule order affects allocation only when phase-local scores
            // exist. In that case the key includes every phase-sensitive
            // dependency; without them behaviorally identical roots can be
            // shared safely.
            keyParts.Add(WorkoutSchedulePolicy.GetMuscularDemandPriority(
                WorkoutSchedulePolicy.GetSequenceMuscularDemand(
                    root,
                    _exercisesById)));
            keyParts.Add(GetPhaseScoreAdjustment(
                state,
                root,
                WorkoutExercisePhase.Warmup));
            keyParts.Add(GetPhaseScoreAdjustment(
                state,
                root,
                WorkoutExercisePhase.PeakPerformance));
            keyParts.Add(GetPhaseScoreAdjustment(
                state,
                root,
                WorkoutExercisePhase.Fatigued));
        }

        return string.Join(':', keyParts);
    }

    private IReadOnlyDictionary<CanonicalMuscleGroup, int>
        CalculateScheduledCanonicalLoadEighthUnits(
            IReadOnlyList<SelectedSequencePlacement> placements,
            LongWorkoutAllocation allocation)
    {
        var loadEighthUnits = new Dictionary<CanonicalMuscleGroup, int>();
        foreach (SelectedSequencePlacement placement in
                 placements)
        {
            int setCount = allocation.SetCountsBySelectionGroupId
                .GetValueOrDefault(placement.Anchor.Id, 1);
            foreach (Exercise exercise in GetSequenceExercises(placement.Root))
            {
                WorkoutMuscleBalancePolicy.AddExerciseLoad(
                    loadEighthUnits,
                    exercise,
                    setCount);
            }
        }

        return loadEighthUnits;
    }

    private void SetActiveLongWorkoutAllocation(WorkoutState state) =>
        ApplyLongWorkoutAllocation(state, ChooseLongWorkoutAllocation(state));

    private void ReconcileLineupWithScheduledPhases(
        WorkoutState state, IReadOnlySet<string>? lockedSelectionGroupIds = null)
    {
        if (state.ExerciseScoreAdjustmentsByPhase.Values.All(
                adjustments => adjustments.Count == 0))
        {
            return;
        }

        WorkoutGroup[] selectionGroups = GetSelectionGroups(state).ToArray();
        if (selectionGroups.Length == 0)
        {
            return;
        }

        // Candidate scores are phase-local, while demand ordering and repeated
        // sets determine the phase in which a selected sequence actually ends.
        // Resolve that circular dependency to a stable lineup rather than
        // estimating phase from the unrelated anatomical bucket order.
        var seenLineups = new HashSet<string>(StringComparer.Ordinal);
        string[] prefixOrder = state.ActiveSelectionGroupOrder
            .Where(id => lockedSelectionGroupIds?.Contains(id) == true).ToArray();
        HashSet<string> protectedGroups = GetSelectedSequencePlacements(state)
            .Where(p => lockedSelectionGroupIds?.Contains(p.Anchor.Id) == true)
            .SelectMany(p => p.CoveredGroups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        for (int pass = 0; pass < selectionGroups.Length; pass++)
        {
            Dictionary<string, int> currentLineup = selectionGroups.ToDictionary(
                group => group.Id,
                group => state.SelectedExerciseIds[GetSelectionStorageKey(
                    group.Id,
                    state.ActiveWorkoutModifiers)],
                StringComparer.Ordinal);
            string signature = string.Join(
                ',',
                selectionGroups.Select(group => currentLineup[group.Id]));
            if (!seenLineups.Add(signature))
            {
                break;
            }

            LongWorkoutAllocation allocation = ChooseLongWorkoutAllocation(state, lockedSelectionGroupIds);
            ApplyLongWorkoutAllocation(state, allocation);
            IReadOnlyDictionary<string, WorkoutExercisePhase>
                scheduledPhaseByGroupId = GetScheduledPhaseByGroupId(
                    state,
                    allocation);
            IReadOnlyDictionary<string, int> nextLineup =
                ChooseBestDistinctLineup(
                    state,
                    selectionGroups,
                    state.ActiveWorkoutModifiers,
                    currentExerciseIds: lockedSelectionGroupIds is null ? currentLineup :
                        currentLineup.Where(e => protectedGroups.Contains(e.Key))
                            .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
                    allowSavedSelectionException: lockedSelectionGroupIds is not null,
                    modifierTransitionProtectedGroupIds: protectedGroups,
                    reservedRepeatMinutes: GetSelectedSequencePlacements(state)
                        .Where(p => lockedSelectionGroupIds?.Contains(p.Anchor.Id) == true)
                        .Sum(p => (state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(p.Anchor.Id, 1) - 1) *
                            p.Root.SequenceBlocks.Length),
                    scheduledPhaseByGroupId: scheduledPhaseByGroupId);
            if (selectionGroups.All(group =>
                    nextLineup[group.Id] == currentLineup[group.Id]))
            {
                return;
            }

            ApplyDistinctLineup(
                state,
                selectionGroups,
                nextLineup,
                clearChangedProgress: false);
            if (lockedSelectionGroupIds is not null) SetEditedSelectionOrder(state, prefixOrder);
        }

        ApplyLongWorkoutAllocation(state, ChooseLongWorkoutAllocation(state, lockedSelectionGroupIds));
    }

    private IReadOnlyDictionary<string, WorkoutExercisePhase>
        GetScheduledPhaseByGroupId(
            WorkoutState state,
            LongWorkoutAllocation allocation)
    {
        Dictionary<string, WorkoutExercisePhase> phaseBySelectionGroupId =
            CreateWorkoutSchedule(
                    state,
                    allocation.SetCountsBySelectionGroupId)
                .GroupBy(group => group.SelectionKey, StringComparer.Ordinal)
                .ToDictionary(
                    groups => groups.Key,
                    groups => WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                        groups.Max(group => group.Order)),
                    StringComparer.Ordinal);
        var result = new Dictionary<string, WorkoutExercisePhase>(
            StringComparer.Ordinal);
        foreach (SelectedSequencePlacement placement in
                 GetSelectedSequencePlacements(state))
        {
            WorkoutExercisePhase phase = phaseBySelectionGroupId[
                placement.Anchor.Id];
            foreach (WorkoutGroup coveredGroup in placement.CoveredGroups)
            {
                result[coveredGroup.Id] = phase;
            }
        }

        return result;
    }

    private static void ApplyLongWorkoutAllocation(
        WorkoutState state,
        LongWorkoutAllocation allocation)
    {
        state.ActiveExtraSetSelectionGroupIds =
            new HashSet<string>(allocation.ExtraSetSelectionGroupIds, StringComparer.Ordinal);
        state.ActiveSetCountsBySelectionGroupId = new Dictionary<string, int>(
            allocation.SetCountsBySelectionGroupId,
            StringComparer.Ordinal);
        state.ActiveDirectionPartnerExerciseIds.Clear();
        state.ActiveFullSideRoundIds.Clear();
    }

    private IReadOnlyDictionary<string, int> GetEffectiveSetCounts(
        WorkoutState state)
    {
        return IsLongWorkoutAllocationValid(state)
            ? state.ActiveSetCountsBySelectionGroupId
            : ChooseLongWorkoutAllocation(state).SetCountsBySelectionGroupId;
    }

    private IReadOnlyList<WorkoutGroup> CreateWorkoutSchedule(
        WorkoutState state,
        IReadOnlyDictionary<string, int> setCountsBySelectionGroupId)
    {
        var rounds = new List<WorkoutGroup>(state.ActiveWorkoutMinutes);
        foreach (SelectedSequencePlacement placement in
                 GetScheduleOrderedPlacements(
                     state,
                     GetSelectedSequencePlacements(state)))
        {
            int setCount = Math.Max(
                1,
                setCountsBySelectionGroupId.GetValueOrDefault(
                    placement.Anchor.Id,
                    1));
            for (int setNumber = 1; setNumber <= setCount; setNumber++)
            {
                for (int blockIndex = 0;
                     blockIndex < placement.Root.SequenceBlocks.Length;
                     blockIndex++)
                {
                    ExerciseSequenceBlock block =
                        placement.Root.SequenceBlocks[blockIndex];
                    Exercise blockExercise = _exercisesById[block.ExerciseId];
                    WorkoutGroup blockGroup = placement.CoveredGroups.Count == 1
                        ? placement.Anchor
                        : placement.CoveredGroups.Single(group =>
                            group.CanonicalGroups.Contains(
                                blockExercise.PrimaryCanonicalGroup));
                    if ((state.ActiveDurationSelectionGroupIds is null
                            ? state.ActiveWorkoutMinutes <= 30
                            : state.ActiveSimpleRoundSelectionGroupIds.Contains(placement.Anchor.Id)) &&
                        placement.Root.SequenceBlocks.Length == 1 &&
                        setCount == 1)
                    {
                        rounds.Add(blockGroup with { Order = rounds.Count + 1 });
                        continue;
                    }

                    rounds.Add(blockGroup with
                    {
                        Id = $"{placement.Anchor.Id}.set{setNumber}." +
                            $"block{blockIndex + 1}",
                        Order = rounds.Count + 1,
                        SelectionGroupId = placement.Anchor.Id,
                        ExerciseOverrideId = block.ExerciseId,
                        SequenceBlockIndex = blockIndex,
                        SequenceBlockCount = placement.Root.SequenceBlocks.Length,
                        SetNumber = setNumber,
                        SetCount = setCount,
                        SequenceSideCue = block.SideCue,
                        SequenceDirectionCue = block.DirectionCue,
                        MirrorSequenceMedia = block.MirrorMedia,
                        SequenceMediaSegment = block.MediaSegment,
                    });
                }
            }
        }

        if (rounds.Count != state.ActiveWorkoutMinutes)
        {
            throw new InvalidOperationException(
                $"The {state.ActiveWorkoutMinutes}-minute workout scheduled " +
                $"{rounds.Count} exercise blocks.");
        }

        return rounds.AsReadOnly();
    }

    private sealed record LongWorkoutAllocation(
        HashSet<string> ExtraSetSelectionGroupIds,
        Dictionary<string, int> SetCountsBySelectionGroupId);

    private sealed record SelectedSequencePlacement(
        Exercise Root,
        WorkoutGroup Anchor,
        IReadOnlyList<WorkoutGroup> CoveredGroups);

    private sealed record ShuffleCandidate(
        Exercise Exercise,
        IReadOnlyList<WorkoutGroup> CoveredGroups,
        LongWorkoutAllocation Allocation);

    private sealed record LegacyPhaseDownvoteEvent(
        long SessionId,
        long Timestamp,
        string SelectionGroupId,
        int RootExerciseId,
        WorkoutExercisePhase Phase);

    private sealed record MuscleBalanceCandidate(
        int ExerciseId,
        IReadOnlyList<WorkoutGroup> CoveredGroups,
        MuscleBalanceEvaluation Balance,
        int RealScore,
        bool IsFreshHard,
        bool IsRecoveringHard,
        bool IsRecoveringModerate,
        long LastHardWorkUnixMilliseconds,
        int EquipmentPreferenceCount,
        bool IsPrimary,
        int CanonicalCoverage);

    private sealed record LegacyActiveProgressSnapshot(
        Dictionary<string, ExerciseOutcome> Outcomes,
        Dictionary<string, int> SelectedExerciseIds,
        Dictionary<string, int> DirectionPartnerExerciseIds,
        HashSet<string> FullSideRoundIds,
        string? PendingMovementGroupId,
        long PendingMovementMillisecondsRemaining,
        long PendingMovementEndsAtUnixMilliseconds,
        bool PendingMovementPausedByUser,
        string? PendingRestGroupId,
        long PendingRestEndsAtUnixMilliseconds,
        bool PendingRestKept);
}
