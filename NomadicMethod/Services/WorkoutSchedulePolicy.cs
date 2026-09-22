using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutSchedulePolicy
{
    public static int GetMuscularDemandPriority(int muscularDemand) =>
        muscularDemand switch
        {
            Exercise.MinimumMuscularDemand => 0,
            Exercise.MaximumMuscularDemand => 1,
            Exercise.ModerateMuscularDemand => 2,
            _ => throw new ArgumentOutOfRangeException(
                nameof(muscularDemand),
                muscularDemand,
                "Muscular demand must be 0, 1, or 2."),
        };

    public static int GetSequenceMuscularDemand(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);

        if (root.SequenceBlocks.Length == 0)
        {
            throw new ArgumentException(
                "A scheduled exercise sequence must contain at least one block.",
                nameof(root));
        }

        return root.SequenceBlocks
            .Select(block => exercisesById.TryGetValue(
                    block.ExerciseId,
                    out Exercise? exercise)
                ? exercise.MuscularDemand
                : throw new ArgumentException(
                    $"Sequence block exercise {block.ExerciseId} is missing.",
                    nameof(exercisesById)))
            .Max();
    }
}
