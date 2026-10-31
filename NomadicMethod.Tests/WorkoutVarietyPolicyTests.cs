using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using NomadicMethod.Services;
using NomadicMethod.Models;

namespace NomadicMethod.Tests;

public sealed class WorkoutVarietyPolicyTests
{
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } })!;

    private static FitnessRatingCatalog Ratings()
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        return FitnessProfilePolicy.Load(source);
    }

    private static (Exercise[] Exercises, Dictionary<int, int[][]> Ratings, WorkoutState State) Fixture(bool excludeEquipment = false, bool excludeAll = false)
    {
        Exercise[] exercises = MassGroupingTaxonomy.GetResolution(3).Groups.SelectMany((group, index) =>
            Enumerable.Range(0, 5).Select(choice => new Exercise
            {
                Id = 90000 + index * 10 + choice, Name = $"Synthetic variety fixture {index}/{choice}", Video = "synthetic.mp4",
                PrimaryCanonicalGroup = group.CanonicalGroups.First(),
                SecondaryCanonicalGroups = group.CanonicalGroups.Skip(1).ToArray(),
                Practice = "Synthetic test only", MotionProfile = "SyntheticTest", Mode = ExerciseMode.Repetition,
                Presentation = ExercisePresentation.Motion, HoldFramePercent = 0, SideSequence = ExerciseSideSequence.Continuous,
                SequenceBlocks = [new() { ExerciseId = 90000 + index * 10 + choice, MirrorMedia = false }],
                OnlyFeetTouchGround = true, ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "None", Silent = true,
                FloorContactPoints = ExerciseFloorContact.Feet,
                ElevationRequired = excludeAll || excludeEquipment && index == 0 && choice == 4,
                MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
                HardFloorCompatibility = ExerciseHardFloorCompatibility.Compatible,
                InsectCompatibility = ExerciseInsectCompatibility.Compatible,
                ShyCompatibility = ExerciseShyCompatibility.Compatible,
                UpperBodyClothingRequirement = ExerciseUpperBodyClothingRequirement.Agnostic,
            })).ToArray();
        var ratings = exercises.ToDictionary(exercise => exercise.Id,
            _ => Enumerable.Range(0, 10).Select(axis => axis == 6 ? new[] { 2 } : Array.Empty<int>()).ToArray());
        var state = new WorkoutState
        {
            WorkoutHistory = [new() { SessionId = 1, Status = WorkoutSessionStatus.Interrupted,
                Decisions = [new() { RootExerciseId = 90000, Outcome = ExerciseOutcome.Tick, DecidedAtUnixMilliseconds = 1 }] }],
        };
        return (exercises, ratings, state);
    }

    [Theory]
    [InlineData("zone")]
    [InlineData("equipment")]
    public void ApplicationPreflightRejectsTheFinalFourChoicePoolWithoutWritingState(string excludedBy)
    {
        var (exercises, ratings, state) = Fixture(excludedBy == "equipment");
        if (excludedBy == "zone") ratings[90004][6] = [1];
        var service = ExerciseSessionService.CreateForApplication(exercises, new Random(1), fitnessRatings: ratings);
        string before = JsonSerializer.Serialize(state);
        Assert.Equal(WorkoutVarietyPolicy.UnavailableMessage,
            Assert.Throws<InvalidOperationException>(() => service.PrepareWorkout(state, 3, WorkoutModifiers.None)).Message);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void ApplicationRestoresAnUnfinishedWorkoutEvenWhenItsFrozenPoolHasFewerChoices()
    {
        var (exercises, ratings, state) = Fixture();
        var service = ExerciseSessionService.CreateForApplication(exercises, new Random(1), fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        var first = service.GetNextGroup(state)!;
        service.PauseMovement(state, first, 17000, pausedByUser: true);
        var selected = state.SelectedExerciseIds.OrderBy(entry => entry.Key).ToArray();
        int[] selectedIds = service.GetActiveGroups(state).Select(group => service.GetSelectedExercise(state, group).Id).Distinct().ToArray();
        var restored = ExerciseSessionService.CreateForApplication(exercises.Where(exercise => selectedIds.Contains(exercise.Id)).ToArray(),
            new Random(2), fitnessRatings: ratings);
        restored.RestoreAfterReopen(state);
        Assert.Equal(selected, state.SelectedExerciseIds.OrderBy(entry => entry.Key).ToArray());
        Assert.Equal(first.Id, restored.GetNextGroup(state)!.Id);
        Assert.Equal(17000, state.PendingMovementMillisecondsRemaining);
    }

    [Fact]
    public void IncidentalMinimumRatingsCannotSupplyTheFifthTrainingChoice()
    {
        var (exercises, original, state) = Fixture();
        var claims = original.ToDictionary(entry => entry.Key, entry => entry.Value.Select(set => set.Length > 0).ToArray());
        var values = original.ToDictionary(entry => entry.Key, entry => entry.Value.Select(set => set.Length == 0 ? new[] { 1 } : set).ToArray());
        values[90000][1] = [1]; claims[90000][1] = true; // Synthetic strength-1 and coordination-2 evidence.
        values[90004][6] = [1]; // Its strength-1 demand is incidental, not a training claim.
        var service = ExerciseSessionService.CreateForApplication(exercises, new Random(1),
            fitnessRatings: new FitnessRatingCatalog(values, claims));
        Assert.Equal(WorkoutVarietyPolicy.UnavailableMessage,
            Assert.Throws<InvalidOperationException>(() => service.PrepareWorkout(state, 3, WorkoutModifiers.None)).Message);
    }

    [Theory]
    [InlineData("settings")]
    [InlineData("duration")]
    public void FailedActiveEditsPreserveTheWholeWorkout(string edit)
    {
        var (exercises, ratings, state) = Fixture(excludeEquipment: edit == "settings");
        var service = ExerciseSessionService.CreateForApplication(exercises, new Random(1), fitnessRatings: ratings);
        service.StartWorkout(state, 3, edit == "settings" ? WorkoutModifiers.Elevation : WorkoutModifiers.None);
        WorkoutGroup current = service.GetNextGroup(state)!;
        if (edit == "duration") ratings[90004][6] = [1];
        string before = JsonSerializer.Serialize(state);
        Assert.Equal(WorkoutVarietyPolicy.UnavailableMessage, Assert.Throws<InvalidOperationException>(() =>
        {
            if (edit == "settings") service.ReconfigureActiveWorkout(state, WorkoutModifiers.None, current.Id);
            else service.ResizeActiveWorkout(state, 5);
        }).Message);
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void ExistingFitnessRatingsAreUnchanged()
    {
        using var profiles = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "post-filter-variety-profiles.json")));
        var ratings = Ratings();
        var rows = profiles.RootElement.GetProperty("existingRatingIds").EnumerateArray().Select(value =>
            new object[] { value.GetInt32(), ratings[value.GetInt32()], ratings.GetTrainingCharacteristics(value.GetInt32())! }).ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows)))).ToLowerInvariant();
        Assert.Equal(profiles.RootElement.GetProperty("existingRatingsSha256").GetString(), hash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ActualCatalogHasFiveSelectableFamiliesAfterSettingsAndZoneAtEveryDuration(int profileIndex)
    {
        using var profiles = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "post-filter-variety-profiles.json")));
        var profile = profiles.RootElement.GetProperty("profiles")[profileIndex];
        var modifiers = (WorkoutModifiers)profile.GetProperty("modifiers").GetInt32();
        int[][] zones = profile.GetProperty("zones").EnumerateArray().Select(set => set.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray();
        var (evidence, evidenceRatings, state) = Fixture(excludeAll: true);
        var actualRatings = Ratings();
        var ratingValues = actualRatings.ToDictionary(entry => entry.Key, entry => entry.Value);
        var trainingClaims = actualRatings.Keys.ToDictionary(id => id, id => actualRatings.GetTrainingCharacteristics(id)!.ToArray());
        for (int axis = 0; axis < 10; axis++)
            evidenceRatings[evidence[axis].Id] = Enumerable.Range(0, 10).Select(index => index == axis ? zones[axis] : Array.Empty<int>()).ToArray();
        foreach (var entry in evidenceRatings)
        {
            ratingValues.Add(entry.Key, entry.Value);
            trainingClaims.Add(entry.Key, entry.Value.Select(set => set.Length > 0).ToArray());
        }
        var ratings = new FitnessRatingCatalog(ratingValues, trainingClaims);
        // These fixtures establish the zone and are physically excluded from
        // every counted pool. No synthetic exercise supplies catalog coverage.
        Assert.All(evidence, exercise => Assert.False(WorkoutModifierPolicy.IsCompatible(exercise, modifiers)));
        state.WorkoutHistory = [new() { SessionId = 1, Status = WorkoutSessionStatus.Interrupted,
            Decisions = evidence.Take(10).Select((exercise, axis) => new WorkoutDecisionLog
                { RootExerciseId = exercise.Id, Outcome = ExerciseOutcome.Tick, DecidedAtUnixMilliseconds = axis + 1 }).ToList() }];
        Exercise[] catalog = Catalog().Concat(evidence).ToArray();
        Assert.Equal(JsonSerializer.Serialize(zones), JsonSerializer.Serialize(FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(catalog, state, ratings))));
        var service = ExerciseSessionService.CreateForApplication(catalog, new Random(1), fitnessRatings: ratings);
        string before = JsonSerializer.Serialize(state);
        foreach (var duration in profiles.RootElement.GetProperty("minutes").EnumerateArray())
            service.EnsureSelectableWorkoutVariety(state, duration.GetInt32(), modifiers);
        Assert.Equal(before, JsonSerializer.Serialize(state));
        var groupsById = new[] { 3, 5, 7, 10, 15, 20, 30 }.SelectMany(minutes =>
            MassGroupingTaxonomy.GetResolution(minutes).Groups).ToDictionary(group => group.Id);
        foreach (int minutes in new[] { 15 })
        {
            var planned = JsonSerializer.Deserialize<WorkoutState>(before)!;
            var planner = ExerciseSessionService.CreateForApplication(catalog, new Random(1), fitnessRatings: ratings);
            planner.StartWorkout(planned, minutes, modifiers);
            WorkoutGroup[] actualGroups = planned.ActiveWorkoutSession!.InitialSelections
                .SelectMany(selection => selection.CoveredWorkoutGroupIds).Distinct().Select(id => groupsById[id]).ToArray();
            planner.EnsureSelectableWorkoutVariety(planned, minutes, modifiers, actualGroups);
            Assert.Equal(minutes, planner.GetActiveGroups(planned).Count);
        }
    }

    [Fact]
    public void CompleteFeasibilityAndMovementIdentityMatchTheSharedCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "post-filter-variety-cases.json")));
        Assert.Equal(5, WorkoutVarietyPolicy.RequiredMovementChoices);
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            int groups = entry.GetProperty("groups").GetInt32();
            var candidates = entry.GetProperty("candidates").EnumerateArray().Select(row =>
                new AtomicSequenceCandidate(row[0].GetInt32(), row[1].GetInt32(), row[2].GetUInt64(),
                    row[3].GetInt32(), new BigInteger[groups], row[0].GetInt32())).ToArray();
            var actual = WorkoutVarietyPolicy.FindDeficiencies(groups, entry.GetProperty("minutes").GetInt32(), candidates,
                entry.GetProperty("required").GetInt32(), groupsToValidate: entry.TryGetProperty("validateMask", out var mask)
                    ? mask.GetUInt64() : ulong.MaxValue).Select(row => new[] { row.GroupIndex, row.DistinctMovementCount, row.RequiredCount }).ToArray();
            int[][] expected = entry.GetProperty("expected").EnumerateArray().Select(row => row.EnumerateArray().Select(value => value.GetInt32()).ToArray()).ToArray();
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        }
    }
}
