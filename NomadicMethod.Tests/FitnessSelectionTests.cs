using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class FitnessSelectionTests
{
    [Fact]
    public void SharedTrainingClaimsPreventIncidentalMinimumLevelShortcuts()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "fitness-selection-preferences.json")));
        foreach (JsonElement entry in fixture.RootElement.GetProperty("maskedMatches").EnumerateArray())
        {
            int[][] ReadSets(string field) => entry.GetProperty(field).EnumerateArray().Select(set =>
                set.EnumerateArray().Select(level => level.GetInt32()).ToArray()).ToArray();
            int[][] levels = ReadSets("levels"), zones = ReadSets("zones");
            bool[] trained = entry.GetProperty("trained").EnumerateArray().Select(value => value.GetBoolean()).ToArray();
            string before = JsonSerializer.Serialize(levels);
            Assert.Equal(entry.GetProperty("eligible").GetBoolean(), FitnessSelectionPolicy.IsEligible(levels, zones, trained));
            Assert.Equal(before, JsonSerializer.Serialize(levels));
        }
        int[][] hard = Enumerable.Range(0, 10).Select(axis => new[] { axis == 1 ? 3 : 1 }).ToArray();
        int[][] easy = Enumerable.Range(0, 10).Select(_ => new[] { 1 }).ToArray();
        int[][] zone = Enumerable.Range(0, 10).Select(_ => new[] { 3 }).ToArray();
        bool[] claims = Enumerable.Range(0, 10).Select(axis => axis == 1).ToArray();
        Assert.False(FitnessSelectionPolicy.IsSequenceEligible([hard, easy], zone, [claims, new bool[10]]));
        Assert.False(FitnessSelectionPolicy.IsSequenceEligible([hard], zone, [null]));
    }

    [Theory]
    [InlineData(ExerciseOutcome.TooEasy)]
    [InlineData(ExerciseOutcome.TooHard)]
    public void MovementDifficultyOutranksTheGlobalProfileAndAppliesToAliases(ExerciseOutcome outcome)
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        state.KeptExerciseRootIdsBySelectionGroupId.Clear();
        foreach (int id in new[] { 3, 6 }) exercises.Single(e => e.Id == id).Score = 10000;
        state.WorkoutHistory[0].Decisions.Add(new() { RootExerciseId = 6, Outcome = outcome, DecidedAtUnixMilliseconds = 2 });
        int[][] zones = FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(exercises, state, ratings));
        Assert.True(FitnessSelectionPolicy.IsEligible(ratings[6], zones));
        string feedback = JsonSerializer.Serialize(state.WorkoutHistory);
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.NotEqual(3, WorkoutModifierPolicy.GetSessionMovementId(service.GetSelectedExercise(state, service.GetNextGroup(state)!)));
        Assert.Empty(state.ExerciseScoreAdjustmentsByPhase);
        Assert.Equal(feedback, JsonSerializer.Serialize(state.WorkoutHistory));
        Assert.Equal(10000, exercises.Single(e => e.Id == 6).Score);
    }

    [Fact]
    public void LoadedTrainingClaimsReachTheActualPlanner()
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        ratings[1][8] = [1];
        ratings[6] = Enumerable.Range(0, 10).Select(axis => axis == 1 ? new[] { 4 } : Array.Empty<int>()).ToArray();
        var claims = ratings.ToDictionary(entry => entry.Key, entry => entry.Value.Select(set => set.Length > 0).ToArray());
        var complete = ratings.ToDictionary(entry => entry.Key,
            entry => entry.Value.Select(set => set.Length > 0 ? set : new[] { 1 }).ToArray());
        string json = JsonSerializer.Serialize(new { schemaVersion = 2, dimensions = FitnessProfilePolicy.Dimensions,
            ratings = complete, trainingCharacteristics = claims });
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        FitnessRatingCatalog loaded = FitnessProfilePolicy.Load(source);
        state.KeptExerciseRootIdsBySelectionGroupId.Clear();
        exercises.Single(exercise => exercise.Id == 6).Score = 10000;
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: loaded);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.DoesNotContain(state.ActiveWorkoutSession!.InitialSelections, selection => selection.RootExerciseId == 6);
        Assert.Equal(3, service.GetActiveGroups(state).Count);
    }

    [Fact]
    public void PhaseRefusalsApplyToAliasesBeforeAnyFitnessProfileExists()
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        state.WorkoutHistory.Clear();
        state.KeptExerciseRootIdsBySelectionGroupId.Clear();
        exercises.Single(e => e.Id == 6).Score = 100;
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup] = new() { [3] = -1 };
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.NotEqual(3, WorkoutModifierPolicy.GetSessionMovementId(service.GetSelectedExercise(state, service.GetNextGroup(state)!)));
        Assert.Equal(new Dictionary<int,int> { [3] = -1 }, state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup]);
    }

    [Fact]
    public void LightStillDominatesDifficultyPreference()
    {
        var (exercises, ratings, state) = DiscoveryFixture(lightStress: true);
        state.WorkoutHistory[0].Decisions.Add(new() { RootExerciseId = 7, Outcome = ExerciseOutcome.TooEasy, DecidedAtUnixMilliseconds = 2 });
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.Light);
        Assert.Equal(7, service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id);
    }

    [Fact]
    public void ExtraSetsPreferOtherEligibleWorkOverAMandatoryDifficultyMismatch()
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        state.WorkoutHistory[0].Decisions.Add(new() { RootExerciseId = 30, Outcome = ExerciseOutcome.TooHard, DecidedAtUnixMilliseconds = 2 });
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 10, WorkoutModifiers.None);
        Assert.Equal(10, service.GetActiveGroups(state).Count);
        Assert.All(state.ActiveWorkoutSession!.InitialSelections.Where(selection => selection.RootExerciseId == 30),
            selection => Assert.Equal(1, selection.SetCount));
        Assert.Contains(state.ActiveWorkoutSession.InitialSelections, selection => selection.RootExerciseId == 30);
    }

    [Fact]
    public void SharedCoverageCannotUndoADifficultyPreference()
    {
        var (exercises, ratings, state) = DiscoveryFixture(shareDeclinedCoverage: true);
        state.KeptExerciseRootIdsBySelectionGroupId.Clear();
        exercises.Single(e => e.Id == 30).Score = 10000;
        state.WorkoutHistory[0].Decisions.Add(new() { RootExerciseId = 30, Outcome = ExerciseOutcome.TooHard, DecidedAtUnixMilliseconds = 2 });
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.Contains(state.ActiveWorkoutSession!.InitialSelections, selection => selection.RootExerciseId == 20);
        Assert.Contains(state.ActiveWorkoutSession.InitialSelections, selection => selection.RootExerciseId == 30);
    }

    [Fact]
    public void SharingCannotBorrowAHardOpportunityFromAnotherSlot()
    {
        var (exercises, ratings, state) = DiscoveryFixture(higherHard: true, shareDeclinedCoverage: true);
        state.KeptExerciseRootIdsBySelectionGroupId.Clear();
        exercises.Single(exercise => exercise.Id == 20).Score = 10000;
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.Contains(state.ActiveWorkoutSession!.InitialSelections, selection => selection.RootExerciseId == 20);
        Assert.Contains(state.ActiveWorkoutSession.InitialSelections, selection => selection.RootExerciseId == 30);
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "fitness-selection-cases.json")));

    [Theory]
    [InlineData(ExerciseOutcome.TooEasy)]
    [InlineData(ExerciseOutcome.TooHard)]
    public void LatestDifficultyCanBroadenATrappedSlotWithoutInventingScores(ExerciseOutcome outcome)
    {
        Exercise[] catalog = LegacyPelvicCatalogFixture.Load();
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            WorkoutHistory = [new() { SessionId = 1, Status = WorkoutSessionStatus.Interrupted,
                Decisions = [new() { RootExerciseId = 918, Outcome = outcome,
                    DecidedAtUnixMilliseconds = Now.AddHours(-1).ToUnixTimeMilliseconds() }] }],
        };
        var service = new ExerciseSessionService(catalog, new Random(5), () => Now);
        service.Initialize(state);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        string scores = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        service.StartWorkout(state, 45, (WorkoutModifiers)5232);
        Assert.DoesNotContain(state.ActiveWorkoutSession!.InitialSelections, selection => selection.RootExerciseId == 918);
        Assert.Contains("r15.abdominal-pelvic-floor", state.ActiveDurationSelectionGroupIds!);
        Assert.Equal(45, service.GetActiveGroups(state).Count);
        Assert.Equal(history, JsonSerializer.Serialize(state.WorkoutHistory));
        Assert.Equal(scores, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        string ActiveSelections() => JsonSerializer.Serialize(service.GetActiveGroups(state)
            .Select(group => new { group.Id, ExerciseId = service.GetSelectedExercise(state, group).Id }));
        string selections = ActiveSelections();
        string log = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        service.RestoreAfterReopen(state);
        Assert.Equal(selections, ActiveSelections());
        Assert.Equal(log, JsonSerializer.Serialize(state.ActiveWorkoutSession));
    }

    [Fact]
    public void SharedEligibilityHandlesUnknownsOverlappingRangesAndNoncontiguousZones()
    {
        using JsonDocument fixture = Fixture();
        foreach (JsonElement entry in fixture.RootElement.GetProperty("rounding").EnumerateArray())
            Assert.Equal(entry.GetProperty("rounded").GetInt32(),
                FitnessProfilePolicy.RoundLevel(entry.GetProperty("level").GetDouble()));
        foreach (JsonElement entry in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[][] zones = entry.GetProperty("zones").EnumerateArray()
                .Select(set => set.EnumerateArray().Select(level => level.GetInt32()).ToArray()).ToArray();
            JsonElement sets = entry.GetProperty("levels");
            int[][]? levels = sets.ValueKind == JsonValueKind.Null ? null :
                sets.EnumerateArray().Select(set => set.EnumerateArray()
                    .Select(level => level.GetInt32()).ToArray()).ToArray();
            Assert.Equal(entry.GetProperty("eligible").GetBoolean(),
                FitnessSelectionPolicy.IsEligible(levels, zones));
        }
    }

    [Fact]
    public void CanonicalRangedSprintingAndReactionTasksMatchTheirSupportedLevels()
    {
        using JsonDocument fixture = Fixture();
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        var ratings = FitnessProfilePolicy.Load(source);
        foreach (JsonElement entry in fixture.RootElement.GetProperty("catalogCases").EnumerateArray())
        {
            int dimension = entry.GetProperty("dimension").GetInt32();
            int[] zone = entry.GetProperty("zone").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            int[][] zones = Enumerable.Range(0, 10).Select(axis => axis == dimension ? zone : Array.Empty<int>()).ToArray();
            Assert.Equal(entry.GetProperty("eligible").GetBoolean(), FitnessSelectionPolicy.IsEligible(
                ratings[entry.GetProperty("id").GetInt32()], zones));
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
                Tier: row.GetProperty("tier").GetInt32())).ToArray();
            var before = candidates.ToArray();
            double[] draws = entry.GetProperty("draws").EnumerateArray()
                .Select(value => value.GetDouble()).ToArray();
            int cursor = 0;
            var ordered = FitnessSelectionPolicy.Order(candidates, e => e.Movement,
                e => e.Tier, () => draws[cursor++]);
            Assert.Equal(entry.GetProperty("expected").EnumerateArray()
                .Select(value => value.GetInt32()), ordered.Select(e => e.Id));
            Assert.Equal(draws.Length, cursor);
            Assert.Equal(before, candidates);
        }
    }

    [Fact]
    public void EqualTierFamiliesHaveEqualChancesRegardlessOfAliases()
    {
        var random = new Random(123);
        (int Id, int Movement, int Tier)[] candidates =
            [(1, 1, 0), (2, 1, 0), (3, 1, 0), (4, 2, 0)];
        int familyOne = 0;
        for (int i = 0; i < 10000; i++)
            if (FitnessSelectionPolicy.Order(candidates, e => e.Movement,
                    e => e.Tier, random.NextDouble)[0].Movement == 1)
                familyOne++;
        Assert.InRange(familyOne, 4750, 5250);
    }

    [Fact]
    public void EverySequenceMemberMustQualifyUnlessTheWholeProfileIsUnknown()
    {
        Assert.False(FitnessSelectionPolicy.IsSequenceEligible([null, [[3]]], [[3]]));
        Assert.True(FitnessSelectionPolicy.IsSequenceEligible([[[3]], [[3,4]]], [[3,4]]));
        Assert.True(FitnessSelectionPolicy.IsSequenceEligible([[[3]], [[2,3]]], [[3,4]]));
        Assert.False(FitnessSelectionPolicy.IsSequenceEligible([[[3]], [[1,2]]], [[3,4]]));
        Assert.True(FitnessSelectionPolicy.IsSequenceEligible([null, [[]]], [[]]));
    }

    private static (Exercise[] Exercises, Dictionary<int, int[][]> Ratings, WorkoutState State) DiscoveryFixture(
        bool higherHard = false, bool lightStress = false, bool shareDeclinedCoverage = false)
    {
        using JsonDocument fixture = Fixture();
        JsonElement discovery = fixture.RootElement.GetProperty("discovery");
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] exercises = discovery.GetProperty("exercises").EnumerateArray().Select(row =>
        {
            int id = row.GetProperty("id").GetInt32();
            CanonicalMuscleGroup[] muscles = groups[row.GetProperty("group").GetInt32()].CanonicalGroups.Order().ToArray();
            return new Exercise
            {
                Id = id, Name = $"Synthetic planner fixture {id}", Video = "", Practice = "", MotionProfile = "",
                PrimaryCanonicalGroup = muscles[0], SecondaryCanonicalGroups = muscles.Skip(1)
                    .Concat(shareDeclinedCoverage && id == 30 ? groups[1].CanonicalGroups : []).ToArray(),
                Mode = ExerciseMode.Repetition, Presentation = ExercisePresentation.Motion, HoldFramePercent = 0,
                SideSequence = ExerciseSideSequence.Continuous, SequenceBlocks = [new() { ExerciseId = id, MirrorMedia = false }],
                SessionMovementId = row.TryGetProperty("movementId", out var movement) ? movement.GetInt32() : id,
                Score = row.TryGetProperty("score", out var score) ? score.GetInt32() : 0,
                MuscularDemand = lightStress && id < 20 && id != 7 ||
                    higherHard && (id is 3 or 4 or 5 || shareDeclinedCoverage && id == 30) ? 2 : 0,
                OnlyFeetTouchGround = true, ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "None", Silent = true,
                MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
                HardFloorCompatibility = ExerciseHardFloorCompatibility.Compatible,
                ShyCompatibility = ExerciseShyCompatibility.Compatible,
                UpperBodyClothingRequirement = ExerciseUpperBodyClothingRequirement.Agnostic,
            };
        }).ToArray();
        var ratings = discovery.GetProperty("exercises").EnumerateArray().ToDictionary(row => row.GetProperty("id").GetInt32(),
            row => row.GetProperty("levels").EnumerateArray().Select(set => set.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray());
        var state = new WorkoutState { CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision };
        state.KeptExerciseRootIdsBySelectionGroupId[groups[0].Id] = discovery.GetProperty("kept").EnumerateArray()
            .Select(v => v.GetInt32()).ToHashSet();
        state.WorkoutHistory = [new() { SessionId = 1, Status = WorkoutSessionStatus.Interrupted,
            Decisions = [new() { RootExerciseId = 1, Outcome = ExerciseOutcome.Tick, DecidedAtUnixMilliseconds = 1 }] }];
        return (exercises, ratings, state);
    }

    [Fact]
    public void EligibleLovesThenEligibleNewMovementsWithoutFamilyRepeats()
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        var seen = new List<int>();
        int iteration = 0;
        do
        {
            WorkoutGroup group = service.GetNextGroup(state)!;
            seen.Add(WorkoutModifierPolicy.GetSessionMovementId(service.GetSelectedExercise(state, group)));
            Assert.Empty(state.Outcomes);
            Assert.True(++iteration <= 12);
            if (!service.CanShuffleNextExercise(state, group)) break;
            Assert.NotNull(service.ShuffleNextExercise(state, group));
            // Restoring and a chart change must retain the selected replacement
            // and every prior refusal, rather than restarting the queue.
            var selected = state.SelectedExerciseIds.OrderBy(e => e.Key).ToArray();
            state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
            service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
            service.RestoreAfterReopen(state);
            Assert.Equal(selected, state.SelectedExerciseIds.OrderBy(e => e.Key).ToArray());
        } while (true);
        using JsonDocument fixture = Fixture();
        Assert.Equal(fixture.RootElement.GetProperty("discovery").GetProperty("expectedFamilyOrder")
            .EnumerateArray().Select(v => v.GetInt32()), seen);
        Assert.Contains(1, state.NextWorkoutExcludedExerciseIds);
        Assert.DoesNotContain(3, state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup].Keys);
        Assert.Equal(3, service.GetActiveGroups(state).Count);
    }

    [Fact]
    public void HistoricalRefusalAppliesToAliasesAndDoesNotEraseHeartsOrScores()
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        string heartSlot = MassGroupingTaxonomy.GetResolution(3).Groups[1].Id;
        state.KeptExerciseRootIdsBySelectionGroupId = new() { [heartSlot] = [20] };
        ratings[20] = ratings[1];
        exercises.Single(e => e.Id == 6).Score = 1000;
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup] = new() { [3] = -1, [4] = -1, [5] = -1 };
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.Equal(7, service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id);
        Assert.Equal(100, exercises.Single(e => e.Id == 7).Score);
        Assert.Equal(new[] { 20 }, state.KeptExerciseRootIdsBySelectionGroupId[heartSlot].Order());
        Assert.Equal(3, state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup].Count);
    }

    private sealed class ZeroRandom : Random { public override double NextDouble() => 0; }

    [Theory]
    [InlineData(false, false, 3)]
    [InlineData(true, false, 7)]
    [InlineData(false, true, 7)]
    public void ZoneSelectionRespectsLightAndRecovery(bool light, bool recovering, int expected)
    {
        var (exercises, ratings, state) = DiscoveryFixture(higherHard: true);
        foreach (int id in new[] { 3, 4, 5 }) ratings[id] = ratings[1];
        exercises.Single(e => e.Id == 3).Score = 1000;
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        state.KeptExerciseRootIdsBySelectionGroupId = new() { [groups[1].Id] = [20] };
        ratings[20] = ratings[1];
        if (recovering)
            state.LastHardWorkUnixMillisecondsByPrimaryMuscle[exercises[0].PrimaryCanonicalGroup.ToString()] =
                Now.AddHours(-1).ToUnixTimeMilliseconds();
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, light ? WorkoutModifiers.Light : WorkoutModifiers.None);
        WorkoutGroup target = service.GetActiveGroups(state).First(g => g.SelectionKey == groups[0].Id);
        Assert.Equal(expected, service.GetSelectedExercise(state, target).Id);
        Assert.Empty(state.Outcomes);
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
    public void BootstrapPlanningPreservesCoverageAndRestoresOriginalSelections(int minutes, bool light)
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;
        using FileStream source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "fitness_ratings.json"));
        var ratings = FitnessProfilePolicy.Load(source);
        // Longer workouts originate in an older catalog and reopen with the full current catalog.
        Exercise[] initialCatalog = minutes > 15 ? LegacyPelvicCatalogFixture.Load() : catalog;
        var service = new ExerciseSessionService(initialCatalog, new Random(minutes), () => Now, fitnessRatings: ratings);
        var state = new WorkoutState();
        service.Initialize(state);
        Exercise anchor = catalog.Single(e => e.Id == 218);
        WorkoutGroup slot = MassGroupingTaxonomy.GetResolution(30).Groups
            .First(g => g.CanonicalGroups.Contains(anchor.PrimaryCanonicalGroup));
        state.KeptExerciseRootIdsBySelectionGroupId[slot.Id] = [anchor.Id];
        string kept = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        Assert.Equal(2, FitnessProfilePolicy.CalculateFavourites(catalog, state, ratings)[1]);
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
        var restored = minutes > 15
            ? ExerciseSessionService.CreateForApplication(catalog, new Random(999), () => Now, fitnessRatings: ratings)
            : new ExerciseSessionService(catalog, new Random(999), () => Now, fitnessRatings: ratings);
        restored.RestoreAfterReopen(state);
        Assert.Equal(selected, state.SelectedExerciseIds.OrderBy(entry => entry.Key).ToArray());
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(log, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Equal(27000, restored.GetPendingMovementMillisecondsRemaining(state, Now.ToUnixTimeMilliseconds()));
        if (minutes > 15)
        {
            // The historic Floor-off pelvic pool is below five. Opening remains
            // valid, while explicit edits must honor the current product gate.
            string before = JsonSerializer.Serialize(state);
            Assert.Contains("fewer than five", Assert.Throws<InvalidOperationException>(() =>
                restored.ReconfigureActiveWorkout(state, flags ^ WorkoutModifiers.Mirror ^ WorkoutModifiers.TallMirror, current.Id)).Message);
            Assert.Equal(before, JsonSerializer.Serialize(state));
            Assert.Contains("fewer than five", Assert.Throws<InvalidOperationException>(() =>
                restored.ResizeActiveWorkout(state, 30)).Message);
            Assert.Equal(before, JsonSerializer.Serialize(state));
            return;
        }
        restored.ReconfigureActiveWorkout(state, flags ^ WorkoutModifiers.Mirror ^ WorkoutModifiers.TallMirror, current.Id);
        int selectedId = restored.GetSelectedExercise(state, restored.GetNextGroup(state)!).Id;
        int duration = minutes == 3 ? 5 : minutes == 45 ? 30 : minutes;
        restored.ResizeActiveWorkout(state, duration);
        Assert.Equal(duration, restored.GetActiveGroups(state).Count);
        Assert.Equal(selectedId, restored.GetSelectedExercise(state, restored.GetNextGroup(state)!).Id);
    }
    [Fact]
    public void CoverageHolesCannotFallBackOutsideTheZone()
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        ratings[30] = Enumerable.Range(0, 10).Select(_ => Array.Empty<int>()).ToArray();
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        Assert.Contains(FitnessSelectionPolicy.UnavailableMessage,
            Assert.Throws<InvalidOperationException>(() => service.StartWorkout(state, 3, WorkoutModifiers.None)).Message);
    }

    [Fact]
    public void OverlappingRangesApplyToDurationAndModifierEditsWithoutReplacingCurrentWork()
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        ratings[20][1] = [1,2,3,4,5];
        ratings[30][1] = [2,3];
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup current = service.GetNextGroup(state)!;
        int id = service.GetSelectedExercise(state, current).Id;
        service.PauseMovement(state, current, 27000, pausedByUser: true);
        service.ResizeActiveWorkout(state, 5);
        service.ReconfigureActiveWorkout(state, WorkoutModifiers.Wall, service.GetNextGroup(state)!.Id);
        Assert.Equal(id, service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id);
        int[][] zones = FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(exercises, state, ratings));
        Assert.All(service.GetActiveGroups(state), group => Assert.True(FitnessSelectionPolicy.IsEligible(
            ratings[service.GetSelectedExercise(state, group).Id], zones)));
        Assert.Equal(5, service.GetActiveGroups(state).Count);
    }

    [Theory]
    [InlineData(ExerciseOutcome.TooEasy)]
    [InlineData(ExerciseOutcome.TooHard)]
    public void DifficultyResponsesRemoveFamilyKeepsWithoutDislikeScores(ExerciseOutcome outcome)
    {
        var (exercises, ratings, state) = DiscoveryFixture();
        string slot = MassGroupingTaxonomy.GetResolution(3).Groups[0].Id;
        state.KeptExerciseRootIdsBySelectionGroupId[slot].UnionWith([3,6]);
        state.WorkoutHistory[0].Decisions.Add(new() { RootExerciseId = 6, Outcome = outcome, DecidedAtUnixMilliseconds = 2 });
        string scores = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        var service = new ExerciseSessionService(exercises, new ZeroRandom(), () => Now, fitnessRatings: ratings);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.DoesNotContain(3, state.KeptExerciseRootIdsBySelectionGroupId[slot]);
        Assert.DoesNotContain(6, state.KeptExerciseRootIdsBySelectionGroupId[slot]);
        Assert.Equal(scores, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
    }

}
