using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutCoveragePolicyTests
{
    [Theory]
    [InlineData(3, "r3.lower-limbs", 1)]
    [InlineData(3, "r3.torso-pelvic-complex", 1)]
    [InlineData(5, "r5.hips-thighs", 1)]
    [InlineData(7, "r7.lower-legs-feet", 1)]
    [InlineData(20, "r20.back-spinal-stabilization", 1)]
    [InlineData(30, "r30.medial-deep-knee-extensors", 1)]
    public void EachGroupOffersItsGenuinelyTrainedMusclesAsAlternatives(
        int minutes,
        string groupId,
        int expected)
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(minutes, groupId);

        Assert.Equal(
            expected,
            WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group));
    }

    [Fact]
    public void SelectabilityRequiresMeaningfulCoverageAndTracksPrimaryOwnershipSeparately()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        CanonicalMuscleGroup[] leaves = group.CanonicalGroups.ToArray();
        Exercise isolatedPrimary = Exercise(1, leaves[0], []);
        Exercise unrelated = Exercise(2, CanonicalMuscleGroup.SpinalExtensors, []);
        Exercise secondaryOnly = Exercise(
            3,
            CanonicalMuscleGroup.SpinalExtensors,
            [leaves[0]]);

        Assert.Equal(1, WorkoutCoveragePolicy.GetCanonicalCoverage(isolatedPrimary, group));
        Assert.True(WorkoutCoveragePolicy.IsSelectable(isolatedPrimary, group));
        Assert.False(WorkoutCoveragePolicy.IsSelectable(unrelated, group));
        Assert.Equal(1, WorkoutCoveragePolicy.GetCanonicalCoverage(secondaryOnly, group));
        Assert.True(WorkoutCoveragePolicy.IsSelectable(secondaryOnly, group));
        Assert.True(WorkoutCoveragePolicy.IsPrimaryForGroup(isolatedPrimary, group));
        Assert.False(WorkoutCoveragePolicy.IsPrimaryForGroup(secondaryOnly, group));
    }

    [Fact]
    public void SequenceKeepsItsRealTargetsAndRequiresTimeForEveryMember()
    {
        WorkoutGroup upper = MassGroupingTaxonomy.GetGroup(3, "r3.head-neck-upper-limbs");
        Exercise compound = Exercise(10, CanonicalMuscleGroup.ShoulderAbductors,
            [CanonicalMuscleGroup.ElbowExtensors], [10, 11]);
        Exercise wrist = Exercise(11, CanonicalMuscleGroup.ForearmExtensorsAndSupinators, []);
        var catalog = new[] { compound, wrist }.ToDictionary(exercise => exercise.Id);

        Assert.True(WorkoutCoveragePolicy.IsSelectable(compound, upper));
        Assert.True(WorkoutSequencePolicy.IsSelectable(compound, catalog, upper));
        Assert.Single(WorkoutSequencePolicy.GetPlacementOptions(compound, catalog, [upper]));
        Assert.False(WorkoutSequencePolicy.IsSelectable(compound, catalog,
            MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs")));
        Assert.Equal(2, compound.SequenceBlocks.Length);
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        CanonicalMuscleGroup[] secondary,
        int[]? sequenceIds = null)
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
            SequenceBlocks = (sequenceIds ?? [id]).Select(memberId =>
                new ExerciseSequenceBlock { ExerciseId = memberId, MirrorMedia = false }).ToArray(),
            UpperBodyClothingRequirement =
                ExerciseUpperBodyClothingRequirement.Agnostic,
            ShyCompatibility = ExerciseShyCompatibility.Compatible,
            Score = 0,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }
}
