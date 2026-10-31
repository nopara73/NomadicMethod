using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ContextResponseTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
    private static string Asset(string name) => Path.Combine(AppContext.BaseDirectory,"Assets",name);
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(Asset("exercises.json")),Options)!;
    private static FitnessRatingCatalog Ratings()
    {
        using Stream stream=File.OpenRead(Asset("fitness_ratings.json"));
        return FitnessProfilePolicy.Load(stream);
    }

    [Fact]
    public void PriorRatingsAndWholeReactionDefinitionsRemainUnchanged()
    {
        using var cases=JsonDocument.Parse(File.ReadAllText(Asset("context-response-cases.json")));
        var ratings=Ratings();
        var rows=ratings.Where(entry=>entry.Key<1741).OrderBy(entry=>entry.Key)
            .Select(entry=>new object[]{entry.Key,entry.Value,ratings.GetTrainingCharacteristics(entry.Key)!}).ToArray();
        Assert.Equal(cases.RootElement.GetProperty("previousRecordCount").GetInt32(),rows.Length);
        string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows)))).ToLowerInvariant();
        Assert.Equal(cases.RootElement.GetProperty("previousRatingsAndClaimsSha256").GetString(),hash);
        JsonNode frozen=JsonNode.Parse(File.ReadAllText(Asset("reaction-protocols-through106.json")))!;
        JsonNode current=JsonNode.Parse(File.ReadAllText(Asset("reaction_drills.json")))!;
        var names=frozen["families"]!.AsArray().Select(f=>f!["id"]!.GetValue<string>()).ToHashSet();
        var ids=frozen["plans"]!.AsArray().Select(p=>p!["exerciseId"]!.GetValue<int>()).ToHashSet();
        var previous=new JsonObject
        {
            ["version"]=current["version"]!.DeepClone(),
            ["families"]=new JsonArray(current["families"]!.AsArray().Where(f=>names.Contains(f!["id"]!.GetValue<string>())).Select(f=>f!.DeepClone()).ToArray()),
            ["plans"]=new JsonArray(current["plans"]!.AsArray().Where(p=>ids.Contains(p!["exerciseId"]!.GetValue<int>())).Select(p=>p!.DeepClone()).ToArray()),
        };
        Assert.True(JsonNode.DeepEquals(frozen,previous));
    }

    private static WorkoutState FullyAssessedFive(Exercise[] catalog,FitnessRatingCatalog ratings)
    {
        var decisions=new List<WorkoutDecisionLog>();var used=new HashSet<int>();
        for(int axis=0;axis<10;axis++)
        {
            Exercise seed=catalog.First(e=>!used.Contains(WorkoutModifierPolicy.GetSessionMovementId(e)) &&
                ratings.HasTrainingClaim(e.Id,axis) && (ratings[e.Id][axis][^1]==4 || ratings[e.Id][axis].SequenceEqual(new[]{5})));
            used.Add(WorkoutModifierPolicy.GetSessionMovementId(seed));
            decisions.Add(new(){RootExerciseId=seed.Id,Outcome=ratings[seed.Id][axis][^1]==4?ExerciseOutcome.TooEasy:ExerciseOutcome.Tick,DecidedAtUnixMilliseconds=axis+1});
        }
        var state=new WorkoutState{CatalogRevision=107,WorkoutHistory=[new(){SessionId=1,Status=WorkoutSessionStatus.Interrupted,
            StartedAtUnixMilliseconds=1,EndedAtUnixMilliseconds=2,WorkoutMinutes=3,StartedBeforeLogging=true,Decisions=decisions}]};
        Assert.All(FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(catalog,state,ratings)),zone=>Assert.Equal(new[]{5},zone));
        return state;
    }

    [Fact]
    public void EveryClosedProfileHasFiveFeasibleChoicesAtEverySetupDuration()
    {
        using var cases=JsonDocument.Parse(File.ReadAllText(Asset("context-response-cases.json")));
        Exercise[] catalog=Catalog();var ratings=Ratings();var byId=catalog.ToDictionary(e=>e.Id);
        var known=new[]{3,5,7,10,15,20,30}.SelectMany(m=>MassGroupingTaxonomy.GetResolution(m).Groups).ToDictionary(g=>g.Id);
        foreach(var profile in cases.RootElement.GetProperty("closedProfiles").EnumerateArray())
        foreach(var duration in cases.RootElement.GetProperty("durations").EnumerateArray())
        {
            var state=FullyAssessedFive(catalog,ratings);
            var service=ExerciseSessionService.CreateForApplication(catalog,new Random(42),()=>DateTimeOffset.FromUnixTimeMilliseconds(1000000),fitnessRatings:ratings);
            int minutes=duration.GetInt32();var modifiers=(WorkoutModifiers)profile.GetProperty("modifiers").GetInt32();
            service.StartWorkout(state,minutes,modifiers);
            Assert.Equal(minutes,service.GetActiveGroups(state).Count);
            var zones=FitnessSelectionPolicy.Zones(FitnessProfilePolicy.Inspect(catalog,state,ratings));
            foreach(WorkoutGroup group in service.GetActiveGroups(state))
            {
                var members=WorkoutSequencePolicy.GetMembers(service.GetSelectedExercise(state,group),byId);
                Assert.True(FitnessSelectionPolicy.IsSequenceEligible(members.Select(e=>ratings[e.Id]),zones,
                    members.Select(e=>ratings.GetTrainingCharacteristics(e.Id))));
            }
            WorkoutGroup[] actual=state.ActiveDurationSelectionGroupIds is { } ids
                ? ids.Select(id=>known[id]).ToArray():MassGroupingTaxonomy.GetResolution(minutes).Groups.ToArray();
            service.EnsureSelectableWorkoutVariety(state,minutes,modifiers,actual);
        }
    }

    [Fact]
    public void Revision106PausedWorkRetainsItsPoolSelectionsAndCheckpoint()
    {
        Exercise[] catalog=Catalog();var ratings=Ratings();
        var state=new WorkoutState{CatalogRevision=107};
        var original=new ExerciseSessionService(WorkoutCatalogPolicy.ForWorkout(catalog,106),new Random(42),fitnessRatings:ratings);
        original.StartWorkout(state,3,WorkoutModifiers.None);
        state.CatalogRevision=106;state.ActiveCatalogRevision=106;
        original.PauseMovement(state,original.GetNextGroup(state)!,27000,pausedByUser:true);
        var selected=state.SelectedExerciseIds.OrderBy(e=>e.Key).ToArray();
        string log=JsonSerializer.Serialize(state.ActiveWorkoutSession);
        state=JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        var restored=ExerciseSessionService.CreateForApplication(catalog,new Random(99),fitnessRatings:ratings);
        restored.RestoreAfterReopen(state);
        Assert.Equal(selected,state.SelectedExerciseIds.OrderBy(e=>e.Key).ToArray());
        Assert.Equal(log,JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Equal(27000,state.PendingMovementMillisecondsRemaining);
        Assert.Equal(930,WorkoutCatalogPolicy.ForWorkout(catalog,106).Count);
        Assert.DoesNotContain(WorkoutCatalogPolicy.ForWorkout(catalog,106),e=>e.Id>=1741);
    }
}
