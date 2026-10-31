import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { exerciseIdentityName, exerciseDisplayName } from '../exercise-names.js';
import { CURRENT_CATALOG_REVISION, RESOLUTIONS, WORKOUT_MODIFIERS,
  WorkoutSession, createDefaultState, isSelectable } from '../workout.js';

const load = async file => JSON.parse(await readFile(new URL(file, import.meta.url), 'utf8'));
const catalog = await load('../../NomadicMethod/Assets/exercises.json');
const policy = await load('../../NomadicMethod/Assets/exercise_names.json');

test('shared reviewed names are display aliases of the existing movements', () => {
  assert.equal(policy.names.length, 709);
  for (const entry of policy.names) {
    assert.equal(catalog.find(e => e.id === entry.id).name, entry.name);
    for (const name of [entry.identityName, entry.name, ...entry.previousNames]) {
      assert.equal(exerciseIdentityName(entry.id, name), entry.identityName);
      assert.equal(exerciseDisplayName(entry.id, name), entry.name);
    }
    assert.equal(exerciseIdentityName(entry.id, 'Unreviewed movement'), 'Unreviewed movement');
  }
});

test('all pre-rename scores survive catalog reconciliation', () => {
  const previous = new Map(policy.names.map(e => [e.id, e.identityName]));
  const state = createDefaultState();
  state.catalogRevision = 103;
  for (const e of catalog) {
    state.catalogIdentities[e.id] = `${previous.get(e.id) ?? e.name}\u001f${e.video}`;
    state.scores[e.id] = -e.id;
  }
  const restored = new WorkoutSession(catalog, state, () => 0);
  restored.reconcileCatalog();
  for (const e of catalog) assert.equal(restored.getScore(e), -e.id, `${e.id}: feedback retained`);
  assert.equal(restored.state.catalogRevision, CURRENT_CATALOG_REVISION);
});

test('display renames retain Ready, paused movement and paused rest checkpoints', () => {
  const exercise = catalog.find(e => e.id === 1255);
  const group = RESOLUTIONS.get(30).groups.find(g => isSelectable(exercise, g));
  for (const phase of ['ready', 'movement', 'rest']) {
    const state = createDefaultState();
    state.catalogRevision = 103;
    state.activeCatalogRevision = 103;
    state.activeWorkoutMinutes = 30;
    state.activeWorkoutModifiers = WORKOUT_MODIFIERS.Floor;
    state.lastWorkoutModifiers = WORKOUT_MODIFIERS.Floor;
    state.catalogIdentities[1255] = `Narrow-Hand Push-Ups\u001f${exercise.video}`;
    state.selectedExerciseIds[group.id] = 1255;
    state.keptExerciseRootIdsBySelectionGroupId[group.id] = [1255];
    state.lastKeptExerciseIds = [1255];
    state.scores[1255] = -3;
    if (phase !== 'ready') {
      const prefix = phase === 'movement' ? 'pendingMovement' : 'pendingRest';
      state[`${prefix}GroupId`] = group.id;
      state[`${prefix}MillisecondsRemaining`] = 31_123;
      state[`${prefix}PausedByUser`] = true;
    }
    const expected = structuredClone(state);
    const restored = new WorkoutSession(catalog, state, () => 0);
    restored.reconcileCatalog();
    expected.catalogRevision = CURRENT_CATALOG_REVISION;
    expected.catalogIdentities = restored.state.catalogIdentities;
    assert.deepEqual(restored.state, expected, phase);
  }
});

test('reviewed aliases never admit unknown names or changed videos', () => {
  const e = catalog.find(e => e.id === 1255);
  for (const identity of [`Unknown push-up\u001f${e.video}`, 'Narrow-Hand Push-Ups\u001fother.mp4']) {
    const state = createDefaultState();
    state.catalogRevision = CURRENT_CATALOG_REVISION;
    state.catalogIdentities[e.id] = identity;
    state.scores[e.id] = -4;
    const restored = new WorkoutSession(catalog, state, () => 0);
    restored.reconcileCatalog();
    assert.equal(restored.getScore(e), 0);
  }
});
