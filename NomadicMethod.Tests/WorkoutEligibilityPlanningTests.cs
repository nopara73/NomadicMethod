using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutEligibilityPlanningTests
{
    private static readonly long Now = DateTimeOffset.Parse("2026-10-31T08:00:00Z").ToUnixTimeMilliseconds();
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static (Exercise[] Catalog, FitnessRatingCatalog Ratings) Load()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        return (catalog, FitnessProfilePolicy.Load(source));
    }

    private static WorkoutState Evidence(IEnumerable<WorkoutDecisionLog> decisions) => new()
    {
        CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        WorkoutHistory = [new() { SessionId = 1, Status = WorkoutSessionStatus.Interrupted,
            StartedAtUnixMilliseconds = Now - 172800000, EndedAtUnixMilliseconds = Now - 172799999,
            WorkoutMinutes = 3, StartedBeforeLogging = true, Decisions = decisions.ToList() }],
    };

    private static WorkoutState LevelFiveEvidence(Exercise[] catalog, FitnessRatingCatalog ratings)
    {
        var decisions = new List<WorkoutDecisionLog>();
        var used = new HashSet<int>();
        for (int axis = 0; axis < 10; axis++)
        {
            Exercise seed = catalog.First(exercise => !used.Contains(WorkoutModifierPolicy.GetSessionMovementId(exercise)) &&
                ratings.HasTrainingClaim(exercise.Id, axis) &&
                (ratings[exercise.Id][axis][^1] == 4 || ratings[exercise.Id][axis].SequenceEqual(new[] { 5 })));
            used.Add(WorkoutModifierPolicy.GetSessionMovementId(seed));
            decisions.Add(new() { RootExerciseId = seed.Id,
                Outcome = ratings[seed.Id][axis][^1] == 4 ? ExerciseOutcome.TooEasy : ExerciseOutcome.Tick,
                DecidedAtUnixMilliseconds = axis + 1 });
        }
        WorkoutState state = Evidence(decisions);
        Assert.All(FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(catalog, state, ratings)),
            zone => Assert.Equal(new[] { 5 }, zone));
        return state;
    }

    private static void AssertComplete(ExerciseSessionService service, WorkoutState state,
        Exercise[] catalog, FitnessRatingCatalog ratings, int minutes)
    {
        Assert.Equal(minutes, service.GetActiveGroups(state).Count);
        int[][] zones = FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(catalog, state, ratings));
        Dictionary<int, Exercise> byId = catalog.ToDictionary(exercise => exercise.Id);
        foreach (WorkoutGroup group in service.GetActiveGroups(state))
        {
            Exercise root = service.GetSelectedExercise(state, group);
            Exercise[] members = WorkoutSequencePolicy.GetMembers(root, byId);
            Assert.True(FitnessSelectionPolicy.IsSequenceEligible(members.Select(member => ratings[member.Id]), zones,
                members.Select(member => ratings.GetTrainingCharacteristics(member.Id))));
        }
    }

    [Fact]
    public void RealPartiallyKnownFeedbackAlwaysKeepsWholeWorkoutsAndHistory()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "eligibility-planning-cases.json")));
        foreach (JsonElement entry in fixture.RootElement.GetProperty("partialFeedback").EnumerateArray())
        {
            var (catalog, ratings) = Load();
            WorkoutState state = Evidence([new() { RootExerciseId = entry.GetProperty("exerciseId").GetInt32(),
                Outcome = ExerciseOutcome.Tick, DecidedAtUnixMilliseconds = 1 }]);
            string history = JsonSerializer.Serialize(state.WorkoutHistory);
            string ratingsBefore = JsonSerializer.Serialize(ratings);
            var scores = catalog.Select(exercise => exercise.Score).ToArray();
            var service = new ExerciseSessionService(catalog, new Random(42), () => DateTimeOffset.FromUnixTimeMilliseconds(Now), fitnessRatings: ratings);
            int minutes = entry.GetProperty("minutes").GetInt32();
            service.StartWorkout(state, minutes, (WorkoutModifiers)entry.GetProperty("modifiers").GetInt32());
            AssertComplete(service, state, catalog, ratings, minutes);
            Assert.Equal(history, JsonSerializer.Serialize(state.WorkoutHistory));
            Assert.Equal(ratingsBefore, JsonSerializer.Serialize(ratings));
            Assert.Equal(scores, catalog.Select(exercise => exercise.Score).ToArray());
        }
    }

    [Fact]
    public void SavedKeepsAndPausedAtomicWorkSurviveCoverageResolutionAndExplicitEdits()
    {
        var (catalog, ratings) = Load();
        WorkoutState probe = LevelFiveEvidence(catalog, ratings);
        var service = new ExerciseSessionService(catalog, new Random(42),
            () => DateTimeOffset.FromUnixTimeMilliseconds(Now), fitnessRatings: ratings);
        service.StartWorkout(probe, 15, WorkoutModifiers.None);
        Dictionary<int, Exercise> byId = catalog.ToDictionary(exercise => exercise.Id);
        HashSet<int> declined = probe.WorkoutHistory.SelectMany(session => session.Decisions)
            .Where(decision => decision.Outcome is ExerciseOutcome.TooEasy or ExerciseOutcome.TooHard)
            .Select(decision => WorkoutModifierPolicy.GetSessionMovementId(byId[decision.RootExerciseId])).ToHashSet();
        Exercise kept = service.GetActiveGroups(probe).Select(group => byId[probe.SelectedExerciseIds[group.SelectionKey]])
            .First(root => !declined.Contains(WorkoutModifierPolicy.GetSessionMovementId(root)) &&
                MassGroupingTaxonomy.GetResolution(15).Groups.Any(group => WorkoutSequencePolicy.IsSelectable(root, byId, group)));
        WorkoutGroup originalSlot = MassGroupingTaxonomy.GetResolution(15).Groups.First(group => WorkoutSequencePolicy.IsSelectable(kept, byId, group));
        WorkoutState state = LevelFiveEvidence(catalog, ratings);
        state.KeptExerciseRootIdsBySelectionGroupId[originalSlot.Id] = [kept.Id];
        service = new ExerciseSessionService(catalog, new Random(43),
            () => DateTimeOffset.FromUnixTimeMilliseconds(Now), fitnessRatings: ratings);
        service.StartWorkout(state, 15, WorkoutModifiers.None);
        Assert.Contains(kept.Id, state.KeptExerciseRootIdsBySelectionGroupId[originalSlot.Id]);
        WorkoutGroup current = service.GetNextGroup(state)!;
        int rootId = state.SelectedExerciseIds[current.SelectionKey];
        int sets = state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(current.SelectionKey, 1);
        service.PauseMovement(state, current, 27000, pausedByUser: true);
        service.ResizeActiveWorkout(state, 10);
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        Assert.Equal(rootId, state.SelectedExerciseIds[current.SelectionKey]);
        Assert.Equal(sets, state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(current.SelectionKey, 1));
        Assert.Equal(27000, state.PendingMovementMillisecondsRemaining);
        service.BeginRest(state, service.GetNextGroup(state)!, Now + 5000);
        service.ReconfigureActiveWorkout(state, WorkoutModifiers.Floor, current.Id);
        Assert.Equal(current.Id, state.PendingRestGroupId);
        Assert.Equal(Now + 5000, state.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(rootId, state.SelectedExerciseIds[current.SelectionKey]);
        Assert.Equal(sets, state.ActiveSetCountsBySelectionGroupId.GetValueOrDefault(current.SelectionKey, 1));
        AssertComplete(service, state, catalog, ratings, 10);
        Assert.Contains(kept.Id, state.KeptExerciseRootIdsBySelectionGroupId[originalSlot.Id]);
    }

    [Fact]
    public void EmptyFineSlotsResolveBeforeFailureAndReopenRetainsTheirPlan()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "eligibility-planning-cases.json")));
        foreach (JsonElement entry in fixture.RootElement.GetProperty("emptyKnownProfiles").EnumerateArray())
        {
            var (catalog, ratings) = Load();
            WorkoutState state = LevelFiveEvidence(catalog, ratings);
            var service = new ExerciseSessionService(catalog, new Random(42), () => DateTimeOffset.FromUnixTimeMilliseconds(Now), fitnessRatings: ratings);
            int minutes = entry.GetProperty("minutes").GetInt32();
            service.StartWorkout(state, minutes, (WorkoutModifiers)entry.GetProperty("modifiers").GetInt32());
            AssertComplete(service, state, catalog, ratings, minutes);
            Assert.NotNull(state.ActiveDurationSelectionGroupIds);
            WorkoutGroup current = service.GetNextGroup(state)!;
            service.PauseMovement(state, current, 27000, pausedByUser: true);
            var selections = state.SelectedExerciseIds.OrderBy(entry => entry.Key).ToArray();
            string partition = JsonSerializer.Serialize(state.ActiveDurationSelectionGroupIds);
            string log = JsonSerializer.Serialize(state.ActiveWorkoutSession);
            state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
            new ExerciseSessionService(catalog, new Random(99), () => DateTimeOffset.FromUnixTimeMilliseconds(Now), fitnessRatings: ratings).RestoreAfterReopen(state);
            var changed = selections.Where(entry => state.SelectedExerciseIds.GetValueOrDefault(entry.Key) != entry.Value).ToArray();
            Assert.True(changed.Length == 0, JsonSerializer.Serialize(new { minutes,
                modifiers = entry.GetProperty("modifiers").GetInt32(), changed,
                active = state.ActiveDurationSelectionGroupIds }));
            Assert.Equal(partition, JsonSerializer.Serialize(state.ActiveDurationSelectionGroupIds));
            Assert.Equal(log, JsonSerializer.Serialize(state.ActiveWorkoutSession));
            Assert.Equal(27000, state.PendingMovementMillisecondsRemaining);
        }
    }
}
