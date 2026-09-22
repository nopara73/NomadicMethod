using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutMuscleBalancePolicyTests
{
    [Fact]
    public void CompleteWorkloadTableCountsEveryPrimaryAndDistinctSecondary()
    {
        Exercise minimum = CreateExercise(
            1,
            CanonicalMuscleGroup.HipAbductors,
            Exercise.MinimumMuscularDemand,
            CanonicalMuscleGroup.GlutealExtensors);
        Exercise moderate = CreateExercise(
            2,
            CanonicalMuscleGroup.AbdominalWall,
            Exercise.ModerateMuscularDemand,
            CanonicalMuscleGroup.GlutealExtensors);
        Exercise hard = CreateExercise(
            3,
            CanonicalMuscleGroup.ElbowFlexors,
            Exercise.MaximumMuscularDemand,
            CanonicalMuscleGroup.GlutealExtensors,
            CanonicalMuscleGroup.GlutealExtensors);

        IReadOnlyDictionary<CanonicalMuscleGroup, int> load =
            WorkoutMuscleBalancePolicy.CalculateCanonicalLoadEighthUnits(
                [minimum, moderate, hard]);

        Assert.Equal(2, load[CanonicalMuscleGroup.HipAbductors]);
        Assert.Equal(4, load[CanonicalMuscleGroup.AbdominalWall]);
        Assert.Equal(8, load[CanonicalMuscleGroup.ElbowFlexors]);
        Assert.Equal(7, load[CanonicalMuscleGroup.GlutealExtensors]);
    }

    [Fact]
    public void OneIdentityCountsOncePerSetAndRepeatedSetsCountAgain()
    {
        Exercise sideSpecific = CreateExercise(
            1,
            CanonicalMuscleGroup.HipAbductors,
            Exercise.MaximumMuscularDemand,
            CanonicalMuscleGroup.GlutealExtensors);

        IReadOnlyDictionary<CanonicalMuscleGroup, int> oneSet =
            WorkoutMuscleBalancePolicy.CalculateCanonicalLoadEighthUnits(
                [sideSpecific]);
        IReadOnlyDictionary<CanonicalMuscleGroup, int> twoSets =
            WorkoutMuscleBalancePolicy.CalculateCanonicalLoadEighthUnits(
                [sideSpecific, sideSpecific]);

        Assert.Equal(8, oneSet[CanonicalMuscleGroup.HipAbductors]);
        Assert.Equal(4, oneSet[CanonicalMuscleGroup.GlutealExtensors]);
        Assert.Equal(16, twoSets[CanonicalMuscleGroup.HipAbductors]);
        Assert.Equal(8, twoSets[CanonicalMuscleGroup.GlutealExtensors]);
    }

    [Fact]
    public void EveryResolutionSumsItsCanonicalChildrenWithoutDoubleCounting()
    {
        var canonicalLoad = new Dictionary<CanonicalMuscleGroup, int>
        {
            [CanonicalMuscleGroup.MedialAndDeepKneeExtensors] = 8,
            [CanonicalMuscleGroup.PosteriorThighAndKneeFlexors] = 4,
        };

        MuscleBalanceEvaluation evaluation =
            WorkoutMuscleBalancePolicy.Evaluate(canonicalLoad);
        MuscleResolutionBalance threeMinute = evaluation.Resolutions
            .Single(resolution => resolution.Minutes == 3);
        MuscleResolutionBalance thirtyMinute = evaluation.Resolutions
            .Single(resolution => resolution.Minutes == 30);
        WorkoutGroup lowerLimbs = MassGroupingTaxonomy.GetGroup(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        WorkoutGroup kneeExtensors = MassGroupingTaxonomy.GetGroup(
            30,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);
        WorkoutGroup kneeFlexors = MassGroupingTaxonomy.GetGroup(
            30,
            CanonicalMuscleGroup.PosteriorThighAndKneeFlexors);

        Assert.Equal(
            12,
            threeMinute.LoadEighthUnitsByGroupId[lowerLimbs.Id]);
        Assert.Equal(
            8,
            thirtyMinute.LoadEighthUnitsByGroupId[kneeExtensors.Id]);
        Assert.Equal(
            4,
            thirtyMinute.LoadEighthUnitsByGroupId[kneeFlexors.Id]);
    }

    [Fact]
    public void OneQuarterIsTheInclusiveBalanceGoalAtEveryResolution()
    {
        var balancedEvaluation = new MuscleBalanceEvaluation(
        [
            ResolutionBalance(minutes: 3, weakest: 2, strongest: 8),
            ResolutionBalance(minutes: 5, weakest: 4, strongest: 8),
        ]);
        var weakerEvaluation = new MuscleBalanceEvaluation(
        [
            ResolutionBalance(minutes: 3, weakest: 1, strongest: 8),
            ResolutionBalance(minutes: 5, weakest: 4, strongest: 8),
        ]);

        Assert.True(balancedEvaluation.IsBalanced);
        Assert.False(weakerEvaluation.IsBalanced);
        Assert.True(WorkoutMuscleBalancePolicy.Compare(
            balancedEvaluation,
            weakerEvaluation) > 0);
    }

    [Fact]
    public void ComparisonImprovesTheWeakestResolutionBeforeTheOthers()
    {
        var firstEvaluation = new MuscleBalanceEvaluation(
        [
            ResolutionBalance(minutes: 3, weakest: 1, strongest: 8),
            ResolutionBalance(minutes: 5, weakest: 2, strongest: 8),
        ]);
        var secondEvaluation = new MuscleBalanceEvaluation(
        [
            ResolutionBalance(minutes: 3, weakest: 2, strongest: 8),
            ResolutionBalance(minutes: 5, weakest: 1, strongest: 8),
        ]);
        var improvedEvaluation = new MuscleBalanceEvaluation(
        [
            ResolutionBalance(minutes: 3, weakest: 2, strongest: 8),
            ResolutionBalance(minutes: 5, weakest: 3, strongest: 8),
        ]);

        Assert.Equal(
            0,
            WorkoutMuscleBalancePolicy.Compare(
                firstEvaluation,
                secondEvaluation));
        Assert.True(WorkoutMuscleBalancePolicy.Compare(
            improvedEvaluation,
            firstEvaluation) > 0);
    }

    private static MuscleResolutionBalance ResolutionBalance(
        int minutes,
        int weakest,
        int strongest) =>
        new(
            minutes,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["weakest"] = weakest,
                ["strongest"] = strongest,
            },
            weakest,
            strongest);

    private static Exercise CreateExercise(
        int id,
        CanonicalMuscleGroup primary,
        int muscularDemand,
        params CanonicalMuscleGroup[] secondary)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primary,
            SecondaryCanonicalGroups = secondary,
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
            SequenceBlocks =
            [
                new ExerciseSequenceBlock
                {
                    ExerciseId = id,
                    MirrorMedia = false,
                },
            ],
            InsectCompatibility = ExerciseInsectCompatibility.Compatible,
            UpperBodyClothingRequirement =
                ExerciseUpperBodyClothingRequirement.Agnostic,
            ShyCompatibility = ExerciseShyCompatibility.Compatible,
            MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
            MuscularDemand = muscularDemand,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 2,
            Equipment = "None",
            Silent = true,
        };
    }
}
