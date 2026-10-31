using System.Numerics;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed partial class ExerciseSessionService
{
    private readonly int _minimumMovementChoices;
    private readonly Dictionary<string, bool> _workoutVarietyCache = new(StringComparer.Ordinal);

    // Application entry points use the full product requirement. The ordinary
    // constructor also supports deliberately small standalone planning catalogs.
    public static ExerciseSessionService CreateForApplication(
        IReadOnlyList<Exercise> exercises,
        Random? random = null,
        Func<DateTimeOffset>? utcNowProvider = null,
        TimeZoneInfo? localTimeZone = null,
        IReadOnlyDictionary<int, int[][]>? fitnessRatings = null) =>
        new(exercises, random, utcNowProvider, localTimeZone, fitnessRatings,
            WorkoutVarietyPolicy.RequiredMovementChoices);

    internal void EnsureSelectableWorkoutVariety(WorkoutState state, int minutes, WorkoutModifiers modifiers,
        IReadOnlyList<WorkoutGroup>? actualGroups = null,
        IReadOnlyDictionary<string, int>? protectedRoots = null, int reservedRepeatMinutes = 0)
    {
        if (_minimumMovementChoices <= 1) return;
        modifiers = NormalizeWorkoutModifiers(modifiers) & ~WorkoutModifiers.Light;
        Dictionary<int, bool> eligibility = GetFitnessEligibility(state);
        WorkoutGroup[] groups = actualGroups?.ToArray() ?? MassGroupingTaxonomy.GetResolution(Math.Min(minutes, 30)).Groups
            .Where(group => WorkoutModifierPolicy.IsSelectionGroupAvailable(group, modifiers)).ToArray();
        bool cacheable = (protectedRoots?.Count ?? 0) == 0 && reservedRepeatMinutes == 0;
        string key = $"{minutes}:{(int)modifiers}:{actualGroups is null}:{_boundCatalogRevision}:" +
            string.Join(',', _boundSingleDirectionExerciseIds.Order()) + ":" +
            string.Join(',', eligibility.Where(entry => entry.Value).Select(entry => entry.Key).Order()) + ":" +
            string.Join('|', groups.Select(group => group.Id));
        if (cacheable && _workoutVarietyCache.TryGetValue(key, out bool valid))
        {
            if (!valid) throw new InvalidOperationException(WorkoutVarietyPolicy.UnavailableMessage);
            return;
        }
        protectedRoots ??= new Dictionary<string, int>();
        IReadOnlyList<Exercise> catalog = actualGroups is null ? _catalogExercises : _exercises;
        var byId = catalog.ToDictionary(exercise => exercise.Id);
        var indexes = groups.Select((group, index) => (group.Id, index)).ToDictionary(entry => entry.Id, entry => entry.index);
        var candidates = new List<AtomicSequenceCandidate>();
        ulong validateMask = 0;
        foreach ((WorkoutGroup group, int index) in groups.Select((group, index) => (group, index)))
            if (!protectedRoots.ContainsKey(group.Id)) validateMask |= 1UL << index;
        var protectedFamilies = protectedRoots.Values.Distinct().Select(id =>
            WorkoutModifierPolicy.GetSessionMovementId(byId[id])).ToHashSet();
        // Completed/current work is mandatory, even when later feedback changed
        // its zone. It never counts as another alternative for a remaining slot.
        foreach (var frozen in protectedRoots.GroupBy(entry => WorkoutModifierPolicy.GetSessionMovementId(byId[entry.Value])))
        {
            Exercise root = byId[frozen.First().Value];
            ulong mask = frozen.Aggregate(0UL, (value, entry) => value | 1UL << indexes[entry.Key]);
            int blocks = frozen.Select(entry => entry.Value).Distinct().Sum(id => byId[id].SequenceBlocks.Length);
            candidates.Add(new(root.Id, WorkoutModifierPolicy.GetSessionMovementId(root), mask,
                blocks, new BigInteger[groups.Length], root.Id));
        }
        foreach (Exercise root in catalog.Where(exercise => exercise.SequenceBlocks.Length > 0 &&
            !protectedFamilies.Contains(WorkoutModifierPolicy.GetSessionMovementId(exercise))))
        {
            _planningCancellationToken.ThrowIfCancellationRequested();
            Exercise[] members = WorkoutSequencePolicy.GetMembers(root, byId);
            if (!eligibility.GetValueOrDefault(root.Id) || members.Length == 0 ||
                !members.All(member => WorkoutModifierPolicy.IsCompatible(member, modifiers)))
                continue;
            int cost = WorkoutSequencePolicy.GetBlockCount(root, minutes);
            foreach (WorkoutGroup[] option in WorkoutSequencePolicy.GetPlacementOptions(root, byId, groups, includeSharedCoverage: true))
            {
                if (option.Any(group => protectedRoots.ContainsKey(group.Id) ||
                    !IsWorkoutSelectionCandidate(state, root, group, modifiers, groups))) continue;
                ulong mask = option.Aggregate(0UL, (value, group) => value | 1UL << indexes[group.Id]);
                candidates.Add(new(root.Id, WorkoutModifierPolicy.GetSessionMovementId(root), mask, cost,
                    new BigInteger[groups.Length], checked(cost * 10000 + root.Id)));
            }
        }
        // A raw shortage already proves failure. Avoid running exact witnesses
        // for a speculative added slot that cannot reach the minimum at all.
        bool rawSupply = Enumerable.Range(0, groups.Length).Where(index => (validateMask & (1UL << index)) != 0)
            .All(index => candidates.Where(candidate => (candidate.CoverageMask & (1UL << index)) != 0)
                .Select(candidate => candidate.MovementId).Distinct().Take(_minimumMovementChoices).Count() >= _minimumMovementChoices);
        bool sufficient = groups.Length > 0 && rawSupply && (validateMask == 0 || WorkoutVarietyPolicy.FindDeficiencies(groups.Length,
            minutes - reservedRepeatMinutes, candidates, _minimumMovementChoices, _planningCancellationToken, validateMask).Count == 0);
        if (cacheable)
        {
            if (_workoutVarietyCache.Count >= 64) _workoutVarietyCache.Clear();
            _workoutVarietyCache[key] = sufficient;
        }
        if (!sufficient) throw new InvalidOperationException(WorkoutVarietyPolicy.UnavailableMessage);
    }
}
