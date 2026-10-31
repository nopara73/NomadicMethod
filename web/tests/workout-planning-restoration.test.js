import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  WorkoutSession, createDefaultState, getResolution, CURRENT_CATALOG_REVISION,
  isCompatibleWithWorkoutModifiers, WORKOUT_MODIFIERS as M,
} from "../workout.js";

const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const cases = JSON.parse(await readFile(new URL("fixtures/workout-planning-cases.json", import.meta.url), "utf8"));
const rejectedSlotCases = JSON.parse(await readFile(new URL("fixtures/rejected-slot-fallback-cases.json", import.meta.url), "utf8"));
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
      const covered = new Set(log.initialSelections.flatMap(s => s.coveredWorkoutGroupIds));
      assert.ok(getResolution(item.expectedResolution).groups.every(g => covered.has(g.id)), 'required anatomy remains covered alongside distinct additions');
      assert.equal(log.initialSelections.filter(s => s.selectionScoreAtStart < 0)
        .reduce((sum, s) => sum + s.sequenceBlockCount * s.setCount, 0), item.expectedRejectedBlocks);
      assert.ok(log.initialSelections.every(s => !item.excludedRoots.includes(s.rootExerciseId)));
      assert.equal(log.initialSelections.reduce((sum, s) => sum + s.sequenceBlockCount * s.setCount, 0), item.minutes);
      for (const selection of log.initialSelections) {
        const root = session.getSelectedExercise(rounds.find(g =>
          (g.selectionGroupId ?? g.id) === selection.selectionGroupId));
        assert.equal(selection.sequenceBlockCount, root.sequenceBlocks.length);
        const selectedRounds = rounds.filter(g => (g.selectionGroupId ?? g.id) === selection.selectionGroupId);
        assert.equal(selectedRounds.length, selection.sequenceBlockCount * selection.setCount);
        assert.ok(selectedRounds.every(g => isCompatibleWithWorkoutModifiers(session.getSelectedExercise(g), item.modifiers)));
      }
    });
  }
}

for (const item of rejectedSlotCases) {
  test(`rejected singleton fallback: ${item.name}`, () => {
    // Preserve the actual older singleton fixture after filling the catalog.
    const legacyCatalog=structuredClone(catalog.filter(e=>e.id<1730));
    legacyCatalog.find(e=>e.id===1069).hardFloorCompatibility="Incompatible";
    const state = {...createDefaultState(),
      catalogRevision: CURRENT_CATALOG_REVISION,
      exerciseScoreAdjustmentsByPhase: Object.fromEntries(['Warmup', 'PeakPerformance', 'Fatigued']
        .map(phase => [phase, {[item.rejectedRoot]: -2}])),
      keptExerciseRootIdsBySelectionGroupId: {'r30.pelvic-floor-perineum': [item.rejectedRoot]},
    };
    let session = new WorkoutSession(legacyCatalog, state, () => 0.5, () => now);
    session.initialize();
    const feedback = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
    session.startWorkout(item.minutes, item.modifiers);
    const log = structuredClone(session.state.activeWorkoutSession);
    assert.ok(log.initialSelections.every(s => s.rootExerciseId !== item.rejectedRoot));
    assert.equal(log.initialSelections.some(s => s.coveredWorkoutGroupIds.includes('r30.pelvic-floor-perineum')), !item.broaden);
    if (item.broaden) assert.ok(session.state.activeDurationSelectionGroupIds.includes('r15.abdominal-pelvic-floor'));
    assert.equal(log.initialSelections.reduce((sum, s) => sum + s.sequenceBlockCount * s.setCount, 0), item.minutes);
    assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, feedback);
    assert.ok(session.state.keptExerciseRootIdsBySelectionGroupId['r30.pelvic-floor-perineum'].includes(item.rejectedRoot));
    assert.equal(session.state.workoutHistory.length, 0);
    assert.equal(log.blocks.length, 0);
    assert.equal(log.decisions.length, 0);
    const rounds = session.getActiveGroups();
    assert.equal(rounds.length, item.minutes);
    assert.ok(rounds.every(round => isCompatibleWithWorkoutModifiers(session.getSelectedExercise(round), item.modifiers)));
    session = new WorkoutSession(catalog, structuredClone(session.state), () => 0.5, () => now);
    session.restoreAfterReopen();
    assert.deepEqual(session.getActiveGroups(), rounds);
    assert.deepEqual(session.state.activeWorkoutSession, log);
  });
}

test("duration extension avoids a rejected future slot and preserves the current sequence", () => {
  const state = {...createDefaultState(), catalogRevision: CURRENT_CATALOG_REVISION,
    exerciseScoreAdjustmentsByPhase: Object.fromEntries(['Warmup', 'PeakPerformance', 'Fatigued']
      .map(phase => [phase, {918: -2}])),
  };
  const session = new WorkoutSession(catalog, state, () => 0.5, () => now);
  session.initialize();
  session.startWorkout(15, 5232);
  const current = session.getNextGroup();
  const currentSequence = session.getActiveGroups().filter(g => (g.selectionGroupId ?? g.id) === (current.selectionGroupId ?? current.id));
  const feedback = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
  const sessionId = session.state.activeWorkoutSession.sessionId;
  session.resizeActiveWorkout(45);
  assert.deepEqual(session.getActiveGroups().slice(0, currentSequence.length), currentSequence);
  assert.equal(session.getNextGroup().id, current.id);
  assert.equal(session.state.activeWorkoutSession.sessionId, sessionId);
  assert.equal(session.getActiveGroups().length, 45);
  assert.ok(session.state.activeWorkoutSession.durationChanges[0].plannedSelections.every(s => s.rootExerciseId !== 918));
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, feedback);
  assert.equal(session.state.activeWorkoutSession.blocks.length, 0);
  assert.equal(session.state.activeWorkoutSession.decisions.length, 0);
});

test("a still-kept singleton retains priority over an old global negative score", () => {
  const state = {...createDefaultState(), catalogRevision: CURRENT_CATALOG_REVISION,
    scores: {918: -2}, keptExerciseRootIdsBySelectionGroupId: {'r30.pelvic-floor-perineum': [918]},
  };
  const session = new WorkoutSession(catalog, state, () => 0.5, () => now);
  session.initialize();
  session.startWorkout(30, 5232);
  assert.ok(session.state.activeWorkoutSession.initialSelections.some(s => s.coveredWorkoutGroupIds.includes('r30.pelvic-floor-perineum')));
  assert.ok(session.state.activeWorkoutSession.initialSelections.some(s => s.rootExerciseId === 918));
  assert.equal(session.getScore(session.exercisesById.get(918)), -2);
});

test("Rejection feedback does not displace Light choices", () => {
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
  const groups = structuredClone(session.state.activeDurationSelectionGroupIds);
  session = new WorkoutSession(catalog, structuredClone(session.state), () => 0.5, () => now);
  session.restoreAfterReopen();
  assert.deepEqual(session.getActiveGroups(), rounds);
  assert.deepEqual(session.state.activeWorkoutSession, log);
  assert.deepEqual(session.state.activeDurationSelectionGroupIds, groups);
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
      assertCompleteGroups();
      session.shuffleNextExercise(session.getNextGroup());
      assertCompleteGroups();
      session.reconfigureActiveWorkout(modifiers ^ M.Mirror ^ M.TallMirror, session.getNextGroup().id);
      assertCompleteGroups();

      function assertCompleteGroups() {
        const rounds = session.getActiveGroups();
        assert.equal(rounds.length, minutes);
        const covered = new Set(session.state.activeWorkoutSession.initialSelections.flatMap(s => s.coveredWorkoutGroupIds));
        assert.ok(getResolution(minutes).groups.every(g => covered.has(g.id)));
        for (const key of new Set(rounds.map(g => g.selectionGroupId ?? g.id))) {
          const selected = rounds.filter(g => (g.selectionGroupId ?? g.id) === key);
          const blocks = selected[0].sequenceBlockCount ?? 1;
          assert.equal(selected.length % blocks, 0);
          assert.deepEqual(selected.map(g => g.sequenceBlockIndex ?? 0), selected.map((_, i) => i % blocks));
        }
      }
    });
  }
}

test("legacy broader plans restore paused sides and preserve completed work through edits", async () => {
  const saved = JSON.parse(await readFile(new URL("fixtures/legacy-adaptive-session.json", import.meta.url), "utf8"));
  let session = new WorkoutSession(catalog, saved, () => 0.5, () => now);
  assert.ok(session.state.activeDurationSelectionGroupIds);
  const current = session.getNextGroup();
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
