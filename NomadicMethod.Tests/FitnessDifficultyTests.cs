using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class FitnessDifficultyTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void SharedDifficultyEvidenceCasesPreserveStateAndMatchWeb()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Assets", "fitness-difficulty-cases.json")));
        foreach (var entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            Exercise[] exercises = entry.GetProperty("exercises").EnumerateArray().Select(row => new Exercise
            {
                Id = row.GetProperty("id").GetInt32(),
                SessionMovementId = row.GetProperty("sessionMovementId").GetInt32(),
                Name = "movement " + row.GetProperty("id").GetInt32(), Video = "", PrimaryCanonicalGroup = default,
                SecondaryCanonicalGroups = [], Practice = "", MotionProfile = "",
                Mode = default, Presentation = default, HoldFramePercent = 0,
                SideSequence = default, OnlyFeetTouchGround = true, ShoeAgnostic = true,
                MaxSpaceMeters = 2, Equipment = "", Silent = true,
            }).ToArray();
            var ratings = entry.GetProperty("exercises").EnumerateArray().ToDictionary(
                row => row.GetProperty("id").GetInt32(), row => row.GetProperty("levels")
                    .Deserialize<int[][]>()!);
            ratings[999] = FitnessProfilePolicy.Dimensions.Select(_ => new[] { 5 }).ToArray();
            // Web's serialized enum spellings map to the same native outcomes.
            var state = JsonSerializer.Deserialize<WorkoutState>(entry.GetProperty("state").GetRawText()
                .Replace("\"easy\"", "\"TooEasy\"").Replace("\"hard\"", "\"TooHard\""), Options)!;
            string before = JsonSerializer.Serialize(state);
            double[] expected = entry.GetProperty("expected").Deserialize<double[]>()!;
            var profile = FitnessProfilePolicy.Inspect(exercises, state, ratings);
            Assert.Equal(expected, FitnessProfilePolicy.Calculate(exercises, state, ratings));
            for (int i = 0; i < profile.Length; i++)
            {
                Assert.Equal(expected[i], profile[i].Mean);
                JsonElement[] rows = entry.GetProperty("evidence").TryGetProperty(FitnessProfilePolicy.Dimensions[i], out var evidence)
                    ? evidence.EnumerateArray().ToArray() : [];
                Assert.Equal(rows.Length, profile[i].Contributions.Count);
                for (int j = 0; j < rows.Length; j++)
                {
                    var actual = profile[i].Contributions[j]; var row = rows[j];
                    Assert.Equal(row[0].GetInt32(), actual.MovementId);
                    Assert.Equal(row[1].GetInt32(), actual.ExerciseId);
                    Assert.Equal(row[2].GetInt32(), actual.RatedLevel);
                    Assert.Equal(row[3].GetString() == "easy" ? ExerciseOutcome.TooEasy : ExerciseOutcome.TooHard, actual.Feedback);
                    Assert.Equal(row[4].GetInt32(), actual.Level);
                    Assert.Equal("movement " + actual.ExerciseId, actual.Name);
                }
            }
            Assert.Equal(JsonSerializer.Serialize(profile), JsonSerializer.Serialize(
                FitnessProfilePolicy.Inspect(exercises.Reverse().ToArray(), state, ratings)));
            Assert.All(FitnessProfilePolicy.Inspect(exercises, state, new Dictionary<int, int[][]>()), axis => Assert.Equal(0, axis.Mean));
            Assert.Equal(before, JsonSerializer.Serialize(state));
        }
    }

    [Fact]
    public void DifficultyResponsesDoNotChangeThePlannerInputOrFreshSelection()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        var ratings = FitnessProfilePolicy.Load(source);
        Exercise exercise = catalog.First(e => ratings[e.Id].SelectMany(set => set).DefaultIfEmpty(0).Max() == 4 &&
            ratings[e.Id].Any(set => set is [4]));
        var first = new WorkoutState { LastKeptExerciseIds = [exercise.Id] };
        var second = new WorkoutState { LastKeptExerciseIds = [exercise.Id], WorkoutHistory =
            [new() { SessionId = 1, Status = WorkoutSessionStatus.Interrupted, Decisions =
                [new() { RootExerciseId = exercise.Id, Outcome = ExerciseOutcome.TooHard, DecidedAtUnixMilliseconds = 1 }] }] };
        Assert.NotEqual(FitnessProfilePolicy.Calculate(catalog, first, ratings), FitnessProfilePolicy.Calculate(catalog, second, ratings));
        Assert.Equal(FitnessProfilePolicy.CalculateFavourites(catalog, first, ratings), FitnessProfilePolicy.CalculateFavourites(catalog, second, ratings));
        var now = DateTimeOffset.FromUnixTimeMilliseconds(100000);
        new ExerciseSessionService(catalog, new Random(42), () => now, fitnessRatings: ratings).StartWorkout(first, 3);
        new ExerciseSessionService(catalog, new Random(42), () => now, fitnessRatings: ratings).StartWorkout(second, 3);
        Assert.Equal(first.SelectedExerciseIds.OrderBy(entry => entry.Key), second.SelectedExerciseIds.OrderBy(entry => entry.Key));
    }
}
