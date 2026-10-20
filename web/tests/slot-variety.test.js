import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  getMinimumExercisesPerModifierPairStatePerGroup,
  SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES,
  MINIMUM_EXERCISES_PER_SHORT_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_LEGACY_MODIFIER_PAIR_STATE_PER_GROUP,
  NEW_WORKOUT_MINUTES,
  getWorkoutMinuteOptions,
  normalizeSetupMinutes,
  normalizeMinutes,
  WorkoutSession, createDefaultState, getWorkoutCatalog,
  SLOT_VARIETY_EXERCISE_IDS, SLOT_VARIETY_COVERAGE_EXCEPTION_GROUP_IDS, BODY_CIRCLE_EXERCISE_ID,
  RESOLUTIONS, isSelectionGroupAvailable,
} from "../workout.js";

test("slot variety retains the five-choice floor for short durations", async () => {
  const cases = JSON.parse(await readFile(
    new URL("./fixtures/slot-variety-cases.json", import.meta.url), "utf8"));
  assert.deepEqual(cases.map(({ minutes }) => minutes), NEW_WORKOUT_MINUTES.filter(minutes => minutes <= 15));
  assert.equal(SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES, 15);
  assert.equal(MINIMUM_EXERCISES_PER_SHORT_MODIFIER_PAIR_STATE_PER_GROUP, 5);
  assert.equal(MINIMUM_EXERCISES_PER_LEGACY_MODIFIER_PAIR_STATE_PER_GROUP, 1);
  for (const { minutes, required } of cases) {
    assert.equal(getMinimumExercisesPerModifierPairStatePerGroup(minutes), required,
      `${minutes}-minute workout`);
  }
});

const catalog = JSON.parse(await readFile(new URL(
  "../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const oldSessions = JSON.parse(await readFile(new URL(
  "./fixtures/legacy-slot-variety-sessions.json", import.meta.url), "utf8"));
for (const phase of ["Ready", "Movement", "Rest"]) test(`revision 81 preserves every original slot and ${phase} checkpoint`, () => {
  let now = Date.UTC(2026, 9, 19, 6);
  for (const fixture of oldSessions) {
    let session = new WorkoutSession(catalog, structuredClone(fixture.state), () => 0.8, () => now);
    const next = session.getNextGroup();
    if (phase === "Movement") session.beginMovement(next, 28_000, now + 28_000);
    if (phase === "Rest") session.beginRest(next, now + 14_000);
    const original = structuredClone(session.state);
    for (let reopen = 0; reopen < 2; reopen++) {
      now += 10 * 86_400_000;
      session = new WorkoutSession(catalog, structuredClone(session.state), () => 0.2, () => now);
      session.restoreAfterReopen();
      assert.deepEqual(session.getActiveGroups(), fixture.rounds);
      assert.deepEqual(session.getActiveGroups().map(g => session.getSelectedExercise(g).id), fixture.selectedIds);
      assert.equal(session.getNextGroup().id, next.id);
      for (const key of ["activeCatalogRevision", "activeWorkoutSession", "workoutHistory", "exerciseScoreAdjustmentsByPhase", "keptExerciseRootIdsBySelectionGroupId", "activeWorkoutIsLightDay"]) {
        assert.deepEqual(session.state[key], original[key], key);
      }
      if (phase === "Movement") {
        assert.equal(session.state.pendingMovementPausedByUser, true);
        assert.equal(session.state.pendingMovementMillisecondsRemaining, 28_000);
      }
      if (phase === "Rest") assert.equal(session.state.pendingRestPausedByUser, true);
    }
  }
});

test("new choices, hip anatomy and exact exceptions do not alter frozen catalogs", () => {
  const old = getWorkoutCatalog(catalog, 81);
  assert.ok(old.every(e => !SLOT_VARIETY_EXERCISE_IDS.includes(e.id)));
  assert.deepEqual(old.find(e => e.id === 906).secondaryCanonicalGroups, ["HipFlexors"]);
  assert.ok(catalog.find(e => e.id === 906).secondaryCanonicalGroups.includes("HipAbductors"));
  assert.equal(getWorkoutCatalog(catalog, 82).length, catalog.filter(e => !e.elevationRequired && e.id !== BODY_CIRCLE_EXERCISE_ID).length);
  assert.equal(getWorkoutCatalog(catalog, 82).some(e => e.id === BODY_CIRCLE_EXERCISE_ID), false);
  for (const id of SLOT_VARIETY_COVERAGE_EXCEPTION_GROUP_IDS) {
    const group = [...RESOLUTIONS.values()].flatMap(r => r.groups).find(g => g.id === id);
    assert.equal(isSelectionGroupAvailable(group, 513, 81), true);
    assert.equal(isSelectionGroupAvailable(group, 513, 82), false);
    assert.equal(isSelectionGroupAvailable(group, 0), true);
  }
});

test("new workouts offer every duration and active workouts only shorten", () => {
  assert.deepEqual(getWorkoutMinuteOptions(), [3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
  for (const minutes of NEW_WORKOUT_MINUTES) {
    assert.equal(normalizeSetupMinutes(minutes), minutes);
    assert.equal(Math.max(...getWorkoutMinuteOptions(minutes)), minutes);
  }
  assert.equal(normalizeSetupMinutes(90), 90);
  assert.equal(normalizeMinutes(90), 90);
  assert.deepEqual(getWorkoutMinuteOptions(30), [3, 5, 7, 10, 15, 20, 30]);
  assert.equal(normalizeSetupMinutes(90, 30), 30);
  assert.equal(normalizeSetupMinutes(30, 30), 30);
});
