namespace NomadicMethod.Models;

public sealed record RecordedWorkoutOutcome(
    Exercise Exercise,
    IReadOnlyList<Exercise> ScoreUpdates);

public sealed record ShuffledExerciseResult(
    Exercise RejectedExercise,
    Exercise ReplacementExercise,
    IReadOnlyList<Exercise> ScoreUpdates);
