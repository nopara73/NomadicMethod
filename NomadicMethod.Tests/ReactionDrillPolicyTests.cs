using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ReactionDrillPolicyTests
{
    private static ReactionPlan Plan() => new()
    {
        Protocol = "ContextChoice",
        Actions = Enumerable.Range(0, 4).Select(i => new ReactionAction
            { Label = $"Action {i}", DurationSeconds = 1 }).ToArray(),
    };

    private static ReactionPlan StopPlan() => new()
    {
        Protocol = "StopOnly",
        Actions = [new ReactionAction { Label = "Known action", DurationSeconds = 1 }],
    };

    [Fact]
    public void SharedStopOnlyTraceHasPredictableStartsAndNeverRevealsCancelledResponse()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "reaction-stop-only-trace.json")));
        double[] values = fixture.RootElement.GetProperty("random").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        int index = 0;
        var drill = new ReactionDrill(StopPlan(), () => values[index++]);
        foreach (JsonElement step in fixture.RootElement.GetProperty("steps").EnumerateArray())
        {
            long now = step.GetProperty("now").GetInt64();
            Assert.Equal(step.GetProperty("changed").GetBoolean(), drill.Advance(now, 45000 - now));
            Assert.Equal(step.GetProperty("phase").GetString(), drill.Phase);
            Assert.Equal(step.GetProperty("number").GetInt32(), drill.Number);
            Assert.Equal(step.GetProperty("deadline").GetInt64(), drill.Deadline);
            Assert.Equal(step.GetProperty("cue").GetString(), drill.Cue);
            Assert.Equal(step.GetProperty("demoActionIndex").GetInt32(), drill.DemoActionIndex);
            Assert.False(drill.Reverse);
        }
    }

    [Fact]
    public void StopOnlyBufferingDelayedCallbacksRestartAndFinalTrialBudgetFailClosed()
    {
        var drill = new ReactionDrill(StopPlan(), () => 0.9);
        Assert.False(drill.Advance(0, 45000, false));
        Assert.Equal("Idle", drill.Phase);
        drill.Advance(0, 45000);
        Assert.False(drill.Advance(5000, 40000, false));
        Assert.Equal(3, drill.Number);
        Assert.True(drill.Advance(5000, 40000));
        Assert.Equal(2, drill.Number);
        Assert.Equal(6000, drill.Deadline);
        Assert.Equal(-1, drill.DemoActionIndex);
        var reopened = new ReactionDrill(StopPlan(), () => 0);
        reopened.Advance(5000, 40000);
        Assert.Equal(3, reopened.Number);
        Assert.Equal(6000, reopened.Deadline);
        var shortTrial = new ReactionDrill(StopPlan(), () => 0);
        shortTrial.Advance(0, 5649);
        Assert.Equal("Complete", shortTrial.Phase);
        Assert.Equal(-1, shortTrial.DemoActionIndex);
        var delayed = new ReactionDrill(StopPlan(), () => 0.9);
        delayed.Advance(0, 45000); delayed.Advance(1000, 44000); delayed.Advance(2000, 43000);
        delayed.Advance(3000, 2199);
        Assert.Equal("Complete", delayed.Phase);
        Assert.Equal(-1, delayed.DemoActionIndex);
    }

    [Fact]
    public void SharedTracePreservesContextDecisionWindowAndDelayedFeedback()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "reaction-drill-trace.json")));
        double[] values = fixture.RootElement.GetProperty("random").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        int index = 0;
        var drill = new ReactionDrill(Plan(), () => values[index++]);
        foreach (JsonElement step in fixture.RootElement.GetProperty("steps").EnumerateArray())
        {
            long now = step.GetProperty("now").GetInt64();
            drill.Advance(now, 45000 - now);
            Assert.Equal(step.GetProperty("phase").GetString(), drill.Phase);
            Assert.Equal(step.GetProperty("reverse").GetBoolean(), drill.Reverse);
            Assert.Equal(step.GetProperty("number").GetInt32(), drill.Number);
            Assert.Equal(step.GetProperty("actionIndex").GetInt32(), drill.ActionIndex);
            Assert.Equal(step.GetProperty("deadline").GetInt64(), drill.Deadline);
            Assert.Equal(drill.Phase == "Feedback" ? drill.ActionIndex : -1, drill.DemoActionIndex);
        }
    }
    [Fact]
    public void BufferingAndPauseNeverSkipToAnAnswerOrCreditTime()
    {
        var drill = new ReactionDrill(Plan(), () => 0);
        Assert.False(drill.Advance(0, 45000, false));
        Assert.Equal("Idle", drill.Phase);
        drill.Advance(0, 45000);
        Assert.False(drill.Advance(5000, 40000, false));
        Assert.Equal("Ready", drill.Phase);
        var resumed = new ReactionDrill(Plan(), () => 0);
        resumed.Advance(5000, 40000);
        Assert.Equal("Ready", resumed.Phase);
        Assert.Equal(6200, resumed.Deadline);
        Assert.Equal(0, resumed.Number);
    }
    [Fact]
    public void NearEndRequiresTimeForACompleteDecisionActionAndReset()
    {
        var drill = new ReactionDrill(Plan(), () => 0);
        drill.Advance(0, 4049);
        Assert.Equal("Complete", drill.Phase);
        var delayed = new ReactionDrill(Plan(), () => 0);
        delayed.Advance(0, 45000);
        delayed.Advance(1200, 2500);
        Assert.Equal("Complete", delayed.Phase);
        Assert.Equal(-1, delayed.DemoActionIndex);
    }
    [Fact]
    public void CanonicalAssetResolvesTwentyOneProtocolsAcrossFivePhysicalFamilies()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() } };
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), options)!;
        using Stream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "reaction_drills.json"));
        IReadOnlyDictionary<int, ReactionPlan> plans = ReactionDrill.Load(stream, catalog);
        Assert.Equal(21, plans.Count);
        Assert.Equal(5, plans.Values.Select(p => p.FamilyId).Distinct().Count());
        foreach (var family in plans.Values.GroupBy(p => p.FamilyId))
        {
            Assert.Equal(family.Key == "arm" ? new[] { 1, 2, 4, 4, 1 } : new[] { 2, 4, 4, 1 },
                family.Select(p => p.Actions.Length));
            Assert.Single(family.Select(p => catalog.Single(e => e.Id == p.ExerciseId).SessionMovementId).Distinct());
        }
    }
    [Fact]
    public void MissingPlansAndResponseMuscleMismatchesFailClosed()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() } };
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), options)!;
        using var empty = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "{\"version\":1,\"families\":[],\"plans\":[]}"));
        Assert.Contains("Missing reaction plan", Assert.Throws<InvalidDataException>(() => ReactionDrill.Load(empty, catalog)).Message);
        JsonNode document = JsonNode.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "reaction_drills.json")))!;
        document["families"]![0]!["actions"]![0]!["sourceId"] = 151;
        using var mismatch = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(document.ToJsonString()));
        Assert.Contains("Invalid reaction training claim", Assert.Throws<InvalidDataException>(() => ReactionDrill.Load(mismatch, catalog)).Message);
        IReadOnlyList<Exercise> old = WorkoutCatalogPolicy.ForWorkout(catalog, 87);
        Assert.Equal(864, old.Count);
        Assert.DoesNotContain(old, item => WorkoutCatalogPolicy.FitnessCoverageExerciseIds.Contains(item.Id));
        Assert.Equal(895, WorkoutCatalogPolicy.ForWorkout(catalog, 88).Count);
        Assert.Equal(901, WorkoutCatalogPolicy.ForWorkout(catalog, 89).Count);
        Assert.Equal(917, WorkoutCatalogPolicy.ForWorkout(catalog, 90).Count);
    }
}
