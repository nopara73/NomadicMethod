import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  orderPlacementsBySetup, orderSelectedSequencePlacementsForSchedule,
  WorkoutSession, createDefaultState, parseStoredState, WORKOUT_MODIFIERS,
} from "../workout.js";

const cases = JSON.parse(readFileSync(new URL("./fixtures/setup-order-cases.json", import.meta.url)));
const catalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url)));
function makeExercise(spec) {
  return { ...catalog[0], id: spec.id, muscularDemand: spec.demand ?? 1,
    wallRequired: spec.wall ?? false, soleWallContactRequired: spec.soleWall ?? false,
    elevationRequired: spec.elevation ?? false, jumpPlatformRequired: spec.jumpPlatform ?? false,
    mirrorRelationship: spec.mirror ? "BenefitsGreatly" : "Agnostic",
    minimumMirrorCoverage: spec.mirror ? "FullBody" : "None",
    onlyFeetTouchGround: !spec.floor,
    floorContactPoints: spec.floor ? "Feet, Hands" : "Feet",
    hardFloorCompatibility: spec.soft ? "Incompatible" : "Compatible",
    locomotionSpace: spec.locomotion ?? "None",
    sequenceBlocks: (spec.blocks ?? [spec.id]).map(exerciseId => ({ exerciseId })) };
}
for (const entry of cases) {
  test(entry.name, () => {
    const exercises = entry.exercises.map(makeExercise);
    const byId = new Map(exercises.map(e => [e.id, e]));
    const placements = exercises.filter(e => e.sequenceBlocks.length && e.id !== entry.currentId)
      .map(root => ({ root, anchor: { id: String(root.id), order: root.id } }));
    const before = JSON.stringify(placements);
    const ordered = orderPlacementsBySetup(placements, byId, entry.modifiers ?? 0,
      byId.get(entry.currentId));
    assert.deepEqual(ordered.map(p => p.root.id), entry.expected);
    assert.equal(JSON.stringify(placements), before);
    assert.deepEqual(new Set(ordered), new Set(placements));
    // A saved order is authoritative even if it has more station transitions.
    const frozen = [...placements].reverse().map(p => p.anchor.id);
    assert.deepEqual(orderSelectedSequencePlacementsForSchedule(placements, byId, frozen,
      entry.modifiers ?? 0).map(p => p.anchor.id), frozen);
  });
}

const profile = WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.Mirror |
  WORKOUT_MODIFIERS.TallMirror | WORKOUT_MODIFIERS.Floor | WORKOUT_MODIFIERS.UpperBodyClothing |
  WORKOUT_MODIFIERS.Elevation | WORKOUT_MODIFIERS.RoomLocomotion;
for (const checkpoint of ["ready", "movement", "rest"]) {
  test(`setup order and progress survive cold reopen at ${checkpoint}`, () => {
    const state = createDefaultState();
    const session = new WorkoutSession(catalog, state, () => 0);
    session.initialize();
    session.startWorkout(15, profile);
    const current = session.getNextGroup();
    if (checkpoint === "movement") session.beginMovement(current, 20_000, Date.now() + 20_000);
    if (checkpoint === "rest") session.beginRest(current, Date.now() + 15_000);
    const before = session.getActiveGroups();
    const log = JSON.stringify(session.state.activeWorkoutSession);
    const restored = parseStoredState(JSON.stringify(session.state));
    const replay = new WorkoutSession(catalog, restored, () => 0);
    replay.restoreAfterReopen();
    assert.deepEqual(replay.getActiveGroups(), before);
    assert.equal(JSON.stringify(replay.state.activeWorkoutSession), log);
    assert.equal(replay.state.activeSetupOrderVersion, 1);
  });
}

test("a pre-logging Ready workout retains its old demand order", () => {
  const state = createDefaultState();
  const session = new WorkoutSession(catalog, state, () => 0);
  session.initialize();
  session.startWorkout(15, profile);
  session.state.activeSetupOrderVersion = 0;
  session.state.activeWorkoutSession = null;
  const before = session.getActiveGroups();
  const restored = parseStoredState(JSON.stringify(session.state));
  const replay = new WorkoutSession(catalog, restored, () => 0);
  replay.restoreAfterReopen();
  assert.deepEqual(replay.getActiveGroups(), before);
});
