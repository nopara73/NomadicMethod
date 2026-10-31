import { WorkoutSession } from "./workout.js";
import { parseFitnessRatings } from "./fitness-profile.js";

self.addEventListener("message", (event) => {
  const {
    generation,
    exercises,
    fitnessRatings: fitnessRatingSource,
    state,
    minutes,
    modifiers,
    mode,
    currentWorkoutGroupId,
  } = event.data ?? {};
  try {
    const fitnessRatings = parseFitnessRatings(fitnessRatingSource);
    const session = WorkoutSession.createForApplication(exercises, state, undefined, undefined, fitnessRatings);
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
