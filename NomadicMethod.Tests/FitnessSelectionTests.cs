using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class FitnessSelectionTests
{
    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "fitness-selection-cases.json")));

    [Fact]
    public void SharedRoundingAndWeightsHandleUnknownsRangesAndBothNeighbors()
    {
        using JsonDocument fixture = Fixture();
        foreach (JsonElement entry in fixture.RootElement.GetProperty("rounding").EnumerateArray())
            Assert.Equal(entry.GetProperty("rounded").GetInt32(),
                FitnessProfilePolicy.RoundLevel(entry.GetProperty("level").GetDouble()));
        foreach (JsonElement entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] profile = entry.GetProperty("profile").EnumerateArray()
                .Select(level => FitnessProfilePolicy.RoundLevel(level.GetDouble())).ToArray();
            JsonElement sets = entry.GetProperty("levels");
            int[][]? levels = sets.ValueKind == JsonValueKind.Null ? null :
                sets.EnumerateArray().Select(set => set.EnumerateArray()
                    .Select(level => level.GetInt32()).ToArray()).ToArray();
            Assert.Equal(entry.GetProperty("weight").GetDouble(),
                FitnessSelectionPolicy.Weight(levels, profile));
        }
    }

    [Fact]
    public void SharedDrawsKeepFamiliesTogetherAndGenerateEachKeyOnce()
    {
        using JsonDocument fixture = Fixture();
        foreach (JsonElement entry in fixture.RootElement.GetProperty("ordering").EnumerateArray())
        {
            var candidates = entry.GetProperty("candidates").EnumerateArray().Select(row => (
                Id: row.GetProperty("id").GetInt32(),
                Movement: row.GetProperty("movementId").GetInt32(),
                Weight: row.GetProperty("weight").GetDouble())).ToArray();
            var before = candidates.ToArray();
            double[] draws = entry.GetProperty("draws").EnumerateArray()
                .Select(value => value.GetDouble()).ToArray();
            int cursor = 0;
            var ordered = FitnessSelectionPolicy.Order(candidates, e => e.Movement,
                e => e.Weight, () => draws[cursor++]);
            Assert.Equal(entry.GetProperty("expected").EnumerateArray()
                .Select(value => value.GetInt32()), ordered.Select(e => e.Id));
            Assert.Equal(draws.Length, cursor);
            Assert.Equal(before, candidates);
        }
    }

    [Fact]
    public void SamplingFavorsNeighborsWithoutBuyingExtraFamilyDraws()
    {
        var random = new Random(123);
        (int Id, int Movement, double Weight)[] candidates =
            [(1, 1, 1), (2, 1, 0.5), (3, 1, 0.25), (4, 2, 0.5)];
        int familyOne = 0;
        for (int i = 0; i < 10000; i++)
            if (FitnessSelectionPolicy.Order(candidates, e => e.Movement,
                    e => e.Weight, random.NextDouble)[0].Movement == 1)
                familyOne++;
        Assert.InRange(familyOne, 6400, 6900);
        var neighbors = new[] { 1, 2, 3, 5 }.Select((level, index) => (
            Id: index, Weight: FitnessSelectionPolicy.Weight([[level]], [2]))).ToArray();
        int[] counts = new int[4];
        for (int i = 0; i < 10000; i++)
            counts[FitnessSelectionPolicy.Order(neighbors, e => e.Id,
                e => e.Weight, random.NextDouble)[0].Id]++;
        Assert.True(counts[1] > counts[0] && counts[1] > counts[2]);
        Assert.InRange(Math.Abs(counts[0] - counts[2]), 0, 200);
        Assert.True(counts[3] > 100);
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> PlanningCases() =>
        JsonSerializer.Deserialize<WorkoutPlanningRestorationTests.PlanningCase[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "workout-planning-cases.json")), Options)!
            .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(PlanningCases))]
    public void ExistingFeedbackOutranksFitness(WorkoutPlanningRestorationTests.PlanningCase item)
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
        foreach (Exercise exercise in catalog) exercise.Score = item.LegacyScore;
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        var ratings = FitnessProfilePolicy.Load(source);
        var service = new ExerciseSessionService(catalog, new Random(765), () => Now, fitnessRatings: ratings);
        var state = new WorkoutState { ExerciseScoreAdjustmentsByPhase = item.Adjustments };
        service.Initialize(state);
        Exercise anchor = catalog.Single(e => e.Id == 218);
        WorkoutGroup slot = MassGroupingTaxonomy.GetResolution(30).Groups
            .First(g => g.CanonicalGroups.Contains(anchor.PrimaryCanonicalGroup));
        state.KeptExerciseRootIdsBySelectionGroupId[slot.Id] = [anchor.Id];
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        service.StartWorkout(state, item.Minutes, (WorkoutModifiers)item.Modifiers);
        var selections = state.ActiveWorkoutSession!.InitialSelections;
        Assert.Equal(item.ExpectedRejectedBlocks, selections.Where(s => s.SelectionScoreAtStart < 0)
            .Sum(s => s.SequenceBlockCount * s.SetCount));
        Assert.DoesNotContain(selections, s => item.ExcludedRoots.Contains(s.RootExerciseId));
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(7, false)]
    [InlineData(10, false)]
    [InlineData(15, false)]
    [InlineData(45, false)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    [InlineData(7, true)]
    [InlineData(10, true)]
    [InlineData(15, true)]
    [InlineData(45, true)]
    public void WeightedPlanningPreservesCoverageAndRestoresOriginalSelections(int minutes, bool light)
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        var ratings = FitnessProfilePolicy.Load(source);
        var service = new ExerciseSessionService(catalog, new Random(minutes), () => Now, fitnessRatings: ratings);
        var state = new WorkoutState();
        service.Initialize(state);
        Exercise anchor = catalog.Single(e => e.Id == 218);
        WorkoutGroup slot = MassGroupingTaxonomy.GetResolution(30).Groups
            .First(g => g.CanonicalGroups.Contains(anchor.PrimaryCanonicalGroup));
        state.KeptExerciseRootIdsBySelectionGroupId[slot.Id] = [anchor.Id];
        string kept = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        Assert.Equal(2, FitnessProfilePolicy.Calculate(catalog, state, ratings)[1]);
        WorkoutModifiers flags = WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror |
            WorkoutModifiers.HardFloor | WorkoutModifiers.Wall |
            (light ? WorkoutModifiers.Light : WorkoutModifiers.None);
        service.StartWorkout(state, minutes, flags);
        Assert.Equal(minutes, service.GetActiveGroups(state).Count);
        Assert.Equal(kept, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
        WorkoutGroup first = service.GetNextGroup(state)!;
        if (service.CanShuffleNextExercise(state, first))
        {
            Assert.NotNull(service.ShuffleNextExercise(state, first));
            Assert.Equal(minutes, service.GetActiveGroups(state).Count);
        }
        WorkoutGroup current = service.GetNextGroup(state)!;
        service.PauseMovement(state, current, 27000, pausedByUser: true);
        var selected = state.SelectedExerciseIds.OrderBy(entry => entry.Key).ToArray();
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        string log = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        var restored = new ExerciseSessionService(catalog, new Random(999), () => Now, fitnessRatings: ratings);
        restored.RestoreAfterReopen(state);
        Assert.Equal(selected, state.SelectedExerciseIds.OrderBy(entry => entry.Key).ToArray());
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(log, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Equal(27000, restored.GetPendingMovementMillisecondsRemaining(state, Now.ToUnixTimeMilliseconds()));
        restored.ReconfigureActiveWorkout(state, flags ^ WorkoutModifiers.Mirror ^ WorkoutModifiers.TallMirror, current.Id);
        int selectedId = restored.GetSelectedExercise(state, restored.GetNextGroup(state)!).Id;
        int duration = minutes == 3 ? 5 : minutes == 45 ? 30 : minutes;
        restored.ResizeActiveWorkout(state, duration);
        Assert.Equal(duration, restored.GetActiveGroups(state).Count);
        Assert.Equal(selectedId, restored.GetSelectedExercise(state, restored.GetNextGroup(state)!).Id);
    }
}
