using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class RegionalCompoundCoverageTests
{
    [Fact]
    public void ReviewedCompoundMovementsAndIsolationCasesMatchBothPlatforms()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };
        string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        Dictionary<int, Exercise> catalog = JsonSerializer.Deserialize<Exercise[]>(
            File.ReadAllText(Path.Combine(assets, "exercises.json")), options)!
            .ToDictionary(exercise => exercise.Id);
        CoverageCase[] cases = JsonSerializer.Deserialize<CoverageCase[]>(
            File.ReadAllText(Path.Combine(assets, "regional-compound-cases.json")), options)!;
        foreach (CoverageCase item in cases)
        {
            WorkoutGroup group = MassGroupingTaxonomy.GetGroup(item.Minutes, item.Group);
            foreach (int id in item.Accepted)
            {
                Assert.True(WorkoutCoveragePolicy.IsSelectable(catalog[id], group),
                    $"{id} {catalog[id].Name} should qualify for {item.Group}");
                Assert.True(WorkoutSequencePolicy.IsSelectable(catalog[id], catalog, group));
            }
            foreach (int id in item.Rejected)
            {
                Assert.False(WorkoutCoveragePolicy.IsSelectable(catalog[id], group),
                    $"{id} {catalog[id].Name} should not qualify for {item.Group}");
                Assert.False(WorkoutSequencePolicy.IsSelectable(catalog[id], catalog, group));
            }
        }

        WorkoutGroup upper = MassGroupingTaxonomy.GetGroup(3, "r3.head-neck-upper-limbs");
        WorkoutModifiers floorProfile = WorkoutModifiers.Insect |
            WorkoutModifiers.HardFloor | WorkoutModifiers.Silence | WorkoutModifiers.Shy;
        Assert.True(WorkoutModifierPolicy.IsCompatible(catalog[248], floorProfile));
        Assert.False(WorkoutModifierPolicy.IsCompatible(catalog[591], floorProfile));
        Assert.Single(WorkoutSequencePolicy.GetPlacementOptions(catalog[248], catalog, [upper]));
        Assert.Equal(2, catalog[591].SequenceBlocks.Length);
    }

    public sealed record CoverageCase(int Minutes, string Group, int[] Accepted, int[] Rejected);
}
