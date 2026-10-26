using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ElevationEquipmentTests
{
    [Fact]
    public void ReviewedCatalogContainsTheThreeDisjointPoolsAndExactExecutionContracts()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), options)!;
        using JsonDocument review = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "elevation-admission.json")));
        Assert.Equal(35, catalog.Count(e => e.ElevationRequired));
        Assert.Empty(WorkoutModifierPolicy.FindElevationCatalogDeficiencies(catalog));
        Assert.True(catalog.Count(e => e.WallRequired && !e.OnlyFeetTouchGround) >= 5);
        foreach (JsonElement item in review.RootElement.EnumerateArray())
        {
            Exercise exercise = Assert.Single(catalog, e => e.Id == item.GetProperty("id").GetInt32());
            Assert.Equal(item.GetProperty("name").GetString(), exercise.Name);
            Assert.Equal(item.GetProperty("primary").GetString(), exercise.PrimaryCanonicalGroup.ToString());
            Assert.Equal(item.GetProperty("side").GetString(), exercise.SideSequence.ToString());
            Assert.Equal(item.GetProperty("floor").GetBoolean(), !exercise.OnlyFeetTouchGround);
            Assert.Equal(item.GetProperty("platform").GetBoolean(), exercise.JumpPlatformRequired);
            Assert.Equal(item.GetProperty("hold").GetBoolean(), exercise.Mode == ExerciseMode.Hold);
            Assert.True(exercise.ElevationRequired);
            WorkoutModifiers profile = WorkoutModifiers.UpperBodyClothing | WorkoutModifiers.Floor;
            Assert.False(WorkoutModifierPolicy.IsCompatible(exercise, profile));
            Assert.Equal(!exercise.JumpPlatformRequired, WorkoutModifierPolicy.IsCompatible(exercise,
                profile | WorkoutModifiers.Elevation));
            Assert.True(WorkoutModifierPolicy.IsCompatible(exercise,
                profile | WorkoutModifiers.Elevation | WorkoutModifiers.JumpPlatform));
        }
    }

    private static Exercise Movement(int id, bool elevation = true, bool platform = false,
        ExerciseFloorContact contacts = ExerciseFloorContact.Feet, int family = 0) => new()
    {
        Id = id, Name = $"Elevation contract {id}", Video = $"fixture-{id}.mp4",
        PrimaryCanonicalGroup = CanonicalMuscleGroup.AbdominalWall,
        SecondaryCanonicalGroups = [], Practice = "Contract fixture", MotionProfile = "Fixture",
        Mode = default, Presentation = default, HoldFramePercent = 0, SideSequence = default,
        OnlyFeetTouchGround = contacts == ExerciseFloorContact.Feet, FloorContactPoints = contacts,
        ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "None", Silent = true,
        InsectCompatibility = ExerciseInsectCompatibility.Incompatible,
        HardFloorCompatibility = ExerciseHardFloorCompatibility.Compatible,
        UpperBodyClothingRequirement = ExerciseUpperBodyClothingRequirement.Agnostic,
        ShyCompatibility = ExerciseShyCompatibility.Compatible,
        MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
        ElevationRequired = elevation, JumpPlatformRequired = platform,
        SessionMovementId = family > 0 ? family : id,
        SequenceBlocks = [new() { ExerciseId = id, MirrorMedia = false }],
    };

    [Theory]
    [InlineData(ElevationEquipment.None, false, false)]
    [InlineData(ElevationEquipment.Support, true, false)]
    [InlineData(ElevationEquipment.JumpPlatform, true, true)]
    public void CapabilitiesAreNestedAndOtherEquipmentSurvives(
        ElevationEquipment equipment, bool support, bool platform)
    {
        WorkoutModifiers baseline = WorkoutModifiers.Wall | WorkoutModifiers.SoleWallContact |
            WorkoutModifiers.Floor | WorkoutModifiers.Silence;
        WorkoutModifiers profile = WorkoutModifierPolicy.WithElevationEquipment(baseline, equipment);
        Assert.Equal(equipment, WorkoutModifierPolicy.GetElevationEquipment(profile));
        Assert.Equal(baseline, profile & baseline);
        Assert.True(WorkoutModifierPolicy.IsCompatible(Movement(1, elevation: false), profile));
        Assert.Equal(support, WorkoutModifierPolicy.IsCompatible(Movement(2), profile));
        Assert.Equal(platform, WorkoutModifierPolicy.IsCompatible(Movement(3, platform: true), profile));
        Assert.Equal(ElevationEquipment.None, WorkoutModifierPolicy.GetElevationEquipment(
            WorkoutModifierPolicy.WithElevationEquipment(profile, ElevationEquipment.None)));
    }

    [Fact]
    public void FloorPermissionClothingAndSurfaceRulesRemainIndependent()
    {
        WorkoutModifiers profile = WorkoutModifierPolicy.WithElevationEquipment(
            WorkoutModifiers.None, ElevationEquipment.Support);
        Exercise hands = Movement(1, contacts: ExerciseFloorContact.Hands);
        Exercise torso = Movement(2, contacts: ExerciseFloorContact.Torso | ExerciseFloorContact.Shoulders);
        Assert.False(WorkoutModifierPolicy.IsCompatible(hands, profile));
        Assert.True(WorkoutModifierPolicy.IsCompatible(hands, profile | WorkoutModifiers.Floor));
        Assert.False(WorkoutModifierPolicy.IsCompatible(torso, profile | WorkoutModifiers.Floor));
        Assert.True(WorkoutModifierPolicy.IsCompatible(torso,
            profile | WorkoutModifiers.Floor | WorkoutModifiers.UpperBodyClothing));
        Assert.False(WorkoutModifierPolicy.IsCompatible(torso, profile | WorkoutModifiers.Floor |
            WorkoutModifiers.UpperBodyClothing | WorkoutModifiers.HardFloor));
        Assert.Equal(WorkoutModifiers.Silence, WorkoutModifierPolicy.Normalize(
            WorkoutModifiers.Silence | WorkoutModifiers.JumpPlatform));
    }

    [Fact]
    public void LegacyOnBecomesSupportAndNeverChangesAnActiveWorkout()
    {
        var state = new WorkoutState { ElevationEquipmentEnabled = true,
            LastWorkoutModifiers = WorkoutModifiers.Silence,
            ActiveWorkoutModifiers = WorkoutModifiers.Wall, ActiveWorkoutMinutes = 10,
            PendingMovementGroupId = "r10.upper-limbs", PendingMovementMillisecondsRemaining = 12_345,
            KeptExerciseRootIdsBySelectionGroupId = new() { ["r10.upper-limbs"] = [701] } };
        WorkoutModifierPolicy.MigrateLegacyElevationSetup(state);
        Assert.Equal(ElevationEquipment.Support, WorkoutModifierPolicy.GetElevationEquipment(state.LastWorkoutModifiers));
        Assert.False(state.ElevationEquipmentEnabled);
        Assert.Equal(WorkoutModifiers.Wall, state.ActiveWorkoutModifiers);
        Assert.Equal(12_345, state.PendingMovementMillisecondsRemaining);
        Assert.Contains(701, state.KeptExerciseRootIdsBySelectionGroupId["r10.upper-limbs"]);
        string serialized = JsonSerializer.Serialize(state);
        WorkoutModifierPolicy.MigrateLegacyElevationSetup(state);
        Assert.Equal(serialized, JsonSerializer.Serialize(state));
        Assert.Equal(ElevationEquipment.None, WorkoutModifierPolicy.GetElevationEquipment(new WorkoutState().LastWorkoutModifiers));
    }

    [Fact]
    public void CatalogFloorsCountSeparateMovementIdentitiesWithoutAliasInflation()
    {
        List<Exercise> catalog = Enumerable.Range(1, 19).Select(id => Movement(id)).ToList();
        catalog.AddRange(Enumerable.Range(20, 5).Select(id => Movement(id, platform: true)));
        catalog.AddRange(Enumerable.Range(25, 5).Select(id => Movement(id, contacts: ExerciseFloorContact.Hands)));
        Assert.Equal("Support", Assert.Single(WorkoutModifierPolicy.FindElevationCatalogDeficiencies(catalog)).Pool);
        catalog.Add(Movement(30, family: 1));
        Assert.Single(WorkoutModifierPolicy.FindElevationCatalogDeficiencies(catalog));
        catalog.Add(Movement(31));
        Assert.Empty(WorkoutModifierPolicy.FindElevationCatalogDeficiencies(catalog));
        catalog.RemoveAll(e => e.Id == 29);
        catalog.Add(Movement(32, contacts: ExerciseFloorContact.Hands, family: 1));
        Assert.Equal("FloorAndElevation", Assert.Single(
            WorkoutModifierPolicy.FindElevationCatalogDeficiencies(catalog)).Pool);
    }

    [Fact]
    public void OldUnfinishedWorkoutsKeepTheirOriginalCatalog()
    {
        Exercise[] catalog = [Movement(1, elevation: false), Movement(2)];
        Assert.Single(WorkoutCatalogPolicy.ForWorkout(catalog, 82));
        Assert.Single(WorkoutCatalogPolicy.ForWorkout(catalog, 83));
        Assert.Equal(2, WorkoutCatalogPolicy.ForWorkout(catalog, 84).Count);
    }
}
