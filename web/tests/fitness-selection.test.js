import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
import test from "node:test";
import { roundFitnessLevel, fitnessSelectionWeight, orderFitnessCandidates,
  calculateFitnessProfile, parseFitnessRatings } from "../fitness-profile.js";
import { WorkoutSession, createDefaultState, getResolution,
  WORKOUT_MODIFIERS as M } from "../workout.js";

const read = file => readFileSync(new URL(file, import.meta.url), "utf8");
const fixture = JSON.parse(read("./fixtures/fitness-selection-cases.json"));
const catalog = JSON.parse(read("../../NomadicMethod/Assets/exercises.json"));
const ratings = parseFitnessRatings(JSON.parse(read("../../NomadicMethod/Assets/fitness_ratings.json")));
const now = () => Date.parse("2026-10-22T08:00:00Z");
const modifiers = M.Mirror | M.TallMirror | M.HardFloor | M.Wall;
const planningCases = JSON.parse(read("./fixtures/workout-planning-cases.json"));

for (const entry of fixture.cases) {
  test("fitness selection: " + entry.name, () => {
    const before = JSON.stringify(entry);
    assert.equal(fitnessSelectionWeight(entry.levels, entry.profile.map(roundFitnessLevel)), entry.weight);
    assert.equal(JSON.stringify(entry), before);
  });
}
for (const entry of fixture.ordering) {
  test("shared weighted order: " + entry.name, () => {
    const before = JSON.stringify(entry.candidates);
    let draws = 0;
    const ordered = orderFitnessCandidates(entry.candidates, e => e.movementId,
      e => e.weight, () => entry.draws[draws++]);
    assert.deepEqual(ordered.map(e => e.id), entry.expected);
    assert.equal(draws, entry.draws.length, "keys are drawn once, including within families");
    assert.equal(JSON.stringify(entry.candidates), before);
  });
}

function seededRandom(seed) {
  return () => ((seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0) / 4294967296);
}

test("sampling favors nearby levels and variants do not multiply family chances", () => {
  const random = seededRandom(123);
  const candidates = [
    {id:1, movementId:1, weight:1}, {id:2, movementId:1, weight:0.5},
    {id:3, movementId:1, weight:0.25}, {id:4, movementId:2, weight:0.5},
  ];
  let familyOne = 0;
  for (let i = 0; i < 10000; i++)
    if (orderFitnessCandidates(candidates, e => e.movementId, e => e.weight, random)[0].movementId === 1)
      familyOne++;
  assert.ok(familyOne > 6400 && familyOne < 6900, String(familyOne));
  const neighbors = [1,2,3,5].map((level, i) => ({id:i, level,
    weight:fitnessSelectionWeight([[level]], [2])}));
  const counts = [0,0,0,0];
  for (let i = 0; i < 10000; i++)
    counts[orderFitnessCandidates(neighbors, e => e.id, e => e.weight, random)[0].id]++;
  assert.ok(counts[1] > counts[0] && counts[1] > counts[2]);
  assert.ok(Math.abs(counts[0] - counts[2]) < 200);
  assert.ok(counts[3] > 100, "distant harder exercises remain possible");
});

test("worker receives the same read-only rating dependency as foreground planning", () => {
  let receive, args, sent;
  class Session {
    constructor(...values) { args = values; this.state = values[1]; }
    prepareWorkout() {}
  }
  vm.runInNewContext(read("../workout-preparation-worker.js").replace(/^import .*;\r?\n/, ""), {
    WorkoutSession: Session,
    self: {addEventListener: (_, callback) => { receive = callback; },
      postMessage: value => { sent = value; }},
  });
  receive({data:{generation:1, exercises:catalog, state:createDefaultState(),
    fitnessRatings:ratings, minutes:3, modifiers}});
  assert.equal(args[4], ratings);
  assert.equal(sent.generation, 1);
  assert.ok(!sent.error);
  const app = read("../app.js");
  assert.match(app, /exercises: exerciseCatalog,\s+fitnessRatings,/);
});

for (const entry of planningCases) {
  test("existing feedback outranks fitness: " + entry.name, () => {
    const state = {...createDefaultState(),
      scores:Object.fromEntries(catalog.map(e => [e.id, entry.legacyScore])),
      exerciseScoreAdjustmentsByPhase:structuredClone(entry.adjustments)};
    const session = new WorkoutSession(catalog, state, seededRandom(765), now, ratings);
    session.initialize();
    const anchor = catalog.find(e => e.id === 218);
    const slot = getResolution(30).groups.find(g => g.canonicalGroups.includes(anchor.primaryCanonicalGroup));
    session.state.keptExerciseRootIdsBySelectionGroupId[slot.id] = [anchor.id];
    const feedback = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
    session.startWorkout(entry.minutes, entry.modifiers);
    const selections = session.state.activeWorkoutSession.initialSelections;
    assert.equal(selections.filter(s => s.selectionScoreAtStart < 0)
      .reduce((sum, s) => sum + s.sequenceBlockCount * s.setCount, 0), entry.expectedRejectedBlocks);
    assert.ok(selections.every(s => !entry.excludedRoots.includes(s.rootExerciseId)));
    assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, feedback);
  });
}

for (const minutes of [3,5,7,10,15,45]) {
  for (const light of [false, true]) {
    test("weighted planning and restoration preserve " + minutes + " minutes, Light " + light, () => {
      const state = createDefaultState();
      const session = new WorkoutSession(catalog, state, seededRandom(minutes), now, ratings);
      session.initialize();
      const anchor = catalog.find(e => e.id === 218);
      const slot = getResolution(30).groups.find(g => g.canonicalGroups.includes(anchor.primaryCanonicalGroup));
      session.state.keptExerciseRootIdsBySelectionGroupId[slot.id] = [anchor.id];
      const kept = structuredClone(session.state.keptExerciseRootIdsBySelectionGroupId);
      assert.equal(calculateFitnessProfile(catalog, session.state, ratings)[1], 2);
      const flags = modifiers | (light ? M.Light : 0);
      session.startWorkout(minutes, flags);
      assert.equal(session.getActiveGroups().length, minutes);
      assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId, kept);
      const first = session.getNextGroup();
      if (session.canShuffleNextExercise(first)) {
        assert.ok(session.shuffleNextExercise(first));
        assert.equal(session.getActiveGroups().length, minutes);
      }
      const current = session.getNextGroup();
      session.pauseMovement(current, 27000, true);
      const selected = structuredClone(session.state.selectedExerciseIds);
      const feedback = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
      const log = structuredClone(session.state.activeWorkoutSession);
      const restored = new WorkoutSession(catalog, structuredClone(session.state),
        seededRandom(999), now, ratings);
      restored.restoreAfterReopen();
      assert.deepEqual(restored.state.selectedExerciseIds, selected);
      assert.deepEqual(restored.state.exerciseScoreAdjustmentsByPhase, feedback);
      assert.deepEqual(restored.state.activeWorkoutSession, log);
      assert.equal(restored.getPendingMovementMillisecondsRemaining(now()), 27000);
      restored.reconfigureActiveWorkout(flags ^ M.Mirror ^ M.TallMirror, current.id);
      const selectedAfterModifierChange = restored.getSelectedExercise(restored.getNextGroup()).id;
      const duration = minutes === 3 ? 5 : minutes === 45 ? 30 : minutes;
      restored.resizeActiveWorkout(duration);
      assert.equal(restored.getActiveGroups().length, duration);
      assert.equal(restored.getSelectedExercise(restored.getNextGroup()).id,
        selectedAfterModifierChange);
    });
  }
}
