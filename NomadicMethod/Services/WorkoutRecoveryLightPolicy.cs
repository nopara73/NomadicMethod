using NomadicMethod.Models;

namespace NomadicMethod.Services;

public readonly record struct WorkoutRecoveryLightStatus(
    int RecoveringMuscleCount,
    int EligibleMuscleCount)
{
    public bool IsActive =>
        EligibleMuscleCount > 0 &&
        RecoveringMuscleCount *
            WorkoutRecoveryLightPolicy.MinimumRecoveryShareDenominator >=
        EligibleMuscleCount *
            WorkoutRecoveryLightPolicy.MinimumRecoveryShareNumerator;
}

public static class WorkoutRecoveryLightPolicy
{
    public const int MinimumRecoveryShareNumerator = 4;

    public const int MinimumRecoveryShareDenominator = 5;

    public static WorkoutRecoveryLightStatus Evaluate(
        IEnumerable<Exercise> selectableExercises,
        IReadOnlyDictionary<string, long>
            lastMeaningfulWorkByPrimaryMuscle,
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        long nowUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(selectableExercises);
        ArgumentNullException.ThrowIfNull(
            lastMeaningfulWorkByPrimaryMuscle);
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);

        var availableDemandByMuscle = selectableExercises
            .Where(exercise =>
                exercise.MuscularDemand is
                    Exercise.ModerateMuscularDemand or
                    Exercise.MaximumMuscularDemand)
            .GroupBy(exercise => exercise.PrimaryCanonicalGroup)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(exercise => exercise.MuscularDemand)
                    .ToHashSet());

        int recoveringMuscleCount = availableDemandByMuscle.Count(entry =>
            entry.Value.All(demand => demand switch
            {
                Exercise.ModerateMuscularDemand =>
                    WorkoutRecoveryPolicy
                        .IsPrimaryMuscleInModerateRecovery(
                            lastMeaningfulWorkByPrimaryMuscle,
                            entry.Key,
                            nowUnixMilliseconds),
                Exercise.MaximumMuscularDemand =>
                    WorkoutRecoveryPolicy.IsPrimaryMuscleRecovering(
                        lastHardWorkByPrimaryMuscle,
                        entry.Key,
                        nowUnixMilliseconds),
                _ => false,
            }));

        return new WorkoutRecoveryLightStatus(
            recoveringMuscleCount,
            availableDemandByMuscle.Count);
    }
}
