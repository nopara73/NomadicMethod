import { beforeSurfaceReview, beforeReactionLabelReview, beforeReactionLabelReviewSource } from "./catalog-review-history.js";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import test from "node:test";
import { resolveReactionDrills } from "../reaction-drills.js";
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M, getWorkoutCatalog, getResolution, getMaximumDistinctLineupSize } from "../workout.js";

const catalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
// Historical anatomy evidence predates the pose audit and the explicit arm-pulse
// clarity correction. Rewind only those reviewed name/profile fields for 264;
// the remaining metadata and media must still match the original evidence.
const armPulseCorrection = JSON.parse(readFileSync(new URL("./fixtures/arm-pulses.json", import.meta.url), "utf8"));
const anatomyReviewCatalog = getWorkoutCatalog(catalog, 99).map(exercise =>
  exercise.id === armPulseCorrection.id ? { ...exercise,
    name: armPulseCorrection.previousName, motionProfile: armPulseCorrection.previousMotionProfile } : exercise)
  .map(exercise => beforeSurfaceReview(exercise, 99));
const review = JSON.parse(readFileSync(new URL("../../docs/catalog-audit/anatomical-region-review-2026-10-26.json", import.meta.url), "utf8"));
const laterReview = JSON.parse(readFileSync(new URL("../../docs/catalog-audit/anatomical-variety-review-2026-10-26.json", import.meta.url), "utf8"));
const reactionDocument = beforeReactionLabelReview(JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/reaction_drills.json", import.meta.url), "utf8")));
const now = () => Date.parse("2026-10-26T00:00:00Z");

test("anatomy corrections remain bound to reviewed movements and media", () => {
  assert.equal(review.entries.length, 84);
  assert.equal(new Set(review.entries.map(e => e.id)).size, 84);
  for (const entry of review.entries) {
    const exercise = structuredClone(anatomyReviewCatalog.find(e => e.id === entry.id));
    const amendment = laterReview.entries.find(e => e.id === entry.id && e.kind === 'AnatomyCorrection');
    if (amendment) {
      assert.equal(amendment.previousMetadataSha256, entry.metadataSha256, 'Explicit review chain: ' + entry.id);
      assert.equal(createHash('sha256').update(JSON.stringify(exercise)).digest('hex'), amendment.metadataSha256);
      exercise.secondaryCanonicalGroups = exercise.secondaryCanonicalGroups.filter(g => !amendment.addedSecondaryGroups.includes(g));
    }
    assert.equal(createHash("sha256").update(JSON.stringify(exercise)).digest("hex"), entry.metadataSha256, String(entry.id));
    const previous = structuredClone(exercise);
    previous.secondaryCanonicalGroups = previous.secondaryCanonicalGroups.filter(g => !entry.addedSecondaryGroups.includes(g));
    assert.equal(createHash("sha256").update(JSON.stringify(previous)).digest("hex"), entry.previousMetadataSha256, "Only reviewed secondary claims change: " + entry.id);
    assert.ok(entry.addedSecondaryGroups.every(g => exercise.secondaryCanonicalGroups.includes(g)));
    const expectedAssets = [exercise.video];
    if (exercise.mode === "Hold") expectedAssets.push(`exercise_hold_frames/exercise_${String(exercise.id).padStart(4, "0")}.png`);
    if (exercise.directionSequence !== "None") expectedAssets.push(`exercise_direction_videos/exercise_${String(exercise.id).padStart(4, "0")}.mp4`);
    if (exercise.practice === "Reaction training") {
      const plan = resolveReactionDrills(reactionDocument, catalog).get(exercise.id);
      assert.deepEqual(entry.reactionPlan, plan);
      expectedAssets.push("reaction_drills.json");
      for (const action of plan.actions) {
        expectedAssets.push(action.video, action.preview);
        assert.ok(entry.assets[action.video]);
        assert.ok(entry.assets[action.preview]);
      }
    }
    assert.deepEqual(Object.keys(entry.assets).sort(), expectedAssets.sort());
    assert.ok(entry.method.length > 0);
    assert.equal(entry.workoutReplay.realTimePlayback, true);
    assert.deepEqual(entry.workoutReplay.errors, []);
    assert.ok(entry.workoutReplay.blocks.some(block => block.id === entry.id));
    for (const [asset, expected] of Object.entries(entry.assets)) {
      const data = readFileSync(new URL("../../NomadicMethod/Assets/" + asset, import.meta.url));
      const content = asset === "reaction_drills.json" ? beforeReactionLabelReviewSource(data.toString("utf8").replaceAll("\r\n", "\n")) : data;
      assert.equal(createHash("sha256").update(content).digest("hex"), expected, asset);
    }
    assert.deepEqual(Object.keys(entry.checks).sort(), ["Metadata", "FullLoop", "Crop", "LoopSeam", "PlaybackSpeed", "Mirroring", "Travel", "HoldFrame", "Equipment", "Workout"].sort());
    for (const [dimension, check] of Object.entries(entry.checks)) {
      assert.ok(check.reason.length > 0);
      assert.ok(check.status === "PASS" || (dimension === "HoldFrame" && exercise.mode !== "Hold" && check.status === "NOT_APPLICABLE"));
    }
  }
});

for (const [id, slot, modifiers] of [[301, "r30.rotator-cuff", 0], [1214, "r30.accessory-hip-adductors", M.Floor | M.UpperBodyClothing], [570, "r30.forearm-flexors-pronators", 0]])
  test("corrected movement has a complete placement in its existing fine slot: " + id, () => {
    const root = catalog.find(e => e.id === id);
    const members = catalog.filter(e => root.sequenceBlocks.some(b => b.exerciseId === e.id));
    const group = getResolution(30).groups.find(g => g.id === slot);
    assert.equal(getMaximumDistinctLineupSize(members, [group], modifiers, root.sequenceBlocks.length), 1);
    if (root.sequenceBlocks.length > 1)
      assert.equal(getMaximumDistinctLineupSize(members, [group], modifiers, root.sequenceBlocks.length - 1), 0);
    if (id === 1214) assert.equal(getMaximumDistinctLineupSize(members, [group], 0, 30), 0);
  });

test("catalog 96 workouts, Keeps, history and downvotes survive the additive update", () => {
  const legacy = structuredClone(catalog.filter(e => e.id < 1720 || e.id > 1729));
  for (const entry of laterReview.entries.filter(e => e.kind === 'AnatomyCorrection')) {
    const e = legacy.find(e => e.id === entry.id);
    e.secondaryCanonicalGroups = e.secondaryCanonicalGroups.filter(g => !entry.addedSecondaryGroups.includes(g));
  }
  for (const entry of review.entries) {
    const e = legacy.find(e => e.id === entry.id);
    e.secondaryCanonicalGroups = e.secondaryCanonicalGroups.filter(g => !entry.addedSecondaryGroups.includes(g));
  }
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId["r15.shoulder"] = [301];
  state.exerciseScoreAdjustmentsByPhase.Warmup = {301: -3};
  state.scores[570] = -5;
  const previous = new WorkoutSession(legacy, state, () => 0, now);
  previous.initialize();
  previous.startWorkout(15, 0);
  previous.state.catalogRevision = 96;
  previous.state.activeCatalogRevision = 96;
  const saved = structuredClone(previous.state);
  const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0, now);
  restored.restoreAfterReopen();
  for (const key of ["selectedExerciseIds", "keptExerciseRootIdsBySelectionGroupId", "exerciseScoreAdjustmentsByPhase", "exerciseScoreAdjustmentsBySelectionGroupId", "workoutHistory", "scores"])
    assert.deepEqual(restored.state[key], saved[key], key);
  assert.equal(restored.state.activeWorkoutSession.sessionId, saved.activeWorkoutSession.sessionId);
  assert.equal(restored.state.activeCatalogRevision, 96);
  assert.equal(restored.state.catalogRevision, 104);
});
