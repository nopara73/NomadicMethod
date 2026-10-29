import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { WorkoutSession, createDefaultState, getWorkoutCatalog, getSequenceBlockCount,
  canUseSingleDirection, getWorkoutBlockAccent, getSessionMovementId, WORKOUT_MODIFIERS as M } from "../workout.js";

const catalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const cases = JSON.parse(readFileSync(new URL("./fixtures/fixed-position-cases.json", import.meta.url), "utf8"));
const now = Date.parse("2026-10-29T00:00:00Z");

for (const c of cases) {
  test(`${c.id} completes both fixed positions despite alternating torso motion`, () => {
    const exercise = catalog.find(e => e.id === c.id);
    assert.equal(exercise.sideSequence, c.sideSequence);
    assert.deepEqual(exercise.sequenceBlocks.map(b => b.sideCue), c.sideCues);
    assert.deepEqual(exercise.sequenceBlocks.map(b => b.mirrorMedia), [false, true]);
    assert.ok(exercise.sequenceBlocks.every(b => b.exerciseId === c.id && b.directionCue === "None"));
    assert.deepEqual(exercise.sequenceBlocks.map(b => getWorkoutBlockAccent({ sequenceSideCue: b.sideCue })), ["blue", "red"]);
    assert.equal(canUseSingleDirection(exercise), false);
    for (const minutes of [3, 5, 7, 10, 15, 20, 30, 60, 90])
      assert.equal(getSequenceBlockCount(exercise, minutes), 2);
    assert.equal(getSessionMovementId(exercise), c.sessionMovementId);
  });

  test(`${c.id} retains both positions when shortening the current selection`, () => {
    const state = createDefaultState();
    state.scores[c.id] = 100000;
    const s = new WorkoutSession(catalog, state, () => 0.4, () => now);
    s.initialize();
    s.startWorkout(60, M.Floor | M.Elevation | M.UpperBodyClothing);
    while (s.getSelectedExercise(s.getNextGroup()).id !== c.id) {
      const next = s.getNextGroup();
      s.beginRest(next, now + 15000);
      if (s.isIntermediateSequenceBlock(next)) s.advanceSequence(next); else s.recordOutcome(next, true);
      s.clearPendingRest();
    }
    const first = s.getNextGroup();
    const key = first.selectionGroupId ?? first.id;
    const before = s.getActiveGroups().filter(g => (g.selectionGroupId ?? g.id) === key);
    assert.equal(before.length, 2);
    const minimum = s.getMinimumActiveWorkoutMinutes();
    assert.ok(minimum < 60);
    s.resizeActiveWorkout(minimum);
    assert.deepEqual(s.getActiveGroups().filter(g => (g.selectionGroupId ?? g.id) === key), before);
    assert.equal(s.state.activeWorkoutMinutes, minimum);
    s.beginRest(s.getNextGroup(), now + 15000);
    s.advanceSequence(s.getNextGroup());
    s.clearPendingRest();
    assert.equal(s.getNextGroup().sequenceSideCue, c.sideCues[1]);
    assert.equal(s.getNextGroup().mirrorSequenceMedia, true);
  });

  for (const phase of ["Ready", "Movement", "Rest"]) test(`${c.id} preserves the revision-98 ${phase} checkpoint and preferences`, () => {
    const previousCatalog = getWorkoutCatalog(catalog, 98);
    const oldExercise = previousCatalog.find(e => e.id === c.id);
    assert.equal(oldExercise.sideSequence, "Alternating");
    assert.equal(oldExercise.sequenceBlocks.length, 1);
    const state = createDefaultState();
    state.scores[c.id] = 100000;
    const old = new WorkoutSession(previousCatalog, state, () => 0.4, () => now);
    old.initialize();
    old.startWorkout(30, M.Floor | M.Elevation | M.UpperBodyClothing);
    const current = old.getActiveGroups().find(g => old.getSelectedExercise(g).id === c.id);
    assert.ok(current, `legacy lineup includes ${c.id}`);
    while (old.getNextGroup().id !== current.id) {
      const next = old.getNextGroup();
      old.beginRest(next, now + 15000);
      if (old.isIntermediateSequenceBlock(next)) old.advanceSequence(next);
      else old.recordOutcome(next, true);
      old.clearPendingRest();
    }
    if (phase === "Movement") old.beginMovement(current, 28000, now + 28000);
    if (phase === "Rest") old.beginRest(current, now + 15000);
    old.state.catalogRevision = 98;
    old.state.activeCatalogRevision = 98;
    if (phase === "Ready") delete old.state.activeCatalogRevision;
    old.state.keptExerciseRootIdsBySelectionGroupId[current.selectionGroupId ?? current.id] = [c.id];
    const saved = structuredClone(old.state);
    const rounds = old.getActiveGroups().map(g => [g.id, old.getSelectedExercise(g).id]);
    let checkpoint = saved;
    for (let reopen = 0; reopen < 2; reopen++) {
      const restored = new WorkoutSession(catalog, structuredClone(checkpoint), () => 0.8, () => now + 86400000);
      restored.restoreAfterReopen();
      assert.equal(restored.state.activeCatalogRevision, 98);
      assert.deepEqual(restored.getActiveGroups().map(g => [g.id, restored.getSelectedExercise(g).id]), rounds);
      for (const key of ["activeWorkoutSession", "workoutHistory", "scores", "keptExerciseRootIdsBySelectionGroupId", "outcomes"])
        assert.deepEqual(restored.state[key], saved[key], key);
      assert.equal(restored.exercisesById.get(c.id).sequenceBlocks.length, 1);
      if (phase === "Movement") {
        assert.equal(restored.state.pendingMovementPausedByUser, true);
        assert.equal(restored.state.pendingMovementMillisecondsRemaining, 28000);
      }
      if (phase === "Rest") {
        assert.equal(restored.state.pendingRestPausedByUser, true);
        assert.equal(restored.state.pendingRestMillisecondsRemaining, 15000);
      }
      checkpoint = structuredClone(restored.state);
    }
  });
}

test("the feet-down Russian twist remains one naturally alternating sequence", () => {
  const exercise = catalog.find(e => e.id === 1154);
  assert.equal(exercise.sideSequence, "Alternating");
  assert.equal(exercise.sequenceBlocks.length, 1);
});
