using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class CompletionProfileTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 4, 0, 0, TimeSpan.Zero);
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
    private static WorkoutState Reload(WorkoutState state) =>
        JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
    private static IReadOnlyDictionary<int, int[][]> Ratings()
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        return FitnessProfilePolicy.Load(source);
    }

    [Fact]
    public void SharedFractionalComparisonContract()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Assets", "completion-profile-cases.json")));
        foreach (var entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var before = entry.GetProperty("before").Deserialize<double[]>();
            var after = entry.GetProperty("after").Deserialize<double[]>();
            var rows = CompletionProfilePolicy.Changes(before, after);
            Assert.Equal(entry.GetProperty("expected").Deserialize<string[][]>(),
                rows.Select(row => new[] { row.Label, row.Before, row.After }).ToArray());
            Assert.All(rows, row => Assert.NotEqual(row.Before, row.After));
            Assert.All(rows.Where(row => row.Before == "—" || row.After == "—"),
                row => Assert.Contains("unknown", row.Description));
        }
    }

    [Fact]
    public void SnapshotsSurvivePreparationMovementDeathEditingFinalFeedbackAndDone()
    {
        Exercise[] catalog = Catalog();
        var ratings = Catalog().ToDictionary(e => e.Id, _ => FitnessProfilePolicy.Dimensions.Select((_, i) => i == 1 ? new[] { 3 } : Array.Empty<int>()).ToArray());
        var service = new ExerciseSessionService(catalog, new Random(5), () => Now, fitnessRatings: ratings);
        var state = new WorkoutState();
        service.Initialize(state);
        service.PrepareWorkout(state, 3);
        Assert.Null(state.ActiveFitnessProfileBaseline);
        service.ActivatePreparedWorkout(state);
        double[] baseline = [.. state.ActiveFitnessProfileBaseline!];
        Assert.Equal(FitnessProfilePolicy.Calculate(catalog, state, ratings), baseline);
        long sessionId = state.ActiveWorkoutSession!.SessionId;

        WorkoutGroup next = service.GetNextGroup(state)!;
        service.BeginMovement(state, next, 45_000, Now.ToUnixTimeMilliseconds() + 45_000);
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(baseline, state.ActiveFitnessProfileBaseline);
        Assert.Equal(sessionId, state.ActiveWorkoutSession!.SessionId);
        Assert.True(state.PendingMovementPausedByUser);
        service.PauseMovement(state, service.GetNextGroup(state)!, 30_000, true);
        service.ResizeActiveWorkout(state, 5);
        service.ReconfigureActiveWorkout(state, state.ActiveWorkoutModifiers ^ WorkoutModifiers.Silence,
            service.GetNextGroup(state)!.Id);
        Assert.Equal(baseline, state.ActiveFitnessProfileBaseline);
        state = Reload(state);
        service.Initialize(state);
        Assert.Equal(baseline, state.ActiveFitnessProfileBaseline);

        while (service.GetNextGroup(state) is { } group)
        {
            service.BeginRest(state, group, Now.ToUnixTimeMilliseconds() + 15_000);
            if (service.IsIntermediateSequenceBlock(state, group)) service.AdvanceSequence(state, group);
            else service.RecordOutcomeWithScoreUpdates(state, group, ExerciseOutcome.TooEasy);
            service.ClearPendingRest(state);
        }
        Assert.True(state.WorkoutCompleted);
        double[] final = [.. state.CompletionFitnessProfileSnapshot!];
        Assert.Contains(final, value => value > 0);
        Assert.NotEmpty(CompletionProfilePolicy.Changes(baseline, final));
        Assert.Equal(FitnessProfilePolicy.Calculate(catalog, state, ratings), final);
        Assert.Single(state.WorkoutHistory);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        state = Reload(state);
        // Reopening must use the saved comparison even if ratings change in an app update.
        service = new ExerciseSessionService(catalog, new Random(2), () => Now.AddDays(3));
        service.Initialize(state);
        Assert.Equal(baseline, state.ActiveFitnessProfileBaseline);
        Assert.Equal(final, state.CompletionFitnessProfileSnapshot);
        Assert.Equal(history, JsonSerializer.Serialize(state.WorkoutHistory));
        service.AcknowledgeCompletion(state);
        Assert.Null(state.ActiveFitnessProfileBaseline);
        Assert.Null(state.CompletionFitnessProfileSnapshot);
        Assert.Equal(final, FitnessProfilePolicy.Calculate(catalog, state, ratings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PreviousEvidenceBaselinesAreNotComparedWithRangeFeedback(int version)
    {
        var service = new ExerciseSessionService(Catalog(), new Random(4), () => Now, fitnessRatings: Ratings());
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 3);
        state.FitnessProfileEvidenceVersion = version;
        state.ActiveFitnessProfileBaseline = Enumerable.Repeat(4d, 10).ToArray();
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(4d, state.ActiveFitnessProfileBaseline![0]); // Preserve saved state until final feedback.
        while (service.GetNextGroup(state) is { } group)
        {
            if (service.IsIntermediateSequenceBlock(state, group)) service.AdvanceSequence(state, group);
            else service.RecordOutcomeWithScoreUpdates(state, group, ExerciseOutcome.TooEasy);
        }
        Assert.Null(state.ActiveFitnessProfileBaseline);
        Assert.Equal(FitnessProfilePolicy.EvidenceVersion, state.FitnessProfileEvidenceVersion);
        Assert.Empty(CompletionProfilePolicy.Changes(state.ActiveFitnessProfileBaseline, state.CompletionFitnessProfileSnapshot));
    }

    [Fact]
    public void LegacyAndExplicitEndDoNotInventComparisons()
    {
        var service = new ExerciseSessionService(Catalog(), new Random(4), () => Now, fitnessRatings: Ratings());
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 3);
        state.ActiveFitnessProfileBaseline = null; // An unfinished older installation.
        state = Reload(state);
        service.Initialize(state);
        Assert.Null(state.ActiveFitnessProfileBaseline);
        while (service.GetNextGroup(state) is { } group) service.RecordOutcome(state, group, true);
        Assert.Empty(CompletionProfilePolicy.Changes(state.ActiveFitnessProfileBaseline, state.CompletionFitnessProfileSnapshot));
        service.AcknowledgeCompletion(state);
        service.StartWorkout(state, 3);
        service.EndActiveWorkout(state);
        Assert.False(state.WorkoutCompleted);
        Assert.Null(state.ActiveFitnessProfileBaseline);
        Assert.Null(state.CompletionFitnessProfileSnapshot);
        Assert.Equal(WorkoutSessionStatus.Interrupted, state.WorkoutHistory[^1].Status);
    }
}
