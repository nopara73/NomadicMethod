using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed partial class ExerciseSessionService
{
    // Cold starts never interpret time outside the app as completed exercise.
    // A running checkpoint may survive abrupt process death without OnPause.
    public void RestoreAfterReopen(WorkoutState state)
    {
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
        if (state.ActiveWorkoutMinutes == 0 || state.WorkoutCompleted)
            return;
        FinalizeCurrentWorkout(state);
    }

    public void ResizeActiveWorkout(WorkoutState state, int minutes)
    {
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
        bool applied = false;
        try
        {
            state.ActiveWorkoutMinutes = minutes;
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
            throw;
        }
    }

    private void SetEditedSelectionOrder(WorkoutState state, IReadOnlyList<string> prefixOrder)
    {
        state.ActiveSelectionGroupOrder = prefixOrder.Concat(GetSelectedSequencePlacements(state)
            .Where(p => !prefixOrder.Contains(p.Anchor.Id))
            .OrderBy(p => WorkoutSchedulePolicy.GetMuscularDemandPriority(
                WorkoutSchedulePolicy.GetSequenceMuscularDemand(p.Root, _exercisesById)))
            .ThenBy(p => p.Anchor.Order).Select(p => p.Anchor.Id)).ToList();
    }
}
