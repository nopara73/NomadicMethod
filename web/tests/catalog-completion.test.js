import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  ACCEPTED_COVERAGE_EXCEPTIONS,
  CURRENT_CATALOG_REVISION,
  RESOLUTIONS,
  WORKOUT_MODIFIERS,
  WorkoutSession,
  createDefaultState,
  getSelectionKey,
  getSessionMovementId,
  getMaximumDistinctLineupSize,
  getMaximumCompleteLineupSize,
  isCompatibleWithWorkoutModifiers,
  isSelectable,
  isSelectionGroupAvailable,
} from "../workout.js";

const catalog = JSON.parse(await readFile(
  new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));

for (const [minutes, profile] of [
  [15, WORKOUT_MODIFIERS.Insect],
  [15, WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor],
  [20, WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor],
  [30, WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor],
  [30, WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence],
  [30, WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Shy],
  [60, WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor | WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Shy],
]) test(`accepted gaps preserve and complete all ${minutes} minutes, profile=${profile}`, () => {
  for (const light of [false, true]) {
    const modifiers = profile | (light ? WORKOUT_MODIFIERS.Light : 0);
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0.4);
    session.startWorkout(minutes, modifiers);
    const rounds = session.getActiveGroups();
    assert.equal(rounds.length, minutes);
    for (const round of rounds) {
      assert.equal(isSelectionGroupAvailable(round, modifiers), true);
      const selected = session.getSelectedExercise(round);
      assert.equal(isCompatibleWithWorkoutModifiers(selected, modifiers), true);
      assert.equal(isSelectable(selected, round), true);
      session.beginRest(round, Date.now() + 15_000);
      if (session.isIntermediateSequenceBlock(round)) session.advanceSequence(round);
      else session.recordOutcome(round, true);
      session.clearPendingRest();
    }
    assert.equal(session.state.workoutCompleted, true);
  }
});

test("accepted gaps apply only to their declared modifier combinations", () => {
  for (const { groupId, requiredModifiers } of ACCEPTED_COVERAGE_EXCEPTIONS) {
    const minutes = Number(groupId.split(".")[0].slice(1));
    const group = RESOLUTIONS.get(minutes).groups.find((item) => item.id === groupId);
    assert.equal(isSelectionGroupAvailable(group, requiredModifiers), false);
    assert.equal(isSelectionGroupAvailable(group, requiredModifiers & ~WORKOUT_MODIFIERS.Insect), true);
    assert.equal(isSelectionGroupAvailable(group, WORKOUT_MODIFIERS.None), true);
    if (requiredModifiers !== WORKOUT_MODIFIERS.Insect)
      assert.equal(isSelectionGroupAvailable(group, WORKOUT_MODIFIERS.Insect), true);
  }
});

for (const [minutes, insect, expectedId, selectionKey] of [
  [10, true, 1029, "r10.anterior-lateral-lower-leg-dorsal-foot"],
  [20, false, 1028, "r20.accessory-hip-adductors"],
  [30, false, 1028, "r30.accessory-hip-adductors"],
]) for (const light of [false, true]) {
  test(`reviewed heel digs and chair squeeze complete ${minutes}-minute Hard Floor workout, Light=${light}`, () => {
    for (const randomValue of [0.01, 0.2, 0.4]) {
      const state = createDefaultState();
      state.scores[expectedId] = 10000;
      const profile = WORKOUT_MODIFIERS.HardFloor | WORKOUT_MODIFIERS.Silence |
        WORKOUT_MODIFIERS.Shy | WORKOUT_MODIFIERS.UpperBodyClothing |
        (insect ? WORKOUT_MODIFIERS.Insect : WORKOUT_MODIFIERS.None) |
        (light ? WORKOUT_MODIFIERS.Light : WORKOUT_MODIFIERS.None);
      const session = new WorkoutSession(catalog, state, () => randomValue);
      session.startWorkout(minutes, profile);
      const rounds = session.getActiveGroups();
      const target = rounds.find((round) => getSelectionKey(round) === selectionKey);
      assert.equal(session.getSelectedExercise(target).id, expectedId);
      assert.ok(rounds.every((round) => isCompatibleWithWorkoutModifiers(
        session.getSelectedExercise(round), profile)));
      const baseRounds = rounds.filter((round) => (round.sequenceBlockIndex ?? 0) === 0);
      assert.equal(new Set(baseRounds.map((round) => getSessionMovementId(
        session.getSelectedExercise(round)))).size, baseRounds.length);
      for (const round of rounds) {
        session.beginRest(round, Date.now() + 15_000);
        if (session.isIntermediateSequenceBlock(round)) session.advanceSequence(round);
        else session.recordOutcome(round, true);
        session.clearPendingRest();
      }
      assert.equal(session.state.workoutCompleted, true);
    }
  });
}

for (const light of [false, true]) {
  test(`reverse rowing closes the distinct elbow slot in an Insect workout, Light=${light}`, () => {
    const row = catalog.find((exercise) => exercise.id === 1030);
    assert.equal(row.primaryCanonicalGroup, "ElbowFlexors");
    assert.deepEqual(row.secondaryCanonicalGroups, []);
    assert.equal(row.muscularDemand, 0);
    assert.equal(row.sequenceBlocks.length, 1);
    assert.equal(isCompatibleWithWorkoutModifiers(row, WORKOUT_MODIFIERS.Insect |
      WORKOUT_MODIFIERS.HardFloor | WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Shy), true);
    assert.equal(isSelectable(row, RESOLUTIONS.get(30).groups.find((group) =>
      group.id === "r30.elbow-flexors")), true);
    assert.equal(isSelectable(row, RESOLUTIONS.get(3).groups.find((group) =>
      group.id === "r3.head-neck-upper-limbs")), false);
    for (const randomValue of [0.01, 0.2, 0.4]) {
      const profile = WORKOUT_MODIFIERS.Insect | (light ? WORKOUT_MODIFIERS.Light : 0);
      const session = new WorkoutSession(catalog, createDefaultState(), () => randomValue);
      session.startWorkout(30, profile);
      const rounds = session.getActiveGroups();
      assert.equal(session.getSelectedExercise(rounds.find((round) =>
        getSelectionKey(round) === "r30.elbow-flexors")).id, 1030);
      const roots = rounds.filter((round) => (round.sequenceBlockIndex ?? 0) === 0);
      assert.equal(new Set(roots.map((round) => getSessionMovementId(
        session.getSelectedExercise(round)))).size, roots.length);
      assert.ok(rounds.every((round) => isCompatibleWithWorkoutModifiers(
        session.getSelectedExercise(round), profile)));
      for (const round of rounds) {
        session.beginRest(round, Date.now() + 15_000);
        if (session.isIntermediateSequenceBlock(round)) session.advanceSequence(round);
        else session.recordOutcome(round, true);
        session.clearPendingRest();
      }
      assert.equal(session.state.workoutCompleted, true);
    }
  });
}

for (const light of [false, true]) {
  test(`chair-squat squeeze supplies Hard Floor/Insect adductor slots without duplicate family, Light=${light}`, () => {
    const chair = catalog.find((exercise) => exercise.id === 1031);
    assert.equal(getSessionMovementId(chair), 969);
    assert.equal(chair.sequenceBlocks.length, 1);
    const profile = WORKOUT_MODIFIERS.HardFloor | WORKOUT_MODIFIERS.Insect |
      WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Shy | WORKOUT_MODIFIERS.UpperBodyClothing |
      (light ? WORKOUT_MODIFIERS.Light : 0);
    assert.equal(isCompatibleWithWorkoutModifiers(chair, profile), true);
    for (const minutes of [20, 30]) assert.equal(isSelectable(chair,
      RESOLUTIONS.get(minutes).groups.find((group) =>
        group.id === `r${minutes}.accessory-hip-adductors`)), true);
    assert.equal(isCompatibleWithWorkoutModifiers(catalog.find((exercise) => exercise.id === 1028), profile), false);
    for (const randomValue of [0.01, 0.2, 0.4]) {
      const state = createDefaultState();
      state.scores[1031] = 10000;
      const session = new WorkoutSession(catalog, state, () => randomValue);
      session.startWorkout(10, profile);
      const rounds = session.getActiveGroups();
      assert.ok(rounds.some((round) => session.getSelectedExercise(round).id === 1031));
      const roots = rounds.filter((round) => (round.sequenceBlockIndex ?? 0) === 0);
      assert.equal(new Set(roots.map((round) => getSessionMovementId(
        session.getSelectedExercise(round)))).size, roots.length);
      assert.ok(rounds.every((round) => isCompatibleWithWorkoutModifiers(
        session.getSelectedExercise(round), profile)));
      for (const round of rounds) {
        session.beginRest(round, Date.now() + 15_000);
        if (session.isIntermediateSequenceBlock(round)) session.advanceSequence(round);
        else session.recordOutcome(round, true);
        session.clearPendingRest();
      }
      assert.equal(session.state.workoutCompleted, true);
    }
  });
}

for (const light of [false, true]) {
  test(`spinal wave supplies Hard Floor/Insect forearm slots and completes a unique lineup, Light=${light}`, () => {
    const wave = catalog.find((exercise) => exercise.id === 1032);
    const profile = WORKOUT_MODIFIERS.HardFloor | WORKOUT_MODIFIERS.Insect |
      WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Shy | WORKOUT_MODIFIERS.UpperBodyClothing |
      (light ? WORKOUT_MODIFIERS.Light : 0);
    assert.equal(isCompatibleWithWorkoutModifiers(wave, profile), true);
    for (const [minutes, key] of [
      [15, "r15.arm-forearm-hand"], [20, "r20.forearm-hand"],
      [30, "r30.forearm-flexors-pronators"], [30, "r30.forearm-extensors-supinators"],
    ]) assert.equal(isSelectable(wave,
      RESOLUTIONS.get(minutes).groups.find((group) => group.id === key)), true);
    assert.notEqual(getSessionMovementId(wave), 1026);
    for (const randomValue of [0.01, 0.2, 0.4]) {
      const state = createDefaultState();
      state.scores[1032] = 10000;
      const session = new WorkoutSession(catalog, state, () => randomValue);
      session.startWorkout(10, profile);
      const rounds = session.getActiveGroups();
      assert.ok(rounds.some((round) => session.getSelectedExercise(round).id === 1032));
      const roots = rounds.filter((round) => (round.sequenceBlockIndex ?? 0) === 0);
      assert.equal(new Set(roots.map((round) => getSessionMovementId(
        session.getSelectedExercise(round)))).size, roots.length);
      assert.ok(rounds.every((round) => isCompatibleWithWorkoutModifiers(
        session.getSelectedExercise(round), profile)));
      for (const round of rounds) {
        session.beginRest(round, Date.now() + 15_000);
        if (session.isIntermediateSequenceBlock(round)) session.advanceSequence(round);
        else session.recordOutcome(round, true);
        session.clearPendingRest();
      }
      assert.equal(session.state.workoutCompleted, true);
    }
  });
}

test("new direct movements preserve existing anatomy, sequence and feedback", () => {
  const chair = catalog.find((exercise) => exercise.id === 1028);
  const heel = catalog.find((exercise) => exercise.id === 1029);
  assert.equal(chair.primaryCanonicalGroup, "MedialAndDeepKneeExtensors");
  assert.ok(chair.secondaryCanonicalGroups.includes("AccessoryHipAdductors"));
  assert.equal(chair.muscularDemand, 2);
  assert.equal(getSessionMovementId(chair), 969);
  assert.equal(getSessionMovementId(catalog.find((exercise) => exercise.id === 969)), 969);
  assert.deepEqual(catalog.find((exercise) => exercise.id === 784).sequenceBlocks
    .map((block) => block.exerciseId), [784, 969, 1000]);
  assert.equal(heel.primaryCanonicalGroup, "HipFlexors");
  assert.deepEqual(heel.secondaryCanonicalGroups, ["AnteriorLateralLowerLegAndDorsalFoot"]);
  assert.equal(heel.muscularDemand, 1);
  assert.equal(heel.sideSequence, "Alternating");
  assert.equal(chair.sequenceBlocks.length, 1);
  assert.equal(heel.sequenceBlocks.length, 1);
  assert.equal(isCompatibleWithWorkoutModifiers(chair, WORKOUT_MODIFIERS.Insect), false);
  assert.equal(isCompatibleWithWorkoutModifiers(catalog.find((exercise) => exercise.id === 194),
    WORKOUT_MODIFIERS.HardFloor), false);
  const state = createDefaultState();
  state.catalogRevision = 75;
  const oldCatalog = catalog.filter((exercise) => ![1028, 1029].includes(exercise.id));
  state.scores = Object.fromEntries(oldCatalog.map((exercise) => [exercise.id, exercise.id % 41 - 20]));
  state.catalogIdentities = Object.fromEntries(oldCatalog.map((exercise) =>
    [exercise.id, `${exercise.name}\u001f${exercise.video}`]));
  state.keptExerciseRootIdsBySelectionGroupId = { "r30.rotator-cuff": [1026] };
  state.lastKeptExerciseIds = [1026];
  const saved = structuredClone(state);
  const session = new WorkoutSession(catalog, state, () => 0);
  session.initialize();
  for (const [id, score] of Object.entries(saved.scores)) assert.equal(session.state.scores[id], score);
  assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId, saved.keptExerciseRootIdsBySelectionGroupId);
  assert.deepEqual(session.state.lastKeptExerciseIds, saved.lastKeptExerciseIds);
});

for (const exerciseId of [480, 517]) {
  test(`Breath of Joy ${exerciseId} Insect correction preserves identity and feedback`, () => {
    const exercise = catalog.find((item) => item.id === exerciseId);
    const breathing = RESOLUTIONS.get(30).groups.find((group) => group.id === "r30.breathing-muscles");
    assert.equal(isCompatibleWithWorkoutModifiers(exercise,
      WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor), true);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise,
      WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence), false);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise,
      WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Shy), false);
    assert.equal(isSelectable(exercise, breathing), true);
    assert.equal(exercise.primaryCanonicalGroup, "BreathingMuscles");
    assert.equal(getSessionMovementId(exercise), 480);
    assert.equal(exercise.sequenceBlocks.length, 1);
    assert.equal(exercise.muscularDemand, 0);

    const state = createDefaultState();
    state.catalogRevision = 74;
    state.scores = Object.fromEntries(catalog.map((item) => [item.id, item.id % 41 - 20]));
    state.catalogIdentities = Object.fromEntries(catalog
      .map((item) => [item.id, `${item.name}\u001f${item.video}`]));
    state.selectedExerciseIds = { [getSelectionKey(breathing)]: exerciseId };
    state.keptExerciseRootIdsBySelectionGroupId = { [getSelectionKey(breathing)]: [exerciseId] };
    state.lastKeptExerciseIds = [exerciseId];
    state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = { BreathingMuscles: 123456 };
    const saved = structuredClone(state);
    const session = new WorkoutSession(catalog, state, () => 0);
    session.initialize();
    for (const key of ["scores", "keptExerciseRootIdsBySelectionGroupId", "lastKeptExerciseIds",
      "lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle"])
      assert.deepEqual(session.state[key], saved[key], key);
    assert.equal(session.state.catalogRevision, CURRENT_CATALOG_REVISION);
  });
}

for (const insect of [false, true]) {
  for (const light of [false, true]) {
    for (const shy of [false, true]) {
      test(`short workout keeps broad training: Light=${light}, Shy=${shy}, Insect=${insect}`, () => {
        for (const randomValue of [0.01, 0.2, 0.4]) {
          const state = createDefaultState();
          state.scores[239] = 100;
          const profile = state.lastWorkoutModifiers |
            (light ? WORKOUT_MODIFIERS.Light : WORKOUT_MODIFIERS.None) |
            (shy ? WORKOUT_MODIFIERS.Shy : WORKOUT_MODIFIERS.None) |
            (insect ? WORKOUT_MODIFIERS.Insect : WORKOUT_MODIFIERS.None);
          const session = new WorkoutSession(catalog, state, () => randomValue);
          session.startWorkout(3, profile);

          const rounds = session.getActiveGroups();
          assert.equal(rounds.length, 3);
          assert.equal(new Set(rounds.map((round) =>
            getSessionMovementId(session.getSelectedExercise(round)))).size, 3);
          for (const round of rounds) {
            const selected = session.getSelectedExercise(round);
            assert.equal(isSelectable(selected, round), true);
            assert.equal(isCompatibleWithWorkoutModifiers(selected, profile), true);
            assert.equal(selected.sequenceBlocks.length, 1);
          }
          const upper = rounds.find((round) => getSelectionKey(round) === "r3.head-neck-upper-limbs");
          const selectedUpper = session.getSelectedExercise(upper);
          assert.equal(isSelectable(selectedUpper, upper), true);
          assert.notEqual(selectedUpper.id, 239);
          if (light && !insect) assert.equal(selectedUpper.muscularDemand, 0);
          for (const round of rounds) session.recordOutcome(round, true);
          assert.equal(session.state.workoutCompleted, true);
        }
      });
    }
  }

}

test("compound workout feedback and history survive reload", () => {
  const state = createDefaultState();
  const profile = state.lastWorkoutModifiers | WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Shy;
  const session = new WorkoutSession(catalog, state, () => 0);
  session.initialize();
  session.startWorkout(3, profile);
  const rounds = session.getActiveGroups();
  const upper = rounds.find((round) => getSelectionKey(round) === "r3.head-neck-upper-limbs");
  assert.equal(session.getSelectedExercise(upper).id, 248);
  for (const round of rounds) {
    session.beginRest(round, Date.now() + 15_000);
    assert.equal(session.keepPendingRest(), true);
    session.recordOutcome(round, true);
    session.clearPendingRest();
  }
  assert.equal(session.state.workoutCompleted, true);
  const saved = JSON.parse(JSON.stringify(session.state));
  const resumed = new WorkoutSession(catalog, JSON.parse(JSON.stringify(saved)), () => 0.5);
  resumed.initialize();
  for (const key of ["workoutHistory", "keptExerciseRootIdsBySelectionGroupId", "scores",
    "lastHardWorkUnixMillisecondsByPrimaryMuscle", "lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle",
    "version", "catalogRevision"])
    assert.deepEqual(resumed.state[key], saved[key], key);
  assert.ok(resumed.state.workoutHistory[0].decisions.some((decision) =>
    decision.selectionGroupId === getSelectionKey(upper) &&
    decision.rootExerciseId === 248 && decision.outcome === "tick"));

  // Done applies the recorded decisions to preferences for the next workout.
  resumed.acknowledgeCompletion();
  assert.ok(resumed.state.keptExerciseRootIdsBySelectionGroupId[getSelectionKey(upper)].includes(248));
  const acknowledged = new WorkoutSession(catalog,
    JSON.parse(JSON.stringify(resumed.state)), () => 0.25);
  acknowledged.initialize();
  assert.deepEqual(acknowledged.state.keptExerciseRootIdsBySelectionGroupId,
    resumed.state.keptExerciseRootIdsBySelectionGroupId);
  for (const key of ["workoutHistory", "scores", "lastHardWorkUnixMillisecondsByPrimaryMuscle",
    "lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle"])
    assert.deepEqual(acknowledged.state[key], saved[key], key);
});

test("planted teacup admission preserves existing catalog feedback", () => {
  const state = createDefaultState();
  state.catalogRevision = 73;
  state.scores = Object.fromEntries(catalog.filter((item) => item.id !== 1027)
    .map((item) => [item.id, item.id % 41 - 20]));
  state.catalogIdentities = Object.fromEntries(catalog.filter((item) => item.id !== 1027)
    .map((item) => [item.id, `${item.name}\u001f${item.video}`]));
  state.keptExerciseRootIdsBySelectionGroupId = { "r30.rotator-cuff": [1026] };
  state.lastKeptExerciseIds = [1026];
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = { RotatorCuff: 123456 };
  const scores = structuredClone(state.scores);
  const session = new WorkoutSession(catalog, state, () => 0);
  session.initialize();
  for (const [id, score] of Object.entries(scores))
    assert.equal(session.state.scores[id], score, `score for ${id}`);
  assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId,
    { "r30.rotator-cuff": [1026] });
  assert.deepEqual(session.state.lastKeptExerciseIds, [1026]);
  assert.equal(session.state.lastHardWorkUnixMillisecondsByPrimaryMuscle.RotatorCuff, 123456);
  assert.equal(session.state.catalogRevision, CURRENT_CATALOG_REVISION);
  assert.equal(isCompatibleWithWorkoutModifiers(
    catalog.find((item) => item.id === 1027), WORKOUT_MODIFIERS.Insect), false);
});

test("repeated spinal wave fills two forearm slots without inventing rotator coverage", () => {
  const wave = catalog.filter((item) => item.id === 1032);
  const keys = ["r30.forearm-flexors-pronators", "r30.forearm-extensors-supinators", "r30.rotator-cuff"];
  const groups = keys.map((key) => RESOLUTIONS.get(30).groups.find((group) => group.id === key));
  const profile = WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor;
  assert.equal(getMaximumDistinctLineupSize(wave, groups, profile, 3), 1);
  assert.equal(getMaximumCompleteLineupSize(wave, groups, profile, 3), 2);
  assert.equal(isSelectable(wave[0], groups[2]), false);
});
