using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutCatalogPolicy
{
    public static IReadOnlySet<int> FitnessCoverageExerciseIds { get; } = new HashSet<int>
    {
        1506, 1507, 1508, 1509, 1516, 1521, 1522, 1523, 1524, 1525, 1531, 1532, 1533, 1534, 1535, 1540, 1602, 1603, 1604, 1612, 1613, 1614, 1622, 1623, 1624, 1632, 1633, 1634, 1642, 1643, 1644,
    };

    public static IReadOnlySet<int> FloorLocomotionExerciseIds { get; } =
        Enumerable.Range(1431, 8).ToHashSet();
    public const int BodyCircleExerciseId = 1303;
    public static IReadOnlySet<int> ExecutionCorrectionExerciseIds { get; } =
        new HashSet<int> { 236, 275, 516, 588, 743, 1097, 1109, 1112, 1117,
            1139, 1162, 1177, 1180, 1214, 1225, 1230, 1244, 1299 };
    private static readonly IReadOnlySet<int> PreviouslyContinuousExecutionIds =
        new HashSet<int> { 236, 275, 516, 743 };
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
        IReadOnlyList<Exercise> catalog, int activeCatalogRevision,
        IReadOnlySet<int>? singleDirectionExerciseIds = null)
    {
        if (activeCatalogRevision is > 0 and < 88)
            catalog = catalog.Where(exercise => !FitnessCoverageExerciseIds.Contains(exercise.Id)).ToArray();
        if (activeCatalogRevision is > 0 and < 85)
            catalog = catalog.Where(exercise => !FloorLocomotionExerciseIds.Contains(exercise.Id)).ToArray();
        if (activeCatalogRevision is > 0 and < 84)
            catalog = catalog.Where(exercise => !exercise.ElevationRequired).ToArray();
        if (activeCatalogRevision is > 0 and < 83)
        {
            // Freeze both the old phase geometry and its original footage.
            // A corrected one-side loop cannot replace an old full-loop phase.
            catalog = catalog.Where(exercise => exercise.Id != BodyCircleExerciseId).Select(exercise =>
                ExecutionCorrectionExerciseIds.Contains(exercise.Id)
                    ? exercise.WithWorkoutMetadata(
                        PreviouslyContinuousExecutionIds.Contains(exercise.Id)
                            ? ExerciseSideSequence.Continuous : ExerciseSideSequence.Alternating,
                        [new ExerciseSequenceBlock { ExerciseId = exercise.Id, MirrorMedia = false }],
                        exercise.ShyCompatibility, directions: ExerciseDirectionSequence.None,
                        video: $"exercise_videos/revision82/exercise_{exercise.Id:D4}.mp4")
                    : exercise).ToArray();
        }
        if (singleDirectionExerciseIds is { Count: > 0 })
            catalog = catalog.Select(exercise => singleDirectionExerciseIds.Contains(exercise.Id) &&
                    WorkoutSequencePolicy.CanUseSingleDirection(exercise)
                ? exercise.WithWorkoutMetadata(exercise.SideSequence,
                    [exercise.SequenceBlocks[0]], exercise.ShyCompatibility)
                : exercise).ToArray();

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
