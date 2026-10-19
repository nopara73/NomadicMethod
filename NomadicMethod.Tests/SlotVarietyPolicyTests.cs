using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class SlotVarietyPolicyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), JsonOptions)!;

    [Theory]
    [InlineData("Ready")]
    [InlineData("Movement")]
    [InlineData("Rest")]
    public void Revision81SessionsKeepOriginalSlotsRoundsAndCheckpoints(string phase)
    {
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "legacy-slot-variety-sessions.json")));
        DateTimeOffset now = new(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);
        foreach (JsonElement fixture in fixtures.RootElement.EnumerateArray())
        {
            WorkoutState state = fixture.GetProperty("state").Deserialize<WorkoutState>(JsonOptions)!;
            Exercise[] catalog = Catalog();
            var service = new ExerciseSessionService(catalog, new Random(7), () => now);
            WorkoutGroup current = service.GetNextGroup(state)!;
            if (phase == "Movement") service.BeginMovement(state, current, 28_000, now.ToUnixTimeMilliseconds() + 28_000);
            if (phase == "Rest") service.BeginRest(state, current, now.ToUnixTimeMilliseconds() + 14_000);
            string history = JsonSerializer.Serialize(state.WorkoutHistory);
            string session = JsonSerializer.Serialize(state.ActiveWorkoutSession);
            string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
            string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
            bool light = state.ActiveWorkoutIsLightDay;
            for (int reopen = 0; reopen < 2; reopen++)
            {
                state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
                now = now.AddDays(10);
                service.RestoreAfterReopen(state);
                WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
                Assert.Equal(fixture.GetProperty("rounds").EnumerateArray().Select(g => g.GetProperty("id").GetString()), rounds.Select(g => g.Id));
                Assert.Equal(fixture.GetProperty("selectedIds").EnumerateArray().Select(id => id.GetInt32()), rounds.Select(g => service.GetSelectedExercise(state, g).Id));
                Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
                Assert.Equal(81, state.ActiveCatalogRevision);
                Assert.Equal(session, JsonSerializer.Serialize(state.ActiveWorkoutSession));
                Assert.Equal(history, JsonSerializer.Serialize(state.WorkoutHistory));
                Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
                Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
                Assert.Equal(light, state.ActiveWorkoutIsLightDay);
                if (phase == "Movement")
                {
                    Assert.True(state.PendingMovementPausedByUser);
                    Assert.Equal(28_000, state.PendingMovementMillisecondsRemaining);
                }
                if (phase == "Rest") Assert.True(state.PendingRestPausedByUser);
            }
        }
    }

    [Fact]
    public void NewChoicesAndHipAnatomyApplyOnlyAfterTheFrozenSession()
    {
        Exercise[] catalog = Catalog();
        IReadOnlyList<Exercise> old = WorkoutCatalogPolicy.ForWorkout(catalog, 81);
        Assert.DoesNotContain(old, e => WorkoutCatalogPolicy.SlotVarietyExerciseIds.Contains(e.Id));
        Assert.Equal(new[] { CanonicalMuscleGroup.HipFlexors }, old.Single(e => e.Id == 906).SecondaryCanonicalGroups);
        Assert.Contains(CanonicalMuscleGroup.HipAbductors, catalog.Single(e => e.Id == 906).SecondaryCanonicalGroups);
        Assert.Equal(catalog.Length, WorkoutCatalogPolicy.ForWorkout(catalog, 82).Count);
        foreach (string id in WorkoutModifierPolicy.SlotVarietyCoverageExceptionGroupIds)
        {
            WorkoutGroup group = MassGroupingTaxonomy.SupportedMinutes.Select(MassGroupingTaxonomy.GetResolution).SelectMany(r => r.Groups).Single(g => g.Id == id);
            WorkoutModifiers profile = WorkoutModifiers.Insect | WorkoutModifiers.Shy;
            Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(group, profile, 81));
            Assert.False(WorkoutModifierPolicy.IsSelectionGroupAvailable(group, profile, 82));
            Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(group, WorkoutModifiers.None));
        }
    }

    [Fact]
    public void NewWorkoutLimitDoesNotInvalidateHistoricalDurations()
    {
        Assert.Equal(new[] { 3, 5, 7, 10, 15 }, ExerciseSessionService.NewWorkoutMinutes);
        Assert.Equal(15, ExerciseSessionService.NormalizeSetupMinutes(90));
        Assert.Equal(90, ExerciseSessionService.NormalizeLastWorkoutMinutes(90));
        Assert.True(ExerciseSessionService.IsValidWorkoutMinutes(90));
        Assert.Equal(new[] { 3, 5, 7, 10, 15, 20, 30 },
            ExerciseSessionService.GetWorkoutMinuteOptions(30));
        Assert.Equal(30, ExerciseSessionService.NormalizeSetupMinutes(90, 30));
        Assert.Equal(30, ExerciseSessionService.NormalizeSetupMinutes(30, 30));
    }

    [Fact]
    public void DurationFloorsMatchTheSharedAndroidWebContract()
    {
        using JsonDocument cases = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "slot-variety-cases.json")));
        Assert.Equal(ExerciseSessionService.NewWorkoutMinutes,
            cases.RootElement.EnumerateArray()
                .Select(row => row.GetProperty("minutes").GetInt32()).ToArray());
        foreach (JsonElement row in cases.RootElement.EnumerateArray())
        {
            Assert.Equal(row.GetProperty("required").GetInt32(),
                WorkoutModifierPolicy.GetMinimumExercisesPerPairStatePerGroup(
                    row.GetProperty("minutes").GetInt32()));
        }
    }
}
