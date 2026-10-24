using System.Text.Json;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ExerciseFeedbackPolicyTests
{
    [Fact]
    public void SharedGestureTracesPermitChangingDirectionWithoutFlicker()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Assets", "exercise-feedback-cases.json")));
        foreach (var trace in fixture.RootElement.GetProperty("traces").EnumerateArray())
        {
            ExerciseOutcome? direction = null;
            foreach (var point in trace.GetProperty("points").EnumerateArray())
            {
                direction = ExerciseFeedbackPolicy.Direction(point[0].GetDouble(), point[1].GetDouble(), direction);
                var expected = point[2].GetString() switch {
                    "x" => ExerciseOutcome.X, "tick" => ExerciseOutcome.Tick,
                    "easy" => ExerciseOutcome.TooEasy, "hard" => ExerciseOutcome.TooHard,
                    _ => (ExerciseOutcome?)null,
                };
                Assert.Equal(expected, direction);
            }
        }
        foreach (var pair in fixture.RootElement.GetProperty("thresholds").EnumerateArray())
            Assert.Equal(pair[1].GetDouble(), ExerciseFeedbackPolicy.Threshold(pair[0].GetDouble()), 8);
        Assert.False(ExerciseFeedbackPolicy.IsResponse(ExerciseOutcome.Neutral));
    }
}
