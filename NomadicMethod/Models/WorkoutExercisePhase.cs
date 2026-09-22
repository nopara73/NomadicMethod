namespace NomadicMethod.Models;

public enum WorkoutExercisePhase
{
    Unknown,
    Warmup,
    PeakPerformance,
    Fatigued,
}

public static class WorkoutExercisePhasePolicy
{
    public const int WarmupFinalBlock = 15;
    public const int PeakPerformanceFinalBlock = 45;

    public static WorkoutExercisePhase FromOneBasedBlockOrder(int blockOrder)
    {
        if (blockOrder <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockOrder));
        }

        if (blockOrder <= WarmupFinalBlock)
        {
            return WorkoutExercisePhase.Warmup;
        }

        return blockOrder <= PeakPerformanceFinalBlock
            ? WorkoutExercisePhase.PeakPerformance
            : WorkoutExercisePhase.Fatigued;
    }
}
