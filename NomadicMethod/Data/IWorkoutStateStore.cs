using NomadicMethod.Models;

namespace NomadicMethod.Data;

public interface IWorkoutStateStore
{
    WorkoutState Load();

    /// <summary>
    /// Persists the state to durable storage before returning.
    /// </summary>
    void Save(WorkoutState state);

    /// <summary>
    /// Updates the stored state immediately and schedules its disk flush without
    /// blocking the caller.
    /// </summary>
    void SaveDeferred(WorkoutState state);
}
