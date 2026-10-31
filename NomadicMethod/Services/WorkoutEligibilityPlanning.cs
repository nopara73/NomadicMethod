using NomadicMethod.Models;
using NomadicMethod.Data;
using System.Text.Json;

namespace NomadicMethod.Services;

public sealed partial class ExerciseSessionService
{
    private string[]? PreflightPreparedGroups(WorkoutState state, int minutes, WorkoutModifiers modifiers)
    {
        if (_minimumMovementChoices <= 1) return null;
        // Preserve the application's fail-without-mutation contract while
        // resolving the effective coverage before enforcing its variety gate.
        string json = JsonSerializer.Serialize(state, WorkoutJsonContext.Default.WorkoutState);
        WorkoutState probe = JsonSerializer.Deserialize(json, WorkoutJsonContext.Default.WorkoutState)!;
        probe.OuraRecovery = state.OuraRecovery;
        probe.ActiveCatalogRevision = CatalogMigrationRules.CurrentCatalogRevision;
        probe.ActiveCoverageAccountingVersion = 2;
        probe.ActiveWorkoutMinutes = minutes;
        probe.ActiveWorkoutModifiers = NormalizeWorkoutModifiers(modifiers);
        probe.ActiveDurationSelectionGroupIds = null;
        probe.ActiveSimpleRoundSelectionGroupIds.Clear();
        probe.ActiveModifierRetainedSelectionGroupIds.Clear();
        probe.ActiveModifierProtectedSelectionGroupId = null;
        probe.ActiveSelectionGroupOrder.Clear();
        try
        {
            UpdateSequenceCatalog(probe, minutes);
            WorkoutGroup[] initial = GetSelectionGroups(probe).ToArray();
            ResolveEligibleLineup(probe, initial, probe.ActiveWorkoutModifiers,
                new HashSet<string>(StringComparer.Ordinal), 0,
                _ => new Dictionary<string, int>(StringComparer.Ordinal), out WorkoutGroup[] resolved);
            return initial.Select(group => group.Id).SequenceEqual(resolved.Select(group => group.Id))
                ? null : resolved.Select(group => group.Id).ToArray();
        }
        finally { UseWorkoutCatalog(state); }
    }

    private void ApplyResolvedSelectionGroups(WorkoutState state, IReadOnlyList<WorkoutGroup> groups,
        IReadOnlySet<string>? protectedGroupIds = null)
    {
        if (groups.Select(group => group.Id).SequenceEqual(GetSelectionGroups(state).Select(group => group.Id))) return;
        state.ActiveDurationSelectionGroupIds = groups.Select(group => group.Id).ToList();
        state.ActiveSimpleRoundSelectionGroupIds = state.ActiveSimpleRoundSelectionGroupIds
            .Where(id => groups.Any(group => group.Id == id))
            .Concat(groups.Where(group => protectedGroupIds?.Contains(group.Id) != true &&
                state.ActiveWorkoutMinutes <= 30).Select(group => group.Id)).ToHashSet(StringComparer.Ordinal);
    }

    // Resolve the anatomical partition before a preparation can fail. Coarsen
    // complete siblings only; exercise ratings, anatomy and physical predicates
    // are never changed. Call only for preparation or an explicit setup edit.
    private IReadOnlyDictionary<string, int> ResolveEligibleLineup(
        WorkoutState state,
        WorkoutGroup[] initialGroups,
        WorkoutModifiers modifiers,
        IReadOnlySet<string> protectedGroupIds,
        int reservedRepeatMinutes,
        Func<WorkoutGroup[], IReadOnlyDictionary<string, int>> plan,
        out WorkoutGroup[] resolvedGroups)
    {
        Dictionary<int, bool> fitness = GetFitnessEligibility(state);
        Dictionary<string, int> protectedRoots = protectedGroupIds
            .Where(id => state.SelectedExerciseIds.ContainsKey(GetSelectionStorageKey(id, modifiers)))
            .ToDictionary(id => id, id => state.SelectedExerciseIds[GetSelectionStorageKey(id, modifiers)], StringComparer.Ordinal);
        IReadOnlyDictionary<string, int> Solve(WorkoutGroup[] groups)
        {
            EnsurePlannableWorkoutAvailability(state, state.ActiveWorkoutMinutes, modifiers,
                groups, protectedRoots, reservedRepeatMinutes);
            return plan(groups);
        }
        int budget = state.ActiveWorkoutMinutes - reservedRepeatMinutes;
        Exercise[] roots = _exercises.Where(root => root.SequenceBlocks.Length > 0 &&
            root.SequenceBlocks.Length <= budget && GetSequenceRoot(root).Id == root.Id &&
            fitness.GetValueOrDefault(root.Id) && GetSequenceExercises(root)
                .All(member => IsCompatibleWithModifiers(member, modifiers))).ToArray();
        HashSet<CanonicalMuscleGroup> required = initialGroups
            .SelectMany(group => group.CanonicalGroups).ToHashSet();
        WorkoutGroup[] parents = KnownWorkoutGroups.Values
            .Where(group => WorkoutModifierPolicy.IsSelectionGroupAvailable(
                group, modifiers, state.ActiveCatalogRevision))
            .OrderBy(group => group.CanonicalGroups.Count)
            .ThenBy(group => group.Id, StringComparer.Ordinal).ToArray();
        var pending = new List<WorkoutGroup[]> { initialGroups };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        InvalidOperationException? firstError = null;
        bool broaderResolutionsAdded = false;
        while (pending.Count > 0)
        {
            _planningCancellationToken.ThrowIfCancellationRequested();
            // Keep as much fine coverage as possible, with deterministic ties.
            int index = 0;
            for (int i = 1; i < pending.Count; i++)
                if (pending[i].Length > pending[index].Length) index = i;
            WorkoutGroup[] groups = pending[index];
            pending.RemoveAt(index);
            string signature = string.Join('|', groups.Select(group => group.Id).Order(StringComparer.Ordinal));
            if (!visited.Add(signature)) continue;
            HashSet<string> empty = groups.Where(group => !protectedGroupIds.Contains(group.Id) &&
                    !roots.Any(root => IsWorkoutSelectionCandidate(state, root, group, modifiers, groups)))
                .Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
            if (empty.Count == 0)
            {
                try
                {
                    IReadOnlyDictionary<string, int> lineup = Solve(groups);
                    resolvedGroups = groups;
                    return lineup;
                }
                catch (InvalidOperationException error) when (
                    error.Message.Contains("No complete exercise lineup exists", StringComparison.Ordinal) ||
                    error.Message == WorkoutVarietyPolicy.UnavailableMessage)
                {
                    // Nonempty slots alone do not prove a simultaneous atomic
                    // lineup fits the remaining budget. Try complete parents.
                    firstError ??= error;
                }
            }
            if (!broaderResolutionsAdded)
            {
                broaderResolutionsAdded = true;
                // Some resolution boundaries cross: no single parent is an
                // exact union of fine siblings. Repartition all unperformed
                // coverage together, retaining every protected slot verbatim.
                WorkoutGroup[] locked = initialGroups.Where(group => protectedGroupIds.Contains(group.Id)).ToArray();
                HashSet<CanonicalMuscleGroup> remaining = initialGroups.Where(group => !protectedGroupIds.Contains(group.Id))
                    .SelectMany(group => group.CanonicalGroups).ToHashSet();
                foreach (int minutes in SupportedWorkoutMinutes.Where(minutes => minutes <= Math.Min(state.ActiveWorkoutMinutes, 30))
                    .OrderDescending())
                {
                    WorkoutGroup[] broader = GetSelectionGroups(minutes, modifiers, catalogRevision: state.ActiveCatalogRevision)
                        .Where(group => group.CanonicalGroups.Overlaps(remaining) && !protectedGroupIds.Contains(group.Id)).ToArray();
                    WorkoutGroup[] proposed = locked.Concat(broader).DistinctBy(group => group.Id).ToArray();
                    if (proposed.Length >= initialGroups.Length ||
                        !required.IsSubsetOf(proposed.SelectMany(group => group.CanonicalGroups).ToHashSet())) continue;
                    pending.Add(proposed);
                }
            }
            foreach (WorkoutGroup parent in parents)
            {
                WorkoutGroup[] siblings = groups.Where(group => group.CanonicalGroups
                    .Overlaps(parent.CanonicalGroups)).ToArray();
                if (siblings.Length < 2 || siblings.Any(group =>
                        protectedGroupIds.Contains(group.Id) ||
                        !group.CanonicalGroups.IsSubsetOf(parent.CanonicalGroups)) ||
                    empty.Count > 0 && !siblings.Any(group => empty.Contains(group.Id))) continue;
                HashSet<CanonicalMuscleGroup> combined = siblings.SelectMany(group => group.CanonicalGroups).ToHashSet();
                if (siblings.Sum(group => group.CanonicalGroups.Count) != combined.Count ||
                    !combined.SetEquals(parent.CanonicalGroups.Where(required.Contains))) continue;
                HashSet<string> replaced = siblings.Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
                WorkoutGroup[] proposed = groups.SelectMany(group => group.Id == siblings[0].Id
                    ? new[] { parent } : replaced.Contains(group.Id) ? [] : new[] { group }).ToArray();
                if (!roots.Any(root => IsWorkoutSelectionCandidate(state, root, parent, modifiers, proposed))) continue;
                pending.Add(proposed);
            }
        }
        resolvedGroups = initialGroups;
        if (firstError is not null) throw firstError;
        // Retain the ordinary, precise unavailable diagnostic for genuinely
        // impossible physical/assessed profiles; never substitute unsuitable work.
        return Solve(initialGroups);
    }
}
