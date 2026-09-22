using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class CatalogIntegrityMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Asset(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", name));

    [Fact]
    public void CatalogIntegrityNamesPreserveCorrectionsButDiscardDifferentActions()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
            Asset("exercises.json"), JsonOptions)!;
        using JsonDocument manifest = JsonDocument.Parse(
            Asset("migration-2026-09-05.json"));
        Dictionary<int, StoredExerciseSnapshot> stored = catalog.ToDictionary(
            e => e.Id,
            e => new StoredExerciseSnapshot(e.Name, e.Video, -e.Id));
        foreach (JsonElement correction in manifest.RootElement
                     .GetProperty("nameCorrections").EnumerateArray()
                     .Concat(manifest.RootElement.GetProperty("identityReplacements").EnumerateArray()))
        {
            int id = correction.GetProperty("id").GetInt32();
            stored[id] = stored[id] with
            {
                Name = correction.GetProperty("previousName").GetString()!,
            };
        }

        int[] replacedIds = manifest.RootElement.GetProperty("identityReplacements")
            .EnumerateArray().Select(entry => entry.GetProperty("id").GetInt32()).ToArray();
        Assert.Equal(17, replacedIds.Length);
        Assert.Equal(stored.Keys.Except(replacedIds).Order(),
            CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored).Order());
        Assert.All(stored, pair => Assert.Equal(-pair.Key, pair.Value.Score));
        stored[138] = stored[138] with { Name = "Unreviewed invented movement" };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored));
    }

    [Fact]
    public void DifferentActionReplacementsClearEvenAnatomicallyValidOldPreferences()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(Asset("exercises.json"), JsonOptions)!;
        Dictionary<int, Exercise> byId = catalog.ToDictionary(e => e.Id);
        foreach (int id in CatalogMigrationRules.ScoreInvalidationsByRevision[73])
        {
            string slot = MassGroupingTaxonomy.GetResolution(30).Groups.Single(
                group => group.CanonicalGroups.Contains(byId[id].PrimaryCanonicalGroup)).Id;
            var state = new WorkoutState
            {
                CatalogRevision = 72,
                ActiveWorkoutMinutes = 30,
                SelectedExerciseIds = new() { [slot] = id },
                KeptExerciseRootIdsBySelectionGroupId = new() { [slot] = [id] },
                ExerciseScoreAdjustmentsBySelectionGroupId = new() { [slot] = new() { [id] = -2 } },
                ExerciseScoreAdjustmentsByPhase = new()
                {
                    [WorkoutExercisePhase.PeakPerformance] = new() { [id] = -3 },
                },
                LastKeptExerciseIds = [id],
                PendingScoreExerciseId = id,
                PendingScoreValue = -4,
            };
            Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state, byId));
            Assert.Empty(state.SelectedExerciseIds);
            Assert.Empty(state.KeptExerciseRootIdsBySelectionGroupId);
            Assert.Empty(state.ExerciseScoreAdjustmentsBySelectionGroupId);
            Assert.Empty(state.ExerciseScoreAdjustmentsByPhase);
            Assert.Empty(state.LastKeptExerciseIds);
            Assert.Equal(0, state.PendingScoreExerciseId);
            Assert.False(CatalogMigrationRules.ReconcileWorkoutState(state, byId));
        }
    }

    [Fact]
    public void EveryChangedStandaloneTrainingClaimRevalidatesOnlyImpossibleFineSlots()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
            Asset("exercises.json"), JsonOptions)!;
        Dictionary<int, Exercise> byId = catalog.ToDictionary(e => e.Id);
        Dictionary<CanonicalMuscleGroup, string> slotByMuscle =
            MassGroupingTaxonomy.GetResolution(30).Groups.ToDictionary(
                group => Assert.Single(group.CanonicalGroups), group => group.Id);
        using JsonDocument review = JsonDocument.Parse(
            Asset("training-claim-corrections-2026-09-05.json"));
        IReadOnlySet<int> placementChanges =
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[73];
        int reviewedRoots = 0;
        IReadOnlySet<int> replacedIds = CatalogMigrationRules.ScoreInvalidationsByRevision[73];

        foreach (JsonElement entry in review.RootElement.GetProperty("entries").EnumerateArray())
        {
            // Newly admitted IDs have no historical fine-slot preference.
            if (entry.GetProperty("previousPrimary").ValueKind == JsonValueKind.Null)
            {
                continue;
            }
            int id = entry.GetProperty("id").GetInt32();
            if (replacedIds.Contains(id) || CatalogMigrationRules.PermanentlyRetiredExerciseIds.Contains(id))
            {
                continue;
            }
            Exercise exercise = byId[id];
            if (exercise.SequenceBlocks.Length == 0 ||
                exercise.SequenceBlocks.Any(block => block.ExerciseId != id))
            {
                continue;
            }
            HashSet<CanonicalMuscleGroup> previous = entry
                .GetProperty("previousSecondary").EnumerateArray()
                .Select(value => Enum.Parse<CanonicalMuscleGroup>(value.GetString()!))
                .Append(Enum.Parse<CanonicalMuscleGroup>(entry
                    .GetProperty("previousPrimary").GetString()!)).ToHashSet();
            HashSet<CanonicalMuscleGroup> current = exercise.SecondaryCanonicalGroups
                .Append(exercise.PrimaryCanonicalGroup).ToHashSet();
            if (previous.SetEquals(current))
            {
                continue;
            }
            reviewedRoots++;
            string[] possibleSlots = previous.Union(current)
                .Select(muscle => slotByMuscle[muscle]).ToArray();
            var state = new WorkoutState
            {
                CatalogRevision = 72,
                ActiveWorkoutMinutes = 30,
                SelectedExerciseIds = possibleSlots.ToDictionary(slot => slot, _ => id),
                KeptExerciseRootIdsBySelectionGroupId = possibleSlots
                    .ToDictionary(slot => slot, _ => new HashSet<int> { id }),
                ExerciseScoreAdjustmentsBySelectionGroupId = possibleSlots
                    .ToDictionary(slot => slot, _ => new Dictionary<int, int> { [id] = -2 }),
                ExerciseScoreAdjustmentsByPhase = new()
                {
                    [WorkoutExercisePhase.PeakPerformance] = new() { [id] = -3 },
                },
                LastKeptExerciseIds = [id],
                PendingScoreExerciseId = id,
                PendingScoreValue = -4,
            };

            Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state, byId));
            foreach (CanonicalMuscleGroup muscle in previous.Union(current))
            {
                string slot = slotByMuscle[muscle];
                bool stillTrained = current.Contains(muscle);
                Assert.True(stillTrained ==
                    state.KeptExerciseRootIdsBySelectionGroupId.ContainsKey(slot),
                    $"Keep changed incorrectly for exercise {id}, slot {slot}.");
                Assert.True(stillTrained ==
                    state.ExerciseScoreAdjustmentsBySelectionGroupId.ContainsKey(slot),
                    $"Slot score changed incorrectly for exercise {id}, slot {slot}.");
                Assert.True((stillTrained && !placementChanges.Contains(id)) ==
                    state.SelectedExerciseIds.ContainsKey(slot),
                    $"Selection changed incorrectly for exercise {id}, slot {slot}.");
            }
            Assert.Equal([id], state.LastKeptExerciseIds);
            Assert.Equal(-3, state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][id]);
            Assert.Equal(id, state.PendingScoreExerciseId);
            Assert.Equal(-4, state.PendingScoreValue);
            Assert.False(CatalogMigrationRules.ReconcileWorkoutState(state, byId));
        }
        Assert.Equal(364, reviewedRoots);
    }
}
