using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class FloorContactPolicyTests
{
    public static IEnumerable<object[]> ContactProfiles()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "floor-contact-cases.json")));
        int[] profiles = document.RootElement.GetProperty("profiles").EnumerateArray()
            .Select(value => value.GetInt32()).ToArray();
        foreach (JsonElement entry in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            Enum.TryParse(entry.GetProperty("floorContactPoints").GetString(),
                out ExerciseFloorContact contacts);
            bool[] expected = entry.GetProperty("expected").EnumerateArray()
                .Select(value => value.GetBoolean()).ToArray();
            for (int index = 0; index < profiles.Length; index++)
                yield return [entry.GetProperty("name").GetString()!, contacts,
                    entry.GetProperty("onlyFeetTouchGround").GetBoolean(),
                    (WorkoutModifiers)profiles[index], expected[index]];
        }
    }

    [Theory]
    [MemberData(nameof(ContactProfiles))]
    public void PhysicalContactRulesRemainIndependent(string name,
        ExerciseFloorContact contacts, bool feetOnly, WorkoutModifiers profile, bool expected)
    {
        var exercise = new Exercise
        {
            Id = 1, Name = name, Video = "contract-fixture.mp4",
            PrimaryCanonicalGroup = CanonicalMuscleGroup.AbdominalWall,
            SecondaryCanonicalGroups = [], Practice = "Contract fixture",
            MotionProfile = "ContractFixture", Mode = default, Presentation = default,
            HoldFramePercent = 0, SideSequence = default,
            OnlyFeetTouchGround = feetOnly, FloorContactPoints = contacts,
            ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "None", Silent = true,
        };
        Assert.Equal(expected, FloorContactPolicy.IsCompatible(exercise, profile));
        Assert.Equal(expected, FloorContactPolicy.IsCompatible(exercise, profile |
            WorkoutModifiers.Insect | WorkoutModifiers.Silence | WorkoutModifiers.Shy));
    }

    private static Exercise FloorExercise(int id, bool wall = false, int family = 0,
        ExerciseFloorContact contacts = ExerciseFloorContact.Feet | ExerciseFloorContact.Hands,
        ExerciseUpperBodyClothingRequirement clothing = ExerciseUpperBodyClothingRequirement.Agnostic,
        ExerciseSequenceBlock[]? blocks = null, bool soles = false,
        LocomotionSpace locomotion = LocomotionSpace.None) => new()
    {
        Id = id, Name = $"Floor contract {id}", Video = $"fixture-{id}.mp4",
        PrimaryCanonicalGroup = CanonicalMuscleGroup.AbdominalWall,
        SecondaryCanonicalGroups = [], Practice = "Contract fixture", MotionProfile = "Fixture",
        Mode = default, Presentation = default, HoldFramePercent = 0, SideSequence = default,
        OnlyFeetTouchGround = contacts == ExerciseFloorContact.Feet, FloorContactPoints = contacts,
        ShoeAgnostic = true, LocomotionSpace = locomotion,
        MaxSpaceMeters = locomotion switch { LocomotionSpace.Room => 6, LocomotionSpace.Field => 50, _ => 2 },
        Equipment = "None", Silent = true,
        InsectCompatibility = ExerciseInsectCompatibility.Incompatible,
        HardFloorCompatibility = ExerciseHardFloorCompatibility.Incompatible,
        UpperBodyClothingRequirement = clothing, ShyCompatibility = ExerciseShyCompatibility.Compatible,
        MirrorRelationship = ExerciseMirrorRelationship.Agnostic, WallRequired = wall,
        SoleWallContactRequired = soles, SessionMovementId = family > 0 ? family : id,
        SequenceBlocks = blocks ?? [new() { ExerciseId = id, MirrorMedia = false }],
    };

    [Theory]
    [InlineData(200, false)]
    [InlineData(201, true)]
    [InlineData(202, true)]
    public void FloorMinimumCountsDistinctSelectableMovements(int count, bool enough)
    {
        Assert.Equal(enough, WorkoutModifierPolicy.FindFloorCatalogDeficiencies(
            Enumerable.Range(1, count).Select(id => FloorExercise(id)).ToArray()).Count == 0);
        Assert.Equal(1, Assert.Single(WorkoutModifierPolicy.FindFloorCatalogDeficiencies(
            Enumerable.Range(1, count).Select(id => FloorExercise(id, family: 1)).ToArray()))
            .MatchingSessionMovementCount);
    }

    [Theory]
    [InlineData(20, false)]
    [InlineData(21, true)]
    [InlineData(22, true)]
    public void FloorAndWallMinimumCountsMovementsRequiringBoth(int count, bool enough)
    {
        Assert.Equal(enough, WorkoutModifierPolicy.FindFloorAndWallCatalogDeficiencies(
            Enumerable.Range(1, count).Select(id => FloorExercise(id, wall: true)).ToArray()).Count == 0);
        Assert.Equal(1, Assert.Single(WorkoutModifierPolicy.FindFloorAndWallCatalogDeficiencies(
            Enumerable.Range(1, count).Select(id => FloorExercise(id, wall: true, family: 1)).ToArray()))
            .MatchingSessionMovementCount);
        Assert.Equal(0, Assert.Single(WorkoutModifierPolicy.FindFloorAndWallCatalogDeficiencies(
            Enumerable.Range(1, count).Select(id => FloorExercise(id)).ToArray())).MatchingSessionMovementCount);
    }

    [Fact]
    public void FloorAndLocomotionCountsRoomAndFieldOncePerWholeMovement()
    {
        foreach (int count in new[] { 20, 21, 22 })
        {
            Exercise[] movements = Enumerable.Range(1, count).Select(id =>
                FloorExercise(id, locomotion: id % 2 == 0 ? LocomotionSpace.Room : LocomotionSpace.Field)).ToArray();
            Assert.Equal(count >= 21,
                WorkoutModifierPolicy.FindFloorAndLocomotionCatalogDeficiencies(movements).Count == 0);
            Exercise[] aliases = Enumerable.Range(1, count).Select(id =>
                FloorExercise(id, family: 1, locomotion: LocomotionSpace.Room)).ToArray();
            Assert.Equal(1, Assert.Single(WorkoutModifierPolicy
                .FindFloorAndLocomotionCatalogDeficiencies(aliases)).MatchingSessionMovementCount);
        }
    }

    [Fact]
    public void FloorAndLocomotionMustOccurInTheSameMemberOfASelectableSequence()
    {
        ExerciseSequenceBlock[] blocks = [new() { ExerciseId = 1, MirrorMedia = false },
            new() { ExerciseId = 2, MirrorMedia = false }];
        Exercise floor = FloorExercise(1, blocks: blocks);
        Exercise travel = FloorExercise(2, contacts: ExerciseFloorContact.Feet,
            blocks: [], locomotion: LocomotionSpace.Room);
        Assert.Equal(0, Assert.Single(WorkoutModifierPolicy
            .FindFloorAndLocomotionCatalogDeficiencies([floor, travel])).MatchingSessionMovementCount);
        floor = FloorExercise(1, contacts: ExerciseFloorContact.Torso,
            clothing: ExerciseUpperBodyClothingRequirement.ClothingRequired,
            blocks: blocks, locomotion: LocomotionSpace.Room);
        travel = FloorExercise(2, clothing: ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired,
            blocks: [], locomotion: LocomotionSpace.Room);
        Assert.Equal(0, Assert.Single(WorkoutModifierPolicy
            .FindFloorAndLocomotionCatalogDeficiencies([floor, travel])).MatchingSessionMovementCount);
        Assert.Equal(0, Assert.Single(WorkoutModifierPolicy
            .FindFloorAndLocomotionCatalogDeficiencies([FloorExercise(1)])).MatchingSessionMovementCount);
    }

    [Fact]
    public void SeparateEquipmentMembersCannotFabricateTheCombinedRequirement()
    {
        ExerciseSequenceBlock[] blocks = [new() { ExerciseId = 1, MirrorMedia = false },
            new() { ExerciseId = 2, MirrorMedia = false }];
        Exercise floor = FloorExercise(1, blocks: blocks);
        Exercise wall = FloorExercise(2, wall: true, contacts: ExerciseFloorContact.Feet, blocks: []);
        Assert.Equal(1, Assert.Single(WorkoutModifierPolicy.FindFloorCatalogDeficiencies([floor, wall]))
            .MatchingSessionMovementCount);
        Assert.Equal(0, Assert.Single(WorkoutModifierPolicy.FindFloorAndWallCatalogDeficiencies([floor, wall]))
            .MatchingSessionMovementCount);
        floor = FloorExercise(1, contacts: ExerciseFloorContact.Torso,
            clothing: ExerciseUpperBodyClothingRequirement.ClothingRequired, blocks: blocks);
        wall = FloorExercise(2, wall: true, contacts: ExerciseFloorContact.Feet,
            clothing: ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired, blocks: []);
        Assert.Equal(0, Assert.Single(WorkoutModifierPolicy.FindFloorCatalogDeficiencies([floor, wall]))
            .MatchingSessionMovementCount);
    }

    [Fact]
    public void FloorLocomotionAdmissionsDoNotEnterAnUnfinishedEarlierWorkout()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), options)!;
        Assert.Equal(8, WorkoutCatalogPolicy.FloorLocomotionExerciseIds.Count);
        Assert.DoesNotContain(WorkoutCatalogPolicy.ForWorkout(catalog, 84),
            exercise => WorkoutCatalogPolicy.FloorLocomotionExerciseIds.Contains(exercise.Id));
        Assert.Equal(8, WorkoutCatalogPolicy.ForWorkout(catalog, 85).Count(
            exercise => WorkoutCatalogPolicy.FloorLocomotionExerciseIds.Contains(exercise.Id)));
        Exercise[] withOneRemoved = WorkoutCatalogPolicy.ForWorkout(catalog, 85).Where(exercise => exercise.Id != 1431).ToArray();
        WorkoutFloorCatalogDeficiency deficit = Assert.Single(WorkoutModifierPolicy
            .FindFloorAndLocomotionCatalogDeficiencies(withOneRemoved));
        Assert.Equal(20, deficit.MatchingSessionMovementCount);
        Assert.Equal(21, deficit.RequiredSessionMovementCount);
    }

    [Fact]
    public void BroadJumpBurpeeRequiresCoveredTorsoForItsChestToFloorContact()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };
        Exercise exercise = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), options)!
            .Single(exercise => exercise.Id == 1436);
        Assert.True(exercise.FloorContactPoints.HasFlag(ExerciseFloorContact.Torso));
        Assert.Equal(ExerciseUpperBodyClothingRequirement.ClothingRequired,
            exercise.UpperBodyClothingRequirement);
        WorkoutModifiers profile = WorkoutModifiers.Floor | WorkoutModifiers.RoomLocomotion;
        Assert.False(WorkoutModifierPolicy.IsCompatible(exercise, profile));
        Assert.True(WorkoutModifierPolicy.IsCompatible(exercise, profile | WorkoutModifiers.UpperBodyClothing));
    }

    [Fact]
    public void BothEquipmentSwitchesAndSolePermissionAreRequired()
    {
        Exercise exercise = FloorExercise(1, wall: true, soles: true);
        foreach (WorkoutModifiers floor in new[] { WorkoutModifiers.None, WorkoutModifiers.Floor })
        foreach (WorkoutModifiers wall in new[] { WorkoutModifiers.None, WorkoutModifiers.Wall })
        foreach (WorkoutModifiers soles in new[] { WorkoutModifiers.None, WorkoutModifiers.SoleWallContact })
            Assert.Equal(floor != 0 && wall != 0 && soles != 0,
                WorkoutModifierPolicy.IsCompatible(exercise, floor | wall | soles));
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("Movement")]
    [InlineData("Rest")]
    public void FloorExpansionPreservesAnUnfinishedVersion80Workout(string phase)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), options)!;
        DateTimeOffset now = new(2026, 10, 15, 6, 0, 0, TimeSpan.Zero);
        var state = new WorkoutState();
        var original = new ExerciseSessionService(catalog.Where(e => e.Id < 1057).ToArray(),
            new Random(12), () => now);
        original.Initialize(state);
        original.StartWorkout(state, 7, ExerciseSessionService.DefaultWorkoutModifiers);
        original.RecordOutcome(state, original.GetNextGroup(state)!, true);
        WorkoutGroup next = original.GetNextGroup(state)!;
        int exerciseId = original.GetSelectedExercise(state, next).Id;
        if (phase == "Movement") original.BeginMovement(state, next, 28_000, now.ToUnixTimeMilliseconds() + 28_000);
        if (phase == "Rest")
        {
            original.BeginRest(state, next, now.ToUnixTimeMilliseconds() + 14_000);
            state.PendingRestMillisecondsRemaining = 14_000;
        }
        state.CatalogRevision = 80;
        state.ActiveCatalogRevision = 80;
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance] = new() { [15] = -3 };
        long sessionId = state.ActiveWorkoutSession!.SessionId;
        string blocks = JsonSerializer.Serialize(state.ActiveWorkoutSession.Blocks);
        int[] keeps = state.LastKeptExerciseIds.Order().ToArray();
        for (int reopen = 1; reopen <= 2; reopen++)
        {
            state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
            var updated = new ExerciseSessionService(catalog, new Random(9), () => now.AddYears(reopen));
            updated.RestoreAfterReopen(state);
            Assert.Equal(exerciseId, updated.GetSelectedExercise(state, updated.GetNextGroup(state)!).Id);
            Assert.Equal(sessionId, state.ActiveWorkoutSession!.SessionId);
            Assert.Equal(blocks, JsonSerializer.Serialize(state.ActiveWorkoutSession.Blocks));
            Assert.Equal(keeps, state.LastKeptExerciseIds.Order());
            Assert.Equal(-3, state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance][15]);
            Assert.False(state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Floor));
            Assert.False(state.LastWorkoutModifiers.HasFlag(WorkoutModifiers.Floor));
            Assert.Equal(80, state.ActiveCatalogRevision);
            if (phase == "Movement") Assert.Equal(28_000, state.PendingMovementMillisecondsRemaining);
            if (phase == "Rest") Assert.Equal(14_000, state.PendingRestMillisecondsRemaining);
        }
    }

    [Theory]
    [InlineData(WorkoutModifiers.UpperBodyClothing, false)]
    [InlineData(WorkoutModifiers.UpperBodyClothing, true)]
    [InlineData(WorkoutModifiers.Floor, false)]
    [InlineData(WorkoutModifiers.Floor, true)]
    [InlineData(WorkoutModifiers.Floor | WorkoutModifiers.UpperBodyClothing | WorkoutModifiers.HardFloor, false)]
    [InlineData(WorkoutModifiers.Floor | WorkoutModifiers.UpperBodyClothing | WorkoutModifiers.HardFloor, true)]
    public void PhysicalSetupChangesReplaceIllegalWorkAndRetainEarnedProgress(
        WorkoutModifiers target, bool movement)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() } };
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), options)!;
        catalog.Single(e => e.Id == 1057).Score = 10_000;
        DateTimeOffset now = new(2026, 10, 15, 6, 0, 0, TimeSpan.Zero);
        var state = new WorkoutState();
        var service = new ExerciseSessionService(catalog, new Random(12), () => now);
        service.Initialize(state);
        service.StartWorkout(state, 3, WorkoutModifiers.Floor | WorkoutModifiers.UpperBodyClothing);
        Assert.Contains(service.GetActiveGroups(state), g => service.GetSelectedExercise(state, g).Id == 1057);
        while (service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id != 1057)
        {
            var prior = service.GetNextGroup(state)!;
            if (service.IsIntermediateSequenceBlock(state, prior)) service.AdvanceSequence(state, prior);
            else service.RecordOutcome(state, prior, true);
        }
        WorkoutGroup current = service.GetNextGroup(state)!;
        if (movement) service.BeginMovement(state, current, 28_000, now.ToUnixTimeMilliseconds() + 28_000);
        var completed = state.Outcomes.ToArray();
        int[] keeps = state.LastKeptExerciseIds.Order().ToArray();
        long sessionId = state.ActiveWorkoutSession!.SessionId;
        service.ReconfigureActiveWorkout(state, target, current.Id);
        Exercise replacement = service.GetSelectedExercise(state, service.GetNextGroup(state)!);
        Assert.NotEqual(1057, replacement.Id);
        Assert.True(WorkoutModifierPolicy.IsCompatible(replacement, target));
        Assert.Equal(sessionId, state.ActiveWorkoutSession!.SessionId);
        Assert.Equal(keeps, state.LastKeptExerciseIds.Order());
        Assert.All(completed, outcome => Assert.Equal(outcome.Value, state.Outcomes[outcome.Key]));
    }
}
