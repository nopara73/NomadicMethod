using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ExecutionProtocolTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void CorrectedProtocolsUseCompleteSidesAndDirections()
    {
        Exercise[] catalog = LoadCatalog();
        using JsonDocument cases = LoadCases();
        Assert.Equal(WorkoutCatalogPolicy.ExecutionCorrectionExerciseIds.Order(),
            cases.RootElement.EnumerateArray().Select(c => c.GetProperty("id").GetInt32()).Order());
        foreach (JsonElement c in cases.RootElement.EnumerateArray())
        {
            int id = c.GetProperty("id").GetInt32();
            Exercise e = catalog.Single(e => e.Id == id);
            Assert.Equal(c.GetProperty("side").GetString(), e.SideSequence.ToString());
            Assert.Equal(c.GetProperty("direction").GetString(), e.DirectionSequence.ToString());
            Assert.Equal(c.GetProperty("blocks").GetInt32(), e.SequenceBlocks.Length);
            Assert.All(e.SequenceBlocks, block => Assert.Equal(id, block.ExerciseId));
            if (e.DirectionSequence != ExerciseDirectionSequence.None)
                for (int index = 0; index < e.SequenceBlocks.Length; index += 2)
                {
                    Assert.Equal(e.SequenceBlocks[index].SideCue, e.SequenceBlocks[index + 1].SideCue);
                    Assert.NotEqual(e.SequenceBlocks[index].DirectionCue, e.SequenceBlocks[index + 1].DirectionCue);
                    Assert.Equal(ExerciseSequenceMediaSegment.FirstDirection, e.SequenceBlocks[index].MediaSegment);
                    Assert.Equal(ExerciseSequenceMediaSegment.SecondDirection, e.SequenceBlocks[index + 1].MediaSegment);
                }
            if (e.SideSequence.UsesTimedSides())
            {
                Assert.Equal(e.SequenceBlocks.Length / 2, e.SequenceBlocks.Count(b => b.MirrorMedia));
                Assert.Equal(2, e.SequenceBlocks.Select(b => b.SideCue).Distinct().Count());
            }
        }
        Exercise ankle = catalog.Single(e => e.Id == 1299);
        Assert.Equal(new[] { ExerciseSequenceDirectionCue.Clockwise, ExerciseSequenceDirectionCue.Counterclockwise,
            ExerciseSequenceDirectionCue.Counterclockwise, ExerciseSequenceDirectionCue.Clockwise },
            ankle.SequenceBlocks.Select(b => b.DirectionCue));
    }

    [Theory]
    [InlineData(81)]
    [InlineData(82)]
    public void FrozenCatalogRetainsOriginalSingleBlockAndFootage(int revision)
    {
        Exercise[] catalog = LoadCatalog();
        IReadOnlyList<Exercise> old = WorkoutCatalogPolicy.ForWorkout(catalog, revision);
        using JsonDocument cases = LoadCases();
        foreach (JsonElement c in cases.RootElement.EnumerateArray())
        {
            int id = c.GetProperty("id").GetInt32();
            if (revision < 82 && id >= 1294) continue;
            Exercise e = old.Single(e => e.Id == id);
            Assert.Equal(c.GetProperty("previousSide").GetString(), e.SideSequence.ToString());
            Assert.Equal(ExerciseDirectionSequence.None, e.DirectionSequence);
            ExerciseSequenceBlock block = Assert.Single(e.SequenceBlocks);
            Assert.Equal(id, block.ExerciseId);
            Assert.Equal(ExerciseSequenceSideCue.None, block.SideCue);
            Assert.Equal(ExerciseSequenceDirectionCue.None, block.DirectionCue);
            Assert.Equal(ExerciseSequenceMediaSegment.Full, block.MediaSegment);
            Assert.False(block.MirrorMedia);
            Assert.Equal($"exercise_videos/revision82/exercise_{id:D4}.mp4", e.Video);
        }
        Assert.Equal(4, catalog.Single(e => e.Id == 1299).SequenceBlocks.Length);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("movement")]
    [InlineData("rest")]
    public void ExistingAnkleWorkoutRetainsItsCheckpointAndHistory(string phase)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "legacy-execution-session.json")));
        JsonElement root = fixture.RootElement;
        WorkoutState state = JsonSerializer.Deserialize<WorkoutState>(root.GetProperty("state"), JsonOptions)!;
        string[] roundIds = root.GetProperty("roundIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
        int[] selectedIds = root.GetProperty("selectedIds").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        string currentId = root.GetProperty("currentId").GetString()!;
        DateTimeOffset now = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        Exercise[] catalog = LoadCatalog();
        catalog.Single(e => e.Id == 1299).Score = 100000;
        var service = new ExerciseSessionService(catalog, new Random(4), () => now);
        service.Initialize(state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        Assert.Equal(currentId, current.Id);
        Assert.Equal(1299, service.GetSelectedExercise(state, current).Id);
        if (phase == "movement") service.BeginMovement(state, current, 28000, now.AddSeconds(28).ToUnixTimeMilliseconds());
        if (phase == "rest") service.BeginRest(state, current, now.AddSeconds(15).ToUnixTimeMilliseconds());
        string history = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        string archived = JsonSerializer.Serialize(state.WorkoutHistory);
        string scores = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        bool light = state.ActiveWorkoutIsLightDay;
        for (int reopen = 0; reopen < 2; reopen++)
        {
            now = now.AddDays(365);
            state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state), JsonOptions)!;
            service = new ExerciseSessionService(catalog, new Random(1), () => now);
            service.RestoreAfterReopen(state);
            Assert.Equal(roundIds, service.GetActiveGroups(state).Select(g => g.Id));
            Assert.Equal(selectedIds, service.GetActiveGroups(state).Select(g => service.GetSelectedExercise(state, g).Id));
            Assert.Equal(currentId, service.GetNextGroup(state)!.Id);
            Assert.Equal(82, state.ActiveCatalogRevision);
            Assert.Equal(history, JsonSerializer.Serialize(state.ActiveWorkoutSession));
            Assert.Equal(archived, JsonSerializer.Serialize(state.WorkoutHistory));
            Assert.Equal(scores, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
            Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
            Assert.Equal(light, state.ActiveWorkoutIsLightDay);
            Exercise selected = service.GetSelectedExercise(state, service.GetNextGroup(state)!);
            Assert.Single(selected.SequenceBlocks);
            Assert.Contains("revision82/", selected.Video);
            Assert.Equal(100000, selected.Score);
            if (phase == "movement")
            {
                Assert.True(state.PendingMovementPausedByUser);
                Assert.Equal(28000, state.PendingMovementMillisecondsRemaining);
            }
            if (phase == "rest")
            {
                Assert.True(state.PendingRestPausedByUser);
                Assert.Equal(15000, state.PendingRestMillisecondsRemaining);
            }
        }
    }

    [Fact]
    public void BodyCircleVariantKeepsOneIdentityAndDoesNotEnterOlderWorkouts()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "body-circle-admission.json")));
        JsonElement c = document.RootElement;
        int id = c.GetProperty("exerciseId").GetInt32();
        Exercise[] catalog = LoadCatalog();
        Exercise exercise = catalog.Single(e => e.Id == id);
        Assert.Equal(WorkoutCatalogPolicy.BodyCircleExerciseId, id);
        Assert.Equal(c.GetProperty("sessionMovementId").GetInt32(), exercise.SessionMovementId);
        Assert.Equal(c.GetProperty("side").GetString(), exercise.SideSequence.ToString());
        Assert.Equal(c.GetProperty("direction").GetString(), exercise.DirectionSequence.ToString());
        Assert.Equal(2, exercise.SequenceBlocks.Length);
        Assert.All(exercise.SequenceBlocks, b => Assert.Equal(ExerciseSequenceSideCue.None, b.SideCue));
        Assert.Equal(catalog.Single(e => e.Id == 1299).SessionMovementId, exercise.SessionMovementId);
        foreach (JsonElement minutes in c.GetProperty("shortMinutes").EnumerateArray())
            Assert.Equal(1, WorkoutSequencePolicy.GetBlockCount(exercise, minutes.GetInt32()));
        foreach (JsonElement minutes in c.GetProperty("completeMinutes").EnumerateArray())
            Assert.Equal(2, WorkoutSequencePolicy.GetBlockCount(exercise, minutes.GetInt32()));
        foreach (JsonElement revision in c.GetProperty("preservedRevisions").EnumerateArray())
            Assert.DoesNotContain(WorkoutCatalogPolicy.ForWorkout(catalog, revision.GetInt32()), e => e.Id == id);
        Assert.Contains(WorkoutCatalogPolicy.ForWorkout(catalog,
            c.GetProperty("introducedRevision").GetInt32()), e => e.Id == id);
        Assert.Equal(4, catalog.Single(e => e.Id == 1299).SequenceBlocks.Length);
    }

    private static Exercise[] LoadCatalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), JsonOptions)!;
    private static JsonDocument LoadCases() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "execution-protocol-cases.json")));
}
