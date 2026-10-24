using System.Text.Json;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ExerciseFeedbackPolicyTests
{
    [Fact]
    public void StampMatchesApprovedPreviewIncludingTimeoutAndConfirmationHold()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Assets", "exercise-feedback-cases.json")));
        var timing = fixture.RootElement.GetProperty("stampTiming");
        Assert.Equal(ExerciseFeedbackPolicy.AutomaticMilliseconds, timing.GetProperty("automatic").GetInt32());
        Assert.Equal(ExerciseFeedbackPolicy.StampMilliseconds, timing.GetProperty("stamp").GetInt32());
        Assert.Equal(ExerciseFeedbackPolicy.RevealMilliseconds, timing.GetProperty("reveal").GetInt32());
        Assert.Equal(ExerciseFeedbackPolicy.ReducedMilliseconds, timing.GetProperty("reduced").GetInt32());
        Assert.Equal(ExerciseFeedbackPolicy.ReducedRevealMilliseconds, timing.GetProperty("reducedReveal").GetInt32());
        foreach (var row in fixture.RootElement.GetProperty("stamp").EnumerateArray())
        {
            var actual = ExerciseFeedbackPolicy.Stamp(row.GetProperty("t").GetDouble(), 360, 20, 30, .2);
            foreach (var property in row.EnumerateObject().Where(p => p.Name != "t"))
            {
                string name = char.ToUpperInvariant(property.Name[0]) + property.Name[1..];
                double value = (double)typeof(ExerciseFeedbackPolicy.StampPose).GetProperty(name)!.GetValue(actual)!;
                Assert.Equal(property.Value.GetDouble(), value, 8);
            }
        }
        foreach (var row in fixture.RootElement.GetProperty("automatic").EnumerateArray())
            Assert.Equal(row[1].GetDouble(), ExerciseFeedbackPolicy.AutomaticMehX(row[0].GetDouble(), 360), 8);
    }

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
        foreach (var row in fixture.RootElement.GetProperty("visuals").EnumerateArray())
        {
            var visual = ExerciseFeedbackPolicy.Visual(row[0].GetDouble(), row[1].GetDouble());
            Assert.Equal(row[2].GetDouble(), visual.Travel, 8);
            Assert.Equal(row[3].GetDouble(), visual.Size, 8);
            Assert.Equal(row[4].GetDouble(), visual.Others, 8);
        }
        Assert.False(ExerciseFeedbackPolicy.IsResponse(ExerciseOutcome.Neutral));
        foreach (var (key, response) in new[] { ("x", ExerciseOutcome.X), ("tick", ExerciseOutcome.Tick),
                     ("easy", ExerciseOutcome.TooEasy), ("hard", ExerciseOutcome.TooHard) })
            Assert.Equal(fixture.RootElement.GetProperty("symbols").GetProperty(key).GetString(),
                ExerciseFeedbackPolicy.Symbol(response));
    }
}
