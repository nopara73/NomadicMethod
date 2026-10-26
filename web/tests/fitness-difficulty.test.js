import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { calculateFitnessProfile, inspectFitnessProfile, calculateFavouriteFitnessProfile, FitnessProfileView,
  FITNESS_DIMENSIONS, fitnessZones, parseFitnessRatings, roundFitnessLevel, formatFitnessLevels } from "../fitness-profile.js";
import { WorkoutSession, createDefaultState } from "../workout.js";

const read = file => JSON.parse(readFileSync(new URL(file, import.meta.url), "utf8"));
const fixture = read("./fixtures/fitness-difficulty-cases.json");
for (const entry of fixture.cases) test("difficulty profile: " + entry.name, () => {
  const exercises = entry.exercises.map(e => ({ ...e, name: "movement " + e.id }));
  const ratings = new Map(exercises.map(e => [e.id, e.levels]));
  ratings.set(999, FITNESS_DIMENSIONS.map(() => [5])); // Retired catalog identity.
  const before = JSON.stringify(entry);
  const profile = inspectFitnessProfile(exercises, entry.state, ratings);
  assert.deepEqual(calculateFitnessProfile(exercises, entry.state, ratings), entry.expected);
  for (let i = 0; i < profile.length; i++) {
    assert.equal(profile[i].mean, entry.expected[i]);
    assert.deepEqual(profile[i].contributions.map(c => [c.movementId, c.exerciseId,
      c.ratedLevels, c.feedback, c.supportedLevels]), entry.evidence[FITNESS_DIMENSIONS[i]] ?? []);
    assert.deepEqual(profile[i].bestLevels, entry.bestLevels[FITNESS_DIMENSIONS[i]] ?? []);
    assert.ok(profile[i].contributions.every(c => c.name === "movement " + c.exerciseId));
  }
  assert.deepEqual(inspectFitnessProfile([...exercises].reverse(), entry.state, ratings), profile);
  assert.ok(inspectFitnessProfile(exercises, entry.state, new Map()).every(axis => !axis.mean));
  assert.deepEqual(inspectFitnessProfile(exercises, JSON.parse(JSON.stringify(entry.state)), ratings), profile);
  assert.equal(JSON.stringify(entry), before);
  if (entry.expected.includes(3.5)) assert.equal(roundFitnessLevel(3.5), 4);
});

for (const entry of read("./fixtures/fitness-precision-cases.json").cases)
test("fractional feedback: " + entry.name, () => {
  const exercises = entry.levelSets.map((_, index) => ({ id: index + 1, sessionMovementId: index + 1 }));
  const ratings = new Map(exercises.map(exercise => [exercise.id,
    FITNESS_DIMENSIONS.map(() => entry.levelSets[exercise.id - 1])]));
  const state = { activeWorkoutSession: { sessionId: 1, decisions: exercises.map((exercise, index) => ({
    rootExerciseId: exercise.id, outcome: entry.feedback[index], decidedAtUnixMilliseconds: index + 1,
  })) } };
  const before = JSON.stringify(state);
  const profile = inspectFitnessProfile(exercises, state, ratings);
  assert.deepEqual(profile.map(axis => axis.mean), FITNESS_DIMENSIONS.map(() => entry.mean));
  assert.deepEqual(fitnessZones(profile), FITNESS_DIMENSIONS.map(() => entry.bestLevels));
  assert.deepEqual(inspectFitnessProfile(exercises, JSON.parse(before), ratings), profile);
  assert.equal(JSON.stringify(state), before);
});

test("level labels retain noncontiguous gaps", () => {
  assert.equal(formatFitnessLevels([1, 3, 4, 5]), "1, 3–5");
  assert.equal(formatFitnessLevels([2, 3]), "2–3");
  assert.equal(formatFitnessLevels([5]), "5");
});

test("the spiderweb draws fractional feedback estimates without snapping them to whole levels", () => {
  const native = readFileSync(new URL("../../NomadicMethod/FitnessProfileView.cs", import.meta.url), "utf8");
  assert.match(native, /_levels = profile.Select\(axis => axis.Mean\).ToArray\(\)/);
  const profile = FITNESS_DIMENSIONS.map((_, index) => ({ mean: [0, 1.75, 2.025, 2.25, 5][index % 5] }));
  let renders = 0;
  const view = { host: { setAttribute() {} }, buttons: [{ disabled: true }], render() { renders++; } };
  FitnessProfileView.prototype.setProfile.call(view, profile);
  assert.deepEqual(view.values, profile.map(axis => axis.mean));
  assert.equal(renders, 1);
  assert.equal(view.buttons[0].disabled, false);
});

test("difficulty evidence drives selection eligibility while legacy favourites remain historical", () => {
  const catalog = read("../../NomadicMethod/Assets/exercises.json");
  const ratings = parseFitnessRatings(read("../../NomadicMethod/Assets/fitness_ratings.json"));
  const exercise = catalog.find(e => ratings.get(e.id).some(set => set.length === 1 && set[0] === 4) &&
    Math.max(...ratings.get(e.id).flat()) === 4);
  assert.ok(exercise);
  const first = createDefaultState();
  first.lastKeptExerciseIds = [exercise.id];
  const second = structuredClone(first);
  second.workoutHistory = [{ sessionId: 1, status: "Interrupted", blocks: [], decisions: [{
    rootExerciseId: exercise.id, outcome: "hard", decidedAtUnixMilliseconds: 1,
  }] }];
  assert.notDeepEqual(calculateFitnessProfile(catalog, first, ratings), calculateFitnessProfile(catalog, second, ratings));
  assert.deepEqual(calculateFavouriteFitnessProfile(catalog, first, ratings), calculateFavouriteFitnessProfile(catalog, second, ratings));
  const sessions = [first, second].map(state => new WorkoutSession(catalog, state, () => 0.42, () => 100000, ratings));
  assert.notDeepEqual(sessions[0].getFitnessEligibility(), sessions[1].getFitnessEligibility());
});
