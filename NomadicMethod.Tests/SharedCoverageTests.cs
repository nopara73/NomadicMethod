using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;
namespace NomadicMethod.Tests;
public sealed class SharedCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    private static string Asset(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", name));
    [Fact]
    public void EveryAnatomicalGroupCreditsSecondaryCoverageAndWholeSequences()
    {
        using var cases = JsonDocument.Parse(Asset("shared-coverage-cases.json"));
        foreach (var resolution in cases.RootElement.GetProperty("coverageResolutions").EnumerateArray())
        {
            int minutes = resolution.GetInt32();
            var groups = MassGroupingTaxonomy.GetResolution(minutes).Groups;
            for (int first = 0; first < groups.Count; first++)
            foreach (int blocks in new[] { 1, 2 })
            {
                int second = (first + 1) % groups.Count;
                var roots = groups.Select((g, i) => Exercise(1000 + i, g.CanonicalGroups.First(),
                    i == first ? [groups[second].CanonicalGroups.First()] : [], i == first ? blocks : 1)).ToArray();
                var lineup = groups.Select((g, i) => (g.Id, Root: roots[i].Id)).ToDictionary(e => e.Id, e => e.Root);
                var result = WorkoutSequencePolicy.ConsolidateCoverage(lineup, groups,
                    roots.ToDictionary(e => e.Id), roots.Select((e, i) => (e.Id, Priority: new BigInteger(i == first ? 100 : 1)))
                        .ToDictionary(e => e.Id, e => e.Priority), minutes);
                Assert.Equal(roots[first].Id, result[groups[first].Id]);
                Assert.Equal(roots[first].Id, result[groups[second].Id]);
                Assert.Equal(groups.Count - 1, result.Values.Distinct().Count());
                Assert.Equal(groups.Count, result.Count);
            }
        }
    }
    [Fact]
    public void ExtraSetsCountMeaningfulSecondaryWorkAcrossRegions()
    {
        using var cases = JsonDocument.Parse(Asset("shared-coverage-cases.json"));
        foreach (var item in cases.RootElement.GetProperty("loadCases").EnumerateArray())
        {
            Dictionary<CanonicalMuscleGroup, int> Load(string name) => JsonSerializer.Deserialize<Dictionary<CanonicalMuscleGroup, int>>(item.GetProperty(name), JsonOptions)!;
            var high = WorkoutMuscleBalancePolicy.GetAdditionalSetLoad(Load("scheduled"), Load("overloaded"));
            var low = WorkoutMuscleBalancePolicy.GetAdditionalSetLoad(Load("scheduled"), Load("preferred"));
            Assert.True(low.Numerator * high.Denominator < high.Numerator * low.Denominator);
        }
    }
    [Fact]
    public void KeptCompoundSharesCoverageAndReopensWithoutReplanning()
    {
        var catalog = JsonSerializer.Deserialize<Exercise[]>(Asset("exercises.json"), JsonOptions)!;
        var state = new WorkoutState { KeptExerciseRootIdsBySelectionGroupId = new() { ["r10.posterior-lower-leg-plantar-foot"] = [555] } };
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var service = new ExerciseSessionService(catalog, new Random(5), () => now);
        service.Initialize(state);
        service.StartWorkout(state, 10, WorkoutModifiers.HardFloor | WorkoutModifiers.Wall | WorkoutModifiers.SoleWallContact | WorkoutModifiers.RoomLocomotion | WorkoutModifiers.Floor | WorkoutModifiers.Elevation);
        var calf = Assert.Single(state.ActiveWorkoutSession!.InitialSelections, s => s.RootExerciseId == 555);
        Assert.True(calf.WasKeptAtWorkoutStart);
        Assert.Equal(new[] { "r10.anterior-lateral-lower-leg-dorsal-foot", "r10.posterior-lower-leg-plantar-foot" }, calf.CoveredWorkoutGroupIds);
        Assert.InRange(calf.SetCount, 1, 2);
        Assert.Equal(10, service.GetActiveGroups(state).Count);
        string rounds = JsonSerializer.Serialize(service.GetActiveGroups(state));
        string session = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        service = new ExerciseSessionService(catalog, new Random(9), () => now.AddDays(1));
        service.Initialize(state);
        Assert.Equal(rounds, JsonSerializer.Serialize(service.GetActiveGroups(state)));
        Assert.Equal(session, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Contains(555, state.KeptExerciseRootIdsBySelectionGroupId["r10.posterior-lower-leg-plantar-foot"]);
    }
    [Fact]
    public void CompoundCanCoverTwoGroupsWithoutRepeatingItsMovement()
    {
        var catalog = JsonSerializer.Deserialize<Exercise[]>(Asset("exercises.json"), JsonOptions)!;
        var root = catalog.Single(e => e.Id == 555);
        var groups = MassGroupingTaxonomy.GetResolution(10).Groups.Where(g => g.Id.Contains("lower-leg")).ToArray();
        Assert.Equal(2, WorkoutModifierPolicy.GetMaximumDistinctLineupSize([root], groups, WorkoutModifiers.None, 10));
    }
    [Fact]
    public void AllocationProjectionExactlyMatchesFullBalanceEvaluation()
    {
        var muscles = Enum.GetValues<CanonicalMuscleGroup>();
        for (int sample = 0; sample < 64; sample++)
        {
            Dictionary<CanonicalMuscleGroup, int> Load(int offset) => muscles.Select((m, i) =>
                (m, value: sample < 3 && offset == sample ? 0 : (sample * (i + offset + 1) + offset) % 13))
                .ToDictionary(e => e.m, e => e.value);
            var current = Load(0); var left = Load(1); var right = Load(2);
            MuscleBalanceEvaluation Projected(Dictionary<CanonicalMuscleGroup, int> extra) =>
                WorkoutMuscleBalancePolicy.Evaluate(muscles.ToDictionary(m => m, m => current[m] + extra[m]));
            Assert.Equal(WorkoutMuscleBalancePolicy.CompareWorkoutLoad(Projected(left), Projected(right)),
                WorkoutMuscleBalancePolicy.CompareProjectedRegionalLoad(WorkoutMuscleBalancePolicy.CreateRegionalLoad(current),
                    WorkoutMuscleBalancePolicy.CreateRegionalLoad(left), WorkoutMuscleBalancePolicy.CreateRegionalLoad(right)));
        }
    }
    private static Exercise Exercise(int id, CanonicalMuscleGroup primary, CanonicalMuscleGroup[] secondary, int blocks) => new()
    {
        Id = id, Name = $"Exercise {id}", Video = $"exercise_{id:D4}.mp4", PrimaryCanonicalGroup = primary,
        SecondaryCanonicalGroups = secondary, Practice = "Test", MotionProfile = "Test", Mode = ExerciseMode.Repetition,
        Presentation = ExercisePresentation.Motion, HoldFramePercent = 0, SideSequence = ExerciseSideSequence.Continuous,
        SequenceBlocks = Enumerable.Range(0, blocks).Select(_ => new ExerciseSequenceBlock { ExerciseId = id, MirrorMedia = false }).ToArray(),
        UpperBodyClothingRequirement = ExerciseUpperBodyClothingRequirement.Agnostic, ShyCompatibility = ExerciseShyCompatibility.Compatible,
        Score = 0, OnlyFeetTouchGround = true, ShoeAgnostic = true, MaxSpaceMeters = 3, Equipment = "None", Silent = true,
    };
}
