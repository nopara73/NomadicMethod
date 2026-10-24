using System.Text.Json;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class FitnessProfileTests
{
    [Fact]
    public void RawProfileFractionsRemainPreciseAndExpertLevelExtendsBeyondTheWeb()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "fitness-profile-cases.json")));
        JsonElement scale = fixture.RootElement.GetProperty("chartScale");
        foreach (JsonElement entry in fixture.RootElement.GetProperty("meanLabels").EnumerateArray())
            Assert.Equal(entry.GetProperty("label").GetString(), FitnessProfilePolicy.FormatMean(entry.GetProperty("mean").GetDouble()));
        Assert.Equal(FitnessProfilePolicy.MaximumLevel, scale.GetProperty("maximumLevel").GetInt32());
        Assert.Equal(FitnessProfilePolicy.WebBoundaryLevel, scale.GetProperty("webBoundaryLevel").GetInt32());
        foreach (JsonElement entry in scale.GetProperty("cases").EnumerateArray())
            Assert.Equal(entry.GetProperty("radiusFraction").GetDouble(),
                FitnessProfilePolicy.ChartRadiusFraction(entry.GetProperty("level").GetDouble()), 12);
        Assert.True(FitnessProfilePolicy.ChartRadiusFraction(5) > FitnessProfilePolicy.ChartRadiusFraction(4));
    }

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
                Name = "movement " + row.GetProperty("id").GetInt32(), Video = "", PrimaryCanonicalGroup = default,
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
            double[] actual = FitnessProfilePolicy.CalculateFavourites(exercises, state, ratings);
            Assert.True(expected.SequenceEqual(actual), entry.GetProperty("name").GetString());
            ratings[999] = FitnessProfilePolicy.Dimensions.Select(_ => new[] { 5 }).ToArray(); // Retired catalog ID.
            FavouriteFitnessProfileAxis[] profile = FitnessProfilePolicy.InspectFavourites(exercises, state, ratings);
            Assert.Equal(expected, profile.Select(axis => axis.Mean));
            for (int dimension = 0; dimension < profile.Length; dimension++)
            {
                int[][] evidence = entry.GetProperty("evidence").TryGetProperty(FitnessProfilePolicy.Dimensions[dimension], out var rows)
                    ? rows.EnumerateArray().Select(row => row.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray() : [];
                Assert.Equal(evidence, profile[dimension].Contributions.Select(c => new[] { c.MovementId, c.ExerciseId, c.Level }).ToArray());
                Assert.All(profile[dimension].Contributions, c => Assert.Equal("movement " + c.ExerciseId, c.Name));
            }
            Assert.Equal(JsonSerializer.Serialize(profile), JsonSerializer.Serialize(
                FitnessProfilePolicy.InspectFavourites(exercises.Reverse().ToArray(), state, ratings)));
            Assert.All(FitnessProfilePolicy.InspectFavourites(exercises, state, new Dictionary<int, int[][]>()), axis =>
            {
                Assert.Equal(0, axis.Mean);
                Assert.Empty(axis.Contributions);
            });
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

    [Theory]
    [InlineData("fitness-profile-admissions.json")]
    [InlineData("fitness-precise-admissions.json")]
    [InlineData("fitness-ability-admissions.json")]
    [InlineData("fitness-zone-admissions.json")]
    public void ReviewedAdmissionsOpenPreciseContributionsAndPreserveTheOlderCatalog(string fixtureName)
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", fixtureName)));
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        var ratings = FitnessProfilePolicy.Load(source);
        foreach (JsonElement entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var state = new WorkoutState { LastKeptExerciseIds = entry.GetProperty("kept")
                .EnumerateArray().Select(id => id.GetInt32()).ToHashSet() };
            string before = JsonSerializer.Serialize(state);
            double[] expected = entry.GetProperty("expected").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            Assert.Equal(expected, FitnessProfilePolicy.CalculateFavourites(catalog, state, ratings));
            Assert.Equal(before, JsonSerializer.Serialize(state));
        }
        int revision = fixture.RootElement.GetProperty("catalogRevision").GetInt32();
        int[] ids = fixture.RootElement.GetProperty("exerciseIds").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        Assert.DoesNotContain(WorkoutCatalogPolicy.ForWorkout(catalog, revision - 1), e => ids.Contains(e.Id));
        Assert.All(ids, id => Assert.Contains(WorkoutCatalogPolicy.ForWorkout(catalog, revision), e => e.Id == id));
        if (fixture.RootElement.TryGetProperty("compatibilityCases", out JsonElement compatibility))
            foreach (JsonElement entry in compatibility.EnumerateArray())
            {
                Exercise exercise = catalog.Single(e => e.Id == entry.GetProperty("id").GetInt32());
                WorkoutModifiers modifiers = entry.GetProperty("modifiers").EnumerateArray()
                    .Aggregate(WorkoutModifiers.None, (mask, name) =>
                        mask | Enum.Parse<WorkoutModifiers>(name.GetString()!));
                Assert.Equal(entry.GetProperty("allowed").GetBoolean(),
                    WorkoutModifierPolicy.IsCompatible(exercise, modifiers));
            }
        if (fixture.RootElement.TryGetProperty("zoneCases", out JsonElement zones))
            foreach (JsonElement entry in zones.EnumerateArray())
            {
                int dimension = entry.GetProperty("dimension").GetInt32();
                int[] zone = entry.GetProperty("zone").EnumerateArray().Select(v => v.GetInt32()).ToArray();
                int[][] profile = Enumerable.Range(0, 10).Select(axis => axis == dimension ? zone : Array.Empty<int>()).ToArray();
                Assert.Equal(entry.GetProperty("eligible").GetBoolean(), FitnessSelectionPolicy.IsEligible(
                    ratings[entry.GetProperty("id").GetInt32()], profile));
            }
        if (fixture.RootElement.TryGetProperty("planningCases", out JsonElement planning))
            foreach (JsonElement entry in planning.EnumerateArray())
            {
                int exerciseId = entry.GetProperty("id").GetInt32();
                int minutes = entry.GetProperty("minutes").GetInt32();
                var state = new WorkoutState();
                state.KeptExerciseRootIdsBySelectionGroupId[entry.GetProperty("keepSlot").GetString()!] = [exerciseId];
                WorkoutModifiers modifiers = entry.GetProperty("modifiers").EnumerateArray()
                    .Aggregate(WorkoutModifiers.None, (mask, name) => mask | Enum.Parse<WorkoutModifiers>(name.GetString()!));
                var service = new ExerciseSessionService(catalog, new Random(42),
                    () => new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero), fitnessRatings: ratings);
                service.StartWorkout(state, minutes, modifiers);
                Assert.Equal(minutes, service.GetActiveGroups(state).Count);
                Assert.Contains(service.GetActiveGroups(state), group => service.GetSelectedExercise(state, group).Id == exerciseId);
            }
        Assert.Equal(266, catalog.Single(e => e.Id == 1621).SessionMovementId);
        Assert.Equal(2, catalog.Single(e => e.Id == 1701).SequenceBlocks.Length);
    }
}
