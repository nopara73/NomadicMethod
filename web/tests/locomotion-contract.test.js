import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  LOCOMOTION_SPACE, WORKOUT_MODIFIERS, WorkoutSession, createDefaultState,
  getLocomotionSpace, withLocomotionSpace, getSessionMovementId,
  findLocomotionCatalogDeficiencies, isLocomotionSpaceMetadataValid,
  isCompatibleWithWorkoutModifiers,
  WORKOUT_EXERCISE_PHASE, RESOLUTIONS,
} from "../workout.js";

const catalog = JSON.parse(await readFile(new URL(
  "../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const capacity = { None: 0, Room: 1, Field: 2 };

for (const active of [false, true]) test(`split series retains every chosen member and feedback, active=${active}`, () => {
  const slot = RESOLUTIONS.get(30).groups.find(g => g.canonicalGroups.includes("ShoulderAbductors")).id;
  const state = createDefaultState();
  Object.assign(state, { catalogRevision: 79, activeCatalogRevision: active ? 79 : 0,
    activeWorkoutMinutes: active ? 3 : 0 });
  state.keptExerciseRootIdsBySelectionGroupId[slot] = [459];
  state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance] = { 459: -3 };
  if (!active) state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance][469] = -7;
  const session = new WorkoutSession(catalog, state, () => 0.4);
  session.initialize();
  if (active) {
    assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId[slot], [459]);
    session.endActiveWorkout();
  }
  assert.deepEqual(new Set(session.state.keptExerciseRootIdsBySelectionGroupId[slot]), new Set([459, 468, 469]));
  const feedback = session.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance];
  assert.equal(feedback[459], -3);
  assert.equal(feedback[468], -3);
  assert.equal(feedback[469], active ? -3 : -7);
  feedback[459] = -8;
  session.initialize();
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance][468], -3);
  assert.equal(session.state.activeCatalogRevision, 0);
  const current = createDefaultState();
  Object.assign(current, { catalogRevision: 80, activeCatalogRevision: 80, activeWorkoutMinutes: 3 });
  current.keptExerciseRootIdsBySelectionGroupId[slot] = [459];
  current.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance] = { 459: -4 };
  const modern = new WorkoutSession(catalog, current, () => 0.4);
  modern.initialize();
  modern.endActiveWorkout();
  assert.deepEqual(modern.state.keptExerciseRootIdsBySelectionGroupId[slot], [459]);
  assert.equal(Object.hasOwn(modern.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance], 468), false);
});

test("three travel capacities preserve other settings and admit shorter travel", () => {
  for (const space of Object.values(LOCOMOTION_SPACE)) {
    const other = WORKOUT_MODIFIERS.Light | WORKOUT_MODIFIERS.Shy;
    const profile = withLocomotionSpace(other, space);
    assert.equal(getLocomotionSpace(profile), space);
    assert.equal(profile & ~(WORKOUT_MODIFIERS.RoomLocomotion | WORKOUT_MODIFIERS.FieldLocomotion), other);
    for (const exercise of catalog) {
      if (capacity[exercise.locomotionSpace] > capacity[space]) {
        assert.equal(isCompatibleWithWorkoutModifiers(exercise, profile), false);
      } else if (!exercise.wallRequired && exercise.mirrorRelationship !== "MirrorOnly") {
        assert.equal(isCompatibleWithWorkoutModifiers(exercise,
          withLocomotionSpace(WORKOUT_MODIFIERS.None, space)), true);
      }
    }
  }
  assert.equal(getLocomotionSpace(WORKOUT_MODIFIERS.FieldLocomotion), "None");
  assert.throws(() => withLocomotionSpace(0, "Unknown"), RangeError);
});

test("approved movement minimums reject either undersized catalog without counting sides or aliases", () => {
  assert.equal(catalog.every(isLocomotionSpaceMetadataValid), true);
  assert.deepEqual(findLocomotionCatalogDeficiencies(catalog), []);
  for (const [space, minimum] of [["Room", 25], ["Field", 5]]) {
    const ids = [...new Set(catalog.filter(e => e.locomotionSpace === space &&
      e.sequenceBlocks.length).map(getSessionMovementId))];
    assert.ok(ids.length >= minimum);
    const kept = new Set(ids.slice(0, minimum - 1));
    const reduced = catalog.filter(e => e.locomotionSpace !== space || kept.has(getSessionMovementId(e)));
    assert.deepEqual(findLocomotionCatalogDeficiencies(reduced), [{
      locomotionSpace: space, matchingSessionMovementCount: minimum - 1,
      requiredSessionMovementCount: minimum,
    }]);
  }
  const invalid = { ...catalog[0], locomotionSpace: "Field", maxSpaceMeters: 2 };
  assert.equal(isLocomotionSpaceMetadataValid(invalid), false);
  assert.equal(isCompatibleWithWorkoutModifiers(invalid, withLocomotionSpace(0, "Field")), false);
});

test("both fixed hand configurations are one complete selection", () => {
  for (const id of [32, 216, 224, 510, 740, 1012]) {
    const exercise = catalog.find(e => e.id === id);
    assert.equal(exercise.sideSequence, "ShownHandPositionThenOpposite");
    assert.deepEqual(exercise.sequenceBlocks.map(b => b.sideCue), ["ShownHandPosition", "OppositeHandPosition"]);
    assert.deepEqual(exercise.sequenceBlocks.map(b => b.mirrorMedia), [false, true]);
    assert.ok(exercise.sequenceBlocks.every(b => b.exerciseId === id));
  }
});

test("all planned members respect each travel allowance and the selected duration", () => {
  for (const space of Object.values(LOCOMOTION_SPACE)) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0.35);
    session.startWorkout(10, withLocomotionSpace(WORKOUT_MODIFIERS.None, space));
    assert.equal(session.getActiveGroups().length, 10);
    for (const round of session.getActiveGroups()) {
      assert.ok(capacity[session.getSelectedExercise(round).locomotionSpace] <= capacity[space]);
    }
    const reopened = new WorkoutSession(catalog, JSON.parse(JSON.stringify(session.state)), () => 0.75);
    reopened.restoreAfterReopen();
    assert.equal(getLocomotionSpace(reopened.state.activeWorkoutModifiers), space);
    assert.equal(reopened.state.activeWorkoutSession.sessionId, session.state.activeWorkoutSession.sessionId);
  }
});

for (const phase of ["Ready", "Movement", "Rest"]) test(`catalog upgrade preserves the original ${phase} checkpoint and hand sequence`, () => {
  const old = catalog.filter(e => [21, 37, 216].includes(e.id)).map(e => ({
    ...e, locomotionSpace: "None", maxSpaceMeters: 2,
    ...(e.id === 216 ? { sideSequence: "Continuous", sequenceBlocks: [{
      exerciseId: 216, sideCue: "None", directionCue: "None",
      mirrorMedia: false, mediaSegment: "Full",
    }] } : {}),
  }));
  const now = Date.UTC(2026, 9, 14, 6);
  const legacy = new WorkoutSession(old, createDefaultState(), () => 0.4, () => now);
  legacy.startWorkout(3, 0);
  const next = legacy.getNextGroup();
  assert.equal(legacy.getSelectedExercise(next).id, 216);
  if (phase === "Movement") legacy.beginMovement(next, 28_000, now + 28_000);
  if (phase === "Rest") legacy.beginRest(next, now + 14_000);
  legacy.state.catalogRevision = 79;
  delete legacy.state.activeCatalogRevision;
  const original = JSON.parse(JSON.stringify(legacy.state));
  let reopened = new WorkoutSession(catalog, original, () => 0.7, () => now + 3_600_000);
  reopened.restoreAfterReopen();
  assert.equal(reopened.getSelectedExercise(reopened.getNextGroup()).id, 216);
  assert.equal(reopened.getActiveGroups().length, 3);
  assert.equal(reopened.state.activeWorkoutSession.sessionId, original.activeWorkoutSession.sessionId);
  assert.deepEqual(reopened.state.activeWorkoutSession.blocks, original.activeWorkoutSession.blocks);
  assert.equal(reopened.state.activeCatalogRevision, 79);
  assert.equal(getLocomotionSpace(reopened.state.activeWorkoutModifiers), "Room");
  reopened = new WorkoutSession(catalog, JSON.parse(JSON.stringify(reopened.state)), () => 0.2, () => now + 7_200_000);
  reopened.restoreAfterReopen();
  assert.equal(reopened.getSelectedExercise(reopened.getNextGroup()).id, 216);
  assert.equal(reopened.getActiveGroups().length, 3);
  assert.equal(reopened.state.lastWorkoutModifiers, 0);
  assert.equal(catalog.find(e => e.id === 216).sequenceBlocks.length, 2);
});
