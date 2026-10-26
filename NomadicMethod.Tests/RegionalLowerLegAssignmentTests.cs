using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class RegionalLowerLegAssignmentTests
{
    private const string RegionSlot = "r10.anterior-lateral-lower-leg-dorsal-foot";
    private const WorkoutModifiers Profile = WorkoutModifiers.HardFloor | WorkoutModifiers.Wall |
        WorkoutModifiers.Silence;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
    private static ExerciseSessionService Planner(Exercise[] catalog) => new(catalog, new Random(1),
        () => DateTimeOffset.Parse("2026-10-26T00:00:00Z"));

    [Fact]
    public void LateralCompartmentWorkCanActuallyFillTheExistingRegionalRound()
    {
        var state = new WorkoutState();
        state.KeptExerciseRootIdsBySelectionGroupId[RegionSlot] = [389];
        Exercise[] catalog = Catalog();
        ExerciseSessionService planner = Planner(catalog);
        planner.Initialize(state);
        planner.StartWorkout(state, 10, Profile);
        Assert.Contains(state.ActiveWorkoutSession!.InitialSelections, selection =>
            selection.RootExerciseId == 389 && selection.CoveredWorkoutGroupIds.Contains(RegionSlot));

        // Toe-angle and arm-range aliases remain one movement each.
        Assert.Single(catalog.Where(exercise => new[] { 187, 252, 253, 254, 562, 566, 581, 582 }
            .Contains(exercise.Id)).Select(WorkoutModifierPolicy.GetSessionMovementId).Distinct());
    }

    [Fact]
    public void AdditiveAnatomyCorrectionPreservesUnfinishedWorkoutAndFeedback()
    {
        JsonNode legacyJson = JsonSerializer.SerializeToNode(Catalog(), Options)!;
        foreach (JsonNode? exercise in legacyJson.AsArray())
        {
            JsonArray groups = exercise!["SecondaryCanonicalGroups"]!.AsArray();
            if (new[] { 1029, 1210, 1295, 1410 }.Contains(exercise["Id"]!.GetValue<int>())) continue;
            foreach (JsonNode? group in groups.ToArray())
                if (group!.GetValue<string>() == nameof(CanonicalMuscleGroup.AnteriorLateralLowerLegAndDorsalFoot))
                    groups.Remove(group);
        }
        Exercise[] legacy = legacyJson.Deserialize<Exercise[]>(Options)!;
        var state = new WorkoutState();
        state.KeptExerciseRootIdsBySelectionGroupId[RegionSlot] = [165];
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup] = new() { [1295] = -2 };
        state.ExerciseScoreAdjustmentsBySelectionGroupId[RegionSlot] = new() { [191] = -3 };
        ExerciseSessionService previous = Planner(legacy);
        previous.Initialize(state);
        previous.StartWorkout(state, 10, Profile);
        state.CatalogRevision = 94;
        state.ActiveCatalogRevision = 94;
        string selections = JsonSerializer.Serialize(state.SelectedExerciseIds);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        string scopedFeedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsBySelectionGroupId);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        long sessionId = state.ActiveWorkoutSession!.SessionId;
        WorkoutState reopened = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        Exercise[] current = Catalog();
        current.Single(exercise => exercise.Id == 187).Score = -4;
        Planner(current).RestoreAfterReopen(reopened);
        Assert.Equal(selections, JsonSerializer.Serialize(reopened.SelectedExerciseIds));
        Assert.Equal(keeps, JsonSerializer.Serialize(reopened.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(feedback, JsonSerializer.Serialize(reopened.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(scopedFeedback, JsonSerializer.Serialize(reopened.ExerciseScoreAdjustmentsBySelectionGroupId));
        Assert.Equal(history, JsonSerializer.Serialize(reopened.WorkoutHistory));
        Assert.Equal(sessionId, reopened.ActiveWorkoutSession!.SessionId);
        Assert.Equal(94, reopened.ActiveCatalogRevision);
        Assert.Equal(95, reopened.CatalogRevision);
        Assert.Equal(-4, current.Single(exercise => exercise.Id == 187).Score);
    }
}
