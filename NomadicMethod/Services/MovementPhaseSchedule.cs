namespace NomadicMethod.Services;

public enum MovementPhase
{
    Preparation,
    Continuous,
    Complete,
}

public readonly record struct MovementPhaseState(
    MovementPhase Phase,
    int SecondsRemaining,
    int SegmentDurationSeconds,
    bool IsExercise);

public static class MovementPhaseSchedule
{
    public const int PreparationDurationSeconds = 5;
    public const int TotalDurationSeconds = 45;

    public static int GetCountdownDurationSeconds(bool includePreparation) =>
        TotalDurationSeconds +
        (includePreparation ? PreparationDurationSeconds : 0);

    public static MovementPhaseState GetState(
        long remainingMilliseconds,
        bool includePreparation)
    {
        if (remainingMilliseconds <= 0)
        {
            return new MovementPhaseState(
                MovementPhase.Complete,
                SecondsRemaining: 0,
                SegmentDurationSeconds: 0,
                IsExercise: false);
        }

        long movementDurationMilliseconds = TotalDurationSeconds * 1_000L;
        long totalDurationMilliseconds = GetCountdownDurationSeconds(
            includePreparation) * 1_000L;
        long boundedRemainingMilliseconds = Math.Min(
            remainingMilliseconds,
            totalDurationMilliseconds);
        if (includePreparation &&
            boundedRemainingMilliseconds > movementDurationMilliseconds)
        {
            return new MovementPhaseState(
                MovementPhase.Preparation,
                ToDisplayedSeconds(
                    boundedRemainingMilliseconds - movementDurationMilliseconds),
                PreparationDurationSeconds,
                IsExercise: false);
        }

        return new MovementPhaseState(
            MovementPhase.Continuous,
            ToDisplayedSeconds(Math.Min(
                boundedRemainingMilliseconds,
                movementDurationMilliseconds)),
            TotalDurationSeconds,
            IsExercise: true);
    }

    private static int ToDisplayedSeconds(long remainingMilliseconds) =>
        checked((int)((remainingMilliseconds + 999L) / 1_000L));
}
