import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { FITNESS_DIMENSIONS, parseFitnessRatings, inspectFitnessProfile,
  calculateFavouriteFitnessProfile, hasFitnessTrainingClaim, isFitnessEligible,
  isFitnessSequenceEligible, serializeFitnessRatings } from '../fitness-profile.js';

const fixture = JSON.parse(readFileSync(new URL('./fixtures/complete-fitness-ratings-cases.json', import.meta.url), 'utf8'));
const exercises = [{ id: 1, name: 'minimum demand' }, { id: 2, name: 'reviewed training' }];
const stateFor = id => ({ workoutHistory: [{ sessionId: 1, decisions: [
  { rootExerciseId: id, outcome: 'easy', decidedAtUnixMilliseconds: 1000 },
] }] });

test('every runtime exercise has ten explicit nonempty ratings from 1 through 5', () => {
  const source = JSON.parse(readFileSync(new URL('../../NomadicMethod/Assets/fitness_ratings.json', import.meta.url), 'utf8'));
  const catalog = JSON.parse(readFileSync(new URL('../../NomadicMethod/Assets/exercises.json', import.meta.url), 'utf8'));
  const ratings = parseFitnessRatings(source);
  assert.equal(source.schemaVersion, 2);
  assert.equal(ratings.size, catalog.length);
  for (const exercise of catalog) {
    const levels = ratings.get(exercise.id);
    assert.equal(levels.length, 10);
    for (const set of levels) {
      assert.ok(set.length > 0, `${exercise.id} has an empty rating`);
      assert.ok(set.every(level => Number.isInteger(level) && level >= 1 && level <= 5));
    }
  }
});

test('empty and invalid ratings fail on every characteristic instead of receiving a default', () => {
  for (let dimension = 0; dimension < 10; dimension++) {
    for (const invalid of fixture.invalidLevelSets) {
      const source = structuredClone(fixture.ratings);
      source.ratings[2][dimension] = invalid;
      assert.throws(() => parseFitnessRatings(source), /nonempty fitness ratings/);
    }
    const missing = structuredClone(fixture.ratings);
    delete missing.ratings[2][dimension];
    assert.throws(() => parseFitnessRatings(JSON.parse(JSON.stringify(missing))));
  }
  const legacy = structuredClone(fixture.ratings);
  legacy.schemaVersion = 1;
  assert.throws(() => parseFitnessRatings(legacy), /schema/);
  const missingClaims = structuredClone(fixture.ratings);
  delete missingClaims.trainingCharacteristics[2];
  assert.throws(() => parseFitnessRatings(missingClaims));
});

test('minimum ratings do not invent training coverage or fitness evidence', () => {
  const ratings = parseFitnessRatings(fixture.ratings);
  assert.ok(ratings.get(1).every(set => JSON.stringify(set) === '[1]'));
  assert.ok(FITNESS_DIMENSIONS.every((_, i) => !hasFitnessTrainingClaim(ratings, 1, i)));
  const state = stateFor(1), before = JSON.stringify(state);
  assert.ok(inspectFitnessProfile(exercises, state, ratings).every(axis => axis.mean === 0 && axis.contributions.length === 0));
  assert.deepEqual(calculateFavouriteFitnessProfile(exercises, { lastKeptExerciseIds: [1] }, ratings), new Array(10).fill(0));
  assert.equal(JSON.stringify(state), before);
  assert.equal(isFitnessEligible(ratings.get(1), FITNESS_DIMENSIONS.map(() => [1])), true);
  assert.equal(isFitnessEligible(ratings.get(1), FITNESS_DIMENSIONS.map(() => [2])), false);
});

test('difficulty feedback retains its reviewed training evidence without altering saved history', () => {
  const ratings = parseFitnessRatings(fixture.ratings), state = stateFor(2), before = JSON.stringify(state);
  assert.deepEqual(inspectFitnessProfile(exercises, state, ratings).map(axis => axis.mean), fixture.expectedEasyProfile);
  assert.equal(JSON.stringify(state), before);
});

test('incidental minimum ratings cannot qualify advanced work or exempt minimum-only work', () => {
  const ratings = parseFitnessRatings(fixture.ratings);
  const minimum = FITNESS_DIMENSIONS.map(() => [1]);
  const advancedClaims = ratings.trainingCharacteristics.get(2);
  const minimumClaims = ratings.trainingCharacteristics.get(1);
  assert.equal(isFitnessEligible(ratings.get(2), minimum, advancedClaims), false);
  const mixed = minimum.map(set => [...set]);
  mixed[1] = [2];
  assert.equal(isFitnessEligible(ratings.get(1), mixed, minimumClaims), false);
  assert.equal(isFitnessEligible(ratings.get(1), minimum, minimumClaims), true);
  assert.equal(isFitnessSequenceEligible([ratings.get(1), ratings.get(2)], minimum,
    [minimumClaims, advancedClaims]), false);
});

test('worker serialization preserves reviewed evidence and complete ratings', () => {
  const original = parseFitnessRatings(fixture.ratings);
  const restored = parseFitnessRatings(structuredClone(serializeFitnessRatings(original)));
  assert.deepEqual([...restored], [...original]);
  assert.deepEqual([...restored.trainingCharacteristics], [...original.trainingCharacteristics]);
  assert.deepEqual(inspectFitnessProfile(exercises, stateFor(2), restored),
    inspectFitnessProfile(exercises, stateFor(2), original));
  assert.ok(inspectFitnessProfile(exercises, stateFor(1), restored).every(axis => axis.mean === 0));
});
