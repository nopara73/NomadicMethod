import assert from "node:assert/strict";
import { readFile, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

import {
  ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES,
  APPROVED_EXERCISE_CORRECTIONS,
  DISCARDED_EXERCISE_IDENTITY_NAMES,
  BROAD_COVERAGE_RESOLUTION_MINUTES,
  CURRENT_WORKOUT_STATE_VERSION,
  CURRENT_CATALOG_REVISION,
  EXERCISE_HARD_FLOOR_COMPATIBILITY,
  EXERCISE_SHY_COMPATIBILITY,
  EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT,
  EXERCISE_INSECT_COMPATIBILITY,
  EXERCISE_MIRROR_COVERAGE,
  EXERCISE_MIRROR_RELATIONSHIP,
  RECOVERY_LIGHT_MINIMUM_SHARE_DENOMINATOR,
  RECOVERY_LIGHT_MINIMUM_SHARE_NUMERATOR,
  HARD_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
  HARD_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
  HARD_MUSCULAR_DEMAND,
  HARD_RECOVERY_WINDOW_MS,
  HARD_ROTATION_STATUS,
  LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP,
  LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT,
  MINIMUM_LEGACY_HARD_PRIMARY_MUSCLES,
  MAXIMUM_MUSCULAR_DEMAND,
  MODERATE_MUSCULAR_DEMAND,
  MODERATE_RECOVERY_WINDOW_MS,
  MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS,
  MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS,
  MINIMUM_MUSCULAR_DEMAND,
  MINIMUM_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MINIMUM_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MODERATE_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MODERATE_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR,
  MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR,
  REST_DURATION_MS,
  SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
  SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
  RESOLUTIONS,
  MIRROR_EQUIPMENT,
  WALL_EQUIPMENT,
  WORKOUT_MODIFIER_VALIDATION_PROFILES,
  SUPPORTED_MINUTES,
  WORKOUT_MODIFIERS,
  WORKOUT_EXERCISE_PHASE,
  WorkoutSession,
  calculateCanonicalMuscleLoadEighthUnits,
  calculateMuscleBalanceEvaluation,
  compareMuscleBalanceEvaluations,
  createWorkoutSchedule,
  createDefaultState,
  evaluateRecoveryLightMode,
  findHardFloorCategoryCoverageDeficiencies,
  findWorkoutModifierMaterialityDeficiencies,
  findWorkoutModifierPairCoverageDeficiencies,
  findMuscularDemandCoverageDeficiencies,
  findSoleWallContactRequiredCatalogDeficiencies,
  findWallRequiredCatalogDeficiencies,
  findCompleteWorkoutProfileLineupDeficiencies,
  getCanonicalCoverage,
  getMaximumDistinctLineupSize,
  getMaximumCompleteLineupSize,
  getMirrorEquipment,
  getWallEquipment,
  getEquipmentPreferenceCount,
  getExerciseVideoPath,
  getHoldFramePath,
  getMovementCountdownDurationMs,
  getMovementDurationMs,
  getMovementPhaseState,
  getMovementPresentation,
  getPersistentSetupModifiers,
  getHardRotationStatus,
  getLastHardWorkUnixMilliseconds,
  getLastMeaningfulWorkUnixMilliseconds,
  getWorkoutsUntilLightWorkout,
  getMuscularDemandSchedulePriority,
  getMinimumExercisesPerModifierPairStatePerGroup,
  getSelectionKey,
  getSequenceMuscularDemand,
  getSessionMovementId,
  getWorkoutBlockAccent,
  getWorkoutDisplayProgress,
  getWorkoutExecutionTimeline,
  getWorkoutExercisePhase,
  hasRepeatedSets,
  hasReviewedMuscularDemand,
  isModerateExerciseRecovering,
  isPrimaryMuscleInModerateRecovery,
  isPrimaryMuscleRecovering,
  isSelectable,
  isSelectableForWorkoutProfile,
  isSelectionGroupAvailable,
  isFinalSequenceRound,
  isSequenceContinuationRound,
  isSequenceRound,
  isSessionMovementMetadataValid,
  isCompatibleWithWorkoutModifiers,
  isLightWorkoutDayDue,
  inferLegacyCompletedTrainingDays,
  isModifierMetadataComplete,
  isMirrorPreferred,
  isWallPreferred,
  normalizeWorkoutModifiers,
  normalizeMinutes,
  parseStoredState,
  withMirrorEquipment,
  withWallEquipment,
} from "../workout.js";

test("exercise phases use inclusive 15- and 45-block boundaries", () => {
  assert.equal(getWorkoutExercisePhase(1), WORKOUT_EXERCISE_PHASE.Warmup);
  assert.equal(getWorkoutExercisePhase(15), WORKOUT_EXERCISE_PHASE.Warmup);
  assert.equal(getWorkoutExercisePhase(16),
    WORKOUT_EXERCISE_PHASE.PeakPerformance);
  assert.equal(getWorkoutExercisePhase(45),
    WORKOUT_EXERCISE_PHASE.PeakPerformance);
  assert.equal(getWorkoutExercisePhase(46), WORKOUT_EXERCISE_PHASE.Fatigued);
  assert.equal(getWorkoutExercisePhase(90), WORKOUT_EXERCISE_PHASE.Fatigued);
});

test("version nineteen slot downvotes migrate from logs into workout phases", () => {
  const group = RESOLUTIONS.get(3).groups[0];
  const root = exercise(
    101,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    4,
  );
  const state = createDefaultState();
  state.version = 19;
  state.catalogRevision = CURRENT_CATALOG_REVISION;
  state.exerciseScoreAdjustmentsBySelectionGroupId = {
    [group.id]: { [root.id]: -3 },
  };
  const decisionSession = (sessionId, order, outcome) => {
    const timestamp = 1_777_000_000_000 + sessionId * 100_000;
    return {
      sessionId,
      startedAtUnixMilliseconds: timestamp - 60_000,
      endedAtUnixMilliseconds: timestamp + 1,
      workoutMinutes: order,
      status: "Completed",
      blocks: [{
        completedAtUnixMilliseconds: timestamp,
        selectionGroupId: group.id,
        order,
        rootExerciseId: root.id,
        exerciseId: root.id,
      }],
      decisions: [{
        decidedAtUnixMilliseconds: timestamp + 1,
        selectionGroupId: group.id,
        rootExerciseId: root.id,
        outcome,
      }],
    };
  };
  const shuffleTimestamp = 1_777_000_200_000;
  state.workoutHistory = [
    decisionSession(1, 1, "tick"),
    {
      sessionId: 2,
      startedAtUnixMilliseconds: shuffleTimestamp - 60_000,
      endedAtUnixMilliseconds: shuffleTimestamp + 1,
      workoutMinutes: 3,
      status: "Interrupted",
      selectionChanges: [{
        changedAtUnixMilliseconds: shuffleTimestamp,
        selectionGroupId: group.id,
        rejectedRootExerciseId: root.id,
      }],
    },
    decisionSession(3, 16, "x"),
    decisionSession(4, 46, "x"),
  ];

  const session = new WorkoutSession([root], state, () => 0);
  session.initialize();

  assert.equal(session.state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.deepEqual(session.state.exerciseScoreAdjustmentsBySelectionGroupId, {});
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.Warmup][root.id], -1);
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance][root.id], -1);
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.Fatigued][root.id], -1);
  assert.equal(session.state.keptExerciseRootIdsBySelectionGroupId[group.id]
    .includes(root.id), true);
  assert.equal(session.state.lastKeptExerciseIds.includes(root.id), true);
  assert.equal(session.getScore(root), 4);

  const restored = parseStoredState(JSON.stringify(session.state));
  assert.equal(restored.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.Warmup][root.id], -1);
});

test("pre-slot history restores keeps without inventing phase downvotes", () => {
  const group = RESOLUTIONS.get(3).groups[0];
  const root = exercise(
    101,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    -2,
  );
  const state = createDefaultState();
  state.version = 18;
  state.catalogRevision = CURRENT_CATALOG_REVISION;
  state.workoutHistory = ["tick", "x"].map((outcome, index) => {
    const timestamp = 1_777_000_000_000 + index * 100_000;
    return {
      sessionId: index + 1,
      startedAtUnixMilliseconds: timestamp - 60_000,
      endedAtUnixMilliseconds: timestamp + 1,
      workoutMinutes: 3,
      status: "Completed",
      blocks: [{
        completedAtUnixMilliseconds: timestamp,
        selectionGroupId: group.id,
        order: index === 0 ? 1 : 16,
        rootExerciseId: root.id,
        exerciseId: root.id,
      }],
      decisions: [{
        decidedAtUnixMilliseconds: timestamp + 1,
        selectionGroupId: group.id,
        rootExerciseId: root.id,
        outcome,
      }],
    };
  });

  const session = new WorkoutSession([root], state, () => 0);
  session.initialize();

  assert.equal(session.state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(session.state.keptExerciseRootIdsBySelectionGroupId[group.id]
    .includes(root.id), true);
  assert.equal(session.state.lastKeptExerciseIds.includes(root.id), true);
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, {});
  assert.equal(session.getScore(root), -2);
});

test("exercise sequences and repeated sets are distinct presentation states", () => {
  const cases = [
    [{ sequenceBlockCount: 1, setCount: 1 }, false, false],
    [{ sequenceBlockCount: 2, setCount: 1 }, true, false],
    [{ sequenceBlockCount: 1, setCount: 2 }, false, true],
    [{ sequenceBlockCount: 3, setCount: 2 }, true, true],
  ];

  for (const [group, expectedSequence, expectedRepeatedSets] of cases) {
    assert.equal(isSequenceRound(group), expectedSequence);
    assert.equal(hasRepeatedSets(group), expectedRepeatedSets);
  }
});

test("display progress counts each logical selection once", () => {
  const groups = [
    { id: "punch.set1.block1", order: 1, selectionGroupId: "punch" },
    { id: "punch.set1.block2", order: 2, selectionGroupId: "punch" },
    { id: "punch.set2.block1", order: 3, selectionGroupId: "punch" },
    { id: "punch.set2.block2", order: 4, selectionGroupId: "punch" },
    { id: "squat", order: 5 },
  ];

  assert.deepEqual(
    getWorkoutDisplayProgress(groups, groups[0]),
    { position: 1, total: 2 },
  );
  assert.deepEqual(
    getWorkoutDisplayProgress(groups, groups[3]),
    { position: 1, total: 2 },
  );
  assert.deepEqual(
    getWorkoutDisplayProgress(groups, groups[4]),
    { position: 2, total: 2 },
  );
});

test("execution timeline contains only real work blocks", () => {
  const groups = [
    {
      id: "punch.set1.block1",
      order: 1,
      selectionGroupId: "punch",
      setNumber: 1,
      sequenceSideCue: "ScreenRight",
    },
    {
      id: "punch.set1.block2",
      order: 2,
      selectionGroupId: "punch",
      setNumber: 1,
      sequenceSideCue: "ScreenLeft",
    },
    {
      id: "punch.set2.block1",
      order: 3,
      selectionGroupId: "punch",
      setNumber: 2,
      sequenceSideCue: "ScreenRight",
    },
    {
      id: "punch.set2.block2",
      order: 4,
      selectionGroupId: "punch",
      setNumber: 2,
      sequenceSideCue: "ScreenLeft",
    },
  ];

  assert.deepEqual(
    getWorkoutExecutionTimeline(groups, groups[2]),
    {
      blocks: ["blue", "red", "blue", "red"],
      setStartBlockIndices: [0, 2],
      currentBlockIndex: 2,
    },
  );
  assert.deepEqual(
    getWorkoutExecutionTimeline(groups, groups[0], true),
    {
      blocks: ["blue", "red", "blue", "red"],
      setStartBlockIndices: [0, 2],
      currentBlockIndex: 1,
    },
  );
});

test("three distinct uncued exercises use the three-phase palette", () => {
  const groups = [1, 2].flatMap((setNumber) => [
    {
      id: `circuit.set${setNumber}.block1`,
      order: (setNumber - 1) * 3 + 1,
      selectionGroupId: "circuit",
      sequenceBlockCount: 3,
      sequenceBlockIndex: 0,
      setNumber,
      exerciseOverrideId: 101,
    },
    {
      id: `circuit.set${setNumber}.block2`,
      order: (setNumber - 1) * 3 + 2,
      selectionGroupId: "circuit",
      sequenceBlockCount: 3,
      sequenceBlockIndex: 1,
      setNumber,
      exerciseOverrideId: 102,
    },
    {
      id: `circuit.set${setNumber}.block3`,
      order: (setNumber - 1) * 3 + 3,
      selectionGroupId: "circuit",
      sequenceBlockCount: 3,
      sequenceBlockIndex: 2,
      setNumber,
      exerciseOverrideId: 103,
    },
  ]);

  assert.deepEqual(
    getWorkoutExecutionTimeline(groups, groups[4]),
    {
      blocks: ["blue", "neutral", "red", "blue", "neutral", "red"],
      setStartBlockIndices: [0, 3],
      currentBlockIndex: 4,
    },
  );
});

test("three distinct exercises preserve real side and direction cues", () => {
  const groups = [
    {
      id: "circuit.block1",
      order: 1,
      selectionGroupId: "circuit",
      sequenceBlockCount: 3,
      sequenceBlockIndex: 0,
      exerciseOverrideId: 101,
      sequenceSideCue: "ScreenRight",
    },
    {
      id: "circuit.block2",
      order: 2,
      selectionGroupId: "circuit",
      sequenceBlockCount: 3,
      sequenceBlockIndex: 1,
      exerciseOverrideId: 102,
      sequenceSideCue: "ScreenLeft",
    },
    {
      id: "circuit.block3",
      order: 3,
      selectionGroupId: "circuit",
      sequenceBlockCount: 3,
      sequenceBlockIndex: 2,
      exerciseOverrideId: 103,
      sequenceDirectionCue: "Forward",
    },
  ];

  assert.deepEqual(
    getWorkoutExecutionTimeline(groups, groups[1]),
    {
      blocks: ["blue", "red", "blue"],
      setStartBlockIndices: [0],
      currentBlockIndex: 1,
    },
  );
});

test("work-block colors come from the real side and direction cues", () => {
  assert.equal(getWorkoutBlockAccent({}), "neutral");
  assert.equal(
    getWorkoutBlockAccent({ sequenceSideCue: "ScreenRight" }),
    "blue",
  );
  assert.equal(
    getWorkoutBlockAccent({ sequenceSideCue: "ScreenLeft" }),
    "red",
  );
  assert.equal(
    getWorkoutBlockAccent({ sequenceDirectionCue: "Clockwise" }),
    "blue",
  );
  assert.equal(
    getWorkoutBlockAccent({ sequenceDirectionCue: "Counterclockwise" }),
    "red",
  );
});

test("complete workload table counts every primary and distinct secondary", () => {
  const incidental = exercise(1, "HipAbductors", ["GlutealExtensors"], 0);
  const moderate = exercise(2, "AbdominalWall", ["GlutealExtensors"], 0);
  moderate.muscularDemand = MODERATE_MUSCULAR_DEMAND;
  const hard = exercise(
    3,
    "ElbowFlexors",
    ["GlutealExtensors", "GlutealExtensors"],
    0,
  );
  hard.muscularDemand = HARD_MUSCULAR_DEMAND;

  const load = calculateCanonicalMuscleLoadEighthUnits([
    incidental,
    moderate,
    hard,
  ]);

  assert.equal(MINIMUM_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS, 2);
  assert.equal(MINIMUM_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS, 1);
  assert.equal(MODERATE_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS, 4);
  assert.equal(MODERATE_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS, 2);
  assert.equal(HARD_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS, 8);
  assert.equal(HARD_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS, 4);
  assert.equal(load.get("HipAbductors"), 2);
  assert.equal(load.get("AbdominalWall"), 4);
  assert.equal(load.get("ElbowFlexors"), 8);
  assert.equal(load.get("GlutealExtensors"), 7);
});

test("one identity counts once per set and actual repeated sets count again", () => {
  const sideSpecific = exercise(
    1,
    "HipAbductors",
    ["GlutealExtensors"],
    0,
  );
  sideSpecific.sideSequence = "ScreenLeftThenRight";
  sideSpecific.muscularDemand = HARD_MUSCULAR_DEMAND;

  const oneRound = calculateCanonicalMuscleLoadEighthUnits([sideSpecific]);
  const twoRounds = calculateCanonicalMuscleLoadEighthUnits([
    sideSpecific,
    sideSpecific,
  ]);

  assert.equal(oneRound.get("HipAbductors"), 8);
  assert.equal(oneRound.get("GlutealExtensors"), 4);
  assert.equal(twoRounds.get("HipAbductors"), 16);
  assert.equal(twoRounds.get("GlutealExtensors"), 8);
});

test("scheduled load does not double-count side blocks within one set", () => {
  const sideSpecific = exercise(1, "HipAbductors", ["GlutealExtensors"], 0);
  sideSpecific.muscularDemand = HARD_MUSCULAR_DEMAND;
  sideSpecific.sequenceBlocks = [
    { ...sideSpecific.sequenceBlocks[0], sideCue: "ScreenLeft" },
    { ...sideSpecific.sequenceBlocks[0], sideCue: "ScreenRight" },
  ];
  const session = new WorkoutSession([sideSpecific], createDefaultState(), () => 0);
  const placements = [{ anchor: { id: "slot" }, root: sideSpecific }];
  const allocation = (setCount) => ({
    setCountsBySelectionGroupId: new Map([["slot", setCount]]),
  });

  const oneSet = session.calculateScheduledCanonicalLoadEighthUnits(
    placements,
    allocation(1),
  );
  const twoSets = session.calculateScheduledCanonicalLoadEighthUnits(
    placements,
    allocation(2),
  );

  assert.equal(oneSet.get("HipAbductors"), 8);
  assert.equal(oneSet.get("GlutealExtensors"), 4);
  assert.equal(twoSets.get("HipAbductors"), 16);
  assert.equal(twoSets.get("GlutealExtensors"), 8);
});

test("all seven resolutions sum their canonical child loads", () => {
  const load = new Map([
    ["MedialAndDeepKneeExtensors", 8],
    ["PosteriorThighAndKneeFlexors", 4],
  ]);
  const evaluation = calculateMuscleBalanceEvaluation(load);
  const threeMinute = evaluation.resolutions.find(({ minutes }) => minutes === 3);
  const thirtyMinute = evaluation.resolutions.find(({ minutes }) => minutes === 30);

  assert.equal(evaluation.resolutions.length, 7);
  assert.equal(
    threeMinute.loadEighthUnitsByGroupId.get("r3.lower-limbs"),
    12,
  );
  assert.equal(
    thirtyMinute.loadEighthUnitsByGroupId.get("r30.medial-deep-knee-extensors"),
    8,
  );
  assert.equal(
    thirtyMinute.loadEighthUnitsByGroupId.get(
      "r30.posterior-thigh-knee-flexors",
    ),
    4,
  );
});

test("one quarter is the inclusive balance goal at every resolution", () => {
  const resolution = (minutes, weakest, strongest) => ({
    minutes,
    loadEighthUnitsByGroupId: new Map(),
    weakestLoadEighthUnits: weakest,
    strongestLoadEighthUnits: strongest,
    isBalanced: strongest === 0 ||
      weakest * MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR >=
        strongest * MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR,
  });
  const balanced = {
    resolutions: [resolution(3, 2, 8), resolution(5, 4, 8)],
    isBalanced: true,
  };
  const weaker = {
    resolutions: [resolution(3, 1, 8), resolution(5, 4, 8)],
    isBalanced: false,
  };

  assert.equal(MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR, 1);
  assert.equal(MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR, 4);
  assert.ok(compareMuscleBalanceEvaluations(balanced, weaker) > 0);
});

test("muscle balance replaces an unkept equal-score choice", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const targetGroup = groups.find((group) =>
    group.canonicalGroups.includes("ShoulderAbductors"));
  const selected = groups.map((group, index) => exercise(
    1 + index,
    group.canonicalGroups[0],
    [],
    0,
  ));
  const alternative = exercise(
    1_001,
    targetGroup.canonicalGroups[0],
    ["PelvicFloorAndPerineum"],
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 30;
  for (let index = 0; index < groups.length; index += 1) {
    state.selectedExerciseIds[groups[index].id] = selected[index].id;
  }
  const session = new WorkoutSession([...selected, alternative], state, () => 0);

  session.startWorkout(30, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[targetGroup.id], alternative.id);
  assert.deepEqual(session.state.scores, {});
  assert.deepEqual(session.state.exerciseScoreAdjustmentsBySelectionGroupId, {});
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, {});
});

test("muscle balance can use an atomic sequence without splitting its slots", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const shoulderAbductors = groups.find((group) =>
    group.canonicalGroups.includes("ShoulderAbductors"));
  const rotatorCuff = groups.find((group) =>
    group.canonicalGroups.includes("RotatorCuff"));
  const selected = groups.map((group, index) => exercise(
    1 + index,
    group.canonicalGroups[0],
    [],
    0,
  ));
  const root = exercise(
    1_001,
    shoulderAbductors.canonicalGroups[0],
    ["PelvicFloorAndPerineum"],
    0,
  );
  root.sequenceBlocks = [
    { ...root.sequenceBlocks[0] },
    {
      ...root.sequenceBlocks[0],
      exerciseId: 1_002,
    },
  ];
  const member = exercise(
    1_002,
    rotatorCuff.canonicalGroups[0],
    [],
    0,
  );
  member.sequenceBlocks = [];
  const state = createDefaultState();
  state.lastWorkoutMinutes = 30;
  for (let index = 0; index < groups.length; index += 1) {
    state.selectedExerciseIds[groups[index].id] = selected[index].id;
  }
  const session = new WorkoutSession(
    [...selected, root, member],
    state,
    () => 0,
  );

  session.startWorkout(30, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[shoulderAbductors.id], root.id);
  assert.equal(session.state.selectedExerciseIds[rotatorCuff.id], root.id);
  const anchor = [shoulderAbductors, rotatorCuff]
    .sort((left, right) => left.order - right.order)[0];
  const sequenceRounds = session.getActiveGroups().filter((round) =>
    getSelectionKey(round) === anchor.id);
  assert.deepEqual(
    sequenceRounds.map((round) => round.exerciseOverrideId),
    [root.id, member.id],
  );
});

test("muscle balance never moves a selected keep", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const targetGroup = groups.find((group) =>
    group.canonicalGroups.includes("ShoulderAbductors"));
  const selected = groups.map((group, index) => exercise(
    1 + index,
    group.canonicalGroups[0],
    [],
    0,
  ));
  const current = selected.find((item) =>
    item.primaryCanonicalGroup === "ShoulderAbductors");
  const alternative = exercise(
    1_001,
    current.primaryCanonicalGroup,
    ["PelvicFloorAndPerineum"],
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 30;
  for (let index = 0; index < groups.length; index += 1) {
    state.selectedExerciseIds[groups[index].id] = selected[index].id;
  }
  state.keptExerciseRootIdsBySelectionGroupId[targetGroup.id] = [current.id];
  const session = new WorkoutSession([...selected, alternative], state, () => 0);

  session.startWorkout(30, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[targetGroup.id], current.id);
  assert.deepEqual(
    session.state.keptExerciseRootIdsBySelectionGroupId[targetGroup.id],
    [current.id],
  );
});

test("muscle balance never promotes a rejected lower-score exercise", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const targetGroup = groups.find((group) =>
    group.canonicalGroups.includes("ShoulderAbductors"));
  const selected = groups.map((group, index) => exercise(
    1 + index,
    group.canonicalGroups[0],
    [],
    0,
  ));
  const current = selected.find((item) =>
    item.primaryCanonicalGroup === "ShoulderAbductors");
  const rejectedAlternative = exercise(
    1_001,
    current.primaryCanonicalGroup,
    ["PelvicFloorAndPerineum"],
    -1,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 30;
  for (let index = 0; index < groups.length; index += 1) {
    state.selectedExerciseIds[groups[index].id] = selected[index].id;
  }
  const session = new WorkoutSession(
    [...selected, rejectedAlternative],
    state,
    () => 0,
  );

  session.startWorkout(30, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[targetGroup.id], current.id);
});

test("long-workout balance uses the actual repeated-set allocation", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const targetGroup = groups.find((group) =>
    group.canonicalGroups.includes("HipFlexors"));
  const selected = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    [],
    0,
  ));
  const alternative = exercise(
    1_001,
    targetGroup.canonicalGroups[0],
    ["AnteriorLateralLowerLegAndDorsalFoot"],
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 45;
  for (let index = 0; index < groups.length; index += 1) {
    state.selectedExerciseIds[groups[index].id] = selected[index].id;
  }
  const session = new WorkoutSession([...selected, alternative], state, () => 0);

  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[targetGroup.id], alternative.id);
  assert.equal(
    session.getActiveGroups().filter((round) =>
      getSelectionKey(round) === targetGroup.id).length,
    2,
  );
});

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(testDirectory, "..", "..");
const catalog = JSON.parse(
  await readFile(path.join(repositoryRoot, "NomadicMethod", "Assets", "exercises.json"), "utf8"),
);

test("asymmetric demonstrations use atomic mirrored side sequences", () => {
  const expectedById = new Map([
    [483, ["Standing Diagonal Head Turns", "ScreenLeftThenRight",
      "ScreenLeft", "ScreenRight"]],
    [493, ["Diagonal Finger Tracking", "ScreenLeftThenRight",
      "ScreenLeft", "ScreenRight"]],
    [587, ["Isometric Shoulder External Rotation Against Wall",
      "ScreenLeftThenRight", "ScreenLeft", "ScreenRight"]],
  ]);

  for (const [exerciseId, [name, sideSequence, firstCue, secondCue]] of
    expectedById) {
    const exercise = catalog.find((candidate) => candidate.id === exerciseId);
    assert.equal(exercise.name, name);
    assert.equal(exercise.sideSequence, sideSequence);
    assert.deepEqual(
      exercise.sequenceBlocks.map((block) => ({
        exerciseId: block.exerciseId,
        sideCue: block.sideCue,
        mirrorMedia: block.mirrorMedia,
      })),
      [
        { exerciseId, sideCue: firstCue, mirrorMedia: false },
        { exerciseId, sideCue: secondCue, mirrorMedia: true },
      ],
    );
  }
});

test("complete pose determines sides including supporting arms and lead stances", () => {
  const expectedSides = new Map([
    [490, "ScreenLeftThenRight"], [491, "ScreenLeftThenRight"],
    [492, "ScreenLeftThenRight"], [495, "ScreenLeftThenRight"],
    [499, "ScreenLeftThenRight"], [501, "ScreenLeftThenRight"],
    [528, "ScreenLeftLeadThenRightLead"],
    [958, "ScreenLeftThenRight"],
  ]);
  for (const [id, sideSequence] of expectedSides) {
    assert.equal(catalog.find(e => e.id === id).sideSequence, sideSequence);
    const blocks = catalog.flatMap(e => e.sequenceBlocks).filter(b => b.exerciseId === id);
    assert.equal(blocks.length, 2, `complete sides for ${id}`);
    assert.deepEqual(blocks.map(b => b.mirrorMedia), [false, true]);
    assert.notEqual(blocks[0].sideCue, blocks[1].sideCue);
    assert.ok(blocks.every(b => b.sideCue !== "None"));
  }
  for (const id of [32, 216, 237, 238, 255, 307, 310, 398, 520, 522, 562, 740]) {
    assert.ok(["Continuous", "Alternating"].includes(catalog.find(e => e.id === id).sideSequence));
  }
  assert.equal(catalog.find(e => e.id === 958).name, "Standing Overhead Side-Stretch Hold");
});

test("alternating cross-body knee crunch metadata matches its demonstration", () => {
  const exercise = catalog.find((candidate) => candidate.id === 507);
  assert.equal(exercise.name,
    "Alternating Cross-Body Knee-to-Elbow Crunch");
  assert.equal(exercise.sideSequence, "Alternating");
  assert.equal(exercise.primaryCanonicalGroup, "AbdominalWall");
  assert.ok(exercise.secondaryCanonicalGroups.includes("HipFlexors"));
  assert.equal(exercise.secondaryCanonicalGroups.includes("ScapularGirdle"),
    false);
  assert.equal(exercise.sequenceBlocks.length, 1);
  assert.equal(exercise.sessionMovementId, 262);
  assert.equal(getSessionMovementId(exercise), 262);
});

test("canonical training claims require direct primaries and material secondaries", () => {
  const canonicalGroups = new Set(RESOLUTIONS.get(30).groups.flatMap((group) =>
    group.canonicalGroups));
  for (const canonicalGroup of canonicalGroups) {
    assert.ok(catalog.some((item) =>
      item.primaryCanonicalGroup === canonicalGroup));
  }

  const reviewedSecondaryIds = new Set([
    124, 125, 132, 176, 219, 338, 394, 408, 577, 636, 684, 790, 818, 825, 884, 885, 905, 917, 960, 973, 998, 1008, 1010, 1013, 1014, 1018, 1022, 1032,
  ]);
  assert.deepEqual(
    new Set(catalog.filter((item) =>
      item.secondaryCanonicalGroups.includes("AbdominalWall"))
      .map((item) => item.id)),
    reviewedSecondaryIds,
  );
  for (const exerciseId of [266, 287, 591, 603, 701]) {
    assert.equal(catalog.find((item) => item.id === exerciseId)
      .secondaryCanonicalGroups.includes("AbdominalWall"), false);
  }

  const squatObliqueCrunch = catalog.find((item) => item.id === 132);
  assert.equal(squatObliqueCrunch.primaryCanonicalGroup,
    "MedialAndDeepKneeExtensors");
  assert.equal(squatObliqueCrunch.muscularDemand, 2);
  assert.ok(squatObliqueCrunch.secondaryCanonicalGroups.includes(
    "AbdominalWall"));
  const widePlieSideBend = catalog.find((item) => item.id === 905);
  assert.equal(widePlieSideBend.primaryCanonicalGroup, "MedialAndDeepKneeExtensors");
  assert.equal(widePlieSideBend.muscularDemand, 2);
  assert.ok(widePlieSideBend.secondaryCanonicalGroups.includes(
    "AbdominalWall"));
  for (const exerciseId of [910, 948, 954]) {
    assert.equal(catalog.find((item) => item.id === exerciseId)
      .primaryCanonicalGroup, "AbdominalWall");
  }

  assert.equal(catalog.some((item) =>
    item.secondaryCanonicalGroups.includes("PelvicFloorAndPerineum")), false);
  const pelvicFloorSlowSqueeze = catalog.find((item) => item.id === 918);
  assert.equal(pelvicFloorSlowSqueeze.name,
    "Standing Pelvic-Floor Slow Squeeze and Release");
  assert.equal(pelvicFloorSlowSqueeze.primaryCanonicalGroup,
    "PelvicFloorAndPerineum");
  assert.deepEqual(pelvicFloorSlowSqueeze.secondaryCanonicalGroups, []);
  assert.equal(pelvicFloorSlowSqueeze.muscularDemand, 1);
  assert.equal(pelvicFloorSlowSqueeze.silent, true);
  assert.equal(pelvicFloorSlowSqueeze.hardFloorCompatibility,
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible);
  assert.equal(pelvicFloorSlowSqueeze.sideSequence, "Continuous");

  const wallKneeRaise = catalog.find((item) => item.id === 911);
  assert.equal(wallKneeRaise.name,
    "Wall-Supported High-Knee Raise");
  assert.equal(wallKneeRaise.primaryCanonicalGroup, "HipFlexors");
  assert.equal(wallKneeRaise.muscularDemand, 1);
  assert.equal(wallKneeRaise.wallRequired, true);
  assert.equal(wallKneeRaise.hardFloorCompatibility,
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible);
  assert.equal(wallKneeRaise.sideSequence, "ScreenLeftThenRight");

  const wallSitArmRaise = catalog.find((item) => item.id === 913);
  assert.equal(wallSitArmRaise.name, "Shallow Wall Sit with Overhead Arm Raises");
  assert.equal(wallSitArmRaise.primaryCanonicalGroup, "MedialAndDeepKneeExtensors");
  assert.equal(wallSitArmRaise.muscularDemand, 1);
  assert.equal(wallSitArmRaise.wallRequired, true);
  assert.equal(wallSitArmRaise.hardFloorCompatibility,
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible);
  assert.equal(wallSitArmRaise.upperBodyClothingRequirement,
    EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.ClothingRequired);
  assert.equal(wallSitArmRaise.sideSequence, "Continuous");

  const standingPelvicTilt = catalog.find((item) => item.id === 916);
  assert.equal(standingPelvicTilt.name, "Standing Pelvic-Tilt Repetitions");
  assert.equal(standingPelvicTilt.primaryCanonicalGroup, "AbdominalWall");
  assert.deepEqual(new Set(standingPelvicTilt.secondaryCanonicalGroups),
    new Set(["SpinalExtensors"]));
  assert.equal(standingPelvicTilt.muscularDemand, 0);
  assert.equal(standingPelvicTilt.wallRequired, false);
  assert.equal(standingPelvicTilt.hardFloorCompatibility,
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible);
  assert.equal(standingPelvicTilt.mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly);
  assert.equal(standingPelvicTilt.minimumMirrorCoverage,
    EXERCISE_MIRROR_COVERAGE.FullBody);
  assert.equal(standingPelvicTilt.sideSequence, "Continuous");

  const standingSpinalWave = catalog.find((item) => item.id === 917);
  assert.equal(standingSpinalWave.name, "Standing Spinal Wave");
  assert.equal(standingSpinalWave.primaryCanonicalGroup,
    "DeepAndIntersegmentalBack");
  assert.deepEqual(new Set(standingSpinalWave.secondaryCanonicalGroups),
    new Set(["AbdominalWall", "SpinalExtensors"]));
  assert.equal(standingSpinalWave.muscularDemand, 0);
  assert.equal(standingSpinalWave.wallRequired, false);
  assert.equal(standingSpinalWave.hardFloorCompatibility,
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible);
  assert.equal(standingSpinalWave.mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly);
  assert.equal(standingSpinalWave.minimumMirrorCoverage,
    EXERCISE_MIRROR_COVERAGE.UpperBody);
  assert.equal(standingSpinalWave.sideSequence, "Continuous");
});

test("muscular demand is fully reviewed and independent of user scores", () => {
  assert.equal(MINIMUM_MUSCULAR_DEMAND, 0);
  assert.equal(MAXIMUM_MUSCULAR_DEMAND, 2);
  assert.deepEqual(
    [0, 1, 2].map((rating) =>
      catalog.filter((exercise) => exercise.muscularDemand === rating).length),
    [119, 289, 137],
  );
  assert.ok(catalog.every(hasReviewedMuscularDemand));
  assert.ok(catalog.every((exercise) => exercise.score === 0));
  assert.equal(catalog.find((exercise) => exercise.id === 211).muscularDemand, 0);
  assert.equal(catalog.find((exercise) => exercise.id === 264).muscularDemand, 1);
  assert.equal(catalog.find((exercise) => exercise.id === 101).muscularDemand, 2);
  const miniSquatCalfRaise = catalog.find((exercise) => exercise.id === 565);
  assert.equal(miniSquatCalfRaise.name,
    "Mini-Squat Calf Raises with Forward Reach");
  assert.equal(miniSquatCalfRaise.primaryCanonicalGroup, "Soleus");
  assert.equal(miniSquatCalfRaise.muscularDemand, 1);
  assert.equal(
    miniSquatCalfRaise.hardFloorCompatibility,
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible,
  );
  assert.ok(miniSquatCalfRaise.secondaryCanonicalGroups.includes(
    "CalfDeepPosteriorLegAndPlantarFoot"));
  assert.equal(hasReviewedMuscularDemand({ muscularDemand: -1 }), false);
  assert.equal(hasReviewedMuscularDemand({ muscularDemand: 3 }), false);
  assert.equal(hasReviewedMuscularDemand({}), false);

  const exercisesById = new Map(catalog.map((item) => [item.id, item]));
  for (const root of catalog.filter((item) => item.sequenceBlocks.length > 1)) {
    for (const memberId of new Set(root.sequenceBlocks.map((block) =>
      block.exerciseId))) {
      assert.equal(hasReviewedMuscularDemand(exercisesById.get(memberId)), true);
    }
  }
});

test("muscular recovery uses persisted rolling primary-muscle windows", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const group = RESOLUTIONS.get(3).groups[0];
  const primaryMuscle = group.canonicalGroups[0];
  const hard = exercise(1, primaryMuscle, [], 0, undefined, true, 2);
  const moderate = exercise(2, primaryMuscle, [], 0, undefined, true, 1);
  const recovering = {
    [primaryMuscle]: now - HARD_RECOVERY_WINDOW_MS + 1,
  };
  const fresh = {
    [primaryMuscle]: now - HARD_RECOVERY_WINDOW_MS,
  };
  const moderateRecovering = {
    [primaryMuscle]: now - MODERATE_RECOVERY_WINDOW_MS + 1,
  };

  assert.equal(HARD_MUSCULAR_DEMAND, 2);
  assert.equal(MODERATE_MUSCULAR_DEMAND, 1);
  assert.equal(MODERATE_RECOVERY_WINDOW_MS, 18 * 60 * 60 * 1000);
  assert.equal(isPrimaryMuscleRecovering(recovering, primaryMuscle, now), true);
  assert.equal(isPrimaryMuscleRecovering(fresh, primaryMuscle, now), false);
  assert.equal(
    getHardRotationStatus(hard, group, recovering, now),
    HARD_ROTATION_STATUS.RecoveringHard,
  );
  assert.equal(
    getHardRotationStatus(hard, group, fresh, now),
    HARD_ROTATION_STATUS.FreshHard,
  );
  assert.equal(
    getHardRotationStatus(moderate, group, recovering, now),
    HARD_ROTATION_STATUS.Neutral,
  );
  assert.equal(isModerateExerciseRecovering(moderate, moderateRecovering, now), true);
  assert.equal(isModerateExerciseRecovering(hard, moderateRecovering, now), false);
  assert.equal(
    isPrimaryMuscleInModerateRecovery({
      [primaryMuscle]: now - MODERATE_RECOVERY_WINDOW_MS,
    }, primaryMuscle, now),
    false,
  );

  const restored = parseStoredState(JSON.stringify({
    lastKeptExerciseIds: [1, 2],
    lastHardWorkUnixMillisecondsByPrimaryMuscle: {
      [primaryMuscle]: now,
      NotAMuscle: now,
      Chest: "invalid",
    },
    lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle: {
      [primaryMuscle]: now - 1,
      NotAMuscle: now,
      Chest: "invalid",
    },
    // These retired fields must not revive the old exact-exercise rule.
    lastKeptLocalDateByExerciseId: { 1: "2026-08-21" },
    activeRecoveryExcludedExerciseIds: [1],
  }));
  assert.deepEqual(restored.lastKeptExerciseIds, [1, 2]);
  assert.deepEqual(restored.lastHardWorkUnixMillisecondsByPrimaryMuscle, {
    [primaryMuscle]: now,
  });
  assert.deepEqual(restored.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle, {
    [primaryMuscle]: now - 1,
  });
  assert.equal(
    getLastHardWorkUnixMilliseconds(
      restored.lastHardWorkUnixMillisecondsByPrimaryMuscle,
      primaryMuscle,
    ),
    now,
  );
  assert.equal(
    getLastMeaningfulWorkUnixMilliseconds(
      restored.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
      primaryMuscle,
    ),
    now - 1,
  );
  assert.equal("activeRecoveryExcludedExerciseIds" in restored, false);
});

test("recovery-light requires four fifths of selectable muscles to have no fresh demand path", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const muscles = [
    "GlutealExtensors",
    "Chest",
    "ElbowFlexors",
    "Soleus",
    "AbdominalWall",
  ];
  const exercises = muscles.map((muscle, index) => exercise(
    index + 1,
    muscle,
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
    true,
    MODERATE_MUSCULAR_DEMAND,
  ));
  const meaningfulRecovery = Object.fromEntries(muscles.slice(0, 4).map(
    (muscle) => [muscle, now - 60 * 60 * 1000],
  ));

  const active = evaluateRecoveryLightMode(
    exercises,
    WORKOUT_MODIFIERS.None,
    meaningfulRecovery,
    {},
    now,
  );
  delete meaningfulRecovery[muscles[3]];
  const inactive = evaluateRecoveryLightMode(
    exercises,
    WORKOUT_MODIFIERS.None,
    meaningfulRecovery,
    {},
    now,
  );

  assert.equal(RECOVERY_LIGHT_MINIMUM_SHARE_NUMERATOR, 4);
  assert.equal(RECOVERY_LIGHT_MINIMUM_SHARE_DENOMINATOR, 5);
  assert.deepEqual(active, {
    recoveringMuscleCount: 4,
    eligibleMuscleCount: 5,
    isActive: true,
  });
  assert.equal(inactive.recoveringMuscleCount, 3);
  assert.equal(inactive.isActive, false);

  const mixedDemand = [
    exercise(11, muscles[0], [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 1),
    exercise(12, muscles[0], [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 2),
  ];
  assert.equal(evaluateRecoveryLightMode(
    mixedDemand,
    WORKOUT_MODIFIERS.None,
    { [muscles[0]]: now - 60 * 60 * 1000 },
    {},
    now,
  ).isActive, false);
  assert.equal(evaluateRecoveryLightMode(
    mixedDemand,
    WORKOUT_MODIFIERS.None,
    { [muscles[0]]: now - 60 * 60 * 1000 },
    { [muscles[0]]: now - 20 * 60 * 60 * 1000 },
    now,
  ).isActive, true);
});

test("duration inventory and legacy normalization match NomadicMethod", () => {
  assert.deepEqual(SUPPORTED_MINUTES, [3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
  assert.equal(normalizeMinutes(6), 7);
  assert.equal(normalizeMinutes(4), 5);
  assert.equal(normalizeMinutes(37), 30);
  assert.equal(normalizeMinutes(38), 45);
  assert.equal(normalizeMinutes(52), 45);
  assert.equal(normalizeMinutes(53), 60);
  assert.equal(normalizeMinutes(75), 90);
  assert.equal(normalizeMinutes(undefined), 10);
});

test("pre-silence modifier state migrates to clothing, hard floor, and quiet defaults", () => {
  const state = parseStoredState(JSON.stringify({
    version: 4,
    lastWorkoutMinutes: 10,
    activeWorkoutMinutes: 0,
  }));
  assert.equal(
    state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.UpperBodyClothing |
      WORKOUT_MODIFIERS.HardFloor |
      WORKOUT_MODIFIERS.Silence,
  );
  assert.equal(state.activeWorkoutModifiers, WORKOUT_MODIFIERS.None);
});

test("versionless stored state migrates to default modifiers without losing its lineup", () => {
  const state = parseStoredState(JSON.stringify({
    lastWorkoutMinutes: 3,
    activeWorkoutMinutes: 0,
    selectedExerciseIds: { "r3.lower-limbs": 101 },
  }));

  assert.equal(
    state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.UpperBodyClothing |
      WORKOUT_MODIFIERS.HardFloor |
      WORKOUT_MODIFIERS.Silence,
  );
  assert.equal(state.selectedExerciseIds["r3.lower-limbs"], 101);
  assert.equal(state.selectedExerciseIds["p2|r3.lower-limbs"], 101);
  assert.equal(state.selectedExerciseIds["p18|r3.lower-limbs"], 101);
  assert.equal(state.selectedExerciseIds["p146|r3.lower-limbs"], 101);
});

test("fresh workouts use clothing, hard floor, and silence unless explicitly relaxed", () => {
  const session = new WorkoutSession(reviewedInsectCatalog(), createDefaultState(), () => 0);

  session.startWorkout(3);

  assert.equal(
    session.state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.UpperBodyClothing |
      WORKOUT_MODIFIERS.HardFloor |
      WORKOUT_MODIFIERS.Silence,
  );
  assert.equal(
    session.state.activeWorkoutModifiers,
    WORKOUT_MODIFIERS.UpperBodyClothing |
      WORKOUT_MODIFIERS.HardFloor |
      WORKOUT_MODIFIERS.Silence,
  );
  for (const group of session.getActiveGroups()) {
    assert.ok(session.state.selectedExerciseIds[`p146|${getSelectionKey(group)}`]);
  }
});

test("version four active workout migrates to silence without losing progress", () => {
  const exercises = reviewedInsectCatalog();
  const groups = RESOLUTIONS.get(3).groups;
  const selectedExerciseIds = {};
  for (const group of groups) {
    const selected = exercises.find((item) =>
      isSelectableForWorkoutProfile(item, group, WORKOUT_MODIFIERS.Insect));
    selectedExerciseIds[`p1|${getSelectionKey(group)}`] = selected.id;
  }
  const pendingEnd = Date.now() + 60_000;
  const state = parseStoredState(JSON.stringify({
    version: 4,
    lastWorkoutMinutes: 3,
    lastWorkoutModifiers: WORKOUT_MODIFIERS.Insect,
    activeWorkoutMinutes: 3,
    activeWorkoutModifiers: WORKOUT_MODIFIERS.Insect,
    selectedExerciseIds,
    outcomes: { [groups[0].id]: "tick" },
    pendingRestGroupId: groups[1].id,
    pendingRestEndsAtUnixMilliseconds: pendingEnd,
    pendingRestKept: true,
  }));
  const session = new WorkoutSession(exercises, state, () => 0);

  assert.equal(
    session.state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence |
      WORKOUT_MODIFIERS.UpperBodyClothing |
      WORKOUT_MODIFIERS.HardFloor,
  );
  assert.equal(
    session.state.activeWorkoutModifiers,
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence,
  );
  assert.equal(session.state.outcomes[groups[0].id], "tick");
  assert.equal(session.state.pendingRestGroupId, groups[1].id);
  assert.equal(session.state.pendingRestKept, true);
  for (const group of groups) {
    assert.ok(session.state.selectedExerciseIds[`p1|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p3|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p17|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p19|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p129|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p131|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p145|${getSelectionKey(group)}`]);
    assert.ok(session.state.selectedExerciseIds[`p147|${getSelectionKey(group)}`]);
  }
});

test("pre-hard-floor state keeps silence relaxed while adding the hard-floor default", () => {
  const state = parseStoredState(JSON.stringify({
    version: 5,
    lastWorkoutMinutes: 10,
    lastWorkoutModifiers: WORKOUT_MODIFIERS.None,
    activeWorkoutMinutes: 0,
  }));

  assert.equal(state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(
    state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.UpperBodyClothing | WORKOUT_MODIFIERS.HardFloor,
  );
});

test("binary mirror state does not guess mirror height during migration", () => {
  const state = parseStoredState(JSON.stringify({
    version: 8,
    lastWorkoutMinutes: 10,
    lastWorkoutModifiers: WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Mirror,
    activeWorkoutMinutes: 0,
    selectedExerciseIds: {
      "r3.lower-limbs": 101,
      "p4|r3.lower-limbs": 102,
      "p5|r3.lower-limbs": 103,
    },
  }));

  assert.equal(state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(
    state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.Insect |
      WORKOUT_MODIFIERS.UpperBodyClothing |
      WORKOUT_MODIFIERS.HardFloor,
  );
  assert.equal(getMirrorEquipment(state.lastWorkoutModifiers), MIRROR_EQUIPMENT.None);
  assert.equal(state.selectedExerciseIds["r3.lower-limbs"], 101);
  assert.equal(state.selectedExerciseIds["p4|r3.lower-limbs"], undefined);
  assert.equal(state.selectedExerciseIds["p5|r3.lower-limbs"], undefined);
});

test("wall equipment modifier survives current state restoration", () => {
  const state = parseStoredState(JSON.stringify({
    ...createDefaultState(),
    version: CURRENT_WORKOUT_STATE_VERSION,
    lastWorkoutModifiers: WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.Silence,
    activeWorkoutMinutes: 3,
    activeWorkoutModifiers: WORKOUT_MODIFIERS.Wall,
  }));

  assert.equal(
    state.lastWorkoutModifiers,
    WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.Silence,
  );
  assert.equal(state.activeWorkoutModifiers, WORKOUT_MODIFIERS.Wall);
});

test("unreviewed catalog cannot silently treat an enabled modifier as off", () => {
  const exercises = RESOLUTIONS.get(3).groups.map((group, index) =>
    exercise(index + 1, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0));
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  assert.equal(isModifierMetadataComplete(exercises), false);
  assert.throws(
    () => session.startWorkout(3, WORKOUT_MODIFIERS.Insect),
    /No complete exercise lineup/,
  );
});

test("neutral profile includes both compatible and explicitly excluded exercises", () => {
  const compatible = exercise(
    1,
    RESOLUTIONS.get(30).groups[0].canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const excluded = exercise(
    2,
    RESOLUTIONS.get(30).groups[0].canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Incompatible,
  );

  assert.equal(
    isCompatibleWithWorkoutModifiers(compatible, WORKOUT_MODIFIERS.None),
    true,
  );
  assert.equal(
    isCompatibleWithWorkoutModifiers(excluded, WORKOUT_MODIFIERS.None),
    true,
  );
  assert.equal(
    isCompatibleWithWorkoutModifiers(compatible, WORKOUT_MODIFIERS.Insect),
    true,
  );
  assert.equal(
    isCompatibleWithWorkoutModifiers(excluded, WORKOUT_MODIFIERS.Insect),
    false,
  );
});

test("hard floor filters incompatible exercises only while enabled", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const compatible = {
    ...exercise(1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    hardFloorCompatibility: EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible,
  };
  const incompatible = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    hardFloorCompatibility: EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible,
  };

  assert.equal(isCompatibleWithWorkoutModifiers(
    compatible, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    incompatible, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    compatible, WORKOUT_MODIFIERS.HardFloor), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    incompatible, WORKOUT_MODIFIERS.HardFloor), false);
});

test("shy mode filters only reviewed incompatible exercises while enabled", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const compatible = {
    ...exercise(1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    shyCompatibility: EXERCISE_SHY_COMPATIBILITY.Compatible,
  };
  const incompatible = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    shyCompatibility: EXERCISE_SHY_COMPATIBILITY.Incompatible,
  };
  const unreviewed = {
    ...exercise(3, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    shyCompatibility: EXERCISE_SHY_COMPATIBILITY.Unreviewed,
  };

  assert.equal(isCompatibleWithWorkoutModifiers(
    compatible, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    incompatible, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    compatible, WORKOUT_MODIFIERS.Shy), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    incompatible, WORKOUT_MODIFIERS.Shy), false);
  assert.equal(isCompatibleWithWorkoutModifiers(
    unreviewed, WORKOUT_MODIFIERS.Shy), false);
  assert.equal(isModifierMetadataComplete([unreviewed]), false);
});

test("hard floor catalog verdicts include slippery-floor traction", () => {
  assert.equal(catalog.filter((exercise) =>
      exercise.hardFloorCompatibility ===
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible).length, 353);
  assert.equal(catalog.filter((exercise) =>
    exercise.hardFloorCompatibility ===
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible).length, 192);

  for (const exerciseId of [37, 194, 610, 326]) {
    assert.equal(
      catalog.find((exercise) => exercise.id === exerciseId)
        .hardFloorCompatibility,
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible,
    );
  }
  for (const exerciseId of [101, 167, 367, 187, 252, 253, 254, 565, 566, 581, 582, 114, 138, 141, 191, 197, 212, 389, 414, 415, 416, 549, 550, 551, 552, 555, 557, 570, 571]) {
    assert.equal(
      catalog.find((exercise) => exercise.id === exerciseId)
        .hardFloorCompatibility,
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible,
    );
  }

  // Fixed arm shape does not negate visible whole-body displacement.
  for (const id of [103, 110, 120, 184, 971, 986]) {
    assert.equal(catalog.find(exercise => exercise.id === id).insectCompatibility, "Compatible");
  }

  for (const id of [16, 187, 252, 253, 254, 255, 272, 562, 565, 566, 569, 581, 582, 389, 549, 552, 555]) {
    assert.equal(catalog.find(exercise => exercise.id === id).insectCompatibility, "Incompatible");
    assert.equal(catalog.find(exercise => exercise.id === id).hardFloorCompatibility, "Compatible");
  }

  const pogoHeadMovements = new Map([
    [439, "Pogo Bounces with Fixed-Gaze Head Turns"],
    [442, "Pogo Bounces with Fixed-Gaze Head Nods"],
    [444, "Pogo Bounces with Fixed-Gaze Head Tilts"],
  ]);
  for (const [exerciseId, name] of pogoHeadMovements) {
    const item = catalog.find((exercise) => exercise.id === exerciseId);
    assert.equal(item.name, name);
    assert.match(item.motionProfile, /^PogoHead/);
    assert.equal(item.muscularDemand, 1);
    assert.equal(item.silent, false);
    assert.equal(item.insectCompatibility,
      EXERCISE_INSECT_COMPATIBILITY.Compatible);
    assert.equal(item.hardFloorCompatibility,
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible);
    assert.ok(item.secondaryCanonicalGroups.includes(
      "CalfDeepPosteriorLegAndPlantarFoot"));
  }

  const airborneImpactNamePattern =
    /\b(?:jump(?:ing|s)?|hop(?:ping|s)?|pogo|bounce(?:s)?|jack(?:s)?|bound(?:s|ing)?)\b/i;
  const airborneImpactProfilePattern = /(?:Jump|Hop|Pogo|Bounce|Jack|Bound)/;
  const airborneImpactExercises = catalog.filter((exercise) =>
    airborneImpactNamePattern.test(exercise.name) ||
    airborneImpactProfilePattern.test(exercise.motionProfile));
  assert.ok(airborneImpactExercises.length > 0);
  assert.ok(airborneImpactExercises.every((exercise) =>
    exercise.hardFloorCompatibility ===
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible));
});

test("dance and hand filler are replaced by coherent silent upper-body work", () => {
  const expectedNames = new Map([
    [218, "Fingertip Wall Push-Ups"],
    [234, "Standing W Extensions"],
    [237, "Standing Overhead Elbow Extensions"],
    [239, "Back-of-Hands Wrist Stretch"],
    [241, "Isometric Palm Press Hold"],
    [283, "Rear-Hand Palm Strike"],
    [291, "Inward Knife-Hand Strikes"],
    [294, "Rear-Hand Outward Knife-Hand Strike"],
    [556, "Single-Arm Backfist"],
  ]);
  for (const [exerciseId, expectedName] of expectedNames) {
    assert.equal(catalog.find((exercise) => exercise.id === exerciseId)?.name,
      expectedName);
  }
  for (const retiredName of [
    "Cumbia Two-Step",
    "Merengue Six-Count Step",
    "Salsa Front-and-Back Basic",
    "Reggaeton Single-Single-Double Step",
    "Basic Mambo Step",
    "Cha-Cha Basic Step",
    "Bachata Side-to-Side Basic",
    "Five-Position Tendon Glide",
    "Pony Step",
  ]) {
    assert.equal(catalog.some((exercise) => exercise.name === retiredName), false);
  }
  const wallPushUps = catalog.find((exercise) => exercise.id === 218);
  assert.equal(wallPushUps.wallRequired, true);
  assert.equal(wallPushUps.primaryCanonicalGroup, "IntrinsicHand");
  assert.equal(wallPushUps.muscularDemand, 2);
  const knifeHandSequence = catalog.find((exercise) => exercise.id === 291);
  assert.deepEqual(knifeHandSequence.sequenceBlocks.map((block) => block.exerciseId),
    [291, 294, 294]);
  const reviewedSides = new Map([
    [283, "ScreenLeftThenRight"], [291, "Alternating"],
    [294, "ScreenRightThenLeft"], [556, "ScreenLeftThenRight"],
  ]);
  assert.ok([...reviewedSides].every(([exerciseId, sideSequence]) => {
    const exercise = catalog.find((candidate) => candidate.id === exerciseId);
    return exercise.sideSequence === sideSequence &&
      exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly;
  }));
});

test("reduced demand coverage adds only reviewed atomic movements", () => {
  const forward = catalog.find((exercise) => exercise.id === 302);
  const backward = catalog.find((exercise) => exercise.id === 304);
  assert.equal(forward.name, "Marching Forward Arm Circles");
  assert.equal(backward.name, "Marching Backward Arm Circles");
  assert.deepEqual(forward.sequenceBlocks.map((block) => block.exerciseId), [302, 304]);
  assert.equal(forward.muscularDemand, 1);
  assert.equal(backward.muscularDemand, 1);

  const twist = catalog.find((exercise) => exercise.id === 305);
  assert.equal(twist.name, "Standing March with Torso Twist");
  assert.equal(twist.sideSequence, "Alternating");
  assert.equal(twist.muscularDemand, 1);

  const flexion = catalog.find((exercise) => exercise.id === 307);
  assert.deepEqual(flexion.sequenceBlocks.map((block) => block.exerciseId), [307, 310]);
  for (const exerciseId of [307, 308, 309, 310]) {
    const exercise = catalog.find((candidate) => candidate.id === exerciseId);
    assert.equal(exercise.practice, "Self-resistance");
    assert.equal(exercise.mode, "Hold");
    assert.equal(exercise.muscularDemand, 2);
    assert.equal(exercise.insectCompatibility,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible);
    assert.equal(exercise.hardFloorCompatibility,
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible);
  }
  assert.equal(catalog.find((exercise) => exercise.id === 308).sideSequence,
    "ScreenLeftThenRight");
  assert.equal(catalog.find((exercise) => exercise.id === 309).sideSequence,
    "ScreenRightThenLeft");
});

test("silence and insect compose as independent positive requirements", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const quietBug = exercise(
    1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const noisyBug = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    silent: false,
  };
  const quietNoBug = exercise(
    3, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Incompatible,
  );
  const noisyNoBug = {
    ...exercise(4, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Incompatible),
    silent: false,
  };
  const candidates = [quietBug, noisyBug, quietNoBug, noisyNoBug];
  const idsFor = (profile) => candidates
    .filter((item) => isCompatibleWithWorkoutModifiers(item, profile))
    .map((item) => item.id);

  assert.deepEqual(idsFor(WORKOUT_MODIFIERS.None), [1, 2, 3, 4]);
  assert.deepEqual(idsFor(WORKOUT_MODIFIERS.Insect), [1, 2]);
  assert.deepEqual(idsFor(WORKOUT_MODIFIERS.Silence), [1, 3]);
  assert.deepEqual(
    idsFor(WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence),
    [1],
  );
});

test("compact and tall mirrors apply coverage without filtering ordinary exercises", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const mirrorOnly = {
    ...exercise(1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
    equipment: "Mirror",
  };
  const fullBodyMirrorOnly = {
    ...mirrorOnly,
    id: 2,
    name: "Exercise 2",
    video: "exercise_videos/exercise_0002.mp4",
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.FullBody,
  };
  const benefitsGreatly = {
    ...exercise(3, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
  };
  const fullBodyBenefitsGreatly = {
    ...exercise(4, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.FullBody,
  };
  const agnostic = exercise(
    5,
    primary,
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );

  assert.equal(isCompatibleWithWorkoutModifiers(mirrorOnly, WORKOUT_MODIFIERS.None), false);
  assert.equal(isCompatibleWithWorkoutModifiers(benefitsGreatly, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(agnostic, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    fullBodyMirrorOnly, WORKOUT_MODIFIERS.Mirror), false);
  assert.ok([mirrorOnly, benefitsGreatly, fullBodyBenefitsGreatly, agnostic].every((item) =>
    isCompatibleWithWorkoutModifiers(item, WORKOUT_MODIFIERS.Mirror)));
  const tallMirror = withMirrorEquipment(
    WORKOUT_MODIFIERS.None,
    MIRROR_EQUIPMENT.Tall,
  );
  assert.ok([
    mirrorOnly,
    fullBodyMirrorOnly,
    benefitsGreatly,
    fullBodyBenefitsGreatly,
    agnostic,
  ].every((item) => isCompatibleWithWorkoutModifiers(item, tallMirror)));
  assert.equal(isMirrorPreferred(mirrorOnly, WORKOUT_MODIFIERS.Mirror), true);
  assert.equal(isMirrorPreferred(benefitsGreatly, WORKOUT_MODIFIERS.Mirror), true);
  assert.equal(isMirrorPreferred(fullBodyBenefitsGreatly, WORKOUT_MODIFIERS.Mirror), false);
  assert.equal(isMirrorPreferred(fullBodyBenefitsGreatly, tallMirror), true);
  assert.equal(isMirrorPreferred(agnostic, WORKOUT_MODIFIERS.Mirror), false);
});

test("wall equipment states unlock exactly their allowed exercises", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const wallRequired = {
    ...exercise(1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    wallRequired: true,
  };
  const soleWallRequired = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    wallRequired: true,
    soleWallContactRequired: true,
  };
  const ordinary = exercise(
    3,
    primary,
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );

  assert.equal(isCompatibleWithWorkoutModifiers(
    wallRequired, WORKOUT_MODIFIERS.None), false);
  assert.equal(isCompatibleWithWorkoutModifiers(
    soleWallRequired, WORKOUT_MODIFIERS.None), false);
  assert.equal(isCompatibleWithWorkoutModifiers(
    wallRequired, WORKOUT_MODIFIERS.Wall), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    soleWallRequired, WORKOUT_MODIFIERS.Wall), false);
  const solesMayTouch = withWallEquipment(
    WORKOUT_MODIFIERS.None,
    WALL_EQUIPMENT.SolesMayTouch,
  );
  assert.equal(isCompatibleWithWorkoutModifiers(
    wallRequired, solesMayTouch), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    soleWallRequired, solesMayTouch), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    ordinary, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    ordinary, WORKOUT_MODIFIERS.Wall), true);
  assert.equal(isWallPreferred(wallRequired, WORKOUT_MODIFIERS.Wall), true);
  assert.equal(isWallPreferred(soleWallRequired, solesMayTouch), true);
  assert.equal(isWallPreferred(wallRequired, WORKOUT_MODIFIERS.None), false);
  assert.equal(isWallPreferred(ordinary, WORKOUT_MODIFIERS.Wall), false);
});

test("wall and mirror preferences compose without one hiding the other", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const wallAndMirrorRelevant = {
    ...exercise(1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
    wallRequired: true,
  };

  assert.equal(getEquipmentPreferenceCount(
    wallAndMirrorRelevant, WORKOUT_MODIFIERS.None), 0);
  assert.equal(getEquipmentPreferenceCount(
    wallAndMirrorRelevant, WORKOUT_MODIFIERS.Wall), 1);
  assert.equal(getEquipmentPreferenceCount(
    wallAndMirrorRelevant, WORKOUT_MODIFIERS.Mirror), 1);
  assert.equal(getEquipmentPreferenceCount(
    wallAndMirrorRelevant,
    WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.Mirror,
  ), 2);
});

test("wall singleton floor counts distinct session movements only", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const exercises = Array.from(
    { length: MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS },
    (_, index) => ({
      ...exercise(
        index + 1,
        primary,
        [],
        0,
        EXERCISE_INSECT_COMPATIBILITY.Compatible,
      ),
      wallRequired: true,
      sessionMovementId: index + 1,
    }),
  );

  assert.deepEqual(findWallRequiredCatalogDeficiencies(exercises), []);

  exercises.at(-1).sessionMovementId = exercises.at(-2).sessionMovementId;
  assert.deepEqual(findWallRequiredCatalogDeficiencies(exercises), [{
    matchingSessionMovementCount: MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS - 1,
    requiredSessionMovementCount: MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS,
  }]);
});

test("sole-wall floor is separate and counts distinct session movements only", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const exercises = Array.from(
    { length: MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS },
    (_, index) => ({
      ...exercise(
        index + 1,
        primary,
        [],
        0,
        EXERCISE_INSECT_COMPATIBILITY.Compatible,
      ),
      wallRequired: true,
      soleWallContactRequired: true,
      sessionMovementId: index + 1,
    }),
  );

  assert.deepEqual(
    findSoleWallContactRequiredCatalogDeficiencies(exercises),
    [],
  );
  assert.equal(findWallRequiredCatalogDeficiencies(exercises).length, 1);

  exercises.at(-1).sessionMovementId = exercises.at(-2).sessionMovementId;
  assert.deepEqual(
    findSoleWallContactRequiredCatalogDeficiencies(exercises),
    [{
      matchingSessionMovementCount:
        MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS - 1,
      requiredSessionMovementCount:
        MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS,
    }],
  );
});

test("wall equipment round-trips and discards an orphan sole qualifier", () => {
  const context = WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence;
  assert.equal(
    getWallEquipment(WORKOUT_MODIFIERS.SoleWallContact),
    WALL_EQUIPMENT.None,
  );
  assert.equal(
    normalizeWorkoutModifiers(WORKOUT_MODIFIERS.SoleWallContact),
    WORKOUT_MODIFIERS.None,
  );

  const solesStayOff = withWallEquipment(
    context,
    WALL_EQUIPMENT.SolesStayOff,
  );
  assert.equal(getWallEquipment(solesStayOff), WALL_EQUIPMENT.SolesStayOff);
  assert.equal(solesStayOff, context | WORKOUT_MODIFIERS.Wall);

  const solesMayTouch = withWallEquipment(
    solesStayOff,
    WALL_EQUIPMENT.SolesMayTouch,
  );
  assert.equal(getWallEquipment(solesMayTouch), WALL_EQUIPMENT.SolesMayTouch);
  assert.equal(
    solesMayTouch,
    context | WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.SoleWallContact,
  );
  assert.equal(
    withWallEquipment(solesMayTouch, WALL_EQUIPMENT.None),
    context,
  );
});

test("mirror equipment round-trips and discards an orphan tall qualifier", () => {
  const context = WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence;
  assert.equal(getMirrorEquipment(WORKOUT_MODIFIERS.TallMirror), MIRROR_EQUIPMENT.None);
  assert.equal(normalizeWorkoutModifiers(WORKOUT_MODIFIERS.TallMirror), WORKOUT_MODIFIERS.None);

  const compact = withMirrorEquipment(context, MIRROR_EQUIPMENT.Compact);
  assert.equal(getMirrorEquipment(compact), MIRROR_EQUIPMENT.Compact);
  assert.equal(compact, context | WORKOUT_MODIFIERS.Mirror);

  const tall = withMirrorEquipment(compact, MIRROR_EQUIPMENT.Tall);
  assert.equal(getMirrorEquipment(tall), MIRROR_EQUIPMENT.Tall);
  assert.equal(tall, context | WORKOUT_MODIFIERS.Mirror | WORKOUT_MODIFIERS.TallMirror);
  assert.equal(withMirrorEquipment(tall, MIRROR_EQUIPMENT.None), context);
});

test("mirror metadata allows every other relationship cell to be empty", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  for (const [relationship, coverage] of [
    ["Agnostic", "None"], ["MirrorOnly", "UpperBody"], ["MirrorOnly", "FullBody"],
    ["BenefitsGreatly", "UpperBody"], ["BenefitsGreatly", "FullBody"],
  ]) {
    const single = exercise(1, primary, [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 0, relationship, coverage);
    assert.equal(isModifierMetadataComplete([single]), true);
  }
});

test("mirror metadata is complete only when relationship matches equipment", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const agnostic = exercise(
    1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const benefitsGreatly = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.FullBody,
  };
  const mirrorOnly = {
    ...exercise(3, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
    equipment: "Mirror",
  };

  assert.equal(isModifierMetadataComplete([agnostic, benefitsGreatly, mirrorOnly]), true);
  assert.equal(isModifierMetadataComplete([{
    ...mirrorOnly,
    equipment: "None",
  }]), false);
  assert.equal(isModifierMetadataComplete([{
    ...benefitsGreatly,
    equipment: "Mirror",
  }]), false);
  assert.equal(isModifierMetadataComplete([{
    ...agnostic,
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.Unreviewed,
  }]), false);
  assert.equal(isModifierMetadataComplete([{
    ...agnostic,
    soleWallContactRequired: true,
  }]), false);
});

test("mirror relevance breaks score ties but never overrides a real vote", () => {
  const group = RESOLUTIONS.get(30).groups[0];
  const agnostic = exercise(
    1,
    group.canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const benefitsGreatly = {
    ...exercise(
      2,
      group.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
    minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
  };
  const tied = new WorkoutSession([agnostic, benefitsGreatly], createDefaultState(), () => 0);
  assert.equal(tied.chooseBestCandidate(group, new Set(), WORKOUT_MODIFIERS.Mirror).id, 2);

  agnostic.score = 1;
  const voted = new WorkoutSession([agnostic, benefitsGreatly], createDefaultState(), () => 0);
  assert.equal(voted.chooseBestCandidate(group, new Set(), WORKOUT_MODIFIERS.Mirror).id, 1);
});

test("wall relevance breaks score ties but never overrides a real vote", () => {
  const group = RESOLUTIONS.get(30).groups[0];
  const ordinary = exercise(
    1,
    group.canonicalGroups[0],
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );
  const wallRequired = {
    ...exercise(
      2,
      group.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    wallRequired: true,
  };
  const tied = new WorkoutSession([ordinary, wallRequired], createDefaultState(), () => 0);
  assert.equal(tied.chooseBestCandidate(group, new Set(), WORKOUT_MODIFIERS.Wall).id, 2);

  ordinary.score = 1;
  const voted = new WorkoutSession([ordinary, wallRequired], createDefaultState(), () => 0);
  assert.equal(voted.chooseBestCandidate(group, new Set(), WORKOUT_MODIFIERS.Wall).id, 1);
});

test("mid-workout modifier changes replace incompatible current movement and replan future selections", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const ordinary = groups.map((group, index) => exercise(
    22_000 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  ));
  const wallRequired = ordinary.map((source) => {
    const id = source.id + 1;
    return {
      ...source,
      id,
      name: `Exercise ${id}`,
      video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
      wallRequired: true,
      sequenceBlocks: source.sequenceBlocks.map((block) => ({
        ...block,
        exerciseId: id,
      })),
    };
  });
  const exercises = [...ordinary, ...wallRequired];
  const now = 1_800_000_000_000;
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.Wall);
  const completedGroup = session.getNextGroup();
  const completedExercise = session.getSelectedExercise(completedGroup);
  assert.equal(completedExercise.wallRequired, true);
  session.recordOutcome(completedGroup, true);

  const currentGroup = session.getNextGroup();
  const currentExercise = session.getSelectedExercise(currentGroup);
  assert.equal(currentExercise.wallRequired, true);
  session.beginMovement(currentGroup, 28_000, now + 28_000);
  const keptBefore = [...session.state.lastKeptExerciseIds].sort(
    (left, right) => left - right,
  );
  const initialSelectionGroups = session.state.activeWorkoutSession
    .initialSelections.map((selection) => selection.selectionGroupId);

  session.reconfigureActiveWorkout(WORKOUT_MODIFIERS.None, currentGroup.id);

  assert.equal(session.state.activeWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.None);
  const replacementGroup = session.getNextGroup();
  const replacementExercise = session.getSelectedExercise(replacementGroup);
  assert.equal(getSelectionKey(replacementGroup), getSelectionKey(currentGroup));
  assert.notEqual(replacementExercise.id, currentExercise.id);
  assert.equal(replacementExercise.wallRequired, false);
  assert.equal(session.state.pendingMovementGroupId, null);
  assert.equal(session.state.pendingMovementMillisecondsRemaining, 0);
  assert.equal(session.state.outcomes[completedGroup.id], "tick");
  assert.deepEqual(
    [...session.state.lastKeptExerciseIds].sort((left, right) => left - right),
    keptBefore,
  );
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, {});
  assert.equal(
    session.state.selectedExerciseIds[getSelectionKey(completedGroup)],
    completedExercise.id,
  );
  assert.ok(session.getActiveGroups()
    .filter((group) => session.state.outcomes[group.id] === undefined)
    .every((group) => session.getSelectedExercise(group).wallRequired === false));
  assert.deepEqual(session.state.activeSelectionGroupOrder, initialSelectionGroups);

  const change = session.state.activeWorkoutSession.modifierChanges[0];
  assert.equal(session.state.activeWorkoutSession.modifierChanges.length, 1);
  assert.equal(change.previousModifiers, WORKOUT_MODIFIERS.Wall);
  assert.equal(change.newModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(change.protectedSelectionGroupId, "");
  assert.equal(session.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(change.plannedSelections.length, groups.length);
  assert.equal(session.state.activeWorkoutSession.modifiers, WORKOUT_MODIFIERS.Wall);

  const restored = new WorkoutSession(
    exercises,
    parseStoredState(JSON.stringify(session.state)),
    () => 0,
    () => now,
  );
  restored.initialize();
  const restoredCurrent = restored.getNextGroup();
  assert.equal(restoredCurrent.id, replacementGroup.id);
  assert.equal(restored.getSelectedExercise(restoredCurrent).id, replacementExercise.id);
  assert.equal(restored.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(restored.state.pendingMovementGroupId, null);
  assert.equal(restored.state.activeWorkoutSession.modifierChanges.length, 1);

  restored.recordOutcome(restoredCurrent, false);

  assert.equal(restored.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(restored.getSelectedExercise(restored.getNextGroup()).wallRequired, false);
});

test("reenabling mirror restores the current mirror-profile selection", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const ordinary = groups.map((group, index) => exercise(
    22_300 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  ));
  const mirrorOnly = ordinary.map((source) => {
    const id = source.id + 1;
    return {
      ...source,
      id,
      name: `Exercise ${id}`,
      video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
      mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
      minimumMirrorCoverage: EXERCISE_MIRROR_COVERAGE.UpperBody,
      equipment: "Mirror",
      sequenceBlocks: source.sequenceBlocks.map((block) => ({
        ...block,
        exerciseId: id,
      })),
    };
  });
  const now = 1_800_000_000_000;
  const session = new WorkoutSession(
    [...ordinary, ...mirrorOnly],
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.Mirror);
  const initialGroup = session.getNextGroup();
  const initialMirrorExercise = session.getSelectedExercise(initialGroup);
  assert.equal(
    initialMirrorExercise.mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
  );
  session.beginMovement(initialGroup, 28_000, now + 28_000);

  session.reconfigureActiveWorkout(WORKOUT_MODIFIERS.None, initialGroup.id);

  const ordinaryGroup = session.getNextGroup();
  assert.equal(
    session.getSelectedExercise(ordinaryGroup).mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.Agnostic,
  );
  assert.equal(session.state.pendingMovementGroupId, null);

  session.reconfigureActiveWorkout(WORKOUT_MODIFIERS.Mirror, ordinaryGroup.id);

  const restoredGroup = session.getNextGroup();
  const restoredMirrorExercise = session.getSelectedExercise(restoredGroup);
  assert.equal(getSelectionKey(restoredGroup), getSelectionKey(initialGroup));
  assert.equal(restoredMirrorExercise.id, initialMirrorExercise.id);
  assert.equal(
    restoredMirrorExercise.mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly,
  );
  assert.equal(session.state.pendingMovementGroupId, null);
  assert.equal(session.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(session.state.activeWorkoutSession.modifierChanges.length, 2);
});

test("light replans current work and disabling it restores the regular profile", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const hard = groups.map((group, index) => exercise(
    22_600 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  ));
  const easy = groups.map((group, index) => exercise(
    22_601 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  ));
  const now = 1_800_000_000_000;
  const state = createDefaultState();
  state.selectedExerciseIds = Object.fromEntries(groups.map((group, index) => [
    group.id,
    hard[index].id,
  ]));
  state.keptExerciseRootIdsBySelectionGroupId = Object.fromEntries(
    groups.map((group, index) => [group.id, [hard[index].id]]),
  );
  const session = new WorkoutSession(
    [...hard, ...easy],
    state,
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const regularGroup = session.getNextGroup();
  const regularExercise = session.getSelectedExercise(regularGroup);
  assert.equal(regularExercise.muscularDemand, 2);
  session.beginMovement(regularGroup, 28_000, now + 28_000);

  session.reconfigureActiveWorkout(WORKOUT_MODIFIERS.Light, regularGroup.id);

  const lightGroup = session.getNextGroup();
  assert.equal(getSelectionKey(lightGroup), getSelectionKey(regularGroup));
  assert.equal(session.getSelectedExercise(lightGroup).muscularDemand, 0);
  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.activeWorkoutModifiers, WORKOUT_MODIFIERS.Light);
  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.pendingMovementGroupId, null);

  session.reconfigureActiveWorkout(WORKOUT_MODIFIERS.None, lightGroup.id);

  const restoredGroup = session.getNextGroup();
  assert.equal(getSelectionKey(restoredGroup), getSelectionKey(regularGroup));
  assert.equal(session.getSelectedExercise(restoredGroup).id, regularExercise.id);
  assert.equal(session.state.activeWorkoutIsLightDay, false);
  assert.equal(session.state.activeWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.deepEqual(
    session.state.activeWorkoutSession.modifierChanges.map((change) =>
      change.newModifiers),
    [WORKOUT_MODIFIERS.Light, WORKOUT_MODIFIERS.None],
  );
});

test("upper-body clothing excludes only exercises requiring the opposite state", () => {
  const primary = RESOLUTIONS.get(30).groups[0].canonicalGroups[0];
  const clothingRequired = {
    ...exercise(1, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    upperBodyClothingRequirement:
      EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.ClothingRequired,
  };
  const bareRequired = {
    ...exercise(2, primary, [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    upperBodyClothingRequirement:
      EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.BareUpperBodyRequired,
  };
  const agnostic = exercise(
    3,
    primary,
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );

  assert.equal(isCompatibleWithWorkoutModifiers(
    clothingRequired, WORKOUT_MODIFIERS.UpperBodyClothing), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    bareRequired, WORKOUT_MODIFIERS.UpperBodyClothing), false);
  assert.equal(isCompatibleWithWorkoutModifiers(
    agnostic, WORKOUT_MODIFIERS.UpperBodyClothing), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    clothingRequired, WORKOUT_MODIFIERS.None), false);
  assert.equal(isCompatibleWithWorkoutModifiers(
    bareRequired, WORKOUT_MODIFIERS.None), true);
  assert.equal(isCompatibleWithWorkoutModifiers(
    agnostic, WORKOUT_MODIFIERS.None), true);
});

test("compatible modifier transition preserves current movement checkpoint", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const ordinary = groups.map((group, index) => exercise(
    22_500 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  ));
  const wallRequired = ordinary.map((source) => {
    const id = source.id + 1;
    return {
      ...source,
      id,
      name: `Exercise ${id}`,
      video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
      wallRequired: true,
      sequenceBlocks: source.sequenceBlocks.map((block) => ({
        ...block,
        exerciseId: id,
      })),
    };
  });
  const now = 1_800_000_000_000;
  const session = new WorkoutSession(
    [...ordinary, ...wallRequired],
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.Wall);
  const currentGroup = session.getNextGroup();
  const currentExercise = session.getSelectedExercise(currentGroup);
  session.beginMovement(currentGroup, 28_000, now + 28_000);

  session.reconfigureActiveWorkout(
    WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.HardFloor,
    currentGroup.id,
  );

  assert.equal(session.getNextGroup().id, currentGroup.id);
  assert.equal(session.getSelectedExercise(session.getNextGroup()).id, currentExercise.id);
  assert.equal(session.state.pendingMovementGroupId, currentGroup.id);
  assert.equal(session.state.pendingMovementMillisecondsRemaining, 28_000);
  assert.equal(session.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(
    session.state.activeWorkoutSession.modifierChanges[0].protectedSelectionGroupId,
    "",
  );
});

test("initialization replaces legacy protected incompatible movement", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const ordinary = groups.map((group, index) => exercise(
    22_700 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  ));
  const wallRequired = ordinary.map((source) => {
    const id = source.id + 1;
    return {
      ...source,
      id,
      name: `Exercise ${id}`,
      video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
      wallRequired: true,
      sequenceBlocks: source.sequenceBlocks.map((block) => ({
        ...block,
        exerciseId: id,
      })),
    };
  });
  const exercises = [...ordinary, ...wallRequired];
  const now = 1_800_000_000_000;
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.Wall);
  const currentGroup = session.getNextGroup();
  session.beginMovement(currentGroup, 28_000, now + 28_000);

  session.state.activeWorkoutModifiers = WORKOUT_MODIFIERS.None;
  session.state.lastWorkoutModifiers = WORKOUT_MODIFIERS.None;
  for (const [index, group] of groups.entries()) {
    session.state.selectedExerciseIds[group.id] = wallRequired[index].id;
  }
  session.state.activeModifierProtectedSelectionGroupId =
    getSelectionKey(currentGroup);

  const restored = new WorkoutSession(
    exercises,
    parseStoredState(JSON.stringify(session.state)),
    () => 0,
    () => now,
  );
  restored.initialize();

  assert.equal(restored.getSelectedExercise(restored.getNextGroup()).wallRequired, false);
  assert.equal(restored.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(restored.state.pendingMovementGroupId, null);
  assert.equal(restored.state.pendingMovementMillisecondsRemaining, 0);
});

test("initialization clears legacy compatible protection without losing checkpoint", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const ordinary = groups.map((group, index) => exercise(
    22_900 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  ));
  const now = 1_800_000_000_000;
  const session = new WorkoutSession(
    ordinary,
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const currentGroup = session.getNextGroup();
  const currentExercise = session.getSelectedExercise(currentGroup);
  session.beginMovement(currentGroup, 28_000, now + 28_000);

  // Previous builds persisted compatible current work as a protection
  // exception. Removing it must not discard a still-valid checkpoint.
  session.state.activeModifierProtectedSelectionGroupId =
    getSelectionKey(currentGroup);

  const restored = new WorkoutSession(
    ordinary,
    parseStoredState(JSON.stringify(session.state)),
    () => 0,
    () => now,
  );

  restored.initialize();

  assert.equal(restored.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(restored.getNextGroup().id, currentGroup.id);
  assert.equal(
    restored.getSelectedExercise(restored.getNextGroup()).id,
    currentExercise.id,
  );
  assert.equal(restored.state.pendingMovementGroupId, currentGroup.id);
  assert.equal(restored.state.pendingMovementMillisecondsRemaining, 28_000);
});

test("compatible modifier transition preserves repeated selection without protection", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const ordinary = groups.map((group, index) => exercise(
    23_000 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  ));
  const wallRequired = ordinary.map((source) => {
    const id = source.id + 1;
    return {
      ...source,
      id,
      name: `Exercise ${id}`,
      video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
      wallRequired: true,
      sequenceBlocks: source.sequenceBlocks.map((block) => ({
        ...block,
        exerciseId: id,
      })),
    };
  });
  const session = new WorkoutSession(
    [...ordinary, ...wallRequired],
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(45, WORKOUT_MODIFIERS.Wall);
  const initialRounds = session.getActiveGroups();
  const firstRepeatedSet = initialRounds.find((round) =>
    round.setCount > 1 && round.setNumber === 1);
  for (const priorRound of initialRounds) {
    if (priorRound.id === firstRepeatedSet.id) {
      break;
    }
    if (session.isIntermediateSequenceBlock(priorRound)) {
      session.advanceSequence(priorRound);
    } else {
      session.recordOutcome(priorRound, false);
    }
  }
  assert.equal(session.getNextGroup().id, firstRepeatedSet.id);
  const protectedExercise = session.getSelectedExercise(firstRepeatedSet);
  assert.equal(protectedExercise.wallRequired, true);

  session.reconfigureActiveWorkout(
    WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.HardFloor,
    firstRepeatedSet.id,
  );
  session.advanceSequence(firstRepeatedSet);

  const secondSet = session.getNextGroup();
  assert.equal(getSelectionKey(secondSet), getSelectionKey(firstRepeatedSet));
  assert.equal(secondSet.setNumber, 2);
  assert.equal(session.state.activeModifierProtectedSelectionGroupId, null);
  assert.equal(session.getSelectedExercise(secondSet).id, protectedExercise.id);

  session.recordOutcome(secondSet, false);

  assert.equal(session.state.activeModifierProtectedSelectionGroupId, null);
});

test("incompatible modifier transition replaces current repeated set", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const ordinary = groups.map((group, index) => exercise(
    23_500 + index * 2,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  ));
  const wallRequired = ordinary.map((source) => {
    const id = source.id + 1;
    return {
      ...source,
      id,
      name: `Exercise ${id}`,
      video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
      wallRequired: true,
      sequenceBlocks: source.sequenceBlocks.map((block) => ({
        ...block,
        exerciseId: id,
      })),
    };
  });
  const session = new WorkoutSession(
    [...ordinary, ...wallRequired],
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(45, WORKOUT_MODIFIERS.Wall);
  const initialRounds = session.getActiveGroups();
  const firstRepeatedSet = initialRounds.find((round) =>
    round.setCount > 1 && round.setNumber === 1);
  for (const priorRound of initialRounds) {
    if (priorRound.id === firstRepeatedSet.id) {
      break;
    }
    if (session.isIntermediateSequenceBlock(priorRound)) {
      session.advanceSequence(priorRound);
    } else {
      session.recordOutcome(priorRound, false);
    }
  }
  const completedBefore = { ...session.state.outcomes };
  session.beginMovement(firstRepeatedSet, 28_000, 1_800_000_028_000);

  session.reconfigureActiveWorkout(
    WORKOUT_MODIFIERS.None,
    firstRepeatedSet.id,
  );

  const replacementRound = session.getNextGroup();
  assert.equal(getSelectionKey(replacementRound), getSelectionKey(firstRepeatedSet));
  assert.equal(session.getSelectedExercise(replacementRound).wallRequired, false);
  assert.deepEqual(session.state.outcomes, completedBefore);
  assert.equal(session.state.pendingMovementGroupId, null);
  assert.equal(session.state.activeModifierProtectedSelectionGroupId, null);
});

test("insect transition removes an unfinished structurally unavailable slot", () => {
  const exercises = reviewedInsectCatalog();
  const now = 1_800_000_000_000;
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(30, WORKOUT_MODIFIERS.None);
  const initialRounds = session.getActiveGroups();
  const unavailableRound = initialRounds.find((round) =>
    !isSelectionGroupAvailable(round, WORKOUT_MODIFIERS.Insect));
  assert.ok(unavailableRound);
  for (const priorRound of initialRounds) {
    if (priorRound.id === unavailableRound.id) {
      break;
    }
    session.recordOutcome(priorRound, false);
  }
  assert.equal(session.getNextGroup().id, unavailableRound.id);
  session.beginMovement(unavailableRound, 28_000, now + 28_000);

  session.reconfigureActiveWorkout(
    WORKOUT_MODIFIERS.Insect,
    unavailableRound.id,
  );

  const replannedRounds = session.getActiveGroups();
  assert.equal(replannedRounds.length, 30);
  assert.ok(replannedRounds.every((round) =>
    getSelectionKey(round) !== getSelectionKey(unavailableRound)));
  assert.equal(session.state.pendingMovementGroupId, null);
  assert.ok(replannedRounds
    .filter((round) => session.state.outcomes[round.id] === undefined)
    .every((round) => session.getSelectedExercise(round).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Compatible));
});

test("insect transition retains completed unavailable work across restart", () => {
  const exercises = reviewedInsectCatalog();
  const now = 1_800_000_000_000;
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(30, WORKOUT_MODIFIERS.None);
  const initialRounds = session.getActiveGroups();
  const unavailableRound = initialRounds.find((round, index) =>
    index < initialRounds.length - 1 &&
    !isSelectionGroupAvailable(round, WORKOUT_MODIFIERS.Insect));
  assert.ok(unavailableRound);
  for (const round of initialRounds) {
    session.recordOutcome(round, false);
    if (round.id === unavailableRound.id) {
      break;
    }
  }
  const currentRound = session.getNextGroup();
  assert.ok(currentRound);

  session.reconfigureActiveWorkout(
    WORKOUT_MODIFIERS.Insect,
    currentRound.id,
  );

  assert.ok(session.state.activeModifierRetainedSelectionGroupIds.includes(
    getSelectionKey(unavailableRound)));
  const retainedRound = session.getActiveGroups().find((round) =>
    getSelectionKey(round) === getSelectionKey(unavailableRound));
  assert.ok(retainedRound);
  const retainedExerciseId = session.getSelectedExercise(retainedRound).id;
  const restored = new WorkoutSession(
    exercises,
    parseStoredState(JSON.stringify(session.state)),
    () => 0,
    () => now,
  );

  restored.initialize();

  assert.ok(restored.state.activeModifierRetainedSelectionGroupIds.includes(
    getSelectionKey(unavailableRound)));
  assert.equal(restored.getActiveGroups().length, 30);
  const restoredRetainedRound = restored.getActiveGroups().find((round) =>
    getSelectionKey(round) === getSelectionKey(unavailableRound));
  assert.ok(restoredRetainedRound);
  assert.equal(
    restored.getSelectedExercise(restoredRetainedRound).id,
    retainedExerciseId,
  );
});

test("insect fine availability uses a wall only where direct work exists", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const pelvicFloor = groups.find((group) =>
    group.canonicalGroups.includes("PelvicFloorAndPerineum"));
  const intrinsicHand = groups.find((group) =>
    group.canonicalGroups.includes("IntrinsicHand"));

  assert.equal(isSelectionGroupAvailable(
    pelvicFloor,
    WORKOUT_MODIFIERS.Insect,
  ), false);
  assert.equal(isSelectionGroupAvailable(
    intrinsicHand,
    WORKOUT_MODIFIERS.Insect,
  ), false);
  assert.equal(isSelectionGroupAvailable(
    pelvicFloor,
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Wall,
  ), false);
  assert.equal(isSelectionGroupAvailable(
    intrinsicHand,
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Wall,
  ), true);
  assert.equal(isSelectionGroupAvailable(
    pelvicFloor,
    WORKOUT_MODIFIERS.None,
  ), true);
  assert.equal(isSelectionGroupAvailable(
    intrinsicHand,
    WORKOUT_MODIFIERS.None,
  ), true);
});

test("validation profiles remain pairwise with compact and tall mirror states", () => {
  assert.equal(WORKOUT_MODIFIER_VALIDATION_PROFILES.length, 28);
  assert.equal(
    new Set(WORKOUT_MODIFIER_VALIDATION_PROFILES).size,
    WORKOUT_MODIFIER_VALIDATION_PROFILES.length,
  );
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(WORKOUT_MODIFIERS.None));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(WORKOUT_MODIFIERS.Insect));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(WORKOUT_MODIFIERS.Shy));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(
    WORKOUT_MODIFIERS.UpperBodyClothing));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(WORKOUT_MODIFIERS.Mirror));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.includes(
    WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Mirror |
      WORKOUT_MODIFIERS.TallMirror,
  ));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.every((profile) =>
    (profile & WORKOUT_MODIFIERS.Wall) === 0));
  assert.ok(WORKOUT_MODIFIER_VALIDATION_PROFILES.every((profile) =>
    (profile & WORKOUT_MODIFIERS.Light) === 0));
  assert.equal(
    normalizeWorkoutModifiers(
      WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Light,
    ),
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Light,
  );
  assert.equal(
    getPersistentSetupModifiers(
      WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Light,
    ),
    WORKOUT_MODIFIERS.Insect,
  );
});

test("insect selection is composed with score and coverage instead of post-filtered", () => {
  const exercises = reviewedInsectCatalog();

  const normal = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
  );
  normal.startWorkout(3, WORKOUT_MODIFIERS.None);
  assert.ok(normal.getActiveGroups().every((group) =>
    normal.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Incompatible));

  const insect = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
  );
  insect.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  assert.equal(isModifierMetadataComplete(exercises), true);
  assert.ok(insect.getActiveGroups().every((group) =>
    insect.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Compatible));
});

test("fully reviewed catalog always honors an enabled modifier", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.flatMap((group, index) => [
    exercise(
      index + 1,
      group.canonicalGroups[0],
      group.canonicalGroups.slice(1),
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    exercise(
      101 + index,
      group.canonicalGroups[0],
      group.canonicalGroups.slice(1),
      100,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible,
    ),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);

  assert.ok(session.getActiveGroups().every((group) =>
    session.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Compatible));
});

test("modifier profiles share keeps without forgetting excluded exercises", () => {
  const session = new WorkoutSession(
    reviewedInsectCatalog(),
    createDefaultState(),
    () => 0,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const keptIds = session.getActiveGroups().map((group) =>
    session.getSelectedExercise(group).id);
  for (const group of session.getActiveGroups()) {
    session.recordOutcome(group, true);
  }
  session.acknowledgeCompletion();
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);

  assert.deepEqual([...session.state.lastKeptExerciseIds].sort(), [...keptIds].sort());
  assert.ok(session.getActiveGroups().every((group) =>
    session.getSelectedExercise(group).insectCompatibility ===
      EXERCISE_INSECT_COMPATIBILITY.Compatible));

  session.finishInterruptedWorkout();
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  assert.deepEqual(
    session.getActiveGroups().map((group) => session.getSelectedExercise(group).id).sort(),
    [...keptIds].sort(),
  );
});

test("a warmup downvote reselects an alternative without global exclusion", () => {
  const exercises = RESOLUTIONS.get(3).groups.flatMap((group, index) => [
    exercise(1 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(2 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(3 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  const groups = session.getActiveGroups();
  const rejectedId = session.getSelectedExercise(groups[0]).id;

  session.recordOutcome(groups[0], false);
  for (const group of groups.slice(1)) {
    session.recordOutcome(group, true);
  }
  session.acknowledgeCompletion();

  assert.equal(
    Object.values(session.state.selectedExerciseIds).includes(rejectedId),
    false,
  );
  assert.deepEqual(session.state.nextWorkoutExcludedExerciseIds, []);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.Warmup
    ][rejectedId],
    -1,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  assert.deepEqual(session.state.nextWorkoutExcludedExerciseIds, []);
  assert.notEqual(session.getSelectedExercise(session.getActiveGroups()[0]).id, rejectedId);
});

test("demand-ordered warmup uses its actual phase when selecting next workout", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const target = groups.find((group) => group.id === "r30.spinal-extensors");
  const rejected = exercise(
    1000,
    target.canonicalGroups[0],
    target.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
    true,
    0,
  );
  const alternative = exercise(
    1001,
    target.canonicalGroups[0],
    target.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
    true,
    0,
  );
  const fixedExercises = groups
    .filter((group) => group.id !== target.id)
    .map((group, index) => exercise(
      index + 1,
      group.canonicalGroups[0],
      group.canonicalGroups.slice(1),
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
      1,
    ));
  const state = createDefaultState();
  state.catalogRevision = CURRENT_CATALOG_REVISION;
  state.selectedExerciseIds = Object.fromEntries(fixedExercises.map((item) => [
    groups.find((group) =>
      group.canonicalGroups.includes(item.primaryCanonicalGroup)).id,
    item.id,
  ]));
  state.selectedExerciseIds[target.id] = rejected.id;
  state.keptExerciseRootIdsBySelectionGroupId = Object.fromEntries(
    Object.entries(state.selectedExerciseIds).map(([groupId, exerciseId]) => [
      groupId,
      [exerciseId],
    ]),
  );
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.Warmup]: { [rejected.id]: -1 },
  };
  const session = new WorkoutSession(
    [rejected, alternative, ...fixedExercises],
    state,
    () => 0,
  );

  session.startWorkout(60, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[target.id], alternative.id);
  const finalTargetBlock = session.getActiveGroups()
    .filter((group) => getSelectionKey(group) === target.id)
    .at(-1);
  assert.equal(
    getWorkoutExercisePhase(finalTargetBlock.order),
    WORKOUT_EXERCISE_PHASE.Warmup,
  );
});

test("short-workout keeps carry into matching long-workout slots", () => {
  const session = new WorkoutSession(
    reviewedInsectCatalog(),
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.Insect);
  const keptExerciseIds = session.getActiveGroups().map((group) =>
    session.getSelectedExercise(group).id);
  for (const group of session.getActiveGroups()) {
    session.recordOutcome(group, true);
  }
  session.acknowledgeCompletion();

  session.startWorkout(45, WORKOUT_MODIFIERS.Insect);

  assert.deepEqual(
    [...session.state.lastKeptExerciseIds].sort((left, right) => left - right),
    [...keptExerciseIds].sort((left, right) => left - right),
  );
  const longKeepIds = Object.entries(
    session.state.keptExerciseRootIdsBySelectionGroupId,
  ).filter(([selectionGroupId]) => selectionGroupId.startsWith("r30."))
    .flatMap(([, rootIds]) => rootIds);
  assert.ok(keptExerciseIds.every((exerciseId) => longKeepIds.includes(exerciseId)));
  assert.equal(session.getActiveGroups().length, 45);
  assert.equal(session.state.activeExtraSetSelectionGroupIds.length, 17);
  assert.equal(
    session.getSelectionGroups().some((group) =>
      group.id === "r30.intrinsic-hand" ||
      group.id === "r30.pelvic-floor-perineum"),
    false,
  );
});

test("a keep and downvote for one exercise remain independent across phases", () => {
  const shortGroups = RESOLUTIONS.get(3).groups;
  const longGroups = RESOLUTIONS.get(30).groups;
  const primary = "AbdominalWall";
  const shortSlot = shortGroups.find((group) =>
    group.canonicalGroups.includes(primary));
  const longSlot = longGroups.find((group) =>
    group.canonicalGroups.includes(primary));
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const shared = exercise(
    1,
    primary,
    allCanonicalGroups.filter((group) => group !== primary),
    10,
  );
  const longAlternatives = longGroups.map((group, index) => exercise(
    100 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  ));
  const shortAlternatives = shortGroups.map((group, index) => exercise(
    200 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  ));
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId = {
    [shortSlot.id]: [shared.id],
  };
  const session = new WorkoutSession(
    [shared, ...longAlternatives, ...shortAlternatives],
    state,
    () => 0,
  );

  session.startWorkout(60, WORKOUT_MODIFIERS.None);
  const longRound = session.getActiveGroups().filter((round) =>
    getSelectionKey(round) === longSlot.id).at(-1);
  assert.equal(session.getSelectedExercise(longRound).id, shared.id);

  for (const prior of session.getActiveGroups().filter((round) =>
    round.order < longRound.order)) {
    session.state.outcomes[prior.id] = "tick";
  }

  session.recordOutcome(longRound, false);

  assert.equal(
    session.state.keptExerciseRootIdsBySelectionGroupId[shortSlot.id]
      .includes(shared.id),
    true,
  );
  assert.equal(
    session.state.keptExerciseRootIdsBySelectionGroupId[longSlot.id]
      ?.includes(shared.id) ?? false,
    true,
  );
  assert.equal(getWorkoutExercisePhase(longRound.order),
    WORKOUT_EXERCISE_PHASE.PeakPerformance);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance
    ][shared.id],
    -1,
  );
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.Warmup
    ],
    undefined,
  );
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.Fatigued
    ],
    undefined,
  );
  assert.equal(session.state.lastKeptExerciseIds.includes(shared.id), true);
  assert.equal(session.getScore(shared), 10);

  session.finishInterruptedWorkout();
  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[shortSlot.id], shared.id);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance
    ][shared.id],
    -1,
  );
});

test("reviewed production catalog satisfies the enforceable coverage hierarchy", () => {
  assert.deepEqual(new Set(catalog.filter((exercise) =>
    exercise.upperBodyClothingRequirement ===
      EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.ClothingRequired)
    .map((exercise) => exercise.id)), new Set([134, 137, 165, 175, 579, 580, 801, 913]));
  assert.deepEqual(new Set(catalog.filter((exercise) =>
    exercise.upperBodyClothingRequirement ===
      EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.BareUpperBodyRequired)
    .map((exercise) => exercise.id)), new Set([524, 525, 526, 527, 528, 790]));
  assert.equal(catalog.filter((exercise) =>
    exercise.upperBodyClothingRequirement ===
      EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.Agnostic).length, 531);
  assert.equal(catalog.filter((exercise) =>
    exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Compatible).length, 428);
  assert.equal(catalog.filter((exercise) =>
    exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Incompatible).length, 117);
  for (const exerciseId of [56, 185, 377, 379, 401, 403, 557]) {
    assert.equal(catalog.find((exercise) => exercise.id === exerciseId).shyCompatibility,
      EXERCISE_SHY_COMPATIBILITY.Incompatible);
  }
  for (const exerciseId of [19, 107, 227, 264, 279, 389, 439, 611, 987]) {
    assert.equal(catalog.find((exercise) => exercise.id === exerciseId).shyCompatibility,
      EXERCISE_SHY_COMPATIBILITY.Compatible);
  }

  const headTurnMarch = catalog.find((exercise) => exercise.id === 202);
  assert.equal(headTurnMarch.name, "Fixed-Gaze Head-Turn March");
  assert.equal(headTurnMarch.primaryCanonicalGroup, "CranialMuscles");
  assert.deepEqual(new Set(headTurnMarch.secondaryCanonicalGroups), new Set([
    "PosteriorNeckAndSuboccipitalMuscles",
    "AnteriorLateralNeckAndHyoidMuscles",
  ]));
  assert.equal(headTurnMarch.muscularDemand, MINIMUM_MUSCULAR_DEMAND);
  assert.equal(headTurnMarch.sideSequence, "Continuous");
  assert.equal(headTurnMarch.hardFloorCompatibility,
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible);
  assert.equal(headTurnMarch.shyCompatibility,
    EXERCISE_SHY_COMPATIBILITY.Compatible);
  assert.equal(headTurnMarch.mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly);
  assert.equal(headTurnMarch.minimumMirrorCoverage,
    EXERCISE_MIRROR_COVERAGE.UpperBody);

  for (const [exerciseId, expectedName] of [
    [204, "Jab-Cross-Hook-Uppercut Combo"],
    [205, "Jab-Cross-Speed-Bag Combo"],
  ]) {
    const boxingCombo = catalog.find((exercise) => exercise.id === exerciseId);
    assert.equal(boxingCombo.name, expectedName);
    assert.equal(boxingCombo.muscularDemand, MODERATE_MUSCULAR_DEMAND);
    assert.equal(boxingCombo.sideSequence, exerciseId === 204
      ? "ScreenRightLeadThenLeftLead" : "ScreenLeftLeadThenRightLead");
    assert.equal(boxingCombo.hardFloorCompatibility,
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible);
    assert.equal(boxingCombo.shyCompatibility,
      EXERCISE_SHY_COMPATIBILITY.Compatible);
    assert.equal(boxingCombo.mirrorRelationship,
      EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly);
    assert.equal(boxingCombo.minimumMirrorCoverage,
      EXERCISE_MIRROR_COVERAGE.UpperBody);
    assert.equal(boxingCombo.sequenceBlocks.length, 2);
    assert.equal(boxingCombo.sequenceBlocks[0].mirrorMedia, false);
    assert.equal(boxingCombo.sequenceBlocks[1].mirrorMedia, true);
  }
  assert.equal(catalog.find((exercise) => exercise.id === 204)
    .primaryCanonicalGroup, "Chest");
  assert.equal(catalog.find((exercise) => exercise.id === 205)
    .primaryCanonicalGroup, "ShoulderAbductors");
  assert.deepEqual(new Set(catalog.find((exercise) => exercise.id === 205)
    .secondaryCanonicalGroups), new Set([
    "Chest",
    "ElbowExtensors",
  ]));
  assert.equal(catalog.find((exercise) => exercise.id === 265).mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship ===
      EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly).length, 99);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.Agnostic).length, 446);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship ===
      EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly).length, 0);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody).length, 0);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody).length, 0);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody).length, 40);
  assert.equal(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody).length, 59);
  assert.deepEqual(new Set(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody)
    .map((exercise) => exercise.id)), new Set());
  assert.deepEqual(new Set(catalog.filter((exercise) =>
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly &&
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody)
    .map((exercise) => exercise.id)), new Set());
  const mostMuscularPose = catalog.find((exercise) => exercise.id === 790);
  assert.equal(mostMuscularPose.name, "Most-Muscular Posing, Hands on Thighs");
  assert.equal(mostMuscularPose.primaryCanonicalGroup, "Chest");
  assert.equal(mostMuscularPose.mode, "Hold");
  assert.equal(mostMuscularPose.presentation, "Still");
  assert.equal(mostMuscularPose.muscularDemand, 1);
  assert.equal(mostMuscularPose.hardFloorCompatibility, "Compatible");
  for (const exerciseId of [524, 525, 526, 527, 528, 790]) {
    const pose = catalog.find((exercise) => exercise.id === exerciseId);
    assert.match(pose.name, /Posing/);
    assert.equal(pose.mode, "Hold");
    assert.equal(pose.presentation, "Still");
    assert.equal(pose.holdFramePercent, 50);
  }
  const standingVacuum = catalog.find((exercise) => exercise.id === 993);
  assert.equal(standingVacuum.name, "Standing Stomach Vacuum, Then Release");
  assert.equal(standingVacuum.primaryCanonicalGroup, "AbdominalWall");
  assert.deepEqual(standingVacuum.secondaryCanonicalGroups, []);
  assert.equal(standingVacuum.mode, "Repetition");
  assert.equal(standingVacuum.muscularDemand, 1);
  assert.equal(catalog.some((exercise) => exercise.id === 90), false);
  assert.equal(catalog.some((exercise) => exercise.name.startsWith("Mirror-Guided ")), false);
  for (const exerciseId of [94, 95, 99, 100, 497, 498, 500, 511, 514]) {
    const exercise = catalog.find((candidate) => candidate.id === exerciseId);
    assert.equal(exercise.mirrorRelationship, EXERCISE_MIRROR_RELATIONSHIP.Agnostic);
    assert.equal(exercise.equipment, "None");
  }
  assert.equal(isModifierMetadataComplete(catalog), true);
  const baseWallExercises = catalog.filter((exercise) =>
    exercise.wallRequired && !exercise.soleWallContactRequired);
  const soleWallExercises = catalog.filter((exercise) =>
    exercise.soleWallContactRequired);
  assert.equal(catalog.filter((exercise) => exercise.wallRequired).length, 35);
  assert.equal(baseWallExercises.length, 29);
  assert.equal(new Set(baseWallExercises
    .map((exercise) => exercise.sessionMovementId || exercise.id)).size, 26);
  assert.deepEqual(
    new Set(soleWallExercises.map((exercise) => exercise.id)),
    new Set([563, 564, 567, 568, 574, 633]),
  );
  assert.equal(new Set(soleWallExercises
    .map((exercise) => exercise.sessionMovementId || exercise.id)).size, 5);
  assert.deepEqual(findWallRequiredCatalogDeficiencies(catalog), []);
  assert.deepEqual(
    findSoleWallContactRequiredCatalogDeficiencies(catalog),
    [],
  );
  const pairwiseDeficiencies = findWorkoutModifierPairCoverageDeficiencies(catalog);
  assert.deepEqual(pairwiseDeficiencies, []);
  assert.equal(
    getMinimumExercisesPerModifierPairStatePerGroup(3),
    MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
  );
  assert.equal(
    getMinimumExercisesPerModifierPairStatePerGroup(30),
    MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
  );

  const hardFloorDeficiencies = findHardFloorCategoryCoverageDeficiencies(catalog);
  assert.deepEqual(hardFloorDeficiencies, []);

  // Demand and materiality inventories are recorded in the diagnostic ledger;
  // playable-workout availability and complete atomic lineups still gate release.
  assert.deepEqual(findCompleteWorkoutProfileLineupDeficiencies(catalog), []);
  const allModifiers = WORKOUT_MODIFIERS.Insect |
    WORKOUT_MODIFIERS.Silence |
    WORKOUT_MODIFIERS.Mirror;
  for (const minutes of SUPPORTED_MINUTES) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes, allModifiers);
    assert.equal(session.state.activeWorkoutModifiers, allModifiers);
    assert.ok(session.getActiveGroups().every((group) =>
      isSelectableForWorkoutProfile(
        session.getSequenceSelectionExerciseForGroup(
          session.getSelectedExercise(group),
          group,
        ),
        group,
        allModifiers,
      )));
  }
});

test("pairwise availability requires one real broad movement", () => {
  const groups = RESOLUTIONS.get(BROAD_COVERAGE_RESOLUTION_MINUTES).groups;
  const targetGroup = groups[1];
  const primary = targetGroup.canonicalGroups[0];
  const secondary = targetGroup.canonicalGroups.slice(1, 3);
  const exercises = [
    exercise(5, primary, secondary, 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, false),
    exercise(6, primary, secondary, 0,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible, true),
    exercise(7, primary, secondary, 0,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible, false),
  ];

  const deficiencies = findWorkoutModifierPairCoverageDeficiencies(exercises)
    .filter((result) =>
      result.minutes === BROAD_COVERAGE_RESOLUTION_MINUTES &&
      result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Silence);

  assert.equal(deficiencies.length, 1);
  assert.equal(deficiencies[0].firstModifierEnabled, true);
  assert.equal(deficiencies[0].secondModifierEnabled, true);
  assert.equal(deficiencies[0].matchingExerciseCount, 0);
  assert.equal(
    deficiencies[0].requiredExerciseCount,
    MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
  );

  exercises.push(exercise(
    8,
    primary,
    secondary,
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
    true,
  ));
  assert.deepEqual(
    findWorkoutModifierPairCoverageDeficiencies(exercises).filter((result) =>
      result.minutes === BROAD_COVERAGE_RESOLUTION_MINUTES &&
      result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Silence),
    [],
  );
});

test("hard-floor coverage requires safe choices without soft-only counterparts", () => {
  const targetGroup = RESOLUTIONS.get(BROAD_COVERAGE_RESOLUTION_MINUTES).groups[1];
  const primary = targetGroup.canonicalGroups[0];
  const secondary = targetGroup.canonicalGroups.slice(1, 3);
  const compatible = Array.from({ length: 5 }, (_, index) => ({
    ...exercise(
      index + 1,
      primary,
      secondary,
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    ),
    hardFloorCompatibility:
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible,
  }));
  const incompatible = Array.from({ length: 4 }, (_, index) => ({
    ...exercise(
      index + 6,
      primary,
      secondary,
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    ),
    hardFloorCompatibility:
      EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible,
  }));

  const target = (result) => result.minutes === 3 && result.groupId === targetGroup.id;
  assert.deepEqual(findHardFloorCategoryCoverageDeficiencies(compatible).filter(target), []);
  const deficiencies = findHardFloorCategoryCoverageDeficiencies([
    ...incompatible,
  ]).filter(target);
  assert.equal(deficiencies.length, 5);
  assert.ok(deficiencies.every((result) => result.hardFloorCompatibility === "Compatible" &&
    result.matchingExerciseCount === 0));
  const fine = RESOLUTIONS.get(30).groups.find((group) => group.canonicalGroups.includes(primary));
  const fineTarget = (result) => result.minutes === 30 && result.groupId === fine.id;
  assert.deepEqual(findHardFloorCategoryCoverageDeficiencies(compatible).filter(fineTarget), []);
  assert.ok(findHardFloorCategoryCoverageDeficiencies(incompatible).filter(fineTarget)
    .some((result) => result.matchingExerciseCount === 0 && result.requiredExerciseCount === 1));
  assert.ok(findHardFloorCategoryCoverageDeficiencies([...compatible, ...incompatible])
    .every((result) => result.hardFloorCompatibility === "Compatible"));
});

test("demand coverage requires whole light sequences and slot-owned hard members", () => {
  const groups = RESOLUTIONS.get(BROAD_COVERAGE_RESOLUTION_MINUTES).groups;
  const targetGroup = groups[1];
  const otherGroup = groups[0];
  const target = targetGroup.canonicalGroups;
  const other = otherGroup.canonicalGroups[0];
  const pureLight = exercise(1, target[0], target.slice(1, 3), 0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 0);
  const mixedRoot = {
    ...exercise(2, target[0], target.slice(1, 3), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 0),
    sequenceBlocks: [{ exerciseId: 2 }, { exerciseId: 3 }],
  };
  const mixedMember = {
    ...exercise(3, target[0], [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 1),
    sequenceBlocks: [],
  };
  const hardElsewhereRoot = {
    ...exercise(4, target[0], target.slice(1, 3), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 0),
    sequenceBlocks: [{ exerciseId: 4 }, { exerciseId: 5 }],
  };
  const hardElsewhereMember = {
    ...exercise(5, other, [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 2),
    sequenceBlocks: [],
  };
  const hardForTargetRoot = {
    ...exercise(6, other, target.slice(1, 3), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 1),
    sequenceBlocks: [{ exerciseId: 6 }, { exerciseId: 7 }],
  };
  const hardForTargetMember = {
    ...exercise(7, target[0], [], 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible, true, 2),
    sequenceBlocks: [],
  };
  const targetDeficiencies = (catalog) =>
    findMuscularDemandCoverageDeficiencies(catalog).filter((result) =>
      result.minutes === BROAD_COVERAGE_RESOLUTION_MINUTES &&
      result.groupId === targetGroup.id &&
      result.profile === WORKOUT_MODIFIERS.None);

  assert.deepEqual(targetDeficiencies([
    mixedRoot,
    mixedMember,
    hardForTargetRoot,
    hardForTargetMember,
  ]), [{
    minutes: BROAD_COVERAGE_RESOLUTION_MINUTES,
    groupId: targetGroup.id,
    groupName: targetGroup.displayName,
    muscularDemand: 0,
    profile: WORKOUT_MODIFIERS.None,
    matchingExerciseCount: 0,
    requiredExerciseCount: 1,
  }]);

  assert.deepEqual(targetDeficiencies([
    pureLight,
    hardElsewhereRoot,
    hardElsewhereMember,
  ]), [{
    minutes: BROAD_COVERAGE_RESOLUTION_MINUTES,
    groupId: targetGroup.id,
    groupName: targetGroup.displayName,
    muscularDemand: 2,
    profile: WORKOUT_MODIFIERS.None,
    matchingExerciseCount: 0,
    requiredExerciseCount: 1,
  }]);

  assert.deepEqual(targetDeficiencies([
    pureLight,
    hardForTargetRoot,
    hardForTargetMember,
  ]), []);
});

test("demand coverage uses one genuine session movement per category", () => {
  const targetGroup = RESOLUTIONS.get(BROAD_COVERAGE_RESOLUTION_MINUTES).groups[1];
  const target = targetGroup.canonicalGroups;
  const exercises = [
    exercise(
      1,
      target[0],
      target.slice(1, 3),
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
      0,
    ),
    exercise(
      2,
      target[0],
      target.slice(1, 3),
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
      2,
    ),
  ];

  assert.deepEqual(
    findMuscularDemandCoverageDeficiencies(exercises).filter((result) =>
      result.minutes === BROAD_COVERAGE_RESOLUTION_MINUTES &&
      result.groupId === targetGroup.id),
    [],
  );
  assert.equal(
    MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
    1,
  );
});

test("fine pairwise buckets measure availability without forcing mirror preference", () => {
  const targetGroup = RESOLUTIONS.get(30).groups[0];
  const agnosticExercise = {
    ...exercise(
      1,
      targetGroup.canonicalGroups[0],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ),
    mirrorRelationship: EXERCISE_MIRROR_RELATIONSHIP.Agnostic,
  };

  assert.deepEqual(
    findWorkoutModifierPairCoverageDeficiencies([agnosticExercise])
      .filter((result) =>
        result.minutes === 30 &&
        result.groupId === targetGroup.id &&
        result.firstModifier === WORKOUT_MODIFIERS.Insect &&
        result.secondModifier === WORKOUT_MODIFIERS.Mirror &&
        result.secondModifierEnabled),
    [],
  );
  assert.equal(
    getMinimumExercisesPerModifierPairStatePerGroup(30),
    MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
  );
});

test("broad pairwise buckets count selectable agnostic movements", () => {
  const targetGroup = RESOLUTIONS.get(BROAD_COVERAGE_RESOLUTION_MINUTES).groups[0];
  const groups = targetGroup.canonicalGroups;
  const catalog = [];
  for (let index = 0; index < 5; index += 1) {
    const rootId = index * 2 + 1;
    const memberId = rootId + 1;
    catalog.push({
      ...exercise(
        rootId,
        groups[0],
        groups.slice(1, 3),
        0,
        EXERCISE_INSECT_COMPATIBILITY.Compatible,
      ),
      sequenceBlocks: [{ exerciseId: rootId }, { exerciseId: memberId }],
    });
    catalog.push({
      ...exercise(
        memberId,
        groups[3],
        groups.slice(4, 6),
        0,
        EXERCISE_INSECT_COMPATIBILITY.Compatible,
      ),
      sequenceBlocks: [],
    });
  }

  const deficiencies = findWorkoutModifierPairCoverageDeficiencies(catalog)
    .filter((result) =>
      result.minutes === BROAD_COVERAGE_RESOLUTION_MINUTES &&
      result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Mirror &&
      result.secondModifierEnabled);

  assert.deepEqual(deficiencies, []);
  const shortage = findWorkoutModifierPairCoverageDeficiencies([])
    .filter((result) => result.minutes === 3 && result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Mirror && result.secondModifierEnabled);
  assert.equal(shortage.length, 4);
  assert.ok(shortage.every((result) => result.matchingExerciseCount === 0));
  assert.ok(findWorkoutModifierMaterialityDeficiencies(catalog).some((result) =>
    result.enabledModifier === WORKOUT_MODIFIERS.Mirror && result.baseProfile === WORKOUT_MODIFIERS.None));
});

test("pairwise floor never counts unreviewed modifier metadata", () => {
  const targetGroup = RESOLUTIONS.get(BROAD_COVERAGE_RESOLUTION_MINUTES).groups[1];
  const primary = targetGroup.canonicalGroups[0];
  const secondary = targetGroup.canonicalGroups.slice(1, 3);
  const exercises = [
    exercise(
      5,
      primary,
      secondary,
      0,
      EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
      true,
    ),
  ];

  const deficiencies = findWorkoutModifierPairCoverageDeficiencies(exercises)
    .filter((result) =>
      result.minutes === BROAD_COVERAGE_RESOLUTION_MINUTES &&
      result.groupId === targetGroup.id &&
      result.firstModifier === WORKOUT_MODIFIERS.Insect &&
      result.secondModifier === WORKOUT_MODIFIERS.Silence);

  assert.equal(deficiencies.length, 4);
  assert.ok(deficiencies.every((deficiency) =>
    deficiency.matchingExerciseCount === 0));
});

test("modifier materiality rejects token and pairwise-redundant filters", () => {
  const canonicalGroups = RESOLUTIONS.get(30).groups.map(
    (group) => group.canonicalGroups[0],
  );
  const coversEveryGroup = (id, insectCompatibility, silent) => exercise(
    id,
    canonicalGroups[0],
    canonicalGroups.slice(1),
    0,
    insectCompatibility,
    silent,
  );
  const tokenCatalog = Array.from({ length: 20 }, (_, index) =>
    coversEveryGroup(
      index + 1,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    ));
  const tokenDeficiencies = findWorkoutModifierMaterialityDeficiencies(tokenCatalog);
  assert.equal(tokenDeficiencies.length, 34);
  assert.ok(tokenDeficiencies.every((deficiency) =>
    deficiency.materialExerciseCount === 0));

  const pairwiseRedundantCatalog = [
    ...Array.from({ length: 20 }, (_, index) => coversEveryGroup(
      index + 1,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    )),
    ...Array.from({ length: 10 }, (_, index) => coversEveryGroup(
      101 + index,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible,
      false,
    )),
  ];
  const pairwiseDeficiencies = findWorkoutModifierMaterialityDeficiencies(
    pairwiseRedundantCatalog,
  );
  assert.ok(pairwiseDeficiencies.some((deficiency) =>
    deficiency.baseProfile === WORKOUT_MODIFIERS.Silence &&
    deficiency.enabledModifier === WORKOUT_MODIFIERS.Insect &&
    deficiency.materialExerciseCount === 0));
  assert.ok(pairwiseDeficiencies.some((deficiency) =>
    deficiency.baseProfile === WORKOUT_MODIFIERS.Insect &&
    deficiency.enabledModifier === WORKOUT_MODIFIERS.Silence &&
    deficiency.materialExerciseCount === 0));
});

test("modifier materiality never credits unreviewed metadata", () => {
  const canonicalGroups = RESOLUTIONS.get(30).groups
    .slice(0, 3)
    .map((group) => group.canonicalGroups[0]);
  const exercises = [
    ...Array.from({ length: 5 }, (_, index) => exercise(
      index + 1,
      canonicalGroups[index % canonicalGroups.length],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
      true,
    )),
    ...Array.from({ length: 5 }, (_, index) => exercise(
      index + 6,
      canonicalGroups[index % canonicalGroups.length],
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
      true,
    )),
  ];

  const deficiency = findWorkoutModifierMaterialityDeficiencies(exercises)
    .find((result) =>
      result.baseProfile === WORKOUT_MODIFIERS.None &&
      result.enabledModifier === WORKOUT_MODIFIERS.Insect);

  assert.equal(deficiency.materialExerciseCount, 0);
  assert.equal(deficiency.affectedGroupCount, 0);
});

test("distinct-lineup matching reroutes shared exercises instead of using greedy counts", () => {
  const groups = [
    { id: "a", displayName: "A", canonicalGroups: ["A"] },
    { id: "b", displayName: "B", canonicalGroups: ["B"] },
    { id: "c", displayName: "C", canonicalGroups: ["C"] },
  ];
  const exercises = [
    exercise(1, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(2, "A", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(3, "B", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
  ];

  assert.equal(
    getMaximumDistinctLineupSize(exercises, groups, WORKOUT_MODIFIERS.Insect),
    3,
  );
});

test("distinct-lineup matching detects a Hall deficit after modifier filtering", () => {
  const groups = [
    { id: "a", displayName: "A", canonicalGroups: ["A"] },
    { id: "b", displayName: "B", canonicalGroups: ["B"] },
    { id: "c", displayName: "C", canonicalGroups: ["C"] },
  ];
  const exercises = [
    exercise(1, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(2, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(3, "C", [], 0, EXERCISE_INSECT_COMPATIBILITY.Incompatible),
  ];

  assert.equal(
    getMaximumDistinctLineupSize(exercises, groups, WORKOUT_MODIFIERS.None),
    3,
  );
  assert.equal(
    getMaximumDistinctLineupSize(exercises, groups, WORKOUT_MODIFIERS.Insect),
    2,
  );
});

test("distinct-lineup capacity counts aliases as one session movement", () => {
  const groups = [
    { id: "a", displayName: "A", canonicalGroups: ["A"] },
    { id: "b", displayName: "B", canonicalGroups: ["B"] },
    { id: "c", displayName: "C", canonicalGroups: ["C"] },
  ];
  const root = {
    ...exercise(1, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    sessionMovementId: 1,
  };
  const alias = {
    ...exercise(2, "A", ["B", "C"], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    sessionMovementId: 1,
  };
  const lastOnly = exercise(
    3,
    "C",
    [],
    0,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
  );

  assert.equal(
    getMaximumDistinctLineupSize(
      [root, alias, lastOnly],
      groups,
      WORKOUT_MODIFIERS.Insect,
    ),
    2,
  );
});

test("distinct-lineup capacity credits a cross-primary atomic sequence", () => {
  const groups = [
    { id: "a", displayName: "A", order: 1, canonicalGroups: ["A"] },
    { id: "b", displayName: "B", order: 2, canonicalGroups: ["B"] },
    { id: "c", displayName: "C", order: 3, canonicalGroups: ["C"] },
  ];
  const root = {
    ...exercise(1, "A", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    sequenceBlocks: [
      { exerciseId: 1, sideCue: "None", directionCue: "None", mirrorMedia: false },
      { exerciseId: 2, sideCue: "None", directionCue: "None", mirrorMedia: false },
    ],
  };
  const member = {
    ...exercise(2, "B", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    sequenceBlocks: [],
  };

  assert.equal(getMaximumDistinctLineupSize(
    [root, member, exercise(
      3,
      "C",
      [],
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    )],
    groups,
    WORKOUT_MODIFIERS.Insect,
    3,
  ), 3);
});

test("same-primary sequence yields naturally when exact capacity is too small", () => {
  const groups = [
    { id: "a", displayName: "A", order: 1, canonicalGroups: ["A"] },
    { id: "b", displayName: "B", order: 2, canonicalGroups: ["B"] },
    { id: "c", displayName: "C", order: 3, canonicalGroups: ["C"] },
  ];
  const root = {
    ...exercise(1, "A", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    sequenceBlocks: [
      { exerciseId: 1, sideCue: "None", directionCue: "None", mirrorMedia: false },
      { exerciseId: 2, sideCue: "None", directionCue: "None", mirrorMedia: false },
    ],
  };
  const member = {
    ...exercise(2, "A", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    sequenceBlocks: [],
  };

  assert.equal(getMaximumDistinctLineupSize(
    [
      root,
      member,
      exercise(3, "B", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
      exercise(4, "C", [], 0, EXERCISE_INSECT_COMPATIBILITY.Compatible),
    ],
    groups,
    WORKOUT_MODIFIERS.Insect,
    3,
  ), 2);
});

test("session movement metadata requires an explicit anatomically related root family", () => {
  const root = {
    ...exercise(1, "A", [], 0),
    sessionMovementId: 1,
  };
  const alias = {
    ...exercise(2, "A", [], 0),
    sessionMovementId: 1,
  };

  assert.equal(isSessionMovementMetadataValid([root, alias]), true);
  assert.equal(isSessionMovementMetadataValid([root]), false);
  assert.equal(isSessionMovementMetadataValid([alias]), false);
  assert.equal(isSessionMovementMetadataValid([
    root,
    {
      ...alias,
      primaryCanonicalGroup: "B",
      secondaryCanonicalGroups: ["A"],
    },
  ]), true);
  assert.equal(isSessionMovementMetadataValid([
    root,
    {
      ...alias,
      primaryCanonicalGroup: "B",
      secondaryCanonicalGroups: [],
    },
  ]), false);
  assert.equal(isSessionMovementMetadataValid([
    { ...exercise(3, "A", [], 0), sessionMovementId: -1 },
  ]), false);
});

test("atomic sequence metadata requires one complete valid owner per exercise", () => {
  const root = exercise(1, "A", ["B"], 0);
  const member = exercise(2, "A", ["B"], 0);
  root.sequenceBlocks = [
    root.sequenceBlocks[0],
    {
      exerciseId: member.id,
      sideCue: "ScreenRight",
      directionCue: "Outward",
      mirrorMedia: true,
      mediaSegment: "Full",
    },
  ];
  member.sequenceBlocks = [];

  assert.equal(isSessionMovementMetadataValid([root, member]), true);
  assert.equal(isSessionMovementMetadataValid([
    root,
    { ...member, sequenceBlocks: [exercise(2, "A", ["B"], 0).sequenceBlocks[0]] },
  ]), false);
  assert.equal(isSessionMovementMetadataValid([
    {
      ...root,
      sequenceBlocks: [
        root.sequenceBlocks[0],
        { ...root.sequenceBlocks[1], sideCue: "InventedSide" },
      ],
    },
    member,
  ]), false);
  assert.equal(isSessionMovementMetadataValid([
    { ...root, sequenceBlocks: [root.sequenceBlocks[1]] },
    member,
  ]), false);
  assert.equal(isSessionMovementMetadataValid([root]), false);
});

test("lineup repair reroutes a saved exercise when preserving it would dead-end", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const shared = exercise(
    1,
    allCanonicalGroups[0],
    allCanonicalGroups.slice(1),
    100,
  );
  const firstOnly = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const lastOnly = exercise(
    3,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds = {
    [groups[0].id]: shared.id,
    [groups[2].id]: lastOnly.id,
  };
  const session = new WorkoutSession([shared, firstOnly, lastOnly], state, () => 0);

  session.repairActiveLineup();

  assert.equal(session.state.selectedExerciseIds[groups[0].id], firstOnly.id);
  assert.equal(session.state.selectedExerciseIds[groups[1].id], shared.id);
  assert.equal(session.state.selectedExerciseIds[groups[2].id], lastOnly.id);
});

test("global lineup and repair allow only one alias of a session movement", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const root = {
    ...exercise(1, allCanonicalGroups[0], allCanonicalGroups.slice(1), 100),
    sessionMovementId: 1,
  };
  const alias = {
    ...exercise(2, allCanonicalGroups[0], allCanonicalGroups.slice(1), 100),
    sessionMovementId: 1,
  };
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const exercises = [root, alias, middle, last];
  const freshSession = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
  );

  freshSession.startWorkout(3, WORKOUT_MODIFIERS.None);

  let selected = groups.map((group) => freshSession.getSelectedExercise(group));
  assert.equal(selected.filter((item) => item.id === 1 || item.id === 2).length, 1);
  assert.equal(new Set(selected.map(getSessionMovementId)).size, selected.length);

  const savedState = createDefaultState();
  savedState.activeWorkoutMinutes = 3;
  savedState.selectedExerciseIds = {
    [groups[0].id]: root.id,
    [groups[1].id]: alias.id,
    [groups[2].id]: last.id,
  };
  const restoredSession = new WorkoutSession(exercises, savedState, () => 0);
  restoredSession.repairActiveLineup();

  selected = groups.map((group) => restoredSession.getSelectedExercise(group));
  assert.equal(selected.filter((item) => item.id === 1 || item.id === 2).length, 1);
  assert.equal(new Set(selected.map(getSessionMovementId)).size, selected.length);
});

test("a keep preference does not move to another compatible slot", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const sharedKept = exercise(
    1,
    allCanonicalGroups[0],
    allCanonicalGroups.slice(1),
    100,
  );
  const firstAlternative = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    100,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    100,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    100,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 3;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [sharedKept.id],
  };
  state.selectedExerciseIds = { [groups[0].id]: sharedKept.id };
  const session = new WorkoutSession(
    [sharedKept, firstAlternative, middle, last],
    state,
    () => 0,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], sharedKept.id);
  assert.equal(session.state.selectedExerciseIds[groups[1].id], middle.id);
  assert.equal(
    session.state.keptExerciseRootIdsBySelectionGroupId[groups[1].id]
      ?.includes(sharedKept.id) ?? false,
    false,
  );
});

test("fresh hard work outranks a non-hard keep and soft mirror preference", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const hard = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const nonHardKeep = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    1,
    EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 3;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [nonHardKeep.id],
  };
  state.selectedExerciseIds = { [groups[0].id]: nonHardKeep.id };
  const session = new WorkoutSession(
    [hard, nonHardKeep, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.Mirror);

  assert.equal(
    session.state.selectedExerciseIds[`p${WORKOUT_MODIFIERS.Mirror}|${groups[0].id}`],
    hard.id,
  );
  assert.ok(session.state.lastKeptExerciseIds.includes(nonHardKeep.id));
});

test("a completed light workout starts the next four-day cadence", () => {
  const dayFour = new Date(2026, 7, 29, 8).getTime();
  const history = [1, 2, 3].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  ));
  history.push({
    ...completedWorkoutSession(99, new Date(2026, 7, 28, 9).getTime()),
    status: "Interrupted",
  });

  assert.equal(LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP, 60);
  assert.equal(LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT, 180);
  assert.equal(isLightWorkoutDayDue(history, dayFour), true);

  history.push(completedWorkoutSession(4, dayFour, true));
  assert.equal(isLightWorkoutDayDue(
    history,
    new Date(2026, 7, 30, 8).getTime(),
  ), false);

  history.push(...[5, 6, 7].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  )));
  assert.equal(isLightWorkoutDayDue(
    history,
    new Date(2026, 8, 2, 8).getTime(),
  ), true);
});

test("a regular workout on a due day does not skip the light workout", () => {
  const dayFour = new Date(2026, 7, 29, 8).getTime();
  const history = [1, 2, 3].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  ));

  assert.equal(isLightWorkoutDayDue(history, dayFour), true);

  history.push(completedWorkoutSession(4, dayFour));
  assert.equal(isLightWorkoutDayDue(
    history,
    new Date(2026, 7, 30, 8).getTime(),
  ), true);

  history.push(completedWorkoutSession(
    5,
    new Date(2026, 7, 30, 8).getTime(),
    true,
  ));
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    new Date(2026, 7, 31, 8).getTime(),
  ), 3);
});

test("version 21 recovers a contiguous legacy day for tomorrow's light workout", () => {
  const now = new Date(2026, 7, 30, 8).getTime();
  const legacyDay = new Date(2026, 7, 27, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  ));
  const state = createDefaultState();
  state.version = 21;
  state.workoutHistory = [
    completedWorkoutSession(1, new Date(2026, 7, 28, 8).getTime()),
    completedWorkoutSession(2, new Date(2026, 7, 29, 8).getTime()),
  ];
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = {
    CalfDeepPosteriorLegAndPlantarFoot: legacyDay,
    GlutealExtensors: legacyDay + 4 * 60_000,
    MedialAndDeepKneeExtensors: legacyDay + 12 * 60_000,
  };
  const session = new WorkoutSession(exercises, state, () => 0, () => now);

  session.initialize();
  session.startWorkout(3, session.getDefaultWorkoutModifiers());

  assert.equal(session.state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(session.state.legacyCompletedTrainingDayUnixMilliseconds.length, 1);
  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.activeWorkoutSession.isLightDay, true);
  assert.equal(session.state.workoutHistory.length, 2);
});

test("light countdown tracks hour-long workouts throughout the repeating cadence", () => {
  const dayOne = new Date(2026, 7, 26, 8).getTime();
  const history = [];

  assert.equal(getWorkoutsUntilLightWorkout(history, 60, dayOne), 3);
  history.push(completedWorkoutSession(1, dayOne));
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    new Date(2026, 7, 26, 9).getTime(),
  ), 2);
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    new Date(2026, 7, 27, 8).getTime(),
  ), 2);
  history.push(completedWorkoutSession(
    2,
    new Date(2026, 7, 27, 8).getTime(),
  ));
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    new Date(2026, 7, 28, 8).getTime(),
  ), 1);
  history.push(completedWorkoutSession(
    3,
    new Date(2026, 7, 28, 8).getTime(),
  ));
  const dayFour = new Date(2026, 7, 29, 8).getTime();
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    new Date(2026, 7, 28, 9).getTime(),
  ), 0);
  assert.equal(getWorkoutsUntilLightWorkout(history, 60, dayFour), 0);
  assert.equal(isLightWorkoutDayDue(history, dayFour), true);

  history.push(completedWorkoutSession(4, dayFour, true));
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    new Date(2026, 7, 29, 9).getTime(),
  ), 0);
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    new Date(2026, 7, 30, 8).getTime(),
  ), 3);
});

test("light countdown uses the latest light workout instead of legacy streak modulo", () => {
  const firstLoggedDay = new Date(2026, 7, 28, 7).getTime();
  const today = new Date(2026, 8, 4, 6, 33).getTime();
  const history = Array.from({ length: 7 }, (_, index) => completedWorkoutSession(
    index + 1,
    firstLoggedDay + index * 24 * 60 * 60_000,
    index === 5,
  ));
  const legacyDay = new Date(2026, 7, 27, 7).getTime();

  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    today,
    [legacyDay],
  ), 2);

  history.push(completedWorkoutSession(8, today));
  assert.equal(getWorkoutsUntilLightWorkout(
    history,
    60,
    today + 60 * 60_000,
    [legacyDay],
  ), 1);
});

test("manual light mode is never remembered as the next session default", () => {
  const now = new Date(2026, 7, 26, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
  ));
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Light);

  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.Silence);
  assert.equal(session.getDefaultWorkoutModifiers(), WORKOUT_MODIFIERS.Silence);
});

for (const nearlyCompleted of [false, true]) {
test(`automatic Light cannot be bypassed with explicit regular modifiers (nearly completed: ${nearlyCompleted})`, () => {
  const now = new Date(2026, 7, 29, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
  ));
  const state = createDefaultState();
  state.lastWorkoutModifiers = WORKOUT_MODIFIERS.Insect |
    WORKOUT_MODIFIERS.Light;
  state.workoutHistory = [1, 2, 3].map((sessionId) => {
    const record = completedWorkoutSession(
      sessionId, new Date(2026, 7, 25 + sessionId, 8).getTime(),
    );
    if (nearlyCompleted) {
      record.blocks = Array.from({ length: [53, 56, 55][sessionId - 1] }, (_, block) => ({
        completedAtUnixMilliseconds: record.startedAtUnixMilliseconds + (block + 1) * 60_000,
      }));
    }
    return record;
  });
  const session = new WorkoutSession(exercises, state, () => 0, () => now);

  assert.equal(
    session.getDefaultWorkoutModifiers(),
    WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Light,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.activeWorkoutModifiers, WORKOUT_MODIFIERS.Light);
  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.activeWorkoutSession.isLightDay, true);
  session.reconfigureActiveWorkout(WORKOUT_MODIFIERS.None, session.getNextGroup().id);
  assert.equal(session.state.activeWorkoutModifiers, WORKOUT_MODIFIERS.Light);
});
}

test("automatic Light is rechecked when activating an earlier prepared workout", () => {
  let now = new Date(2026, 8, 3, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.flatMap((group, index) => [
    { ...exercise(index + 1, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0), muscularDemand: 2 },
    { ...exercise(index + 101, group.canonicalGroups[0], group.canonicalGroups.slice(1), 0), muscularDemand: 0 },
  ]);
  const state = createDefaultState();
  state.workoutHistory = [
    completedWorkoutSession(1, new Date(2026, 8, 1, 8).getTime()),
    completedWorkoutSession(2, new Date(2026, 8, 2, 8).getTime()),
  ];
  const session = new WorkoutSession(exercises, state, () => 0, () => now);
  session.prepareWorkout(3, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.activeWorkoutIsLightDay, false);
  assert.ok(session.getActiveGroups().some((group) =>
    session.getSelectedExercise(group).muscularDemand === 2));

  session.state.workoutHistory.push(completedWorkoutSession(3, now));
  now += 2 * 60 * 60_000;
  session.activatePreparedWorkout();

  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.activeWorkoutSession.isLightDay, true);
  assert.ok(session.getActiveGroups().every((group) =>
    session.getSelectedExercise(group).muscularDemand === 0));
  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.workoutHistory.length, 3);
});

test("version 24 migrates an active light workout into its own profile", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const selected = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  ));
  selected[0].muscularDemand = 2;
  const lowerScoredEasy = exercise(
    99,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    -1,
  );
  const exercises = [...selected, lowerScoredEasy];
  const state = createDefaultState();
  state.version = 24;
  state.catalogRevision = CURRENT_CATALOG_REVISION;
  state.lastWorkoutMinutes = 3;
  state.lastWorkoutModifiers = WORKOUT_MODIFIERS.Silence |
    WORKOUT_MODIFIERS.Light;
  state.activeWorkoutMinutes = 3;
  state.activeWorkoutModifiers = WORKOUT_MODIFIERS.Silence;
  state.activeWorkoutIsLightDay = true;
  state.selectedExerciseIds = Object.fromEntries(groups.map((group, index) => [
    `p${WORKOUT_MODIFIERS.Silence}|${group.id}`,
    selected[index].id,
  ]));
  const session = new WorkoutSession(exercises, state, () => 0);

  session.initialize();

  const lightProfile = WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Light;
  assert.equal(session.state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(session.state.lastWorkoutModifiers, WORKOUT_MODIFIERS.Silence);
  assert.equal(session.state.activeWorkoutModifiers, lightProfile);
  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.selectedExerciseIds[
    `p${WORKOUT_MODIFIERS.Silence}|${groups[0].id}`
  ], selected[0].id);
  assert.equal(session.state.selectedExerciseIds[
    `p${lightProfile}|${groups[0].id}`
  ], lowerScoredEasy.id);
  assert.ok(session.getActiveGroups().every((round) =>
    session.getSelectedExercise(round).muscularDemand === 0));
  assert.equal(session.state.activeWorkoutSession.isLightDay, true);
  assert.equal(session.state.activeWorkoutSession.modifiers, lightProfile);
});

test("legacy day inference rejects sparse hard-work evidence", () => {
  const now = new Date(2026, 7, 30, 8).getTime();
  const legacyDay = new Date(2026, 7, 27, 8).getTime();
  const inferred = inferLegacyCompletedTrainingDays(
    [
      completedWorkoutSession(1, new Date(2026, 7, 28, 8).getTime()),
      completedWorkoutSession(2, new Date(2026, 7, 29, 8).getTime()),
    ],
    {
      GlutealExtensors: legacyDay,
      MedialAndDeepKneeExtensors: legacyDay + 4 * 60_000,
    },
    [],
    now,
  );

  assert.equal(MINIMUM_LEGACY_HARD_PRIMARY_MUSCLES, 3);
  assert.deepEqual(inferred, []);
});

test("light day demand zero outranks a hard keep without deleting it", () => {
  const now = new Date(2026, 7, 29, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const hardKeep = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const easy = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.workoutHistory = [1, 2, 3].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  ));
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [hardKeep.id],
  };
  const session = new WorkoutSession(
    [hardKeep, easy, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.Light);

  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(
    session.state.selectedExerciseIds[
      `p${WORKOUT_MODIFIERS.Light}|${groups[0].id}`
    ],
    easy.id,
  );
  assert.ok(session.state.keptExerciseRootIdsBySelectionGroupId[groups[0].id]
    .includes(hardKeep.id));
  assert.equal(session.state.activeWorkoutSession.isLightDay, true);
  const restored = parseStoredState(JSON.stringify(session.state));
  assert.equal(restored.activeWorkoutIsLightDay, true);
  assert.equal(restored.activeWorkoutSession.isLightDay, true);
});

test("light day pulls demand zero from a lower score bucket without changing scores", () => {
  const now = new Date(2026, 7, 29, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const topScoredHard = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const lowerScoredEasy = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    -1,
  );
  const middle = exercise(3, groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1), 0);
  const last = exercise(4, groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1), 0);
  const state = createDefaultState();
  state.workoutHistory = [1, 2, 3].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  ));
  const session = new WorkoutSession(
    [topScoredHard, lowerScoredEasy, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.Light);

  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(
    session.state.selectedExerciseIds[
      `p${WORKOUT_MODIFIERS.Light}|${groups[0].id}`
    ],
    lowerScoredEasy.id,
  );
  assert.equal(topScoredHard.score, 0);
  assert.equal(lowerScoredEasy.score, -1);
});

test("version 25 replans unfinished active light work without rewriting completed work", () => {
  const now = Date.UTC(2026, 8, 2, 6);
  const groups = RESOLUTIONS.get(5).groups;
  const easy = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  ));
  const hardRoot = exercise(
    101,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const hardMember = exercise(
    102,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  hardRoot.sequenceBlocks = [
    { ...hardRoot.sequenceBlocks[0] },
    { ...hardMember.sequenceBlocks[0] },
  ];
  hardMember.sequenceBlocks = [];
  const hardThird = exercise(
    103,
    groups[3].canonicalGroups[0],
    groups[3].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const hardFourth = exercise(
    104,
    groups[4].canonicalGroups[0],
    groups[4].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const exercises = [
    ...easy,
    hardRoot,
    hardMember,
    hardThird,
    hardFourth,
  ];
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(5, WORKOUT_MODIFIERS.Light);
  assert.ok(session.getActiveGroups().every((round) =>
    session.getSelectedExercise(round).muscularDemand === 0));
  session.state.catalogRevision = CURRENT_CATALOG_REVISION;
  const profilePrefix = `p${WORKOUT_MODIFIERS.Light}|`;
  session.state.selectedExerciseIds[`${profilePrefix}${groups[1].id}`] =
    hardRoot.id;
  session.state.selectedExerciseIds[`${profilePrefix}${groups[2].id}`] =
    hardRoot.id;
  session.state.selectedExerciseIds[`${profilePrefix}${groups[3].id}`] =
    hardThird.id;
  session.state.selectedExerciseIds[`${profilePrefix}${groups[4].id}`] =
    hardFourth.id;
  session.state.activeSetCountsBySelectionGroupId = {
    [groups[0].id]: 1,
    [groups[1].id]: 1,
    [groups[3].id]: 1,
    [groups[4].id]: 1,
  };
  session.state.activeExtraSetSelectionGroupIds = [];
  session.state.activeSelectionGroupOrder = [];

  const completed = session.getNextGroup();
  assert.equal(getSelectionKey(completed), groups[0].id);
  session.recordOutcome(completed, true);
  session.state.keptExerciseRootIdsBySelectionGroupId[groups[0].id] =
    [easy[0].id];
  session.state.lastKeptExerciseIds.push(easy[0].id);
  const firstHardBlock = session.getNextGroup();
  assert.equal(getSelectionKey(firstHardBlock), groups[1].id);
  assert.equal(firstHardBlock.sequenceBlockIndex, 0);
  session.beginRest(firstHardBlock, now + 15_000);
  session.advanceSequence(firstHardBlock);
  const unfinishedHardBlock = session.getNextGroup();
  assert.equal(getSelectionKey(unfinishedHardBlock), groups[1].id);
  assert.equal(unfinishedHardBlock.sequenceBlockIndex, 1);
  session.pauseMovement(unfinishedHardBlock, 30_000, true);
  assert.equal(session.state.activeWorkoutSession.blocks.length, 1);
  assert.equal(session.state.activeWorkoutSession.decisions.length, 1);
  const sessionId = session.state.activeWorkoutSession.sessionId;
  session.state.version = 25;

  const restored = new WorkoutSession(
    exercises,
    JSON.parse(JSON.stringify(session.state)),
    () => 0,
    () => now + 60_000,
  );
  restored.initialize();

  assert.equal(restored.state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(restored.state.activeWorkoutSession.sessionId, sessionId);
  assert.equal(restored.state.outcomes[completed.id], "tick");
  assert.equal(restored.state.activeWorkoutSession.decisions.length, 1);
  assert.equal(restored.state.activeWorkoutSession.blocks.length, 1);
  assert.equal(
    restored.state.activeWorkoutSession.blocks[0].rootExerciseId,
    hardRoot.id,
  );
  assert.equal(restored.state.pendingRestGroupId, null);
  const restarted = restored.getNextGroup();
  assert.equal(getSelectionKey(restarted), groups[1].id);
  assert.equal(restarted.sequenceBlockIndex ?? 0, 0);
  assert.equal(restored.state.pendingMovementGroupId, restarted.id);
  assert.equal(restored.state.pendingMovementMillisecondsRemaining, 50_000);
  assert.equal(restored.state.pendingMovementEndsAtUnixMilliseconds, 0);
  assert.equal(restored.state.pendingMovementPausedByUser, true);
  assert.equal(restored.getSelectedExercise(restarted).muscularDemand, 0);
  assert.ok(restored.getActiveGroups().every((round) =>
    restored.getSelectedExercise(round).muscularDemand === 0));
  assert.ok(restored.state.keptExerciseRootIdsBySelectionGroupId[groups[0].id]
    .includes(easy[0].id));
  assert.equal(restored.state.activeWorkoutSession.modifierChanges.length, 1);
  const migration = restored.state.activeWorkoutSession.modifierChanges[0];
  assert.equal(migration.previousModifiers, WORKOUT_MODIFIERS.Light);
  assert.equal(migration.newModifiers, WORKOUT_MODIFIERS.Light);
  assert.ok(migration.plannedSelections.every((selection) =>
    exercises.find((candidate) => candidate.id === selection.rootExerciseId)
      .muscularDemand === 0));
});

test("light day requires every block of an atomic sequence to be demand zero", () => {
  const now = new Date(2026, 7, 29, 8).getTime();
  const groups = RESOLUTIONS.get(5).groups;
  const mixedRoot = exercise(1, groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1), 0);
  const moderateMember = exercise(2, groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1), 0, undefined, true, 1);
  mixedRoot.sequenceBlocks = [
    { ...mixedRoot.sequenceBlocks[0] },
    { ...moderateMember.sequenceBlocks[0] },
  ];
  moderateMember.sequenceBlocks = [];
  const easyFirst = exercise(3, groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1), 0);
  const easySecond = exercise(4, groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1), 0);
  const fillers = groups.slice(2).map((group, index) => exercise(
    5 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  ));
  const state = createDefaultState();
  state.workoutHistory = [1, 2, 3].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  ));
  state.selectedExerciseIds = {
    [groups[0].id]: mixedRoot.id,
    [groups[1].id]: mixedRoot.id,
    ...Object.fromEntries(groups.slice(2).map((group, index) =>
      [group.id, fillers[index].id])),
  };
  const session = new WorkoutSession(
    [mixedRoot, moderateMember, easyFirst, easySecond, ...fillers],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(5, WORKOUT_MODIFIERS.Light);

  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.selectedExerciseIds[
    `p${WORKOUT_MODIFIERS.Light}|${groups[0].id}`
  ], easyFirst.id);
  assert.equal(session.state.selectedExerciseIds[
    `p${WORKOUT_MODIFIERS.Light}|${groups[1].id}`
  ], easySecond.id);
  assert.equal(Object.entries(session.state.selectedExerciseIds)
    .filter(([key]) => key.startsWith(`p${WORKOUT_MODIFIERS.Light}|`))
    .map(([, value]) => value)
    .includes(mixedRoot.id), false);
});

test("light-day shuffle uses the best available demand-zero exercise", () => {
  const now = new Date(2026, 7, 29, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const easyOne = exercise(1, groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1), 0);
  const easyTwo = exercise(2, groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1), 0);
  const hard = exercise(3, groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1), 0, undefined, true, 2);
  const middle = exercise(4, groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1), 0);
  const last = exercise(5, groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1), 0);
  const state = createDefaultState();
  state.workoutHistory = [1, 2, 3].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  ));
  const session = new WorkoutSession(
    [easyOne, easyTwo, hard, middle, last],
    state,
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.Light);

  const result = session.shuffleNextExercise(session.getNextGroup());

  assert.equal(result.replacementExercise.muscularDemand, 0);
});

test("version twenty prepared workout recognizes an existing light-day streak", () => {
  const now = new Date(2026, 7, 29, 8).getTime();
  const groups = RESOLUTIONS.get(3).groups;
  const hard = exercise(1, groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1), 0, undefined, true, 2);
  const easy = exercise(2, groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1), 0);
  const middle = exercise(3, groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1), 0);
  const last = exercise(4, groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1), 0);
  const state = createDefaultState();
  state.version = 20;
  state.catalogRevision = CURRENT_CATALOG_REVISION;
  state.activeWorkoutMinutes = 3;
  state.activeWorkoutModifiers = WORKOUT_MODIFIERS.None;
  state.lastWorkoutMinutes = 3;
  state.selectedExerciseIds = {
    [groups[0].id]: hard.id,
    [groups[1].id]: middle.id,
    [groups[2].id]: last.id,
  };
  state.workoutHistory = [1, 2, 3].map((sessionId) => completedWorkoutSession(
    sessionId,
    new Date(2026, 7, 25 + sessionId, 8).getTime(),
  ));
  const session = new WorkoutSession(
    [hard, easy, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.initialize();

  assert.equal(session.state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(session.state.activeWorkoutIsLightDay, true);
  assert.equal(session.state.selectedExerciseIds[
    `p${WORKOUT_MODIFIERS.Light}|${groups[0].id}`
  ], easy.id);
  assert.equal(session.state.activeWorkoutSession.isLightDay, true);
});

test("a same-muscle sequence is ranked by its hardest primary block", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const root = exercise(1, groups[0].canonicalGroups[0], [], 0);
  const hardMember = exercise(1_001, groups[0].canonicalGroups[0], [], 0);
  hardMember.muscularDemand = HARD_MUSCULAR_DEMAND;
  root.sequenceBlocks = [
    { ...root.sequenceBlocks[0] },
    { ...hardMember.sequenceBlocks[0] },
  ];
  hardMember.sequenceBlocks = [];
  const nonHardKeep = exercise(1_002, groups[0].canonicalGroups[0], [], 0);
  nonHardKeep.muscularDemand = MODERATE_MUSCULAR_DEMAND;
  const fillers = groups.slice(1).map((group, index) => exercise(
    index + 2,
    group.canonicalGroups[0],
    [],
    0,
  ));
  const state = createDefaultState();
  state.lastWorkoutMinutes = 45;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [nonHardKeep.id],
  };
  state.selectedExerciseIds[groups[0].id] = nonHardKeep.id;
  const session = new WorkoutSession(
    [root, hardMember, nonHardKeep, ...fillers],
    state,
    () => 0,
  );

  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], root.id);
  assert.ok(session.state.lastKeptExerciseIds.includes(nonHardKeep.id));
});

test("fresh hard keep gets an opportunity despite a lower saved score", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const hardKeep = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    -1,
    undefined,
    true,
    2,
  );
  const nonHardKeep = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    1,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [hardKeep.id, nonHardKeep.id],
  };
  const session = new WorkoutSession(
    [hardKeep, nonHardKeep, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], hardKeep.id);
});

test("recovering hard keep yields to a non-hard keep without being forgotten", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const hardKeep = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    2,
  );
  const nonHardKeep = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    1,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 3;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [hardKeep.id, nonHardKeep.id],
  };
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = {
    [hardKeep.primaryCanonicalGroup]: now - 4 * 60 * 60 * 1000,
  };
  state.selectedExerciseIds = { [groups[0].id]: hardKeep.id };
  const session = new WorkoutSession(
    [hardKeep, nonHardKeep, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], nonHardKeep.id);
  assert.ok(session.state.lastKeptExerciseIds.includes(hardKeep.id));
  assert.ok(session.state.lastKeptExerciseIds.includes(nonHardKeep.id));
});

test("recovering moderate keep yields to easy work without being forgotten", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const moderateKeep = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    1,
  );
  const easy = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    0,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastWorkoutMinutes = 3;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [moderateKeep.id],
  };
  state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = {
    [moderateKeep.primaryCanonicalGroup]: now - 4 * 60 * 60 * 1000,
  };
  state.selectedExerciseIds = { [groups[0].id]: moderateKeep.id };
  const session = new WorkoutSession(
    [moderateKeep, easy, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], easy.id);
  assert.ok(session.state.lastKeptExerciseIds.includes(moderateKeep.id));
});

test("hard rotation never overrides a higher persisted user score", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const rejectedHard = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    -1,
    undefined,
    true,
    2,
  );
  const nonHard = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    1,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  const session = new WorkoutSession(
    [rejectedHard, nonHard, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], nonHard.id);
  assert.equal(session.getScore(rejectedHard), -1);
  assert.equal(session.getScore(nonHard), 0);
});

test("recovery remains soft when the hard exercise has a higher user score", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const recoveringHard = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    1,
    undefined,
    true,
    2,
  );
  const nonHard = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    1,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = {
    [recoveringHard.primaryCanonicalGroup]: now - 4 * 60 * 60 * 1000,
  };
  const session = new WorkoutSession(
    [recoveringHard, nonHard, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], recoveringHard.id);
});

test("moderate recovery remains soft when the exercise has a higher user score", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const recoveringModerate = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    1,
    undefined,
    true,
    1,
  );
  const easy = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    undefined,
    true,
    0,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = {
    [recoveringModerate.primaryCanonicalGroup]: now - 4 * 60 * 60 * 1000,
  };
  const session = new WorkoutSession(
    [recoveringModerate, easy, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], recoveringModerate.id);
});

test("equivalent fresh hard candidates favor the longest-rested primary muscle", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const recentlyWorked = "IntrinsicHand";
  const longestRested = "ForearmExtensorsAndSupinators";
  const sharedCoverage = [
    recentlyWorked,
    longestRested,
    ...groups[0].canonicalGroups.filter((muscle) =>
      muscle !== recentlyWorked && muscle !== longestRested),
  ];
  const recentHard = exercise(
    1,
    recentlyWorked,
    sharedCoverage.filter((muscle) => muscle !== recentlyWorked),
    0,
    undefined,
    true,
    2,
  );
  const restedHard = exercise(
    2,
    longestRested,
    sharedCoverage.filter((muscle) => muscle !== longestRested),
    0,
    undefined,
    true,
    2,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const state = createDefaultState();
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = {
    [recentlyWorked]: now - 40 * 60 * 60 * 1000,
    [longestRested]: now - 72 * 60 * 60 * 1000,
  };
  const session = new WorkoutSession(
    [recentHard, restedHard, middle, last],
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.selectedExerciseIds[groups[0].id], restedHard.id);
});

test("completed hard exercise starts both recovery windows but skipped does not", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const hard = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    10,
    undefined,
    true,
    2,
  );
  const alternative = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    10,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    10,
  );

  const completed = new WorkoutSession(
    [hard, alternative, middle, last],
    createDefaultState(),
    () => 0,
    () => now,
  );
  completed.startWorkout(3, WORKOUT_MODIFIERS.None);
  const completedGroup = completed.getActiveGroups().find((group) =>
    completed.getSelectedExercise(group).id === hard.id);
  for (const prior of completed.getActiveGroups().filter((group) =>
    group.order < completedGroup.order)) {
    completed.state.outcomes[prior.id] = "neutral";
  }
  completed.beginRest(completedGroup, now + 15_000);
  const persistedCompletion = parseStoredState(JSON.stringify(completed.state));

  assert.equal(
    persistedCompletion.lastHardWorkUnixMillisecondsByPrimaryMuscle[
      hard.primaryCanonicalGroup
    ],
    now,
  );
  assert.equal(
    persistedCompletion.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[
      hard.primaryCanonicalGroup
    ],
    now,
  );
  assert.equal(completed.getScore(hard), 10);

  const skipped = new WorkoutSession(
    [hard, alternative, middle, last],
    createDefaultState(),
    () => 0,
    () => now,
  );
  skipped.startWorkout(3, WORKOUT_MODIFIERS.None);
  const skippedGroup = skipped.getActiveGroups().find((group) =>
    skipped.getSelectedExercise(group).id === hard.id);
  for (const prior of skipped.getActiveGroups().filter((group) =>
    group.order < skippedGroup.order)) {
    skipped.state.outcomes[prior.id] = "neutral";
  }
  skipped.recordOutcome(skippedGroup, false);

  assert.deepEqual(skipped.state.lastHardWorkUnixMillisecondsByPrimaryMuscle, {});
  assert.deepEqual(skipped.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle, {});
});

test("completed moderate exercise starts only meaningful recovery", () => {
  const now = Date.UTC(2026, 7, 22, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const moderate = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    10,
    undefined,
    true,
    1,
  );
  const alternative = exercise(
    2,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const middle = exercise(
    3,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    10,
  );
  const last = exercise(
    4,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    10,
  );
  const session = new WorkoutSession(
    [moderate, alternative, middle, last],
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const completedGroup = session.getActiveGroups().find((group) =>
    session.getSelectedExercise(group).id === moderate.id);
  for (const prior of session.getActiveGroups().filter((group) =>
    group.order < completedGroup.order)) {
    session.state.outcomes[prior.id] = "neutral";
  }

  session.beginRest(completedGroup, now + 15_000);

  assert.equal(
    session.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[
      moderate.primaryCanonicalGroup
    ],
    now,
  );
  assert.deepEqual(session.state.lastHardWorkUnixMillisecondsByPrimaryMuscle, {});
  assert.equal(session.getScore(moderate), 10);
});

test("rejected replacements use global matching instead of greedy group order", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);
  const currentFirst = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const currentMiddle = exercise(
    2,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const currentLast = exercise(
    3,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    10,
  );
  const sharedReplacement = exercise(
    4,
    allCanonicalGroups[0],
    allCanonicalGroups.slice(1),
    100,
  );
  const firstOnlyReplacement = exercise(
    5,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    5,
  );
  const state = createDefaultState();
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds = {
    [groups[0].id]: currentFirst.id,
    [groups[1].id]: currentMiddle.id,
    [groups[2].id]: currentLast.id,
  };
  const session = new WorkoutSession(
    [
      currentFirst,
      currentMiddle,
      currentLast,
      sharedReplacement,
      firstOnlyReplacement,
    ],
    state,
    () => 0,
  );
  session.repairActiveLineup();

  const activeGroups = session.getActiveGroups();
  session.recordOutcome(activeGroups[0], false);
  session.recordOutcome(activeGroups[1], false);
  session.recordOutcome(activeGroups[2], true);
  session.acknowledgeCompletion();
  assert.equal(session.state.activeWorkoutMinutes, 0);
  assert.equal(session.state.selectedExerciseIds[groups[0].id], undefined);
  assert.equal(session.state.selectedExerciseIds[groups[1].id], undefined);
  session.prepareWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(
    session.state.selectedExerciseIds[groups[0].id],
    firstOnlyReplacement.id,
  );
  assert.equal(
    session.state.selectedExerciseIds[groups[1].id],
    sharedReplacement.id,
  );
  assert.equal(session.state.selectedExerciseIds[groups[2].id], currentLast.id);
});

test("every resolution covers all canonical leaves once in scheduled order", () => {
  for (const [minutes, resolution] of RESOLUTIONS) {
    assert.equal(resolution.groups.length, minutes);
    assert.deepEqual(
      resolution.groups.map((group) => group.order),
      Array.from({ length: minutes }, (_, index) => index + 1),
    );
    const leaves = resolution.groups.flatMap((group) => group.canonicalGroups);
    assert.equal(leaves.length, 30);
    assert.equal(new Set(leaves).size, 30);
  }

  assert.equal(RESOLUTIONS.get(3).groups[0].id, "r3.head-neck-upper-limbs");
  assert.equal(RESOLUTIONS.get(3).groups.at(-1).id, "r3.lower-limbs");
  assert.equal(RESOLUTIONS.get(30).groups[0].id, "r30.pelvic-floor-perineum");
  assert.equal(RESOLUTIONS.get(30).groups.at(-1).id, "r30.medial-deep-knee-extensors");
});

test("workout schedule orders demand zero then two then one before muscle order", () => {
  const groups = RESOLUTIONS.get(5).groups;
  const demandByMuscleOrder = [
    MODERATE_MUSCULAR_DEMAND,
    MINIMUM_MUSCULAR_DEMAND,
    MAXIMUM_MUSCULAR_DEMAND,
    MINIMUM_MUSCULAR_DEMAND,
    MODERATE_MUSCULAR_DEMAND,
  ];
  const exercises = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
    true,
    demandByMuscleOrder[index],
  ));
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  session.startWorkout(5, WORKOUT_MODIFIERS.None);

  const expectedSelectionOrder = [...groups]
    .sort((left, right) =>
      getMuscularDemandSchedulePriority(
        demandByMuscleOrder[left.order - 1],
      ) - getMuscularDemandSchedulePriority(
        demandByMuscleOrder[right.order - 1],
      ) || left.order - right.order)
    .map((group) => group.id);
  const rounds = session.getActiveGroups();
  assert.deepEqual(rounds.map(getSelectionKey), expectedSelectionOrder);
  assert.deepEqual(
    rounds.map((round) => session.getSelectedExercise(round).muscularDemand),
    [0, 0, 2, 1, 1],
  );
  assert.deepEqual(
    session.state.activeWorkoutSession.initialSelections.map(
      (selection) => selection.selectionGroupId,
    ),
    expectedSelectionOrder,
  );

  const snapshotsByGroup = new Map(
    session.state.activeWorkoutSession.initialSelections.map((selection) =>
      [selection.selectionGroupId, selection]),
  );
  session.state.activeWorkoutSession.initialSelections = groups.map((group) =>
    snapshotsByGroup.get(group.id));
  assert.deepEqual(
    session.getActiveGroups().map(getSelectionKey),
    groups.map((group) => group.id),
  );
});

test("mixed-demand sequence uses its highest demand and remains atomic", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const root = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
    true,
    MINIMUM_MUSCULAR_DEMAND,
  );
  const member = exercise(
    2,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
    EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
    true,
    MAXIMUM_MUSCULAR_DEMAND,
  );
  const easyStandalone = exercise(
    3,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  root.sequenceBlocks = [
    root.sequenceBlocks[0],
    member.sequenceBlocks[0],
  ];
  member.sequenceBlocks = [];
  const session = new WorkoutSession(
    [root, member, easyStandalone],
    createDefaultState(),
    () => 0,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  const rounds = session.getActiveGroups();
  assert.deepEqual(
    rounds.map(getSelectionKey),
    [groups[2].id, groups[0].id, groups[0].id],
  );
  assert.deepEqual(
    rounds.map((round) => session.getSelectedExercise(round).id),
    [easyStandalone.id, root.id, member.id],
  );
  assert.equal(
    getSequenceMuscularDemand(root, session.exercisesById),
    MAXIMUM_MUSCULAR_DEMAND,
  );
});

test("the reviewed catalog satisfies every roll-up and selects distinct exercises", () => {
  assert.equal(catalog.length, 545);
  assert.equal(new Set(catalog.map((exercise) => exercise.id)).size, 545);
  assert.equal(new Set(catalog.map((exercise) => exercise.name)).size, 545);
  assert.equal(isSessionMovementMetadataValid(catalog), true);
  const actualSessionMovements = {};
  for (const exercise of catalog.filter((item) => item.sessionMovementId > 0)) {
    actualSessionMovements[exercise.sessionMovementId] ??= [];
    actualSessionMovements[exercise.sessionMovementId].push(exercise.id);
  }
  for (const exerciseIds of Object.values(actualSessionMovements)) {
    exerciseIds.sort((left, right) => left - right);
  }
  assert.deepEqual(
    actualSessionMovements,
    {
      31: [31, 169, 486],
      101: [101, 110],
      102: [102, 111, 147],
      104: [19, 103, 104, 105, 107, 109, 136, 199, 626],
      113: [113, 135],
      115: [115, 532, 996, 997],
      117: [116, 117, 123, 150],
      120: [120, 184],
      124: [124, 636],
      134: [134, 137],
      143: [143, 538],
      153: [153, 603],
      159: [159, 649],
      160: [160, 533],
      177: [177, 186],
      187: [187, 252, 253, 254, 562, 566, 581, 582],
      192: [192, 195],
      211: [211, 233],
      214: [214, 223, 236, 755, 756],
      231: [231, 685],
      256: [256, 845, 958],
      262: [262, 507],
      264: [264, 275],
      266: [266, 301],
      270: [270, 677],
      277: [261, 277],
      278: [278, 326],
      286: [286, 541],
      288: [288, 758],
      292: [148, 292, 542],
      311: [311, 321, 816],
      327: [327, 546],
      329: [329, 531],
      437: [437, 1002],
      475: [475, 919],
      480: [480, 517],
      529: [529, 539],
      549: [549, 570],
      550: [550, 551],
      568: [568, 633],
      579: [579, 580],
      712: [712, 1012],
      625: [625, 1021],
      1010: [1010, 1018],
      1026: [1026, 1027],
      969: [969, 1028, 1031],
      948: [948, 949],
    },
  );
  const exercisesById = new Map(catalog.map((exercise) => [exercise.id, exercise]));
  for (const root of catalog.filter((exercise) =>
    new Set(exercise.sequenceBlocks.map((block) => block.exerciseId)).size > 1)) {
    assert.equal(
      new Set(root.sequenceBlocks.map((block) =>
        exercisesById.get(block.exerciseId).mode)).size,
      1,
      `${root.name} must not force repetition and static variants together`,
    );
  }
  const breathingExercises = catalog.filter(
    (exercise) => exercise.primaryCanonicalGroup === "BreathingMuscles",
  );
  assert.equal(breathingExercises.length, 7);
  for (const exercise of breathingExercises) {
    assert.match(exercise.name, /\b(?:inhale|exhale|breath|laugh|laughter)/i);
  }
  const overheadBreathingFlow = catalog.find((exercise) => exercise.id === 395);
  assert.equal(
    overheadBreathingFlow.name,
    "Alternating Knee Lift and Overhead Reach",
  );
  assert.equal(overheadBreathingFlow.mode, "Repetition");
  assert.equal(overheadBreathingFlow.presentation, "Motion");
  assert.equal(overheadBreathingFlow.primaryCanonicalGroup, "HipFlexors");
  assert.equal(overheadBreathingFlow.secondaryCanonicalGroups.includes(
    "BreathingMuscles",
  ), false);
  const alternatingSideTap = catalog.find((exercise) => exercise.id === 397);
  assert.equal(
    alternatingSideTap.name,
    "Alternating Side Tap with Diagonal Reach",
  );
  assert.equal(alternatingSideTap.sideSequence, "Alternating");
  assert.equal(alternatingSideTap.sequenceBlocks.length, 1);
  assert.deepEqual(
    {
      exerciseId: alternatingSideTap.sequenceBlocks[0].exerciseId,
      sideCue: alternatingSideTap.sequenceBlocks[0].sideCue,
      mirrorMedia: alternatingSideTap.sequenceBlocks[0].mirrorMedia,
    },
    { exerciseId: 397, sideCue: "None", mirrorMedia: false },
  );
  assert.equal(alternatingSideTap.primaryCanonicalGroup, "ShoulderAbductors");
  assert.equal(
    alternatingSideTap.secondaryCanonicalGroups.includes("BreathingMuscles"),
    false,
  );
  assert.equal(alternatingSideTap.secondaryCanonicalGroups.includes(
    "AccessoryHipAdductors",
  ), false);
  assert.equal(alternatingSideTap.secondaryCanonicalGroups.includes(
    "ScapularGirdle",
  ), false);
  const wideStanceReach = catalog.find((exercise) => exercise.id === 193);
  assert.equal(wideStanceReach.name, "Hip Hinge with Overhead Reach");
  assert.equal(wideStanceReach.muscularDemand, 1);
  assert.equal(wideStanceReach.primaryCanonicalGroup,
    "PosteriorThighAndKneeFlexors");
  assert.equal(wideStanceReach.secondaryCanonicalGroups.includes(
    "MedialAndDeepKneeExtensors",
  ), false);
  const narrowStanceReach = catalog.find((exercise) => exercise.id === 417);
  assert.equal(narrowStanceReach.name, "Narrow-Stance Overhead-to-Toe Reach");
  assert.equal(narrowStanceReach.muscularDemand, 0);
  assert.equal(narrowStanceReach.secondaryCanonicalGroups.includes(
    "CranialMuscles",
  ), false);
  assert.equal(narrowStanceReach.secondaryCanonicalGroups.includes(
    "AnteriorLateralNeckAndHyoidMuscles",
  ), false);
  assert.equal(narrowStanceReach.secondaryCanonicalGroups.includes(
    "PosteriorNeckAndSuboccipitalMuscles",
  ), false);
  const kneeRaiseHold = catalog.find((exercise) => exercise.id === 95);
  assert.equal(kneeRaiseHold.name, "Single-Leg Knee-Raise Hold");
  assert.equal(kneeRaiseHold.mode, "Hold");
  assert.equal(kneeRaiseHold.presentation, "Still");
  assert.equal(kneeRaiseHold.holdFramePercent, 50);
  assert.equal(kneeRaiseHold.insectCompatibility, "Incompatible");
  assert.ok([267, 553, 558, 559].every((exerciseId) =>
    !catalog.some((exercise) => exercise.id === exerciseId)));
  const standingKneeExtensionHold = catalog.find((exercise) => exercise.id === 145);
  assert.equal(standingKneeExtensionHold.name, "Wall-Supported Standing Knee-Extension Hold");
  assert.equal(standingKneeExtensionHold.mode, "Hold");
  assert.equal(standingKneeExtensionHold.presentation, "Still");
  assert.equal(standingKneeExtensionHold.holdFramePercent, 50);
  assert.equal(standingKneeExtensionHold.sideSequence, "ScreenLeftThenRight");
  const forwardSideLegCircles = catalog.find((exercise) => exercise.id === 617);
  const backwardSideLegCircles = catalog.find((exercise) => exercise.id === 620);
  assert.equal(forwardSideLegCircles.name, "Standing Forward Side-Leg Circles");
  assert.equal(backwardSideLegCircles.name, "Standing Backward Side-Leg Circles");
  assert.equal(backwardSideLegCircles.sideSequence, "ScreenLeftThenRight");
  assert.equal(backwardSideLegCircles.primaryCanonicalGroup, "HipAbductors");
  assert.notEqual(forwardSideLegCircles.video, backwardSideLegCircles.video);

  for (const [exerciseId, expectedName] of [
    [577, "Standing Side Crunch and Side Kick"],
    [618, "Single-Side Knee Raise with Torso Twist"],
    [915, "Single-Side Split-Stance Knee Drive with Overhead Reach"],
  ]) {
    const exercise = catalog.find((candidate) => candidate.id === exerciseId);
    assert.equal(exercise.name, expectedName);
    assert.equal(exercise.sequenceBlocks.length, 2);
  }
  assert.equal(catalog.find((exercise) => exercise.id === 834).sequenceBlocks.length, 4);
  const alternatingHighKneePull = catalog.find((exercise) => exercise.id === 219);
  assert.equal(alternatingHighKneePull.name, "High-Knee Cross-Body Pull");
  assert.equal(alternatingHighKneePull.sideSequence, "ScreenRightThenLeft");
  const highKneeSideReach = catalog.find((exercise) => exercise.id === 618);
  assert.equal(highKneeSideReach.primaryCanonicalGroup, "HipFlexors");
  assert.equal(
    highKneeSideReach.secondaryCanonicalGroups.includes("PelvicFloorAndPerineum"),
    false,
  );

  for (const [minutes, resolution] of RESOLUTIONS) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);
    const selected = session
      .getActiveGroups()
      .map((group) => session.getSelectedExercise(group));
    assert.equal(selected.length, minutes);
    assert.equal(new Set(selected.map((exercise) => exercise.id)).size, minutes);
    assert.equal(new Set(selected.map(getSessionMovementId)).size, minutes);
  }

  for (const minutes of [45, 60, 90]) {
    const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);
    const selected = session
      .getActiveGroups()
      .map((group) => session.getSelectedExercise(group));
    const baseSelections = session.getSelectionGroups()
      .map((group) => session.getSelectedExercise(group));
    assert.equal(
      new Set(baseSelections.map(getSessionMovementId)).size,
      session.getSelectedSequencePlacements().length,
    );
    assert.equal(session.getActiveGroups().length, minutes);
    assert.ok(session.getActiveGroups().every((round) => {
      const rootId = session.state.selectedExerciseIds[getSelectionKey(round)];
      const root = session.exercisesById.get(rootId);
      return root.sequenceBlocks[round.sequenceBlockIndex].exerciseId ===
        session.getSelectedExercise(round).id;
    }));
  }
});

test("long workouts emit adjacent atomic sequence blocks with exact duration", () => {
  const exercises = directionPairCatalog();
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  const rounds = session.getActiveGroups();
  assert.equal(rounds.length, 45);
  assert.equal(new Set(rounds.map((round) => round.id)).size, 45);
  assert.deepEqual(
    rounds.map((round) => round.order),
    Array.from({ length: 45 }, (_, index) => index + 1),
  );

  const selectedSequence = session.getSelectionGroups().find((group) =>
    session.state.selectedExerciseIds[group.id] === 1);
  assert.ok(selectedSequence);
  const sequenceRounds = rounds.filter((round) =>
    getSelectionKey(round) === selectedSequence.id);
  assert.ok(sequenceRounds.length >= 2);
  assert.ok(sequenceRounds.every((round) => round.sequenceBlockCount === 2));
  for (let index = 1; index < sequenceRounds.length; index += 1) {
    assert.equal(sequenceRounds[index].order, sequenceRounds[index - 1].order + 1);
  }
  for (const setNumber of new Set(sequenceRounds.map((round) => round.setNumber))) {
    const setRounds = sequenceRounds.filter((round) =>
      round.setNumber === setNumber);
    assert.deepEqual(setRounds.map((round) => round.sequenceBlockIndex), [0, 1]);
    assert.deepEqual(setRounds.map((round) => round.exerciseOverrideId), [1, 2]);
    assert.equal(isSequenceContinuationRound(setRounds[0]), setNumber > 1);
    assert.equal(isSequenceContinuationRound(setRounds[1]), true);
  }
  assert.equal(isFinalSequenceRound(sequenceRounds.at(-1)), true);
  assert.equal(getMovementCountdownDurationMs(sequenceRounds[0]), 50_000);
  assert.equal(getMovementCountdownDurationMs(sequenceRounds[1]), 45_000);
});

for (const [minutes, expectedMultiBlockSets, expectedRepeatedSingles] of [
  [45, 1, 15],
  [60, 2, 28],
]) {
  test(`${minutes}-minute extra sets prefer single blocks within each set round`, () => {
    const groups = RESOLUTIONS.get(30).groups;
    const first = exercise(
      1,
      groups[0].canonicalGroups[0],
      groups[0].canonicalGroups.slice(1),
      100,
    );
    const second = exercise(
      2,
      groups[1].canonicalGroups[0],
      groups[1].canonicalGroups.slice(1),
      100,
    );
    first.muscularDemand = HARD_MUSCULAR_DEMAND;
    second.muscularDemand = HARD_MUSCULAR_DEMAND;
    first.sequenceBlocks = [
      { ...first.sequenceBlocks[0] },
      { ...second.sequenceBlocks[0] },
    ];
    second.sequenceBlocks = [];
    const singleBlockExercises = groups.slice(2).map((group, index) => exercise(
      100 + index,
      group.canonicalGroups[0],
      group.canonicalGroups.slice(1),
      100,
    ));
    const session = new WorkoutSession(
      [first, second, ...singleBlockExercises],
      createDefaultState(),
      () => 0,
    );

    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);

    assert.equal(
      session.state.activeSetCountsBySelectionGroupId[groups[0].id],
      expectedMultiBlockSets,
    );
    assert.equal(
      groups.slice(2).filter((group) =>
        session.state.activeSetCountsBySelectionGroupId[group.id] === 2).length,
      expectedRepeatedSingles,
    );
    assert.ok(Object.values(session.state.activeSetCountsBySelectionGroupId)
      .every((setCount) => setCount >= 1 && setCount <= 2));
  });
}

test("kept multiblock sequence repeats before unkept single-block exercises", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const first = exercise(
    1,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    100,
  );
  const second = exercise(
    2,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    100,
  );
  first.sequenceBlocks = [
    { ...first.sequenceBlocks[0] },
    { ...second.sequenceBlocks[0] },
  ];
  second.sequenceBlocks = [];
  const singleBlockExercises = groups.slice(2).map((group, index) => exercise(
    100 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    100,
  ));
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[groups[0].id] = [first.id];
  const session = new WorkoutSession(
    [first, second, ...singleBlockExercises],
    state,
    () => 0,
  );

  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.equal(
    session.state.activeSetCountsBySelectionGroupId[groups[0].id],
    2,
  );
  assert.equal(
    groups.slice(2).filter((group) =>
      session.state.activeSetCountsBySelectionGroupId[group.id] === 2).length,
    13,
  );
  assert.ok(Object.values(session.state.activeSetCountsBySelectionGroupId)
    .every((setCount) => setCount >= 1 && setCount <= 2));
});

test("extra sets prefer keeps then hard exercises among unkept choices", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const exercises = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
  ));
  for (const candidate of exercises.slice(10, 25)) {
    candidate.muscularDemand = HARD_MUSCULAR_DEMAND;
  }
  const state = createDefaultState();
  for (let index = 0; index < 10; index += 1) {
    state.keptExerciseRootIdsBySelectionGroupId[groups[index].id] = [
      exercises[index].id,
    ];
  }
  const session = new WorkoutSession(exercises, state, () => 0);

  session.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.ok(groups.slice(0, 10).every((group) =>
    session.state.activeSetCountsBySelectionGroupId[group.id] === 2));
  assert.equal(
    groups.slice(10, 25).filter((group) =>
      session.state.activeSetCountsBySelectionGroupId[group.id] === 2).length,
    5,
  );
  assert.ok(groups.slice(25).every((group) =>
    session.state.activeSetCountsBySelectionGroupId[group.id] === 1));
  assert.equal(session.state.activeExtraSetSelectionGroupIds.length, 15);
});

for (const minutes of [3, 5, 7, 10, 15, 20, 30]) {
  test(`same-primary sequences yield to exact ${minutes}-minute block capacity`, () => {
    const exercises = directionPairCatalog();
    const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);

    assert.ok(session.getSelectionGroups().every((group) => {
      const selected = session.exercisesById.get(
        session.state.selectedExerciseIds[group.id],
      );
      return selected.sequenceBlocks.length === 1;
    }));
    assert.deepEqual(session.state.activeDirectionPartnerExerciseIds, {});
    assert.deepEqual(session.state.activeFullSideRoundIds, []);
  });
}

test("cross-primary atomic sequence fills two slots in a three-minute workout", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const qualified = (id, group, score) => exercise(
    id,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    score,
  );
  const first = qualified(1, groups[0], 100);
  first.sequenceBlocks = [
    { ...first.sequenceBlocks[0] },
    {
      exerciseId: 2,
      sideCue: "None",
      directionCue: "None",
      mirrorMedia: false,
      mediaSegment: "Full",
    },
  ];
  const second = qualified(2, groups[1], 100);
  second.sequenceBlocks = [];
  const exercises = [
    first,
    second,
    qualified(10, groups[0], 0),
    qualified(11, groups[1], 0),
    qualified(12, groups[2], 100),
  ];
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);

  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  const restored = new WorkoutSession(
    exercises,
    parseStoredState(JSON.stringify(session.state)),
    () => 0,
  );
  restored.initialize();

  assert.equal(restored.state.selectedExerciseIds[groups[0].id], first.id);
  assert.equal(restored.state.selectedExerciseIds[groups[1].id], first.id);
  const rounds = restored.getActiveGroups();
  assert.deepEqual(
    rounds.map((round) => restored.getSelectedExercise(round).id),
    [1, 2, 12],
  );
  assert.equal(getSelectionKey(rounds[0]), getSelectionKey(rounds[1]));
  assert.equal(restored.isIntermediateSequenceBlock(rounds[0]), true);
  assert.equal(restored.isIntermediateSequenceBlock(rounds[1]), false);

  restored.advanceSequence(rounds[0]);
  const restDeadline = Date.now() + 15_000;
  restored.beginRest(rounds[1], restDeadline);
  assert.equal(restored.getPendingRestGroup()?.id, rounds[1].id);
  assert.equal(restored.getPendingRestMillisecondsRemaining(restDeadline - 10_000), 10_000);
  assert.equal(restored.keepPendingRest(), true);

  const resumedRest = new WorkoutSession(exercises, parseStoredState(JSON.stringify(restored.state)), () => 0);
  resumedRest.initialize();
  assert.equal(resumedRest.getPendingRestGroup()?.id, rounds[1].id);
  assert.equal(resumedRest.state.pendingRestKept, true);
});

test("integration member keeps its rest when only the sequence targets the selected muscle", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const root = exercise(1, "ShoulderAbductors", ["RotatorCuff"], 100);
  root.sideSequence = "ScreenRightThenLeft";
  root.sequenceBlocks = [
    { exerciseId: 1, sideCue: "ScreenRight", directionCue: "None", mirrorMedia: false, mediaSegment: "Full" },
    { exerciseId: 1, sideCue: "ScreenLeft", directionCue: "None", mirrorMedia: true, mediaSegment: "Full" },
    { exerciseId: 2, sideCue: "None", directionCue: "None", mirrorMedia: false, mediaSegment: "Full" },
  ];
  const member = { ...exercise(2, "Chest", ["ShoulderAbductors"], 100), sequenceBlocks: [] };
  const exercises = [root, member, ...groups.map((group, index) =>
    exercise(index + 100, group.canonicalGroups[0], [], 0))];
  const state = createDefaultState();
  state.selectedExerciseIds["r30.rotator-cuff"] = root.id;
  state.keptExerciseRootIdsBySelectionGroupId["r30.rotator-cuff"] = [root.id];
  const session = new WorkoutSession(exercises, state, () => 0);
  session.startWorkout(60, WORKOUT_MODIFIERS.None);
  const target = session.getActiveGroups().findLast((group) =>
    session.getSelectedExercise(group).id === member.id &&
    !group.canonicalGroups.includes(member.primaryCanonicalGroup));
  assert.ok(target, "the admitted sequence must have an integration member outside its selected fine muscle");
  while (session.getNextGroup().id !== target.id) {
    const group = session.getNextGroup();
    if (session.isIntermediateSequenceBlock(group)) session.advanceSequence(group);
    else session.recordOutcome(group, true);
  }
  const restDeadline = Date.now() + 15_000;
  session.beginRest(target, restDeadline);
  assert.equal(session.getPendingRestGroup()?.id, target.id);
  assert.equal(session.getPendingRestMillisecondsRemaining(restDeadline - 10_000), 10_000);
  assert.equal(session.keepPendingRest(), true);
  const resumed = new WorkoutSession(exercises, parseStoredState(JSON.stringify(session.state)), () => 0);
  resumed.initialize();
  assert.equal(resumed.getPendingRestGroup()?.id, target.id);
  assert.equal(resumed.state.pendingRestKept, true);
});

test("cross-primary shuffle replaces every covered slot atomically", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const qualified = (id, group, score) => exercise(
    id,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    score,
  );
  const linked = (root, memberId) => {
    root.sequenceBlocks = root.id < memberId
      ? [
          { ...root.sequenceBlocks[0] },
          {
            exerciseId: memberId,
            sideCue: "None",
            directionCue: "None",
            mirrorMedia: false,
            mediaSegment: "Full",
          },
        ]
      : [];
    return root;
  };
  const first = linked(qualified(1, groups[0], 100), 2);
  const second = linked(qualified(2, groups[1], 100), 1);
  const replacementFirst = linked(qualified(3, groups[0], 0), 4);
  const replacementSecond = linked(qualified(4, groups[1], 0), 3);
  const third = qualified(5, groups[2], 100);
  const session = new WorkoutSession(
    [first, second, replacementFirst, replacementSecond, third],
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  const result = session.shuffleNextExercise(session.getActiveGroups()[0]);

  assert.equal(result.rejectedExercise.id, first.id);
  assert.equal(result.replacementExercise.id, replacementFirst.id);
  assert.equal(session.state.selectedExerciseIds[groups[0].id], replacementFirst.id);
  assert.equal(session.state.selectedExerciseIds[groups[1].id], replacementFirst.id);
  assert.equal(session.getScore(first), 100);
  assert.equal(session.getScore(second), 100);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.Warmup
    ][first.id],
    -1,
  );
  assert.deepEqual(session.state.exerciseScoreAdjustmentsBySelectionGroupId, {});
  assert.deepEqual(
    session.getActiveGroups().map((round) => session.getSelectedExercise(round).id),
    [replacementFirst.id, replacementSecond.id, third.id],
  );
});

test("cross-primary sequence uses each block's muscular recovery state", () => {
  const now = Date.UTC(2026, 7, 24, 12);
  const groups = RESOLUTIONS.get(3).groups;
  const qualified = (id, group) => exercise(
    id,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    0,
  );
  const first = qualified(1, groups[0]);
  first.sequenceBlocks.push({
    exerciseId: 2,
    sideCue: "None",
    directionCue: "None",
    mirrorMedia: false,
    mediaSegment: "Full",
  });
  const hardSecond = qualified(2, groups[1]);
  hardSecond.sequenceBlocks = [];
  hardSecond.muscularDemand = HARD_MUSCULAR_DEMAND;
  const firstFallback = qualified(3, groups[0]);
  const secondFallback = qualified(4, groups[1]);
  const third = qualified(5, groups[2]);
  const exercises = [first, hardSecond, firstFallback, secondFallback, third];

  const fresh = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );
  fresh.startWorkout(3, WORKOUT_MODIFIERS.None);
  assert.equal(fresh.state.selectedExerciseIds[groups[0].id], first.id);
  assert.equal(fresh.state.selectedExerciseIds[groups[1].id], first.id);

  const recoveringState = createDefaultState();
  recoveringState.lastHardWorkUnixMillisecondsByPrimaryMuscle = {
    [hardSecond.primaryCanonicalGroup]: now - 4 * 60 * 60 * 1000,
  };
  const recovering = new WorkoutSession(
    exercises,
    recoveringState,
    () => 0,
    () => now,
  );
  recovering.startWorkout(3, WORKOUT_MODIFIERS.None);
  assert.equal(
    recovering.state.selectedExerciseIds[groups[0].id],
    firstFallback.id,
  );
  assert.equal(
    recovering.state.selectedExerciseIds[groups[1].id],
    secondFallback.id,
  );
});

test("four-block side-by-direction sequences preserve independent cues", () => {
  const root = catalog.find((exercise) => exercise.id === 214);
  assert.deepEqual(
    root.sequenceBlocks.map((block) => ({
      exerciseId: block.exerciseId,
      sideCue: block.sideCue,
      mirrorMedia: block.mirrorMedia,
    })),
    [
      { exerciseId: 214, sideCue: "ScreenRight", mirrorMedia: false },
      { exerciseId: 214, sideCue: "ScreenLeft", mirrorMedia: true },
      { exerciseId: 755, sideCue: "ScreenRight", mirrorMedia: false },
      { exerciseId: 755, sideCue: "ScreenLeft", mirrorMedia: true },
    ],
  );
  assert.deepEqual(catalog.find((exercise) => exercise.id === 755).sequenceBlocks, []);
});

test("simultaneous and alternating bilateral movements remain one block", () => {
  const ownerByExerciseId = new Map();
  for (const root of catalog.filter((item) => item.sequenceBlocks.length > 0)) {
    for (const block of root.sequenceBlocks) {
      ownerByExerciseId.set(block.exerciseId, root);
    }
  }
  for (const exerciseId of [248, 394, 397, 421, 427, 468]) {
    const owner = ownerByExerciseId.get(exerciseId);
    assert.equal(
      owner.sequenceBlocks.filter((block) => block.exerciseId === exerciseId).length,
      1,
    );
  }
  assert.equal(catalog.find((exercise) => exercise.id === 421).sideSequence, "Continuous");
  for (const exerciseId of [248, 394, 397, 427, 468]) {
    assert.equal(
      catalog.find((exercise) => exercise.id === exerciseId).sideSequence,
      "Alternating",
    );
  }
});

test("shuffle rejects the current exercise and replaces only its slot", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.flatMap((group, index) => [
    exercise(1 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10),
    exercise(2 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 7),
    exercise(3 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 5),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const activeGroups = session.getActiveGroups();
  const current = activeGroups[0];
  const originalId = session.getSelectedExercise(current).id;
  const otherSelections = new Map(activeGroups.slice(1).map((group) => [
    group.id,
    session.getSelectedExercise(group).id,
  ]));
  const originalScores = new Map(exercises.map((item) => [item.id, item.score]));
  session.state.keptExerciseRootIdsBySelectionGroupId = {
    [getSelectionKey(current)]: [originalId],
  };
  session.syncLegacyKeptExerciseIds();
  session.state.selectedExerciseIds[
    `p${WORKOUT_MODIFIERS.Insect}|${getSelectionKey(current)}`
  ] = originalId;

  assert.equal(session.canShuffleNextExercise(current), true);
  const result = session.shuffleNextExercise(current);

  assert.ok(result);
  assert.equal(result.rejectedExercise.id, originalId);
  assert.notEqual(result.replacementExercise.id, originalId);
  assert.equal(
    session.getSelectedExercise(current).id,
    result.replacementExercise.id,
  );
  assert.deepEqual(session.state.outcomes, {});
  assert.deepEqual(result.scoreUpdates.map((item) => item.id), [originalId]);
  assert.equal(session.getScore(result.rejectedExercise), originalScores.get(originalId));
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      getWorkoutExercisePhase(current.order)
    ][originalId],
    -1,
  );
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(originalId));
  assert.equal(session.state.lastKeptExerciseIds.includes(originalId), true);
  assert.equal(
    Object.values(session.state.selectedExerciseIds).includes(originalId),
    false,
  );
  assert.ok(exercises
    .filter((item) => item.id !== originalId)
    .every((item) => item.score === originalScores.get(item.id)));
  assert.ok(activeGroups.slice(1).every((group) =>
    session.getSelectedExercise(group).id === otherSelections.get(group.id)));
  assert.equal(session.state.activeWorkoutSession.selectionChanges.length, 1);
  const change = session.state.activeWorkoutSession.selectionChanges[0];
  assert.equal(change.kind, "Shuffle");
  assert.ok(change.changedAtUnixMilliseconds > 0);
  assert.equal(change.selectionGroupId, getSelectionKey(current));
  assert.equal(change.rejectedRootExerciseId, originalId);
  assert.equal(change.rejectedRootExerciseName, result.rejectedExercise.name);
  assert.equal(change.rejectedSelectionScoreBeforeChange, originalScores.get(originalId));
  assert.equal(change.rejectedSelectionWasKeptAtWorkoutStart, false);
  assert.equal(change.replacementRootExerciseId, result.replacementExercise.id);
  assert.equal(change.replacementRootExerciseName, result.replacementExercise.name);
  assert.equal(
    change.replacementSelectionScore,
    originalScores.get(result.replacementExercise.id),
  );
});

test("shuffle does not offer another alias of the rejected movement", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const current = {
    ...exercise(
      1,
      groups[0].canonicalGroups[0],
      groups[0].canonicalGroups.slice(1),
      100,
    ),
    sessionMovementId: 1,
  };
  const alias = {
    ...exercise(
      2,
      groups[0].canonicalGroups[0],
      groups[0].canonicalGroups.slice(1),
      50,
    ),
    sessionMovementId: 1,
  };
  const alternative = exercise(
    3,
    groups[0].canonicalGroups[0],
    groups[0].canonicalGroups.slice(1),
    0,
  );
  const middle = exercise(
    4,
    groups[1].canonicalGroups[0],
    groups[1].canonicalGroups.slice(1),
    0,
  );
  const last = exercise(
    5,
    groups[2].canonicalGroups[0],
    groups[2].canonicalGroups.slice(1),
    0,
  );
  const session = new WorkoutSession(
    [current, alias, alternative, middle, last],
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);

  const result = session.shuffleNextExercise(session.getActiveGroups()[0]);

  assert.equal(result.rejectedExercise.id, current.id);
  assert.equal(result.replacementExercise.id, alternative.id);
});

test("shuffle visits every eligible alternative without repeating", () => {
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.flatMap((group, index) => [
    exercise(1 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 10),
    exercise(2 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 7),
    exercise(3 + index * 3, group.canonicalGroups[0], group.canonicalGroups.slice(1), 5),
  ]);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = session.getNextGroup();
  const visitedExerciseIds = new Set([session.getSelectedExercise(group).id]);

  let shuffleCount = 0;
  while (session.canShuffleNextExercise(group)) {
    const result = session.shuffleNextExercise(group);
    assert.equal(visitedExerciseIds.has(result.replacementExercise.id), false);
    visitedExerciseIds.add(result.replacementExercise.id);
    assert.deepEqual(session.state.outcomes, {});
    shuffleCount += 1;
    assert.ok(shuffleCount < 10);
  }

  assert.deepEqual([...visitedExerciseIds].sort((a, b) => a - b), [1, 2, 3]);
  const currentExerciseId = session.getSelectedExercise(group).id;
  assert.deepEqual(
    [...visitedExerciseIds]
      .filter((id) => id !== currentExerciseId)
      .sort((a, b) => a - b),
    session.state.nextWorkoutExcludedExerciseIds
      .filter((id) => visitedExerciseIds.has(id))
      .sort((a, b) => a - b),
  );
});

test("sequence shuffle rejects every member once and never starts mid-sequence", () => {
  const exercises = directionPairCatalog();
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(45, WORKOUT_MODIFIERS.None);
  const lead = session.getActiveGroups().find((round) =>
    session.state.selectedExerciseIds[getSelectionKey(round)] === 1 &&
    round.sequenceBlockIndex === 0 &&
    round.setNumber === 1);
  assert.ok(lead);
  for (const prior of session.getActiveGroups().filter((round) =>
    round.order < lead.order)) {
    session.state.outcomes[prior.id] = "tick";
  }

  assert.equal(session.canShuffleNextExercise(lead), true);
  const result = session.shuffleNextExercise(lead);
  assert.ok(result);
  assert.deepEqual(result.scoreUpdates.map((item) => item.id).sort(), [1, 2]);
  assert.equal(session.getScore(exercises[0]), 100);
  assert.equal(session.getScore(exercises[1]), 100);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      getWorkoutExercisePhase(lead.order)
    ]["1"],
    -1,
  );
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(1));
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(2));

  const continuation = {
    ...lead,
    sequenceBlockIndex: 1,
  };
  assert.equal(session.canShuffleNextExercise(continuation), false);
  assert.equal(session.shuffleNextExercise(continuation), null);
});

test("one keep decision is available only after the final sequence block", () => {
  const exercises = directionPairCatalog();
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(45, WORKOUT_MODIFIERS.None);
  const sequenceGroup = session.getSelectionGroups().find((group) =>
    session.state.selectedExerciseIds[group.id] === 1);
  const sequenceRounds = session.getActiveGroups().filter((round) =>
    getSelectionKey(round) === sequenceGroup.id);
  for (const prior of session.getActiveGroups().filter((round) =>
    round.order < sequenceRounds[0].order)) {
    session.state.outcomes[prior.id] = "tick";
  }

  assert.throws(
    () => session.recordOutcome(sequenceRounds[0], true),
    /final block/,
  );
  for (const round of sequenceRounds.slice(0, -1)) {
    assert.equal(session.getNextGroup().id, round.id);
    assert.equal(session.isIntermediateSequenceBlock(round), true);
    assert.equal(
      session.getNextSequenceBlock(round).id,
      sequenceRounds[sequenceRounds.indexOf(round) + 1].id,
    );
    session.advanceSequence(round);
  }
  const decisionRound = sequenceRounds.at(-1);
  assert.equal(session.getNextGroup().id, decisionRound.id);
  assert.equal(session.getNextSequenceBlock(decisionRound), null);
  assert.equal(isFinalSequenceRound(decisionRound), true);
  session.recordOutcome(decisionRound, true);

  assert.ok(sequenceRounds.slice(0, -1).every((round) =>
    session.state.outcomes[round.id] === "neutral"));
  assert.equal(session.state.outcomes[decisionRound.id], "tick");
  assert.equal(session.state.scores["1"], undefined);
  assert.equal(session.state.scores["2"], undefined);
});

test("pending intermediate rest restores the upcoming block without advancing", () => {
  const exercises = directionPairCatalog();
  const started = new WorkoutSession(exercises, createDefaultState(), () => 0);
  started.startWorkout(45, WORKOUT_MODIFIERS.None);
  const sequenceGroup = started.getSelectionGroups().find((group) =>
    started.state.selectedExerciseIds[group.id] === 1);
  const sequenceRounds = started.getActiveGroups().filter((round) =>
    getSelectionKey(round) === sequenceGroup.id);
  const completedBlock = sequenceRounds[0];
  for (const prior of started.getActiveGroups().filter((round) =>
    round.order < completedBlock.order)) {
    started.state.outcomes[prior.id] = "tick";
  }
  started.beginRest(completedBlock, Date.now() + 60_000);

  const restored = new WorkoutSession(
    exercises,
    parseStoredState(JSON.stringify(started.state)),
    () => 0,
  );
  restored.initialize();

  const pendingBlock = restored.getPendingRestGroup();
  const upcomingBlock = restored.getNextSequenceBlock(pendingBlock);
  assert.equal(pendingBlock.id, completedBlock.id);
  assert.equal(upcomingBlock.id, sequenceRounds[1].id);
  assert.equal(restored.getNextGroup().id, completedBlock.id);
  assert.equal(restored.state.outcomes[completedBlock.id], undefined);
});

test("rejecting any current block rejects the whole sequence with one member vote", () => {
  const exercises = directionPairCatalog();
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(45, WORKOUT_MODIFIERS.None);
  const firstRound = session.getActiveGroups().find((round) =>
    session.state.selectedExerciseIds[getSelectionKey(round)] === 1);
  for (const prior of session.getActiveGroups().filter((round) =>
    round.order < firstRound.order)) {
    session.state.outcomes[prior.id] = "tick";
  }

  session.rejectCurrentSequence(firstRound);

  const sequenceRounds = session.getActiveGroups().filter((round) =>
    getSelectionKey(round) === getSelectionKey(firstRound));
  assert.equal(session.getScore(exercises[0]), 100);
  assert.equal(session.getScore(exercises[1]), 100);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      getWorkoutExercisePhase(firstRound.order)
    ]["1"],
    -1,
  );
  assert.equal(
    session.state.activeWorkoutSession.decisions[0].exercisePhase,
    getWorkoutExercisePhase(firstRound.order),
  );
  assert.ok(sequenceRounds.slice(0, -1).every((round) =>
    session.state.outcomes[round.id] === "neutral"));
  assert.equal(session.state.outcomes[sequenceRounds.at(-1).id], "x");
  assert.notEqual(
    getSelectionKey(session.getNextGroup()),
    getSelectionKey(firstRound),
  );
});

test("legacy in-progress sided movement migrates without resetting its workout", () => {
  const exercises = directionPairCatalog();
  exercises[0].sideSequence = "ScreenRightThenLeft";
  exercises[0].sequenceBlocks = [
    {
      exerciseId: 1,
      sideCue: "ScreenRight",
      directionCue: "None",
      mirrorMedia: false,
      mediaSegment: "Full",
    },
    {
      exerciseId: 1,
      sideCue: "ScreenLeft",
      directionCue: "None",
      mirrorMedia: true,
      mediaSegment: "Full",
    },
  ];
  exercises[1].sequenceBlocks = [{
    exerciseId: 2,
    sideCue: "None",
    directionCue: "None",
    mirrorMedia: false,
    mediaSegment: "Full",
  }];
  exercises[1].score = -100;

  const started = new WorkoutSession(exercises, createDefaultState(), () => 0);
  started.startWorkout(45, WORKOUT_MODIFIERS.None);
  const sequenceGroup = started.getSelectionGroups().find((group) =>
    started.state.selectedExerciseIds[group.id] === 1);
  const sequenceRounds = started.getActiveGroups().filter((round) =>
    getSelectionKey(round) === sequenceGroup.id);
  const stored = JSON.parse(JSON.stringify(started.state));
  stored.version = 13;
  stored.outcomes = {};
  for (const selectionKey of new Set(started.getActiveGroups()
    .filter((round) => round.order < sequenceRounds[0].order)
    .map(getSelectionKey))) {
    stored.outcomes[`${selectionKey}.set1`] = "tick";
  }
  stored.pendingMovementGroupId = `${sequenceGroup.id}.set1`;
  stored.pendingMovementMillisecondsRemaining = 10_000;
  stored.pendingMovementEndsAtUnixMilliseconds = 0;
  stored.pendingMovementPausedByUser = true;
  stored.activeFullSideRoundIds = [];
  stored.activeDirectionPartnerExerciseIds = {};

  const restored = new WorkoutSession(exercises, stored, () => 0);
  restored.initialize();

  const pending = restored.getPendingMovementGroup();
  assert.equal(restored.state.activeWorkoutMinutes, 45);
  assert.equal(restored.state.version, CURRENT_WORKOUT_STATE_VERSION);
  assert.equal(pending.sequenceBlockIndex, 1);
  assert.equal(restored.state.pendingMovementMillisecondsRemaining, 35_000);
  const migratedSequenceRounds = restored.getActiveGroups().filter((round) =>
    getSelectionKey(round) === sequenceGroup.id);
  assert.equal(restored.state.outcomes[migratedSequenceRounds[0].id], "neutral");
  assert.equal(restored.state.workoutCompleted, false);
});

test("selection uses truthful associations and ranks score, primary, then coverage", () => {
  const group = {
    id: "test",
    displayName: "Test",
    order: 1,
    canonicalGroups: ["A", "B"],
  };
  const secondaryOnly = exercise(1, "C", ["A", "B"], 99);
  const highScore = exercise(2, "A", [], 3);
  const broadLowScore = exercise(3, "A", ["B"], 2);
  const broadEqualScore = exercise(4, "A", ["B"], 3);

  assert.equal(isSelectable(secondaryOnly, group), true);
  const session = new WorkoutSession(
    [secondaryOnly, highScore, broadLowScore, broadEqualScore],
    createDefaultState(),
    () => 0,
  );
  assert.equal(session.chooseBestCandidate(group).id, secondaryOnly.id);
  session.setScore(secondaryOnly, 3);
  assert.equal(session.chooseBestCandidate(group).id, broadEqualScore.id);
  assert.equal(getCanonicalCoverage(broadEqualScore, group), 2);
});

test("every block is 45 seconds and only the sequence entrance has preparation", () => {
  assert.deepEqual(getMovementPhaseState(50_000, true), {
    phase: "Preparation",
    secondsRemaining: 5,
    segmentDurationSeconds: 5,
    isExercise: false,
  });
  assert.deepEqual(getMovementPhaseState(45_000, true), {
    phase: "Continuous",
    secondsRemaining: 45,
    segmentDurationSeconds: 45,
    isExercise: true,
  });
  assert.deepEqual(getMovementPhaseState(45_000, false), {
    phase: "Continuous",
    secondsRemaining: 45,
    segmentDurationSeconds: 45,
    isExercise: true,
  });
  assert.deepEqual(getMovementPhaseState(1, false), {
    phase: "Continuous",
    secondsRemaining: 1,
    segmentDurationSeconds: 45,
    isExercise: true,
  });
  assert.deepEqual(getMovementPhaseState(0, false), {
    phase: "Complete",
    secondsRemaining: 0,
    segmentDurationSeconds: 0,
    isExercise: false,
  });
  assert.equal(getMovementDurationMs(), 45_000);
  assert.equal(getMovementCountdownDurationMs({}), 50_000);
  assert.equal(getMovementCountdownDurationMs({
    sequenceBlockIndex: 1,
    setNumber: 1,
  }), 45_000);
  assert.equal(getMovementCountdownDurationMs({
    sequenceBlockIndex: 0,
    setNumber: 2,
  }), 45_000);
});

test("sequence presentation composes side, direction, and mirroring independently", () => {
  const group = {
    sequenceSideCue: "ScreenRight",
    sequenceDirectionCue: "Inward",
    mirrorSequenceMedia: true,
  };
  assert.deepEqual(getMovementPresentation(group, "Continuous"), {
    sideCue: "ScreenRight",
    directionCue: "Inward",
    mirrorMedia: true,
  });
  assert.deepEqual(getMovementPresentation(group, "Complete"), {
    sideCue: "None",
    directionCue: "None",
    mirrorMedia: false,
  });
});

test("every catalog exercise has exactly one atomic sequence owner", () => {
  const ownership = new Map(catalog.map((exercise) => [exercise.id, []]));
  for (const root of catalog.filter((exercise) =>
    exercise.sequenceBlocks.length > 0)) {
    for (const memberId of new Set(root.sequenceBlocks.map((block) =>
      block.exerciseId))) {
      ownership.get(memberId)?.push(root.id);
    }
  }
  for (const [exerciseId, ownerIds] of ownership) {
    assert.equal(ownerIds.length, 1, `exercise ${exerciseId}`);
  }

  for (const rootId of [214, 223, 288, 617]) {
    assert.equal(catalog.find((exercise) => exercise.id === rootId)
      .sequenceBlocks.length, 4);
  }
  for (const rootId of [327, 414]) {
    assert.equal(catalog.find((exercise) => exercise.id === rootId)
      .sequenceBlocks.length, 2);
  }
  for (const rootId of [418, 546]) {
    assert.equal(catalog.find((exercise) => exercise.id === rootId)
      .sequenceBlocks.length, 1);
  }
});

test("keeps carry across workout duration resolutions", () => {
  const allCanonicalGroups = RESOLUTIONS.get(30).groups
    .flatMap((group) => group.canonicalGroups);

  for (const [previousMinutes, nextMinutes] of [
    [3, 5],
    [5, 3],
  ]) {
    const previousGroups = RESOLUTIONS.get(previousMinutes).groups;
    const nextGroups = RESOLUTIONS.get(nextMinutes).groups;
    const keptExercises = previousGroups.map((group, index) => {
      const primary = group.canonicalGroups[0];
      return exercise(
        index + 1,
        primary,
        allCanonicalGroups.filter((candidate) => candidate !== primary),
        0,
      );
    });
    const nextDurationAlternatives = nextGroups.map((group, index) => {
      const primary = group.canonicalGroups[0];
      return exercise(
        101 + index,
        primary,
        allCanonicalGroups.filter((candidate) => candidate !== primary),
        10,
      );
    });
    const state = createDefaultState();
    for (const [index, group] of previousGroups.entries()) {
      state.selectedExerciseIds[group.id] = keptExercises[index].id;
      state.keptExerciseRootIdsBySelectionGroupId[group.id] = [
        keptExercises[index].id,
      ];
    }
    const session = new WorkoutSession(
      [...keptExercises, ...nextDurationAlternatives],
      state,
      () => 0,
    );

    session.initialize();

    for (const [index, group] of nextGroups.entries()) {
      session.state.selectedExerciseIds[group.id] = nextDurationAlternatives[index].id;
    }
    session.startWorkout(nextMinutes, WORKOUT_MODIFIERS.None);

    const keptExerciseIds = new Set(keptExercises.map((item) => item.id));
    const selectedExerciseIds = nextGroups.map(
      (group) => session.state.selectedExerciseIds[group.id],
    );
    assert.equal(session.state.lastKeptExerciseIds.length, previousMinutes);
    assert.equal(
      selectedExerciseIds.filter((exerciseId) => keptExerciseIds.has(exerciseId)).length,
      Math.min(previousMinutes, nextMinutes),
    );
    assert.ok(Object.keys(session.state.keptExerciseRootIdsBySelectionGroupId)
      .some((selectionGroupId) => selectionGroupId.startsWith(
        `r${previousMinutes}.`,
      )));
    const targetKeepIds = Object.entries(
      session.state.keptExerciseRootIdsBySelectionGroupId,
    ).filter(([selectionGroupId]) => selectionGroupId.startsWith(
      `r${nextMinutes}.`,
    )).flatMap(([, rootIds]) => rootIds);
    assert.ok(selectedExerciseIds
      .filter((exerciseId) => keptExerciseIds.has(exerciseId))
      .every((exerciseId) => targetKeepIds.includes(exerciseId)));
    assert.equal(new Set(selectedExerciseIds).size, nextMinutes);
  }
});

test("a phase-specific rejection does not erase an existing keep", () => {
  const phaseCatalog = RESOLUTIONS.get(3).groups.flatMap((group, index) => [
    exercise(10_001 + index * 2, group.canonicalGroups[0],
      group.canonicalGroups.slice(1), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(10_002 + index * 2, group.canonicalGroups[0],
      group.canonicalGroups.slice(1), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
  ]);
  const session = new WorkoutSession(
    phaseCatalog,
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const keptRound = session.getActiveGroups().at(-1);
  const kept = session.getSelectedExercise(keptRound);
  session.state.keptExerciseRootIdsBySelectionGroupId = {
    [getSelectionKey(keptRound)]: [session.getSequenceRoot(kept).id],
  };
  session.syncLegacyKeptExerciseIds();

  session.finishInterruptedWorkout();

  assert.equal(session.state.lastKeptExerciseIds.includes(kept.id), true);

  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  for (const round of session.getActiveGroups()) {
    session.recordOutcome(round, session.getSelectedExercise(round).id !== kept.id);
  }
  session.acknowledgeCompletion();

  assert.equal(session.state.lastKeptExerciseIds.includes(kept.id), true);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.Warmup
    ][session.getSequenceRoot(kept).id],
    -1,
  );
});

test("rejection records phase feedback and purges only its current lineup slot", () => {
  const phaseCatalog = RESOLUTIONS.get(3).groups.flatMap((group, index) => [
    exercise(10_011 + index * 2, group.canonicalGroups[0],
      group.canonicalGroups.slice(1), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
    exercise(10_012 + index * 2, group.canonicalGroups[0],
      group.canonicalGroups.slice(1), 0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible),
  ]);
  const session = new WorkoutSession(
    phaseCatalog,
    createDefaultState(),
    () => 0,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const groups = session.getActiveGroups();
  const rejectedGroup = groups.find((group) =>
    getSelectionKey(group) === RESOLUTIONS.get(3).groups[0].id);
  const rejected = session.getSelectedExercise(rejectedGroup);
  const rejectedSlotId = getSelectionKey(rejectedGroup);
  const keptIds = new Map(
    groups
      .filter((group) => getSelectionKey(group) !== rejectedSlotId)
      .map((group) => [
        getSelectionKey(group),
        session.getSelectedExercise(group).id,
      ]),
  );
  const canonicalGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes(rejected.primaryCanonicalGroup),
  );
  session.state.selectedExerciseIds[canonicalGroup.id] = rejected.id;
  const insectRejectedSlotKey =
    `p${WORKOUT_MODIFIERS.Insect}|${rejectedSlotId}`;
  session.state.selectedExerciseIds[insectRejectedSlotKey] = rejected.id;

  for (const group of groups) {
    session.recordOutcome(group, getSelectionKey(group) !== rejectedSlotId);
  }
  assert.equal(session.getScore(rejected), 0);
  assert.equal(
    session.state.exerciseScoreAdjustmentsByPhase[
      getWorkoutExercisePhase(rejectedGroup.order)
    ][session.getSequenceRoot(rejected).id],
    -1,
  );
  session.acknowledgeCompletion();

  assert.equal(session.state.selectedExerciseIds[insectRejectedSlotKey], undefined);
  assert.equal(session.state.selectedExerciseIds[canonicalGroup.id], rejected.id);
  for (const [groupId, exerciseId] of keptIds) {
    assert.equal(session.state.selectedExerciseIds[groupId], exerciseId);
  }
  assert.equal(session.state.activeWorkoutMinutes, 0);
});

test("interrupted movement is neutral while an explicitly abandoned rest settles once", () => {
  const neutral = new WorkoutSession(catalog, createDefaultState(), () => 0);
  neutral.startWorkout(3, WORKOUT_MODIFIERS.None);
  const neutralGroup = neutral.getNextGroup();
  const neutralExercise = neutral.getSelectedExercise(neutralGroup);
  neutral.finishInterruptedWorkout();
  assert.equal(neutral.getScore(neutralExercise), 0);
  assert.equal(neutral.state.activeWorkoutMinutes, 0);

  const rejected = new WorkoutSession(catalog, createDefaultState(), () => 0);
  rejected.startWorkout(3, WORKOUT_MODIFIERS.None);
  const rejectedGroup = rejected.getNextGroup();
  const rejectedExercise = rejected.getSelectedExercise(rejectedGroup);
  rejected.beginRest(rejectedGroup, Date.now() + 15_000);
  const serialized = JSON.stringify(rejected.state);

  const restored = new WorkoutSession(catalog, parseStoredState(serialized), () => 0);
  restored.initialize();
  assert.equal(restored.getPendingRestGroup()?.id, rejectedGroup.id);
  assert.equal(restored.getScore(rejectedExercise), 0);
  restored.finishInterruptedWorkout();
  assert.equal(restored.getScore(rejectedExercise), 0);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      getWorkoutExercisePhase(rejectedGroup.order)
    ][restored.getSequenceRoot(rejectedExercise).id],
    -1,
  );
  assert.equal(restored.state.activeWorkoutMinutes, 0);

  const restoredAgain = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(restored.state)),
    () => 0,
  );
  restoredAgain.initialize();
  assert.equal(restoredAgain.getScore(rejectedExercise), 0);
  assert.equal(
    restoredAgain.state.exerciseScoreAdjustmentsByPhase[
      getWorkoutExercisePhase(rejectedGroup.order)
    ][restoredAgain.getSequenceRoot(rejectedExercise).id],
    -1,
  );
});

test("pending rest survives schedule order and coverage changes for the performed exercise", () => {
  const started = new WorkoutSession(catalog, createDefaultState(), () => 0);
  started.initialize();
  started.startWorkout(3, WORKOUT_MODIFIERS.None);
  const activeGroups = started.getActiveGroups();
  const pendingGroup = activeGroups.at(-1);
  for (const completedGroup of activeGroups.slice(0, -1)) {
    started.recordOutcome(completedGroup, true);
  }
  const performed = started.getSelectedExercise(pendingGroup);
  started.beginRest(pendingGroup, Date.now() + 15_000);

  const retainedAssignment = pendingGroup.canonicalGroups.includes(
    performed.primaryCanonicalGroup,
  )
    ? []
    : [pendingGroup.canonicalGroups[0]];
  const changedCatalog = catalog.map((item) =>
    item.id === performed.id
      ? { ...item, secondaryCanonicalGroups: retainedAssignment }
      : item,
  );
  assert.equal(isSelectable(changedCatalog.find((item) => item.id === performed.id), pendingGroup), false);

  const restored = new WorkoutSession(
    changedCatalog,
    parseStoredState(JSON.stringify(started.state)),
    () => 0,
  );
  restored.initialize();

  assert.equal(restored.getPendingRestGroup()?.id, pendingGroup.id);
  assert.equal(restored.getScore(performed), 0);
  restored.finishInterruptedWorkout();
  assert.equal(restored.getScore(performed), 0);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      getWorkoutExercisePhase(pendingGroup.order)
    ][restored.getSequenceRoot(performed).id],
    -1,
  );
  assert.equal(restored.state.activeWorkoutMinutes, 0);
});

test("catalog identity replacement clears inherited score and workout references", () => {
  const started = new WorkoutSession(catalog, createDefaultState(), () => 0);
  started.initialize();
  started.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = started.getNextGroup();
  const retired = started.getSelectedExercise(group);
  const retiredRoot = started.getSequenceRoot(retired);
  const keptExerciseIds = started.getSequenceExercises(retiredRoot)
    .map((exercise) => exercise.id)
    .sort((left, right) => left - right);
  started.setScore(retired, -4);
  started.beginRest(group, Date.now() + 15_000);
  started.state.keptExerciseRootIdsBySelectionGroupId = {
    [getSelectionKey(group)]: [retiredRoot.id],
  };
  started.syncLegacyKeptExerciseIds();

  const changedCatalog = catalog.map((item) =>
    item.id === retired.id
      ? { ...item, name: `${item.name} Reviewed Replacement` }
      : item,
  );
  const restored = new WorkoutSession(
    changedCatalog,
    parseStoredState(JSON.stringify(started.state)),
    () => 0,
  );
  restored.initialize();

  assert.equal(restored.state.scores[String(retired.id)], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.activeWorkoutMinutes, 3);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
  assert.deepEqual(
    [...restored.state.lastKeptExerciseIds].sort((left, right) => left - right),
    keptExerciseIds,
  );
  assert.equal(restored.getScore(changedCatalog.find((item) => item.id === retired.id)), 0);
});

test("approved timed-side name cleanup preserves browser memory", () => {
  const normalized = catalog.find(
    (item) =>
      item.sequenceBlocks.length > 1 &&
      !item.name.startsWith("Alternating "),
  );
  const group = RESOLUTIONS.get(30).groups.find((candidate) => isSelectable(normalized, candidate));
  const priorCatalog = catalog.map((item) =>
    item.id === normalized.id
      ? { ...item, name: `Alternating ${item.name}` }
      : item,
  );
  const prior = new WorkoutSession(priorCatalog, createDefaultState(), () => 0);
  prior.initialize();
  prior.state.selectedExerciseIds[group.id] = normalized.id;
  prior.setScore(priorCatalog.find((item) => item.id === normalized.id), -3);

  const restored = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(prior.state)),
    () => 0,
  );
  restored.initialize();

  assert.equal(restored.state.selectedExerciseIds[group.id], normalized.id);
  assert.equal(restored.getScore(normalized), -3);
});

test("approved clarity corrections preserve browser memory", () => {
  for (const [exerciseId, [previousName, currentName]] of APPROVED_EXERCISE_CORRECTIONS) {
    if (exerciseId === 500) {
      continue;
    }
    const currentCatalog = catalog.map((item) =>
      item.id === exerciseId ? { ...item, name: currentName } : item,
    );
    const priorCatalog = currentCatalog.map((item) =>
      item.id === exerciseId ? { ...item, name: previousName } : item,
    );
    const currentExercise = currentCatalog.find((item) => item.id === exerciseId);
    const group = RESOLUTIONS.get(30).groups.find((candidate) =>
      isSelectable(currentExercise, candidate),
    );
    const prior = new WorkoutSession(priorCatalog, createDefaultState(), () => 0);
    prior.initialize();
    prior.state.selectedExerciseIds[group.id] = exerciseId;
    prior.setScore(priorCatalog.find((item) => item.id === exerciseId), -3);

    const restored = new WorkoutSession(
      currentCatalog,
      parseStoredState(JSON.stringify(prior.state)),
      () => 0,
    );
    restored.initialize();

    const isSequenceMemberOnly = currentExercise.sequenceBlocks.length === 0;
    const isUnavailableWithoutMirror = currentExercise.mirrorRelationship ===
      EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly;
    const isUnavailableWithoutWall = currentExercise.wallRequired;
    const differentAction = SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(73).has(exerciseId) ||
      DISCARDED_EXERCISE_IDENTITY_NAMES.get(exerciseId)?.has(previousName) === true;
    assert.equal(
      restored.state.selectedExerciseIds[group.id],
      differentAction || isSequenceMemberOnly || isUnavailableWithoutMirror || isUnavailableWithoutWall
        ? undefined
        : exerciseId,
    );
    assert.equal(restored.getScore(currentExercise), differentAction ? 0 : -3);
  }
});

test("an in-progress movement restores with its exact paused time", () => {
  const now = Date.UTC(2026, 7, 23, 2, 0, 0);
  const started = new WorkoutSession(catalog, createDefaultState(), () => 0);
  started.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = started.getNextGroup();
  started.beginMovement(group, 42_000, now + 42_000);

  const restoredRunning = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(started.state)),
    () => 0,
  );
  restoredRunning.initialize();
  assert.equal(restoredRunning.state.activeWorkoutMinutes, 3);
  assert.equal(restoredRunning.getPendingMovementGroup()?.id, group.id);
  assert.equal(
    restoredRunning.getPendingMovementMillisecondsRemaining(now + 2_000),
    40_000,
  );
  assert.equal(
    restoredRunning.getPendingMovementMillisecondsRemaining(now + 120_000),
    42_000,
  );

  restoredRunning.pauseMovement(group, 31_123, false);
  const restoredPaused = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(restoredRunning.state)),
    () => 0,
  );
  restoredPaused.initialize();

  assert.equal(restoredPaused.state.activeWorkoutMinutes, 3);
  assert.equal(restoredPaused.state.pendingMovementGroupId, group.id);
  assert.equal(restoredPaused.state.pendingMovementMillisecondsRemaining, 31_123);
  assert.equal(restoredPaused.state.pendingMovementEndsAtUnixMilliseconds, 0);
  assert.equal(restoredPaused.state.pendingMovementPausedByUser, false);
  assert.equal(
    restoredPaused.getPendingMovementMillisecondsRemaining(now + 7_200_000),
    31_123,
  );
});

test("a pending rest remains resumable after state restoration", () => {
  const now = Date.UTC(2026, 7, 23, 2, 0, 0);
  const started = new WorkoutSession(catalog, createDefaultState(), () => now);
  started.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = started.getNextGroup();
  started.beginRest(group, now + REST_DURATION_MS);

  const restored = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(started.state)),
    () => now,
  );
  restored.initialize();

  assert.equal(restored.getPendingRestGroup()?.id, group.id);
  assert.equal(restored.state.pendingRestEndsAtUnixMilliseconds, now + REST_DURATION_MS);
  assert.equal(restored.state.activeWorkoutMinutes, 3);
});

test("a user-paused rest freezes across persistence until explicitly resumed", () => {
  const now = Date.UTC(2026, 7, 23, 2, 0, 0);
  const started = new WorkoutSession(catalog, createDefaultState(), () => now);
  started.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = started.getNextGroup();
  started.beginRest(group, now + REST_DURATION_MS);

  started.pauseRest(group, 9_876);

  assert.equal(started.state.pendingRestPausedByUser, true);
  assert.equal(started.state.pendingRestEndsAtUnixMilliseconds, 0);
  assert.equal(started.state.pendingRestMillisecondsRemaining, 9_876);
  assert.equal(
    started.getPendingRestMillisecondsRemaining(now + 14_400_000),
    9_876,
  );

  const restored = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(started.state)),
    () => now,
  );
  restored.initialize();

  assert.equal(restored.getPendingRestGroup()?.id, group.id);
  assert.equal(restored.state.pendingRestPausedByUser, true);
  assert.equal(
    restored.getPendingRestMillisecondsRemaining(now + 86_400_000),
    9_876,
  );

  const resumedAt = now + 86_400_000;
  restored.resumeRest(group, resumedAt + 9_876);

  assert.equal(restored.state.pendingRestPausedByUser, false);
  assert.equal(restored.state.pendingRestMillisecondsRemaining, 0);
  assert.equal(restored.state.pendingRestEndsAtUnixMilliseconds, resumedAt + 9_876);
  assert.equal(
    restored.getPendingRestMillisecondsRemaining(resumedAt + 2_000),
    7_876,
  );
});

test("clearing a paused rest clears every pause checkpoint field", () => {
  const now = Date.UTC(2026, 7, 23, 2, 0, 0);
  const session = new WorkoutSession(catalog, createDefaultState(), () => now);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = session.getNextGroup();
  session.beginRest(group, now + REST_DURATION_MS);
  session.pauseRest(group, 8_000);

  session.clearPendingRest();

  assert.equal(session.state.pendingRestGroupId, null);
  assert.equal(session.state.pendingRestEndsAtUnixMilliseconds, 0);
  assert.equal(session.state.pendingRestMillisecondsRemaining, 0);
  assert.equal(session.state.pendingRestPausedByUser, false);
  assert.equal(session.state.pendingRestKept, false);
});

test("beginning rest clears the completed movement resume checkpoint", () => {
  const now = Date.UTC(2026, 7, 23, 2, 0, 0);
  const session = new WorkoutSession(catalog, createDefaultState(), () => 0);
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const group = session.getNextGroup();
  session.beginMovement(group, 50_000, now + 50_000);

  session.beginRest(group, now + 15_000);

  assert.equal(session.state.pendingMovementGroupId, null);
  assert.equal(session.state.pendingMovementMillisecondsRemaining, 0);
  assert.equal(session.state.pendingMovementEndsAtUnixMilliseconds, 0);
  assert.equal(session.state.pendingMovementPausedByUser, false);
  assert.equal(session.state.pendingRestGroupId, group.id);
});

test("second clarity corrections preserve earlier browser memory", () => {
  for (const [exerciseId, previousNames] of
    ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES) {
    const currentName = APPROVED_EXERCISE_CORRECTIONS.get(exerciseId)[1];
    const currentCatalog = catalog.map((item) =>
      item.id === exerciseId ? { ...item, name: currentName } : item,
    );
    const currentExercise = currentCatalog.find((item) => item.id === exerciseId);
    const group = RESOLUTIONS.get(30).groups.find((candidate) =>
      isSelectable(currentExercise, candidate),
    );

    for (const previousName of previousNames) {
      const priorCatalog = currentCatalog.map((item) =>
        item.id === exerciseId ? { ...item, name: previousName } : item,
      );
      const prior = new WorkoutSession(priorCatalog, createDefaultState(), () => 0);
      prior.initialize();
      prior.state.selectedExerciseIds[group.id] = exerciseId;
      prior.setScore(priorCatalog.find((item) => item.id === exerciseId), -4);

      const restored = new WorkoutSession(
        currentCatalog,
        parseStoredState(JSON.stringify(prior.state)),
        () => 0,
      );
      restored.initialize();

      const owner = currentCatalog.find((item) => item.sequenceBlocks
        .some((block) => block.exerciseId === exerciseId));
      const isUnavailableWithoutMirror = currentExercise.mirrorRelationship ===
        EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly;
      const isUnavailableWithoutWall = currentExercise.wallRequired;
      const differentAction = SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(73).has(exerciseId) ||
        DISCARDED_EXERCISE_IDENTITY_NAMES.get(exerciseId)?.has(previousName) === true;
      assert.equal(
        restored.state.selectedExerciseIds[group.id],
        !differentAction && owner.id === exerciseId &&
          !isUnavailableWithoutMirror &&
          !isUnavailableWithoutWall
          ? exerciseId
          : undefined,
      );
      assert.equal(restored.getScore(currentExercise), differentAction ? 0 : -4);
    }
  }
});

test("catalog revision retires only exercises changed by that revision", () => {
  const latestReplacementIds = new Set(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION]
      .filter(([revision]) => revision > 12)
      .flatMap(([, exerciseIds]) => [...exerciseIds]),
  );
  const replacements = catalog.filter((item) =>
    typeof item.retiredName === "string" && item.retiredName,
  );
  const replacement = catalog.find((item) => item.id === 223);
  const historicalReplacement = replacements.find((item) => item.id === 591);
  const group = RESOLUTIONS.get(30).groups.find((candidate) =>
    isSelectable(replacement, candidate),
  );
  const state = createDefaultState();
  state.catalogRevision = 12;
  state.activeWorkoutMinutes = 30;
  state.selectedExerciseIds[group.id] = replacement.id;
  for (const item of replacements) {
    state.scores[String(item.id)] = -4;
  }
  state.scores[String(replacement.id)] = -4;
  state.outcomes[group.id] = "tick";
  state.pendingRestGroupId = group.id;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 15_000;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[group.id], undefined);
  for (const item of replacements) {
    assert.equal(
      restored.state.scores[String(item.id)],
      latestReplacementIds.has(item.id) ? undefined : -4,
    );
  }
  assert.equal(restored.state.scores[String(replacement.id)], undefined);
  assert.equal(restored.state.scores[String(historicalReplacement.id)], -4);
  assert.equal(restored.state.outcomes[group.id], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("revision workout invalidations preserve scores for unchanged exercise identities", () => {
  const workoutOnlyIds = [119, 140, 340];
  const state = createDefaultState();
  state.catalogRevision = 17;

  for (const exerciseId of workoutOnlyIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of workoutOnlyIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(restored.state.scores[String(exerciseId)], -4);
  }
});

test("revision semantic replacements reset scores", () => {
  const semanticReplacementIds = [115, 212, 260, 512, 649];
  const state = createDefaultState();
  state.catalogRevision = 17;

  for (const exerciseId of semanticReplacementIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of semanticReplacementIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
});

test("unilateral timing revision rebuilds workouts without resetting scores", () => {
  const timingCorrectionIds = [
    31, 219, 248, 282, 390, 394, 395,
    397, 508, 576, 577, 618, 816, 834,
  ];
  const state = createDefaultState();
  state.catalogRevision = 24;

  for (const exerciseId of timingCorrectionIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(25), false);
  const laterSemanticCorrections = new Set([31, 219, 395, 577, 618, 834]);
  for (const exerciseId of timingCorrectionIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(
      restored.state.scores[String(exerciseId)],
      laterSemanticCorrections.has(exerciseId) ? undefined : -4,
    );
  }
});

test("illustration correction revision rebuilds workouts without resetting scores", () => {
  const correctedIds = [31, 282, 391, 507, 508, 577];
  const state = createDefaultState();
  state.catalogRevision = 25;

  for (const exerciseId of correctedIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(26), false);
  const laterSemanticCorrections = new Set([31, 507, 577]);
  for (const exerciseId of correctedIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(
      restored.state.scores[String(exerciseId)],
      laterSemanticCorrections.has(exerciseId) ? undefined : -4,
    );
  }
});

test("karate demonstration correction rebuilds workouts and resets only the replacement score", () => {
  const correctedIds = [231, 685, 687];
  const state = createDefaultState();
  state.catalogRevision = 26;

  for (const exerciseId of correctedIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.deepEqual([...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(27)], [687]);
  for (const exerciseId of correctedIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
  }
  assert.equal(restored.state.scores["231"], -4);
  assert.equal(restored.state.scores["685"], -4);
  assert.equal(restored.state.scores["687"], undefined);
});

test("forward-fold replacement revision rebuilds workouts and resets its score", () => {
  const state = createDefaultState();
  state.catalogRevision = 27;
  state.selectedExerciseIds["changed.251"] = 251;
  state.scores["251"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.deepEqual([...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(28)], [251]);
  assert.equal(restored.state.selectedExerciseIds["changed.251"], undefined);
  assert.equal(restored.state.scores["251"], undefined);
});

test("reactivated replacement revision drops only changed progress and scores", () => {
  const changedIds = [
    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
    486, 487, 488, 489, 494, 496, 517, 518, 519,
  ];
  const changedIdSet = new Set(changedIds);
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(29)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(29)],
    changedIds,
  );

  const groups = RESOLUTIONS.get(30).groups;
  const changedExercise = catalog.find((exercise) => exercise.id === changedIds[0]);
  const changedGroup = groups.find((group) => isSelectable(changedExercise, group));
  const retainedExercise = catalog.find((exercise) => exercise.id === 15);
  const retainedGroup = groups.find((group) =>
    group.id !== changedGroup.id && isSelectable(retainedExercise, group));
  const retainedSequenceExerciseIds = [...new Set(
    retainedExercise.sequenceBlocks.map((block) => block.exerciseId),
  )];
  const changedSelectionKey = getSelectionKey(changedGroup);
  const retainedSelectionKey = getSelectionKey(retainedGroup);
  const state = createDefaultState();
  state.catalogRevision = 28;
  state.activeWorkoutMinutes = 30;
  state.selectedExerciseIds[changedSelectionKey] = changedExercise.id;
  state.selectedExerciseIds[retainedSelectionKey] = retainedExercise.id;
  state.outcomes[changedGroup.id] = "x";
  state.outcomes[retainedGroup.id] = "tick";
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 15_000;
  state.pendingRestKept = true;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [changedSelectionKey]: [changedExercise.id],
    [retainedSelectionKey]: [retainedExercise.id],
  };
  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -4;
  }
  state.scores[String(retainedExercise.id)] = -7;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedSelectionKey], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(
    restored.state.selectedExerciseIds[retainedSelectionKey],
    retainedExercise.id,
  );
  assert.equal(restored.state.outcomes[retainedGroup.id], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestEndsAtUnixMilliseconds, 0);
  assert.equal(restored.state.pendingRestKept, false);
  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
  assert.equal(restored.state.scores[String(retainedExercise.id)], -7);
  assert.deepEqual(
    new Set(restored.state.lastKeptExerciseIds),
    new Set([changedExercise.id, ...retainedSequenceExerciseIds]),
  );
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("media repair revision retires invalid assets without resetting media-only scores", () => {
  const changedIds = [
    229, 467, 474, 481, 483, 491, 493, 495, 497, 499,
    501, 504, 513, 516,
  ];
  const semanticIds = [229, 497, 501, 504, 513];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(30)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(30)],
    semanticIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 29;
  state.selectedExerciseIds["changed.media"] = 467;
  state.selectedExerciseIds["changed.retired"] = 229;
  state.scores["467"] = -7;
  state.scores["501"] = -6;
  state.scores["229"] = -5;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds["changed.media"], undefined);
  assert.equal(restored.state.selectedExerciseIds["changed.retired"], undefined);
  assert.equal(restored.state.scores["467"], -7);
  assert.equal(restored.state.scores["501"], undefined);
  assert.equal(restored.state.scores["229"], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
  assert.equal(catalog.some((exercise) => exercise.id === 229), false);
  assert.equal(catalog.find((exercise) => exercise.id === 497)?.mirrorRelationship,
    EXERCISE_MIRROR_RELATIONSHIP.Agnostic);
});

test("heel illustration correction revision resets changed workout and scores", () => {
  const changedIds = [414, 415, 416, 418, 419];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(31)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(31)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 30;
  state.activeWorkoutMinutes = 30;
  const changedExercise = catalog.find((exercise) => exercise.id === 414);
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    isSelectable(changedExercise, group));
  state.selectedExerciseIds[changedGroup.id] = changedExercise.id;
  state.outcomes[changedGroup.id] = "x";
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = 123456;
  state.pendingRestKept = true;
  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -exerciseId;
  }
  const retainedExercise = catalog.find((exercise) => !changedIds.includes(exercise.id));
  state.scores[String(retainedExercise.id)] = -7;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestEndsAtUnixMilliseconds, 0);
  assert.equal(restored.state.pendingRestKept, false);
  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
  assert.equal(restored.state.scores[String(retainedExercise.id)], -7);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("single-side clarity revision resets corrected replacements", () => {
  const correctedReplacementIds = [31, 219, 395, 507, 577, 618, 654, 834];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(32)],
    correctedReplacementIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(32)],
    correctedReplacementIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 31;
  state.selectedExerciseIds["changed.knee-pull"] = 31;
  state.selectedExerciseIds["changed.high-knee-reach"] = 618;
  state.selectedExerciseIds["name-only"] = 914;
  state.scores["31"] = -3;
  state.scores["618"] = -2;
  state.scores["914"] = -1;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds["changed.knee-pull"], undefined);
  assert.equal(
    restored.state.selectedExerciseIds["changed.high-knee-reach"],
    undefined,
  );
  assert.equal(restored.state.selectedExerciseIds["name-only"], 914);
  assert.equal(restored.state.scores["31"], undefined);
  assert.equal(restored.state.scores["618"], undefined);
  assert.equal(restored.state.scores["914"], -1);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("direction split revision resets every linked identity", () => {
  const linkedDirectionIds = [
    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(33)],
    linkedDirectionIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(33)],
    linkedDirectionIds,
  );
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 32;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 264;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  for (const exerciseId of linkedDirectionIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  for (const exerciseId of linkedDirectionIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("alternating correction rebuilds workout without resetting scores", () => {
  const correctedIds = [98, 390, 508, 576, 816];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(34)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(34), false);
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 33;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 576;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["576"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["576"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("high-knee alternation correction rebuilds workout without resetting scores", () => {
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(35)],
    [219],
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(35), false);
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 34;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 219;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["219"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["219"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("vague elbow-strike replacement rebuilds workout and resets its score", () => {
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(36)],
    [684],
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(36)],
    [684],
  );
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 35;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 684;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["684"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["684"], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("alternating loop corrections rebuild workouts without resetting scores", () => {
  const correctedIds = [31, 176, 195, 391, 413, 884, 885];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(37)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(37), false);
  const state = createDefaultState();
  const groups = RESOLUTIONS.get(3).groups;
  const changedGroup = groups[0].id;
  const retainedGroup = groups[1].id;
  state.catalogRevision = 36;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 884;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["884"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["884"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("direction name correction preserves scores across later placement rebuild", () => {
  assert.equal(SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.has(38), false);
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(38), false);
  assert.equal(SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.has(40), false);
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(40), false);
  const state = createDefaultState();
  const groupId = RESOLUTIONS.get(3).groups[0].id;
  state.catalogRevision = 37;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[groupId] = 223;
  state.outcomes[groupId] = "tick";
  state.pendingRestGroupId = groupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["223"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[groupId], undefined);
  assert.equal(restored.state.outcomes[groupId], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestKept, false);
  assert.equal(restored.state.scores["223"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("mirror-only correction drops stale selection but preserves score", () => {
  assert.deepEqual([...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(41)], [500]);
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(41), false);
  const state = createDefaultState();
  const groupId = RESOLUTIONS.get(3).groups[0].id;
  state.catalogRevision = 40;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[groupId] = 500;
  state.outcomes[groupId] = "tick";
  state.pendingRestGroupId = groupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["500"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[groupId], undefined);
  assert.equal(restored.state.outcomes[groupId], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["500"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("mirror relationship and muscle corrections rebuild workout without resetting scores", () => {
  const correctedIds = [105, 107, 108, 245, 280, 591, 884, 885, 905];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(42)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(42), false);
  const state = createDefaultState();
  const changedGroup = RESOLUTIONS.get(3).groups[0].id;
  const retainedGroup = RESOLUTIONS.get(3).groups[1].id;
  state.catalogRevision = 41;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[changedGroup] = 884;
  state.selectedExerciseIds[retainedGroup] = 22;
  state.outcomes[changedGroup] = "x";
  state.outcomes[retainedGroup] = "tick";
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["884"] = -4;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup], 22);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["884"], -4);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("genuine mirror practice revision retires duplicate and preserves corrected scores", () => {
  const changedIds = [90, 94, 95, 99, 100, 497, 498, 500, 511, 514];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(44)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(44)],
    [90],
  );

  const state = createDefaultState();
  const correctedGroup = RESOLUTIONS.get(3).groups[0].id;
  const retiredGroup = RESOLUTIONS.get(3).groups[1].id;
  state.catalogRevision = 43;
  state.activeWorkoutMinutes = 3;
  state.selectedExerciseIds[correctedGroup] = 94;
  state.selectedExerciseIds[retiredGroup] = 90;
  state.outcomes[correctedGroup] = "tick";
  state.outcomes[retiredGroup] = "x";
  state.pendingRestGroupId = correctedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.scores["94"] = -4;
  state.scores["90"] = -6;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[correctedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[retiredGroup], undefined);
  assert.equal(restored.state.outcomes[correctedGroup], undefined);
  assert.equal(restored.state.outcomes[retiredGroup], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["94"], -4);
  assert.equal(restored.state.scores["90"], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("directional circles belong to complete atomic sequences", () => {
  const expectedDirectionSequences = new Map([
    [406, "ClockwiseThenCounterclockwise"],
    [409, "ClockwiseThenCounterclockwise"],
    [561, "ClockwiseThenCounterclockwise"],
    [608, "CounterclockwiseThenClockwise"],
    [611, "CounterclockwiseThenClockwise"],
  ]);
  const expectedNames = new Map([
    [264, "Standing Arm Circles"],
    [275, "Small Arm Circles"],
    [406, "Standing Wheel Arm Circles"],
    [409, "Full Neck Circles"],
    [460, "Jogging in Place with Arm Circles"],
    [588, "Belly-Dance Alternating Shoulder Rolls"],
    [608, "Hip Circles"],
    [611, "Wide-Stance Hip Circles"],
    [743, "Standing Large Arm Circles"],
    [214, "Single-Arm Wrist Circles"],
    [223, "Controlled Wrist Circles"],
    [288, "Standing Knee-and-Ankle Circles"],
    [755, "Outward Wrist Circles"],
    [756, "Outward Controlled Wrist Circles"],
    [758, "Backward Knee-and-Ankle Circles"],
  ]);

  for (const [exerciseId, expectedName] of expectedNames) {
    assert.equal(
      catalog.find((exercise) => exercise.id === exerciseId)?.name,
      expectedName,
    );
  }
  for (const [exerciseId, expectedSequence] of expectedDirectionSequences) {
    assert.equal(
      catalog.find((exercise) => exercise.id === exerciseId)?.directionSequence,
      expectedSequence,
    );
  }
  const oneWayCircleName = /\b(?:clockwise|counterclockwise|forward|backward|inward|outward)\b.*\bcircles\b/i;
  const ownerByExerciseId = new Map(catalog.flatMap((root) =>
    root.sequenceBlocks.map((block) => [block.exerciseId, root])));
  assert.ok(catalog.every((exercise) => {
    if (!oneWayCircleName.test(exercise.name)) {
      return true;
    }
    const owner = ownerByExerciseId.get(exercise.id);
    return owner?.sequenceBlocks.length > 1 &&
      owner.sequenceBlocks.some((block) => block.exerciseId === exercise.id);
  }));
});

test("complete-direction revision retires duplicates and preserves side-leg scores", () => {
  const workoutIds = [
    264, 275, 406, 409, 460, 588, 608, 611, 617, 620, 743,
    757, 759, 760, 761, 762, 763, 764,
  ];
  const scoreIds = [
    264, 275, 406, 409, 460, 588, 608, 611, 743,
    757, 759, 760, 761, 762, 763, 764,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(45)],
    workoutIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(45)],
    scoreIds,
  );

  const state = createDefaultState();
  const changedGroup = RESOLUTIONS.get(3).groups[0].id;
  const relinkedGroup = RESOLUTIONS.get(3).groups[1].id;
  state.catalogRevision = 44;
  state.selectedExerciseIds[changedGroup] = 409;
  state.selectedExerciseIds[relinkedGroup] = 617;
  state.outcomes[changedGroup] = "tick";
  state.outcomes[relinkedGroup] = "tick";
  state.scores["409"] = -4;
  state.scores["617"] = -2;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup], undefined);
  assert.equal(restored.state.selectedExerciseIds[relinkedGroup], undefined);
  assert.equal(restored.state.scores["409"], undefined);
  assert.equal(restored.state.scores["617"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("lead-stance timing revision preserves scores except later semantic replacements", () => {
  const leadStanceIds = [
    265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(46)],
    leadStanceIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(46), false);

  const state = createDefaultState();
  state.catalogRevision = 45;
  for (const exerciseId of leadStanceIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of leadStanceIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(
      restored.state.scores[String(exerciseId)],
      [287, 473].includes(exerciseId) ? undefined : -4,
    );
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("unilateral setup correction rebuilds workouts without resetting scores", () => {
  const correctedIds = [198, 398, 421, 427, 468, 512, 515];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(47)],
    correctedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(47), false);

  const state = createDefaultState();
  state.catalogRevision = 46;
  for (const exerciseId of correctedIds) {
    state.selectedExerciseIds[`changed.${exerciseId}`] = exerciseId;
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of correctedIds) {
    assert.equal(restored.state.selectedExerciseIds[`changed.${exerciseId}`], undefined);
    assert.equal(restored.state.scores[String(exerciseId)], exerciseId === 515 ? undefined : -4);
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("hard-floor coverage revision resets only changed exercise progress and scores", () => {
  const changedIds = [
    439, 442, 444, 478,
    549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
    559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
    569, 570, 571, 574, 575, 578, 581, 582, 583,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(51)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(51)],
    changedIds,
  );

  const state = createDefaultState();
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    isSelectable(catalog.find((exercise) => exercise.id === 561), group));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.id !== changedGroup.id &&
    isSelectable(catalog.find((exercise) => exercise.id === 15), group));
  state.catalogRevision = 50;
  state.selectedExerciseIds[changedGroup.id] = 561;
  state.selectedExerciseIds[retainedGroup.id] = 15;
  state.outcomes[changedGroup.id] = "x";
  state.outcomes[retainedGroup.id] = "tick";
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [changedGroup.id]: [561],
    [retainedGroup.id]: [15],
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], 15);
  assert.equal(restored.state.outcomes[retainedGroup.id], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.lastKeptExerciseIds.includes(561), true);
  assert.equal(restored.state.lastKeptExerciseIds.includes(15), true);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("split floor sequences discard obsolete rounds but preserve scores and Keeps", () => {
  for (const [exerciseId, groupId] of [[327, "r30.chest"], [414, "r30.cranial-muscles"]]) {
    const state = createDefaultState();
    state.catalogRevision = 72;
    state.activeWorkoutMinutes = 30;
    state.activeWorkoutModifiers = WORKOUT_MODIFIERS.None;
    state.selectedExerciseIds = { [groupId]: exerciseId };
    state.keptExerciseRootIdsBySelectionGroupId = { [groupId]: [exerciseId] };
    state.lastKeptExerciseIds = [exerciseId];
    state.scores = { [exerciseId]: 7 };
    state.pendingMovementGroupId = `${groupId}.set1.block3`;
    state.pendingMovementMillisecondsRemaining = 4000;
    state.pendingMovementPausedByUser = true;
    const restored = new WorkoutSession(catalog, state, () => 0);

    restored.reconcileCatalog();

    assert.equal(restored.state.pendingMovementGroupId, null);
    assert.equal(restored.state.pendingMovementMillisecondsRemaining, 0);
    assert.equal(restored.state.pendingMovementPausedByUser, false);
    assert.equal(restored.state.scores[String(exerciseId)], 7);
    assert.ok(restored.state.keptExerciseRootIdsBySelectionGroupId[groupId].includes(exerciseId));
    assert.ok(restored.state.lastKeptExerciseIds.includes(exerciseId));
  }
});

test("demonstration-integrity revision rebuilds changed workout state without resetting scores", () => {
  const changedIds = [
    32, 58, 92, 95, 104, 105, 107, 108, 109, 119, 167, 168,
    169, 190, 193, 195, 252, 253, 267, 282, 295, 296, 390,
    391, 392, 393, 394, 395, 397, 398, 399, 400, 401, 407,
    408, 410, 411, 412, 413, 417, 420, 424, 426, 427, 428,
    431, 432, 433, 434, 435, 436, 437, 438, 440, 441, 443,
    445, 448, 450, 451, 452, 455, 456, 457, 458, 459, 460,
    461, 462, 463, 464, 465, 469, 471, 472, 475, 476, 478,
    479, 480, 484, 487, 488, 517, 530, 537, 548, 549, 550,
    551, 552, 553, 554, 555, 556, 557, 558, 559, 560, 561,
    562, 563, 564, 565, 566, 567, 568, 569, 570, 571, 574,
    575, 578, 581, 582, 583, 591, 609, 610, 611, 612, 613,
    615, 616, 619, 687, 884, 885, 886, 887,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(52)],
    changedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(52), false);

  const state = createDefaultState();
  state.catalogRevision = 51;
  state.activeWorkoutModifiers =
    WORKOUT_MODIFIERS.HardFloor | WORKOUT_MODIFIERS.Silence;
  const [changedGroup, retiredGroup, retainedGroup] =
    RESOLUTIONS.get(3).groups.map((group) => group.id);
  const changedStorageKey =
    `p${state.activeWorkoutModifiers}|${changedGroup}`;
  const retiredStorageKey =
    `p${state.activeWorkoutModifiers}|${retiredGroup}`;
  const retainedStorageKey =
    `p${state.activeWorkoutModifiers}|${retainedGroup}`;
  state.selectedExerciseIds = {
    [changedStorageKey]: 417,
    [retiredStorageKey]: 267,
    [retainedStorageKey]: 101,
  };
  state.outcomes = {
    [changedGroup]: "x",
    [retiredGroup]: "x",
    [retainedGroup]: "tick",
  };
  state.scores = { 417: -4, 267: -6, 101: -2 };
  const changedKeepGroup = [...RESOLUTIONS.values()]
    .flatMap((resolution) => resolution.groups)
    .find((group) => isSelectable(
      catalog.find((item) => item.id === 417),
      group,
    ));
  const retainedKeepGroup = [...RESOLUTIONS.values()]
    .flatMap((resolution) => resolution.groups)
    .find((group) => isSelectable(
       catalog.find((item) => item.id === 101),
      group,
    ));
  assert.ok(changedKeepGroup);
  assert.ok(retainedKeepGroup);
  state.keptExerciseRootIdsBySelectionGroupId = {};
  for (const [groupId, rootId] of [
    [changedKeepGroup.id, 417],
    [retainedKeepGroup.id, 101],
    [retiredGroup, 267],
  ]) {
    state.keptExerciseRootIdsBySelectionGroupId[groupId] = [
      ...(state.keptExerciseRootIdsBySelectionGroupId[groupId] ?? []),
      rootId,
    ];
  }
  state.lastKeptExerciseIds = [417, 267, 101];
  state.pendingRestGroupId = changedGroup;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedStorageKey], undefined);
  assert.equal(restored.state.selectedExerciseIds[retiredStorageKey], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedStorageKey], 101);
  assert.equal(restored.state.outcomes[changedGroup], undefined);
  assert.equal(restored.state.outcomes[retiredGroup], undefined);
  assert.equal(restored.state.outcomes[retainedGroup], "tick");
  assert.equal(restored.state.scores["417"], -4);
  // Revision reconciliation preserves historical scores. The initialization
  // pass that has the bundled inventory removes absent score IDs; deployment
  // migration coverage below verifies that cleanup separately.
  assert.equal(restored.state.scores["267"], -6);
  assert.equal(restored.state.scores["101"], -2);
  assert.equal(restored.state.pendingRestGroupId, null);
  // Slot-scoped Keeps for corrected exercises survive. A Keep whose exercise
  // was retired is removed because no valid sequence root remains.
  assert.deepEqual(restored.state.lastKeptExerciseIds.sort((a, b) => a - b),
    [101, 417]);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("slippery hard-floor revision rebuilds placements without erasing feedback", () => {
  const changedIds = [
    17, 19, 37, 41, 58, 60, 92, 93, 97, 103, 104, 105,
    107, 108, 109, 112, 116, 117, 120, 121, 122, 123, 124, 125,
    126, 127, 128, 129, 133, 136, 142, 143, 150, 156, 163, 174,
    178, 180, 181, 182, 183, 184, 190, 192, 193, 195, 199, 203,
    231, 232, 245, 278, 279, 280, 282, 303, 311, 314, 315,
    326, 340, 404, 408, 412, 478, 484, 508, 509, 534, 535,
    536, 538, 572, 576, 591, 610, 611, 626, 633, 636, 685, 687,
    733, 746, 748, 750, 816, 884, 885, 886, 887, 905, 915, 971,
    973, 986, 999,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(53)],
    changedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(53), false);

  const changedExercise = catalog.find((exercise) => exercise.id === 37);
  const retainedExercise = catalog.find((exercise) => exercise.id === 101);
  const changedGroup = [...RESOLUTIONS.values()]
    .flatMap((resolution) => resolution.groups)
    .find((group) => isSelectable(changedExercise, group));
  const retainedGroup = [...RESOLUTIONS.values()]
    .flatMap((resolution) => resolution.groups)
    .find((group) => group.id !== changedGroup.id &&
      isSelectable(retainedExercise, group));
  assert.ok(changedGroup);
  assert.ok(retainedGroup);

  const state = createDefaultState();
  state.catalogRevision = 52;
  state.activeWorkoutModifiers = WORKOUT_MODIFIERS.HardFloor;
  const changedStorageKey = `p${WORKOUT_MODIFIERS.HardFloor}|${changedGroup.id}`;
  const retainedStorageKey = `p${WORKOUT_MODIFIERS.HardFloor}|${retainedGroup.id}`;
  state.selectedExerciseIds = {
    [changedStorageKey]: 37,
    [retainedStorageKey]: 101,
  };
  state.outcomes = {
    [changedGroup.id]: "x",
    [retainedGroup.id]: "tick",
  };
  state.scores = { 37: -3, 101: -1 };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 37: -3 },
  };
  state.lastKeptExerciseIds = [37, 101];
  state.keptExerciseRootIdsBySelectionGroupId = {
    [changedGroup.id]: [37],
    [retainedGroup.id]: [101],
  };
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedStorageKey], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedStorageKey], 101);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[retainedGroup.id], "tick");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.scores["37"], -3);
  assert.equal(restored.state.scores["101"], -1);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance]["37"],
    -3,
  );
  assert.deepEqual(restored.state.lastKeptExerciseIds.sort((a, b) => a - b),
    [37, 101]);
  assert.deepEqual(
    restored.state.keptExerciseRootIdsBySelectionGroupId[changedGroup.id],
    [37],
  );
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);

  const softFloorState = createDefaultState();
  softFloorState.catalogRevision = 52;
  softFloorState.activeWorkoutModifiers = WORKOUT_MODIFIERS.None;
  softFloorState.selectedExerciseIds[changedGroup.id] = 37;
  softFloorState.outcomes[changedGroup.id] = "tick";
  softFloorState.pendingRestGroupId = changedGroup.id;
  softFloorState.pendingRestEndsAtUnixMilliseconds = 123456;
  softFloorState.pendingRestKept = true;

  const softFloorRestored = new WorkoutSession(catalog, softFloorState, () => 0);
  softFloorRestored.reconcileCatalog();

  assert.equal(softFloorRestored.state.selectedExerciseIds[changedGroup.id], 37);
  assert.equal(softFloorRestored.state.outcomes[changedGroup.id], "tick");
  assert.equal(softFloorRestored.state.pendingRestGroupId, changedGroup.id);
  assert.equal(softFloorRestored.state.pendingRestEndsAtUnixMilliseconds, 123456);
  assert.equal(softFloorRestored.state.pendingRestKept, true);
});

test("sole-wall revision rebuilds changed workout state and resets scores", () => {
  const changedIds = [563, 564, 567, 568, 574];
  assert.equal(CURRENT_CATALOG_REVISION, 79);
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(54)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(54)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 53;
  state.activeWorkoutMinutes = 30;
  state.activeWorkoutModifiers =
    WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.SoleWallContact;
  const changedGroupId = "r30.deep-hip-rotators";
  const retainedGroupId = "r30.gluteal-extensors";
  const profilePrefix = `p${state.activeWorkoutModifiers}|`;
  state.selectedExerciseIds = {
    [`${profilePrefix}${changedGroupId}`]: 563,
    [`${profilePrefix}${retainedGroupId}`]: 15,
  };
  state.outcomes = {
    [changedGroupId]: "x",
    [retainedGroupId]: "tick",
  };
  state.scores = { 563: -4, 15: -2 };
  state.pendingRestGroupId = changedGroupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(
    restored.state.selectedExerciseIds[`${profilePrefix}${changedGroupId}`],
    undefined,
  );
  assert.equal(
    restored.state.selectedExerciseIds[`${profilePrefix}${retainedGroupId}`],
    undefined, // Later anatomy correction invalidates 15 in this gluteal slot.
  );
  assert.equal(restored.state.outcomes[changedGroupId], undefined);
  assert.equal(restored.state.outcomes[retainedGroupId], undefined);
  assert.equal(restored.state.scores["563"], undefined);
  assert.equal(restored.state.scores["15"], -2);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("bare-upper-body expansion drops retired slot state and scores", () => {
  const changedIds = [790, 993];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(55)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(55)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 54;
  state.activeWorkoutMinutes = 30;
  state.activeWorkoutModifiers =
    WORKOUT_MODIFIERS.Mirror | WORKOUT_MODIFIERS.TallMirror;
  const profilePrefix = `p${state.activeWorkoutModifiers}|`;
  const changedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("ScapularGirdle")).id;
  const retainedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("PosteriorThighAndKneeFlexors")).id;
  state.selectedExerciseIds = {
    [`${profilePrefix}${changedGroupId}`]: 790,
    [`${profilePrefix}${retainedGroupId}`]: 15,
  };
  state.outcomes = {
    [changedGroupId]: "x",
    [retainedGroupId]: "tick",
  };
  state.scores = { 790: -4, 993: -3, 15: -2 };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(
    restored.state.selectedExerciseIds[`${profilePrefix}${changedGroupId}`],
    undefined,
  );
  assert.equal(
    restored.state.selectedExerciseIds[`${profilePrefix}${retainedGroupId}`],
    15,
  );
  assert.equal(restored.state.outcomes[changedGroupId], undefined);
  assert.equal(restored.state.scores["790"], undefined);
  assert.equal(restored.state.scores["993"], undefined);
  assert.equal(restored.state.scores["15"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("hand-shape replacement revision rebuilds changed workout state and resets scores", () => {
  const changedIds = [218, 234, 237, 239, 240, 241, 242, 283, 291, 556];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(56)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(56)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 55;
  state.activeWorkoutMinutes = 30;
  const changedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("DeepHipRotators")).id;
  const retainedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("PosteriorThighAndKneeFlexors")).id;
  state.selectedExerciseIds = {
    [changedGroupId]: 241,
    [retainedGroupId]: 15,
  };
  state.outcomes = {
    [changedGroupId]: "x",
    [retainedGroupId]: "tick",
  };
  state.scores = { 241: -4, 15: -2 };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.Warmup]: { 241: -4, 15: -2 },
  };
  state.pendingRestGroupId = changedGroupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroupId], undefined);
  assert.equal(restored.state.outcomes[changedGroupId], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroupId], 15);
  assert.equal(restored.state.scores["241"], undefined);
  assert.equal(restored.state.scores["15"], -2);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.Warmup]["241"],
    undefined,
  );
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.Warmup]["15"],
    -2,
  );
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("uppercut demonstration revision rebuilds only its workout state and score", () => {
  const changedIds = [287];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(57)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(57)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 56;
  state.activeWorkoutMinutes = 30;
  const changedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("ElbowFlexors")).id;
  const retainedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("PosteriorThighAndKneeFlexors")).id;
  state.selectedExerciseIds = {
    [changedGroupId]: 287,
    [retainedGroupId]: 15,
  };
  state.outcomes = {
    [changedGroupId]: "x",
    [retainedGroupId]: "tick",
  };
  state.scores = { 287: -4, 15: -2 };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 287: -4, 15: -2 },
  };
  state.pendingRestGroupId = changedGroupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroupId], undefined);
  assert.equal(restored.state.outcomes[changedGroupId], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroupId], 15);
  assert.equal(restored.state.scores["287"], undefined);
  assert.equal(restored.state.scores["15"], -2);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance]["287"],
    undefined,
  );
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance]["15"],
    -2,
  );
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("dance cleanup revision rebuilds changed workout state and resets scores", () => {
  const changedIds = [218, 234, 237, 239, 241, 283, 291, 294, 556];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(58)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(58)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 57;
  state.activeWorkoutMinutes = 30;
  const changedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("ShoulderAbductors")).id;
  const retainedGroupId = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("PosteriorThighAndKneeFlexors")).id;
  state.selectedExerciseIds = {
    [changedGroupId]: 294,
    [retainedGroupId]: 15,
  };
  state.outcomes = {
    [changedGroupId]: "x",
    [retainedGroupId]: "tick",
  };
  state.scores = { 294: -4, 15: -2 };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 294: -4, 15: -2 },
  };
  state.pendingRestGroupId = changedGroupId;
  state.pendingRestEndsAtUnixMilliseconds = Date.now() + 60_000;
  state.pendingRestKept = true;

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroupId], undefined);
  assert.equal(restored.state.outcomes[changedGroupId], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroupId], 15);
  assert.equal(restored.state.scores["294"], undefined);
  assert.equal(restored.state.scores["15"], -2);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance]["294"],
    undefined,
  );
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance]["15"],
    -2,
  );
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("mini-squat calf-raise correction rebuilds placement without erasing feedback", () => {
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(59)],
    [565],
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(59), false);

  const state = createDefaultState();
  state.catalogRevision = 58;
  state.activeWorkoutMinutes = 30;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("MedialAndDeepKneeExtensors"));
  state.selectedExerciseIds[changedGroup.id] = 565;
  state.outcomes[changedGroup.id] = "tick";
  state.scores["565"] = -4;
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 565: -4 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(
    restored.state.selectedExerciseIds[changedGroup.id],
    undefined,
  );
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.scores["565"], -4);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance]["565"],
    -4,
  );
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("alternating side-tap correction rebuilds placement without erasing feedback", () => {
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(60)],
    [397],
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(60), false);

  const state = createDefaultState();
  state.catalogRevision = 59;
  state.activeWorkoutMinutes = 30;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("HipAbductors"));
  state.selectedExerciseIds[changedGroup.id] = 397;
  state.outcomes[changedGroup.id] = "tick";
  state.scores["397"] = -4;
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 397: -4 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.scores["397"], -4);
  assert.equal(
    restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance]["397"],
    -4,
  );
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("demand coverage expansion resets reused exercise identities", () => {
  const changedIds = [302, 304, 305, 307, 308, 309, 310];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(61)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(61)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 60;
  state.activeWorkoutMinutes = 30;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("ShoulderAbductors"));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("MedialAndDeepKneeExtensors"));
  state.selectedExerciseIds[changedGroup.id] = 302;
  state.selectedExerciseIds[retainedGroup.id] = 101;
  state.outcomes[changedGroup.id] = "x";
  state.outcomes[retainedGroup.id] = "tick";
  state.scores["302"] = -4;
  state.scores["101"] = -2;
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 302: -4, 101: -2 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], 101);
  assert.equal(restored.state.scores["302"], undefined);
  assert.equal(restored.state.scores["101"], -2);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["302"], undefined);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["101"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("coverage hierarchy revision rebuilds changed placements without resetting feedback", () => {
  const changedIds = [248, 281, 286, 367, 393, 529, 537, 545];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(62)],
    changedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(62), false);

  const state = createDefaultState();
  state.catalogRevision = 61;
  state.activeWorkoutMinutes = 30;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("PosteriorThighAndKneeFlexors"));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("MedialAndDeepKneeExtensors"));
  state.selectedExerciseIds[changedGroup.id] = 367;
  state.selectedExerciseIds[retainedGroup.id] = 101;
  state.outcomes[changedGroup.id] = "x";
  state.outcomes[retainedGroup.id] = "tick";
  state.scores["367"] = -4;
  state.scores["101"] = -2;
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 367: -4, 101: -2 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], 101);
  assert.equal(restored.state.scores["367"], -4);
  assert.equal(restored.state.scores["101"], -2);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["367"], -4);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["101"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("pogo identity correction rebuilds placements without resetting feedback", () => {
  const changedIds = [439, 442, 444];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(63)],
    changedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(63), false);

  const state = createDefaultState();
  state.catalogRevision = 62;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("AnteriorLateralNeckAndHyoidMuscles"));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("MedialAndDeepKneeExtensors"));
  state.selectedExerciseIds[changedGroup.id] = 444;
  state.selectedExerciseIds[retainedGroup.id] = 101;
  state.outcomes[changedGroup.id] = "tick";
  state.outcomes[retainedGroup.id] = "x";
  state.scores["444"] = -4;
  state.scores["101"] = -2;
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 444: -4, 101: -2 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], 101);
  assert.equal(restored.state.scores["444"], -4);
  assert.equal(restored.state.scores["101"], -2);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["444"], -4);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["101"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("independent variations replace coupled placements without resetting feedback", () => {
  const changedIds = [
    104, 113, 117, 120, 123, 135, 177, 184, 186, 199,
    256, 261, 626, 677, 845, 996, 997,
  ];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(64)],
    changedIds,
  );
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(64), false);

  const state = createDefaultState();
  state.catalogRevision = 63;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("DeepHipRotators"));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("MedialAndDeepKneeExtensors"));
  state.selectedExerciseIds[changedGroup.id] = 177;
  state.selectedExerciseIds[retainedGroup.id] = 101;
  state.outcomes[changedGroup.id] = "tick";
  state.outcomes[retainedGroup.id] = "x";
  state.scores["177"] = -4;
  state.scores["101"] = -2;
  state.keptExerciseRootIdsBySelectionGroupId[changedGroup.id] = [177];
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 177: -4, 101: -2 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], 101);
  assert.equal(restored.state.scores["177"], -4);
  assert.equal(restored.state.scores["101"], -2);
  assert.deepEqual(
    restored.state.keptExerciseRootIdsBySelectionGroupId[changedGroup.id],
    [177],
  );
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["177"], -4);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["101"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("knee crunch metadata correction rebuilds placement and resets feedback", () => {
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(65)],
    [507],
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(65)],
    [507],
  );

  const state = createDefaultState();
  state.catalogRevision = 64;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("AbdominalWall"));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("MedialAndDeepKneeExtensors"));
  state.selectedExerciseIds[changedGroup.id] = 507;
  state.selectedExerciseIds[retainedGroup.id] = 101;
  state.outcomes[changedGroup.id] = "tick";
  state.outcomes[retainedGroup.id] = "x";
  state.pendingScoreExerciseId = 507;
  state.pendingScoreValue = -4;
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 507: -4, 101: -2 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], 101);
  assert.equal(restored.state.pendingScoreExerciseId, undefined);
  assert.equal(restored.state.pendingScoreValue, undefined);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["507"], undefined);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["101"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("bodybuilding posing revision replaces static work and resets feedback", () => {
  const changedIds = [524, 525, 526, 527, 528, 790];
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(66)],
    changedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(66)],
    changedIds,
  );

  const state = createDefaultState();
  state.catalogRevision = 65;
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("AbdominalWall"));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.canonicalGroups.includes("MedialAndDeepKneeExtensors"));
  state.selectedExerciseIds[changedGroup.id] = 528;
  state.selectedExerciseIds[retainedGroup.id] = 101;
  state.outcomes[changedGroup.id] = "tick";
  state.outcomes[retainedGroup.id] = "x";
  state.keptExerciseRootIdsBySelectionGroupId[changedGroup.id] = [528];
  state.pendingScoreExerciseId = 528;
  state.pendingScoreValue = -4;
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 528: -4, 101: -2 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], 101);
  assert.deepEqual(
    restored.state.keptExerciseRootIdsBySelectionGroupId[changedGroup.id],
    [528],
  );
  assert.equal(restored.state.pendingScoreExerciseId, undefined);
  assert.equal(restored.state.pendingScoreValue, undefined);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["528"], undefined);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["101"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("material-training revision removes only anatomically invalid slots and keeps", () => {
  const addedIds = [911, 913, 916, 917];
  assert.equal(CURRENT_CATALOG_REVISION, 79);
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(67)],
    addedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(67)],
    addedIds,
  );
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(69)],
    [918, 919],
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(69)],
    [918, 919],
  );

  const invalidAbdominalSlot = "r30.abdominal-wall";
  const validChestSlot = "r30.chest";
  const unrelatedLegacySlot = "legacy.unrelated-slot";
  const invalidRound = `${invalidAbdominalSlot}.set1.block1`;
  const state = createDefaultState();
  state.catalogRevision = 66;
  state.activeWorkoutMinutes = 30;
  state.selectedExerciseIds = {
    [invalidAbdominalSlot]: 701,
    [validChestSlot]: 701,
    [unrelatedLegacySlot]: 1,
  };
  state.outcomes = {
    [invalidRound]: "tick",
    [validChestSlot]: "x",
    [unrelatedLegacySlot]: "tick",
  };
  state.pendingMovementGroupId = invalidRound;
  state.pendingMovementEndsAtUnixMilliseconds = 123456;
  state.pendingMovementMillisecondsRemaining = 4000;
  state.pendingMovementPausedByUser = true;
  state.pendingRestGroupId = invalidRound;
  state.pendingRestEndsAtUnixMilliseconds = 234567;
  state.pendingRestMillisecondsRemaining = 5000;
  state.pendingRestPausedByUser = true;
  state.pendingRestKept = true;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [invalidAbdominalSlot]: [701, 910],
    [validChestSlot]: [701],
  };
  state.lastKeptExerciseIds = [701, 910, 962];
  state.scores = { 701: -4, 910: 3, 911: -6 };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 701: -3, 910: -2 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[invalidAbdominalSlot],
    undefined);
  assert.equal(restored.state.selectedExerciseIds[validChestSlot], 701);
  assert.equal(restored.state.selectedExerciseIds[unrelatedLegacySlot], 1);
  assert.equal(restored.state.outcomes[invalidRound], undefined);
  assert.equal(restored.state.outcomes[validChestSlot], "x");
  assert.equal(restored.state.outcomes[unrelatedLegacySlot], "tick");
  assert.equal(restored.state.pendingMovementGroupId, null);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.deepEqual(
    restored.state.keptExerciseRootIdsBySelectionGroupId[invalidAbdominalSlot],
    undefined, // The integrity audit also removes the old abdominal claim from 910.
  );
  assert.deepEqual(
    restored.state.keptExerciseRootIdsBySelectionGroupId[validChestSlot],
    [701],
  );
  assert.deepEqual(
    [...restored.state.lastKeptExerciseIds].sort((left, right) => left - right),
    [701],
  );
  assert.equal(restored.state.scores["701"], -4);
  assert.equal(restored.state.scores["910"], 3);
  assert.equal(restored.state.scores["911"], undefined);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["701"], -3);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["910"], -2);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("training-claim revision removes only newly invalid slot feedback", () => {
  const addedIds = [918, 919];
  assert.equal(CURRENT_CATALOG_REVISION, 79);
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(69)],
    addedIds,
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(69)],
    addedIds,
  );

  const invalidPelvicFloorSlot = "r30.pelvic-floor-perineum";
  const validHipFlexorSlot = "r30.hip-flexors";
  const invalidRound = `${invalidPelvicFloorSlot}.set1.block1`;
  const state = createDefaultState();
  state.catalogRevision = 68;
  state.activeWorkoutMinutes = 30;
  state.selectedExerciseIds = {
    [invalidPelvicFloorSlot]: 613,
    [validHipFlexorSlot]: 613,
  };
  state.outcomes = {
    [invalidRound]: "tick",
    [validHipFlexorSlot]: "x",
  };
  state.pendingMovementGroupId = invalidRound;
  state.pendingMovementEndsAtUnixMilliseconds = 123456;
  state.pendingMovementMillisecondsRemaining = 4000;
  state.pendingMovementPausedByUser = true;
  state.pendingScoreExerciseId = 613;
  state.pendingScoreValue = -1;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [invalidPelvicFloorSlot]: [613],
    [validHipFlexorSlot]: [613],
  };
  state.lastKeptExerciseIds = [613];
  state.exerciseScoreAdjustmentsBySelectionGroupId = {
    [invalidPelvicFloorSlot]: { 613: -2 },
    [validHipFlexorSlot]: { 613: -1 },
  };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 613: -3 },
  };
  state.scores = { 613: -4, 918: -6, 919: -7 };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[invalidPelvicFloorSlot],
    undefined);
  assert.equal(restored.state.selectedExerciseIds[validHipFlexorSlot], 613);
  assert.equal(restored.state.outcomes[invalidRound], undefined);
  assert.equal(restored.state.outcomes[validHipFlexorSlot], "x");
  assert.equal(restored.state.pendingMovementGroupId, null);
  assert.equal(restored.state.keptExerciseRootIdsBySelectionGroupId[
    invalidPelvicFloorSlot], undefined);
  assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId[
    validHipFlexorSlot], [613]);
  assert.equal(restored.state.exerciseScoreAdjustmentsBySelectionGroupId[
    invalidPelvicFloorSlot], undefined);
  assert.equal(restored.state.exerciseScoreAdjustmentsBySelectionGroupId[
    validHipFlexorSlot]["613"], -1);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["613"], -3);
  assert.equal(restored.state.scores["613"], -4);
  assert.equal(restored.state.scores["918"], undefined);
  assert.equal(restored.state.scores["919"], undefined);
  // Browser state no longer persists the obsolete pre-phase pending-score
  // fields; the durable global and phase feedback above remain untouched.
  assert.equal(restored.state.pendingScoreExerciseId, undefined);
  assert.equal(restored.state.pendingScoreValue, undefined);
  assert.deepEqual(restored.state.lastKeptExerciseIds, [613]);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("complete-pose revision rebuilds affected placements without erasing feedback", () => {
  const changedIds = [307, 490, 491, 492, 495, 499, 501, 520, 528, 561, 958];
  assert.deepEqual(SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(72), new Set(changedIds));
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(72), false);
  for (const id of changedIds.filter(id => id !== 501)) {
    const exercise = catalog.find(e => e.id === id);
    const group = RESOLUTIONS.get(30).groups.find(g => g.canonicalGroups.includes(exercise.primaryCanonicalGroup));
    const state = createDefaultState();
    state.catalogRevision = 71;
    state.selectedExerciseIds = { [group.id]: id };
    state.outcomes = { [group.id]: "tick" };
    state.keptExerciseRootIdsBySelectionGroupId = { [group.id]: [id] };
    state.scores = { [id]: -4 };
    state.exerciseScoreAdjustmentsBySelectionGroupId = { [group.id]: { [id]: -1 } };
    state.exerciseScoreAdjustmentsByPhase = {
      [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { [id]: -3 },
    };
    const restored = new WorkoutSession(catalog, state, () => 0);
    restored.reconcileCatalog();
    assert.equal(restored.state.selectedExerciseIds[group.id], undefined);
    assert.equal(restored.state.outcomes[group.id], undefined);
    if (id === 520) {
      assert.equal(restored.getScore(exercise), 0);
      assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId, {});
      assert.deepEqual(restored.state.exerciseScoreAdjustmentsBySelectionGroupId, {});
      assert.deepEqual(restored.state.exerciseScoreAdjustmentsByPhase, {});
      continue;
    }
    assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId[group.id], [id]);
    assert.equal(restored.state.scores[id], -4);
    assert.equal(restored.state.exerciseScoreAdjustmentsBySelectionGroupId[group.id][id], -1);
    assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance][id], -3);
    assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
  }
});

test("corrected two-sided revision rebuilds placements and preserves feedback", () => {
  const changedIds = new Set([32, 483, 493]);
  assert.equal(CURRENT_CATALOG_REVISION, 79);
  assert.deepEqual(SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(71), changedIds);
  assert.equal(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.has(71), false);

  const changed = catalog.find((exercise) => exercise.id === 483);
  const retained = catalog.find((exercise) => exercise.id === 15);
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    isSelectable(changed, group));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    group.id !== changedGroup.id && isSelectable(retained, group));
  const state = createDefaultState();
  state.catalogRevision = 70;
  state.selectedExerciseIds = {
    [changedGroup.id]: changed.id,
    [retainedGroup.id]: retained.id,
  };
  state.outcomes = {
    [changedGroup.id]: "tick",
    [retainedGroup.id]: "x",
  };
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = 123456;
  state.pendingRestKept = true;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [changedGroup.id]: [changed.id],
  };
  state.lastKeptExerciseIds = [changed.id];
  state.scores = { 32: -2, 483: -4, 493: -3 };
  state.exerciseScoreAdjustmentsBySelectionGroupId = {
    [changedGroup.id]: { 483: -1 },
  };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { 483: -3 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], retained.id);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[retainedGroup.id], "x");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestKept, false);
  assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId[
    changedGroup.id], [changed.id]);
  assert.ok(restored.state.lastKeptExerciseIds.includes(changed.id));
  assert.equal(restored.state.scores["32"], -2);
  assert.equal(restored.state.scores["483"], -4);
  assert.equal(restored.state.scores["493"], -3);
  assert.equal(restored.state.exerciseScoreAdjustmentsBySelectionGroupId[
    changedGroup.id]["483"], -1);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance]["483"], -3);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("Shy audit revision rebuilds only Shy placements and preserves feedback", () => {
  const changedIds = [
    56, 59, 98, 108, 176, 185, 188, 190, 202, 203, 204, 205,
    220, 224, 231, 258, 269, 283, 289, 290, 377, 379, 392,
    398, 399, 400, 401, 402, 403, 404, 405, 410, 474, 481,
    498, 543, 557, 608, 609, 678, 685, 687,
  ];
  assert.deepEqual(
    SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(70),
    new Set(changedIds),
  );
  assert.deepEqual(
    SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(70),
    new Set([202, 204, 205]),
  );

  const changed = catalog.find((exercise) => exercise.id === 56);
  const shyGroup = RESOLUTIONS.get(30).groups.find((group) =>
    isSelectable(changed, group));
  const nonShyGroup = RESOLUTIONS.get(20).groups.find((group) =>
    isSelectable(changed, group));
  const shyStorageKey = `p${WORKOUT_MODIFIERS.Shy}|${shyGroup.id}`;
  const nonShyStorageKey = `p${WORKOUT_MODIFIERS.None}|${nonShyGroup.id}`;
  const state = createDefaultState();
  state.catalogRevision = 69;
  state.activeWorkoutModifiers = WORKOUT_MODIFIERS.Shy;
  state.selectedExerciseIds = {
    [shyStorageKey]: changed.id,
    [nonShyStorageKey]: changed.id,
  };
  state.outcomes = { [shyGroup.id]: "tick" };
  state.pendingRestGroupId = shyGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = 123456;
  state.pendingRestKept = true;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [shyGroup.id]: [changed.id],
  };
  state.lastKeptExerciseIds = [changed.id];
  state.scores = { [changed.id]: -4 };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { [changed.id]: -3 },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[shyStorageKey], undefined);
  assert.equal(restored.state.selectedExerciseIds[nonShyStorageKey], changed.id);
  assert.equal(restored.state.outcomes[shyGroup.id], undefined);
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestKept, false);
  assert.deepEqual(
    restored.state.keptExerciseRootIdsBySelectionGroupId[shyGroup.id],
    [changed.id],
  );
  assert.ok(restored.state.lastKeptExerciseIds.includes(changed.id));
  assert.equal(restored.state.scores[String(changed.id)], -4);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance][String(changed.id)], -3);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("Shy audit revision fully clears reused exercise identities", () => {
  const reusedIds = [202, 204, 205];
  const changed = catalog.find((exercise) => exercise.id === reusedIds[0]);
  const retained = catalog.find((exercise) => exercise.id === 15);
  const changedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    isSelectable(changed, group));
  const retainedGroup = RESOLUTIONS.get(30).groups.find((group) =>
    isSelectable(retained, group));
  const state = createDefaultState();
  state.catalogRevision = 69;
  state.activeWorkoutModifiers = WORKOUT_MODIFIERS.None;
  state.selectedExerciseIds = {
    [changedGroup.id]: changed.id,
    [retainedGroup.id]: retained.id,
  };
  state.outcomes = {
    [changedGroup.id]: "tick",
    [retainedGroup.id]: "x",
  };
  state.pendingRestGroupId = changedGroup.id;
  state.pendingRestEndsAtUnixMilliseconds = 123456;
  state.pendingRestKept = true;
  state.keptExerciseRootIdsBySelectionGroupId = {
    [changedGroup.id]: [changed.id],
    [retainedGroup.id]: [retained.id],
  };
  state.lastKeptExerciseIds = [changed.id, retained.id];
  state.scores = { 202: -4, 204: -3, 205: -2, 15: -1 };
  state.exerciseScoreAdjustmentsByPhase = {
    [WORKOUT_EXERCISE_PHASE.PeakPerformance]: {
      202: -4,
      204: -3,
      205: -2,
      15: -1,
    },
  };

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  assert.equal(restored.state.selectedExerciseIds[changedGroup.id], undefined);
  assert.equal(restored.state.selectedExerciseIds[retainedGroup.id], retained.id);
  assert.equal(restored.state.outcomes[changedGroup.id], undefined);
  assert.equal(restored.state.outcomes[retainedGroup.id], "x");
  assert.equal(restored.state.pendingRestGroupId, null);
  assert.equal(restored.state.pendingRestKept, false);
  assert.equal(
    restored.state.keptExerciseRootIdsBySelectionGroupId[changedGroup.id],
    undefined,
  );
  assert.deepEqual(
    restored.state.keptExerciseRootIdsBySelectionGroupId[retainedGroup.id],
    [retained.id],
  );
  assert.equal(restored.state.lastKeptExerciseIds.includes(changed.id), false);
  assert.equal(restored.state.lastKeptExerciseIds.includes(retained.id), true);
  for (const exerciseId of reusedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
    assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
      WORKOUT_EXERCISE_PHASE.PeakPerformance][String(exerciseId)], undefined);
  }
  assert.equal(restored.state.scores[String(retained.id)], -1);
  assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[
    WORKOUT_EXERCISE_PHASE.PeakPerformance][String(retained.id)], -1);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("unclear exercise replacement revision resets every changed score", () => {
  const changedIds = [
    211, 213, 214, 215, 218, 223, 224,
    236, 237, 241, 242, 245, 283, 289,
  ];
  const state = createDefaultState();
  state.catalogRevision = 19;

  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
});

test("catalog clarity reset revision resets every replaced identity", () => {
  const changedIds = [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(21)];
  assert.equal(changedIds.length, 68);
  const state = createDefaultState();
  state.catalogRevision = 20;

  for (const exerciseId of changedIds) {
    state.scores[String(exerciseId)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const exerciseId of changedIds) {
    assert.equal(restored.state.scores[String(exerciseId)], undefined);
  }
});

test("deployment migration preserves present keeps and drops missing exercises", () => {
  const present = catalog.find((item) => item.id === 223);
  const group = RESOLUTIONS.get(30).groups.find((candidate) =>
    isSelectable(present, candidate),
  );
  const state = createDefaultState();
  state.version = 18;
  state.catalogRevision = 12;
  state.selectedExerciseIds[group.id] = present.id;
  state.lastKeptExerciseIds = [
    ...new Set(present.sequenceBlocks.map((block) => block.exerciseId)),
    999999,
  ];

  const restored = new WorkoutSession(
    catalog,
    parseStoredState(JSON.stringify(state)),
    () => 0,
  );
  restored.initialize();

  assert.deepEqual(
    restored.state.lastKeptExerciseIds,
    [...new Set(present.sequenceBlocks.map((block) => block.exerciseId))],
  );
  assert.equal(restored.state.selectedExerciseIds[group.id], undefined);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);

  restored.startWorkout(45, WORKOUT_MODIFIERS.None);

  assert.equal(restored.state.selectedExerciseIds[group.id], present.id);
  const selectedBlockIds = restored.getActiveGroups()
    .filter((round) => getSelectionKey(round) === group.id)
    .map((round) => restored.getSelectedExercise(round).id);
  const expectedBlockIds = present.sequenceBlocks.map((block) => block.exerciseId);
  assert.equal(selectedBlockIds.length % expectedBlockIds.length, 0);
  for (let index = 0; index < selectedBlockIds.length;
    index += expectedBlockIds.length) {
    assert.deepEqual(
      selectedBlockIds.slice(index, index + expectedBlockIds.length),
      expectedBlockIds,
    );
  }
});

test("a legacy partial sequence keep is removed instead of promoted", () => {
  const root = catalog.find((item) => item.id === 223);
  const memberIds = [...new Set(root.sequenceBlocks.map((block) => block.exerciseId))];
  assert.ok(memberIds.length > 1);
  const partialState = createDefaultState();
  partialState.version = 18;
  partialState.lastKeptExerciseIds = [memberIds[0]];
  const group = RESOLUTIONS.get(30).groups.find((candidate) =>
    isSelectable(root, candidate));
  partialState.selectedExerciseIds[group.id] = root.id;
  const partial = new WorkoutSession(catalog, partialState, () => 0);

  partial.initialize();

  assert.deepEqual(partial.state.lastKeptExerciseIds, []);

  const completeState = createDefaultState();
  completeState.version = 18;
  completeState.lastKeptExerciseIds = memberIds;
  completeState.selectedExerciseIds[group.id] = root.id;
  const complete = new WorkoutSession(catalog, completeState, () => 0);

  complete.initialize();

  assert.deepEqual(new Set(complete.state.lastKeptExerciseIds), new Set(memberIds));
});

test("version fourteen progress migrates only when repaired selection stays in its sequence", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const exercises = groups.map((group, index) => exercise(
    index + 1,
    group.canonicalGroups[0],
    [],
    10,
  ));
  const root = exercises[0];
  const member = exercise(1_001, groups[0].canonicalGroups[0], [], 10);
  root.sequenceBlocks = [
    { ...root.sequenceBlocks[0] },
    { ...member.sequenceBlocks[0] },
  ];
  member.sequenceBlocks = [];
  const currentCatalog = [...exercises, member];
  const makeState = (legacyExerciseId) => {
    const state = createDefaultState();
    state.version = 14;
    state.catalogRevision = CURRENT_CATALOG_REVISION;
    state.activeWorkoutMinutes = 45;
    state.lastWorkoutMinutes = 45;
    for (let index = 0; index < groups.length; index += 1) {
      state.selectedExerciseIds[groups[index].id] = exercises[index].id;
    }
    state.selectedExerciseIds[groups[0].id] = legacyExerciseId;
    state.outcomes[groups[0].id] = "tick";
    return state;
  };

  const matching = new WorkoutSession(
    currentCatalog,
    makeState(member.id),
    () => 0,
  );
  matching.initialize();
  const matchingRounds = matching.getActiveGroups().filter((round) =>
    getSelectionKey(round) === groups[0].id);
  assert.equal(matching.state.selectedExerciseIds[groups[0].id], root.id);
  assert.deepEqual(
    matchingRounds.map((round) => matching.state.outcomes[round.id]),
    ["neutral", "tick"],
  );

  const unrelated = new WorkoutSession(
    currentCatalog,
    makeState(exercises[1].id),
    () => 0,
  );
  unrelated.initialize();
  const unrelatedRounds = unrelated.getActiveGroups().filter((round) =>
    getSelectionKey(round) === groups[0].id);
  assert.equal(unrelated.state.selectedExerciseIds[groups[0].id], root.id);
  assert.ok(unrelatedRounds.every((round) =>
    unrelated.state.outcomes[round.id] === undefined));
});

test("legacy catalog revision still retires every historical replacement", () => {
  const replacements = catalog.filter((item) =>
    typeof item.retiredName === "string" && item.retiredName,
  );
  const state = createDefaultState();
  state.catalogRevision = 2;
  for (const item of replacements) {
    state.scores[String(item.id)] = -4;
  }

  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();

  for (const item of replacements) {
    assert.equal(restored.state.scores[String(item.id)], undefined);
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test("legacy browser state without a catalog revision migrates like Android", () => {
  const restored = parseStoredState(JSON.stringify({ lastWorkoutMinutes: 10 }));
  assert.equal(restored.catalogRevision, 0);
});

test("corrupt local state resets safely", () => {
  assert.deepEqual(parseStoredState("not json"), createDefaultState());
  assert.equal(parseStoredState('{"lastWorkoutMinutes":6}').lastWorkoutMinutes, 7);
});

test("prepared workout stays unlogged until instant activation", () => {
  let now = Date.UTC(2026, 7, 29, 8);
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.map((group, index) => exercise(
    19_000 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    10,
  ));
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );

  session.prepareWorkout(3, WORKOUT_MODIFIERS.None);

  assert.equal(session.state.activeWorkoutMinutes, 3);
  assert.equal(session.state.activeWorkoutSession, null);
  assert.deepEqual(session.state.workoutHistory, []);
  assert.equal(session.state.nextWorkoutSessionId, 1);
  assert.equal(session.getActiveGroups().length, 3);

  now += 120_000;
  session.activatePreparedWorkout();

  assert.equal(session.state.activeWorkoutSession.startedAtUnixMilliseconds, now);
  assert.equal(session.state.nextWorkoutSessionId, 2);
  assert.throws(
    () => session.activatePreparedWorkout(),
    /activatable prepared workout/,
  );
});

test("completed workout history preserves exact blocks decisions and prior keeps", () => {
  let now = Date.UTC(2026, 7, 27, 8);
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.map((group, index) => exercise(
    20_000 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    10,
    EXERCISE_INSECT_COMPATIBILITY.Compatible,
    true,
    [HARD_MUSCULAR_DEMAND, MINIMUM_MUSCULAR_DEMAND, MODERATE_MUSCULAR_DEMAND][index],
  ));
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId = {
    [groups[0].id]: [exercises[0].id],
  };
  const session = new WorkoutSession(
    exercises,
    state,
    () => 0,
    () => now,
  );

  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.activeWorkoutSession.status, "InProgress");
  assert.deepEqual(
    session.state.activeWorkoutSession.keptExerciseIdsAtStart,
    [exercises[0].id],
  );
  assert.deepEqual(
    session.state.activeWorkoutSession
      .keptExerciseRootIdsBySelectionGroupIdAtStart,
    { [groups[0].id]: [exercises[0].id] },
  );
  assert.equal(session.state.activeWorkoutSession.initialSelections.length, 3);

  while (session.getNextGroup()) {
    const group = session.getNextGroup();
    now += 60_000;
    session.beginRest(group, now + REST_DURATION_MS);
    now += 1_000;
    session.recordOutcome(group, true);
    session.clearPendingRest();
  }
  session.initialize();

  assert.equal(session.state.activeWorkoutSession, null);
  assert.equal(session.state.workoutHistory.length, 1);
  session.acknowledgeCompletion();

  assert.equal(session.state.activeWorkoutSession, null);
  assert.equal(session.state.workoutHistory.length, 1);
  const completed = session.state.workoutHistory[0];
  assert.equal(completed.status, "Completed");
  assert.equal(completed.blocks.length, 3);
  assert.equal(completed.decisions.length, 3);
  assert.equal(
    completed.blocks.filter((block) =>
      block.muscularDemand === HARD_MUSCULAR_DEMAND).length,
    1,
  );
  assert.deepEqual(completed.blocks.map((block) => block.order), [1, 2, 3]);
  assert.ok(completed.blocks.every((block) =>
    block.sequenceBlockNumber === 1 &&
    block.sequenceBlockCount === 1 &&
    block.setNumber === 1 &&
    block.setCount === 1));
  assert.equal(
    completed.blocks.find((block) => block.exerciseId === exercises[0].id)
      .wasSequenceKeptAtWorkoutStart,
    true,
  );

  const persisted = parseStoredState(JSON.stringify(session.state));
  assert.deepEqual(persisted.workoutHistory, session.state.workoutHistory);
  assert.deepEqual(
    persisted.workoutHistory[0].keptExerciseIdsAtStart,
    [exercises[0].id],
  );
  assert.deepEqual(
    persisted.workoutHistory[0].keptExerciseRootIdsBySelectionGroupIdAtStart,
    { [groups[0].id]: [exercises[0].id] },
  );
});

test("interrupted workout history archives only actually completed blocks once", () => {
  let now = Date.UTC(2026, 7, 27, 9);
  const groups = RESOLUTIONS.get(3).groups;
  const exercises = groups.map((group, index) => exercise(
    21_000 + index,
    group.canonicalGroups[0],
    group.canonicalGroups.slice(1),
    10,
  ));
  const session = new WorkoutSession(
    exercises,
    createDefaultState(),
    () => 0,
    () => now,
  );
  session.startWorkout(3, WORKOUT_MODIFIERS.None);
  const first = session.getNextGroup();
  now += 60_000;
  session.beginRest(first, now + REST_DURATION_MS);
  session.keepPendingRest();
  now += 1_000;

  session.finishInterruptedWorkout();
  session.finishInterruptedWorkout();

  assert.equal(session.state.workoutHistory.length, 1);
  assert.equal(session.state.workoutHistory[0].status, "Interrupted");
  assert.equal(session.state.workoutHistory[0].blocks.length, 1);
  assert.equal(session.state.workoutHistory[0].blocks[0].exerciseId,
    session.state.workoutHistory[0].decisions[0].rootExerciseId);
  assert.equal(session.state.workoutHistory[0].decisions.length, 1);
});

test("each direction uses its own complete loop from time zero", () => {
  const exercise = { id: 406, video: "exercise_videos/exercise_0406.mp4" };
  assert.equal(getExerciseVideoPath(exercise), exercise.video);
  assert.equal(getExerciseVideoPath(exercise, "Full"), exercise.video);
  assert.equal(getExerciseVideoPath(exercise, "FirstDirection"), exercise.video);
  assert.equal(getExerciseVideoPath(exercise, "SecondDirection"),
    "exercise_direction_videos/exercise_0406.mp4");
});

test("runtime media maps to MP4s and reviewed hold frames, never GIFs", async () => {
  const directionIds = [];
  const holds = [];
  for (const item of catalog) {
    const videoPath = getExerciseVideoPath(item);
    assert.match(videoPath, /\.mp4$/);
    assert.doesNotMatch(videoPath, /exercise_gifs|\.gif$/i);
    await assertFile(path.join(repositoryRoot, "NomadicMethod", "Assets", videoPath));

    if (item.directionSequence !== "None") {
      directionIds.push(item.id);
      const owner = catalog.find((candidate) => candidate.sequenceBlocks
        .some((block) => block.exerciseId === item.id));
      const directionSegments = owner.sequenceBlocks
        .filter((block) => block.exerciseId === item.id)
        .map((block) => block.mediaSegment)
        .filter((segment) => segment !== "Full");
      assert.deepEqual(
        directionSegments,
        ["FirstDirection", "SecondDirection"],
      );
      for (const segment of directionSegments) {
        const directionVideoPath = getExerciseVideoPath(item, segment);
        assert.equal(directionVideoPath, segment === "FirstDirection"
          ? item.video
          : `exercise_direction_videos/exercise_${String(item.id).padStart(4, "0")}.mp4`);
        await assertFile(path.join(repositoryRoot, "NomadicMethod", "Assets", directionVideoPath));
      }
    }
    if (item.mode === "Hold") {
      holds.push(item);
      assert.match(item.name, /\b(?:hold|isometric|pose|posing|stance|stretch|sit|against)\b/i);
      await assertFile(
        path.join(repositoryRoot, "NomadicMethod", "Assets", getHoldFramePath(item)),
      );
    }
  }

  assert.deepEqual(directionIds, [406, 409, 561, 608, 611, 1018]);
  assert.ok(catalog.every((item) =>
    !Object.hasOwn(item, "directionPartnerExerciseId")));
  const multiExerciseSequenceRoots = catalog
    .filter((root) => new Set(root.sequenceBlocks.map((block) => block.exerciseId)).size > 1)
    .map((root) => root.id);
  assert.deepEqual(multiExerciseSequenceRoots, [
    96, 115, 178, 179, 180, 181,
    211, 214, 220, 223, 252, 264, 285, 288, 291, 302, 307,
    367, 392, 393, 415, 420, 459, 465, 491, 500, 502, 566, 610, 612,
    617, 742, 784, 834, 910, 948,
  ]);
  assert.ok(holds.length > 0);
  assert.ok(holds.some((item) => item.presentation === "Still"));
});

test("initial document contains no exercise-media URL", async () => {
  const html = await readFile(path.join(repositoryRoot, "web", "index.html"), "utf8");
  assert.doesNotMatch(html, /exercise_(?:videos|direction_videos|hold_frames)\//);
  assert.match(html, /src="\.\/app\.js"/);
  assert.doesNotMatch(html, /settings|sign[ -]?in|learn more/i);
  const durationLabels = html.match(/id="duration-labels"[\s\S]+?<\/div>/)?.[0] ?? "";
  assert.deepEqual(
    [...durationLabels.matchAll(/<span>(\d+)<\/span>/g)].map((item) => Number(item[1])),
    SUPPORTED_MINUTES,
  );
  assert.match(html, new RegExp(`max="${SUPPORTED_MINUTES.length - 1}"`));
});

test("browser shell pauses for buffering and keeps desktop layouts bounded", async () => {
  const [javascript, stylesheet] = await Promise.all([
    readFile(path.join(repositoryRoot, "web", "app.js"), "utf8"),
    readFile(path.join(repositoryRoot, "web", "styles.css"), "utf8"),
  ]);
  assert.match(javascript, /\.onwaiting\s*=\s*\(\)\s*=>\s*handleVideoWaiting/);
  assert.match(javascript, /pauseMovement\("buffering"\)/);
  assert.match(javascript, /\.onprogress\s*=/);
  assert.match(javascript, /resumePausedMovementWhenVisible\(\)/);
  assert.match(javascript, /MEDIA_RECOVERY_TIMEOUT_MS/);
  assert.doesNotMatch(javascript, /\.onloadeddata\s*=/);
  assert.match(javascript, /navigator\.wakeLock\?\.request/);
  assert.match(stylesheet, /@media \(orientation: landscape\)\s*\{/);
  assert.doesNotMatch(stylesheet, /@media \(orientation: landscape\) and \(min-width:/);
  assert.match(stylesheet, /@media \(min-width: 1000px\)\s*\{/);
  assert.match(stylesheet, /\.duration-controls\s*\{[\s\S]*?width: min\(100%, clamp\(760px, 58vw, 1120px\)\);/);
  assert.match(stylesheet, /width: clamp\(300px, min\(20vw, 34dvh\), 420px\);/);
  assert.match(stylesheet, /width: clamp\(112px, 8vw, 144px\);/);
  assert.match(stylesheet, /width: min\(calc\(100% - 64px\), 1600px\);/);
  assert.match(stylesheet, /grid-template-columns: minmax\(360px, 500px\) minmax\(520px, 900px\);/);
  assert.match(stylesheet, /grid-column: 2;\s*grid-row: 1 \/ span 2;/);
  assert.match(stylesheet, /width: min\(100%, 78dvh, 900px\);/);
  assert.match(stylesheet, new RegExp(`grid-template-columns: repeat\\(${SUPPORTED_MINUTES.length}, 1fr\\)`));
});

for (const light of [false, true]) {
  test(`repeated movement rounds preserve separate progress, Keeps and history, Light=${light}`, () => {
    const muscles = RESOLUTIONS.get(30).groups.flatMap((group) => group.canonicalGroups);
    const exercises = [exercise(1, muscles[0], muscles.slice(1), 0)];
    let session = new WorkoutSession(exercises, createDefaultState(), () => 0);
    session.startWorkout(3, light ? WORKOUT_MODIFIERS.Light : 0);
    const rounds = session.getActiveGroups();
    assert.equal(rounds.length, 3);
    assert.equal(new Set(rounds.map((round) => round.id)).size, 3);
    assert.ok(rounds.every((round) => session.getSelectedExercise(round).id === 1));
    assert.equal(session.state.activeWorkoutSession.initialSelections.length, 3);
    session.beginRest(rounds[0], Date.now() + 15_000);
    assert.equal(session.keepPendingRest(), true);
    session.recordOutcome(rounds[0], true);
    session.clearPendingRest();
    session.pauseMovement(rounds[1], 27_000, true);
    session = new WorkoutSession(exercises, parseStoredState(JSON.stringify(session.state)), () => 0.5);
    session.initialize();
    assert.equal(session.getNextGroup().id, rounds[1].id);
    assert.equal(session.state.activeWorkoutSession.blocks.length, 1);
    assert.equal(session.getPendingMovementMillisecondsRemaining(Date.now()), 27_000);
    session.reconfigureActiveWorkout(session.state.activeWorkoutModifiers | WORKOUT_MODIFIERS.Silence, rounds[1].id);
    assert.equal(session.getNextGroup().id, rounds[1].id);
    assert.equal(Object.keys(session.state.outcomes).length, 1);
    assert.ok(session.state.activeWorkoutSession.decisions.some((decision) =>
      decision.selectionGroupId === getSelectionKey(rounds[0]) && decision.outcome === "tick"));
    for (const round of rounds.slice(1)) {
      session.beginRest(round, Date.now() + 15_000);
      session.recordOutcome(round, round.id === rounds[2].id);
      session.clearPendingRest();
    }
    assert.equal(session.state.workoutCompleted, true);
    const log = session.state.workoutHistory[0];
    assert.equal(log.blocks.length, 3);
    assert.equal(log.decisions.length, 3);
    assert.equal(new Set(log.blocks.map((block) => block.workoutGroupId)).size, 3);
    assert.equal(new Set(log.decisions.map((decision) => decision.selectionGroupId)).size, 3);
    session.acknowledgeCompletion();
    assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId[getSelectionKey(rounds[0])], [1]);
    assert.equal(session.state.keptExerciseRootIdsBySelectionGroupId[getSelectionKey(rounds[1])], undefined);
    assert.ok(log.blocks.every((block) => block.rootExerciseId === 1));
  });
}

for (const crossPrimary of [false, true]) {
  test(`repeated atomic sequences keep every block and cross-primary placement, cross=${crossPrimary}`, () => {
    const exercises = directionPairCatalog().slice(0, 2);
    if (crossPrimary) {
      exercises[1].primaryCanonicalGroup = "ForearmFlexorsAndPronators";
      exercises[1].secondaryCanonicalGroups = RESOLUTIONS.get(30).groups
        .flatMap((group) => group.canonicalGroups).filter((muscle) => muscle !== exercises[1].primaryCanonicalGroup);
    }
    const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
    session.startWorkout(60, 0);
    const rounds = session.getActiveGroups();
    const placementCount = crossPrimary ? 29 : 30;
    assert.equal(rounds.length, 60);
    assert.equal(session.state.activeWorkoutSession.initialSelections.length, placementCount);
    assert.equal(new Set(rounds.map((round) => round.id)).size, 60);
    for (let i = 0; i < rounds.length; i += 2) {
      assert.equal(getSelectionKey(rounds[i]), getSelectionKey(rounds[i + 1]));
      assert.deepEqual(rounds.slice(i, i + 2).map((round) => session.getSelectedExercise(round).id), [1, 2]);
      for (const round of rounds.slice(i, i + 2)) {
        session.beginRest(round, Date.now() + 15_000);
        if (session.isIntermediateSequenceBlock(round)) session.advanceSequence(round);
        else session.recordOutcome(round, true);
        session.clearPendingRest();
      }
    }
    assert.equal(session.state.workoutCompleted, true);
    assert.equal(session.state.workoutHistory[0].blocks.length, 60);
    assert.equal(session.state.workoutHistory[0].decisions.length, placementCount);
  });
}

test("repeated movements cannot truncate an atomic sequence to fit the duration", () => {
  const exercises = directionPairCatalog().slice(0, 2);
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  assert.throws(() => session.startWorkout(3, 0), /No complete exercise lineup/);
  assert.equal(getMaximumCompleteLineupSize(exercises, RESOLUTIONS.get(3).groups, 0, 3), 0);
});

function completedWorkoutSession(
  sessionId,
  startedAtUnixMilliseconds,
  isLightDay = false,
) {
  return {
    sessionId,
    startedAtUnixMilliseconds,
    endedAtUnixMilliseconds: startedAtUnixMilliseconds + 60 * 60_000,
    workoutMinutes: 60,
    modifiers: isLightDay
      ? WORKOUT_MODIFIERS.Light
      : WORKOUT_MODIFIERS.None,
    isLightDay,
    status: "Completed",
  };
}

for (const minutes of [30, 90]) {
  test(`Done with one choice per slot preserves feedback and starts again (${minutes}m)`, () => {
    const groups = RESOLUTIONS.get(30).groups;
    const exercises = groups.map((group, index) =>
      exercise(index + 1, group.canonicalGroups[0], [], 7));
    const state = createDefaultState();
    state.keptExerciseRootIdsBySelectionGroupId = Object.fromEntries(
      groups.map((group, index) => [group.id, [exercises[index].id]]),
    );
    const session = new WorkoutSession(exercises, state, () => 0);
    session.startWorkout(minutes, WORKOUT_MODIFIERS.None);
    while (session.getNextGroup()) {
      const group = session.getNextGroup();
      session.beginRest(group, Date.now() + REST_DURATION_MS);
      if (session.isIntermediateSequenceBlock(group)) session.advanceSequence(group);
      else session.recordOutcome(group, false);
      session.clearPendingRest();
    }
    const history = structuredClone(session.state.workoutHistory);
    const feedback = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
    const keeps = structuredClone(session.state.keptExerciseRootIdsBySelectionGroupId);
    const hardWork = structuredClone(session.state.lastHardWorkUnixMillisecondsByPrimaryMuscle);
    const meaningfulWork = structuredClone(session.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);
    // Closing must not run the matcher, not even when a future plan is possible.
    session.chooseBestDistinctLineup = () => { throw new Error("Unexpected future selection"); };
    session.acknowledgeCompletion();
    const restored = new WorkoutSession(exercises,
      parseStoredState(JSON.stringify(session.state)), () => 0);
    restored.initialize();
    assert.equal(restored.state.activeWorkoutMinutes, 0);
    assert.deepEqual(restored.state.outcomes, {});
    assert.deepEqual(restored.state.selectedExerciseIds, {});
    assert.deepEqual(restored.state.workoutHistory, history);
    assert.deepEqual(restored.state.exerciseScoreAdjustmentsByPhase, feedback);
    assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId, keeps);
    assert.deepEqual(restored.state.lastHardWorkUnixMillisecondsByPrimaryMuscle, hardWork);
    assert.deepEqual(restored.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle, meaningfulWork);
    assert.ok(exercises.every((exercise) => exercise.score === 7));
    restored.prepareWorkout(minutes, WORKOUT_MODIFIERS.None);
    assert.equal(restored.getActiveGroups().length, minutes);
    assert.equal(new Set(restored.getActiveGroups().map(getSelectionKey)).size, groups.length);
  });
}

test("leaving rest with no replacement settles once without choosing a future lineup", () => {
  const exercises = RESOLUTIONS.get(30).groups.map((group, index) =>
    exercise(index + 1, group.canonicalGroups[0], [], 0));
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(30, WORKOUT_MODIFIERS.None);
  const first = session.getNextGroup();
  const rejectedId = session.getSelectedExercise(first).id;
  session.beginRest(first, Date.now() + REST_DURATION_MS);
  session.chooseBestDistinctLineup = () => { throw new Error("Unexpected future selection"); };
  session.finishInterruptedWorkout();
  session.finishInterruptedWorkout();
  session.initialize();
  assert.equal(session.state.workoutHistory.length, 1);
  const [history] = session.state.workoutHistory;
  assert.equal(history.status, "Interrupted");
  assert.equal(history.blocks.length, 1);
  assert.equal(history.decisions.length, 1);
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase.Warmup[rejectedId], -1);
  assert.equal(session.state.activeWorkoutMinutes, 0);
});

test("a downvote cannot force a lower-score alternative just because a workout ended", () => {
  const groups = RESOLUTIONS.get(30).groups;
  const exercises = groups.map((group, index) =>
    exercise(index + 1, group.canonicalGroups[0], [], 10));
  exercises.push(exercise(1001, groups[0].canonicalGroups[0], [], 7));
  const session = new WorkoutSession(exercises, createDefaultState(), () => 0);
  session.startWorkout(30, WORKOUT_MODIFIERS.None);
  const rejectedId = session.state.selectedExerciseIds[groups[0].id];
  for (const group of session.getActiveGroups()) {
    session.recordOutcome(group, group.id !== groups[0].id);
  }
  session.acknowledgeCompletion();
  assert.equal(session.state.selectedExerciseIds[groups[0].id], undefined);
  session.prepareWorkout(30, WORKOUT_MODIFIERS.None);
  assert.equal(session.state.selectedExerciseIds[groups[0].id], rejectedId);
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase.Warmup[rejectedId], -1);
  assert.equal(exercises[0].score, 10);
});

function exercise(
  id,
  primaryCanonicalGroup,
  secondaryCanonicalGroups,
  score,
  insectCompatibility = EXERCISE_INSECT_COMPATIBILITY.Unreviewed,
  silent = true,
  muscularDemand = 0,
  mirrorRelationship = EXERCISE_MIRROR_RELATIONSHIP.Agnostic,
  minimumMirrorCoverage = mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.Agnostic
    ? EXERCISE_MIRROR_COVERAGE.None
    : EXERCISE_MIRROR_COVERAGE.UpperBody,
  hardFloorCompatibility = EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible,
) {
  return {
    id,
    name: `Exercise ${id}`,
    video: `exercise_videos/exercise_${String(id).padStart(4, "0")}.mp4`,
    primaryCanonicalGroup,
    secondaryCanonicalGroups,
    score,
    muscularDemand,
    insectCompatibility,
    hardFloorCompatibility,
    upperBodyClothingRequirement:
      EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.Agnostic,
    shyCompatibility: EXERCISE_SHY_COMPATIBILITY.Compatible,
    wallRequired: false,
    soleWallContactRequired: false,
    mirrorRelationship,
    minimumMirrorCoverage,
    equipment: mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly
      ? "Mirror"
      : "None",
    silent,
    sideSequence: "Continuous",
    directionSequence: "None",
    sequenceBlocks: [{
      exerciseId: id,
      sideCue: "None",
      directionCue: "None",
      mirrorMedia: false,
      mediaSegment: "Full",
    }],
    mode: "Repetition",
    presentation: "Motion",
  };
}

function reviewedInsectCatalog() {
  const canonicalGroups = RESOLUTIONS.get(30).groups.map(
    (group) => group.canonicalGroups[0],
  );
  const exercises = [];
  let exerciseId = 10_000;
  for (const primary of canonicalGroups) {
    const secondary = canonicalGroups.filter((group) => group !== primary);
    exercises.push(exercise(
      exerciseId++,
      primary,
      secondary,
      0,
      EXERCISE_INSECT_COMPATIBILITY.Compatible,
    ));
    exercises.push(exercise(
      exerciseId++,
      primary,
      secondary,
      100,
      EXERCISE_INSECT_COMPATIBILITY.Incompatible,
    ));
  }
  return exercises;
}

function directionPairCatalog() {
  const canonicalGroups = [...new Set(RESOLUTIONS.get(30).groups.flatMap(
    (group) => group.canonicalGroups,
  ))];
  const first = {
    ...exercise(1, canonicalGroups[0], canonicalGroups.slice(1), 100),
    sequenceBlocks: [
      {
        exerciseId: 1,
        sideCue: "None",
        directionCue: "Forward",
        mirrorMedia: false,
        mediaSegment: "Full",
      },
      {
        exerciseId: 2,
        sideCue: "None",
        directionCue: "Backward",
        mirrorMedia: false,
        mediaSegment: "Full",
      },
    ],
  };
  const second = {
    ...exercise(2, canonicalGroups[0], canonicalGroups.slice(1), 100),
    sequenceBlocks: [],
  };
  const fillers = Array.from({ length: 30 }, (_, index) => {
    const primary = canonicalGroups[index % canonicalGroups.length];
    return exercise(
      100 + index,
      primary,
      canonicalGroups.filter((group) => group !== primary),
      0,
    );
  });
  return [first, second, ...fillers];
}

async function assertFile(file) {
  const information = await stat(file);
  assert.equal(information.isFile(), true);
  assert.ok(information.size > 0);
}
