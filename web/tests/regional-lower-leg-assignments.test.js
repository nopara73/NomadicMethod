import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import test from "node:test";
import { WorkoutSession, createDefaultState, getSessionMovementId, isCompatibleWithWorkoutModifiers as isExerciseCompatible, WORKOUT_MODIFIERS as M } from "../workout.js";

const catalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const region = "AnteriorLateralLowerLegAndDorsalFoot";
const slot = "r10.anterior-lateral-lower-leg-dorsal-foot";
const profile = M.HardFloor | M.Wall | M.Silence;
const now = () => Date.parse("2026-10-26T00:00:00Z");

test("restored assignments remain bound to reviewed metadata and exact runtime media", () => {
  const review = JSON.parse(readFileSync(new URL(
    "../../docs/catalog-audit/lower-leg-review-2026-10-26.json", import.meta.url), "utf8"));
  assert.equal(review.entries.length, 40);
  assert.equal(new Set(review.entries.map(entry => entry.id)).size, 40);
  for (const entry of review.entries) {
    assert.deepEqual(Object.keys(entry.checks).sort(), ["Metadata", "FullLoop", "Crop", "LoopSeam",
      "PlaybackSpeed", "Mirroring", "Travel", "HoldFrame", "Equipment", "Workout"].sort());
    const exercise = catalog.find(candidate => candidate.id === entry.id);
    assert.ok(exercise.secondaryCanonicalGroups.includes(region), String(entry.id));
    assert.equal(createHash("sha256").update(JSON.stringify(exercise)).digest("hex"), entry.metadataSha256,
      `metadata ${entry.id}`);
    for (const [asset, expected] of Object.entries(entry.assets)) {
      const data = readFileSync(new URL("../../NomadicMethod/Assets/" + asset, import.meta.url));
      assert.equal(createHash("sha256").update(data).digest("hex"), expected, asset);
    }
    for (const [dimension, check] of Object.entries(entry.checks)) {
      assert.ok(check.reason.length > 0, `${entry.id} ${dimension}`);
      assert.ok(check.status === "PASS" || (dimension === "HoldFrame" && exercise.mode !== "Hold" &&
        check.status === "NOT_APPLICABLE"), `${entry.id} ${dimension}`);
    }
  }
});

test("follow-up assignments remain bound to reviewed metadata and exact runtime media", () => {
  const review = JSON.parse(readFileSync(new URL(
    "../../docs/catalog-audit/regional-followup-review-2026-10-26.json", import.meta.url), "utf8"));
  assert.equal(review.entries.length, 13);
  assert.equal(new Set(review.entries.map(entry => entry.id)).size, 13);
  for (const entry of review.entries) {
    assert.deepEqual(Object.keys(entry.checks).sort(), ["Metadata", "FullLoop", "Crop", "LoopSeam",
      "PlaybackSpeed", "Mirroring", "Travel", "HoldFrame", "Equipment", "Workout"].sort());
    const exercise = catalog.find(candidate => candidate.id === entry.id);
    const groups = entry.id === 419
      ? ["AnteriorLateralNeckAndHyoidMuscles", "PosteriorNeckAndSuboccipitalMuscles"] : [region];
    assert.ok(groups.every(group => exercise.secondaryCanonicalGroups.includes(group)), String(entry.id));
    assert.equal(entry.workoutReplay.realTimePlayback, true);
    assert.deepEqual(entry.workoutReplay.errors, []);
    assert.ok(entry.workoutReplay.blocks.some(block => block.id === entry.id));
    assert.equal(createHash("sha256").update(JSON.stringify(exercise)).digest("hex"), entry.metadataSha256,
      `metadata ${entry.id}`);
    for (const [asset, expected] of Object.entries(entry.assets)) {
      const data = readFileSync(new URL("../../NomadicMethod/Assets/" + asset, import.meta.url));
      assert.equal(createHash("sha256").update(data).digest("hex"), expected, asset);
    }
    for (const [dimension, check] of Object.entries(entry.checks)) {
      assert.ok(check.reason.length > 0, `${entry.id} ${dimension}`);
      assert.ok(check.status === "PASS" || (dimension === "HoldFrame" && exercise.mode !== "Hold" &&
        check.status === "NOT_APPLICABLE"), `${entry.id} ${dimension}`);
    }
  }
});

test("lateral-compartment work can actually fill the existing regional round", () => {
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[slot] = [389];
  const session = new WorkoutSession(catalog, state, () => 0, now);
  session.initialize();
  session.startWorkout(10, profile);
  assert.ok(session.state.activeWorkoutSession.initialSelections.some(selection =>
    selection.rootExerciseId === 389 && selection.coveredWorkoutGroupIds.includes(slot)));
  assert.equal(new Set(catalog.filter(exercise => [187,252,253,254,562,566,581,582]
    .includes(exercise.id)).map(getSessionMovementId)).size, 1);
});

for (const sourceRevision of [94, 95])
for (const evidenceVersion of [3, 4])
test(`additive anatomy correction preserves a workout, Keeps and feedback from catalog ${sourceRevision} and fitness evidence ${evidenceVersion}`, () => {
  const legacy = structuredClone(catalog);
  for (const exercise of legacy) {
    if (sourceRevision === 95 && ![428,434,439,441,442,444,445,458,462,464,465,1051].includes(exercise.id)) continue;
    if (![1029,1210,1295,1410].includes(exercise.id))
      exercise.secondaryCanonicalGroups = exercise.secondaryCanonicalGroups.filter(group => group !== region);
  }
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[slot] = [165];
  state.exerciseScoreAdjustmentsByPhase.Warmup = {1295: -2};
  state.exerciseScoreAdjustmentsBySelectionGroupId[slot] = {191: -3};
  state.scores[187] = -4;
  const previous = new WorkoutSession(legacy, state, () => 0, now);
  previous.initialize();
  previous.startWorkout(10, profile);
  previous.state.catalogRevision = sourceRevision;
  previous.state.activeCatalogRevision = sourceRevision;
  previous.state.fitnessProfileEvidenceVersion = evidenceVersion;
  const saved = structuredClone(previous.state);
  const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0, now);
  restored.restoreAfterReopen();
  for (const key of ["selectedExerciseIds", "keptExerciseRootIdsBySelectionGroupId", "exerciseScoreAdjustmentsByPhase",
    "exerciseScoreAdjustmentsBySelectionGroupId", "workoutHistory", "scores"])
    assert.deepEqual(restored.state[key], saved[key], key);
  assert.equal(restored.state.activeWorkoutSession.sessionId, saved.activeWorkoutSession.sessionId);
  assert.equal(restored.state.activeCatalogRevision, sourceRevision);
  assert.equal(restored.state.catalogRevision, 96);
});

for (const [id, minutes, target] of [[428,10,"r10.anterior-lateral-lower-leg-dorsal-foot"]])
test("corrected movement fills existing anatomical slot: " + id, () => {
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[target] = [id];
  const session = new WorkoutSession(catalog, state, () => 0, now);
  session.initialize();
  session.startWorkout(minutes, 0);
  assert.ok(session.state.activeWorkoutSession.initialSelections.some(selection =>
    selection.rootExerciseId === id && selection.coveredWorkoutGroupIds.includes(target)));
});

test("hopping assignments preserve Hard Floor and Silence restrictions", () => {
  for (const exercise of catalog.filter(e => [428,434,439,441,442,444,445,458,462,464,465,1051].includes(e.id))) {
    assert.equal(isExerciseCompatible(exercise, M.HardFloor), false, String(exercise.id));
    assert.equal(isExerciseCompatible(exercise, M.Silence), false, String(exercise.id));
  }
});
