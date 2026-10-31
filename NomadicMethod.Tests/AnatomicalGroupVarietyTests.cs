using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;
namespace NomadicMethod.Tests;
public sealed class AnatomicalGroupVarietyTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
    private static JsonArray CatalogJson() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")))!.AsArray();
    [Fact]
    public void SharedCounterCasesRejectDuplicatesPartialSequencesAndImpossibleSetups()
    {
        JsonArray catalog = CatalogJson();
        JsonArray cases = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Assets", "anatomical-variety-cases.json")))!.AsArray();
        foreach (JsonNode? fixture in cases)
        {
            int templateId = fixture!["templateId"]!.GetValue<int>();
            JsonNode template = catalog.Single(e => e!["id"]!.GetValue<int>() == templateId)!;
            Exercise[] exercises = fixture["exercises"]!.AsArray().Select(spec =>
            {
                JsonObject clone = template.DeepClone().AsObject();
                foreach (var property in spec!.AsObject())
                    if (property.Key != "sequenceIds") clone[property.Key] = property.Value?.DeepClone();
                clone["sequenceBlocks"] = new JsonArray(spec["sequenceIds"]!.AsArray().Select(id =>
                    (JsonNode)new JsonObject { ["exerciseId"] = id!.GetValue<int>(),
                        ["sideCue"] = "None", ["directionCue"] = "None",
                        ["mirrorMedia"] = false, ["mediaSegment"] = "Full" }).ToArray());
                return clone.Deserialize<Exercise>(Options)!;
            }).ToArray();
            var inventory = WorkoutModifierPolicy.GetAnatomicalGroupVarietyInventory(exercises);
            Assert.Equal(90, inventory.Count);
            string groupId = fixture["groupId"]!.GetValue<string>();
            var row = inventory.Single(row => row.GroupId == groupId);
            int expected = fixture["expectedCount"]!.GetValue<int>();
            Assert.True(row.MatchingExerciseCount == expected, fixture["name"]!.GetValue<string>());
            Assert.Equal(10, row.RequiredExerciseCount);
            Assert.Equal(expected < 10, WorkoutModifierPolicy.FindAnatomicalGroupVarietyDeficiencies(exercises)
                .Any(row => row.GroupId == groupId));
        }
    }
    [Fact]
    public void EveryExistingGroupAndSubgroupHasTenSelectableCatalogMovements()
    {
        Exercise[] catalog = CatalogJson().Deserialize<Exercise[]>(Options)!;
        Assert.Equal(10, WorkoutModifierPolicy.MinimumSessionMovementsPerAnatomicalGroup);
        Assert.Equal(90, WorkoutModifierPolicy.GetAnatomicalGroupVarietyInventory(catalog).Count);
        Assert.Empty(WorkoutModifierPolicy.FindAnatomicalGroupVarietyDeficiencies(catalog));
    }

    [Fact]
    public void NewVarietyAdmissionsStayOutOfFrozenEarlierWorkoutCatalogs()
    {
        Exercise[] catalog = CatalogJson().Deserialize<Exercise[]>(Options)!;
        int[] additions = Enumerable.Range(1720, 10).ToArray();
        Assert.Equal(additions, WorkoutCatalogPolicy.AnatomicalVarietyExerciseIds.Order());
        Assert.DoesNotContain(WorkoutCatalogPolicy.ForWorkout(catalog, 97), e => additions.Contains(e.Id));
        Assert.Equal(920, WorkoutCatalogPolicy.ForWorkout(catalog, 97).Count);
        Assert.All(additions, id => Assert.Contains(WorkoutCatalogPolicy.ForWorkout(catalog, 98), e => e.Id == id));
    }
}
