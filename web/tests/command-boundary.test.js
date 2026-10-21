import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { WorkoutSession, createDefaultState, canUseSingleDirection, WORKOUT_MODIFIERS as M } from "../workout.js";

const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const now = Date.UTC(2026, 9, 21);
function worker(state) {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0.4, () => now);
  session.state = structuredClone(state);
  return session;
}
function checkpoint(phase) {
  const session = new WorkoutSession(catalog, { ...createDefaultState(), scores: Object.fromEntries(
    catalog.filter(canUseSingleDirection).map(e => [e.id, 10000])) }, () => 0.4, () => now);
  session.initialize();
  session.startWorkout(10, M.None);
  const first = session.getNextGroup();
  session.beginRest(first, now + 15000);
  if (session.isIntermediateSequenceBlock(first)) session.advanceSequence(first);
  else session.recordOutcome(first, true);
  session.clearPendingRest();
  const current = session.getNextGroup();
  if (phase === "movement") {
    session.beginMovement(current, 27000, now + 27000);
    session.pauseMovement(current, 27000, true);
  }
  if (phase === "rest") session.beginRest(current, now + 15000);
  return session;
}
for (const phase of ["ready", "movement", "rest"]) {
  test(`independent End archives only recorded ${phase} work`, () => {
    const original = checkpoint(phase);
    const session = worker(original.state);
    const blocks = structuredClone(original.state.activeWorkoutSession.blocks);
    const feedback = structuredClone(original.state.exerciseScoreAdjustmentsByPhase);
    session.endActiveWorkout();
    assert.equal(session.state.activeWorkoutMinutes, 0);
    assert.equal(session.state.activeWorkoutSession, null);
    assert.deepEqual(session.state.workoutHistory[0].blocks, blocks);
    assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, feedback);
    assert.equal(session.state.workoutHistory[0].status, "Interrupted");
    const saved = structuredClone(session.state);
    session.endActiveWorkout();
    assert.deepEqual(session.state, saved);
  });
  for (const minutes of [5, 15, 30]) test(`independent ${phase} settings apply ${minutes} minutes without changing recorded work`, () => {
    const original = checkpoint(phase);
    const before = original.getActiveGroups();
    const current = original.getNextGroup();
    const session = worker(original.state);
    assert.deepEqual(session.getActiveGroups(), before);
    session.reconfigureActiveWorkout(session.state.activeWorkoutModifiers, current.id);
    session.resizeActiveWorkout(minutes);
    assert.equal(session.getActiveGroups().length, minutes);
    assert.deepEqual(session.getActiveGroups().slice(0, current.order), before.slice(0, current.order));
    assert.deepEqual(session.state.activeWorkoutSession.blocks, original.state.activeWorkoutSession.blocks);
    assert.equal(session.state.activeWorkoutSession.durationChanges.length, 1);
    const restored = worker(session.state);
    restored.restoreAfterReopen();
    assert.deepEqual(restored.getActiveGroups(), session.getActiveGroups());
  });
}
test("Stop remains available when unfinished selections are missing", () => {
  const session = checkpoint("ready");
  const blocks = structuredClone(session.state.activeWorkoutSession.blocks);
  session.state.selectedExerciseIds = {};
  session.state.activeSetCountsBySelectionGroupId = {};
  session.endActiveWorkout();
  assert.equal(session.state.activeWorkoutMinutes, 0);
  assert.deepEqual(session.state.workoutHistory[0].blocks, blocks);
});
