using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ReactionCuePolicyTests
{
    [Theory]
    [InlineData(1, 5500, 8000, 1600, 2200)]
    [InlineData(2, 4000, 6500, 1200, 1800)]
    [InlineData(3, 3000, 5000, 900, 1400)]
    [InlineData(4, 2000, 3500, 700, 1100)]
    [InlineData(5, 1200, 2500, 450, 850)]
    public void EachLevelHasFixedMoveAndFreezeRanges(
        int level,
        int minimumMove,
        int maximumMove,
        int minimumFreeze,
        int maximumFreeze)
    {
        ReactionCueTiming timing = ReactionCuePolicy.GetTiming(level);
        Assert.Equal(minimumMove, timing.MinimumMoveMilliseconds);
        Assert.Equal(maximumMove, timing.MaximumMoveMilliseconds);
        Assert.Equal(minimumFreeze, timing.MinimumFreezeMilliseconds);
        Assert.Equal(maximumFreeze, timing.MaximumFreezeMilliseconds);
        Assert.Equal(
            minimumMove,
            ReactionCuePolicy.GetNextDurationMilliseconds(level, moving: true, 0));
        Assert.Equal(
            maximumMove,
            ReactionCuePolicy.GetNextDurationMilliseconds(
                level,
                moving: true,
                Math.BitDecrement(1d)));
        Assert.Equal(
            minimumFreeze,
            ReactionCuePolicy.GetNextDurationMilliseconds(level, moving: false, 0));
        Assert.Equal(
            maximumFreeze,
            ReactionCuePolicy.GetNextDurationMilliseconds(
                level,
                moving: false,
                Math.BitDecrement(1d)));
    }

    [Fact]
    public void CueProtocolDoesNotChangeLegacyActiveWorkouts()
    {
        Exercise exercise = ExerciseWithReactionCueLevel(5);
        Assert.False(ReactionCuePolicy.IsEnabled(
            exercise,
            ReactionCuePolicy.IntroducedCatalogRevision - 1));
        Assert.True(ReactionCuePolicy.IsEnabled(
            exercise,
            ReactionCuePolicy.IntroducedCatalogRevision));
        Assert.False(ReactionCuePolicy.IsEnabled(
            ExerciseWithReactionCueLevel(0),
            ReactionCuePolicy.IntroducedCatalogRevision));
    }

    private static Exercise ExerciseWithReactionCueLevel(int level) => new()
    {
        Id = 1,
        Name = "Reaction cue test",
        Video = "exercise_videos/exercise_0001.mp4",
        PrimaryCanonicalGroup = CanonicalMuscleGroup.Chest,
        SecondaryCanonicalGroups = [],
        Practice = "Test",
        MotionProfile = "Test",
        Mode = ExerciseMode.Repetition,
        Presentation = ExercisePresentation.Motion,
        HoldFramePercent = 0,
        SideSequence = ExerciseSideSequence.Continuous,
        ReactionCueLevel = level,
        OnlyFeetTouchGround = true,
        ShoeAgnostic = true,
        MaxSpaceMeters = 2,
        Equipment = "None",
        Silent = true,
    };
}
