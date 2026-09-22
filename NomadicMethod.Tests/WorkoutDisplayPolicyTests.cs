using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutDisplayPolicyTests
{
    private static readonly IReadOnlySet<CanonicalMuscleGroup> Chest =
        new HashSet<CanonicalMuscleGroup> { CanonicalMuscleGroup.Chest };

    [Fact]
    public void Progress_counts_each_logical_selection_once()
    {
        WorkoutGroup[] groups =
        [
            Group("punch.set1.block1", 1, "punch", 0, 2, 1, 2),
            Group("punch.set1.block2", 2, "punch", 1, 2, 1, 2),
            Group("punch.set2.block1", 3, "punch", 0, 2, 2, 2),
            Group("punch.set2.block2", 4, "punch", 1, 2, 2, 2),
            Group("squat", 5, null, 0, 1, 1, 1),
        ];

        WorkoutDisplayProgress first = WorkoutDisplayPolicy.GetProgress(
            groups,
            groups[0]);
        WorkoutDisplayProgress fourth = WorkoutDisplayPolicy.GetProgress(
            groups,
            groups[3]);
        WorkoutDisplayProgress fifth = WorkoutDisplayPolicy.GetProgress(
            groups,
            groups[4]);

        Assert.Equal(new WorkoutDisplayProgress(1, 2), first);
        Assert.Equal(first, fourth);
        Assert.Equal(new WorkoutDisplayProgress(2, 2), fifth);
    }

    [Fact]
    public void Timeline_contains_only_real_work_blocks_in_execution_order()
    {
        WorkoutGroup[] groups =
        [
            Group(
                "punch.set1.block1",
                1,
                "punch",
                0,
                2,
                1,
                2,
                ExerciseSequenceSideCue.ScreenRight),
            Group(
                "punch.set1.block2",
                2,
                "punch",
                1,
                2,
                1,
                2,
                ExerciseSequenceSideCue.ScreenLeft),
            Group(
                "punch.set2.block1",
                3,
                "punch",
                0,
                2,
                2,
                2,
                ExerciseSequenceSideCue.ScreenRight),
            Group(
                "punch.set2.block2",
                4,
                "punch",
                1,
                2,
                2,
                2,
                ExerciseSequenceSideCue.ScreenLeft),
        ];

        WorkoutExecutionTimeline timeline = WorkoutDisplayPolicy.GetTimeline(
            groups,
            groups[2]);

        Assert.Equal(
            [
                WorkoutBlockAccent.Blue,
                WorkoutBlockAccent.Red,
                WorkoutBlockAccent.Blue,
                WorkoutBlockAccent.Red,
            ],
            timeline.Blocks);
        Assert.Equal([0, 2], timeline.SetStartBlockIndices);
        Assert.Equal(2, timeline.CurrentBlockIndex);
    }

    [Fact]
    public void Transition_moves_only_the_playhead_to_the_upcoming_block()
    {
        WorkoutGroup[] groups =
        [
            Group("pair.block1", 1, "pair", 0, 2, 1, 1),
            Group("pair.block2", 2, "pair", 1, 2, 1, 1),
        ];

        WorkoutExecutionTimeline playing = WorkoutDisplayPolicy.GetTimeline(
            groups,
            groups[0]);
        WorkoutExecutionTimeline transition = WorkoutDisplayPolicy.GetTimeline(
            groups,
            groups[0],
            selectUpcomingBlock: true);

        Assert.Equal(playing.Blocks, transition.Blocks);
        Assert.Equal([0], playing.SetStartBlockIndices);
        Assert.Equal(playing.SetStartBlockIndices, transition.SetStartBlockIndices);
        Assert.Equal(0, playing.CurrentBlockIndex);
        Assert.Equal(1, transition.CurrentBlockIndex);
    }

    [Fact]
    public void Three_distinct_uncued_exercises_use_three_phase_palette()
    {
        WorkoutGroup[] groups =
        [
            Group(
                "circuit.set1.block1",
                1,
                "circuit",
                0,
                3,
                1,
                2,
                exerciseOverrideId: 101),
            Group(
                "circuit.set1.block2",
                2,
                "circuit",
                1,
                3,
                1,
                2,
                exerciseOverrideId: 102),
            Group(
                "circuit.set1.block3",
                3,
                "circuit",
                2,
                3,
                1,
                2,
                exerciseOverrideId: 103),
            Group(
                "circuit.set2.block1",
                4,
                "circuit",
                0,
                3,
                2,
                2,
                exerciseOverrideId: 101),
            Group(
                "circuit.set2.block2",
                5,
                "circuit",
                1,
                3,
                2,
                2,
                exerciseOverrideId: 102),
            Group(
                "circuit.set2.block3",
                6,
                "circuit",
                2,
                3,
                2,
                2,
                exerciseOverrideId: 103),
        ];

        WorkoutExecutionTimeline timeline = WorkoutDisplayPolicy.GetTimeline(
            groups,
            groups[4]);

        Assert.Equal(
            [
                WorkoutBlockAccent.Blue,
                WorkoutBlockAccent.Neutral,
                WorkoutBlockAccent.Red,
                WorkoutBlockAccent.Blue,
                WorkoutBlockAccent.Neutral,
                WorkoutBlockAccent.Red,
            ],
            timeline.Blocks);
        Assert.Equal([0, 3], timeline.SetStartBlockIndices);
        Assert.Equal(4, timeline.CurrentBlockIndex);
    }

    [Fact]
    public void Three_distinct_exercises_do_not_override_real_cues()
    {
        WorkoutGroup[] groups =
        [
            Group("circuit.block1", 1, "circuit", 0, 3, 1, 1,
                ExerciseSequenceSideCue.ScreenRight, exerciseOverrideId: 101),
            Group("circuit.block2", 2, "circuit", 1, 3, 1, 1,
                ExerciseSequenceSideCue.ScreenLeft, exerciseOverrideId: 102),
            Group("circuit.block3", 3, "circuit", 2, 3, 1, 1,
                directionCue: ExerciseSequenceDirectionCue.Forward,
                exerciseOverrideId: 103),
        ];

        WorkoutExecutionTimeline timeline = WorkoutDisplayPolicy.GetTimeline(
            groups,
            groups[1]);

        Assert.Equal(
            [
                WorkoutBlockAccent.Blue,
                WorkoutBlockAccent.Red,
                WorkoutBlockAccent.Blue,
            ],
            timeline.Blocks);
    }

    [Theory]
    [InlineData(
        ExerciseSequenceSideCue.None,
        ExerciseSequenceDirectionCue.None,
        WorkoutBlockAccent.Neutral)]
    [InlineData(
        ExerciseSequenceSideCue.ScreenRight,
        ExerciseSequenceDirectionCue.None,
        WorkoutBlockAccent.Blue)]
    [InlineData(
        ExerciseSequenceSideCue.ScreenLeft,
        ExerciseSequenceDirectionCue.None,
        WorkoutBlockAccent.Red)]
    [InlineData(
        ExerciseSequenceSideCue.None,
        ExerciseSequenceDirectionCue.Clockwise,
        WorkoutBlockAccent.Blue)]
    [InlineData(
        ExerciseSequenceSideCue.None,
        ExerciseSequenceDirectionCue.Counterclockwise,
        WorkoutBlockAccent.Red)]
    public void Accent_comes_from_the_real_block_cues(
        ExerciseSequenceSideCue sideCue,
        ExerciseSequenceDirectionCue directionCue,
        WorkoutBlockAccent expected)
    {
        WorkoutGroup group = Group(
            "block",
            1,
            null,
            0,
            1,
            1,
            1,
            sideCue,
            directionCue);

        Assert.Equal(expected, WorkoutDisplayPolicy.GetAccent(group));
    }

    private static WorkoutGroup Group(
        string id,
        int order,
        string? selectionGroupId,
        int sequenceBlockIndex,
        int sequenceBlockCount,
        int setNumber,
        int setCount,
        ExerciseSequenceSideCue sideCue = ExerciseSequenceSideCue.None,
        ExerciseSequenceDirectionCue directionCue =
            ExerciseSequenceDirectionCue.None,
        int exerciseOverrideId = 0) =>
        new(
            id,
            id,
            order,
            Chest,
            SelectionGroupId: selectionGroupId,
            ExerciseOverrideId: exerciseOverrideId,
            SequenceBlockIndex: sequenceBlockIndex,
            SequenceBlockCount: sequenceBlockCount,
            SetNumber: setNumber,
            SetCount: setCount,
            SequenceSideCue: sideCue,
            SequenceDirectionCue: directionCue);
}
