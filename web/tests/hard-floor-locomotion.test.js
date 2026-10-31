import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import test from 'node:test';
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M,
  RESOLUTIONS, CURRENT_CATALOG_REVISION, getSessionMovementId,
  isCompatibleWithWorkoutModifiers } from '../workout.js';

const read = file => JSON.parse(readFileSync(new URL(file, import.meta.url), 'utf8'));
const review = read('../../docs/catalog-audit/hard-floor-locomotion-review-2026-10-31.json');
const catalog = read('../../NomadicMethod/Assets/exercises.json');
const byId = new Map(catalog.map(exercise => [exercise.id, exercise]));
const profile = M.Floor | M.HardFloor | M.RoomLocomotion | M.Wall | M.SoleWallContact;

test('the locomotion review covers the exact 44 original exclusions once', () => {
  assert.equal(review.entries.length, 44);
  assert.equal(new Set(review.entries.map(entry => getSessionMovementId(byId.get(entry.id)))).size, 44);
  assert.equal(review.entries.filter(entry => entry.compatibility === 'Compatible').length, 11);
  assert.equal(review.entries.filter(entry => entry.compatibility === 'Incompatible').length, 33);
});

for (const entry of review.entries) {
  test(`reviewed Hard Floor locomotion: ${entry.id} ${entry.name}`, () => {
    const exercise = byId.get(entry.id);
    assert.equal(exercise.name, entry.name);
    assert.equal(exercise.floorContactPoints, entry.floorContactPoints);
    assert.equal(exercise.hardFloorCompatibility, entry.compatibility);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, profile), entry.compatibility === 'Compatible');
    assert.equal(isCompatibleWithWorkoutModifiers(exercise,
      (profile & ~M.HardFloor) | M.UpperBodyClothing), true);
    if (entry.compatibility === 'Compatible')
      assert.equal(isCompatibleWithWorkoutModifiers(exercise, profile & ~M.Floor), exercise.onlyFeetTouchGround);
    const footage = readFileSync(new URL(`../../NomadicMethod/Assets/${entry.video}`, import.meta.url));
    assert.equal(createHash('sha256').update(footage).digest('hex'), entry.videoSha256,
      'A changed packaged demonstration requires a fresh surface review');
  });
}

test('the reviewed catalog has 18 physically compatible Room movements with Floor available', () => {
  const roots = catalog.filter(exercise => exercise.sequenceBlocks.length &&
    exercise.locomotionSpace === 'Room' && exercise.sequenceBlocks.every(block =>
      isCompatibleWithWorkoutModifiers(byId.get(block.exerciseId), profile)));
  assert.equal(new Set(roots.map(getSessionMovementId)).size, 18);
});

for (const phase of ['Ready', 'Movement', 'Rest']) {
  test(`surface metadata upgrade preserves the old ${phase} workout and feedback`, () => {
    const changed = new Set(review.entries.filter(entry => entry.compatibility === 'Compatible').map(entry => entry.id));
    const previous = catalog.map(exercise => changed.has(exercise.id)
      ? {...exercise, hardFloorCompatibility: 'Incompatible'} : exercise);
    const group = RESOLUTIONS.get(30).groups.find(group => group.canonicalGroups.includes('AbdominalWall'));
    const state = createDefaultState();
    Object.assign(state, {catalogRevision:100, activeCatalogRevision:100, activeWorkoutMinutes:30,
      activeWorkoutModifiers:profile, lastWorkoutModifiers:profile,
      activeDurationSelectionGroupIds:[group.id], activeSelectionGroupOrder:[group.id],
      activeSetupOrderVersion:1, selectedExerciseIds:{[`p${profile}|${group.id}`]:1223},
      scores:{1223:7}, keptExerciseRootIdsBySelectionGroupId:{[group.id]:[1223]}, lastKeptExerciseIds:[1223]});
    const now = Date.parse('2026-10-31T05:00:00Z');
    const old = new WorkoutSession(previous, state, () => 0.5, () => now);
    old.initialize();
    const next = old.getNextGroup();
    if (phase === 'Movement') old.beginMovement(next, 32000, now + 32000);
    if (phase === 'Rest') old.beginRest(next, now + 15000);
    old.state.catalogRevision = 100;
    const saved = structuredClone(old.state);
    const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0.9, () => now);
    restored.initialize();
    assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
    assert.equal(restored.state.activeCatalogRevision, 100);
    for (const key of ['selectedExerciseIds','activeSelectionGroupOrder','activeSetCountsBySelectionGroupId',
      'scores','keptExerciseRootIdsBySelectionGroupId','exerciseScoreAdjustmentsByPhase',
      'workoutHistory','activeWorkoutSession','outcomes','lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle',
      'lastHardWorkUnixMillisecondsByPrimaryMuscle','pendingMovementGroupId','pendingRestGroupId'])
      assert.deepEqual(restored.state[key], saved[key], key);
  });
}
