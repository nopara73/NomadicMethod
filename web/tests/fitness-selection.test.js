import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
import test from "node:test";
import { roundFitnessLevel, fitnessSelectionTier, fitnessSequenceTier, orderFitnessCandidates,
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
    assert.equal(fitnessSelectionTier(entry.levels, entry.profile.map(roundFitnessLevel)), entry.tier);
    assert.equal(JSON.stringify(entry), before);
  });
}
for (const entry of fixture.ordering) {
  test("shared tier order: " + entry.name, () => {
    const before = JSON.stringify(entry.candidates);
    let draws = 0;
    const ordered = orderFitnessCandidates(entry.candidates, e => e.movementId,
      e => e.tier, () => entry.draws[draws++]);
    assert.deepEqual(ordered.map(e => e.id), entry.expected);
    assert.equal(draws, entry.draws.length, "keys are drawn once, including within families");
    assert.equal(JSON.stringify(entry.candidates), before);
  });
}

function seededRandom(seed) {
  return () => ((seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0) / 4294967296);
}

test("unrated sequence members do not mask known training levels", () => {
  assert.equal(fitnessSequenceTier([null, [[4]]], [2]), 7);
  assert.equal(fitnessSequenceTier([[[2]], [[3]]], [2]), 0);
  assert.equal(fitnessSequenceTier([null, [[]]], [2]), 6);
});

test("equal-tier families have equal chances regardless of aliases", () => {
  const random = seededRandom(123);
  const candidates = [
    {id:1, movementId:1, tier:0}, {id:2, movementId:1, tier:0},
    {id:3, movementId:1, tier:0}, {id:4, movementId:2, tier:0},
  ];
  let familyOne = 0;
  for (let i = 0; i < 10000; i++)
    if (orderFitnessCandidates(candidates, e => e.movementId, e => e.tier, random)[0].movementId === 1)
      familyOne++;
  assert.ok(familyOne > 4750 && familyOne < 5250, String(familyOne));
});

function discoveryFixture(higherHard = false) {
  const groups = getResolution(3).groups;
  const exercises = fixture.discovery.exercises.map(row => ({
    id: row.id, name: `Synthetic planner fixture ${row.id}`, video: "", practice: "", motionProfile: "",
    primaryCanonicalGroup: groups[row.group].canonicalGroups[0],
    secondaryCanonicalGroups: groups[row.group].canonicalGroups.slice(1),
    mode: "Repetition", presentation: "Motion", holdFramePercent: 0, sideSequence: "Continuous",
    sequenceBlocks: [{exerciseId: row.id, sideCue: "None", directionCue: "None", mediaSegment: "Full", mirrorMedia: false}],
    sessionMovementId: row.movementId ?? row.id, score: row.score ?? 0,
    muscularDemand: higherHard && [3,4,5].includes(row.id) ? 2 : 0,
    onlyFeetTouchGround: true, shoeAgnostic: true, maxSpaceMeters: 2, equipment: "None", silent: true,
    mirrorRelationship: "Agnostic", minimumMirrorCoverage: "None", hardFloorCompatibility: "Compatible",
    shyCompatibility: "Compatible", upperBodyClothingRequirement: "Agnostic", floorContactPoints: "Feet",
    locomotionSpace: "None", insectCompatibility: "Compatible",
  }));
  const ratings = new Map(fixture.discovery.exercises.map(row => [row.id, row.levels]));
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[groups[0].id] = [...fixture.discovery.kept];
  const session = new WorkoutSession(exercises, state, () => 0, now, ratings);
  return { exercises, ratings, session };
}

test("hearts, every +1 ability, current, lower, then fallbacks without family repeats after cold reopen", () => {
  let {exercises, ratings, session} = discoveryFixture();
  session.startWorkout(3, 0);
  const seen = [];
  do {
    const group = session.getNextGroup();
    seen.push(session.getSelectedExercise(group).sessionMovementId);
    assert.deepEqual(session.state.outcomes, {});
    assert.ok(seen.length <= 12);
    if (!session.canShuffleNextExercise(group)) break;
    assert.ok(session.shuffleNextExercise(group));
    const selected = structuredClone(session.state.selectedExerciseIds);
    session = new WorkoutSession(exercises, structuredClone(session.state), () => 0, now, ratings);
    session.restoreAfterReopen();
    assert.deepEqual(session.state.selectedExerciseIds, selected);
  } while (true);
  assert.deepEqual(seen, fixture.discovery.expectedFamilyOrder);
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(6));
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase.Warmup[6], undefined, "no invented alias vote");
  assert.equal(session.getActiveGroups().length, 3);
});

test("historical refusals apply to aliases without rewriting scores or hearts", () => {
  const {session, ratings} = discoveryFixture();
  const heartSlot = getResolution(3).groups[1].id;
  session.state.keptExerciseRootIdsBySelectionGroupId = {[heartSlot]: [20]};
  ratings.set(20, ratings.get(1));
  session.state.scores[6] = 1000;
  session.state.exerciseScoreAdjustmentsByPhase.Warmup = {3:-1,4:-1,5:-1};
  session.startWorkout(3, 0);
  assert.equal(session.getSelectedExercise(session.getNextGroup()).id, 7);
  assert.equal(session.getScore(session.exercisesById.get(7)), 100);
  assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId[heartSlot], [20]);
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase.Warmup, {3:-1,4:-1,5:-1});
});

for (const [light, recovering, expected] of [[false,false,3], [true,false,7], [false,true,7]]) {
  test(`upward exploration respects Light ${light} and recovery ${recovering}`, () => {
    const {session, ratings} = discoveryFixture(true);
    const groups = getResolution(3).groups;
    session.state.keptExerciseRootIdsBySelectionGroupId = {[groups[1].id]: [20]};
    ratings.set(20, ratings.get(1));
    if (recovering) session.state.lastHardWorkUnixMillisecondsByPrimaryMuscle[groups[0].canonicalGroups[0]] =
      now() - 3600000;
    session.startWorkout(3, light ? M.Light : 0);
    const target = session.getActiveGroups().find(g => (g.selectionGroupId ?? g.id) === groups[0].id);
    assert.equal(session.getSelectedExercise(target).id, expected);
    assert.deepEqual(session.state.outcomes, {});
  });
}

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
    test("tiered planning and restoration preserve " + minutes + " minutes, Light " + light, () => {
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
