using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;
using Xunit;

namespace NomadicMethod.Tests;

public sealed class WorkoutDurationPolicyTests
{
    [Fact]
    public void TimeBudgetsRemainIndependentOfAnatomicalResolutionAndAuditThresholds()
    {
        Assert.Equal(new[] { 3, 5, 7, 10, 15, 20, 30, 45, 60, 90 }, WorkoutDurationPolicy.Minutes);
        Assert.Same(WorkoutDurationPolicy.Minutes, ExerciseSessionService.SupportedWorkoutMinutes);
        Assert.Equal(10, WorkoutDurationPolicy.DefaultMinutes);
        Assert.Equal(3, WorkoutDurationPolicy.Normalize(int.MinValue));
        Assert.Equal(90, WorkoutDurationPolicy.Normalize(int.MaxValue));
        Assert.Equal(30, WorkoutDurationPolicy.Normalize(25));
        Assert.False(WorkoutDurationPolicy.IsSupported(25));
        Assert.Equal(30, MassGroupingTaxonomy.SupportedMinutes.Max());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(90)]
    public void EverySelectableDurationStartsACompletePlanAndSurvivesReopen(int minutes)
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() } })!;
        var service = new ExerciseSessionService(catalog, new Random(17));
        var state = new WorkoutState { LastWorkoutMinutes = minutes };
        service.Initialize(state);
        int selected = WorkoutDurationPolicy.Normalize(state.LastWorkoutMinutes);
        Assert.Equal(minutes, selected);
        service.StartWorkout(state, selected);
        Assert.Equal(minutes, service.GetActiveGroups(state).Count);
        string plan = JsonSerializer.Serialize(service.GetActiveGroups(state));
        string history = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        new ExerciseSessionService(catalog, new Random(23)).RestoreAfterReopen(state);
        Assert.Equal(minutes, state.ActiveWorkoutMinutes);
        Assert.Equal(minutes, state.LastWorkoutMinutes);
        Assert.Equal(plan, JsonSerializer.Serialize(service.GetActiveGroups(state)));
        Assert.Equal(history, JsonSerializer.Serialize(state.ActiveWorkoutSession));
    }
}
