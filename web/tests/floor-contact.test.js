import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import {
  WORKOUT_MODIFIERS, isFloorContactCompatible, isFloorContactMetadataReviewed,
  findFloorCatalogDeficiencies, findFloorAndWallCatalogDeficiencies,
  findFloorAndLocomotionCatalogDeficiencies,
  FLOOR_LOCOMOTION_EXERCISE_IDS, getWorkoutCatalog,
  isCompatibleWithWorkoutModifiers,
  WorkoutSession, createDefaultState, parseStoredState, DEFAULT_WORKOUT_MODIFIERS,
  WORKOUT_EXERCISE_PHASE,
} from "../workout.js";

const fixture = JSON.parse(readFileSync(new URL("./fixtures/floor-contact-cases.json", import.meta.url)));
for (const entry of fixture.cases) {
  for (let index = 0; index < fixture.profiles.length; index++) {
    const profile = fixture.profiles[index];
    test(`${entry.name}: floor/clothing/hard-floor profile ${profile}`, () => {
      assert.equal(isFloorContactCompatible(entry, profile), entry.expected[index]);
      assert.equal(isFloorContactCompatible(entry, profile |
        WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Shy),
      entry.expected[index], "Other toggles cannot override physical floor contact");
    });
  }
}

test("only explicit legacy feet-only metadata can omit contact points", () => {
  assert.equal(isFloorContactMetadataReviewed({ onlyFeetTouchGround: true }), true);
  assert.equal(isFloorContactMetadataReviewed({ onlyFeetTouchGround: false }), false);
  assert.equal(isFloorContactMetadataReviewed({}), false);
});

const baseExercise = JSON.parse(readFileSync(new URL(
  "../../NomadicMethod/Assets/exercises.json", import.meta.url)))[0];
function floorExercise(id, changes = {}) {
  return { ...baseExercise, id, sessionMovementId: id,
    onlyFeetTouchGround: false, floorContactPoints: "Feet, Hands",
    sequenceBlocks: [{ exerciseId: id, sideCue: "None", directionCue: "None",
      mirrorMedia: false, mediaSegment: "Full" }], ...changes };
}

for (const [count, enough] of [[200, false], [201, true], [202, true]]) {
  test(`floor minimum counts ${count} distinct selectable movements`, () => {
    const exercises = Array.from({ length: count }, (_, i) => floorExercise(i + 1));
    assert.equal(findFloorCatalogDeficiencies(exercises).length === 0, enough);
    const aliases = exercises.map(e => ({ ...e, sessionMovementId: 1 }));
    assert.equal(findFloorCatalogDeficiencies(aliases)[0].matchingSessionMovementCount, 1);
  });
}
for (const [count, enough] of [[20, false], [21, true], [22, true]]) {
  test(`floor-and-wall minimum counts ${count} movements requiring both`, () => {
    const exercises = Array.from({ length: count }, (_, i) => floorExercise(i + 1, { wallRequired: true }));
    assert.equal(findFloorAndWallCatalogDeficiencies(exercises).length === 0, enough);
    assert.equal(findFloorAndWallCatalogDeficiencies(exercises.map(e => ({
      ...e, sessionMovementId: 1 })))[0].matchingSessionMovementCount, 1);
    assert.equal(findFloorAndWallCatalogDeficiencies(exercises.map(e => ({
      ...e, wallRequired: false })))[0].matchingSessionMovementCount, 0);
  });
}

test("floor-and-wall cannot be fabricated by combining separate equipment members", () => {
  const floor = floorExercise(1, { sequenceBlocks: [{ exerciseId: 1 }, { exerciseId: 2 }] });
  const wall = floorExercise(2, { onlyFeetTouchGround: true, floorContactPoints: "Feet",
    wallRequired: true, sequenceBlocks: [] });
  assert.equal(findFloorCatalogDeficiencies([floor, wall])[0].matchingSessionMovementCount, 1);
  assert.equal(findFloorAndWallCatalogDeficiencies([floor, wall])[0].matchingSessionMovementCount, 0);
  wall.upperBodyClothingRequirement = "BareUpperBodyRequired";
  floor.floorContactPoints = "Torso";
  floor.upperBodyClothingRequirement = "ClothingRequired";
  assert.equal(findFloorCatalogDeficiencies([floor, wall])[0].matchingSessionMovementCount, 0);
});

for (const [count, enough] of [[20, false], [21, true], [22, true]]) {
  test(`floor-and-locomotion minimum counts ${count} distinct Room or Field movements`, () => {
    const exercises = Array.from({ length: count }, (_, i) => floorExercise(i + 1, {
      locomotionSpace: i % 2 ? "Room" : "Field", maxSpaceMeters: i % 2 ? 6 : 20,
    }));
    assert.equal(findFloorAndLocomotionCatalogDeficiencies(exercises).length === 0, enough);
    assert.equal(findFloorAndLocomotionCatalogDeficiencies(exercises.map(e => ({
      ...e, sessionMovementId: 1 })))[0].matchingSessionMovementCount, 1);
  });
}

test("floor-and-locomotion needs both in one member and one consistent clothing profile", () => {
  const floor = floorExercise(1, { sequenceBlocks: [{ exerciseId: 1 }, { exerciseId: 2 }] });
  const travel = floorExercise(2, { onlyFeetTouchGround: true, floorContactPoints: "Feet",
    locomotionSpace: "Room", maxSpaceMeters: 6, sequenceBlocks: [] });
  assert.equal(findFloorAndLocomotionCatalogDeficiencies([floor, travel])[0].matchingSessionMovementCount, 0);
  floor.locomotionSpace = "Room";
  floor.floorContactPoints = "Torso";
  floor.upperBodyClothingRequirement = "ClothingRequired";
  travel.upperBodyClothingRequirement = "BareUpperBodyRequired";
  assert.equal(findFloorAndLocomotionCatalogDeficiencies([floor, travel])[0].matchingSessionMovementCount, 0);
  assert.equal(findFloorAndLocomotionCatalogDeficiencies([floorExercise(1)])[0].matchingSessionMovementCount, 0);
});

test("both equipment switches and sole permission are enforced independently", () => {
  const exercise = floorExercise(1, { wallRequired: true, soleWallContactRequired: true });
  for (const floor of [0, WORKOUT_MODIFIERS.Floor])
    for (const wall of [0, WORKOUT_MODIFIERS.Wall])
      for (const soles of [0, WORKOUT_MODIFIERS.SoleWallContact]) {
        assert.equal(isCompatibleWithWorkoutModifiers(exercise, floor | wall | soles),
          Boolean(floor && wall && soles));
      }
});

const catalog = JSON.parse(readFileSync(new URL(
  "../../NomadicMethod/Assets/exercises.json", import.meta.url)));
test("the admitted catalog satisfies all distinct floor minimums", () => {
  assert.deepEqual(findFloorCatalogDeficiencies(catalog), []);
  assert.deepEqual(findFloorAndWallCatalogDeficiencies(catalog), []);
  assert.deepEqual(findFloorAndLocomotionCatalogDeficiencies(catalog), []);
});

test("revision-85 floor travel stays out of earlier workouts and its boundary inventory needs every admission", () => {
  assert.equal(FLOOR_LOCOMOTION_EXERCISE_IDS.length, 8);
  assert.equal(getWorkoutCatalog(catalog, 84)
    .some(e => FLOOR_LOCOMOTION_EXERCISE_IDS.includes(e.id)), false);
  assert.equal(getWorkoutCatalog(catalog, 85)
    .filter(e => FLOOR_LOCOMOTION_EXERCISE_IDS.includes(e.id)).length, 8);
  const revision85Catalog = getWorkoutCatalog(catalog, 85);
  assert.deepEqual(findFloorAndLocomotionCatalogDeficiencies(revision85Catalog.filter(e => e.id !== 1431)), [{
    matchingSessionMovementCount: 20, requiredSessionMovementCount: 21,
  }]);
  for (const id of FLOOR_LOCOMOTION_EXERCISE_IDS) {
    const exercise = catalog.find(e => e.id === id);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, 0), false);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, WORKOUT_MODIFIERS.Floor), false);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, WORKOUT_MODIFIERS.Floor |
      WORKOUT_MODIFIERS.UpperBodyClothing | WORKOUT_MODIFIERS.RoomLocomotion), true);
  }
});

for (const phase of ["Ready", "Movement", "Rest"]) {
  test(`chest-contact correction retains the version-85 ${phase} session and completed work`, () => {
    const now = Date.UTC(2026, 9, 20, 6);
    const oldCatalog = catalog.map(e => ({ ...e, score: e.id === 1436 ? 1_000_000 : 0,
      ...(e.id === 1436 ? { floorContactPoints: "Feet, Hands", upperBodyClothingRequirement: "Agnostic" } : {}) }));
    const profile = WORKOUT_MODIFIERS.Floor | WORKOUT_MODIFIERS.RoomLocomotion;
    const original = new WorkoutSession(oldCatalog, createDefaultState(), () => 0.4, () => now);
    original.startWorkout(60, profile);
    const target = original.exercisesById.get(1436);
    const slot = original.getSelectionGroups().find(g => original.isSavedSelectionValid(target, g, profile));
    original.state.selectedExerciseIds[original.getSelectionStorageKey(slot.id, profile)] = 1436;
    for (let i = 0; i < 80; i++) {
      const next = original.getNextGroup();
      if (original.getSelectedExercise(next).id === 1436) break;
      if (original.isIntermediateSequenceBlock(next)) original.advanceSequence(next);
      else original.recordOutcome(next, true);
    }
    const current = original.getNextGroup();
    assert.equal(original.getSelectedExercise(current).id, 1436);
    if (phase === "Movement") original.beginMovement(current, 28_000, now + 28_000);
    if (phase === "Rest") {
      original.beginRest(current, now + 14_000);
      original.state.pendingRestMillisecondsRemaining = 14_000;
    }
    original.state.catalogRevision = original.state.activeCatalogRevision = 85;
    original.keepSequenceInSlot(slot.id, target);
    original.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance] = { 1436: -3 };
    const before = structuredClone(original.state);
    const completed = before.activeWorkoutSession.blocks.filter(b => b.completedAtUnixMilliseconds);
    if (phase === "Rest") assert.ok(completed.length > 0);
    const restored = new WorkoutSession(catalog, parseStoredState(JSON.stringify(before)), () => 0.4, () => now + 86_400_000);
    restored.restoreAfterReopen();
    assert.equal(restored.state.activeWorkoutSession.sessionId, before.activeWorkoutSession.sessionId);
    assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId,
      before.keptExerciseRootIdsBySelectionGroupId);
    assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance][1436], -3);
    assert.deepEqual(restored.state.activeWorkoutSession.blocks.filter(b => b.completedAtUnixMilliseconds), completed);
    assert.equal(isCompatibleWithWorkoutModifiers(restored.getSelectedExercise(restored.getNextGroup()), profile), true);
    assert.equal(isCompatibleWithWorkoutModifiers(catalog.find(e => e.id === 1436), profile), false);
  });

  test(`floor expansion preserves an unfinished version-80 ${phase} workout`, () => {
    const now = Date.UTC(2026, 9, 15, 6);
    const original = new WorkoutSession(catalog.filter(e => e.id < 1057),
      createDefaultState(), () => 0.4, () => now);
    original.startWorkout(7, DEFAULT_WORKOUT_MODIFIERS);
    original.recordOutcome(original.getNextGroup(), true);
    const next = original.getNextGroup();
    const id = original.getSelectedExercise(next).id;
    if (phase === "Movement") original.beginMovement(next, 28_000, now + 28_000);
    if (phase === "Rest") {
      original.beginRest(next, now + 14_000);
      original.state.pendingRestMillisecondsRemaining = 14_000;
    }
    original.state.catalogRevision = 80;
    original.state.activeCatalogRevision = 80;
    original.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance] = {15:-3};
    let state = structuredClone(original.state);
    for (let reopen = 1; reopen <= 2; reopen++) {
      const updated = new WorkoutSession(catalog, parseStoredState(JSON.stringify(state)),
        () => 0.1, () => now + reopen * 365 * 86400_000);
      updated.restoreAfterReopen();
      assert.equal(updated.getSelectedExercise(updated.getNextGroup()).id, id);
      assert.equal(updated.state.activeWorkoutSession.sessionId, original.state.activeWorkoutSession.sessionId);
      assert.deepEqual(updated.state.activeWorkoutSession.blocks, original.state.activeWorkoutSession.blocks);
      assert.deepEqual(updated.state.lastKeptExerciseIds, original.state.lastKeptExerciseIds);
      assert.equal(updated.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance][15], -3);
      assert.equal(updated.state.activeWorkoutModifiers & WORKOUT_MODIFIERS.Floor, 0);
      assert.equal(updated.state.lastWorkoutModifiers & WORKOUT_MODIFIERS.Floor, 0);
      assert.equal(updated.state.activeCatalogRevision, 80);
      if (phase === "Movement") assert.equal(updated.state.pendingMovementMillisecondsRemaining, 28_000);
      if (phase === "Rest") assert.equal(updated.state.pendingRestMillisecondsRemaining, 14_000);
      state = structuredClone(updated.state);
    }
  });
}

for (const target of [WORKOUT_MODIFIERS.UpperBodyClothing, WORKOUT_MODIFIERS.Floor,
  WORKOUT_MODIFIERS.Floor | WORKOUT_MODIFIERS.UpperBodyClothing | WORKOUT_MODIFIERS.HardFloor]) {
  for (const movement of [false,true]) test(`physical setup ${target} replaces illegal work and retains progress, moving=${movement}`,()=>{
    const exercises=catalog.map(e=>({...e,score:e.id===1057?10_000:e.score}));
    const now=Date.UTC(2026,9,15,6);
    const session=new WorkoutSession(exercises,createDefaultState(),()=>0.4,()=>now);
    session.startWorkout(3,WORKOUT_MODIFIERS.Floor|WORKOUT_MODIFIERS.UpperBodyClothing);
    assert.ok(session.getActiveGroups().some(g=>session.getSelectedExercise(g).id===1057));
    while(session.getSelectedExercise(session.getNextGroup()).id!==1057){
      const prior=session.getNextGroup();
      if(session.isIntermediateSequenceBlock(prior))session.advanceSequence(prior);
      else session.recordOutcome(prior,true);
    }
    const current=session.getNextGroup();
    if(movement)session.beginMovement(current,28_000,now+28_000);
    const completed=structuredClone(session.state.outcomes);
    const keeps=structuredClone(session.state.lastKeptExerciseIds);
    const id=session.state.activeWorkoutSession.sessionId;
    session.reconfigureActiveWorkout(target,current.id);
    const replacement=session.getSelectedExercise(session.getNextGroup());
    assert.notEqual(replacement.id,1057);
    assert.equal(isCompatibleWithWorkoutModifiers(replacement,target),true);
    assert.equal(session.state.activeWorkoutSession.sessionId,id);
    assert.deepEqual(session.state.lastKeptExerciseIds,keeps);
    for(const [key,value] of Object.entries(completed))assert.equal(session.state.outcomes[key],value);
  });
}
