using NomadicMethod.Models;

namespace NomadicMethod.Services;

public enum WorkoutBlockAccent
{
    Neutral,
    Blue,
    Red,
}

public readonly record struct WorkoutDisplayProgress(int Position, int Total);

public sealed record WorkoutExecutionTimeline(
    IReadOnlyList<WorkoutBlockAccent> Blocks,
    IReadOnlyList<int> SetStartBlockIndices,
    int CurrentBlockIndex);

public static class WorkoutDisplayPolicy
{
    public static WorkoutDisplayProgress GetProgress(
        IReadOnlyList<WorkoutGroup> activeGroups,
        WorkoutGroup currentGroup)
    {
        ArgumentNullException.ThrowIfNull(activeGroups);
        ArgumentNullException.ThrowIfNull(currentGroup);

        string[] selectionKeys = activeGroups
            .OrderBy(group => group.Order)
            .Select(group => group.SelectionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        int position = Array.FindIndex(
            selectionKeys,
            key => string.Equals(
                key,
                currentGroup.SelectionKey,
                StringComparison.Ordinal));
        if (position < 0 || selectionKeys.Length == 0)
        {
            throw new InvalidOperationException(
                "The current workout group is not in the active workout.");
        }

        return new WorkoutDisplayProgress(position + 1, selectionKeys.Length);
    }

    public static WorkoutExecutionTimeline GetTimeline(
        IReadOnlyList<WorkoutGroup> activeGroups,
        WorkoutGroup currentGroup,
        bool selectUpcomingBlock = false)
    {
        ArgumentNullException.ThrowIfNull(activeGroups);
        ArgumentNullException.ThrowIfNull(currentGroup);

        WorkoutGroup[] blocks = activeGroups
            .Where(group => string.Equals(
                group.SelectionKey,
                currentGroup.SelectionKey,
                StringComparison.Ordinal))
            .OrderBy(group => group.Order)
            .ToArray();
        int currentBlockIndex = Array.FindIndex(
            blocks,
            group => string.Equals(
                group.Id,
                currentGroup.Id,
                StringComparison.Ordinal));
        if (currentBlockIndex < 0 || blocks.Length == 0)
        {
            throw new InvalidOperationException(
                "The current workout group has no active execution timeline.");
        }
        if (selectUpcomingBlock && currentBlockIndex + 1 < blocks.Length)
        {
            currentBlockIndex++;
        }

        bool usesThreeDistinctExercisePalette =
            UsesThreeDistinctExercisePalette(blocks);
        return new WorkoutExecutionTimeline(
            blocks
                .Select(group => usesThreeDistinctExercisePalette
                    ? GetThreeDistinctExerciseAccent(group)
                    : GetAccent(group))
                .ToArray(),
            blocks
                .Select((group, index) => (group.SetNumber, Index: index))
                .Where(entry => entry.Index == 0 ||
                    entry.SetNumber != blocks[entry.Index - 1].SetNumber)
                .Select(entry => entry.Index)
                .ToArray(),
            currentBlockIndex);
    }

    private static bool UsesThreeDistinctExercisePalette(
        IReadOnlyList<WorkoutGroup> blocks) =>
        blocks.Count > 0 &&
        blocks.All(group =>
            group.SequenceBlockCount == 3 &&
            group.ExerciseOverrideId > 0 &&
            GetAccent(group) == WorkoutBlockAccent.Neutral) &&
        blocks
            .GroupBy(group => group.SetNumber)
            .All(set =>
                set.Count() == 3 &&
                set.Select(group => group.ExerciseOverrideId)
                    .Distinct()
                    .Count() == 3);

    private static WorkoutBlockAccent GetThreeDistinctExerciseAccent(
        WorkoutGroup group) =>
        group.SequenceBlockIndex switch
        {
            0 => WorkoutBlockAccent.Blue,
            1 => WorkoutBlockAccent.Neutral,
            2 => WorkoutBlockAccent.Red,
            _ => throw new ArgumentOutOfRangeException(
                nameof(group),
                "A three-exercise palette requires block indexes 0 through 2."),
        };

    public static WorkoutBlockAccent GetAccent(WorkoutGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return group.SequenceSideCue switch
        {
            ExerciseSequenceSideCue.ScreenRight or
                ExerciseSequenceSideCue.ShownLeadStance =>
                WorkoutBlockAccent.Blue,
            ExerciseSequenceSideCue.ScreenLeft or
                ExerciseSequenceSideCue.OppositeLeadStance =>
                WorkoutBlockAccent.Red,
            _ => group.SequenceDirectionCue switch
            {
                ExerciseSequenceDirectionCue.Forward or
                    ExerciseSequenceDirectionCue.Clockwise or
                    ExerciseSequenceDirectionCue.Inward =>
                    WorkoutBlockAccent.Blue,
                ExerciseSequenceDirectionCue.Backward or
                    ExerciseSequenceDirectionCue.Counterclockwise or
                    ExerciseSequenceDirectionCue.Outward =>
                    WorkoutBlockAccent.Red,
                _ => WorkoutBlockAccent.Neutral,
            },
        };
    }
}
