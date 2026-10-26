import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import test from "node:test";
import { WorkoutSession, createDefaultState, getSessionMovementId, WORKOUT_MODIFIERS as M } from "../workout.js";

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

for (const evidenceVersion of [3, 4])
test(`additive anatomy correction preserves a workout, Keeps and feedback from fitness evidence ${evidenceVersion}`, () => {
  const legacy = structuredClone(catalog);
  for (const exercise of legacy) {
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
  previous.state.catalogRevision = 94;
  previous.state.activeCatalogRevision = 94;
  previous.state.fitnessProfileEvidenceVersion = evidenceVersion;
  const saved = structuredClone(previous.state);
  const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0, now);
  restored.restoreAfterReopen();
  for (const key of ["selectedExerciseIds", "keptExerciseRootIdsBySelectionGroupId", "exerciseScoreAdjustmentsByPhase",
    "exerciseScoreAdjustmentsBySelectionGroupId", "workoutHistory", "scores"])
    assert.deepEqual(restored.state[key], saved[key], key);
  assert.equal(restored.state.activeWorkoutSession.sessionId, saved.activeWorkoutSession.sessionId);
  assert.equal(restored.state.activeCatalogRevision, 94);
  assert.equal(restored.state.catalogRevision, 95);
});
