using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class HardFloorLocomotionReviewTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
    private const WorkoutModifiers Profile = WorkoutModifiers.Floor | WorkoutModifiers.HardFloor |
        WorkoutModifiers.RoomLocomotion | WorkoutModifiers.Wall | WorkoutModifiers.SoleWallContact;
    private static string Asset(string name) => Path.Combine(AppContext.BaseDirectory, "Assets", name);
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(Asset("exercises.json")), Options)!;
    private static JsonDocument Review() => JsonDocument.Parse(File.ReadAllText(Asset("hard-floor-locomotion-review.json")));

    [Fact]
    public void EveryReviewedMovementRetainsItsContactsAndExactPhysicalVerdict()
    {
        Exercise[] catalog = Catalog();
        var byId = catalog.ToDictionary(exercise => exercise.Id);
        using JsonDocument review = Review();
        JsonElement[] entries = review.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(44, entries.Length);
        Assert.Equal(44, entries.Select(entry => WorkoutModifierPolicy.GetSessionMovementId(byId[entry.GetProperty("id").GetInt32()])).Distinct().Count());
        Assert.Equal(11, entries.Count(entry => entry.GetProperty("compatibility").GetString() == "Compatible"));
        foreach (JsonElement entry in entries)
        {
            Exercise exercise = byId[entry.GetProperty("id").GetInt32()];
            bool compatible = entry.GetProperty("compatibility").GetString() == "Compatible";
            Assert.Equal(ExerciseNamePolicy.DisplayName(exercise.Id,
                entry.GetProperty("name").GetString()!), exercise.Name);
            Assert.Equal(Enum.Parse<ExerciseFloorContact>(entry.GetProperty("floorContactPoints").GetString()!), exercise.FloorContactPoints);
            Assert.Equal(compatible ? ExerciseHardFloorCompatibility.Compatible : ExerciseHardFloorCompatibility.Incompatible, exercise.HardFloorCompatibility);
            Assert.Equal(compatible, WorkoutModifierPolicy.IsCompatible(exercise, Profile));
            Assert.True(WorkoutModifierPolicy.IsCompatible(exercise, (Profile & ~WorkoutModifiers.HardFloor) | WorkoutModifiers.UpperBodyClothing));
            if (compatible)
                Assert.Equal(exercise.OnlyFeetTouchGround, WorkoutModifierPolicy.IsCompatible(exercise, Profile & ~WorkoutModifiers.Floor));
        }
        Assert.Equal(18, catalog.Where(exercise => exercise.SequenceBlocks.Length > 0 &&
                exercise.LocomotionSpace == LocomotionSpace.Room && exercise.SequenceBlocks.All(block =>
                    WorkoutModifierPolicy.IsCompatible(byId[block.ExerciseId], Profile)))
            .Select(WorkoutModifierPolicy.GetSessionMovementId).Distinct().Count());
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("Movement")]
    [InlineData("Rest")]
    public void SurfaceMetadataUpgradePreservesExistingWorkoutAndFeedback(string phase)
    {
        Exercise[] catalog = Catalog();
        using JsonDocument review = Review();
        HashSet<int> changedIds = review.RootElement.GetProperty("entries").EnumerateArray()
            .Where(entry => entry.GetProperty("compatibility").GetString() == "Compatible")
            .Select(entry => entry.GetProperty("id").GetInt32()).ToHashSet();
        JsonArray oldRecords = JsonNode.Parse(File.ReadAllText(Asset("exercises.json")))!.AsArray();
        foreach (JsonNode? record in oldRecords)
            if (changedIds.Contains(record!["id"]!.GetValue<int>()))
                record["hardFloorCompatibility"] = "Incompatible";
        Exercise[] previousCatalog = JsonSerializer.Deserialize<Exercise[]>(oldRecords.ToJsonString(), Options)!;
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups.Single(group =>
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.AbdominalWall));
        var state = new WorkoutState
        {
            CatalogRevision = 100, ActiveCatalogRevision = 100, ActiveWorkoutMinutes = 30,
            ActiveWorkoutModifiers = Profile, LastWorkoutModifiers = Profile,
            ActiveDurationSelectionGroupIds = [group.Id], ActiveSelectionGroupOrder = [group.Id],
            ActiveSetupOrderVersion = 1,
            SelectedExerciseIds = new() { [$"p{(int)Profile}|{group.Id}"] = 1223 },
            PendingScoreUpdates = new() { [1223] = 7 }, LastKeptExerciseIds = [1223],
            KeptExerciseRootIdsBySelectionGroupId = new() { [group.Id] = [1223] },
        };
        DateTimeOffset now = new(2026, 10, 31, 5, 0, 0, TimeSpan.Zero);
        var oldService = new ExerciseSessionService(previousCatalog, new Random(5), () => now);
        oldService.Initialize(state);
        WorkoutGroup next = oldService.GetNextGroup(state)!;
        if (phase == "Movement") oldService.BeginMovement(state, next, 32000, now.ToUnixTimeMilliseconds() + 32000);
        if (phase == "Rest") oldService.BeginRest(state, next, now.ToUnixTimeMilliseconds() + 15000);
        state.CatalogRevision = 100;
        WorkoutState saved = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state, Options), Options)!;
        var restoredService = new ExerciseSessionService(catalog, new Random(8), () => now);
        restoredService.Initialize(state);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
        Assert.Equal(100, state.ActiveCatalogRevision);
        Assert.Equal(JsonSerializer.Serialize(saved.SelectedExerciseIds, Options), JsonSerializer.Serialize(state.SelectedExerciseIds, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.ActiveSelectionGroupOrder, Options), JsonSerializer.Serialize(state.ActiveSelectionGroupOrder, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.ActiveSetCountsBySelectionGroupId, Options), JsonSerializer.Serialize(state.ActiveSetCountsBySelectionGroupId, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.PendingScoreUpdates, Options), JsonSerializer.Serialize(state.PendingScoreUpdates, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.KeptExerciseRootIdsBySelectionGroupId, Options), JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.WorkoutHistory, Options), JsonSerializer.Serialize(state.WorkoutHistory, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.ActiveWorkoutSession, Options), JsonSerializer.Serialize(state.ActiveWorkoutSession, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.Outcomes, Options), JsonSerializer.Serialize(state.Outcomes, Options));
        Assert.Equal(saved.PendingMovementGroupId, state.PendingMovementGroupId);
        Assert.Equal(saved.PendingRestGroupId, state.PendingRestGroupId);
        Assert.Equal(JsonSerializer.Serialize(saved.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle, Options), JsonSerializer.Serialize(state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle, Options));
        Assert.Equal(JsonSerializer.Serialize(saved.LastHardWorkUnixMillisecondsByPrimaryMuscle, Options), JsonSerializer.Serialize(state.LastHardWorkUnixMillisecondsByPrimaryMuscle, Options));
    }
}
