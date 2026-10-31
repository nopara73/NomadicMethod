import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import test from "node:test";
import { exerciseDisplayName } from "../exercise-names.js";
import { WorkoutSession, createDefaultState, getWorkoutCatalog, getSequenceBlockCount,
  canUseSingleDirection, WORKOUT_MODIFIERS as M } from "../workout.js";

const catalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const c = JSON.parse(readFileSync(new URL("./fixtures/arm-pulses.json", import.meta.url), "utf8"));

test("arm pulses complete both fixed stances before the two wheel directions", () => {
  const root = catalog.find(e => e.id === c.id);
  assert.equal(root.name, exerciseDisplayName(root.id, c.name));
  assert.equal(root.motionProfile, c.motionProfile);
  assert.equal(root.sideSequence, c.sideSequence);
  assert.deepEqual(root.sequenceBlocks, c.blocks);
  assert.equal(new Set(root.sequenceBlocks.map(b => b.exerciseId)).size, 2);
  assert.equal(canUseSingleDirection(root), false);
  for (const minutes of [3, 5, 7, 10, 15, 20, 30, 60, 90])
    assert.equal(getSequenceBlockCount(root, minutes), 4);
  for (const revision of [82, 99, 100, 101]) {
    const previous = getWorkoutCatalog(catalog, revision).find(e => e.id === c.id);
    assert.equal(previous.sideSequence, "Continuous");
    assert.deepEqual(previous.sequenceBlocks, c.previousBlocks);
  }
  const video = readFileSync(new URL(`../../NomadicMethod/Assets/${c.video}`, import.meta.url));
  assert.equal(createHash("sha256").update(video).digest("hex"), c.videoSha256);
});

test("the clarity rename preserves feedback while the older different action resets it", () => {
  for (const [previousName, expected] of [[c.previousName, -7], [c.legacyName, 0]]) {
    const state = createDefaultState();
    state.catalogRevision = 101;
    state.catalogIdentities = { [c.id]: `${previousName}\u001f${c.video}` };
    state.scores[c.id] = -7;
    state.keptExerciseRootIdsBySelectionGroupId["r30.shoulder-abductors"] = [c.id];
    const session = new WorkoutSession(catalog, state, () => 0.4);
    session.initialize();
    assert.equal(session.state.scores[c.id] ?? 0, expected);
    if (expected === -7)
      assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId["r30.shoulder-abductors"], [c.id]);
  }
});

for (const phase of ["Ready", "Movement", "Rest"])
  test(`unfinished three-part sequence retains its ${phase} checkpoint, feedback and history`, () => {
    const now = Date.parse("2026-10-31T00:00:00Z");
    const legacy = getWorkoutCatalog(catalog, 101);
    const state = createDefaultState();
    state.scores[c.id] = 100000;
    const old = new WorkoutSession(legacy, state, () => 0.4, () => now);
    old.initialize();
    old.state.keptExerciseRootIdsBySelectionGroupId["r30.shoulder-abductors"] = [c.id];
    old.startWorkout(60, M.Floor | M.Elevation | M.UpperBodyClothing);
    old.state.catalogRevision = old.state.activeCatalogRevision = 101;
    const current = old.getActiveGroups().find(g => old.getSelectedExercise(g).id === c.id);
    assert.ok(current);
    while (old.getNextGroup().id !== current.id) {
      const next = old.getNextGroup();
      old.beginRest(next, now + 15000);
      if (old.isIntermediateSequenceBlock(next)) old.advanceSequence(next);
      else old.recordOutcome(next, true);
      old.clearPendingRest();
    }
    if (phase === "Movement") old.beginMovement(current, 28000, now + 28000);
    if (phase === "Rest") old.beginRest(current, now + 15000);
    old.state.keptExerciseRootIdsBySelectionGroupId[current.selectionGroupId ?? current.id] = [c.id];
    const groups = old.getActiveGroups();
    const saved = structuredClone(old.state);
    const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0.8, () => now + 365 * 86400000);
    restored.restoreAfterReopen();
    assert.equal(restored.state.activeCatalogRevision, 101);
    assert.deepEqual(restored.getActiveGroups(), groups);
    assert.equal(restored.getNextGroup().id, current.id);
    assert.deepEqual(restored.exercisesById.get(c.id).sequenceBlocks, c.previousBlocks);
    for (const key of ["activeWorkoutSession", "workoutHistory", "scores", "keptExerciseRootIdsBySelectionGroupId", "outcomes"])
      assert.deepEqual(restored.state[key], saved[key], key);
    if (phase === "Movement") {
      assert.equal(restored.state.pendingMovementPausedByUser, true);
      assert.equal(restored.state.pendingMovementMillisecondsRemaining, 28000);
    }
    if (phase === "Rest") {
      assert.equal(restored.state.pendingRestPausedByUser, true);
      assert.equal(restored.state.pendingRestMillisecondsRemaining, 15000);
    }
    restored.resizeActiveWorkout(restored.getMinimumActiveWorkoutMinutes());
    assert.equal(restored.getNextGroup().id, current.id);
    assert.deepEqual(restored.exercisesById.get(c.id).sequenceBlocks, c.previousBlocks);
    assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId, saved.keptExerciseRootIdsBySelectionGroupId);
  });
