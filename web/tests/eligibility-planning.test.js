import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { WorkoutSession, createDefaultState, getSessionMovementId, getResolution } from "../workout.js";
import { FITNESS_DIMENSIONS, fitnessZones, inspectFitnessProfile, parseFitnessRatings, isFitnessSequenceEligible } from "../fitness-profile.js";

const read = path => JSON.parse(readFileSync(new URL(path, import.meta.url), "utf8"));
const catalog = read("../../NomadicMethod/Assets/exercises.json");
const ratings = parseFitnessRatings(read("../../NomadicMethod/Assets/fitness_ratings.json"));
const fixture = read("./fixtures/eligibility-planning-cases.json");
const now = () => Date.parse("2026-10-31T08:00:00Z");
const evidence = decisions => ({ ...createDefaultState(), workoutHistory: [{ sessionId: 1,
  status: "Interrupted", startedAtUnixMilliseconds: now() - 172800000,
  endedAtUnixMilliseconds: now() - 172799999, workoutMinutes: 3, startedBeforeLogging: true,
  blocks: [], decisions }] });

function levelFiveEvidence() {
  const decisions = [], used = new Set();
  for (let axis = 0; axis < 10; axis++) {
    const seed = catalog.find(exercise => !used.has(getSessionMovementId(exercise)) &&
      ratings.trainingCharacteristics.get(exercise.id)[axis] &&
      (ratings.get(exercise.id)[axis].at(-1) === 4 ||
        JSON.stringify(ratings.get(exercise.id)[axis]) === "[5]"));
    assert.ok(seed, `real catalog evidence for axis ${axis}`);
    used.add(getSessionMovementId(seed));
    decisions.push({ rootExerciseId: seed.id,
      outcome: ratings.get(seed.id)[axis].at(-1) === 4 ? "easy" : "tick", decidedAtUnixMilliseconds: axis + 1 });
  }
  const state = evidence(decisions);
  assert.deepEqual(fitnessZones(inspectFitnessProfile(catalog, state, ratings)), FITNESS_DIMENSIONS.map(() => [5]));
  return state;
}

function assertComplete(session, minutes) {
  const groups = session.getActiveGroups();
  assert.equal(groups.length, minutes);
  const zones = fitnessZones(inspectFitnessProfile(catalog, session.state, ratings));
  for (const group of groups) {
    const root = session.getSequenceRoot(session.getSelectedExercise(group));
    const members = session.getSequenceExercises(root);
    assert.ok(isFitnessSequenceEligible(members.map(member => ratings.get(member.id)), zones,
      members.map(member => ratings.trainingCharacteristics.get(member.id))));
  }
}

for (const entry of fixture.partialFeedback) {
  test(`partial feedback prepares ${entry.minutes} minutes under modifiers ${entry.modifiers}`, () => {
    const state = evidence([{ rootExerciseId: entry.exerciseId, outcome: "tick", decidedAtUnixMilliseconds: 1 }]);
    const catalogBefore = JSON.stringify(catalog);
    const ratingsBefore = JSON.stringify([...ratings]);
    const session = new WorkoutSession(catalog, state, () => 0.42, now, ratings);
    const history = structuredClone(session.state.workoutHistory);
    session.startWorkout(entry.minutes, entry.modifiers);
    assertComplete(session, entry.minutes);
    assert.deepEqual(session.state.workoutHistory, history);
    assert.equal(JSON.stringify(catalog), catalogBefore);
    assert.equal(JSON.stringify([...ratings]), ratingsBefore);
  });
}

for (const entry of fixture.emptyKnownProfiles) {
  test(`empty assessed slots resolve and reopen unchanged: ${entry.minutes}/${entry.modifiers}`, () => {
    const session = new WorkoutSession(catalog, levelFiveEvidence(), () => 0.42, now, ratings);
    session.startWorkout(entry.minutes, entry.modifiers);
    assertComplete(session, entry.minutes);
    assert.ok(session.state.activeDurationSelectionGroupIds);
    session.pauseMovement(session.getNextGroup(), 27000, true);
    const saved = structuredClone(session.state);
    const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0.19, now, ratings);
    restored.restoreAfterReopen();
    assert.deepEqual(restored.state.selectedExerciseIds, saved.selectedExerciseIds);
    assert.deepEqual(restored.state.activeDurationSelectionGroupIds, saved.activeDurationSelectionGroupIds);
    assert.deepEqual(restored.state.activeWorkoutSession, saved.activeWorkoutSession);
    assert.equal(restored.state.pendingMovementMillisecondsRemaining, 27000);
  });
}

test("saved Keeps and paused atomic work survive coverage resolution and explicit edits", () => {
  const probe = new WorkoutSession(catalog, levelFiveEvidence(), () => 0.42, now, ratings);
  probe.startWorkout(15, 0);
  const byId = new Map(catalog.map(exercise => [exercise.id, exercise]));
  const declined = new Set(probe.state.workoutHistory.flatMap(session => session.decisions)
    .filter(decision => ["easy", "hard"].includes(decision.outcome))
    .map(decision => getSessionMovementId(byId.get(decision.rootExerciseId))));
  const kept = probe.getActiveGroups().map(group => probe.getSequenceRoot(probe.getSelectedExercise(group)))
    .find(root => !declined.has(getSessionMovementId(root)) && getResolution(15).groups.some(group =>
      probe.isWorkoutSelectionCandidate(root, group, 0, getResolution(15).groups)));
  assert.ok(kept);
  const originalSlot = getResolution(15).groups.find(group => probe.isWorkoutSelectionCandidate(kept, group, 0, getResolution(15).groups));
  const state = levelFiveEvidence();
  state.keptExerciseRootIdsBySelectionGroupId[originalSlot.id] = [kept.id];
  const session = new WorkoutSession(catalog, state, () => 0.43, now, ratings);
  session.startWorkout(15, 0);
  assert.ok(session.state.keptExerciseRootIdsBySelectionGroupId[originalSlot.id].includes(kept.id));
  const current = session.getNextGroup();
  const rootId = session.getSequenceRoot(session.getSelectedExercise(current)).id;
  const counts = structuredClone(session.state.activeSetCountsBySelectionGroupId);
  session.pauseMovement(current, 27000, true);
  session.resizeActiveWorkout(10);
  assert.equal(session.getNextGroup().id, current.id);
  assert.equal(session.getSequenceRoot(session.getSelectedExercise(current)).id, rootId);
  assert.equal(session.state.pendingMovementMillisecondsRemaining, 27000);
  session.beginRest(session.getNextGroup(), now() + 5000);
  session.reconfigureActiveWorkout(4096, current.id);
  assert.equal(session.state.pendingRestGroupId, current.id);
  assert.equal(session.state.pendingRestEndsAtUnixMilliseconds, now() + 5000);
  assert.equal(session.getSequenceRoot(session.getSelectedExercise(session.getNextGroup())).id, rootId);
  for (const key of [current.selectionGroupId ?? current.id])
    assert.equal(session.state.activeSetCountsBySelectionGroupId[key] ?? 1, counts[key] ?? 1);
  assertComplete(session, 10);
  assert.ok(session.state.keptExerciseRootIdsBySelectionGroupId[originalSlot.id].includes(kept.id));
});

