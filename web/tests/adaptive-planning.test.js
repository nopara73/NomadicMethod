import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  WorkoutSession, createDefaultState, getResolution,
  isCompatibleWithWorkoutModifiers, WORKOUT_MODIFIERS as M,
} from "../workout.js";

const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const cases = JSON.parse(await readFile(new URL("fixtures/adaptive-planning-cases.json", import.meta.url), "utf8"));
const profile = M.Mirror | M.TallMirror | M.HardFloor | M.Wall;
const now = Date.parse("2026-09-26T04:00:00Z");
const rejectedState = () => ({
  ...createDefaultState(),
  exerciseScoreAdjustmentsByPhase: structuredClone(cases[2].adjustments),
});

for (const item of cases) {
  for (const random of [0, 0.5, 0.9]) {
    test(`${item.name} (random ${random})`, () => {
      const state = {
        ...createDefaultState(),
        scores: Object.fromEntries(catalog.map(e => [e.id, item.legacyScore])),
        exerciseScoreAdjustmentsByPhase: structuredClone(item.adjustments),
      };
      const session = new WorkoutSession(catalog, state, () => random, () => now);
      session.initialize();
      const feedback = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
      const scores = structuredClone(session.state.scores);
      session.startWorkout(item.minutes, item.modifiers);
      const rounds = session.getActiveGroups();
      const log = session.state.activeWorkoutSession;
      assert.equal(rounds.length, item.minutes);
      assert.equal(log.workoutMinutes, item.minutes);
      assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, feedback);
      assert.deepEqual(session.state.scores, scores);
      assert.equal(session.state.workoutHistory.length, 0);
      assert.deepEqual(log.initialSelections.flatMap(s => s.coveredWorkoutGroupIds).sort(),
        getResolution(item.expectedResolution).groups.map(g => g.id).sort());
      assert.equal(log.initialSelections.filter(s => s.selectionScoreAtStart < 0)
        .reduce((sum, s) => sum + s.sequenceBlockCount * s.setCount, 0), item.expectedRejectedBlocks);
      assert.ok(log.initialSelections.every(s => !item.excludedRoots.includes(s.rootExerciseId)));
      assert.equal(log.initialSelections.reduce((sum, s) => sum + s.sequenceBlockCount * s.setCount, 0), item.minutes);
      for (const selection of log.initialSelections) {
        const root = catalog.find(e => e.id === selection.rootExerciseId);
        if (item.minutes <= 7) {
          assert.equal(selection.sequenceBlockCount, 1);
          assert.equal(selection.setCount, 1);
        }
        assert.equal(selection.sequenceBlockCount, root.sequenceBlocks.length);
        const selectedRounds = rounds.filter(g => (g.selectionGroupId ?? g.id) === selection.selectionGroupId);
        assert.equal(selectedRounds.length, selection.sequenceBlockCount * selection.setCount);
        assert.ok(selectedRounds.every(g => isCompatibleWithWorkoutModifiers(session.getSelectedExercise(g), item.modifiers)));
      }
    });
  }
}

test("Light adaptation cannot spend more blocks on demanding work", () => {
  const baseline = new WorkoutSession(catalog, createDefaultState(), () => 0.5, () => now);
  baseline.initialize();
  baseline.startWorkout(7, profile | M.Light);
  const session = new WorkoutSession(catalog, rejectedState(), () => 0.5, () => now);
  session.initialize();
  session.startWorkout(7, profile | M.Light);
  const lightBlocks = s => s.state.activeWorkoutSession.initialSelections
    .filter(p => catalog.find(e => e.id === p.rootExerciseId).sequenceBlocks
      .every(b => catalog.find(e => e.id === b.exerciseId).muscularDemand === 0))
    .reduce((sum, p) => sum + p.sequenceBlockCount * p.setCount, 0);
  assert.ok(lightBlocks(session) >= lightBlocks(baseline));
  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.getActiveGroups().length, 7);
});

test("an existing Ready workout does not replan its grouping on upgrade", () => {
  let session = new WorkoutSession(catalog, createDefaultState(), () => 0.5, () => now);
  session.initialize();
  session.startWorkout(7, profile);
  session.state.exerciseScoreAdjustmentsByPhase = rejectedState().exerciseScoreAdjustmentsByPhase;
  const rounds = session.getActiveGroups();
  const log = structuredClone(session.state.activeWorkoutSession);
  session = new WorkoutSession(catalog, structuredClone(session.state), () => 0.5, () => now);
  session.restoreAfterReopen();
  assert.deepEqual(session.getActiveGroups(), rounds);
  assert.deepEqual(session.state.activeWorkoutSession, log);
  assert.equal(session.state.activeDurationSelectionGroupIds, null);
});

for (const minutes of [3, 5, 7]) {
  for (const light of [false, true]) {
    test(`${minutes}-minute muscle groups survive higher-scoring paired exercises (Light ${light})`, () => {
      const session = new WorkoutSession(catalog, {
        ...createDefaultState(),
        scores: Object.fromEntries(catalog.map(e => [e.id, e.sequenceBlocks.length === 1 ? -5 : 50])),
      }, () => 0.5, () => now);
      const modifiers = profile | (light ? M.Light : 0);
      session.initialize();
      session.startWorkout(minutes, modifiers);
      assertSingleBlockGroups();
      session.shuffleNextExercise(session.getNextGroup());
      assertSingleBlockGroups();
      session.reconfigureActiveWorkout(modifiers ^ M.Mirror ^ M.TallMirror, session.getNextGroup().id);
      assertSingleBlockGroups();

      function assertSingleBlockGroups() {
        const rounds = session.getActiveGroups();
        assert.equal(rounds.length, minutes);
        assert.deepEqual(rounds.map(g => g.selectionGroupId ?? g.id).sort(),
          getResolution(minutes).groups.map(g => g.id).sort());
        assert.ok(rounds.every(g => session.getSelectedExercise(g).sequenceBlocks.length === 1));
      }
    });
  }
}

test("broader plans restore paused sides and preserve completed work through edits", () => {
  let session = new WorkoutSession(catalog, rejectedState(), () => 0.5, () => now);
  session.initialize();
  session.startWorkout(10, profile);
  assert.ok(session.state.activeDurationSelectionGroupIds);
  const first = session.getNextGroup();
  session.beginRest(first, now + 15_000);
  if (session.isIntermediateSequenceBlock(first)) session.advanceSequence(first);
  else session.recordOutcome(first, true);
  session.clearPendingRest();
  const current = session.getNextGroup();
  session.beginMovement(current, 25_000, now + 25_000);
  session.pauseMovement(current, 25_000, true);
  const rounds = session.getActiveGroups();
  const blocks = structuredClone(session.state.activeWorkoutSession.blocks);
  const feedback = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
  const sessionId = session.state.activeWorkoutSession.sessionId;
  session = new WorkoutSession(catalog, structuredClone(session.state), () => 0.5, () => now);
  session.restoreAfterReopen();
  assert.deepEqual(session.getActiveGroups(), rounds);
  assert.equal(session.state.pendingMovementMillisecondsRemaining, 25_000);
  assert.equal(session.state.pendingMovementGroupId, current.id);
  session.resizeActiveWorkout(15);
  assert.equal(session.getActiveGroups().length, 15);
  assert.equal(session.getNextGroup().id, current.id);
  session.reconfigureActiveWorkout(profile ^ M.Mirror ^ M.TallMirror, current.id);
  assert.equal(session.getActiveGroups().length, 15);
  assert.deepEqual(session.state.activeWorkoutSession.blocks, blocks);
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, feedback);
  assert.equal(session.state.activeWorkoutSession.sessionId, sessionId);
  assert.equal(session.state.activeWorkoutSession.durationChanges.length, 1);
});
