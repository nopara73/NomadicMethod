import { WorkoutSession } from "./workout.js";

self.addEventListener("message", (event) => {
  const {
    generation,
    exercises,
    fitnessRatings,
    state,
    minutes,
    modifiers,
    mode,
    currentWorkoutGroupId,
  } = event.data ?? {};
  try {
    const session = new WorkoutSession(exercises, state, undefined, undefined, fitnessRatings);
    if (mode === "reconfigure") {
      session.reconfigureActiveWorkout(modifiers, currentWorkoutGroupId);
      session.resizeActiveWorkout(minutes);
    } else {
      session.prepareWorkout(minutes, modifiers);
    }
    self.postMessage({ generation, state: session.state });
  } catch (error) {
    self.postMessage({
      generation,
      error: error instanceof Error ? error.message : String(error),
    });
  }
});
