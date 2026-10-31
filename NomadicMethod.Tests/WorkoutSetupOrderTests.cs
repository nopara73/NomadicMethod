using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutSetupOrderTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private const WorkoutModifiers Profile = WorkoutModifiers.Wall | WorkoutModifiers.Mirror |
        WorkoutModifiers.TallMirror | WorkoutModifiers.Floor | WorkoutModifiers.UpperBodyClothing |
        WorkoutModifiers.Elevation | WorkoutModifiers.RoomLocomotion;

    public static IEnumerable<object[]> Cases() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "setup-order-cases.json")))
        .RootElement.EnumerateArray().Select(item => new object[] { item.GetRawText() });

    [Theory]
    [MemberData(nameof(Cases))]
    public void BothPlatformsFollowTheSameSetupContract(string json)
    {
        SetupCase entry = JsonSerializer.Deserialize<SetupCase>(json, Options)!;
        Exercise[] exercises = entry.Exercises.Select(MakeExercise).ToArray();
        Dictionary<int, Exercise> byId = exercises.ToDictionary(e => e.Id);
        Exercise[] selected = exercises.Where(e => e.SequenceBlocks.Length > 0 &&
            e.Id != entry.CurrentId).ToArray();
        Exercise[] ordered = WorkoutSchedulePolicy.OrderBySetup(selected,
            e => e, e => e.Id, byId, entry.Modifiers, byId.GetValueOrDefault(entry.CurrentId));
        Assert.Equal(entry.Expected, ordered.Select(e => e.Id));
        Assert.Equal(selected.OrderBy(e => e.Id), ordered.OrderBy(e => e.Id));
        Assert.Equal(selected, exercises.Where(e => e.SequenceBlocks.Length > 0 && e.Id != entry.CurrentId));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(8, false)]
    [InlineData(8, true)]
    public void EquipmentGroupsStayContiguousAcrossDifferentSurfacesAndSpaces(
        int equipmentCount, bool hasCurrent)
    {
        // Two incomparable physical stations per equipment combination exercise
        // both exact routing (four stations) and its bounded fallback (sixteen).
        Exercise[] exercises = Enumerable.Range(0, equipmentCount * 2).Select(i =>
            MakeExercise(new ExerciseSpec
            {
                Id = i + 1, Wall = (i / 2 & 1) != 0, Mirror = (i / 2 & 2) != 0,
                Elevation = (i / 2 & 4) != 0, Demand = i % 2,
                Floor = i % 2 != 0, Soft = i % 2 != 0,
                Locomotion = i % 2 == 0 ? LocomotionSpace.Room : LocomotionSpace.None,
            })).ToArray();
        var byId = exercises.ToDictionary(e => e.Id);
        Exercise[] ordered = WorkoutSchedulePolicy.OrderBySetup(exercises,
            e => e, e => e.Id, byId, Profile, hasCurrent ? exercises[0] : null);
        int[] equipment = ordered.Select(e => (e.Id - 1) / 2).ToArray();
        int[] visits = equipment.Where((value, i) => i == 0 || value != equipment[i - 1]).ToArray();
        Assert.Equal(equipmentCount, visits.Length);
        Assert.Equal(equipmentCount, visits.Distinct().Count());
        Assert.Equal(exercises.OrderBy(e => e.Id), ordered.OrderBy(e => e.Id));
        if (hasCurrent) Assert.Equal(0, visits[0]);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("movement")]
    [InlineData("rest")]
    public void SetupOrderAndProgressSurviveColdReopen(string checkpoint)
    {
        Exercise[] catalog = Catalog();
        var service = new ExerciseSessionService(catalog, new Random(0));
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 15, Profile);
        WorkoutGroup current = service.GetNextGroup(state)!;
        if (checkpoint == "movement") service.BeginMovement(state, current, 20_000,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 20_000);
        if (checkpoint == "rest") service.BeginRest(state, current,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15_000);
        WorkoutGroup[] before = service.GetActiveGroups(state).ToArray();
        string log = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        service.RestoreAfterReopen(state);
        Assert.Equal(before, service.GetActiveGroups(state));
        Assert.Equal(log, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Equal(1, state.ActiveSetupOrderVersion);
        if (checkpoint == "movement")
        {
            Assert.Equal(current.Id, state.PendingMovementGroupId);
            Assert.Equal(20_000, state.PendingMovementMillisecondsRemaining);
            Assert.Equal(0, state.PendingMovementEndsAtUnixMilliseconds);
        }
    }

    [Fact]
    public void PreLoggingReadyWorkoutRetainsItsOldDemandOrder()
    {
        var service = new ExerciseSessionService(Catalog(), new Random(0));
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 15, Profile);
        state.ActiveSetupOrderVersion = 0;
        state.ActiveWorkoutSession = null;
        WorkoutGroup[] before = service.GetActiveGroups(state).ToArray();
        state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        service.RestoreAfterReopen(state);
        Assert.Equal(before, service.GetActiveGroups(state));
    }

    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), Options)!;

    private static Exercise MakeExercise(ExerciseSpec spec) => new()
    {
        Id = spec.Id, Name = $"Fixture {spec.Id}", Video = "fixture.mp4",
        PrimaryCanonicalGroup = CanonicalMuscleGroup.AbdominalWall, SecondaryCanonicalGroups = [],
        Practice = "Fixture", MotionProfile = "Fixture", Mode = ExerciseMode.Repetition,
        Presentation = ExercisePresentation.Motion, HoldFramePercent = 0,
        SideSequence = ExerciseSideSequence.Continuous,
        SequenceBlocks = (spec.Blocks ?? [spec.Id]).Select(id => new ExerciseSequenceBlock
            { ExerciseId = id, MirrorMedia = false }).ToArray(),
        MuscularDemand = spec.Demand, WallRequired = spec.Wall, SoleWallContactRequired = spec.SoleWall,
        ElevationRequired = spec.Elevation, JumpPlatformRequired = spec.JumpPlatform,
        MirrorRelationship = spec.Mirror ? ExerciseMirrorRelationship.BenefitsGreatly : ExerciseMirrorRelationship.Agnostic,
        MinimumMirrorCoverage = spec.Mirror ? ExerciseMirrorCoverage.FullBody : ExerciseMirrorCoverage.None,
        OnlyFeetTouchGround = !spec.Floor,
        FloorContactPoints = spec.Floor ? ExerciseFloorContact.Feet | ExerciseFloorContact.Hands : ExerciseFloorContact.Feet,
        HardFloorCompatibility = spec.Soft ? ExerciseHardFloorCompatibility.Incompatible : ExerciseHardFloorCompatibility.Compatible,
        LocomotionSpace = spec.Locomotion, ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "None", Silent = true,
    };

    private sealed class SetupCase
    {
        public string Name { get; set; } = "";
        public WorkoutModifiers Modifiers { get; set; }
        public int CurrentId { get; set; }
        public ExerciseSpec[] Exercises { get; set; } = [];
        public int[] Expected { get; set; } = [];
    }
    private sealed class ExerciseSpec
    {
        public int Id { get; set; }
        public int Demand { get; set; } = 1;
        public bool Wall { get; set; }
        public bool SoleWall { get; set; }
        public bool Mirror { get; set; }
        public bool Elevation { get; set; }
        public bool JumpPlatform { get; set; }
        public bool Floor { get; set; }
        public bool Soft { get; set; }
        public LocomotionSpace Locomotion { get; set; }
        public int[]? Blocks { get; set; }
    }
}
