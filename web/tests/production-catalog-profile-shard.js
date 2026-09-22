import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  WORKOUT_MODIFIER_VALIDATION_PROFILES,
  SUPPORTED_MINUTES,
  WorkoutSession,
  createDefaultState,
  RESOLUTIONS,
  getSelectionKey,
  getSessionMovementId,
  getMaximumDistinctLineupSize,
  isSelectionGroupAvailable,
  isSelectableForWorkoutProfile,
} from "../workout.js";

const catalog = JSON.parse(readFileSync(
  new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url),
  "utf8",
));

export function registerProductionCatalogProfileShard(shardIndex, shardCount) {
  test(`production catalog workout profiles ${shardIndex + 1}/${shardCount}`, () => {
    const profiles = WORKOUT_MODIFIER_VALIDATION_PROFILES.filter(
      (_, profileIndex) => profileIndex % shardCount === shardIndex,
    );
    assert.ok(profiles.length > 0);
    for (const profile of profiles) {
      for (const minutes of SUPPORTED_MINUTES) {
        const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
        session.startWorkout(minutes, profile);
        const rounds = session.getActiveGroups();
        assert.equal(rounds.length, minutes);
        const selections = [...new Map(rounds.map((round) =>
          [getSelectionKey(round), session.getSelectedExercise(round)])).values()];
        if (new Set(selections.map(getSessionMovementId)).size < selections.length) {
          const availableGroups = RESOLUTIONS.get(Math.min(minutes, 30)).groups
            .filter((group) => isSelectionGroupAvailable(group, profile));
          assert.ok(getMaximumDistinctLineupSize(catalog, availableGroups, profile, minutes) < availableGroups.length,
            `Repeated a movement despite a complete distinct lineup: ${minutes} minutes, ${profile}`);
        }
        assert.ok(session.getActiveGroups().every((group) => {
          const selected = session.getSelectedExercise(group);
          return isSelectableForWorkoutProfile(
            session.getSequenceSelectionExerciseForGroup(selected, group),
            group,
            profile,
          );
        }));
      }
    }
  });
}
