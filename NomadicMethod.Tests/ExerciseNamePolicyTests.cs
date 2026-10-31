using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ExerciseNamePolicyTests
{
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        })!;

    [Fact]
    public void SharedReviewedAliasesPreserveEveryExistingCatalogIdentity()
    {
        Exercise[] catalog = Catalog();
        Dictionary<int, Exercise> byId = catalog.ToDictionary(e => e.Id);
        Dictionary<int, StoredExerciseSnapshot> stored = catalog.ToDictionary(
            e => e.Id, e => new StoredExerciseSnapshot(e.Name, e.Video, -e.Id));
        using Stream stream = typeof(ExerciseNamePolicy).Assembly
            .GetManifestResourceStream("NomadicMethod.ExerciseNames")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement names = document.RootElement.GetProperty("names");
        Assert.Equal(709, names.GetArrayLength());
        foreach (JsonElement entry in names.EnumerateArray())
        {
            int id = entry.GetProperty("id").GetInt32();
            string identity = entry.GetProperty("identityName").GetString()!;
            string name = entry.GetProperty("name").GetString()!;
            Assert.Equal(name, byId[id].Name);
            foreach (string alias in new[] { identity, name }.Concat(entry.GetProperty("previousNames")
                         .EnumerateArray().Select(alias => alias.GetString()!)))
            {
                Assert.Equal(identity, ExerciseNamePolicy.IdentityName(id, alias));
                Assert.Equal(name, ExerciseNamePolicy.DisplayName(id, alias));
            }
            Assert.Equal("Unreviewed movement", ExerciseNamePolicy.IdentityName(id, "Unreviewed movement"));
            stored[id] = stored[id] with { Name = identity };
        }
        Assert.Equal(stored.Keys.Order(), CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored).Order());
        Assert.All(stored, pair => Assert.Equal(-pair.Key, pair.Value.Score));
        stored[1255] = stored[1255] with { Name = "Unknown push-up" };
        Assert.Throws<InvalidOperationException>(() => CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored));
        stored[1255] = new("Narrow-Hand Push-Ups", "other.mp4", -4);
        Assert.Throws<InvalidOperationException>(() => CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored));
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("movement")]
    [InlineData("rest")]
    public void RenamesRetainUnfinishedWorkoutCheckpoints(string phase)
    {
        Exercise[] catalog = Catalog();
        Exercise exercise = catalog.Single(e => e.Id == 1255);
        string group = MassGroupingTaxonomy.GetResolution(30).Groups.Single(
            g => g.CanonicalGroups.Contains(exercise.PrimaryCanonicalGroup)).Id;
        var state = new WorkoutState
        {
            CatalogRevision = 103,
            ActiveCatalogRevision = 103,
            ActiveWorkoutMinutes = 30,
            ActiveWorkoutModifiers = WorkoutModifiers.Floor,
            LastWorkoutModifiers = WorkoutModifiers.Floor,
            SelectedExerciseIds = new() { [group] = 1255 },
            KeptExerciseRootIdsBySelectionGroupId = new() { [group] = [1255] },
            LastKeptExerciseIds = [1255],
        };
        if (phase == "movement")
        {
            state.PendingMovementGroupId = group;
            state.PendingMovementMillisecondsRemaining = 31_123;
            state.PendingMovementPausedByUser = true;
        }
        if (phase == "rest")
        {
            state.PendingRestGroupId = group;
            state.PendingRestMillisecondsRemaining = 31_123;
            state.PendingRestPausedByUser = true;
        }
        string before = JsonSerializer.Serialize(state);
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state, catalog.ToDictionary(e => e.Id)));
        state.CatalogRevision = 103;
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }
}
