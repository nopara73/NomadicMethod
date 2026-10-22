using System.Text.Json;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class FitnessProfileTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Fact]
    public void SharedCasesProduceTheSameProfileWithoutChangingWorkoutState()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "fitness-profile-cases.json")));
        foreach (JsonElement entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            Exercise[] exercises = entry.GetProperty("exercises").EnumerateArray().Select(row => new Exercise
            {
                Id = row.GetProperty("id").GetInt32(),
                SessionMovementId = row.GetProperty("sessionMovementId").GetInt32(),
                Name = "fixture", Video = "", PrimaryCanonicalGroup = default,
                SecondaryCanonicalGroups = [], Practice = "", MotionProfile = "",
                Mode = default, Presentation = default, HoldFramePercent = 0,
                SideSequence = default, OnlyFeetTouchGround = true, ShoeAgnostic = true,
                MaxSpaceMeters = 2, Equipment = "", Silent = true,
            }).ToArray();
            var ratings = entry.GetProperty("exercises").EnumerateArray().ToDictionary(
                row => row.GetProperty("id").GetInt32(),
                row => row.GetProperty("levels").EnumerateArray().Select(set =>
                    set.EnumerateArray().Select(level => level.GetInt32()).ToArray()).ToArray());
            WorkoutState state = JsonSerializer.Deserialize<WorkoutState>(
                entry.GetProperty("state").GetRawText(), Options)!;
            string before = JsonSerializer.Serialize(state);
            double[] expected = entry.GetProperty("expected").EnumerateArray()
                .Select(value => value.GetDouble()).ToArray();
            double[] actual = FitnessProfilePolicy.Calculate(exercises, state, ratings);
            Assert.True(expected.SequenceEqual(actual), entry.GetProperty("name").GetString());
            Assert.Equal(before, JsonSerializer.Serialize(state));
        }
    }

    [Fact]
    public void RuntimeRatingsCoverEveryCatalogExercise()
    {
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        var ratings = FitnessProfilePolicy.Load(source);
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")));
        Assert.Equal(catalog.RootElement.GetArrayLength(), ratings.Count);
        Assert.All(catalog.RootElement.EnumerateArray(),
            exercise => Assert.True(ratings.ContainsKey(exercise.GetProperty("id").GetInt32())));
    }
}
