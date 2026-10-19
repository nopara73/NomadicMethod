using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutCatalogPolicy
{
    public static IReadOnlySet<int> SlotVarietyExerciseIds { get; } =
        new HashSet<int> { 1294, 1295, 1296, 1297, 1298, 1299, 1300, 1301, 1302 };
    public static IReadOnlySet<int> HandPositionExerciseIds { get; } =
        new HashSet<int> { 32, 216, 224, 510, 740, 1012 };
    private static readonly IReadOnlySet<int> ShyCorrectionIds = new HashSet<int>
    {
        432, 435, 436, 437, 438, 439, 440, 441, 443, 446, 447, 452, 453, 458,
        460, 461, 468, 469, 471, 472, 476, 479, 494, 496, 547, 1002,
        1025, 1026, 1027,
    };

    public static void MigrateSplitJackPreferences(WorkoutState state,
        IEnumerable<Exercise> catalog, int sourceRevision)
    {
        if (sourceRevision is < 73 or >= 80) return;
        int[] ids = [459, 468, 469];
        Exercise[] members = catalog.Where(e => ids.Contains(e.Id)).ToArray();
        // Frozen unfinished workouts still own the original three-member series.
        if (members.Length != ids.Length || members.Any(e =>
            e.SequenceBlocks.Length != 1 || e.SequenceBlocks[0].ExerciseId != e.Id)) return;
        foreach (HashSet<int> kept in state.KeptExerciseRootIdsBySelectionGroupId.Values)
            if (kept.Contains(459)) kept.UnionWith(ids);
        if (state.LastKeptExerciseIds.Contains(459)) state.LastKeptExerciseIds.UnionWith(ids);
        foreach (Dictionary<int, int> scores in state.ExerciseScoreAdjustmentsByPhase.Values
            .Concat(state.ExerciseScoreAdjustmentsBySelectionGroupId.Values))
        {
            if (!scores.TryGetValue(459, out int value)) continue;
            foreach (int id in ids) scores.TryAdd(id, value);
        }
    }

    public static IReadOnlyList<Exercise> ForWorkout(
        IReadOnlyList<Exercise> catalog, int activeCatalogRevision)
    {
        if (activeCatalogRevision is > 0 and < 82)
        {
            // Catalog additions and newly recognized lateral knee lifts must
            // not change the lineup geometry of an unfinished older workout.
            catalog = catalog.Where(exercise => !SlotVarietyExerciseIds.Contains(exercise.Id))
                .Select(exercise => exercise.Id == 906
                    ? exercise.WithWorkoutMetadata(exercise.SideSequence,
                        exercise.SequenceBlocks, exercise.ShyCompatibility,
                        [CanonicalMuscleGroup.HipFlexors])
                    : exercise).ToArray();
        }
        if (activeCatalogRevision is < 73 or >= 80) return catalog;
        int[] jackIds = [459, 468, 469];
        bool hasJackSeries = jackIds.All(id => catalog.Any(e => e.Id == id));
        ExerciseSequenceBlock[] jackBlocks = hasJackSeries
            ? jackIds.SelectMany(id => catalog.Single(e => e.Id == id).SequenceBlocks).ToArray() : [];
        return catalog.Select(exercise =>
        {
            bool handPosition = HandPositionExerciseIds.Contains(exercise.Id) &&
                exercise.SideSequence == ExerciseSideSequence.ShownHandPositionThenOpposite;
            bool shyCorrection = ShyCorrectionIds.Contains(exercise.Id);
            bool jackSeries = hasJackSeries && jackIds.Contains(exercise.Id);
            if (!handPosition && !shyCorrection && !jackSeries) return exercise;
            return exercise.WithWorkoutMetadata(
                handPosition ? ExerciseSideSequence.Continuous : exercise.SideSequence,
                handPosition ? [new ExerciseSequenceBlock { ExerciseId = exercise.Id,
                    MirrorMedia = false }] : jackSeries
                    ? exercise.Id == 459 ? jackBlocks : [] : exercise.SequenceBlocks,
                shyCorrection ? ExerciseShyCompatibility.Compatible : exercise.ShyCompatibility);
        }).ToArray();
    }
}
