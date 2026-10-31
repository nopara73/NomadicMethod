using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class CompleteFitnessRatingTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "complete-fitness-ratings-cases.json")));

    private static FitnessRatingCatalog Load(string json)
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return FitnessProfilePolicy.Load(source);
    }

    [Fact]
    public void EveryRuntimeExerciseHasTenNonemptyRatingsFromOneThroughFive()
    {
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        FitnessRatingCatalog ratings = FitnessProfilePolicy.Load(source);
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")));
        Assert.Equal(catalog.RootElement.GetArrayLength(), ratings.Count);
        foreach (JsonElement exercise in catalog.RootElement.EnumerateArray())
        {
            int[][] levels = ratings[exercise.GetProperty("id").GetInt32()];
            Assert.Equal(10, levels.Length);
            Assert.All(levels, set =>
            {
                Assert.NotEmpty(set);
                Assert.All(set, level => Assert.InRange(level, 1, 5));
            });
        }
    }

    [Fact]
    public void EmptyAndInvalidRatingsFailOnEveryCharacteristicWithoutDefaults()
    {
        using JsonDocument fixture = Fixture();
        string valid = fixture.RootElement.GetProperty("ratings").GetRawText();
        for (int dimension = 0; dimension < 10; dimension++)
        {
            foreach (JsonElement invalid in fixture.RootElement.GetProperty("invalidLevelSets").EnumerateArray())
            {
                JsonNode changed = JsonNode.Parse(valid)!;
                changed["ratings"]!["2"]![dimension] = JsonNode.Parse(invalid.GetRawText());
                Assert.Throws<InvalidDataException>(() => Load(changed.ToJsonString()));
            }
        }
        JsonNode legacy = JsonNode.Parse(valid)!;
        legacy["schemaVersion"] = 1;
        Assert.Throws<InvalidDataException>(() => Load(legacy.ToJsonString()));
        JsonNode missingClaims = JsonNode.Parse(valid)!;
        missingClaims["trainingCharacteristics"]!.AsObject().Remove("2");
        Assert.Throws<InvalidDataException>(() => Load(missingClaims.ToJsonString()));
    }

    [Fact]
    public void MinimumRatingsDoNotInventTrainingEvidenceOrChangeHistory()
    {
        using JsonDocument fixture = Fixture();
        FitnessRatingCatalog ratings = Load(fixture.RootElement.GetProperty("ratings").GetRawText());
        Exercise[] exercises = [Exercise(1), Exercise(2)];
        Assert.All(ratings[1], set => Assert.Equal(new[] { 1 }, set));
        Assert.All(Enumerable.Range(0, 10), dimension => Assert.False(ratings.HasTrainingClaim(1, dimension)));
        WorkoutState state = StateFor(1);
        string before = JsonSerializer.Serialize(state);
        Assert.All(FitnessProfilePolicy.Inspect(exercises, state, ratings), axis =>
        {
            Assert.Equal(0, axis.Mean);
            Assert.Empty(axis.Contributions);
        });
        Assert.Equal(new double[10], FitnessProfilePolicy.CalculateFavourites(exercises,
            new WorkoutState { LastKeptExerciseIds = [1] }, ratings));
        Assert.Equal(before, JsonSerializer.Serialize(state));
        Assert.True(FitnessSelectionPolicy.IsEligible(ratings[1], Enumerable.Range(0, 10).Select(_ => new[] { 1 }).ToArray()));
        Assert.False(FitnessSelectionPolicy.IsEligible(ratings[1], Enumerable.Range(0, 10).Select(_ => new[] { 2 }).ToArray()));

        state = StateFor(2);
        before = JsonSerializer.Serialize(state);
        double[] expected = fixture.RootElement.GetProperty("expectedEasyProfile").EnumerateArray()
            .Select(value => value.GetDouble()).ToArray();
        Assert.Equal(expected, FitnessProfilePolicy.Calculate(exercises, state, ratings));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    private static WorkoutState StateFor(int id) => new()
    {
        WorkoutHistory = [new WorkoutSessionLog { SessionId = 1, Decisions = [new WorkoutDecisionLog
        {
            RootExerciseId = id, Outcome = ExerciseOutcome.TooEasy, DecidedAtUnixMilliseconds = 1000,
        }] }],
    };

    [Fact]
    public void IncidentalMinimumRatingsCannotQualifyAdvancedOrExemptMinimumOnlyWork()
    {
        using JsonDocument fixture = Fixture();
        FitnessRatingCatalog ratings = Load(fixture.RootElement.GetProperty("ratings").GetRawText());
        int[][] minimum = Enumerable.Range(0, 10).Select(_ => new[] { 1 }).ToArray();
        IReadOnlyList<bool>? advancedClaims = ratings.GetTrainingCharacteristics(2);
        IReadOnlyList<bool>? minimumClaims = ratings.GetTrainingCharacteristics(1);
        Assert.False(FitnessSelectionPolicy.IsEligible(ratings[2], minimum, advancedClaims));
        int[][] mixed = minimum.Select(set => set.ToArray()).ToArray();
        mixed[1] = [2];
        Assert.False(FitnessSelectionPolicy.IsEligible(ratings[1], mixed, minimumClaims));
        Assert.True(FitnessSelectionPolicy.IsEligible(ratings[1], minimum, minimumClaims));
        Assert.False(FitnessSelectionPolicy.IsSequenceEligible([ratings[1], ratings[2]], minimum,
            [minimumClaims, advancedClaims]));
    }

    private static Exercise Exercise(int id) => new()
    {
        Id = id, SessionMovementId = id, Name = "movement " + id, Video = "",
        PrimaryCanonicalGroup = default, SecondaryCanonicalGroups = [], Practice = "", MotionProfile = "",
        Mode = default, Presentation = default, HoldFramePercent = 0, SideSequence = default,
        OnlyFeetTouchGround = true, ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "", Silent = true,
    };
}
