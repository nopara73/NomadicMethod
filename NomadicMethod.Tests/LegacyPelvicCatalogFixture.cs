using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NomadicMethod.Models;

namespace NomadicMethod.Tests;

// Deliberately retain the older sparse physical pool for fallback regressions.
// Genuine current-catalog coverage is checked by WorkoutVarietyPolicyTests.
internal static class LegacyPelvicCatalogFixture
{
    public static Exercise[] Load()
    {
        JsonArray catalog = JsonNode.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")))!.AsArray();
        var prior = new JsonArray(catalog.Where(entry => entry!["id"]!.GetValue<int>() < 1730)
            .Select(entry => entry!.DeepClone()).ToArray());
        prior.Single(entry => entry!["id"]!.GetValue<int>() == 1069)!["hardFloorCompatibility"] = "Incompatible";
        return prior.Deserialize<Exercise[]>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        })!;
    }
}

