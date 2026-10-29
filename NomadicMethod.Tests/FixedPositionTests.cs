using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class FixedPositionTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;

    [Theory]
    [InlineData(1223)]
    [InlineData(1413)]
    public void AlternatingTrunkMotionStillRequiresBothFixedPositions(int id)
    {
        using JsonDocument cases = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "fixed-position-cases.json")));
        JsonElement c = cases.RootElement.EnumerateArray().Single(e => e.GetProperty("id").GetInt32() == id);
        Exercise exercise = Catalog().Single(e => e.Id == id);
        Assert.Equal(c.GetProperty("sideSequence").GetString(), exercise.SideSequence.ToString());
        Assert.True(exercise.SideSequence.UsesTimedSides());
        Assert.Equal(c.GetProperty("sideCues").EnumerateArray().Select(v => v.GetString()),
            exercise.SequenceBlocks.Select(b => b.SideCue.ToString()));
        Assert.Equal(new[] { false, true }, exercise.SequenceBlocks.Select(b => b.MirrorMedia));
        Assert.All(exercise.SequenceBlocks, b => Assert.Equal(id, b.ExerciseId));
        Assert.False(WorkoutSequencePolicy.CanUseSingleDirection(exercise));
        foreach (int minutes in new[] { 3, 5, 7, 10, 15, 20, 30, 60, 90 })
            Assert.Equal(2, WorkoutSequencePolicy.GetBlockCount(exercise, minutes));
        Assert.Equal(c.GetProperty("sessionMovementId").GetInt32(), WorkoutModifierPolicy.GetSessionMovementId(exercise));
        WorkoutGroup template = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        Assert.Equal(new[] { WorkoutBlockAccent.Blue, WorkoutBlockAccent.Red },
            exercise.SequenceBlocks.Select(b => WorkoutDisplayPolicy.GetAccent(template with { SequenceSideCue = b.SideCue })));
    }

    [Theory]
    [InlineData(1223)]
    [InlineData(1413)]
    public void ShorteningRetainsBothPositionsOfTheCurrentSelection(int id)
    {
        Exercise[] catalog = Catalog();
        catalog.Single(e => e.Id == id).Score = 100000;
        var service = new ExerciseSessionService(catalog, new Random(1));
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 60, WorkoutModifiers.Floor | WorkoutModifiers.Elevation |
            WorkoutModifiers.UpperBodyClothing);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string targetKey = rounds.First(g => service.GetSelectedExercise(state, g).Id == id).SelectionKey;
        state.ActiveSelectionGroupOrder = new[] { targetKey }.Concat(rounds.Select(g => g.SelectionKey)
            .Where(key => key != targetKey).Distinct()).ToList();
        WorkoutGroup[] before = service.GetActiveGroups(state).Where(g => g.SelectionKey == targetKey).ToArray();
        Assert.True(before.Length >= 2 && before.Length % 2 == 0);
        int minimum = service.GetMinimumActiveWorkoutMinutes(state);
        Assert.True(minimum < 60);
        service.ResizeActiveWorkout(state, minimum);
        Assert.Equal(before, service.GetActiveGroups(state).Where(g => g.SelectionKey == targetKey));
        service.AdvanceSequence(state, service.GetNextGroup(state)!);
        Assert.Equal(catalog.Single(e => e.Id == id).SequenceBlocks[1].SideCue,
            service.GetNextGroup(state)!.SequenceSideCue);
    }

    [Theory]
    [InlineData(1223, "Ready")]
    [InlineData(1223, "Movement")]
    [InlineData(1223, "Rest")]
    [InlineData(1413, "Ready")]
    [InlineData(1413, "Movement")]
    [InlineData(1413, "Rest")]
    public void Revision98RetainsItsOriginalCheckpointAndPreferences(int id, string phase)
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
        Exercise[] catalog = Catalog();
        catalog.Single(e => e.Id == id).Score = 100000;
        Exercise[] legacy = WorkoutCatalogPolicy.ForWorkout(catalog, 98).ToArray();
        Assert.Equal(ExerciseSideSequence.Alternating, legacy.Single(e => e.Id == id).SideSequence);
        Assert.Single(legacy.Single(e => e.Id == id).SequenceBlocks);
        var state = new WorkoutState();
        var service = new ExerciseSessionService(legacy, new Random(1), () => now);
        service.Initialize(state);
        service.StartWorkout(state, 30, WorkoutModifiers.Floor | WorkoutModifiers.Elevation | WorkoutModifiers.UpperBodyClothing);
        WorkoutGroup current = service.GetActiveGroups(state).First(g => service.GetSelectedExercise(state, g).Id == id);
        while (service.GetNextGroup(state)!.Id != current.Id)
        {
            WorkoutGroup next = service.GetNextGroup(state)!;
            service.BeginRest(state, next, now.AddSeconds(15).ToUnixTimeMilliseconds());
            if (service.IsIntermediateSequenceBlock(state, next)) service.AdvanceSequence(state, next);
            else service.RecordOutcome(state, next, true);
            service.ClearPendingRest(state);
        }
        if (phase == "Movement") service.BeginMovement(state, current, 28000, now.AddSeconds(28).ToUnixTimeMilliseconds());
        if (phase == "Rest") service.BeginRest(state, current, now.AddSeconds(15).ToUnixTimeMilliseconds());
        state.CatalogRevision = 98;
        state.ActiveCatalogRevision = 98;
        if (phase == "Ready") state.ActiveCatalogRevision = 0;
        state.KeptExerciseRootIdsBySelectionGroupId[current.SelectionKey] = [id];
        string history = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        string archived = JsonSerializer.Serialize(state.WorkoutHistory);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string scores = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        var rounds = service.GetActiveGroups(state).Select(g => (g.Id, service.GetSelectedExercise(state, g).Id)).ToArray();
        for (int reopen = 0; reopen < 2; reopen++)
        {
            state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state), Options)!;
            service = new ExerciseSessionService(catalog, new Random(3), () => now.AddDays(1));
            service.RestoreAfterReopen(state);
            Assert.Equal(98, state.ActiveCatalogRevision);
            Assert.Equal(rounds, service.GetActiveGroups(state).Select(g => (g.Id, service.GetSelectedExercise(state, g).Id)));
            Assert.Equal(history, JsonSerializer.Serialize(state.ActiveWorkoutSession));
            Assert.Equal(archived, JsonSerializer.Serialize(state.WorkoutHistory));
            Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
            Assert.Equal(scores, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
            Assert.Single(service.GetSelectedExercise(state, current).SequenceBlocks);
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
        }
    }
}
