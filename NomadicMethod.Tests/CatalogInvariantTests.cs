using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class CatalogInvariantTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void EachDirectionUsesItsOwnCompleteVideoLoop()
    {
        Exercise[] exercises = JsonSerializer.Deserialize<Exercise[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
            JsonOptions)!;
        Exercise[] directional = exercises.Where(exercise =>
            exercise.DirectionSequence != ExerciseDirectionSequence.None).ToArray();
        Assert.NotEmpty(directional);
        foreach (Exercise exercise in directional)
        {
            Assert.Equal(exercise.Video,
                exercise.GetVideoAssetPath(ExerciseSequenceMediaSegment.Full));
            Assert.Equal(exercise.Video,
                exercise.GetVideoAssetPath(ExerciseSequenceMediaSegment.FirstDirection));
            Assert.Equal($"exercise_direction_videos/exercise_{exercise.Id:D4}.mp4",
                exercise.GetVideoAssetPath(ExerciseSequenceMediaSegment.SecondDirection));
        }
    }

    [Fact]
    public void CompletePoseDeterminesSidesIncludingSupportingArmsAndLeadStances()
    {
        Exercise[] exercises = JsonSerializer.Deserialize<Exercise[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
            JsonOptions)!;
        Dictionary<int, ExerciseSideSequence> expectedSides = new()
        {
            [490] = ExerciseSideSequence.ScreenLeftThenRight,
            [491] = ExerciseSideSequence.ScreenLeftThenRight,
            [492] = ExerciseSideSequence.ScreenLeftThenRight,
            [495] = ExerciseSideSequence.ScreenLeftThenRight,
            [499] = ExerciseSideSequence.ScreenLeftThenRight,
            [501] = ExerciseSideSequence.ScreenLeftThenRight,
            [528] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [958] = ExerciseSideSequence.ScreenLeftThenRight,
        };
        foreach ((int id, ExerciseSideSequence expected) in expectedSides)
        {
            Exercise member = exercises.Single(exercise => exercise.Id == id);
            Assert.Equal(expected, member.SideSequence);
            var blocks = exercises.SelectMany(exercise => exercise.SequenceBlocks)
                .Where(block => block.ExerciseId == id).ToArray();
            Assert.Equal(2, blocks.Length);
            Assert.Equal(new[] { false, true }, blocks.Select(block => block.MirrorMedia));
            Assert.NotEqual(blocks[0].SideCue, blocks[1].SideCue);
            Assert.All(blocks, block => Assert.NotEqual(ExerciseSequenceSideCue.None, block.SideCue));
        }

        // Interlacing fingers does not make otherwise bilateral work unilateral.
        // The self-hug and tutting clips already exchange upper/lower arm roles.
        foreach (int id in new[] { 32, 216, 237, 238, 255, 307, 310, 398, 520, 522, 562, 740 })
            Assert.False(exercises.Single(exercise => exercise.Id == id).SideSequence.UsesTimedSides());
        Assert.Equal("Standing Overhead Side-Stretch Hold", exercises.Single(exercise => exercise.Id == 958).Name);
    }

    [Fact]
    public void BundledCatalogSatisfiesEligibilityAndWorkoutProfiles()
    {
        Exercise[] exercises = JsonSerializer.Deserialize<Exercise[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
            JsonOptions)!;
        WorkoutModifierPairCoverageDeficiency[] pairwiseDeficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises).ToArray();
        Assert.Empty(pairwiseDeficiencies);
        Assert.Equal(
            1,
            WorkoutModifierPolicy.GetMinimumExercisesPerPairStatePerGroup(3));
        Assert.Equal(
            1,
            WorkoutModifierPolicy.GetMinimumExercisesPerPairStatePerGroup(30));
        WorkoutHardFloorCategoryCoverageDeficiency[] hardFloorCategoryDeficiencies =
            WorkoutModifierPolicy
                .FindHardFloorCategoryCoverageDeficiencies(exercises)
                .ToArray();
        Assert.Empty(hardFloorCategoryDeficiencies);
        // Demand-category counts and percentage materiality remain in the
        // diagnostic ledger. They do not define whether a workout is playable.
        WorkoutProfileCompletionDeficiency[] lineupDeficiencies =
            WorkoutModifierPolicy.FindCompleteLineupDeficiencies(exercises).ToArray();
        Assert.Empty(lineupDeficiencies);
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        IReadOnlyDictionary<int, Exercise> sequenceRootByExerciseId = exercises
            .Where(root => root.SequenceBlocks.Length > 0)
            .SelectMany(root => root.SequenceBlocks
                .Select(block => (block.ExerciseId, Root: root)))
            .DistinctBy(entry => entry.ExerciseId)
            .ToDictionary(entry => entry.ExerciseId, entry => entry.Root);
        Parallel.ForEach(
            WorkoutModifierPolicy.ValidationProfiles,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            },
            profile =>
            {
                // Profiles are independent. Give each one a deterministic
                // random stream and service so exhaustive validation can use
                // separate cores without sharing mutable session state.
                    var profileService = new ExerciseSessionService(
                        exercises,
                        new Random(1));
                foreach (int minutes in ExerciseSessionService.SupportedWorkoutMinutes)
                {
                    var profileState = new WorkoutState();
                    try
                    {
                        profileService.StartWorkout(profileState, minutes, profile);
                    }
                    catch (Exception exception)
                    {
                        throw new InvalidOperationException(
                            $"Workout generation failed for {minutes} minutes " +
                            $"with modifier profile {profile}.",
                            exception);
                    }
                    WorkoutGroup[] activeGroups = profileService
                        .GetActiveGroups(profileState)
                        .ToArray();
                    Exercise[] baseSelections = activeGroups
                        .GroupBy(group => group.SelectionKey, StringComparer.Ordinal)
                        .Select(rounds => profileService.GetSelectedExercise(
                            profileState,
                            rounds.First()))
                        .ToArray();
                    int distinctMovementCount = baseSelections
                        .Select(WorkoutModifierPolicy.GetSessionMovementId).Distinct().Count();
                    if (distinctMovementCount < baseSelections.Length)
                    {
                        WorkoutGroup[] availableGroups = MassGroupingTaxonomy
                            .GetResolution(Math.Min(minutes, 30)).Groups
                            .Where(group => WorkoutModifierPolicy.IsSelectionGroupAvailable(group, profile))
                            .ToArray();
                        Assert.True(WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                            exercises, availableGroups, profile, minutes) < availableGroups.Length,
                            $"Repeated a movement despite a complete distinct lineup: {minutes} minutes, {profile}.");
                    }
                    Assert.All(profileService.GetActiveGroups(profileState), group =>
                        Assert.True(WorkoutModifierPolicy.IsCompatible(
                            profileService.GetSelectedExercise(profileState, group),
                            profile)));
                    IReadOnlyList<WorkoutGroup> resolutionGroups =
                        MassGroupingTaxonomy.GetResolution(
                            minutes > 30 ? 30 : minutes).Groups;
                    Assert.All(
                        activeGroups.GroupBy(
                            group => group.SelectionKey,
                            StringComparer.Ordinal),
                        rounds =>
                        {
                            Exercise selectedMember =
                                profileService.GetSelectedExercise(
                                    profileState,
                                    rounds.First());
                            Exercise root =
                                sequenceRootByExerciseId[selectedMember.Id];
                            Assert.Contains(
                                WorkoutSequencePolicy.GetPlacementOptions(
                                    root,
                                    exercisesById,
                                    resolutionGroups),
                                placement => placement.Any(group =>
                                    group.Id == rounds.Key));
                        });
                    Assert.Equal(minutes, activeGroups.Length);
                }
            });
        var profileService = new ExerciseSessionService(exercises, new Random(1));
        WorkoutModifiers allModifiers = WorkoutModifiers.Insect |
            WorkoutModifiers.Silence |
            WorkoutModifiers.Mirror;
        foreach (int minutes in ExerciseSessionService.SupportedWorkoutMinutes)
        {
            var profileState = new WorkoutState();
            profileService.StartWorkout(profileState, minutes, allModifiers);
            Assert.Equal(allModifiers, profileState.ActiveWorkoutModifiers);
            WorkoutGroup[] activeGroups = profileService
                .GetActiveGroups(profileState)
                .ToArray();
            Assert.All(activeGroups, group =>
                Assert.True(WorkoutModifierPolicy.IsCompatible(
                    profileService.GetSelectedExercise(profileState, group),
                    allModifiers)));
            IReadOnlyList<WorkoutGroup> resolutionGroups =
                MassGroupingTaxonomy.GetResolution(
                    minutes > 30 ? 30 : minutes).Groups;
            Assert.All(
                activeGroups.GroupBy(group => group.SelectionKey, StringComparer.Ordinal),
                rounds =>
                {
                    Exercise selectedMember = profileService.GetSelectedExercise(
                        profileState,
                        rounds.First());
                    Exercise root = sequenceRootByExerciseId[selectedMember.Id];
                    Assert.Contains(
                        WorkoutSequencePolicy.GetPlacementOptions(
                            root,
                            exercisesById,
                            resolutionGroups),
                        placement => placement.Any(group => group.Id == rounds.Key));
                });
        }
    }

    [Fact]
    public void BundledCatalogHasReviewedMetadataAndSequences()
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        string json = File.ReadAllText(catalogPath);
        using JsonDocument document = JsonDocument.Parse(json);
        Exercise[] exercises = JsonSerializer.Deserialize<Exercise[]>(json, JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");

        Assert.DoesNotContain(document.RootElement.EnumerateArray(), element =>
            element.TryGetProperty("muscleGroups", out _));
        Assert.True(exercises.Length >= 300);
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Id).Distinct().Count());
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Name).Distinct().Count());
        Assert.Equal(exercises.Length, exercises.Select(exercise => exercise.Video).Distinct().Count());
        Assert.All(document.RootElement.EnumerateArray(), element =>
        {
            Assert.True(element.TryGetProperty("muscularDemand", out JsonElement value));
            Assert.Equal(JsonValueKind.Number, value.ValueKind);
            Assert.InRange(
                value.GetInt32(),
                Exercise.MinimumMuscularDemand,
                Exercise.MaximumMuscularDemand);
            Assert.True(element.TryGetProperty(
                "wallRequired",
                out JsonElement wallRequired));
            Assert.Contains(
                wallRequired.ValueKind,
                new[] { JsonValueKind.True, JsonValueKind.False });
            Assert.True(element.TryGetProperty(
                "soleWallContactRequired",
                out JsonElement soleWallContactRequired));
            Assert.Contains(
                soleWallContactRequired.ValueKind,
                new[] { JsonValueKind.True, JsonValueKind.False });
            Assert.False(
                soleWallContactRequired.GetBoolean() &&
                !wallRequired.GetBoolean());
        });
        Assert.Equal(119, exercises.Count(exercise => exercise.MuscularDemand == 0));
        Assert.Equal(289, exercises.Count(exercise => exercise.MuscularDemand == 1));
        Assert.Equal(137, exercises.Count(exercise => exercise.MuscularDemand == 2));
        Assert.All(Enum.GetValues<CanonicalMuscleGroup>(), canonicalGroup =>
            Assert.Contains(exercises, exercise =>
                exercise.PrimaryCanonicalGroup == canonicalGroup));
        HashSet<int> reviewedAbdominalSecondaryIds =
        [
            124, 125, 132, 176, 219, 338, 394, 408, 577, 636, 684, 790, 818, 825, 884, 885, 905, 917, 960, 973, 998, 1008, 1010, 1013, 1014, 1018, 1022, 1032,
        ];
        Assert.True(reviewedAbdominalSecondaryIds.SetEquals(exercises
            .Where(exercise => exercise.SecondaryCanonicalGroups.Contains(
                CanonicalMuscleGroup.AbdominalWall))
            .Select(exercise => exercise.Id)));
        Assert.All(new[] { 266, 287, 591, 603, 701 }, exerciseId =>
            Assert.DoesNotContain(
                CanonicalMuscleGroup.AbdominalWall,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SecondaryCanonicalGroups));

        Exercise squatObliqueCrunch = exercises.Single(exercise =>
            exercise.Id == 132);
        Assert.Equal(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            squatObliqueCrunch.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.MaximumMuscularDemand,
            squatObliqueCrunch.MuscularDemand);
        Assert.Contains(
            CanonicalMuscleGroup.AbdominalWall,
            squatObliqueCrunch.SecondaryCanonicalGroups);
        Exercise widePlieSideBend = exercises.Single(exercise =>
            exercise.Id == 905);
        Assert.Equal(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            widePlieSideBend.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.MaximumMuscularDemand,
            widePlieSideBend.MuscularDemand);
        Assert.Contains(
            CanonicalMuscleGroup.AbdominalWall,
            widePlieSideBend.SecondaryCanonicalGroups);

        Assert.All(new[] { 910, 948, 954 }, exerciseId => Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            exercises.Single(exercise => exercise.Id == exerciseId)
                .PrimaryCanonicalGroup));
        Assert.DoesNotContain(exercises, exercise =>
            exercise.SecondaryCanonicalGroups.Contains(
                CanonicalMuscleGroup.PelvicFloorAndPerineum));
        Exercise pelvicFloorSlowSqueeze = exercises.Single(exercise =>
            exercise.Id == 918);
        Assert.Equal(
            "Standing Pelvic-Floor Slow Squeeze and Release",
            pelvicFloorSlowSqueeze.Name);
        Assert.Equal(
            CanonicalMuscleGroup.PelvicFloorAndPerineum,
            pelvicFloorSlowSqueeze.PrimaryCanonicalGroup);
        Assert.Empty(pelvicFloorSlowSqueeze.SecondaryCanonicalGroups);
        Assert.Equal(
            Exercise.ModerateMuscularDemand,
            pelvicFloorSlowSqueeze.MuscularDemand);
        Assert.True(pelvicFloorSlowSqueeze.Silent);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            pelvicFloorSlowSqueeze.HardFloorCompatibility);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            pelvicFloorSlowSqueeze.SideSequence);
        Exercise auditedHeadTurnMarch = exercises.Single(exercise =>
            exercise.Id == 919);
        Assert.Equal(
            "Marching in Place with Head Turns",
            auditedHeadTurnMarch.Name);
        Assert.Equal(
            CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
            auditedHeadTurnMarch.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.PosteriorNeckAndSuboccipitalMuscles,
            },
            auditedHeadTurnMarch.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(
            Exercise.MinimumMuscularDemand,
            auditedHeadTurnMarch.MuscularDemand);
        Assert.True(auditedHeadTurnMarch.Silent);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            auditedHeadTurnMarch.HardFloorCompatibility);
        Assert.Equal(
            ExerciseInsectCompatibility.Compatible,
            auditedHeadTurnMarch.InsectCompatibility);
        Assert.Equal(
            ExerciseShyCompatibility.Compatible,
            auditedHeadTurnMarch.ShyCompatibility);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            auditedHeadTurnMarch.SideSequence);
        Assert.Single(auditedHeadTurnMarch.SequenceBlocks);
        Exercise wallKneeRaise = exercises.Single(exercise =>
            exercise.Id == 911);
        Assert.Equal(
            "Wall-Supported High-Knee Raise",
            wallKneeRaise.Name);
        Assert.Equal(
            CanonicalMuscleGroup.HipFlexors,
            wallKneeRaise.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.ModerateMuscularDemand,
            wallKneeRaise.MuscularDemand);
        Assert.True(wallKneeRaise.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            wallKneeRaise.HardFloorCompatibility);
        Assert.Equal(
            ExerciseSideSequence.ScreenLeftThenRight,
            wallKneeRaise.SideSequence);

        Exercise wallSitArmRaise = exercises.Single(exercise =>
            exercise.Id == 913);
        Assert.Equal("Shallow Wall Sit with Overhead Arm Raises", wallSitArmRaise.Name);
        Assert.Equal(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            wallSitArmRaise.PrimaryCanonicalGroup);
        Assert.Equal(
            Exercise.ModerateMuscularDemand,
            wallSitArmRaise.MuscularDemand);
        Assert.True(wallSitArmRaise.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            wallSitArmRaise.HardFloorCompatibility);
        Assert.Equal(
            ExerciseUpperBodyClothingRequirement.ClothingRequired,
            wallSitArmRaise.UpperBodyClothingRequirement);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            wallSitArmRaise.SideSequence);
        Exercise standingPelvicTilt = exercises.Single(exercise =>
            exercise.Id == 916);
        Assert.Equal(
            "Standing Pelvic-Tilt Repetitions",
            standingPelvicTilt.Name);
        Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            standingPelvicTilt.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.SpinalExtensors,
            },
            standingPelvicTilt.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(
            Exercise.MinimumMuscularDemand,
            standingPelvicTilt.MuscularDemand);
        Assert.False(standingPelvicTilt.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            standingPelvicTilt.HardFloorCompatibility);
        Assert.Equal(
            ExerciseMirrorRelationship.BenefitsGreatly,
            standingPelvicTilt.MirrorRelationship);
        Assert.Equal(
            ExerciseMirrorCoverage.FullBody,
            standingPelvicTilt.MinimumMirrorCoverage);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            standingPelvicTilt.SideSequence);
        Exercise standingSpinalWave = exercises.Single(exercise =>
            exercise.Id == 917);
        Assert.Equal("Standing Spinal Wave", standingSpinalWave.Name);
        Assert.Equal(
            CanonicalMuscleGroup.DeepAndIntersegmentalBack,
            standingSpinalWave.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.AbdominalWall,
                CanonicalMuscleGroup.SpinalExtensors,
            },
            standingSpinalWave.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(
            Exercise.MinimumMuscularDemand,
            standingSpinalWave.MuscularDemand);
        Assert.False(standingSpinalWave.WallRequired);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            standingSpinalWave.HardFloorCompatibility);
        Assert.Equal(
            ExerciseMirrorRelationship.BenefitsGreatly,
            standingSpinalWave.MirrorRelationship);
        Assert.Equal(
            ExerciseMirrorCoverage.UpperBody,
            standingSpinalWave.MinimumMirrorCoverage);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            standingSpinalWave.SideSequence);
        // Exact families individually adjudicated during the catalog audit.
        Dictionary<int, int[]> expectedSessionMovements = new()
        {
            [31] = [31, 169, 486],
            [101] = [101, 110],
            [102] = [102, 111, 147],
            [104] = [19, 103, 104, 105, 107, 109, 136, 199, 626],
            [113] = [113, 135],
            [115] = [115, 532, 996, 997],
            [117] = [116, 117, 123, 150],
            [120] = [120, 184],
            [124] = [124, 636],
            [134] = [134, 137],
            [143] = [143, 538],
            [153] = [153, 603],
            [159] = [159, 649],
            [160] = [160, 533],
            [177] = [177, 186],
            [187] = [187, 252, 253, 254, 562, 566, 581, 582],
            [192] = [192, 195],
            [211] = [211, 233],
            [214] = [214, 223, 236, 755, 756],
            [231] = [231, 685],
            [256] = [256, 845, 958],
            [262] = [262, 507],
            [264] = [264, 275],
            [266] = [266, 301],
            [270] = [270, 677],
            [277] = [261, 277],
            [278] = [278, 326],
            [286] = [286, 541],
            [288] = [288, 758],
            [292] = [148, 292, 542],
            [311] = [311, 321, 816],
            [327] = [327, 546],
            [329] = [329, 531],
            [437] = [437, 1002],
            [475] = [475, 919],
            [480] = [480, 517],
            [529] = [529, 539],
            [549] = [549, 570],
            [550] = [550, 551],
            [568] = [568, 633],
            [579] = [579, 580],
            [625] = [625, 1021],
            [712] = [712, 1012],
            [1010] = [1010, 1018],
            [1026] = [1026, 1027],
            [969] = [969, 1028, 1031],
            [948] = [948, 949],
        };
        Dictionary<int, int[]> actualSessionMovements = exercises
            .Where(exercise => exercise.SessionMovementId > 0)
            .GroupBy(exercise => exercise.SessionMovementId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(exercise => exercise.Id).Order().ToArray());
        Assert.Equal(expectedSessionMovements.Keys.Order(), actualSessionMovements.Keys.Order());
        Assert.All(expectedSessionMovements, expected => Assert.Equal(
            expected.Value,
            actualSessionMovements[expected.Key]));
        Assert.All(exercises, exercise => Assert.Equal(0, exercise.Score));
        Assert.Equal(0, exercises.Single(exercise => exercise.Id == 211).MuscularDemand);
        Assert.Equal(1, exercises.Single(exercise => exercise.Id == 264).MuscularDemand);
        Assert.Equal(2, exercises.Single(exercise => exercise.Id == 101).MuscularDemand);
        Exercise miniSquatCalfRaise = exercises.Single(exercise => exercise.Id == 565);
        Assert.Equal("Mini-Squat Calf Raises with Forward Reach", miniSquatCalfRaise.Name);
        Assert.Equal(CanonicalMuscleGroup.Soleus, miniSquatCalfRaise.PrimaryCanonicalGroup);
        Assert.Equal(1, miniSquatCalfRaise.MuscularDemand);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            miniSquatCalfRaise.HardFloorCompatibility);
        Assert.Contains(
            CanonicalMuscleGroup.CalfDeepPosteriorLegAndPlantarFoot,
            miniSquatCalfRaise.SecondaryCanonicalGroups);
        Exercise forwardMarchingArmCircles = exercises.Single(exercise =>
            exercise.Id == 302);
        Assert.Equal("Marching Forward Arm Circles", forwardMarchingArmCircles.Name);
        Assert.Equal(
            new[] { 302, 304 },
            forwardMarchingArmCircles.SequenceBlocks.Select(block => block.ExerciseId));
        Assert.All(
            new[] { 302, 304 },
            exerciseId => Assert.Equal(
                1,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .MuscularDemand));
        Exercise standingMarchTwist = exercises.Single(exercise => exercise.Id == 305);
        Assert.Equal("Standing March with Torso Twist", standingMarchTwist.Name);
        Assert.Equal(ExerciseSideSequence.Alternating, standingMarchTwist.SideSequence);
        Assert.Equal(1, standingMarchTwist.MuscularDemand);
        Exercise neckFlexion = exercises.Single(exercise => exercise.Id == 307);
        Assert.Equal(
            new[] { 307, 310 },
            neckFlexion.SequenceBlocks.Select(block => block.ExerciseId));
        Assert.All(
            new[] { 307, 308, 309, 310 },
            exerciseId =>
            {
                Exercise exercise = exercises.Single(candidate => candidate.Id == exerciseId);
                Assert.Equal("Self-resistance", exercise.Practice);
                Assert.Equal(ExerciseMode.Hold, exercise.Mode);
                Assert.Equal(Exercise.MaximumMuscularDemand, exercise.MuscularDemand);
                Assert.Equal(
                    ExerciseInsectCompatibility.Incompatible,
                    exercise.InsectCompatibility);
                Assert.Equal(
                    ExerciseHardFloorCompatibility.Compatible,
                    exercise.HardFloorCompatibility);
            });
        Assert.Equal(
            ExerciseSideSequence.ScreenLeftThenRight,
            exercises.Single(exercise => exercise.Id == 308).SideSequence);
        Assert.Equal(
            ExerciseSideSequence.ScreenRightThenLeft,
            exercises.Single(exercise => exercise.Id == 309).SideSequence);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.InsectCompatibility == ExerciseInsectCompatibility.Unreviewed);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Unreviewed);
        Assert.Equal(353, exercises.Count(exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Compatible));
        Assert.Equal(192, exercises.Count(exercise =>
            exercise.HardFloorCompatibility == ExerciseHardFloorCompatibility.Incompatible));
        Assert.All(
            new[] { 37, 194, 610, 326 },
            exerciseId => Assert.Equal(
                ExerciseHardFloorCompatibility.Incompatible,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .HardFloorCompatibility));
        Assert.All(
            new[] { 101, 167, 367, 187, 252, 253, 254, 565, 566, 581, 582, 114, 138, 141, 191, 197, 212, 389, 414, 415, 416, 549, 550, 551, 552, 555, 557, 570, 571 },
            exerciseId => Assert.Equal(
                ExerciseHardFloorCompatibility.Compatible,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .HardFloorCompatibility));
        // Whole-body displacement counts even when the arms maintain their shape.
        Assert.All(
            new[] { 103, 110, 120, 184, 971, 986 },
            exerciseId => Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                exercises.Single(exercise => exercise.Id == exerciseId).InsectCompatibility));
        Assert.All(
            new[] { 16, 187, 252, 253, 254, 255, 272, 562, 565, 566, 569, 581, 582, 389, 549, 552, 555 },
            exerciseId => Assert.Equal(
                ExerciseInsectCompatibility.Incompatible,
                exercises.Single(exercise => exercise.Id == exerciseId).InsectCompatibility));
        var pogoHeadMovements = new Dictionary<int, string>
        {
            [439] = "Pogo Bounces with Fixed-Gaze Head Turns",
            [442] = "Pogo Bounces with Fixed-Gaze Head Nods",
            [444] = "Pogo Bounces with Fixed-Gaze Head Tilts",
        };
        Assert.All(pogoHeadMovements, expected =>
        {
            Exercise exercise = exercises.Single(candidate =>
                candidate.Id == expected.Key);
            Assert.Equal(expected.Value, exercise.Name);
            Assert.StartsWith("PogoHead", exercise.MotionProfile);
            Assert.Equal(1, exercise.MuscularDemand);
            Assert.False(exercise.Silent);
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                exercise.InsectCompatibility);
            Assert.Equal(
                ExerciseHardFloorCompatibility.Incompatible,
                exercise.HardFloorCompatibility);
            Assert.Contains(
                CanonicalMuscleGroup.CalfDeepPosteriorLegAndPlantarFoot,
                exercise.SecondaryCanonicalGroups);
        });
        Exercise[] airborneImpactExercises = exercises.Where(exercise =>
            System.Text.RegularExpressions.Regex.IsMatch(
                exercise.Name,
                @"(?i)\b(?:jump(?:ing|s)?|hop(?:ping|s)?|pogo|bounce(?:s)?|jack(?:s)?|bound(?:s|ing)?)\b") ||
            System.Text.RegularExpressions.Regex.IsMatch(
                exercise.MotionProfile,
                @"(?:Jump|Hop|Pogo|Bounce|Jack|Bound)"))
            .ToArray();
        Assert.NotEmpty(airborneImpactExercises);
        Assert.All(airborneImpactExercises, exercise => Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            exercise.HardFloorCompatibility));
        Assert.DoesNotContain(exercises, exercise =>
            exercise.UpperBodyClothingRequirement ==
                ExerciseUpperBodyClothingRequirement.Unreviewed);
        Assert.Equal(
            new HashSet<int> { 134, 137, 165, 175, 579, 580, 801, 913 },
            exercises.Where(exercise =>
                    exercise.UpperBodyClothingRequirement ==
                        ExerciseUpperBodyClothingRequirement.ClothingRequired)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(
            new HashSet<int> { 524, 525, 526, 527, 528, 790 },
            exercises.Where(exercise =>
                    exercise.UpperBodyClothingRequirement ==
                        ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(531, exercises.Count(exercise =>
            exercise.UpperBodyClothingRequirement == ExerciseUpperBodyClothingRequirement.Agnostic));
        Assert.DoesNotContain(exercises, exercise =>
            exercise.ShyCompatibility == ExerciseShyCompatibility.Unreviewed);
        HashSet<int> shyIncompatibleExerciseIds =
        [
            56, 58, 59, 92, 93, 98, 108, 176, 178, 181, 182, 183, 185, 188,
            190, 203, 220, 224, 231, 242, 258, 269, 276, 283, 285, 286, 290, 291,
            294, 327, 377, 379, 392, 398, 399, 400, 401, 402, 403, 404, 405, 408,
            410, 411, 413, 414, 415, 416, 418, 419, 442, 444, 449, 474, 478,
            480, 481, 484, 489, 490, 491, 492, 493, 495, 497, 498, 499, 500, 501,
            505, 506, 511, 513, 514, 515, 517, 518, 520, 521, 522, 523, 524, 525,
            526, 527, 528, 535, 536, 541, 543, 545, 546, 556, 557, 561, 588, 608,
            609, 666, 678, 681, 684, 685, 687, 790, 916, 917, 993, 1003, 1007, 1008, 1010, 1011, 1013, 1014, 1017, 1018,
        ];
        Assert.Equal(
            shyIncompatibleExerciseIds,
            exercises.Where(exercise =>
                    exercise.ShyCompatibility ==
                        ExerciseShyCompatibility.Incompatible)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(428, exercises.Count(exercise =>
            exercise.ShyCompatibility == ExerciseShyCompatibility.Compatible));
        Assert.All(
            new[] { 56, 185, 377, 379, 401, 403, 557 },
            exerciseId => Assert.Equal(
                ExerciseShyCompatibility.Incompatible,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .ShyCompatibility));
        Assert.All(
            new[] { 19, 107, 227, 264, 279, 389, 439, 611, 987 },
            exerciseId => Assert.Equal(
                ExerciseShyCompatibility.Compatible,
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .ShyCompatibility));

        Exercise headTurnMarch = exercises.Single(exercise => exercise.Id == 202);
        Assert.Equal("Fixed-Gaze Head-Turn March", headTurnMarch.Name);
        Assert.Equal(CanonicalMuscleGroup.CranialMuscles, headTurnMarch.PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.PosteriorNeckAndSuboccipitalMuscles,
                CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
            },
            headTurnMarch.SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(0, headTurnMarch.MuscularDemand);
        Assert.Equal(ExerciseSideSequence.Continuous, headTurnMarch.SideSequence);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Compatible,
            headTurnMarch.HardFloorCompatibility);
        Assert.Equal(
            ExerciseShyCompatibility.Compatible,
            headTurnMarch.ShyCompatibility);
        Assert.Equal(ExerciseMirrorRelationship.BenefitsGreatly, headTurnMarch.MirrorRelationship);
        Assert.Equal(ExerciseMirrorCoverage.UpperBody, headTurnMarch.MinimumMirrorCoverage);

        foreach ((int exerciseId, string expectedName) in new[]
                 {
                     (204, "Jab-Cross-Hook-Uppercut Combo"),
                     (205, "Jab-Cross-Speed-Bag Combo"),
                 })
        {
            Exercise boxingCombo = exercises.Single(exercise =>
                exercise.Id == exerciseId);
            Assert.Equal(expectedName, boxingCombo.Name);
            Assert.Equal(1, boxingCombo.MuscularDemand);
            Assert.Equal(
                exerciseId == 204
                    ? ExerciseSideSequence.ScreenRightLeadThenLeftLead
                    : ExerciseSideSequence.ScreenLeftLeadThenRightLead,
                boxingCombo.SideSequence);
            Assert.Equal(
                ExerciseHardFloorCompatibility.Incompatible,
                boxingCombo.HardFloorCompatibility);
            Assert.Equal(
                ExerciseShyCompatibility.Compatible,
                boxingCombo.ShyCompatibility);
            Assert.Equal(
                ExerciseMirrorRelationship.BenefitsGreatly,
                boxingCombo.MirrorRelationship);
            Assert.Equal(
                ExerciseMirrorCoverage.UpperBody,
                boxingCombo.MinimumMirrorCoverage);
            Assert.Equal(2, boxingCombo.SequenceBlocks.Length);
            Assert.False(boxingCombo.SequenceBlocks[0].MirrorMedia);
            Assert.True(boxingCombo.SequenceBlocks[1].MirrorMedia);
        }
        Assert.Equal(
            CanonicalMuscleGroup.Chest,
            exercises.Single(exercise => exercise.Id == 204)
                .PrimaryCanonicalGroup);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            exercises.Single(exercise => exercise.Id == 205)
                .PrimaryCanonicalGroup);
        Assert.Equal(
            new HashSet<CanonicalMuscleGroup>
            {
                CanonicalMuscleGroup.Chest,
                CanonicalMuscleGroup.ElbowExtensors,
            },
            exercises.Single(exercise => exercise.Id == 205)
                .SecondaryCanonicalGroups.ToHashSet());
        Assert.Equal(
            ExerciseMirrorRelationship.BenefitsGreatly,
            exercises.Single(exercise => exercise.Id == 265).MirrorRelationship);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.Unreviewed);
        Assert.Equal(99, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.BenefitsGreatly));
        Assert.Equal(446, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic));
        Assert.Equal(0, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly));
        Assert.Equal(0, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody));
        Assert.Equal(0, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody));
        Assert.Equal(40, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.BenefitsGreatly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody));
        Assert.Equal(59, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.BenefitsGreatly &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody));
        Assert.Equal(446, exercises.Count(exercise =>
            exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic &&
            exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.None));
        Assert.DoesNotContain(exercises, exercise => exercise.Id == 90);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.Name.StartsWith("Mirror-Guided ", StringComparison.Ordinal));
        Assert.Equal(
            new HashSet<int>(),
            exercises.Where(exercise =>
                    exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
                    exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.UpperBody)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Assert.Equal(
            new HashSet<int>(),
            exercises.Where(exercise =>
                    exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly &&
                    exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.FullBody)
                .Select(exercise => exercise.Id)
                .ToHashSet());
        Exercise mostMuscularPose = exercises.Single(exercise => exercise.Id == 790);
        Assert.Equal("Most-Muscular Posing, Hands on Thighs", mostMuscularPose.Name);
        Assert.Equal(CanonicalMuscleGroup.Chest, mostMuscularPose.PrimaryCanonicalGroup);
        Assert.Equal(ExerciseMode.Hold, mostMuscularPose.Mode);
        Assert.Equal(ExercisePresentation.Still, mostMuscularPose.Presentation);
        Assert.Equal(1, mostMuscularPose.MuscularDemand);
        Assert.Equal(ExerciseHardFloorCompatibility.Compatible,
            mostMuscularPose.HardFloorCompatibility);
        int[] bodybuildingPosingIds = [524, 525, 526, 527, 528, 790];
        Assert.All(bodybuildingPosingIds, exerciseId =>
        {
            Exercise pose = exercises.Single(exercise => exercise.Id == exerciseId);
            Assert.Contains("Posing", pose.Name);
            Assert.Equal(ExerciseMode.Hold, pose.Mode);
            Assert.Equal(ExercisePresentation.Still, pose.Presentation);
            Assert.Equal(50, pose.HoldFramePercent);
        });
        Exercise standingVacuum = exercises.Single(exercise => exercise.Id == 993);
        Assert.Equal("Standing Stomach Vacuum, Then Release", standingVacuum.Name);
        Assert.Equal(CanonicalMuscleGroup.AbdominalWall, standingVacuum.PrimaryCanonicalGroup);
        Assert.Empty(standingVacuum.SecondaryCanonicalGroups);
        Assert.Equal(ExerciseMode.Repetition, standingVacuum.Mode);
        Assert.Equal(1, standingVacuum.MuscularDemand);
        Assert.All(
            exercises.Where(exercise =>
                new HashSet<int> { 94, 95, 99, 100, 497, 498, 500, 511, 514 }
                    .Contains(exercise.Id)),
            exercise =>
            {
                Assert.Equal(ExerciseMirrorRelationship.Agnostic, exercise.MirrorRelationship);
                Assert.Equal("None", exercise.Equipment);
            });
        Assert.All(
            exercises.Where(exercise =>
                exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly),
            exercise => Assert.Equal("Mirror", exercise.Equipment));
        Assert.All(
            exercises.Where(exercise =>
                exercise.MirrorRelationship != ExerciseMirrorRelationship.MirrorOnly),
            exercise => Assert.Equal("None", exercise.Equipment));
        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises));
        Exercise[] wallRequired = exercises
            .Where(exercise => exercise.WallRequired)
            .ToArray();
        Exercise[] baseWallRequired = wallRequired
            .Where(exercise => !exercise.SoleWallContactRequired)
            .ToArray();
        Exercise[] soleWallRequired = wallRequired
            .Where(exercise => exercise.SoleWallContactRequired)
            .ToArray();
        Assert.Equal(35, wallRequired.Length);
        Assert.Equal(29, baseWallRequired.Length);
        Assert.Equal(
            26,
            baseWallRequired
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
        Assert.Equal(6, soleWallRequired.Length);
        Assert.Equal(
            new HashSet<int> { 563, 564, 567, 568, 574, 633 },
            soleWallRequired.Select(exercise => exercise.Id).ToHashSet());
        Assert.Equal(
            5,
            soleWallRequired
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
        Assert.Empty(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));
        Assert.Empty(WorkoutModifierPolicy
            .FindSoleWallContactRequiredCatalogDeficiencies(exercises));
        Assert.All(wallRequired, exercise =>
        {
            Assert.False(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.None));
        });
        Assert.All(baseWallRequired, exercise =>
        {
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Wall |
                    WorkoutModifiers.UpperBodyClothing));
        });
        WorkoutModifiers soleWallProfile =
            WorkoutModifierPolicy.WithWallEquipment(
                WorkoutModifiers.UpperBodyClothing,
                WallEquipment.SolesMayTouch);
        Assert.All(soleWallRequired, exercise =>
        {
            Assert.False(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Wall));
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                soleWallProfile));
        });
        Assert.All(
            exercises.Where(exercise => exercise.Mode == ExerciseMode.Hold),
            exercise => Assert.Equal(
                ExerciseInsectCompatibility.Incompatible,
                exercise.InsectCompatibility));
        Exercise[] breathingExercises = exercises
            .Where(exercise =>
                exercise.PrimaryCanonicalGroup == CanonicalMuscleGroup.BreathingMuscles)
            .ToArray();
        Assert.Equal(7, breathingExercises.Length);
        Assert.All(breathingExercises, exercise =>
            Assert.Matches(
                "(?i)\\b(inhale|exhale|breath|laugh|laughter)",
                exercise.Name));
        Exercise overheadBreathingFlow = exercises.Single(exercise => exercise.Id == 395);
        Assert.Equal(
            "Alternating Knee Lift and Overhead Reach",
            overheadBreathingFlow.Name);
        Assert.Equal(ExerciseMode.Repetition, overheadBreathingFlow.Mode);
        Assert.Equal(ExercisePresentation.Motion, overheadBreathingFlow.Presentation);
        Assert.Equal(
            CanonicalMuscleGroup.HipFlexors,
            overheadBreathingFlow.PrimaryCanonicalGroup);
        Assert.Equal(new[] { CanonicalMuscleGroup.ShoulderAbductors },
            overheadBreathingFlow.SecondaryCanonicalGroups);
        Exercise alternatingSideTap = exercises.Single(exercise => exercise.Id == 397);
        Assert.Equal(
            "Alternating Side Tap with Diagonal Reach",
            alternatingSideTap.Name);
        Assert.Equal(ExerciseSideSequence.Alternating, alternatingSideTap.SideSequence);
        ExerciseSequenceBlock alternatingSideTapBlock = Assert.Single(
            alternatingSideTap.SequenceBlocks);
        Assert.Equal(alternatingSideTap.Id, alternatingSideTapBlock.ExerciseId);
        Assert.Equal(ExerciseSequenceSideCue.None, alternatingSideTapBlock.SideCue);
        Assert.False(alternatingSideTapBlock.MirrorMedia);
        Assert.Equal(CanonicalMuscleGroup.ShoulderAbductors,
            alternatingSideTap.PrimaryCanonicalGroup);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.BreathingMuscles,
            alternatingSideTap.SecondaryCanonicalGroups);
        Assert.Empty(alternatingSideTap.SecondaryCanonicalGroups);
        Exercise standingKneeExtensionHold = exercises.Single(exercise => exercise.Id == 145);
        Assert.Equal("Wall-Supported Standing Knee-Extension Hold", standingKneeExtensionHold.Name);
        Assert.Equal(ExerciseMode.Hold, standingKneeExtensionHold.Mode);
        Assert.Equal(ExercisePresentation.Still, standingKneeExtensionHold.Presentation);
        Assert.Equal(50, standingKneeExtensionHold.HoldFramePercent);
        Assert.Equal(
            ExerciseSideSequence.ScreenLeftThenRight,
            standingKneeExtensionHold.SideSequence);
        Exercise[] timedSideExercises = exercises
            .Where(exercise => exercise.SideSequence.UsesTimedSides())
            .ToArray();
        Assert.Equal(199, timedSideExercises.Length);
        Dictionary<int, (string Name, ExerciseSideSequence SideSequence)>
            auditedMirroredSideSequences = new()
            {
                [483] = ("Standing Diagonal Head Turns",
                    ExerciseSideSequence.ScreenLeftThenRight),
                [493] = ("Diagonal Finger Tracking",
                    ExerciseSideSequence.ScreenLeftThenRight),
                [587] = ("Isometric Shoulder External Rotation Against Wall",
                    ExerciseSideSequence.ScreenLeftThenRight),
            };
        Assert.All(auditedMirroredSideSequences, expected =>
        {
            Exercise exercise = exercises.Single(candidate => candidate.Id == expected.Key);
            Assert.Equal(expected.Value.Name, exercise.Name);
            Assert.Equal(expected.Value.SideSequence, exercise.SideSequence);
            Assert.Equal(2, exercise.SequenceBlocks.Length);
            Assert.All(exercise.SequenceBlocks, block =>
                Assert.Equal(exercise.Id, block.ExerciseId));
            ExerciseSequenceSideCue expectedFirstCue =
                expected.Value.SideSequence == ExerciseSideSequence.ScreenLeftThenRight
                    ? ExerciseSequenceSideCue.ScreenLeft
                    : ExerciseSequenceSideCue.ScreenRight;
            ExerciseSequenceSideCue expectedSecondCue =
                expectedFirstCue == ExerciseSequenceSideCue.ScreenLeft
                    ? ExerciseSequenceSideCue.ScreenRight
                    : ExerciseSequenceSideCue.ScreenLeft;
            Assert.Equal(expectedFirstCue, exercise.SequenceBlocks[0].SideCue);
            Assert.Equal(expectedSecondCue, exercise.SequenceBlocks[1].SideCue);
            Assert.False(exercise.SequenceBlocks[0].MirrorMedia);
            Assert.True(exercise.SequenceBlocks[1].MirrorMedia);
        });
        Assert.DoesNotContain(
            timedSideExercises.Where(exercise =>
                !exercise.SideSequence.UsesTimedLeadStances()),
            exercise =>
            exercise.Name.StartsWith("Alternating ", StringComparison.Ordinal));
        Exercise[] alternatingExercises = exercises
            .Where(exercise =>
                exercise.SideSequence == ExerciseSideSequence.Alternating)
            .ToArray();
        Assert.Equal(152, alternatingExercises.Length);
        Assert.DoesNotContain(alternatingExercises, exercise => exercise.Id == 219);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 15);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 429);
        Assert.DoesNotContain(alternatingExercises, exercise => exercise.Id == 398);
        Assert.DoesNotContain(alternatingExercises, exercise => exercise.Id == 515);
        Assert.Contains(alternatingExercises, exercise => exercise.Id == 919);
        Exercise[] timedDirectionExercises = exercises
            .Where(exercise =>
                exercise.DirectionSequence != ExerciseDirectionSequence.None)
            .ToArray();
        Dictionary<int, ExerciseDirectionSequence> auditedDirectionSequences = new()
        {
            [406] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [409] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [561] = ExerciseDirectionSequence.ClockwiseThenCounterclockwise,
            [608] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [611] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
            [1018] = ExerciseDirectionSequence.CounterclockwiseThenClockwise,
        };
        Assert.Equal(
            auditedDirectionSequences.Keys.ToHashSet(),
            timedDirectionExercises.Select(exercise => exercise.Id).ToHashSet());
        Assert.All(auditedDirectionSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key)
                    .DirectionSequence));
        Dictionary<int, int[]> auditedMultiMemberSequences = new()
        {
            [96] = [96, 540],
            [115] = [115, 532],
            [178] = [178, 535],
            [179] = [179, 539],
            [180] = [180, 534],
            [181] = [181, 536],
            [211] = [211, 213],
            [214] = [214, 755],
            [220] = [220, 543],
            [223] = [223, 756],
            [252] = [252, 253, 254],
            [264] = [264, 406],
            [285] = [285, 545],
            [288] = [288, 758],
            [291] = [291, 294],
            [302] = [302, 304],
            [307] = [307, 310],
            [367] = [367, 529],
            [392] = [392, 399, 400],
            [393] = [393, 537],
            [415] = [415, 416],
            [420] = [420, 421, 426],
            [459] = [459, 468, 469],
            [465] = [465, 445],
            [491] = [491, 501],
            [500] = [500, 505, 506],
            [502] = [502, 503],
            [566] = [566, 581, 582],
            [610] = [610, 232],
            [612] = [612, 530],
            [617] = [617, 620],
            [742] = [742, 338],
            [784] = [784, 969, 1000],
            [834] = [834, 914],
            [910] = [910, 962],
            [948] = [948, 949],
        };
        int[] actualMultiMemberRoots = exercises
            .Where(root => root.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .Count() > 1)
            .Select(root => root.Id)
            .Order()
            .ToArray();
        Assert.Equal(auditedMultiMemberSequences.Keys.Order(), actualMultiMemberRoots);
        Assert.All(auditedMultiMemberSequences, expected =>
        {
            Exercise root = exercises.Single(exercise => exercise.Id == expected.Key);
            int[] expectedBlocks = expected.Value
                .SelectMany(memberId =>
                {
                    Exercise member = exercises.Single(exercise => exercise.Id == memberId);
                    int sideBlocks = member.SideSequence.UsesTimedSides() ? 2 : 1;
                    int directionBlocks = member.DirectionSequence ==
                            ExerciseDirectionSequence.None
                        ? 1
                        : 2;
                    return Enumerable.Repeat(memberId, sideBlocks * directionBlocks);
                })
                .ToArray();
            Assert.Equal(
                expectedBlocks,
                root.SequenceBlocks.Select(block => block.ExerciseId).ToArray());
            Assert.Single(root.SequenceBlocks
                .Select(block => exercises.Single(exercise =>
                    exercise.Id == block.ExerciseId).Mode)
                .Distinct());
        });
        Dictionary<int, int> expectedSequenceBlockDistribution = new()
        {
            [1] = 301,
            [2] = 167,
            [3] = 21,
            [4] = 13,
        };
        Dictionary<int, int> actualSequenceBlockDistribution = exercises
            .Where(exercise => exercise.SequenceBlocks.Length > 0)
            .GroupBy(exercise => exercise.SequenceBlocks.Length)
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(
            expectedSequenceBlockDistribution.Keys.Order(),
            actualSequenceBlockDistribution.Keys.Order());
        Assert.All(expectedSequenceBlockDistribution, expected => Assert.Equal(
            expected.Value,
            actualSequenceBlockDistribution[expected.Key]));
        var sequenceOwners = exercises
            .Where(exercise => exercise.SequenceBlocks.Length > 0)
            .SelectMany(root => root.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .Select(memberId => (RootId: root.Id, MemberId: memberId)))
            .ToArray();
        Assert.Equal(exercises.Length, sequenceOwners.Length);
        Assert.All(
            sequenceOwners.GroupBy(owner => owner.MemberId),
            owners => Assert.Single(owners));
        Assert.Equal(
            exercises.Select(exercise => exercise.Id).Order(),
            sequenceOwners.Select(owner => owner.MemberId).Order());
        Assert.All(
            exercises.Where(exercise => exercise.SequenceBlocks.Length > 0),
            root => Assert.Contains(
                root.SequenceBlocks,
                block => block.ExerciseId == root.Id));
        string[] oneWayCircleTerms =
        [
            "Clockwise", "Counterclockwise", "Forward", "Backward",
            "Inward", "Outward",
        ];
        HashSet<int> sequenceMemberIds = exercises
            .Where(exercise => exercise.SequenceBlocks.Length > 0)
            .SelectMany(exercise => exercise.SequenceBlocks
                .Select(block => block.ExerciseId))
            .ToHashSet();
        Assert.DoesNotContain(exercises, exercise =>
            exercise.Name.EndsWith("Circles", StringComparison.Ordinal) &&
            oneWayCircleTerms.Any(term => exercise.Name.Contains(
                term,
                StringComparison.Ordinal)) &&
            !sequenceMemberIds.Contains(exercise.Id));
        int[] declaredReplacementIds = exercises
            .Where(exercise => !string.IsNullOrWhiteSpace(exercise.RetiredName))
            .Select(exercise => exercise.Id)
            .Order()
            .ToArray();
        Assert.Equal(
            CatalogMigrationRules.ReplacedExerciseIds
                .Except(CatalogMigrationRules.PermanentlyRetiredExerciseIds)
                .Order(),
            declaredReplacementIds);
        Assert.DoesNotContain(exercises, exercise =>
            CatalogMigrationRules.PermanentlyRetiredExerciseIds.Contains(exercise.Id));
        Assert.All(
            exercises.Where(exercise =>
                CatalogMigrationRules.ReplacedExerciseIds.Contains(exercise.Id)),
            exercise => Assert.NotEqual(exercise.Name, exercise.RetiredName));

        Dictionary<int, ExerciseSideSequence> auditedSideSequences = new()
        {
            [58] = ExerciseSideSequence.ScreenLeftThenRight,
            [98] = ExerciseSideSequence.Alternating,
            [115] = ExerciseSideSequence.ScreenLeftThenRight,
            [116] = ExerciseSideSequence.Alternating,
            [117] = ExerciseSideSequence.ScreenLeftThenRight,
            [123] = ExerciseSideSequence.ScreenLeftThenRight,
            [126] = ExerciseSideSequence.ScreenLeftThenRight,
            [135] = ExerciseSideSequence.Continuous,
            [143] = ExerciseSideSequence.ScreenRightThenLeft,
            [184] = ExerciseSideSequence.ScreenRightThenLeft,
            [186] = ExerciseSideSequence.ScreenRightThenLeft,
            [211] = ExerciseSideSequence.ScreenLeftThenRight,
            [212] = ExerciseSideSequence.Continuous,
            [213] = ExerciseSideSequence.ScreenLeftThenRight,
            [214] = ExerciseSideSequence.ScreenRightThenLeft,
            [215] = ExerciseSideSequence.ScreenLeftThenRight,
            [216] = ExerciseSideSequence.Continuous,
            [217] = ExerciseSideSequence.ScreenLeftThenRight,
            [218] = ExerciseSideSequence.Continuous,
            [220] = ExerciseSideSequence.ScreenLeftThenRight,
            [232] = ExerciseSideSequence.ScreenRightThenLeft,
            [233] = ExerciseSideSequence.ScreenRightThenLeft,
            [225] = ExerciseSideSequence.ScreenLeftThenRight,
            [234] = ExerciseSideSequence.Continuous,
            [236] = ExerciseSideSequence.Continuous,
            [237] = ExerciseSideSequence.Continuous,
            [239] = ExerciseSideSequence.Continuous,
            [240] = ExerciseSideSequence.Continuous,
            [241] = ExerciseSideSequence.Continuous,
            [242] = ExerciseSideSequence.Alternating,
            [245] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [256] = ExerciseSideSequence.ScreenRightThenLeft,
            [257] = ExerciseSideSequence.Continuous,
            [258] = ExerciseSideSequence.Alternating,
            [268] = ExerciseSideSequence.Continuous,
            [269] = ExerciseSideSequence.ScreenLeftThenRight,
            [278] = ExerciseSideSequence.ScreenRightThenLeft,
            [279] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [283] = ExerciseSideSequence.ScreenLeftThenRight,
            [289] = ExerciseSideSequence.Continuous,
            [291] = ExerciseSideSequence.Alternating,
            [292] = ExerciseSideSequence.ScreenRightThenLeft,
            [293] = ExerciseSideSequence.ScreenLeftThenRight,
            [294] = ExerciseSideSequence.ScreenRightThenLeft,
            [305] = ExerciseSideSequence.Alternating,
            [308] = ExerciseSideSequence.ScreenLeftThenRight,
            [309] = ExerciseSideSequence.ScreenRightThenLeft,
            [326] = ExerciseSideSequence.ScreenRightThenLeft,
            [338] = ExerciseSideSequence.ScreenLeftThenRight,
            [31] = ExerciseSideSequence.Alternating,
            [176] = ExerciseSideSequence.Alternating,
            [195] = ExerciseSideSequence.ScreenLeftThenRight,
            [198] = ExerciseSideSequence.ScreenRightThenLeft,
            [219] = ExerciseSideSequence.ScreenRightThenLeft,
            [248] = ExerciseSideSequence.Alternating,
            [265] = ExerciseSideSequence.ScreenLeftLeadThenRightLead,
            [274] = ExerciseSideSequence.Alternating,
            [280] = ExerciseSideSequence.ScreenRightThenLeft,
            [287] = ExerciseSideSequence.Alternating,
            [282] = ExerciseSideSequence.ScreenLeftThenRight,
            [390] = ExerciseSideSequence.Alternating,
            [391] = ExerciseSideSequence.Alternating,
            [394] = ExerciseSideSequence.Alternating,
            [395] = ExerciseSideSequence.Alternating,
            [397] = ExerciseSideSequence.Alternating,
            [398] = ExerciseSideSequence.Continuous,
            [407] = ExerciseSideSequence.Continuous,
            [408] = ExerciseSideSequence.ScreenRightThenLeft,
            [410] = ExerciseSideSequence.ScreenLeftThenRight,
            [411] = ExerciseSideSequence.ScreenLeftThenRight,
            [413] = ExerciseSideSequence.Alternating,
            [414] = ExerciseSideSequence.ScreenRightThenLeft,
            [415] = ExerciseSideSequence.ScreenRightThenLeft,
            [416] = ExerciseSideSequence.ScreenRightThenLeft,
            [417] = ExerciseSideSequence.Continuous,
            [418] = ExerciseSideSequence.Alternating,
            [419] = ExerciseSideSequence.ScreenLeftThenRight,
            [421] = ExerciseSideSequence.Continuous,
            [427] = ExerciseSideSequence.Alternating,
            [468] = ExerciseSideSequence.Alternating,
            [473] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [482] = ExerciseSideSequence.Continuous,
            [483] = ExerciseSideSequence.ScreenLeftThenRight,
            [507] = ExerciseSideSequence.Alternating,
            [508] = ExerciseSideSequence.Alternating,
            [512] = ExerciseSideSequence.ScreenRightThenLeft,
            [513] = ExerciseSideSequence.ScreenLeftThenRight,
            [515] = ExerciseSideSequence.Continuous,
            [576] = ExerciseSideSequence.Alternating,
            [577] = ExerciseSideSequence.ScreenRightThenLeft,
            [575] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [578] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [583] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [563] = ExerciseSideSequence.ScreenRightThenLeft,
            [564] = ExerciseSideSequence.ScreenRightThenLeft,
            [567] = ExerciseSideSequence.ScreenRightThenLeft,
            [568] = ExerciseSideSequence.ScreenRightThenLeft,
            [574] = ExerciseSideSequence.ScreenLeftThenRight,
            [591] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [611] = ExerciseSideSequence.Continuous,
            [617] = ExerciseSideSequence.ScreenLeftThenRight,
            [618] = ExerciseSideSequence.ScreenRightThenLeft,
            [619] = ExerciseSideSequence.Continuous,
            [620] = ExerciseSideSequence.ScreenLeftThenRight,
            [648] = ExerciseSideSequence.ScreenRightThenLeft,
            [649] = ExerciseSideSequence.ScreenRightThenLeft,
            [572] = ExerciseSideSequence.ScreenLeftThenRight,
            [636] = ExerciseSideSequence.Alternating,
            [685] = ExerciseSideSequence.ScreenLeftThenRight,
            [686] = ExerciseSideSequence.ScreenLeftThenRight,
            [745] = ExerciseSideSequence.ScreenRightThenLeft,
            [816] = ExerciseSideSequence.Alternating,
            [834] = ExerciseSideSequence.ScreenLeftThenRight,
            [884] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [885] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [886] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [887] = ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            [910] = ExerciseSideSequence.ScreenLeftThenRight,
            [996] = ExerciseSideSequence.ScreenLeftThenRight,
            [997] = ExerciseSideSequence.ScreenLeftThenRight,
            [998] = ExerciseSideSequence.Alternating,
            [999] = ExerciseSideSequence.Alternating,
        };
        Assert.All(auditedSideSequences, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).SideSequence));

        int[] leadStanceExerciseIds =
        [
            204, 205, 245, 265, 279, 473, 528, 538, 575, 578, 583, 591,
            884, 885, 886, 887, 1024,
        ];
        Assert.Equal(
            leadStanceExerciseIds,
            exercises
                .Where(exercise => exercise.SideSequence.UsesTimedLeadStances())
                .Select(exercise => exercise.Id)
                .Order()
                .ToArray());
        Assert.All(
            leadStanceExerciseIds,
            exerciseId => Assert.True(
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SideSequence
                    .UsesTimedSides()));

        Exercise forwardSideLegCircles = exercises.Single(exercise => exercise.Id == 617);
        Exercise backwardSideLegCircles = exercises.Single(exercise => exercise.Id == 620);
        Assert.Equal("Standing Forward Side-Leg Circles", forwardSideLegCircles.Name);
        Assert.Equal("Standing Backward Side-Leg Circles", backwardSideLegCircles.Name);
        Assert.NotEqual(forwardSideLegCircles.Video, backwardSideLegCircles.Video);
        Assert.Equal(
            CanonicalMuscleGroup.HipAbductors,
            backwardSideLegCircles.PrimaryCanonicalGroup);

        int[] auditedSidedClarityReplacementIds =
        [
            16, 20, 47, 97, 117, 179, 180, 184, 186, 195, 198, 211,
            213, 220, 225, 256, 269, 278, 279, 282, 283, 285, 294, 326,
            329, 396, 512, 513, 556, 572, 577, 618, 685, 745, 834,
        ];
        Assert.All(auditedSidedClarityReplacementIds, exerciseId =>
            Assert.True(
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SideSequence
                    .UsesTimedSides()));

        int[] auditedContinuousClarityReplacementIds =
        [
            15, 17, 19, 31, 107, 135, 150, 169, 176, 193, 201, 218,
            230, 234, 237, 239, 240, 241, 242, 248, 251, 257, 258, 262,
            263, 266, 268, 270, 275, 286, 289, 291, 301, 314, 321, 394,
            395, 397, 413, 421, 425, 427, 468, 507, 516, 615, 636, 677,
            683, 687,
        ];
        Assert.All(auditedContinuousClarityReplacementIds, exerciseId =>
            Assert.False(
                exercises.Single(exercise => exercise.Id == exerciseId)
                    .SideSequence
                    .UsesTimedSides()));
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            exercises.Single(exercise => exercise.Id == 118).SideSequence);
        Exercise externalRotation = exercises.Single(exercise => exercise.Id == 268);
        Assert.Equal(
            "Goalpost-to-T Arm Extensions",
            externalRotation.Name);
        Assert.Equal(ExerciseMode.Repetition, externalRotation.Mode);
        Assert.Equal(ExercisePresentation.Motion, externalRotation.Presentation);
        Assert.Equal(ExerciseSideSequence.Continuous, externalRotation.SideSequence);
        Assert.Equal(0, externalRotation.HoldFramePercent);
        Exercise unsupportedSissySquat = exercises.Single(exercise => exercise.Id == 212);
        Assert.Equal(
            "Unsupported Sissy Squat",
            unsupportedSissySquat.Name);
        Assert.Equal(ExerciseMode.Repetition, unsupportedSissySquat.Mode);
        Assert.Equal(ExercisePresentation.Motion, unsupportedSissySquat.Presentation);
        Assert.Equal(0, unsupportedSissySquat.HoldFramePercent);
        Assert.Equal(
            ExerciseSideSequence.Continuous,
            unsupportedSissySquat.SideSequence);

        Dictionary<int, string> auditedCorrectedNames = new()
        {
            [31] = "Alternating Knee Raises with Two-Arm Pull-Down",
            [219] = "High-Knee Cross-Body Pull",
            [21] = "Alternating Single-Leg Hinge with Forward Reach",
            [105] = "Wide Turned-Out Squat",
            [115] = "Pistol Squat with Bottom Pause",
            [119] = "Tiptoe Walking Back and Forth",
            [126] = "Squat with Side Kick",
            [135] = "Wide Overhead Squat Hold",
            [139] = "Wide-Squat Alternating Heel Raises",
            [145] = "Wall-Supported Standing Knee-Extension Hold",
            [188] = "Narrow Turned-Out Shallow Squat",
            [193] = "Hip Hinge with Overhead Reach",
            [195] = "Side Lunge to Knee-Up Balance",
            [197] = "Squat to Calf Raise",
            [198] = "Wide Squat to Feet-Together Calf Raise",
            [199] = "Horse-Stance Squat",
            [211] = "Assisted Standing Wrist-Flexion Stretch",
            [212] = "Unsupported Sissy Squat",
            [213] = "Assisted Standing Wrist-Extension Stretch",
            [214] = "Single-Arm Wrist Circles",
            [215] = "Forearm Pronation-Supination Flow",
            [216] = "Interlaced-Finger Palm-Out Stretch",
            [217] = "Tree Pose Hold",
            [218] = "Fingertip Wall Push-Ups",
            [223] = "Controlled Wrist Circles",
            [224] = "Qigong Interlaced Wrist Rolls",
            [225] = "Opposite-Hand Fist-Down Wrist Stretch",
            [231] = "Step-In Karate Reverse Punch",
            [232] = "Extended Side Angle Hold",
            [233] = "Standing Wrist Flexion Stretch",
            [234] = "Standing W Extensions",
            [236] = "Bilateral Wrist Circles",
            [237] = "Standing Overhead Elbow Extensions",
            [239] = "Back-of-Hands Wrist Stretch",
            [240] = "Grapevine Step",
            [241] = "Isometric Palm Press Hold",
            [242] = "Jazz Square",
            [245] = "Jab-Jab-Cross-Shovel-Hook Combo",
            [246] = "Bodyweight Cuban Rotation",
            [248] = "Alternating Side-Tap Palm Pushes",
            [251] = "Forward Fold to Overhead Reach",
            [256] = "Overhead Side-Stretch Hold",
            [257] = "Finger Spreading with Arms Held Forward",
            [258] = "Alternating Karate Downward Blocks",
            [262] = "Standing Bicycle Crunches",
            [270] = "Goalpost Arm Hold",
            [282] = "Side-Step Knee Drive with Alternating Side Punches",
            [283] = "Rear-Hand Palm Strike",
            [288] = "Standing Knee-and-Ankle Circles",
            [289] = "Fist Opening and Closing with Arms Held Forward",
            [290] = "Thumb and Little-Finger Switches",
            [291] = "Inward Knife-Hand Strikes",
            [294] = "Rear-Hand Outward Knife-Hand Strike",
            [326] = "Rear-Hand Straight Punch",
            [338] = "Overhead Triceps Stretch with Side Bend",
            [390] = "Step-Touch with Goalpost Arm Openings",
            [391] = "Alternating High-Knee Inner-Foot Taps",
            [394] = "Alternating Cross-Body Knee with Arm Sweep",
            [395] = "Alternating Knee Lift and Overhead Reach",
            [396] = "Standing Front-to-Side Knee Lifts",
            [397] = "Alternating Side Tap with Diagonal Reach",
            [398] = "Inhale Arms Open, Exhale Arms Together",
            [399] = "Inhale Chest Open, Exhale Arms Close with Shallow Squat",
            [400] = "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down",
            [401] = "Alternating Inhale-Twist, Exhale-Push",
            [264] = "Standing Arm Circles",
            [275] = "Small Arm Circles",
            [406] = "Standing Wheel Arm Circles",
            [409] = "Full Neck Circles",
            [417] = "Narrow-Stance Overhead-to-Toe Reach",
            [460] = "Jogging in Place with Arm Circles",
            [483] = "Standing Diagonal Head Turns",
            [490] = "Track Finger Side to Side, Head Still",
            [501] = "Keep Eyes on Finger While Turning Head",
            [507] = "Alternating Cross-Body Knee-to-Elbow Crunch",
            [508] = "Alternating Side Taps with Forward Overhead Raises",
            [510] = "Clasped-Hands Chest-Opening Forward-Fold Hold",
            [996] = "Partial Pistol Squat",
            [997] = "Bottom Pistol Squat Hold",
            [998] = "Deep-Squat Thoracic Rotation",
            [999] = "Deep-Squat Walk",
            [512] = "Standing Upper-Back and Neck Hug Stretch",
            [513] = "Single-Leg Head Nods",
            [95] = "Single-Leg Knee-Raise Hold",
            [556] = "Single-Arm Backfist",
            [561] = "Tiptoe Turn with Head Spot",
            [562] = "First-Position Calf Raises",
            [563] = "Hip Airplane with Back Foot on Wall",
            [564] = "Standing Foot-to-Wall Press Hold",
            [565] = "Mini-Squat Calf Raises with Forward Reach",
            [566] = "Parallel Calf Raises",
            [567] = "Rear-Foot-on-Wall Split Squat",
            [568] = "Toes-on-Wall Calf Stretch",
            [574] = "Wall Toe Taps",
            [581] = "Toes-In Calf Raises",
            [582] = "Toes-Out Calf Raises",
            [615] = "Alternating Hamstring Curl with Prayer-to-Open Arms",
            [577] = "Standing Side Crunch and Side Kick",
            [618] = "Single-Side Knee Raise with Torso Twist",
            [654] = "Alternating Side Leg Lifts and Heel Curls",
            [834] = "Single-Side Diagonal Knee Drive with Overhead Pull",
            [915] = "Single-Side Split-Stance Knee Drive with Overhead Reach",
            [588] = "Belly-Dance Alternating Shoulder Rolls",
            [591] = "Shadow Boxing",
            [608] = "Hip Circles",
            [611] = "Wide-Stance Hip Circles",
            [626] = "Sumo Squat Hold",
            [649] = "Standing Side-Leg Raise with Knee Extension",
            [686] = "Standing Knee-to-Chest Glute Stretch",
            [687] = "Horse-Stance Alternating Straight Punches",
            [712] = "Standing Arms-Back Chest-Opener Hold",
            [755] = "Outward Wrist Circles",
            [756] = "Outward Controlled Wrist Circles",
            [758] = "Backward Knee-and-Ankle Circles",
            [743] = "Standing Large Arm Circles",
            [843] = "Arm-Behind-Back Assisted Side Neck Stretch",
            [969] = "Chair-Pose Hold",
            [1000] = "Standing Forward-Fold Hold",
        };
        Assert.All(auditedCorrectedNames, expected =>
            Assert.Equal(
                expected.Value,
                exercises.Single(exercise => exercise.Id == expected.Key).Name));
        string[] retiredFillerNames =
        [
            "Cumbia Two-Step",
            "Merengue Six-Count Step",
            "Salsa Front-and-Back Basic",
            "Reggaeton Single-Single-Double Step",
            "Basic Mambo Step",
            "Cha-Cha Basic Step",
            "Bachata Side-to-Side Basic",
            "Five-Position Tendon Glide",
            "Pony Step",
        ];
        Assert.DoesNotContain(exercises, exercise =>
            retiredFillerNames.Contains(exercise.Name, StringComparer.Ordinal));
        Exercise fingertipWallPushUps = exercises.Single(exercise => exercise.Id == 218);
        Assert.True(fingertipWallPushUps.WallRequired);
        Assert.Equal(
            CanonicalMuscleGroup.IntrinsicHand,
            fingertipWallPushUps.PrimaryCanonicalGroup);
        Assert.Equal(Exercise.MaximumMuscularDemand, fingertipWallPushUps.MuscularDemand);
        Exercise knifeHandSequence = exercises.Single(exercise => exercise.Id == 291);
        Assert.Equal(
            new[] { 291, 294, 294 },
            knifeHandSequence.SequenceBlocks.Select(block => block.ExerciseId));
        Assert.All(
            new[] { 283, 291, 294, 556 },
            exerciseId =>
            {
                Exercise exercise = exercises.Single(candidate => candidate.Id == exerciseId);
                Assert.Equal(
                    ExerciseMirrorRelationship.BenefitsGreatly,
                    exercise.MirrorRelationship);
            });
        Assert.Equal(ExerciseSideSequence.Alternating, knifeHandSequence.SideSequence);

        Exercise wideStanceReach = exercises.Single(exercise => exercise.Id == 193);
        Assert.Equal(1, wideStanceReach.MuscularDemand);
        Assert.Equal(
            CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
            wideStanceReach.PrimaryCanonicalGroup);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            wideStanceReach.SecondaryCanonicalGroups);

        Exercise narrowStanceReach = exercises.Single(exercise => exercise.Id == 417);
        Assert.Equal(0, narrowStanceReach.MuscularDemand);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.CranialMuscles,
            narrowStanceReach.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
            narrowStanceReach.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.PosteriorNeckAndSuboccipitalMuscles,
            narrowStanceReach.SecondaryCanonicalGroups);

        Exercise kneeRaiseHold = exercises.Single(exercise => exercise.Id == 95);
        Assert.Equal(ExerciseMode.Hold, kneeRaiseHold.Mode);
        Assert.Equal(ExercisePresentation.Still, kneeRaiseHold.Presentation);
        Assert.Equal(50, kneeRaiseHold.HoldFramePercent);
        Assert.Equal(
            ExerciseInsectCompatibility.Incompatible,
            kneeRaiseHold.InsectCompatibility);
        Assert.DoesNotContain(exercises, exercise =>
            exercise.Id is 267 or 553 or 558 or 559);

        int[] explicitSingleSideKneeExerciseIds =
            [618, 834, 915];
        Assert.All(explicitSingleSideKneeExerciseIds, exerciseId =>
        {
            Exercise exercise = exercises.Single(candidate => candidate.Id == exerciseId);
            Assert.StartsWith("Single-Side ", exercise.Name);
            Assert.True(exercise.SideSequence.UsesTimedSides());
        });
        Exercise alternatingKneeCrunch =
            exercises.Single(exercise => exercise.Id == 507);
        Assert.Equal(
            "Alternating Cross-Body Knee-to-Elbow Crunch",
            alternatingKneeCrunch.Name);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            alternatingKneeCrunch.SideSequence);
        Assert.Equal(
            CanonicalMuscleGroup.AbdominalWall,
            alternatingKneeCrunch.PrimaryCanonicalGroup);
        Assert.Contains(
            CanonicalMuscleGroup.HipFlexors,
            alternatingKneeCrunch.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.ScapularGirdle,
            alternatingKneeCrunch.SecondaryCanonicalGroups);
        Assert.Single(alternatingKneeCrunch.SequenceBlocks);
        Assert.Equal(262, alternatingKneeCrunch.SessionMovementId);
        Exercise highKneeSideReach = exercises.Single(exercise => exercise.Id == 618);
        Assert.Equal(
            CanonicalMuscleGroup.HipFlexors,
            highKneeSideReach.PrimaryCanonicalGroup);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.PelvicFloorAndPerineum,
            highKneeSideReach.SecondaryCanonicalGroups);

        Assert.Equal(
            CanonicalMuscleGroup.ElbowExtensors,
            exercises.Single(exercise => exercise.Id == 283).PrimaryCanonicalGroup);

        Exercise shadowBoxing = exercises.Single(exercise => exercise.Id == 591);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            shadowBoxing.PrimaryCanonicalGroup);
        Assert.Equal(
            ExerciseSideSequence.ScreenRightLeadThenLeftLead,
            shadowBoxing.SideSequence);
        Assert.Equal(ExerciseMode.Repetition, shadowBoxing.Mode);
        Assert.Equal(ExercisePresentation.Motion, shadowBoxing.Presentation);
        Assert.Contains(
            CanonicalMuscleGroup.Chest,
            shadowBoxing.SecondaryCanonicalGroups);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.AbdominalWall,
            shadowBoxing.SecondaryCanonicalGroups);

        Exercise alternatingUppercuts = exercises.Single(exercise => exercise.Id == 287);
        Assert.Equal(
            "Wide-Stance Alternating Uppercuts",
            alternatingUppercuts.Name);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            alternatingUppercuts.PrimaryCanonicalGroup);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            alternatingUppercuts.SideSequence);
        Assert.Single(alternatingUppercuts.SequenceBlocks);
        Assert.Equal(
            ExerciseHardFloorCompatibility.Incompatible,
            alternatingUppercuts.HardFloorCompatibility);
        Assert.DoesNotContain(
            CanonicalMuscleGroup.AbdominalWall,
            alternatingUppercuts.SecondaryCanonicalGroups);
        Assert.Contains(
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            alternatingUppercuts.SecondaryCanonicalGroups);

        Exercise restoredShoulderRaise = exercises.Single(exercise => exercise.Id == 266);
        Assert.Equal("Alternating Overhead Arm Raises", restoredShoulderRaise.Name);
        Assert.Equal("Standing Palms-Up Arm Raise", restoredShoulderRaise.RetiredName);
        Assert.Equal(
            CanonicalMuscleGroup.ShoulderAbductors,
            restoredShoulderRaise.PrimaryCanonicalGroup);
        Assert.Equal(
            ExerciseSideSequence.Alternating,
            restoredShoulderRaise.SideSequence);
        Assert.Equal(ExerciseMode.Repetition, restoredShoulderRaise.Mode);
        Assert.Equal(ExercisePresentation.Motion, restoredShoulderRaise.Presentation);
        Assert.Equal(0, restoredShoulderRaise.HoldFramePercent);
        Assert.Empty(restoredShoulderRaise.SecondaryCanonicalGroups);

        Assert.Contains(exercises, exercise => exercise.Silent);
        Assert.Contains(exercises, exercise => !exercise.Silent);

        Assert.All(exercises, exercise =>
        {
            Assert.InRange(exercise.Id, 1, int.MaxValue);
            Assert.True(Enum.IsDefined(exercise.PrimaryCanonicalGroup));
            Assert.Equal(
                exercise.SecondaryCanonicalGroups.Length,
                exercise.SecondaryCanonicalGroups.Distinct().Count());
            Assert.DoesNotContain(
                exercise.PrimaryCanonicalGroup,
                exercise.SecondaryCanonicalGroups);
            Assert.All(exercise.SecondaryCanonicalGroups, group =>
                Assert.True(Enum.IsDefined(group)));
            Assert.True(exercise.OnlyFeetTouchGround);
            Assert.True(exercise.ShoeAgnostic);
            Assert.InRange(exercise.MaxSpaceMeters, 1, 2);
            Assert.Equal(
                exercise.MirrorRelationship == ExerciseMirrorRelationship.MirrorOnly
                    ? "Mirror"
                    : "None",
                exercise.Equipment);
            Assert.Equal(
                exercise.MirrorRelationship == ExerciseMirrorRelationship.Agnostic,
                exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.None);
            Assert.False(string.IsNullOrWhiteSpace(exercise.Practice));
            Assert.False(string.IsNullOrWhiteSpace(exercise.MotionProfile));
            Assert.True(Enum.IsDefined(exercise.SideSequence));
            Assert.True(Enum.IsDefined(exercise.DirectionSequence));
            Assert.True(Enum.IsDefined(exercise.Presentation));
            Assert.Equal(0, exercise.Score);

            if (exercise.Presentation == ExercisePresentation.Still)
            {
                Assert.Equal(ExerciseMode.Hold, exercise.Mode);
            }

            if (exercise.Mode == ExerciseMode.Hold)
            {
                Assert.Matches(
                    "(?i)\\b(hold|isometric|pose|posing|stance|stretch|sit|against)\\b",
                    exercise.Name);
                Assert.InRange(exercise.HoldFramePercent, 1, 99);
                Assert.True(
                    File.Exists(Path.Combine(
                        AppContext.BaseDirectory,
                        "Assets",
                        "exercise_hold_frames",
                        $"exercise_{exercise.Id:D4}.png")),
                    $"Exercise {exercise.Id} has no reviewed hold frame.");
            }
            else
            {
                Assert.Equal(0, exercise.HoldFramePercent);
            }
        });
    }
}
