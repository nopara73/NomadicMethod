using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class CatalogPositionTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;

    [Fact]
    public void EveryReviewedFixedPositionSurvivesItsWholeOwningSequence()
    {
        Exercise[] catalog = Catalog();
        using JsonDocument cases = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "catalog-position-cases.json")));
        foreach (JsonElement c in cases.RootElement.EnumerateArray())
        {
            int id = c.GetProperty("id").GetInt32();
            Exercise exercise = catalog.Single(e => e.Id == id);
            Assert.Equal(c.GetProperty("sideSequence").GetString(), exercise.SideSequence.ToString());
            Assert.True(exercise.SideSequence.UsesTimedSides());
            foreach (Exercise owner in catalog.Where(e => e.SequenceBlocks.Any(b => b.ExerciseId == id)))
            {
                ExerciseSequenceBlock[] pair = owner.SequenceBlocks.Where(b => b.ExerciseId == id).ToArray();
                Assert.Equal(new[] { false, true }, pair.Select(b => b.MirrorMedia));
                Assert.Equal(new[] { WorkoutBlockAccent.Blue, WorkoutBlockAccent.Red }, pair.Select(b =>
                    WorkoutDisplayPolicy.GetAccent(MassGroupingTaxonomy.GetResolution(30).Groups[0] with { SequenceSideCue = b.SideCue })));
                Assert.False(WorkoutSequencePolicy.CanUseSingleDirection(owner));
                foreach (int minutes in new[] { 3, 5, 7, 10, 15, 20, 30, 60, 90 })
                    Assert.Equal(owner.SequenceBlocks.Length, WorkoutSequencePolicy.GetBlockCount(owner, minutes));
            }
        }
    }

    [Fact]
    public void Revision99PreservesAllOriginalProtocolsAndLinkedMembers()
    {
        Exercise[] catalog = Catalog();
        using Stream source = typeof(WorkoutProtocolHistory).Assembly.GetManifestResourceStream("NomadicMethod.WorkoutProtocolHistory")!;
        using JsonDocument history = JsonDocument.Parse(source);
        IReadOnlyList<Exercise> restored = WorkoutCatalogPolicy.ForWorkout(catalog, 99);
        foreach (JsonElement old in history.RootElement.GetProperty("exercises").EnumerateArray())
        {
            Exercise actual = restored.Single(e => e.Id == old.GetProperty("id").GetInt32());
            Assert.Equal(old.GetProperty("sideSequence").GetString(), actual.SideSequence.ToString());
            Assert.Equal(old.GetProperty("directionSequence").GetString(), actual.DirectionSequence.ToString());
            Assert.Equal(JsonSerializer.Deserialize<ExerciseSequenceBlock[]>(old.GetProperty("sequenceBlocks"), Options), actual.SequenceBlocks);
        }
        Assert.Equal(catalog, WorkoutCatalogPolicy.ForWorkout(catalog, CatalogMigrationRules.CurrentCatalogRevision));
    }

    [Theory]
    [InlineData(103, "Ready")]
    [InlineData(103, "Movement")]
    [InlineData(103, "Rest")]
    [InlineData(1144, "Ready")]
    [InlineData(1144, "Movement")]
    [InlineData(1144, "Rest")]
    [InlineData(307, "Ready")]
    [InlineData(307, "Movement")]
    [InlineData(307, "Rest")]
    public void Revision99RetainsFixedPositionAndLinkedCheckpointsBeforeInitialization(int id, string phase)
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
        Exercise[] catalog = Catalog();
        catalog.Single(e => e.Id == id).Score = 100000;
        var legacy = WorkoutCatalogPolicy.ForWorkout(catalog, 99).ToArray();
        var service = new ExerciseSessionService(legacy, new Random(1), () => now);
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 60, WorkoutModifiers.Floor | WorkoutModifiers.Elevation | WorkoutModifiers.UpperBodyClothing);
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
        state.CatalogRevision = 99;
        state.ActiveCatalogRevision = 0; // Legacy records infer the workout revision at every command boundary.
        state.KeptExerciseRootIdsBySelectionGroupId[current.SelectionKey] = [id];
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string prior = JsonSerializer.Serialize(state);
        string history = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        service = new ExerciseSessionService(catalog, new Random(3), () => now.AddDays(1));
        Assert.Equal(rounds, service.GetActiveGroups(state));
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        Assert.Equal(prior, JsonSerializer.Serialize(state));
        service.RestoreAfterReopen(state);
        Assert.Equal(99, state.ActiveCatalogRevision);
        Assert.Equal(rounds, service.GetActiveGroups(state));
        Assert.Equal(history, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(legacy.Single(e => e.Id == id).SequenceBlocks,
            service.GetSelectedExercise(state, current).SequenceBlocks);
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
        int minimum = service.GetMinimumActiveWorkoutMinutes(state);
        service.ResizeActiveWorkout(state, minimum);
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(minimum < 60 ? 1 : 0, state.ActiveWorkoutSession!.DurationChanges.Count);
    }
}
