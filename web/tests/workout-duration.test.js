import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { checkDurationPolicy } from "../scripts/workout-duration-policy.mjs";
import { SUPPORTED_MINUTES, normalizeMinutes, WorkoutSession, createDefaultState, RESOLUTIONS } from "../workout.js";

test("Android and both web startup paths consume one duration contract", async () => {
  await checkDurationPolicy();
  const contract = JSON.parse(await readFile(new URL(
    "../../NomadicMethod/Assets/workout_durations.json", import.meta.url), "utf8"));
  assert.deepEqual(SUPPORTED_MINUTES, [3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
  assert.deepEqual(SUPPORTED_MINUTES, contract.minutes);
  assert.strictEqual(SUPPORTED_MINUTES, globalThis.nomadicMethodDuration.minutes);
  assert.equal(normalizeMinutes(NaN), contract.defaultMinutes);
  assert.equal(normalizeMinutes(25), 30);
  assert.equal(normalizeMinutes(-1), 3);
  assert.equal(normalizeMinutes(100), 90);
  assert.equal(Math.max(...RESOLUTIONS.keys()), 30);
});

const catalog = JSON.parse(await readFile(new URL(
  "../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
for (const minutes of [3, 5, 7, 10, 15, 20, 30, 45, 60, 90]) {
  test(`${minutes} minutes can be selected, started and restored without changing the plan`, () => {
    const state = createDefaultState();
    state.lastWorkoutMinutes = minutes;
    let session = new WorkoutSession(catalog, state, () => 0.4);
    session.initialize();
    const selected = normalizeMinutes(session.state.lastWorkoutMinutes);
    assert.equal(selected, minutes);
    session.startWorkout(selected);
    const plan = structuredClone(session.getActiveGroups());
    const history = structuredClone(session.state.activeWorkoutSession);
    assert.equal(plan.length, minutes);
    session = new WorkoutSession(catalog, JSON.parse(JSON.stringify(session.state)), () => 0.6);
    session.restoreAfterReopen();
    assert.equal(session.state.activeWorkoutMinutes, minutes);
    assert.equal(session.state.lastWorkoutMinutes, minutes);
    assert.deepEqual(session.getActiveGroups(), plan);
    assert.deepEqual(session.state.activeWorkoutSession, history);
  });
}
