using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed partial class ExerciseSessionService
{
    // Merge only complete sibling slots into an existing broader target, and
    // retain the change only when the final schedule reduces refused work.
    // Run only for preparation or an explicit duration edit, never on reopen.
    private void BroadenRejectedSelectionSlots(WorkoutState state,
        IReadOnlySet<string>? lockedSelectionGroupIds = null)
    {
        HashSet<int> declinedMovements = GetDifficultyDeclinedMovementIds(state);
        if (declinedMovements.Count == 0 &&
            state.ExerciseScoreAdjustmentsByPhase.Values.All(entries => entries.Values.All(value => value >= 0))) return;
        Dictionary<int, bool> fitness = GetFitnessEligibility(state);
        Exercise[] roots = _exercises.Where(e => e.SequenceBlocks.Length > 0 &&
            GetSequenceRoot(e).Id == e.Id && fitness.GetValueOrDefault(e.Id)).ToArray();
        while (true)
        {
            _planningCancellationToken.ThrowIfCancellationRequested();
            var before = GetPreparedWorkoutFeedbackCost(state, declinedMovements);
            if (before.DeclinedBlocks == 0 && before.RefusedBlocks == 0) return;
            WorkoutGroup[] groups = GetSelectionGroups(state).ToArray();
            HashSet<string> protectedGroups = GetSelectedSequencePlacements(state)
                .Where(p => lockedSelectionGroupIds?.Contains(p.Anchor.Id) == true)
                .SelectMany(p => p.CoveredGroups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
            var phases = GetScheduledPhaseByGroupId(state, state.ActiveSetCountsBySelectionGroupId);
            bool improved = false;
            foreach (WorkoutGroup trapped in groups)
            {
                if (protectedGroups.Contains(trapped.Id)) continue;
                WorkoutExercisePhase phase = phases[trapped.Id];
                Exercise[] eligible = roots.Where(root => IsWorkoutSelectionCandidate(
                    state, root, trapped, state.ActiveWorkoutModifiers, groups)).ToArray();
                if (state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light) && eligible.Any(IsDemandZeroSequence))
                    eligible = eligible.Where(IsDemandZeroSequence).ToArray();
                if (eligible.Length == 0 || eligible.Any(root =>
                        !declinedMovements.Contains(WorkoutModifierPolicy.GetSessionMovementId(root)) &&
                        GetPhaseRefusalDepth(state, root, phase) == 0)) continue;
                foreach (WorkoutGroup broader in KnownWorkoutGroups.Values
                    .Where(g => g.CanonicalGroups.IsProperSupersetOf(trapped.CanonicalGroups) &&
                        WorkoutModifierPolicy.IsSelectionGroupAvailable(g, state.ActiveWorkoutModifiers,
                            state.ActiveCatalogRevision))
                    .OrderBy(g => g.CanonicalGroups.Count).ThenBy(g => g.Id, StringComparer.Ordinal))
                {
                    _planningCancellationToken.ThrowIfCancellationRequested();
                    WorkoutGroup[] siblings = groups.Where(g => g.CanonicalGroups.Overlaps(
                        broader.CanonicalGroups)).ToArray();
                    if (siblings.Length < 2 || siblings.Any(g => protectedGroups.Contains(g.Id) ||
                            !g.CanonicalGroups.IsSubsetOf(broader.CanonicalGroups)) ||
                        !siblings.SelectMany(g => g.CanonicalGroups).ToHashSet().SetEquals(
                            broader.CanonicalGroups)) continue;
                    HashSet<string> replaced = siblings.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
                    WorkoutGroup[] proposed = groups.SelectMany(g => g.Id == siblings[0].Id
                        ? new[] { broader } : replaced.Contains(g.Id) ? [] : new[] { g }).ToArray();
                    if (!roots.Any(root => !declinedMovements.Contains(WorkoutModifierPolicy.GetSessionMovementId(root)) &&
                            GetPhaseRefusalDepth(state, root, phase) == 0 &&
                            IsWorkoutSelectionCandidate(state, root, broader,
                                state.ActiveWorkoutModifiers, proposed))) continue;
                    if (!TryBroaderPreparedWorkout(state, proposed, before, declinedMovements, lockedSelectionGroupIds)) continue;
                    improved = true;
                    break;
                }
                if (improved) break;
            }
            if (!improved) return;
        }
    }

    // Old global scores still rank choices. Explicit phase or difficulty
    // feedback can change the anatomical grouping of a new plan.
    private long GetPhaseRefusalDepth(WorkoutState state, Exercise root, WorkoutExercisePhase phase) =>
        GetMovementRoots(root).Max(variant => Math.Max(0L,
            -(long)GetPhaseScoreAdjustment(state, variant, phase)));

    private (int DeclinedBlocks, int RefusedBlocks, int LightBlocks) GetPreparedWorkoutFeedbackCost(
        WorkoutState state, IReadOnlySet<int> declinedMovements)
    {
        var phases = GetScheduledPhaseByGroupId(state, state.ActiveSetCountsBySelectionGroupId);
        int declined = 0, refused = 0, light = 0;
        foreach (SelectedSequencePlacement placement in GetSelectedSequencePlacements(state))
        {
            int blocks = placement.Root.SequenceBlocks.Length *
                state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(placement.Anchor.Id, 1);
            if (declinedMovements.Contains(WorkoutModifierPolicy.GetSessionMovementId(placement.Root)))
                declined += blocks;
            if (GetPhaseRefusalDepth(state, placement.Root, phases[placement.Anchor.Id]) > 0)
                refused += blocks;
            if (IsDemandZeroSequence(placement.Root)) light += blocks;
        }
        return (declined, refused, light);
    }

    private bool TryBroaderPreparedWorkout(WorkoutState state, WorkoutGroup[] groups,
        (int DeclinedBlocks, int RefusedBlocks, int LightBlocks) before,
        IReadOnlySet<int> declinedMovements, IReadOnlySet<string>? lockedSelectionGroupIds)
    {
        var previousGroups = state.ActiveDurationSelectionGroupIds;
        var previousSimple = state.ActiveSimpleRoundSelectionGroupIds;
        var previousSelections = state.SelectedExerciseIds;
        var previousCounts = state.ActiveSetCountsBySelectionGroupId;
        var previousExtra = state.ActiveExtraSetSelectionGroupIds;
        var previousOrder = state.ActiveSelectionGroupOrder;
        SelectedSequencePlacement[] locked = GetScheduleOrderedPlacements(state, GetSelectedSequencePlacements(state))
            .Where(p => lockedSelectionGroupIds?.Contains(p.Anchor.Id) == true).ToArray();
        Dictionary<string, int> lockedExercises = locked.SelectMany(p => p.CoveredGroups.Select(g =>
            (g.Id, RootId: p.Root.Id))).ToDictionary(e => e.Id, e => e.RootId, StringComparer.Ordinal);
        bool accepted = false;
        try
        {
            state.ActiveDurationSelectionGroupIds = groups.Select(g => g.Id).ToList();
            state.ActiveSimpleRoundSelectionGroupIds = lockedSelectionGroupIds is not null
                ? new(previousSimple, StringComparer.Ordinal)
                : state.ActiveWorkoutMinutes <= 30 ? groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal) : [];
            state.SelectedExerciseIds = new(previousSelections, StringComparer.Ordinal);
            state.ActiveSetCountsBySelectionGroupId = new(previousCounts, StringComparer.Ordinal);
            state.ActiveExtraSetSelectionGroupIds = new(previousExtra, StringComparer.Ordinal);
            state.ActiveSelectionGroupOrder = [];
            var lineup = ChooseBestDistinctLineup(state, groups, state.ActiveWorkoutModifiers,
                currentExerciseIds: lockedExercises,
                allowSavedSelectionException: lockedSelectionGroupIds is not null,
                modifierTransitionProtectedGroupIds: lockedExercises.Keys.ToHashSet(StringComparer.Ordinal),
                reservedRepeatMinutes: locked.Sum(p => (previousCounts.GetValueOrDefault(p.Anchor.Id, 1) - 1) *
                    p.Root.SequenceBlocks.Length));
            ApplyDistinctLineup(state, groups, lineup, clearChangedProgress: false);
            RebalanceNewExercisesByMuscleBalance(state, lockedSelectionGroupIds);
            if (lockedSelectionGroupIds is not null) SetEditedSelectionOrder(state, locked.Select(p => p.Anchor.Id).ToArray());
            ApplyLongWorkoutAllocation(state, ChooseLongWorkoutAllocation(state, lockedSelectionGroupIds));
            ReconcileLineupWithScheduledPhases(state, lockedSelectionGroupIds);
            var after = GetPreparedWorkoutFeedbackCost(state, declinedMovements);
            accepted = (after.DeclinedBlocks < before.DeclinedBlocks ||
                after.DeclinedBlocks == before.DeclinedBlocks && after.RefusedBlocks < before.RefusedBlocks) &&
                (!state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light) ||
                    after.LightBlocks >= before.LightBlocks);
            return accepted;
        }
        catch (InvalidOperationException error) when (error.Message.Contains("No complete exercise lineup", StringComparison.Ordinal) ||
            error.Message.StartsWith("The selected ", StringComparison.Ordinal))
        {
            // Eligibility alone does not prove a complete atomic lineup fits.
            return false;
        }
        finally
        {
            if (!accepted)
            {
                state.ActiveDurationSelectionGroupIds = previousGroups;
                state.ActiveSimpleRoundSelectionGroupIds = previousSimple;
                state.SelectedExerciseIds = previousSelections;
                state.ActiveSetCountsBySelectionGroupId = previousCounts;
                state.ActiveExtraSetSelectionGroupIds = previousExtra;
                state.ActiveSelectionGroupOrder = previousOrder;
            }
        }
    }

    private void UpdateSequenceCatalog(WorkoutState state, int minutes,
        IReadOnlySet<int>? protectedRootIds = null)
    {
        state.ActiveSingleDirectionExerciseIds ??= [];
        state.ActiveSingleDirectionExerciseIds = _catalogExercises
            .Where(exercise => WorkoutSequencePolicy.CanUseSingleDirection(exercise) &&
                (protectedRootIds?.Contains(exercise.Id) == true
                    ? state.ActiveSingleDirectionExerciseIds.Contains(exercise.Id)
                    : minutes < 30))
            .Select(exercise => exercise.Id).ToHashSet();
        UseWorkoutCatalog(state);
    }

    // Cold starts never interpret time outside the app as completed exercise.
    // A running checkpoint may survive abrupt process death without OnPause.
    public void RestoreAfterReopen(WorkoutState state)
    {
        UseWorkoutCatalog(state);
        if (state.PendingMovementMillisecondsRemaining > 0)
        {
            state.PendingMovementEndsAtUnixMilliseconds = 0;
            state.PendingMovementPausedByUser = true;
        }
        if (state.PendingRestGroupId is not null)
        {
            state.PendingRestMillisecondsRemaining =
                state.PendingRestMillisecondsRemaining > 0
                    ? state.PendingRestMillisecondsRemaining
                    : RestDurationMilliseconds;
            state.PendingRestEndsAtUnixMilliseconds = 0;
            state.PendingRestPausedByUser = true;
        }
        Initialize(state);
    }

    public int GetMinimumActiveWorkoutMinutes(WorkoutState state)
    {
        UseWorkoutCatalog(state);
        WorkoutGroup? current = GetNextGroup(state);
        if (current is null) return MinimumWorkoutMinutes;
        int protectedBlocks = GetActiveGroups(state)
            .Where(round => round.SelectionKey == current.SelectionKey)
            .Max(round => round.Order);
        return SupportedWorkoutMinutes.First(minutes => minutes >= protectedBlocks);
    }

    // Invoked only for the user's explicit confirmed end, never on a
    // lifecycle event. Unfinished work is not a rejection or a completed block.
    public void EndActiveWorkout(WorkoutState state)
    {
        UseWorkoutCatalog(state);
        if (state.ActiveWorkoutMinutes == 0 || state.WorkoutCompleted)
            return;
        if (state.ActiveWorkoutSession is null)
        {
            FinalizeCurrentWorkout(state);
            return;
        }
        // A swipe is already an explicit decision, even while its rest clock runs.
        // Ending without one must still never manufacture a dislike.
        if (GetPendingRestFeedback(state) is { } feedback &&
            GetValidPendingRestGroup(state) is { IsFinalSequenceRound: true } pending)
        {
            ApplySequenceOutcome(state, pending, feedback == ExerciseOutcome.Tick, explicitOutcome: feedback);
            ClearPendingRest(state);
            ApplyRecordedWorkoutKeeps(state);
        }
        // Stop archives recorded work; it never needs to solve the unfinished lineup.
        FinalizeActiveWorkoutSession(state, WorkoutSessionStatus.Interrupted);
        ResetToDurationSelection(state);
    }

    public void ResizeActiveWorkout(WorkoutState state, int minutes)
    {
        UseWorkoutCatalog(state);
        if (state.ActiveWorkoutMinutes == minutes) return;
        if (!IsValidWorkoutMinutes(minutes) || state.WorkoutCompleted ||
            state.ActiveWorkoutMinutes == 0 || minutes < GetMinimumActiveWorkoutMinutes(state))
            throw new InvalidOperationException("The duration must include the current exercise sequence.");

        WorkoutGroup current = GetNextGroup(state)!;
        WorkoutGroup[] priorRounds = GetActiveGroups(state).ToArray();
        SelectedSequencePlacement[] prior = GetScheduleOrderedPlacements(
            state, GetSelectedSequencePlacements(state));
        string[] lockedIds = prior.TakeWhile(p => p.Anchor.Id != current.SelectionKey)
            .Select(p => p.Anchor.Id).Append(current.SelectionKey).ToArray();
        HashSet<string> locked = lockedIds.ToHashSet(StringComparer.Ordinal);
        SelectedSequencePlacement[] prefix = prior.Where(p => locked.Contains(p.Anchor.Id)).ToArray();
        Dictionary<string, int> lockedExercises = prefix
            .SelectMany(p => p.CoveredGroups.Select(g => (g.Id, RootId: p.Root.Id)))
            .ToDictionary(e => e.Id, e => e.RootId, StringComparer.Ordinal);
        HashSet<string> protectedGroups = lockedExercises.Keys.ToHashSet(StringComparer.Ordinal);
        int protectedCount = priorRounds.Count(round => locked.Contains(round.SelectionKey));
        int freeMinutes = minutes - protectedCount;
        HashSet<CanonicalMuscleGroup> covered = prefix
            .SelectMany(p => GetSequenceExercises(p.Root))
            .SelectMany(e => e.SecondaryCanonicalGroups.Append(e.PrimaryCanonicalGroup)).ToHashSet();
        WorkoutGroup[] candidates = GetSelectionGroups(minutes, state.ActiveWorkoutModifiers)
            .Where(g => !protectedGroups.Contains(g.Id))
            .OrderBy(g => g.CanonicalGroups.Count(covered.Contains) / (double)g.CanonicalGroups.Count)
            .ThenBy(g => g.Order).ToArray();
        // At the end of the finest resolution, an extension may need a new
        // broader slot; do not append sets to a slot already completed/kept.
        if (freeMinutes > 0 && candidates.Length == 0)
            candidates = KnownWorkoutGroups.Values
                .Where(g => !protectedGroups.Contains(g.Id) &&
                    WorkoutModifierPolicy.IsSelectionGroupAvailable(g, state.ActiveWorkoutModifiers))
                .OrderBy(g => g.CanonicalGroups.Count).ThenBy(g => g.Id, StringComparer.Ordinal).ToArray();

        int previousMinutes = state.ActiveWorkoutMinutes;
        int previousLastMinutes = state.LastWorkoutMinutes;
        var previousGroups = state.ActiveDurationSelectionGroupIds;
        var previousSimple = state.ActiveSimpleRoundSelectionGroupIds;
        var previousSelections = state.SelectedExerciseIds;
        var previousCounts = state.ActiveSetCountsBySelectionGroupId;
        var previousExtra = state.ActiveExtraSetSelectionGroupIds;
        var previousOrder = state.ActiveSelectionGroupOrder;
        var previousRetained = state.ActiveModifierRetainedSelectionGroupIds;
        var previousSingleDirections = state.ActiveSingleDirectionExerciseIds;
        bool applied = false;
        try
        {
            state.ActiveWorkoutMinutes = minutes;
            UpdateSequenceCatalog(state, minutes, prefix.Select(p => p.Root.Id).ToHashSet());
            state.ActiveSimpleRoundSelectionGroupIds = priorRounds
                .Where(r => r.Id == r.SelectionKey && locked.Contains(r.SelectionKey))
                .Select(r => r.SelectionKey).ToHashSet(StringComparer.Ordinal);
            state.ActiveModifierRetainedSelectionGroupIds = new(protectedGroups, StringComparer.Ordinal);
            Exception? lastError = null;
            // Preserve the solver's 63-slot bitmask contract even after several
            // cross-resolution edits. Remaining time can be allocated as sets.
            int maximumCount = Math.Min(Math.Min(freeMinutes, candidates.Length), 63 - protectedGroups.Count);
            for (int count = maximumCount; count >= (freeMinutes > 0 ? 1 : 0); count--)
            {
                state.SelectedExerciseIds = new(previousSelections, StringComparer.Ordinal);
                state.ActiveSetCountsBySelectionGroupId = new(previousCounts, StringComparer.Ordinal);
                state.ActiveExtraSetSelectionGroupIds = new(previousExtra, StringComparer.Ordinal);
                state.ActiveSelectionGroupOrder = [];
                WorkoutGroup[] groups = prefix.SelectMany(p => p.CoveredGroups)
                    .Concat(candidates.Take(count)).DistinctBy(g => g.Id).ToArray();
                state.ActiveDurationSelectionGroupIds = groups.Select(g => g.Id).ToList();
                try
                {
                    IReadOnlyDictionary<string, int> lineup = ChooseBestDistinctLineup(
                        state, groups, state.ActiveWorkoutModifiers,
                        currentExerciseIds: lockedExercises, allowSavedSelectionException: true,
                        modifierTransitionProtectedGroupIds: protectedGroups,
                        reservedRepeatMinutes: protectedCount - prefix.Sum(p => p.Root.SequenceBlocks.Length));
                    ApplyDistinctLineup(state, groups, lineup, clearChangedProgress: false);
                    RebalanceNewExercisesByMuscleBalance(state, locked);
                    SetEditedSelectionOrder(state, lockedIds);
                    ApplyLongWorkoutAllocation(state, ChooseLongWorkoutAllocation(state, locked));
                    ReconcileLineupWithScheduledPhases(state, locked);
                    BroadenRejectedSelectionSlots(state, locked);
                    WorkoutGroup[] resized = GetActiveGroups(state).ToArray();
                    if (!priorRounds.Take(protectedCount).SequenceEqual(resized.Take(protectedCount)) ||
                        GetNextGroup(state)?.Id != current.Id)
                    {
                        int changed = Enumerable.Range(0, protectedCount).FirstOrDefault(i => priorRounds[i] != resized[i]);
                        throw new InvalidOperationException($"A duration edit changed protected block {changed + 1}: " +
                            $"{priorRounds[changed]} -> {resized[changed]}.");
                    }
                    applied = true;
                    break;
                }
                catch (InvalidOperationException error)
                {
                    // Fewer finer slots can accommodate a complete atomic
                    // sequence where a one-slot-per-minute plan cannot fit.
                    lastError ??= error;
                }
            }
            if (!applied) throw new InvalidOperationException("The new duration could not preserve this workout.", lastError);
            state.LastWorkoutMinutes = minutes;
            WorkoutSessionLog session = EnsureActiveWorkoutSession(state, startedBeforeLogging: true);
            session.WorkoutMinutes = minutes;
            session.DurationChanges.Add(new WorkoutDurationChangeLog
            {
                ChangedAtUnixMilliseconds = GetCurrentUnixTimeMilliseconds(),
                PreviousMinutes = previousMinutes,
                NewMinutes = minutes,
                PlannedSelections = CreateCurrentSelectionSnapshots(state, session),
            });
        }
        catch
        {
            state.ActiveWorkoutMinutes = previousMinutes;
            state.LastWorkoutMinutes = previousLastMinutes;
            state.ActiveDurationSelectionGroupIds = previousGroups;
            state.ActiveSimpleRoundSelectionGroupIds = previousSimple;
            state.SelectedExerciseIds = previousSelections;
            state.ActiveSetCountsBySelectionGroupId = previousCounts;
            state.ActiveExtraSetSelectionGroupIds = previousExtra;
            state.ActiveSelectionGroupOrder = previousOrder;
            state.ActiveModifierRetainedSelectionGroupIds = previousRetained;
            state.ActiveSingleDirectionExerciseIds = previousSingleDirections;
            UseWorkoutCatalog(state);
            throw;
        }
    }

    private void SetEditedSelectionOrder(WorkoutState state, IReadOnlyList<string> prefixOrder)
    {
        SelectedSequencePlacement[] placements = GetSelectedSequencePlacements(state);
        Exercise? current = placements.FirstOrDefault(p => p.Anchor.Id == prefixOrder.LastOrDefault())?.Root;
        state.ActiveSelectionGroupOrder = prefixOrder.Concat(WorkoutSchedulePolicy.OrderBySetup(
                placements.Where(p => !prefixOrder.Contains(p.Anchor.Id)),
                p => p.Root, p => p.Anchor.Order, _exercisesById, state.ActiveWorkoutModifiers, current,
                _planningCancellationToken)
            .Select(p => p.Anchor.Id)).ToList();
    }
}
