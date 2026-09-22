using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class LegacyWorkoutStateMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void SerializedVersionFourStateRetainsHistoryAndScoreJournal()
    {
        const string json =
            """
            {
              "version": 4,
              "selectedExercises": {
                "Glutes": "Existing movement",
                "Core": "Pending movement"
              },
              "outcomes": {
                "Glutes": "Tick"
              },
              "pendingRestMuscleGroup": "Core",
              "pendingRestEndsAtUnixMilliseconds": 123456,
              "pendingRestKept": false,
              "pendingScoreExerciseId": 27,
              "pendingScoreValue": -3,
              "lastWorkoutMinutes": 4,
              "activeWorkoutMinutes": 4,
              "workoutCompleted": false,
              "completionAcknowledged": false
            }
            """;

        LegacyWorkoutState legacy = JsonSerializer.Deserialize<LegacyWorkoutState>(
                json,
                JsonOptions)
            ?? throw new InvalidOperationException("Legacy state did not deserialize.");

        WorkoutState migrated = LegacyWorkoutStateMigration.Migrate(legacy);

        Assert.Equal(4, migrated.Version);
        Assert.Equal("Existing movement", migrated.LegacySelectedExerciseNames["Glutes"]);
        Assert.Equal("Pending movement", migrated.LegacySelectedExerciseNames["Core"]);
        Assert.Equal(ExerciseOutcome.Tick, migrated.LegacyOutcomes["Glutes"]);
        Assert.Equal("Core", migrated.LegacyPendingRestGroup);
        Assert.Equal(123456, migrated.PendingRestEndsAtUnixMilliseconds);
        Assert.False(migrated.PendingRestKept);
        Assert.Equal(27, migrated.PendingScoreExerciseId);
        Assert.Equal(-3, migrated.PendingScoreValue);
        Assert.Equal(4, migrated.LastWorkoutMinutes);
        Assert.Equal(4, migrated.ActiveWorkoutMinutes);
    }
}
