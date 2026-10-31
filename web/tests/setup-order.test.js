import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  orderPlacementsBySetup, orderSelectedSequencePlacementsForSchedule, getSequenceSetup,
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

for (const equipmentCount of [2, 8]) {
  for (const currentSpace of [-1, 0, 1, 2]) {
    test(`${equipmentCount} equipment and locomotion groups stay contiguous across surfaces (current: ${currentSpace})`, () => {
      // Six stations use exact routing; twenty-four exercise the bounded fallback.
      const exercises = Array.from({ length: equipmentCount * 6 }, (_, i) => makeExercise({
        id: i + 1, wall: Boolean(Math.floor(i / 6) & 1), mirror: Boolean(Math.floor(i / 6) & 2),
        elevation: Boolean(Math.floor(i / 6) & 4), demand: i % 2,
        floor: Boolean(i % 2), soft: Boolean(i % 2),
        locomotion: ["None", "Room", "Field"][Math.floor(i / 2) % 3],
      }));
      const byId = new Map(exercises.map(e => [e.id, e]));
      const placements = exercises.map(root => ({ root, anchor: { id: String(root.id), order: root.id } }));
      const ordered = orderPlacementsBySetup(placements, byId, profile | WORKOUT_MODIFIERS.FieldLocomotion,
        currentSpace >= 0 ? exercises[currentSpace * 2] : null);
      const equipment = ordered.map(p => Math.floor((p.root.id - 1) / 6));
      const visits = equipment.filter((value, i) => i === 0 || value !== equipment[i - 1]);
      assert.equal(visits.length, equipmentCount);
      assert.equal(new Set(visits).size, equipmentCount);
      for (const group of new Set(equipment)) {
        const spaces = ordered.filter((_, i) => equipment[i] === group)
          .map(p => Math.floor((p.root.id - 1) / 2) % 3);
        const spaceVisits = spaces.filter((value, i) => i === 0 || value !== spaces[i - 1]);
        assert.equal(spaceVisits.length, 3);
        assert.equal(new Set(spaceVisits).size, 3);
        assert.equal(spaces.filter((value, i) => value !== 0 && (i === 0 || spaces[i - 1] === 0)).length, 1);
      }
      assert.deepEqual(new Set(ordered), new Set(placements));
      if (currentSpace >= 0) {
        assert.equal(visits[0], 0);
        assert.equal(Math.floor((ordered[0].root.id - 1) / 2) % 3, currentSpace);
      }
    });
  }
}

const profile = WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.Mirror |
  WORKOUT_MODIFIERS.TallMirror | WORKOUT_MODIFIERS.Floor | WORKOUT_MODIFIERS.UpperBodyClothing |
  WORKOUT_MODIFIERS.Elevation | WORKOUT_MODIFIERS.RoomLocomotion;
for (const equipment of [false, true]) {
  for (const minutes of [15, 45, 90]) {
    test(`actual ${minutes}-minute prepared workout keeps equipment and travel groups together (equipment: ${equipment})`, () => {
      const modifiers = WORKOUT_MODIFIERS.RoomLocomotion | WORKOUT_MODIFIERS.FieldLocomotion |
        (equipment ? profile | WORKOUT_MODIFIERS.SoleWallContact : 0);
      const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
      session.initialize();
      session.startWorkout(minutes, modifiers);
      const byId = new Map(catalog.map(e => [e.id, e]));
      const setups = session.state.activeWorkoutSession.initialSelections
        .map(s => getSequenceSetup(byId.get(s.rootExerciseId), byId, modifiers));
      const equipmentGroups = setups.map(s => s.station & 7);
      const equipmentVisits = equipmentGroups.filter((value, i) => i === 0 || value !== equipmentGroups[i - 1]);
      assert.equal(equipmentVisits.length, new Set(equipmentVisits).size);
      for (const group of new Set(equipmentGroups)) {
        const travel = setups.filter(s => (s.station & 7) === group).map(s => s.station & 24);
        const visits = travel.filter((value, i) => i === 0 || value !== travel[i - 1]);
        assert.equal(visits.length, new Set(visits).size);
        assert.ok(travel.filter((value, i) => value !== 0 && (i === 0 || travel[i - 1] === 0)).length <= 1);
      }
      assert.equal(session.getActiveGroups().length, minutes);
    });
  }
}
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
