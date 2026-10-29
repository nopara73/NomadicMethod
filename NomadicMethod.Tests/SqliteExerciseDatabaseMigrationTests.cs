using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Data;
using NomadicMethod.Models;
using NomadicMethod.Services;
using Microsoft.Data.Sqlite;

namespace NomadicMethod.Tests;

public sealed class SqliteExerciseDatabaseMigrationTests
{
    private static readonly HashSet<int> WallExerciseIds =
    [
        134, 137, 149, 153, 162, 163, 165, 166, 172, 175,
        579, 580, 584, 585, 586, 587, 603, 633,
        701, 702, 703, 704, 801, 835,
    ];

    private static readonly HashSet<int> BareUpperBodyExpansionExerciseIds =
    [
        790, 993,
    ];

    private static readonly HashSet<int> DemandCoverageExpansionExerciseIds =
    [
        302, 304, 305, 307, 308, 309, 310,
    ];

    private static readonly HashSet<int> MaterialTrainingExpansionExerciseIds =
    [
        911, 913, 916, 917,
    ];

    private static readonly HashSet<int> TrainingClaimIntegrityExpansionExerciseIds =
    [
        918, 919,
    ];

    private static readonly HashSet<int> ShyAuditReusedExerciseIds =
    [
        202, 204, 205,
    ];

    private static readonly JsonSerializerOptions CatalogJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(14)]
    [InlineData(66)]
    [InlineData(67)]
    [InlineData(68)]
    [InlineData(69)]
    [InlineData(70)]
    [InlineData(71)]
    [InlineData(72)]
    [InlineData(73)]
    [InlineData(74)]
    [InlineData(75)]
    [InlineData(76)]
    [InlineData(77)]
    [InlineData(78)]
    [InlineData(79)]
    [InlineData(80)]
    [InlineData(81)]
    [InlineData(82)]
    [InlineData(83)]
    [InlineData(84)]
    [InlineData(85)]
    [InlineData(86)]
    [InlineData(87)]
    [InlineData(88)]
    [InlineData(89)]
    [InlineData(90)]
    [InlineData(91)]
    [InlineData(92)]
    [InlineData(93)]
    [InlineData(94)]
    [InlineData(95)]
    [InlineData(96)]
    [InlineData(97)]
    [InlineData(98)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(104)]
    [InlineData(105)]
    [InlineData(106)]
    [InlineData(107)]
    [InlineData(108)]
    [InlineData(109)]
    [InlineData(110)]
    [InlineData(111)]
    [InlineData(112)]
    [InlineData(113)]
    [InlineData(114)]
    public void EverySupportedDatabaseCanUpgradeToTheCurrentCatalog(int oldVersion)
    {
        Assert.Equal(116, ExerciseDatabaseVersionPolicy.CurrentVersion);
        Assert.True(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
            oldVersion,
            ExerciseDatabaseVersionPolicy.CurrentVersion));
    }

    [Theory]
    [InlineData(13, 95)]
    [InlineData(68, 68)]
    [InlineData(69, 69)]
    [InlineData(70, 70)]
    [InlineData(71, 71)]
    [InlineData(72, 72)]
    [InlineData(73, 73)]
    [InlineData(74, 74)]
    [InlineData(75, 75)]
    [InlineData(76, 76)]
    [InlineData(77, 77)]
    [InlineData(78, 78)]
    [InlineData(79, 79)]
    [InlineData(80, 80)]
    [InlineData(81, 81)]
    [InlineData(82, 82)]
    [InlineData(83, 83)]
    [InlineData(84, 84)]
    [InlineData(85, 85)]
    [InlineData(86, 86)]
    [InlineData(87, 87)]
    [InlineData(88, 88)]
    [InlineData(89, 89)]
    [InlineData(90, 90)]
    [InlineData(91, 91)]
    [InlineData(92, 92)]
    [InlineData(93, 93)]
    [InlineData(94, 94)]
    [InlineData(95, 95)]
    [InlineData(105, 105)]
    public void UnsupportedDatabaseTransitionsRemainRejected(
        int oldVersion,
        int newVersion)
    {
        Assert.False(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
            oldVersion,
            newVersion));
    }

    [Fact]
    public void Version97UpgradeAddsSlotChoicesAndPreservesEveryExistingIdentityAndScore()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), CatalogJsonOptions)!;
        Dictionary<int, StoredExerciseSnapshot> prior = catalog
            .Where(e => !WorkoutCatalogPolicy.AnatomicalVarietyExerciseIds.Contains(e.Id) &&
                !WorkoutCatalogPolicy.ZoneGapExerciseIds.Contains(e.Id) &&
                !WorkoutCatalogPolicy.AbilityGapExerciseIds.Contains(e.Id) &&
                !WorkoutCatalogPolicy.PreciseSkillExerciseIds.Contains(e.Id) &&
                !WorkoutCatalogPolicy.ProfileContributorExerciseIds.Contains(e.Id) &&
                !WorkoutCatalogPolicy.FitnessCoverageExerciseIds.Contains(e.Id) &&
                !WorkoutCatalogPolicy.SlotVarietyExerciseIds.Contains(e.Id) &&
                e.Id != WorkoutCatalogPolicy.BodyCircleExerciseId && !e.ElevationRequired &&
                !WorkoutCatalogPolicy.FloorLocomotionExerciseIds.Contains(e.Id))
            .ToDictionary(e => e.Id, e => new StoredExerciseSnapshot(e.Name, e.Video, e.Id % 17 - 8));
        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(catalog, prior);
        Assert.Equal(805, prior.Count);
        Assert.Equal(prior.Keys.Order(), preserved.Order());
        Assert.DoesNotContain(preserved, WorkoutCatalogPolicy.SlotVarietyExerciseIds.Contains);
        Assert.DoesNotContain(WorkoutCatalogPolicy.BodyCircleExerciseId, preserved);
        Assert.Equal(114, catalog.Length - preserved.Count);
        Assert.All(prior, item => Assert.Equal(item.Key % 17 - 8, item.Value.Score));
        Assert.True(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
            97, ExerciseDatabaseVersionPolicy.CurrentVersion));
    }

    [Theory]
    [InlineData(104, 88, 884)]
    [InlineData(105, 89, 890)]
    public void CatalogUpgradeMustPrecedeLoadingNewReactionPlans(int databaseVersion, int catalogRevision, int count)
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), CatalogJsonOptions)!;
        Exercise[] previousCatalog = WorkoutCatalogPolicy.ForWorkout(catalog, catalogRevision).ToArray();
        Assert.Equal(count, previousCatalog.Length);
        using Stream plans = File.OpenRead(Path.Combine(
            AppContext.BaseDirectory, "Assets", "reaction_drills.json"));
        // New reaction plans reference records absent from each previous
        // installation. SQLite must refresh the catalog before resolving them.
        Assert.Throws<InvalidDataException>(() => ReactionDrill.Load(plans, previousCatalog));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision + 16,
            ExerciseDatabaseVersionPolicy.CurrentVersion);
        Assert.True(ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
            databaseVersion, ExerciseDatabaseVersionPolicy.CurrentVersion));

        Dictionary<int, StoredExerciseSnapshot> saved = previousCatalog.ToDictionary(
            exercise => exercise.Id,
            exercise => new StoredExerciseSnapshot(exercise.Name, exercise.Video, exercise.Id % 17 - 8));
        Assert.Equal(saved.Keys.Order(),
            CatalogMigrationRules.ValidatePreservedCatalog(catalog, saved).Order());
        Assert.All(saved, item => Assert.Equal(item.Key % 17 - 8, item.Value.Score));
        plans.Position = 0;
        IReadOnlyDictionary<int, ReactionPlan> loaded = ReactionDrill.Load(plans, catalog);
        Assert.Equal("Initiation", loaded[1621].Protocol);
        Assert.Equal(21, loaded.Count);
        Assert.All(new[] { 1605, 1615, 1625, 1635, 1645 }, id => Assert.Equal("StopOnly", loaded[id].Protocol));
    }

    [Fact]
    public void Version67UpgradeAddsSequenceRecordsAndPreservesEveryUnchangedScore()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        int[] addedExerciseIds =
        [
            529, 530, 531, 532, 533, 534, 535, 536, 537,
            538, 539, 540, 541, 542, 543, 545, 546, 547, 548,
        ];
        HashSet<int> added = addedExerciseIds.ToHashSet();
        HashSet<int> laterHardFloorCoverageIds = new int[]
        {
            549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
            559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
            569, 570, 571, 574, 575, 578, 581, 582, 583,
        }.ToHashSet();
        var storedVersion67 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise =>
                !added.Contains(exercise.Id) &&
                !laterHardFloorCoverageIds.Contains(exercise.Id) &&
                !BareUpperBodyExpansionExerciseIds.Contains(exercise.Id) &&
                !DemandCoverageExpansionExerciseIds.Contains(exercise.Id) &&
                !MaterialTrainingExpansionExerciseIds.Contains(exercise.Id) &&
                !TrainingClaimIntegrityExpansionExerciseIds.Contains(exercise.Id) &&
                !ShyAuditReusedExerciseIds.Contains(exercise.Id) &&
                !WallExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Id switch
                    {
                        520 => "Silent Vowel-Shape Sequence",
                        521 => "Smile-to-Neutral Transitions",
                        _ => exercise.Name,
                    },
                    exercise.Video,
                    Score: exercise.Id % 17 - 8));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion67);

        Assert.Equal(919, catalog.Length);
        Assert.Equal(429, storedVersion67.Count);
        Assert.Equal(427, preserved.Count);
        Assert.DoesNotContain(520, preserved);
        Assert.DoesNotContain(521, preserved);
        Assert.Equal(
            storedVersion67.Keys.Except([520, 521]).Order(),
            preserved.Order());
        Assert.All(storedVersion67, entry =>
            Assert.Equal(entry.Key % 17 - 8, entry.Value.Score));
        Assert.Equal(5, catalog.Count(exercise =>
            added.Contains(exercise.Id) &&
            exercise.MuscularDemand == Exercise.MaximumMuscularDemand));
        Assert.All(addedExerciseIds, exerciseId =>
            Assert.Contains(catalog, root => root.SequenceBlocks.Any(block =>
                block.ExerciseId == exerciseId)));
    }

    [Fact]
    public void Version69UpgradeAddsHardFloorCoverageRecordsAndPreservesUnchangedScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        int[] addedExerciseIds =
        [
            549, 550, 551, 552, 554, 555, 556, 557,
            560, 561, 562, 563, 564, 565, 566, 567, 568,
            569, 570, 571, 574, 575, 578, 581, 582, 583,
        ];
        HashSet<int> added = addedExerciseIds.ToHashSet();
        Dictionary<int, StoredExerciseSnapshot> storedVersion69 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise =>
                !added.Contains(exercise.Id) &&
                !BareUpperBodyExpansionExerciseIds.Contains(exercise.Id) &&
                !DemandCoverageExpansionExerciseIds.Contains(exercise.Id) &&
                !MaterialTrainingExpansionExerciseIds.Contains(exercise.Id) &&
                !TrainingClaimIntegrityExpansionExerciseIds.Contains(exercise.Id) &&
                !ShyAuditReusedExerciseIds.Contains(exercise.Id) &&
                !WallExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    Score: exercise.Id % 19 - 9));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion69);

        Assert.Equal(919, catalog.Length);
        Assert.Equal(448, storedVersion69.Count);
        Assert.Equal(storedVersion69.Keys.Order(), preserved.Order());
        Assert.All(storedVersion69, entry =>
            Assert.Equal(entry.Key % 19 - 9, entry.Value.Score));
        Assert.All(addedExerciseIds, exerciseId =>
        {
            Exercise addedExercise = Assert.Single(
                catalog,
                exercise => exercise.Id == exerciseId);
            Assert.Equal(
                exerciseId is 549 or 550 or 551 or 552 or 554 or 555 or 557 or 560 or 562 or 565 or 566 or 569 or 570 or 571 or 581 or 582
                    ? ExerciseHardFloorCompatibility.Compatible
                    : ExerciseHardFloorCompatibility.Incompatible,
                addedExercise.HardFloorCompatibility);
            Assert.DoesNotContain(exerciseId, preserved);
        });
    }

    [Fact]
    public void PublishedVersion68CatalogUpgradesWithoutRejectingChangedHardFloorExercises()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        int[] addedAfterVersion68 =
        [
            547, 548,
            549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
            559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
            569, 570, 571, 574, 575, 578, 581, 582, 583,
        ];
        var publishedVersion68Names = new Dictionary<int, string>
        {
            [439] = "Pogo Bounces with Fixed-Gaze Head Turns",
            [442] = "Pogo Bounces with Fixed-Gaze Head Nods",
            [444] = "Pogo Bounces with Fixed-Gaze Head Tilts",
            [478] = "Eye-Tracking Rotational Jumps",
        };
        HashSet<int> added = addedAfterVersion68.ToHashSet();
        Dictionary<int, StoredExerciseSnapshot> storedVersion68 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise =>
                !added.Contains(exercise.Id) &&
                !BareUpperBodyExpansionExerciseIds.Contains(exercise.Id) &&
                !DemandCoverageExpansionExerciseIds.Contains(exercise.Id) &&
                !MaterialTrainingExpansionExerciseIds.Contains(exercise.Id) &&
                !TrainingClaimIntegrityExpansionExerciseIds.Contains(exercise.Id) &&
                !ShyAuditReusedExerciseIds.Contains(exercise.Id) &&
                !WallExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    publishedVersion68Names.GetValueOrDefault(
                        exercise.Id,
                        exercise.Name),
                    exercise.Video,
                    Score: exercise.Id % 19 - 9));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion68);

        Assert.Equal(446, storedVersion68.Count);
        Assert.Equal(445, preserved.Count);
        Assert.Equal(
            storedVersion68.Keys
                .Except([478])
                .Order(),
            preserved.Order());
        Assert.All(storedVersion68, entry =>
            Assert.Equal(entry.Key % 19 - 9, entry.Value.Score));
    }

    [Fact]
    public void Version75UpgradeAddsBareUpperBodyMirrorPracticesWithoutResettingScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        Dictionary<int, StoredExerciseSnapshot> storedVersion75 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise =>
                !BareUpperBodyExpansionExerciseIds.Contains(exercise.Id) &&
                !DemandCoverageExpansionExerciseIds.Contains(exercise.Id) &&
                !MaterialTrainingExpansionExerciseIds.Contains(exercise.Id) &&
                !TrainingClaimIntegrityExpansionExerciseIds.Contains(exercise.Id) &&
                !ShyAuditReusedExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    Score: exercise.Id % 29 - 14));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion75);

        Assert.Equal(919, catalog.Length);
        Assert.Equal(498, storedVersion75.Count);
        Assert.Equal(storedVersion75.Keys.Order(), preserved.Order());
        Assert.All(storedVersion75, entry =>
            Assert.Equal(entry.Key % 29 - 14, entry.Value.Score));
        Assert.All(BareUpperBodyExpansionExerciseIds, exerciseId =>
        {
            Exercise addedExercise = Assert.Single(
                catalog,
                exercise => exercise.Id == exerciseId);
            Assert.Equal(
                exerciseId == 790
                    ? ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired
                    : ExerciseUpperBodyClothingRequirement.Agnostic,
                addedExercise.UpperBodyClothingRequirement);
            Assert.Equal(
                exerciseId == 790
                    ? ExerciseMirrorRelationship.BenefitsGreatly
                    : ExerciseMirrorRelationship.Agnostic,
                addedExercise.MirrorRelationship);
            Assert.DoesNotContain(exerciseId, preserved);
        });
    }

    [Fact]
    public void Version81UpgradeAddsDemandCoverageRecordsWithoutReusingScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        Dictionary<int, StoredExerciseSnapshot> storedVersion81 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise =>
                !DemandCoverageExpansionExerciseIds.Contains(exercise.Id) &&
                !MaterialTrainingExpansionExerciseIds.Contains(exercise.Id) &&
                !TrainingClaimIntegrityExpansionExerciseIds.Contains(exercise.Id) &&
                !ShyAuditReusedExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    Score: exercise.Id % 31 - 15));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion81);

        Assert.Equal(919, catalog.Length);
        Assert.Equal(500, storedVersion81.Count);
        Assert.Equal(storedVersion81.Keys.Order(), preserved.Order());
        Assert.All(storedVersion81, entry =>
            Assert.Equal(entry.Key % 31 - 15, entry.Value.Score));
        Assert.All(DemandCoverageExpansionExerciseIds, exerciseId =>
        {
            Exercise addedExercise = Assert.Single(
                catalog,
                exercise => exercise.Id == exerciseId);
            Assert.Equal(0, addedExercise.Score);
            Assert.DoesNotContain(exerciseId, preserved);
        });
    }

    [Fact]
    public void Version85UpgradeAddsAbdominalTrainingRecordsWithoutReusingScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        Dictionary<int, StoredExerciseSnapshot> storedVersion85 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise =>
                !MaterialTrainingExpansionExerciseIds.Contains(exercise.Id) &&
                !TrainingClaimIntegrityExpansionExerciseIds.Contains(exercise.Id) &&
                !ShyAuditReusedExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    Score: exercise.Id % 37 - 18));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion85);

        Assert.Equal(919, catalog.Length);
        Assert.Equal(507, storedVersion85.Count);
        Assert.Equal(storedVersion85.Keys.Order(), preserved.Order());
        Assert.All(storedVersion85, entry =>
            Assert.Equal(entry.Key % 37 - 18, entry.Value.Score));
        Assert.All(MaterialTrainingExpansionExerciseIds, exerciseId =>
        {
            Exercise addedExercise = Assert.Single(
                catalog,
                exercise => exercise.Id == exerciseId);
            Assert.Equal(exerciseId switch
            {
                911 => CanonicalMuscleGroup.HipFlexors,
                913 => CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                917 => CanonicalMuscleGroup.DeepAndIntersegmentalBack,
                _ => CanonicalMuscleGroup.AbdominalWall,
            }, addedExercise.PrimaryCanonicalGroup);
            Assert.Equal(0, addedExercise.Score);
            Assert.DoesNotContain(exerciseId, preserved);
        });
    }

    [Fact]
    public void Version87UpgradeAddsTrainingClaimIntegrityWorkWithoutReusingScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        Dictionary<int, StoredExerciseSnapshot> storedVersion87 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise =>
                !TrainingClaimIntegrityExpansionExerciseIds.Contains(exercise.Id) &&
                !ShyAuditReusedExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    Score: exercise.Id % 41 - 20));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion87);

        Assert.Equal(919, catalog.Length);
        Assert.Equal(511, storedVersion87.Count);
        Assert.Equal(storedVersion87.Keys.Order(), preserved.Order());
        Assert.All(storedVersion87, entry =>
            Assert.Equal(entry.Key % 41 - 20, entry.Value.Score));
        Exercise pelvicFloorExercise = Assert.Single(
            catalog,
            exercise => exercise.Id == 918);
        Assert.Equal(
            CanonicalMuscleGroup.PelvicFloorAndPerineum,
            pelvicFloorExercise.PrimaryCanonicalGroup);
        Assert.Empty(pelvicFloorExercise.SecondaryCanonicalGroups);
        Assert.Equal(0, pelvicFloorExercise.Score);
        Assert.DoesNotContain(pelvicFloorExercise.Id, preserved);
        Exercise headTurnMarch = Assert.Single(
            catalog,
            exercise => exercise.Id == 919);
        Assert.Equal(
            CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
            headTurnMarch.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.PosteriorNeckAndSuboccipitalMuscles,
            },
            headTurnMarch.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(0, headTurnMarch.Score);
        Assert.DoesNotContain(headTurnMarch.Id, preserved);
    }

    [Fact]
    public void Version88UpgradeReplacesRetiredShyAuditSlotsWithoutReusingScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        var retiredNames = new Dictionary<int, string>
        {
            [202] = "Hook Fist",
            [204] = "Full Fist",
            [205] = "Tabletop Fist",
        };
        Dictionary<int, StoredExerciseSnapshot> storedVersion88 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    retiredNames.GetValueOrDefault(exercise.Id, exercise.Name),
                    exercise.Video,
                    Score: exercise.Id % 41 - 20));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion88);

        Assert.Equal(919, catalog.Length);
        Assert.Equal(
            storedVersion88.Keys
                .Except(ShyAuditReusedExerciseIds)
                .ToHashSet(),
            preserved);
        Assert.All(ShyAuditReusedExerciseIds, exerciseId =>
        {
            Assert.DoesNotContain(exerciseId, preserved);
            Assert.Equal(
                0,
                catalog.Single(exercise => exercise.Id == exerciseId).Score);
            Assert.Equal(
                exerciseId % 41 - 20,
                storedVersion88[exerciseId].Score);
        });
    }

    [Fact]
    public void Version72UpgradeReclassifiesSlippingRisksWithoutResettingScores()
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                CatalogJsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        Dictionary<int, StoredExerciseSnapshot> storedVersion72 = catalog
            .Where(exercise => exercise.Id <= 1000) // Later integrity-audit admissions were not published yet.
            .Where(exercise => !ShyAuditReusedExerciseIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.Name,
                    exercise.Video,
                    Score: exercise.Id % 23 - 11));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog,
            storedVersion72);

        Assert.Equal(
            storedVersion72.Keys
                .Except(ShyAuditReusedExerciseIds)
                .ToHashSet(),
            preserved);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            catalog.Single(exercise => exercise.Id == 37).HardFloorCompatibility);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            catalog.Single(exercise => exercise.Id == 101).HardFloorCompatibility);
        Assert.All(storedVersion72, entry =>
            Assert.Equal(entry.Key % 23 - 11, entry.Value.Score));
    }

    [Fact]
    public void LegacyMirrorOnlyRowCanBeCopiedIntoCurrentSchema()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Execute(
            connection,
            """
            CREATE TABLE exercises (
                id INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                video TEXT NOT NULL UNIQUE,
                practice TEXT NOT NULL,
                motion_profile TEXT NOT NULL,
                score INTEGER NOT NULL,
                muscular_demand INTEGER NOT NULL,
                only_feet_touch_ground INTEGER NOT NULL,
                shoe_agnostic INTEGER NOT NULL,
                max_space_meters INTEGER NOT NULL,
                equipment TEXT NOT NULL,
                silent INTEGER NOT NULL,
                exercise_mode TEXT NOT NULL,
                presentation TEXT NOT NULL,
                hold_frame_percent INTEGER NOT NULL,
                side_sequence TEXT NOT NULL,
                direction_sequence TEXT NOT NULL,
                direction_partner_exercise_id INTEGER NOT NULL,
                insect_compatibility TEXT NOT NULL,
                mirror_relationship TEXT NOT NULL,
                mirror_coverage TEXT NOT NULL
            );

            INSERT INTO exercises VALUES (
                528,
                'Mirror Posture Check',
                'exercise_0528.mp4',
                'Posture',
                'Static',
                7,
                1,
                1,
                1,
                2,
                'Mirror',
                1,
                'Repetition',
                'Motion',
                0,
                'Continuous',
                'None',
                0,
                'Compatible',
                'MirrorOnly',
                'FullBody');
            """);

        Execute(
            connection,
            ExerciseDatabaseMigrationSql.DropRebuiltExerciseTableIfPresent);
        Execute(
            connection,
            ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);

        Execute(
            connection,
            ExerciseDatabaseMigrationSql
                .CopyExistingExercisesWithNeutralCatalogMetadata);
        Execute(connection, "DROP TABLE exercises");
        Execute(
            connection,
            ExerciseDatabaseMigrationSql.RenameRebuiltExerciseTable);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, video, score, equipment,
                hard_floor_compatibility, shy_compatibility,
                mirror_relationship, mirror_coverage, wall_required,
                sole_wall_contact_required, session_movement_id, floor_contact_points
            FROM exercises
            WHERE id = 528
            """;
        using SqliteDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Mirror Posture Check", reader.GetString(0));
        Assert.Equal("exercise_0528.mp4", reader.GetString(1));
        Assert.Equal(7, reader.GetInt32(2));
        Assert.Equal("None", reader.GetString(3));
        Assert.Equal("Unreviewed", reader.GetString(4));
        Assert.Equal("Unreviewed", reader.GetString(5));
        Assert.Equal("Unreviewed", reader.GetString(6));
        Assert.Equal("None", reader.GetString(7));
        Assert.Equal(0, reader.GetInt32(8));
        Assert.Equal(0, reader.GetInt32(9));
        Assert.Equal(0, reader.GetInt32(10));
        Assert.Equal((int)ExerciseFloorContact.Feet, reader.GetInt32(11));
        Assert.False(reader.Read());
    }

    [Fact]
    public void CurrentSchemaPersistsWallContactRequirementsAsBooleans()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(
            connection,
            ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);
        Execute(
            connection,
            $"""
            INSERT INTO {ExerciseDatabaseMigrationSql.RebuiltExerciseTableName} (
                id, name, video, practice, motion_profile, muscular_demand,
                insect_compatibility, hard_floor_compatibility,
                mirror_relationship, wall_required,
                sole_wall_contact_required)
            VALUES (
                1, 'Wall Sit', 'exercise_0001.mp4', 'Bodyweight strength',
                'WallSit', 2, 'Compatible', 'Compatible', 'Agnostic', 1, 1)
            """);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"SELECT wall_required, sole_wall_contact_required FROM " +
            ExerciseDatabaseMigrationSql.RebuiltExerciseTableName +
            " WHERE id = 1";

        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
        Assert.False(reader.Read());
    }

    [Fact]
    public void CurrentSchemaRejectsSoleWallContactWithoutWallEquipment()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(
            connection,
            ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);

        Assert.Throws<SqliteException>(() => Execute(
            connection,
            $"""
            INSERT INTO {ExerciseDatabaseMigrationSql.RebuiltExerciseTableName} (
                id, name, video, practice, motion_profile, muscular_demand,
                insect_compatibility, hard_floor_compatibility,
                mirror_relationship, wall_required,
                sole_wall_contact_required)
            VALUES (
                1, 'Invalid wall contact', 'exercise_0001.mp4', 'Test',
                'InvalidWallContact', 0, 'Compatible', 'Compatible',
                'Agnostic', 0, 1)
            """));
    }

    [Fact]
    public void CurrentSchemaStoresFloorContactsWithoutChangingScores()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);
        string table = ExerciseDatabaseMigrationSql.RebuiltExerciseTableName;
        Execute(connection, $"""
            INSERT INTO {table} (id, name, video, practice, motion_profile,
                score, muscular_demand, insect_compatibility, hard_floor_compatibility,
                mirror_relationship, only_feet_touch_ground, floor_contact_points)
            VALUES (1, 'Kneeling floor fixture', 'fixture.mp4', 'Test', 'Test',
                -7, 1, 'Incompatible', 'Incompatible', 'Agnostic', 0, 19)
            """);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT only_feet_touch_ground, floor_contact_points, score FROM {table}";
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            Assert.True(reader.Read());
            Assert.Equal(0, reader.GetInt32(0));
            Assert.Equal((int)(ExerciseFloorContact.Feet | ExerciseFloorContact.Hands |
                ExerciseFloorContact.Knees), reader.GetInt32(1));
            Assert.Equal(-7, reader.GetInt32(2));
        }
        foreach (int invalid in new[] { -1, 2048 })
            Assert.Throws<SqliteException>(() => Execute(connection,
                $"UPDATE {table} SET floor_contact_points = {invalid} WHERE id = 1"));
    }

    [Fact]
    public void ElevationSchemaDefaultsPreserveScoresAndRejectOrphanPlatform()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);
        string table = ExerciseDatabaseMigrationSql.RebuiltExerciseTableName;
        Execute(connection, $"""
            INSERT INTO {table} (id, name, video, practice, motion_profile,
                score, muscular_demand, insect_compatibility, hard_floor_compatibility,
                mirror_relationship)
            VALUES (1, 'Preserved movement', 'original.mp4', 'Test', 'Test',
                -7, 1, 'Incompatible', 'Compatible', 'Agnostic')
            """);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT elevation_required, jump_platform_required, score FROM {table}";
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            Assert.True(reader.Read());
            Assert.Equal(0, reader.GetInt32(0));
            Assert.Equal(0, reader.GetInt32(1));
            Assert.Equal(-7, reader.GetInt32(2));
        }
        Assert.Throws<SqliteException>(() => Execute(connection,
            $"UPDATE {table} SET jump_platform_required = 1 WHERE id = 1"));
        Execute(connection, $"UPDATE {table} SET elevation_required = 1, jump_platform_required = 1 WHERE id = 1");
        command.CommandText = $"SELECT score FROM {table} WHERE id = 1";
        Assert.Equal(-7L, command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
