import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M } from "../workout.js";

const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
function finish(session) {
  const group = session.getNextGroup();
  session.beginRest(group, Date.now() + 15_000);
  if (session.isIntermediateSequenceBlock(group)) session.advanceSequence(group);
  else session.recordOutcome(group, true);
  session.clearPendingRest();
}

for (const phase of ["ready", "movement", "rest"]) {
  test(`cold reopen after ten years preserves ${phase} progress`, () => {
    let now = Date.now();
    let session = new WorkoutSession(catalog, createDefaultState(), () => 0, () => now);
    session.initialize();
    session.startWorkout(10);
    finish(session);
    const next = session.getNextGroup();
    if (phase === "movement") session.beginMovement(next, 31_000, now + 31_000);
    if (phase === "rest") session.beginRest(next, now + 15_000);
    const saved = JSON.parse(JSON.stringify(session.state));
    now += 3653 * 24 * 60 * 60 * 1000;
    session = new WorkoutSession(catalog, saved, () => 0, () => now);
    session.restoreAfterReopen();
    assert.equal(session.getNextGroup().id, next.id);
    assert.equal(session.state.activeWorkoutSession.sessionId, saved.activeWorkoutSession.sessionId);
    assert.deepEqual(session.state.outcomes, saved.outcomes);
    assert.deepEqual(session.state.workoutHistory, saved.workoutHistory);
    if (phase === "movement") assert.equal(session.getPendingMovementMillisecondsRemaining(now), 31_000);
    if (phase === "rest") assert.equal(session.state.pendingRestPausedByUser, true);
  });
}

for (const [from, to] of [[3, 60], [10, 5], [30, 45], [60, 30], [60, 90], [90, 15]]) {
  test(`resize ${from} to ${to}, keep the prefix, reload and finish`, () => {
    let session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.initialize();
    session.startWorkout(from);
    finish(session);
    const current = session.getNextGroup();
    const prefix = session.getActiveGroups().slice(0, current.order);
    session.resizeActiveWorkout(to);
    assert.equal(session.getActiveGroups().length, to);
    assert.deepEqual(session.getActiveGroups().slice(0, current.order), prefix);
    assert.equal(session.getNextGroup().id, current.id);
    assert.equal(session.state.activeWorkoutSession.durationChanges.length, 1);
    session = new WorkoutSession(catalog, JSON.parse(JSON.stringify(session.state)), () => 0);
    session.restoreAfterReopen();
    assert.equal(session.getNextGroup().id, current.id);
    assert.equal(session.getActiveGroups().length, to);
    while (!session.state.workoutCompleted) finish(session);
    assert.equal(session.state.workoutHistory.at(-1).status, "Completed");
  });
}

for (const phase of ["ready", "movement", "rest"]) test(`explicit end from ${phase} preserves actual work and does not start another session`, () => {
  const session = new WorkoutSession(catalog);
  session.initialize();
  session.startWorkout(10);
  finish(session);
  if (phase === "rest") session.beginRest(session.getNextGroup(), Date.now() + 15_000);
  if (phase === "movement") session.beginMovement(session.getNextGroup(), 25_000, Date.now() + 25_000);
  const votes = JSON.stringify(session.state.exerciseScoreAdjustmentsByPhase);
  const oldId = session.state.activeWorkoutSession.sessionId;
  const blocks = structuredClone(session.state.activeWorkoutSession.blocks);
  session.endActiveWorkout();
  assert.equal(JSON.stringify(session.state.exerciseScoreAdjustmentsByPhase), votes);
  assert.equal(session.state.workoutHistory.at(-1).status, "Interrupted");
  assert.equal(session.state.workoutHistory.at(-1).sessionId, oldId);
  assert.deepEqual(session.state.workoutHistory.at(-1).blocks, blocks);
  assert.equal(session.state.activeWorkoutMinutes, 0);
  assert.equal(session.state.activeWorkoutSession, null);
  assert.equal(session.state.pendingRestGroupId, null);
  assert.equal(session.state.pendingMovementGroupId, null);
  const ended = JSON.stringify(session.state);
  session.endActiveWorkout();
  assert.equal(JSON.stringify(session.state), ended);
  const restored = new WorkoutSession(catalog, JSON.parse(ended));
  restored.restoreAfterReopen();
  assert.equal(restored.state.activeWorkoutMinutes, 0);
  assert.equal(restored.state.workoutHistory.length, 1);
});

for (const profile of [M.None, M.Insect | M.Silence | M.UpperBodyClothing,
  M.HardFloor | M.Silence | M.UpperBodyClothing,
  M.Wall | M.UpperBodyClothing]) {
  test(`duration and modifier edits preserve paused work with profile ${profile}`, () => {
    let session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.initialize();
    session.startWorkout(60, profile);
    for (let i = 0; i < 10; i++) finish(session);
    const current = session.getNextGroup();
    session.beginMovement(current, 25_000, Date.now() + 25_000);
    session.pauseMovement(current, 25_000, true);
    let outcomes = JSON.stringify(session.state.outcomes);
    const completedWork = JSON.stringify(session.state.activeWorkoutSession.blocks);
    session.resizeActiveWorkout(30);
    assert.equal(session.state.pendingMovementGroupId, current.id);
    assert.equal(session.state.pendingMovementMillisecondsRemaining, 25_000);
    assert.equal(JSON.stringify(session.state.outcomes), outcomes);
    session.reconfigureActiveWorkout(profile ^ M.Wall, current.id);
    assert.equal(JSON.stringify(session.state.activeWorkoutSession.blocks), completedWork);
    outcomes = JSON.stringify(session.state.outcomes);
    session.resizeActiveWorkout(45);
    session = new WorkoutSession(catalog, JSON.parse(JSON.stringify(session.state)), () => 0);
    session.restoreAfterReopen();
    assert.equal(session.getActiveGroups().length, 45);
    assert.equal(JSON.stringify(session.state.outcomes), outcomes);
  });
}

for (const minutes of [60, 90]) {
  test(`extend from the last fine slot to ${minutes} and reject destructive shortening`, () => {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.initialize();
    session.startWorkout(30);
    while (session.getNextGroup().order < 30) finish(session);
    const outcomes = JSON.stringify(session.state.outcomes);
    session.resizeActiveWorkout(minutes);
    assert.equal(session.getActiveGroups().length, minutes);
    assert.equal(JSON.stringify(session.state.outcomes), outcomes);
    assert.equal(session.getNextGroup().order, 30);
    const before = JSON.stringify(session.state);
    assert.throws(() => session.resizeActiveWorkout(3));
    assert.equal(JSON.stringify(session.state), before);
  });
}

test("an unsupported duration plan leaves the entire active state untouched", () => {
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  const profile = M.Insect | M.HardFloor | M.Silence | M.UpperBodyClothing;
  session.initialize();
  session.startWorkout(60, profile);
  for (let i = 0; i < 10; i++) finish(session);
  session.resizeActiveWorkout(30);
  session.reconfigureActiveWorkout(profile | M.Wall, session.getNextGroup().id);
  const before = JSON.stringify(session.state);
  assert.throws(() => session.resizeActiveWorkout(45));
  assert.equal(JSON.stringify(session.state), before);
});
