import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  CURRENT_CATALOG_REVISION,
  RESOLUTIONS,
  SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
  SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
  WORKOUT_EXERCISE_PHASE,
  WorkoutSession,
  createDefaultState,
} from "../workout.js";

const catalog = JSON.parse(await readFile(new URL(
  "../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const review = JSON.parse(await readFile(new URL(
  "../../docs/catalog-audit/training-claim-corrections-2026-09-05.json",
  import.meta.url), "utf8"));
const migration = JSON.parse(await readFile(new URL(
  "../../docs/catalog-audit/migration-2026-09-05.json", import.meta.url), "utf8"));

test("obsolete clarity aliases cannot restore scores from different historical actions", () => {
  const historicalNames = new Map([
    [241, "Hook-Fist Tendon Glide"], [242, "Full-Fist Tendon Glide"],
    [256, "Self-Resisted Overhead Pull"], [257, "Self-Resisted Chest-Level Pull"], [258, "Self-Resisted Low Pull"],
    [262, "Standing Hands-to-Thigh Abdominal Press"], [270, "Bodyweight Svend Press"],
    [283, "Straight-Fist Tendon Glide"], [291, "Open-to-Claw Tendon Glide"],
    [394, "Standing Open-and-Close Breathing"],
    [395, "Standing Overhead Rib-Expansion Breathing"],
    [425, "Chin-Tuck Isometric"], [556, "Standing Fist Clench and Release"],
  ]);
  const state = createDefaultState();
  // Isolate identity reconciliation from revision-based score invalidation.
  state.catalogRevision = CURRENT_CATALOG_REVISION;
  for (const exercise of catalog) {
    state.catalogIdentities[exercise.id] =
      `${historicalNames.get(exercise.id) ?? exercise.name}\u001f${exercise.video}`;
    state.scores[exercise.id] = -exercise.id;
  }
  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();
  for (const exercise of catalog) {
    assert.equal(restored.getScore(exercise), historicalNames.has(exercise.id) ? 0 : -exercise.id,
      `${exercise.id}: only the replaced historical identity loses its score`);
  }
});

test("integrity migration preserves corrections but discards different actions", () => {
  const state = createDefaultState();
  state.catalogRevision = 72;
  const previousNames = new Map([...migration.nameCorrections, ...migration.identityReplacements]
    .map(c => [c.id, c.previousName]));
  const replacedIds = new Set(migration.identityReplacements.map(c => c.id));
  assert.equal(replacedIds.size, 17);
  const priorCatalog = catalog.map(e => ({ ...e, name: previousNames.get(e.id) ?? e.name }));
  const prior = new WorkoutSession(priorCatalog, state, () => 0);
  prior.reconcileCatalog();
  for (const exercise of catalog) prior.setScore(exercise, -exercise.id);
  prior.state.catalogRevision = 72;
  const restored = new WorkoutSession(catalog, prior.state, () => 0);
  restored.reconcileCatalog();
  for (const exercise of catalog) {
    assert.equal(restored.getScore(exercise), replacedIds.has(exercise.id) ? 0 : -exercise.id);
  }
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
  assert.deepEqual(SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(73), replacedIds);
});

test("different-action replacements clear even anatomically valid old preferences", () => {
  for (const replacement of migration.identityReplacements) {
    const e = catalog.find(item => item.id === replacement.id);
    const slot = RESOLUTIONS.get(30).groups.find(g => g.canonicalGroups.includes(e.primaryCanonicalGroup)).id;
    const state = createDefaultState();
    state.catalogRevision = 72;
    state.activeWorkoutMinutes = 30;
    state.selectedExerciseIds = { [slot]: e.id };
    state.keptExerciseRootIdsBySelectionGroupId = { [slot]: [e.id] };
    state.exerciseScoreAdjustmentsBySelectionGroupId = { [slot]: { [e.id]: -2 } };
    state.exerciseScoreAdjustmentsByPhase = { [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { [e.id]: -3 } };
    state.lastKeptExerciseIds = [e.id];
    state.scores = { [e.id]: -4 };
    const restored = new WorkoutSession(catalog, state, () => 0);
    restored.reconcileCatalog();
    assert.deepEqual(restored.state.selectedExerciseIds, {});
    assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId, {});
    assert.deepEqual(restored.state.exerciseScoreAdjustmentsBySelectionGroupId, {});
    assert.deepEqual(restored.state.exerciseScoreAdjustmentsByPhase, {});
    assert.deepEqual(restored.state.lastKeptExerciseIds, []);
    assert.equal(restored.getScore(e), 0);
  }
});

test("every changed standalone claim removes only impossible fine-slot selections and Keeps", () => {
  const byId = new Map(catalog.map(e => [e.id, e]));
  const slotByMuscle = new Map(RESOLUTIONS.get(30).groups.map(g => [g.canonicalGroups[0], g.id]));
  const placementChanges = SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(73);
  let reviewedRoots = 0;
  const replacedIds = new Set(migration.identityReplacements.map(c => c.id));
  for (const entry of review.entries) {
    // Newly admitted IDs have no historical fine-slot preference.
    if (entry.previousPrimary === null) continue;
    if (replacedIds.has(entry.id) || migration.retiredIds.includes(entry.id)) continue;
    const e = byId.get(entry.id);
    if (e.sequenceBlocks.length === 0 || e.sequenceBlocks.some(b => b.exerciseId !== e.id)) continue;
    const previous = new Set([entry.previousPrimary, ...entry.previousSecondary]);
    const current = new Set([e.primaryCanonicalGroup, ...e.secondaryCanonicalGroups]);
    if (previous.size === current.size && [...previous].every(m => current.has(m))) continue;
    reviewedRoots++;
    const muscles = new Set([...previous, ...current]);
    const slots = [...muscles].map(m => slotByMuscle.get(m));
    const state = createDefaultState();
    state.catalogRevision = 72;
    state.activeWorkoutMinutes = 30;
    state.selectedExerciseIds = Object.fromEntries(slots.map(s => [s, e.id]));
    state.keptExerciseRootIdsBySelectionGroupId = Object.fromEntries(slots.map(s => [s, [e.id]]));
    state.exerciseScoreAdjustmentsBySelectionGroupId = Object.fromEntries(slots.map(s => [s, { [e.id]: -2 }]));
    state.exerciseScoreAdjustmentsByPhase = { [WORKOUT_EXERCISE_PHASE.PeakPerformance]: { [e.id]: -3 } };
    state.lastKeptExerciseIds = [e.id];
    state.scores = { [e.id]: -4 };
    const restored = new WorkoutSession(catalog, state, () => 0);
    restored.reconcileCatalog();
    for (const muscle of muscles) {
      const slot = slotByMuscle.get(muscle);
      const trained = current.has(muscle);
      const context = `${e.id} ${slot}`;
      assert.equal(restored.state.keptExerciseRootIdsBySelectionGroupId[slot] !== undefined, trained, `Keep ${context}`);
      assert.equal(restored.state.exerciseScoreAdjustmentsBySelectionGroupId[slot] !== undefined, trained, `slot score ${context}`);
      assert.equal(restored.state.selectedExerciseIds[slot] !== undefined, trained && !placementChanges.has(e.id), `selection ${context}`);
    }
    assert.deepEqual(restored.state.lastKeptExerciseIds, [e.id]);
    assert.equal(restored.state.exerciseScoreAdjustmentsByPhase[WORKOUT_EXERCISE_PHASE.PeakPerformance][e.id], -3);
    assert.equal(restored.getScore(e), -4);
  }
  assert.equal(reviewedRoots, 364);
});
