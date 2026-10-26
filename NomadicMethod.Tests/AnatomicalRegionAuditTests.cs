using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class AnatomicalRegionAuditTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
    private static ExerciseSessionService Planner(Exercise[] catalog) => new(catalog, new Random(1),
        () => DateTimeOffset.Parse("2026-10-26T00:00:00Z"));

    [Theory]
    [InlineData(301, "r30.rotator-cuff", WorkoutModifiers.None)]
    [InlineData(1214, "r30.accessory-hip-adductors", WorkoutModifiers.Floor | WorkoutModifiers.UpperBodyClothing)]
    [InlineData(570, "r30.forearm-flexors-pronators", WorkoutModifiers.None)]
    public void CorrectedMovementHasACompletePlacementInItsExistingFineSlot(int id, string slot, WorkoutModifiers modifiers)
    {
        Exercise[] catalog = Catalog();
        Exercise movement = catalog.Single(exercise => exercise.Id == id);
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(30, slot);
        var members = catalog.Where(exercise => movement.SequenceBlocks.Any(block =>
            block.ExerciseId == exercise.Id)).ToArray();
        Assert.Equal(1, WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
            members, [group], modifiers, movement.SequenceBlocks.Length));
        if (movement.SequenceBlocks.Length > 1)
            Assert.Equal(0, WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                members, [group], modifiers, movement.SequenceBlocks.Length - 1));
        if (id == 1214)
            Assert.Equal(0, WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                members, [group], WorkoutModifiers.None, 30));
    }

    [Fact]
    public void Revision96WorkoutAndFeedbackSurviveAdditiveClaims()
    {
        JsonNode legacyJson = JsonSerializer.SerializeToNode(Catalog(), Options)!;
        foreach (JsonNode? exercise in legacyJson.AsArray())
        {
            int id = exercise!["Id"]!.GetValue<int>();
            JsonArray groups = exercise["SecondaryCanonicalGroups"]!.AsArray();
            foreach (JsonNode? group in groups.ToArray())
                if ((id == 301 && group!.GetValue<string>() == "RotatorCuff") ||
                    (id == 1214 && group!.GetValue<string>() == "AccessoryHipAdductors") ||
                    (id == 570 && new[] { "ForearmFlexorsAndPronators", "ForearmExtensorsAndSupinators" }
                        .Contains(group!.GetValue<string>()))) groups.Remove(group);
        }
        Exercise[] legacy = legacyJson.Deserialize<Exercise[]>(Options)!;
        var state = new WorkoutState();
        state.KeptExerciseRootIdsBySelectionGroupId["r15.shoulder"] = [301];
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup] = new() { [301] = -3 };
        var planner = Planner(legacy);
        planner.Initialize(state);
        planner.StartWorkout(state, 15, WorkoutModifiers.None);
        state.CatalogRevision = 96;
        state.ActiveCatalogRevision = 96;
        string saved = JsonSerializer.Serialize(state);
        WorkoutState reopened = JsonSerializer.Deserialize<WorkoutState>(saved)!;
        Planner(Catalog()).RestoreAfterReopen(reopened);
        Assert.Equal(97, reopened.CatalogRevision);
        Assert.Equal(96, reopened.ActiveCatalogRevision);
        Assert.Equal(JsonSerializer.Serialize(state.SelectedExerciseIds), JsonSerializer.Serialize(reopened.SelectedExerciseIds));
        Assert.Equal(JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId), JsonSerializer.Serialize(reopened.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase), JsonSerializer.Serialize(reopened.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(JsonSerializer.Serialize(state.WorkoutHistory), JsonSerializer.Serialize(reopened.WorkoutHistory));
        Assert.Equal(state.ActiveWorkoutSession!.SessionId, reopened.ActiveWorkoutSession!.SessionId);
    }
}
