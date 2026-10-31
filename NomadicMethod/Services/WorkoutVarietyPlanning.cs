using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed partial class ExerciseSessionService
{
    // Anatomical coverage is a floor, not a reason to spend unused time on the
    // same movement. Reuse the existing cross-resolution slot mechanism used by
    // duration edits to schedule additional complete, distinct selections.
    private void FillRemainingTimeWithDistinctSelections(WorkoutState state)
    {
        if (state.ActiveCoverageAccountingVersion < 2 || state.ActiveWorkoutSession is not null)
            return;

        while (true)
        {
            bool added = false;
            while (TryAddDistinctSelection(state)) added = true;
            if (!added) break;
            SetActiveLongWorkoutAllocation(state);
            ReconcileLineupWithScheduledPhases(state);
        }
    }

    private bool TryAddDistinctSelection(WorkoutState state)
    {
        _planningCancellationToken.ThrowIfCancellationRequested();
        WorkoutGroup[] groups = GetSelectionGroups(state).ToArray();
        SelectedSequencePlacement[] placements = GetSelectedSequencePlacements(state);
        int remaining = state.ActiveWorkoutMinutes - placements.Sum(p => p.Root.SequenceBlocks.Length);
        if (remaining <= 0 || groups.Length >= 63) return false;

        HashSet<int> usedMovements = placements.Select(p =>
            WorkoutModifierPolicy.GetSessionMovementId(p.Root)).ToHashSet();
        Dictionary<int, bool> eligibility = GetFitnessEligibility(state);
        HashSet<int> excluded = GetExcludedMovementIds(state);
        Exercise[] available = _exercises.Where(root =>
            root.SequenceBlocks.Length > 0 && root.SequenceBlocks.Length <= remaining &&
            GetSequenceRoot(root).Id == root.Id && eligibility.GetValueOrDefault(root.Id) &&
            !usedMovements.Contains(WorkoutModifierPolicy.GetSessionMovementId(root)) &&
            !excluded.Contains(WorkoutModifierPolicy.GetSessionMovementId(root)) &&
            GetSequenceExercises(root).All(member => IsCompatibleWithModifiers(member, state.ActiveWorkoutModifiers)))
            .ToArray();
        if (available.Length == 0) return false;

        var occupiedGroupIds = groups.Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        LongWorkoutAllocation allocation = ChooseLongWorkoutAllocation(state, selectedPlacements: placements);
        var load = CalculateScheduledCanonicalLoadEighthUnits(placements, allocation);
        bool isLight = state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light);
        int LightBlocks(IEnumerable<SelectedSequencePlacement> selections, LongWorkoutAllocation sets) =>
            selections.Where(p => IsDemandZeroSequence(p.Root)).Sum(p =>
                p.Root.SequenceBlocks.Length * sets.SetCountsBySelectionGroupId[p.Anchor.Id]);
        int priorLightBlocks = isLight ? LightBlocks(placements, allocation) : 0;
        if (priorLightBlocks == state.ActiveWorkoutMinutes && !available.Any(IsDemandZeroSequence))
            return false;

        WorkoutGroup[] candidates = KnownWorkoutGroups.Values.Where(g =>
                !occupiedGroupIds.Contains(g.Id) &&
                WorkoutModifierPolicy.IsSelectionGroupAvailable(g, state.ActiveWorkoutModifiers))
            .OrderBy(g => g.CanonicalGroups.Average(m => load.GetValueOrDefault(m)))
            .ThenBy(g => g.CanonicalGroups.Count)
            .ThenBy(g => g.Id, StringComparer.Ordinal).ToArray();
        foreach (WorkoutGroup extraGroup in candidates)
        {
            _planningCancellationToken.ThrowIfCancellationRequested();
            WorkoutGroup[] expanded = [.. groups, extraGroup];
            HashSet<int> allowedRoots = available.Where(root =>
                GetSequenceExercises(root).All(member =>
                    WorkoutCoveragePolicy.GetCanonicalCoverage(member, extraGroup) * 2 >= extraGroup.CanonicalGroups.Count) &&
                IsWorkoutSelectionCandidate(state, root, extraGroup, state.ActiveWorkoutModifiers, [extraGroup]))
                .Select(root => root.Id).ToHashSet();
            if (allowedRoots.Count == 0) continue;
            var carriedKeeps = BuildCrossResolutionKeepPreferences(state, [extraGroup]);
            while (allowedRoots.Count > 0)
            {
                IReadOnlyDictionary<string, int> lineup;
                try
                {
                    lineup = ChooseBestDistinctLineup(state, [extraGroup], state.ActiveWorkoutModifiers,
                        carriedKeepRootIdsBySelectionGroupId: carriedKeeps,
                        reservedRepeatMinutes: state.ActiveWorkoutMinutes - remaining,
                        allowedExerciseIds: allowedRoots);
                    Exercise extraRoot = _exercisesById[lineup[extraGroup.Id]];
                    if (usedMovements.Contains(WorkoutModifierPolicy.GetSessionMovementId(extraRoot)))
                    { allowedRoots.Remove(extraRoot.Id); continue; }
                    SelectedSequencePlacement[] next = [.. placements, new(extraRoot, extraGroup, [extraGroup])];
                    LongWorkoutAllocation nextAllocation = ChooseLongWorkoutAllocation(state, selectedPlacements: next);
                    if (isLight && LightBlocks(next, nextAllocation) < priorLightBlocks ||
                        GetPhaseRefusalDepth(state, extraRoot, GetAddedSelectionPhase(state, next, nextAllocation, extraGroup.Id)) > 0)
                    { allowedRoots.Remove(extraRoot.Id); continue; }
                }
                catch (InvalidOperationException error) when (
                    error.Message.Contains("No complete exercise lineup exists", StringComparison.Ordinal) ||
                    error.Message.Contains("selected sequence lengths cannot fill", StringComparison.Ordinal))
                {
                    // A candidate slot may have no unused movement whose complete
                    // sequence fits. Other existing slots can still fill the budget.
                    break;
                }
                state.ActiveDurationSelectionGroupIds = expanded.Select(g => g.Id).ToList();
                state.ActiveSelectionGroupOrder.Clear();
                state.SelectedExerciseIds[GetSelectionStorageKey(extraGroup.Id, state.ActiveWorkoutModifiers)] = lineup[extraGroup.Id];
                if (carriedKeeps.GetValueOrDefault(extraGroup.Id)?.Contains(lineup[extraGroup.Id]) == true &&
                    GetKeptRootIdsForSlot(state, extraGroup.Id).Add(lineup[extraGroup.Id]))
                    SyncLegacyKeptExerciseIds(state);
                return true;
            }
        }
        return false;
    }

    private WorkoutExercisePhase GetAddedSelectionPhase(WorkoutState state,
        SelectedSequencePlacement[] placements, LongWorkoutAllocation allocation, string groupId)
    {
        int blockOrder = 0;
        foreach (var placement in GetScheduleOrderedPlacements(state, placements))
        {
            blockOrder += placement.Root.SequenceBlocks.Length * allocation.SetCountsBySelectionGroupId[placement.Anchor.Id];
            if (placement.Anchor.Id == groupId) return WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(blockOrder);
        }
        throw new InvalidOperationException("The additional selection is missing from its proposed schedule.");
    }
}
