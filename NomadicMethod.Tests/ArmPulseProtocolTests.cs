using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ArmPulseProtocolTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "arm-pulses.json")));

    [Fact]
    public void ArmPulsesCompleteBothFootPositionsBeforeTheTwoWheelDirections()
    {
        using JsonDocument fixture = Fixture();
        JsonElement c = fixture.RootElement;
        Exercise[] catalog = Catalog();
        Exercise root = catalog.Single(e => e.Id == c.GetProperty("id").GetInt32());
        Assert.Equal(c.GetProperty("name").GetString(), root.Name);
        Assert.Equal(c.GetProperty("motionProfile").GetString(), root.MotionProfile);
        Assert.Equal(c.GetProperty("sideSequence").GetString(), root.SideSequence.ToString());
        Assert.Equal(JsonSerializer.Deserialize<ExerciseSequenceBlock[]>(c.GetProperty("blocks"), Options), root.SequenceBlocks);
        Assert.Equal(2, WorkoutSequencePolicy.GetMembers(root, catalog.ToDictionary(e => e.Id)).Length);
        Assert.False(WorkoutSequencePolicy.CanUseSingleDirection(root));
        foreach (int minutes in new[] { 3, 5, 7, 10, 15, 20, 30, 60, 90 })
            Assert.Equal(4, WorkoutSequencePolicy.GetBlockCount(root, minutes));
        foreach (int revision in new[] { 82, 99, 100, 101 })
        {
            Exercise previous = WorkoutCatalogPolicy.ForWorkout(catalog, revision).Single(e => e.Id == root.Id);
            Assert.Equal(ExerciseSideSequence.Continuous, previous.SideSequence);
            Assert.Equal(JsonSerializer.Deserialize<ExerciseSequenceBlock[]>(c.GetProperty("previousBlocks"), Options), previous.SequenceBlocks);
        }
    }

    [Fact]
    public void ClarityRenamePreservesFeedbackButOlderDifferentActionsStillResetIt()
    {
        using JsonDocument fixture = Fixture();
        JsonElement c = fixture.RootElement;
        Exercise[] catalog = Catalog();
        int id = c.GetProperty("id").GetInt32();
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [id] = new(c.GetProperty("previousName").GetString()!, c.GetProperty("video").GetString()!, -7),
        };
        Assert.Contains(id, CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored));
        stored[id] = stored[id] with { Name = c.GetProperty("legacyName").GetString()! };
        Assert.DoesNotContain(id, CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored));
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("Movement")]
    [InlineData("Rest")]
    public void UnfinishedThreePartSequenceRetainsCheckpointFeedbackAndHistory(string phase)
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-31T00:00:00Z");
        Exercise[] catalog = Catalog();
        catalog.Single(e => e.Id == 264).Score = 100000;
        Exercise[] legacy = WorkoutCatalogPolicy.ForWorkout(catalog, 101).ToArray();
        var old = new ExerciseSessionService(legacy, new Random(1), () => now);
        var state = new WorkoutState();
        old.Initialize(state);
        state.KeptExerciseRootIdsBySelectionGroupId["r30.shoulder-abductors"] = [264];
        old.StartWorkout(state, 60, WorkoutModifiers.Floor | WorkoutModifiers.Elevation | WorkoutModifiers.UpperBodyClothing);
        state.CatalogRevision = state.ActiveCatalogRevision = 101;
        WorkoutGroup current = old.GetActiveGroups(state).First(g => old.GetSelectedExercise(state, g).Id == 264);
        while (old.GetNextGroup(state)!.Id != current.Id)
        {
            WorkoutGroup next = old.GetNextGroup(state)!;
            old.BeginRest(state, next, now.AddSeconds(15).ToUnixTimeMilliseconds());
            if (old.IsIntermediateSequenceBlock(state, next)) old.AdvanceSequence(state, next);
            else old.RecordOutcome(state, next, true);
            old.ClearPendingRest(state);
        }
        if (phase == "Movement") old.BeginMovement(state, current, 28000, now.AddSeconds(28).ToUnixTimeMilliseconds());
        if (phase == "Rest") old.BeginRest(state, current, now.AddSeconds(15).ToUnixTimeMilliseconds());
        state.KeptExerciseRootIdsBySelectionGroupId[current.SelectionKey] = [264];
        WorkoutGroup[] groups = old.GetActiveGroups(state).ToArray();
        string history = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        var restored = new ExerciseSessionService(catalog, new Random(3), () => now.AddDays(365));
        restored.RestoreAfterReopen(state);
        Assert.Equal(101, state.ActiveCatalogRevision);
        Assert.Equal(groups, restored.GetActiveGroups(state));
        Assert.Equal(current.Id, restored.GetNextGroup(state)!.Id);
        Assert.Equal(3, restored.GetSelectedExercise(state, current).SequenceBlocks.Length);
        Assert.Equal(history, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        if (phase == "Movement")
        {
            Assert.True(state.PendingMovementPausedByUser);
            Assert.Equal(28000, state.PendingMovementMillisecondsRemaining);
        }
        if (phase == "Rest")
        {
            Assert.True(state.PendingRestPausedByUser);
            Assert.Equal(15000, state.PendingRestMillisecondsRemaining);
        }
        int minimum = restored.GetMinimumActiveWorkoutMinutes(state);
        restored.ResizeActiveWorkout(state, minimum);
        Assert.Equal(current.Id, restored.GetNextGroup(state)!.Id);
        Assert.Equal(3, restored.GetSelectedExercise(state, current).SequenceBlocks.Length);
        Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
    }
}
