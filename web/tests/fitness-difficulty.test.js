import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { calculateFitnessProfile, inspectFitnessProfile, calculateFavouriteFitnessProfile,
  FITNESS_DIMENSIONS, parseFitnessRatings, roundFitnessLevel } from "../fitness-profile.js";
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
      c.ratedLevel, c.feedback, c.level]), entry.evidence[FITNESS_DIMENSIONS[i]] ?? []);
    assert.ok(profile[i].contributions.every(c => c.name === "movement " + c.exerciseId));
  }
  assert.deepEqual(inspectFitnessProfile([...exercises].reverse(), entry.state, ratings), profile);
  assert.ok(inspectFitnessProfile(exercises, entry.state, new Map()).every(axis => !axis.mean));
  assert.deepEqual(inspectFitnessProfile(exercises, JSON.parse(JSON.stringify(entry.state)), ratings), profile);
  assert.equal(JSON.stringify(entry), before);
  if (entry.expected.includes(3.5)) assert.equal(roundFitnessLevel(3.5), 4);
});

test("difficulty evidence changes the display without changing planner tiers or fresh selections", () => {
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
  assert.deepEqual(sessions[0].getFitnessTiers(), sessions[1].getFitnessTiers());
  sessions.forEach(session => session.startWorkout(3));
  assert.deepEqual(sessions[0].state.selectedExerciseIds, sessions[1].state.selectedExerciseIds);
});
