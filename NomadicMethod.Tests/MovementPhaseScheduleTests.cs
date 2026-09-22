using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class MovementPhaseScheduleTests
{
    [Theory]
    [InlineData(50_000, 5)]
    [InlineData(49_999, 5)]
    [InlineData(45_001, 1)]
    public void First_block_has_five_seconds_of_preparation(
        long remainingMilliseconds,
        int expectedSeconds)
    {
        MovementPhaseState state = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            includePreparation: true);

        Assert.Equal(MovementPhase.Preparation, state.Phase);
        Assert.Equal(expectedSeconds, state.SecondsRemaining);
        Assert.Equal(5, state.SegmentDurationSeconds);
        Assert.False(state.IsExercise);
    }

    [Fact]
    public void Sequence_continuation_starts_its_full_block_after_the_rest()
    {
        MovementPhaseState state = MovementPhaseSchedule.GetState(
            45_000,
            includePreparation: false);

        Assert.Equal(MovementPhase.Continuous, state.Phase);
        Assert.Equal(45, state.SecondsRemaining);
        Assert.Equal(45, state.SegmentDurationSeconds);
        Assert.True(state.IsExercise);
        Assert.Equal(50, MovementPhaseSchedule.GetCountdownDurationSeconds(true));
        Assert.Equal(45, MovementPhaseSchedule.GetCountdownDurationSeconds(false));
    }

    [Theory]
    [InlineData(45_000, 45)]
    [InlineData(44_000, 44)]
    [InlineData(1_001, 2)]
    [InlineData(1, 1)]
    public void Every_exercise_block_gets_forty_five_seconds(
        long remainingMilliseconds,
        int expectedSeconds)
    {
        MovementPhaseState state = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            includePreparation: true);

        Assert.Equal(MovementPhase.Continuous, state.Phase);
        Assert.Equal(expectedSeconds, state.SecondsRemaining);
        Assert.Equal(45, state.SegmentDurationSeconds);
        Assert.True(state.IsExercise);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Finished_countdowns_are_complete(long remainingMilliseconds)
    {
        MovementPhaseState state = MovementPhaseSchedule.GetState(
            remainingMilliseconds,
            includePreparation: true);

        Assert.Equal(MovementPhase.Complete, state.Phase);
        Assert.Equal(0, state.SecondsRemaining);
        Assert.Equal(0, state.SegmentDurationSeconds);
        Assert.False(state.IsExercise);
    }
}
