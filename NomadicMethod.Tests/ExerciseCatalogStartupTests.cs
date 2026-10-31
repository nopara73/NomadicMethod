using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using NomadicMethod.Data;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ExerciseCatalogStartupTests
{
    private static string Asset(string name) => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", name));

    private static Exercise[] ReadCatalog(JsonNode? changed = null) =>
        JsonSerializer.Deserialize(changed?.ToJsonString() ?? Asset("exercises.json"),
            ExerciseCatalogJsonContext.Default.ExerciseArray)!;

    [Fact]
    public void BundledCatalogPassesTheActualAndroidStartupValidator()
    {
        ExerciseCatalogValidator.Validate(ReadCatalog(), requireInitialScores: true);
    }

    [Fact]
    public void SharedDisplayTitlesKeepDistinctExerciseIdentities()
    {
        using JsonDocument fixture = JsonDocument.Parse(Asset("catalog-display-title-case.json"));
        int[] ids = fixture.RootElement.GetProperty("exerciseIds").EnumerateArray().Select(id => id.GetInt32()).ToArray();
        string title = fixture.RootElement.GetProperty("title").GetString()!;
        JsonArray catalog = JsonNode.Parse(Asset("exercises.json"))!.AsArray();
        foreach (JsonNode? entry in catalog.Where(entry => ids.Contains(entry!["id"]!.GetValue<int>())))
            entry!["name"] = title;
        Exercise[] exercises = ReadCatalog(catalog);

        ExerciseCatalogValidator.Validate(exercises, requireInitialScores: true);

        Assert.Equal(ids.Order(), exercises.Where(exercise => ids.Contains(exercise.Id)).Select(exercise => exercise.Id).Order());
        Assert.All(exercises.Where(exercise => ids.Contains(exercise.Id)), exercise => Assert.Equal(title, exercise.Name));
    }

    [Theory]
    [InlineData("id", "duplicate exercise IDs")]
    [InlineData("video", "duplicate media")]
    [InlineData("name", "exercise metadata")]
    public void StartupStillRejectsBrokenIdentitiesMediaAndEmptyTitles(string property, string issue)
    {
        JsonArray catalog = JsonNode.Parse(Asset("exercises.json"))!.AsArray();
        catalog[1]![property] = property == "name" ? JsonValue.Create(" ") : catalog[0]![property]!.DeepClone();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ExerciseCatalogValidator.Validate(ReadCatalog(catalog), requireInitialScores: true));

        Assert.Contains(issue, error.Message);
    }

    [Fact]
    public void BundledScoresMustBeNeutralWhileStoredScoresRemainValid()
    {
        Exercise[] catalog = ReadCatalog();
        catalog[0].Score = -3;
        Assert.Contains("initial scores", Assert.Throws<InvalidOperationException>(() =>
            ExerciseCatalogValidator.Validate(catalog, requireInitialScores: true)).Message);
        ExerciseCatalogValidator.Validate(catalog, requireInitialScores: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshAndUpgradedSchemasPreserveIndependentScoresForSharedTitles(bool upgrade)
    {
        using JsonDocument fixture = JsonDocument.Parse(Asset("catalog-display-title-case.json"));
        int[] ids = fixture.RootElement.GetProperty("exerciseIds").EnumerateArray().Select(id => id.GetInt32()).ToArray();
        int[] scores = fixture.RootElement.GetProperty("scores").EnumerateArray().Select(score => score.GetInt32()).ToArray();
        string title = fixture.RootElement.GetProperty("title").GetString()!;
        string[] identities = ids.Select(id => ExerciseNamePolicy.IdentityName(id, title)).ToArray();
        using var database = new SqliteConnection("Data Source=:memory:");
        database.Open();
        Execute(database, ExerciseDatabaseMigrationSql.CreateExerciseTable);
        for (int index = 0; index < ids.Length; index++)
        {
            using SqliteCommand insert = database.CreateCommand();
            insert.CommandText = """
                INSERT INTO exercises (id, name, video, practice, motion_profile, score, muscular_demand)
                VALUES ($id, $name, $video, 'Test', 'Test', $score, 0)
                """;
            insert.Parameters.AddWithValue("$id", ids[index]);
            insert.Parameters.AddWithValue("$name", upgrade ? $"Legacy title {index}" : identities[index]);
            insert.Parameters.AddWithValue("$video", $"exercise_{ids[index]}.mp4");
            insert.Parameters.AddWithValue("$score", scores[index]);
            insert.ExecuteNonQuery();
        }
        if (upgrade)
        {
            Execute(database, ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);
            Execute(database, ExerciseDatabaseMigrationSql.CopyExistingExercisesWithNeutralCatalogMetadata);
            Execute(database, "DROP TABLE exercises");
            Execute(database, ExerciseDatabaseMigrationSql.RenameRebuiltExerciseTable);
            for (int index = 0; index < ids.Length; index++)
            {
                using SqliteCommand rename = database.CreateCommand();
                rename.CommandText = "UPDATE exercises SET name = $identity WHERE id = $id";
                rename.Parameters.AddWithValue("$identity", identities[index]);
                rename.Parameters.AddWithValue("$id", ids[index]);
                rename.ExecuteNonQuery();
            }
        }
        using SqliteCommand query = database.CreateCommand();
        query.CommandText = "SELECT id, name, score FROM exercises ORDER BY id";
        using SqliteDataReader rows = query.ExecuteReader();
        for (int index = 0; index < ids.Length; index++)
        {
            Assert.True(rows.Read());
            Assert.Equal(ids[index], rows.GetInt32(0));
            Assert.Equal(identities[index], rows.GetString(1));
            Assert.Equal(title, ExerciseNamePolicy.DisplayName(ids[index], rows.GetString(1)));
            Assert.Equal(scores[index], rows.GetInt32(2));
        }
        Assert.False(rows.Read());
    }

    private static void Execute(SqliteConnection database, string sql)
    {
        using SqliteCommand command = database.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
