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
                int[] best = entry.GetProperty("bestLevels").TryGetProperty(FitnessProfilePolicy.Dimensions[i], out var bestLevels)
                    ? bestLevels.Deserialize<int[]>()! : [];
                Assert.Equal(best, profile[i].BestLevels);
                JsonElement[] rows = entry.GetProperty("evidence").TryGetProperty(FitnessProfilePolicy.Dimensions[i], out var evidence)
                    ? evidence.EnumerateArray().ToArray() : [];
                Assert.Equal(rows.Length, profile[i].Contributions.Count);
                for (int j = 0; j < rows.Length; j++)
                {
                    var actual = profile[i].Contributions[j]; var row = rows[j];
                    Assert.Equal(row[0].GetInt32(), actual.MovementId);
                    Assert.Equal(row[1].GetInt32(), actual.ExerciseId);
                    Assert.Equal(row[2].Deserialize<int[]>(), actual.RatedLevels);
                    Assert.Equal(row[3].GetString() switch
                    {
                        "tick" => ExerciseOutcome.Tick,
                        "easy" => ExerciseOutcome.TooEasy,
                        _ => ExerciseOutcome.TooHard,
                    }, actual.Feedback);
                    Assert.Equal(row[4].Deserialize<int[]>(), actual.SupportedLevels);
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
    public void SmallDifficultyChangesRemainFractionalWithoutChangingDiscreteSelectionZones()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Assets", "fitness-precision-cases.json")));
        foreach (var entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[][] sets = entry.GetProperty("levelSets").Deserialize<int[][]>()!;
            Exercise[] exercises = sets.Select((_, index) => new Exercise
            {
                Id = index + 1, SessionMovementId = index + 1, Name = "movement " + (index + 1),
                Video = "", PrimaryCanonicalGroup = default, SecondaryCanonicalGroups = [],
                Practice = "", MotionProfile = "", Mode = default, Presentation = default,
                HoldFramePercent = 0, SideSequence = default, OnlyFeetTouchGround = true,
                ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "", Silent = true,
            }).ToArray();
            var ratings = exercises.ToDictionary(exercise => exercise.Id,
                exercise => FitnessProfilePolicy.Dimensions.Select(_ => sets[exercise.Id - 1]).ToArray());
            var state = new WorkoutState { ActiveWorkoutSession = new() { SessionId = 1 } };
            string[] responses = entry.GetProperty("feedback").Deserialize<string[]>()!;
            for (int index = 0; index < exercises.Length; index++)
                state.ActiveWorkoutSession.Decisions.Add(new()
                {
                    RootExerciseId = exercises[index].Id, DecidedAtUnixMilliseconds = index + 1,
                    Outcome = responses[index] switch
                    {
                        "easy" => ExerciseOutcome.TooEasy, "hard" => ExerciseOutcome.TooHard, _ => ExerciseOutcome.Tick,
                    },
                });
            string before = JsonSerializer.Serialize(state);
            var profile = FitnessProfilePolicy.Inspect(exercises, state, ratings);
            Assert.All(profile, axis => Assert.Equal(entry.GetProperty("mean").GetDouble(), axis.Mean));
            Assert.All(FitnessSelectionPolicy.Zones(profile), zone =>
                Assert.Equal(entry.GetProperty("bestLevels").Deserialize<int[]>(), zone));
            Assert.Equal(before, JsonSerializer.Serialize(state));
        }
    }

    [Fact]
    public void LevelLabelsRetainNoncontiguousGaps()
    {
        Assert.Equal("1, 3–5", FitnessProfilePolicy.FormatLevels([1, 3, 4, 5]));
        Assert.Equal("2–3", FitnessProfilePolicy.FormatLevels([2, 3]));
        Assert.Equal("5", FitnessProfilePolicy.FormatLevels([5]));
    }

    [Fact]
    public void DifficultyResponsesChangeSelectionZonesWithoutRewritingLegacyFavourites()
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
        Assert.NotEqual(FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(catalog, first, ratings)),
            FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(catalog, second, ratings)));
    }
}
