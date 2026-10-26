using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NomadicMethod.Data;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WallTibialisHardFloorTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-26T00:00:00Z");
    private const string ShinSlot = "r10.anterior-lateral-lower-leg-dorsal-foot";
    private const WorkoutModifiers Profile = WorkoutModifiers.HardFloor | WorkoutModifiers.Wall |
        WorkoutModifiers.Silence;
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
    private static ExerciseSessionService Planner(Exercise[] catalog) => new(catalog, new Random(1), () => Now);

    [Fact]
    public void HardFloorSupportsTheControlledWallRaiseWithoutRelaxingOtherRestrictions()
    {
        Exercise exercise = Catalog().Single(e => e.Id == 165);
        using JsonDocument cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Assets", "wall-tibialis-hard-floor-cases.json")));
        foreach (JsonElement item in cases.RootElement.EnumerateArray())
        {
            WorkoutModifiers modifiers = item.GetProperty("modifiers").EnumerateArray()
                .Aggregate(WorkoutModifiers.None, (flags, value) => flags |
                    Enum.Parse<WorkoutModifiers>(value.GetString()!));
            Assert.Equal(item.GetProperty("eligible").GetBoolean(),
                WorkoutModifierPolicy.IsCompatible(exercise, modifiers));
        }
        Assert.True(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(108,
            ExerciseDatabaseVersionPolicy.CurrentVersion));
    }

    [Fact]
    public void SavedKeepCanFillTheHardFloorShinSlot()
    {
        Exercise[] catalog = Catalog();
        var state = new WorkoutState();
        state.KeptExerciseRootIdsBySelectionGroupId[ShinSlot] = [165];
        ExerciseSessionService planner = Planner(catalog);
        planner.Initialize(state);
        planner.StartWorkout(state, 10, Profile);
        Assert.Equal(165, state.ActiveWorkoutSession!.InitialSelections.Single(selection =>
            selection.CoveredWorkoutGroupIds.Contains(ShinSlot)).RootExerciseId);
        Assert.Equal(10, planner.GetActiveGroups(state).Count);
        Assert.Equal([165], state.KeptExerciseRootIdsBySelectionGroupId[ShinSlot]);
    }

    [Theory]
    [InlineData(92)]
    [InlineData(93)]
    public void CorrectionPreservesUnfinishedLineupAndFeedback(int revision)
    {
        JsonNode legacyJson = JsonSerializer.SerializeToNode(Catalog(), Options)!;
        JsonNode tibialis = legacyJson.AsArray().Single(exercise => exercise!["Id"]!.GetValue<int>() == 165)!;
        tibialis["UpperBodyClothingRequirement"] = "ClothingRequired";
        if (revision == 92) tibialis["HardFloorCompatibility"] = "Incompatible";
        Exercise[] legacy = legacyJson.Deserialize<Exercise[]>(Options)!;
        legacy.Single(exercise => exercise.Id == 165).Score = 4;
        var state = new WorkoutState();
        state.KeptExerciseRootIdsBySelectionGroupId[ShinSlot] = [165];
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup] = new() { [1295] = -2 };
        ExerciseSessionService previous = Planner(legacy);
        previous.Initialize(state);
        previous.StartWorkout(state, 10, Profile);
        state.CatalogRevision = revision;
        state.ActiveCatalogRevision = revision;
        string selections = JsonSerializer.Serialize(state.SelectedExerciseIds);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        long sessionId = state.ActiveWorkoutSession!.SessionId;
        WorkoutState reopened = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        Exercise[] current = Catalog();
        current.Single(exercise => exercise.Id == 165).Score = 4;
        Planner(current).RestoreAfterReopen(reopened);
        Assert.Equal(selections, JsonSerializer.Serialize(reopened.SelectedExerciseIds));
        Assert.Equal(keeps, JsonSerializer.Serialize(reopened.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(feedback, JsonSerializer.Serialize(reopened.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(history, JsonSerializer.Serialize(reopened.WorkoutHistory));
        Assert.Equal(sessionId, reopened.ActiveWorkoutSession!.SessionId);
        Assert.Equal(4, current.Single(exercise => exercise.Id == 165).Score);
    }
}
