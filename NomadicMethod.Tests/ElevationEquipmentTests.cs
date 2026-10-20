using System.Text.Json;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ElevationEquipmentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EquipmentSurvivesStateCopiesAndReopenWithoutChangingWorkout(bool enabled)
    {
        var state = new WorkoutState
        {
            ElevationEquipmentEnabled = enabled,
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            LastWorkoutMinutes = 10,
            LastWorkoutModifiers = ExerciseSessionService.DefaultWorkoutModifiers,
            SelectedExerciseIds = new() { ["p146|r10.upper-limbs"] = 101 },
            KeptExerciseRootIdsBySelectionGroupId = new() { ["r10.upper-limbs"] = [101] },
            PendingMovementGroupId = "r10.upper-limbs",
            PendingMovementMillisecondsRemaining = 12_345,
        };

        string json = JsonSerializer.Serialize(state, JsonOptions);
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>(json, JsonOptions)!;
        new ExerciseSessionService([]).RestoreAfterReopen(restored);

        Assert.Equal(enabled, restored.ElevationEquipmentEnabled);
        Assert.Equal(state.LastWorkoutModifiers, restored.LastWorkoutModifiers);

        WorkoutState withoutElevation = JsonSerializer.Deserialize<WorkoutState>(json, JsonOptions)!;
        withoutElevation.ElevationEquipmentEnabled = false;
        new ExerciseSessionService([]).RestoreAfterReopen(withoutElevation);
        restored.ElevationEquipmentEnabled = false;
        Assert.Equal(JsonSerializer.Serialize(withoutElevation, JsonOptions),
            JsonSerializer.Serialize(restored, JsonOptions));
    }

    [Fact]
    public void ExistingStateDefaultsElevationToOff()
    {
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>("{\"version\":30}", JsonOptions)!;
        Assert.False(restored.ElevationEquipmentEnabled);
    }
}
