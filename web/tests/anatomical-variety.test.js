import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { MINIMUM_SESSION_MOVEMENTS_PER_ANATOMICAL_GROUP, getAnatomicalGroupVarietyInventory, findAnatomicalGroupVarietyDeficiencies, ANATOMICAL_VARIETY_EXERCISE_IDS, getWorkoutCatalog, WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M } from '../workout.js';
const catalog = JSON.parse(readFileSync(new URL('../../NomadicMethod/Assets/exercises.json', import.meta.url), 'utf8'));
const cases = JSON.parse(readFileSync(new URL('./fixtures/anatomical-variety-cases.json', import.meta.url), 'utf8'));
for (const fixture of cases) test('global anatomical variety: ' + fixture.name, () => {
  const template = catalog.find(e => e.id === fixture.templateId);
  const exercises = fixture.exercises.map(spec => {
    const result = { ...structuredClone(template), ...spec, name: 'Synthetic counter fixture ' + spec.id };
    result.sequenceBlocks = spec.sequenceIds.map(exerciseId => ({ exerciseId, sideCue: 'None', directionCue: 'None', mirrorMedia: false, mediaSegment: 'Full' }));
    delete result.sequenceIds;
    return result;
  });
  const inventory = getAnatomicalGroupVarietyInventory(exercises);
  assert.equal(inventory.length, 90);
  const row = inventory.find(row => row.groupId === fixture.groupId);
  assert.equal(row.matchingExerciseCount, fixture.expectedCount);
  assert.equal(row.requiredExerciseCount, 10);
  assert.equal(findAnatomicalGroupVarietyDeficiencies(exercises).some(row => row.groupId === fixture.groupId), fixture.expectedCount < 10);
});
test('all ninety existing groups and subgroups have ten selectable catalog movements', () => {
  assert.equal(MINIMUM_SESSION_MOVEMENTS_PER_ANATOMICAL_GROUP, 10);
  assert.equal(getAnatomicalGroupVarietyInventory(catalog).length, 90);
  assert.deepEqual(findAnatomicalGroupVarietyDeficiencies(catalog), []);
});

test('additions preserve an unfinished revision-97 workout and its preferences', () => {
  const additions = Array.from({ length: 10 }, (_, index) => 1720 + index);
  assert.deepEqual(ANATOMICAL_VARIETY_EXERCISE_IDS, additions);
  assert.equal(getWorkoutCatalog(catalog, 97).length, 909);
  assert.ok(getWorkoutCatalog(catalog, 97).every(e => !additions.includes(e.id)));
  assert.ok(additions.every(id => getWorkoutCatalog(catalog, 98).some(e => e.id === id)));
  const anatomyCorrections = new Set([1069, 1071, 1209, 1216, 1292]);
  const previousCatalog = structuredClone(getWorkoutCatalog(catalog, 97));
  for (const e of previousCatalog) if (anatomyCorrections.has(e.id))
    e.secondaryCanonicalGroups = e.secondaryCanonicalGroups.filter(g => g !== 'PelvicFloorAndPerineum');
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId['r15.shoulder'] = [301];
  state.scores[918] = -7;
  const now = () => Date.parse('2026-10-26T00:00:00Z');
  const original = new WorkoutSession(previousCatalog, state, () => 0, now);
  original.initialize();
  original.startWorkout(15, M.Floor | M.UpperBodyClothing | M.Wall | M.Elevation | M.SoleWallContact);
  original.state.catalogRevision = 97;
  original.state.activeCatalogRevision = 97;
  const saved = structuredClone(original.state);
  const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0, now);
  restored.restoreAfterReopen();
  for (const key of ['selectedExerciseIds', 'keptExerciseRootIdsBySelectionGroupId', 'scores', 'workoutHistory', 'outcomes', 'exerciseScoreAdjustmentsByPhase', 'exerciseScoreAdjustmentsBySelectionGroupId'])
    assert.deepEqual(restored.state[key], saved[key], key);
  assert.equal(restored.state.activeWorkoutSession.sessionId, saved.activeWorkoutSession.sessionId);
  assert.equal(restored.state.activeCatalogRevision, 97);
  assert.equal(restored.state.catalogRevision, 104);
});
