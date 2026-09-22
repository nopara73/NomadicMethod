using NomadicMethod.Models;

namespace NomadicMethod.Data;

public interface IExerciseDatabase : IDisposable
{
    IReadOnlyList<Exercise> Exercises { get; }

    void UpdateScore(Exercise exercise);
}
