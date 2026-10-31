import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
import test from "node:test";
import { FITNESS_DIMENSIONS, roundFitnessLevel, isFitnessEligible, isFitnessSequenceEligible, fitnessZones, inspectFitnessProfile, orderFitnessCandidates,
  calculateFavouriteFitnessProfile, parseFitnessRatings, serializeFitnessRatings } from "../fitness-profile.js";
import { WorkoutSession, createDefaultState, getResolution,
  WORKOUT_MODIFIERS as M } from "../workout.js";

const read = file => readFileSync(new URL(file, import.meta.url), "utf8");
const fixture = JSON.parse(read("./fixtures/fitness-selection-cases.json"));
const catalog = JSON.parse(read("../../NomadicMethod/Assets/exercises.json"));
const ratings = parseFitnessRatings(JSON.parse(read("../../NomadicMethod/Assets/fitness_ratings.json")));
const now = () => Date.parse("2026-10-22T08:00:00Z");
const modifiers = M.Mirror | M.TallMirror | M.HardFloor | M.Wall;
const planningCases = JSON.parse(read("./fixtures/workout-planning-cases.json"));
const preferences = JSON.parse(read("./fixtures/fitness-selection-preferences.json"));

for (const entry of preferences.maskedMatches) {
  test("shared training-claim selection: " + entry.name, () => {
    const before=JSON.stringify(entry);
    assert.equal(isFitnessEligible(entry.levels,entry.zones,entry.trained),entry.eligible);
    assert.equal(JSON.stringify(entry),before);
  });
}
test("every atomic member must match with its own training claims", () => {
  const hard=FITNESS_DIMENSIONS.map((_,i)=>[i===1?3:1]);
  const easy=FITNESS_DIMENSIONS.map(()=>[1]);
  const zone=FITNESS_DIMENSIONS.map(()=>[3]);
  const claims=FITNESS_DIMENSIONS.map((_,i)=>i===1);
  assert.equal(isFitnessSequenceEligible([hard,easy],zone,[claims,FITNESS_DIMENSIONS.map(()=>false)]),false);
  assert.equal(isFitnessSequenceEligible([hard],zone,[null]),false);
});

for (const entry of fixture.cases) {
  test("fitness selection: " + entry.name, () => {
    const before = JSON.stringify(entry);
    assert.equal(isFitnessEligible(entry.levels, entry.zones), entry.eligible);
    assert.equal(JSON.stringify(entry), before);
  });
}
for (const entry of fixture.catalogCases) {
  test(`canonical exercise ${entry.id} overlaps axis ${entry.dimension} zone ${entry.zone}`, () => {
    const zones = FITNESS_DIMENSIONS.map((_, index) => index === entry.dimension ? entry.zone : []);
    assert.equal(isFitnessEligible(ratings.get(entry.id), zones), entry.eligible);
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

test("every sequence member must qualify, with bootstrap only for wholly unknown profiles", () => {
  assert.equal(isFitnessSequenceEligible([null, [[3]]], [[3]]), false);
  assert.equal(isFitnessSequenceEligible([[[3]], [[3,4]]], [[3,4]]), true);
  assert.equal(isFitnessSequenceEligible([[[3]], [[2,3]]], [[3,4]]), true);
  assert.equal(isFitnessSequenceEligible([[[3]], [[1,2]]], [[3,4]]), false);
  assert.equal(isFitnessSequenceEligible([null, [[]]], [[]]), true);
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

function discoveryFixture(higherHard = false, lightStress = false, shareDeclinedCoverage = false) {
  const groups = getResolution(3).groups;
  const exercises = fixture.discovery.exercises.map(row => ({
    id: row.id, name: `Synthetic planner fixture ${row.id}`, video: "", practice: "", motionProfile: "",
    primaryCanonicalGroup: groups[row.group].canonicalGroups[0],
    secondaryCanonicalGroups: groups[row.group].canonicalGroups.slice(1)
      .concat(shareDeclinedCoverage && row.id===30 ? groups[1].canonicalGroups : []),
    mode: "Repetition", presentation: "Motion", holdFramePercent: 0, sideSequence: "Continuous",
    sequenceBlocks: [{exerciseId: row.id, sideCue: "None", directionCue: "None", mediaSegment: "Full", mirrorMedia: false}],
    sessionMovementId: row.movementId ?? row.id, score: row.score ?? 0,
    muscularDemand: lightStress && row.id<20 && row.id!==7 || higherHard && [3,4,5].includes(row.id) ? 2 : 0,
    onlyFeetTouchGround: true, shoeAgnostic: true, maxSpaceMeters: 2, equipment: "None", silent: true,
    mirrorRelationship: "Agnostic", minimumMirrorCoverage: "None", hardFloorCompatibility: "Compatible",
    shyCompatibility: "Compatible", upperBodyClothingRequirement: "Agnostic", floorContactPoints: "Feet",
    locomotionSpace: "None", insectCompatibility: "Compatible",
  }));
  const ratings = new Map(fixture.discovery.exercises.map(row => [row.id, row.levels]));
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[groups[0].id] = [...fixture.discovery.kept];
  state.workoutHistory = [{sessionId:1, status:"Interrupted", blocks:[], decisions:[{
    rootExerciseId:1, outcome:"tick", decidedAtUnixMilliseconds:1,
  }]}];
  const session = new WorkoutSession(exercises, state, () => 0, now, ratings);
  return { exercises, ratings, session };
}

for (const outcome of preferences.difficultyResponses) {
  test("movement difficulty outranks the aggregate profile across aliases: " + outcome, () => {
    const {session,ratings,exercises}=discoveryFixture();
    session.state.keptExerciseRootIdsBySelectionGroupId={};
    for(const id of [3,6])session.state.scores[id]=10000;
    session.state.workoutHistory[0].decisions.push({rootExerciseId:6,outcome,decidedAtUnixMilliseconds:2});
    const zones=fitnessZones(inspectFitnessProfile(exercises,session.state,ratings));
    assert.equal(isFitnessEligible(ratings.get(6),zones),true);
    const history=structuredClone(session.state.workoutHistory);
    session.startWorkout(3,0);
    assert.notEqual(session.getSelectedExercise(session.getNextGroup()).sessionMovementId,3);
    assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase,{});
    assert.deepEqual(session.state.workoutHistory,history);
    assert.equal(session.getScore(session.exercisesById.get(6)),10000);
  });
}

test("loaded training claims reach foreground and worker-transported planning", () => {
  const {exercises, ratings, session} = discoveryFixture();
  ratings.get(1)[8] = [1];
  ratings.set(6, FITNESS_DIMENSIONS.map((_, dimension) => dimension === 1 ? [4] : []));
  const source = {schemaVersion:2, dimensions:[...FITNESS_DIMENSIONS],
    ratings:Object.fromEntries([...ratings].map(([id, axes]) => [id, axes.map(set => set.length ? set : [1])])),
    trainingCharacteristics:Object.fromEntries([...ratings].map(([id, axes]) => [id, axes.map(set => set.length > 0)]))};
  const loaded = parseFitnessRatings(source);
  session.state.keptExerciseRootIdsBySelectionGroupId = {};
  session.state.scores[6] = 10000;
  for (const dependency of [loaded, parseFitnessRatings(structuredClone(serializeFitnessRatings(loaded)))]) {
    const planner = new WorkoutSession(exercises, structuredClone(session.state), () => 0, now, dependency);
    assert.equal(planner.getFitnessEligibility().get(6), false);
    planner.startWorkout(3, 0);
    assert.ok(planner.state.activeWorkoutSession.initialSelections.every(selection => selection.rootExerciseId !== 6));
    assert.equal(planner.getActiveGroups().length, 3);
  }
});

test("phase refusals apply to aliases before a fitness profile exists", () => {
  const {session}=discoveryFixture();
  session.state.workoutHistory=[];
  session.state.keptExerciseRootIdsBySelectionGroupId={};
  session.state.scores[6]=10000;
  session.state.exerciseScoreAdjustmentsByPhase.Warmup={3:-1};
  session.startWorkout(3,0);
  assert.notEqual(session.getSelectedExercise(session.getNextGroup()).sessionMovementId,3);
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase.Warmup,{3:-1});
});

test("Light still dominates difficulty preference", () => {
  const {session}=discoveryFixture(false,true);
  session.state.workoutHistory[0].decisions.push({rootExerciseId:7,outcome:"easy",decidedAtUnixMilliseconds:2});
  session.startWorkout(3,M.Light);
  assert.equal(session.getSelectedExercise(session.getNextGroup()).id,7);
});

test("extra sets prefer other eligible work over a mandatory difficulty mismatch", () => {
  const {session}=discoveryFixture();
  session.state.workoutHistory[0].decisions.push({rootExerciseId:30,outcome:"hard",decidedAtUnixMilliseconds:2});
  session.startWorkout(10,0);
  const selected=session.state.activeWorkoutSession.initialSelections.filter(s=>s.rootExerciseId===30);
  assert.equal(session.getActiveGroups().length,10);
  assert.ok(selected.length>0);
  assert.ok(selected.every(s=>s.setCount===1));
});

test("shared coverage cannot undo a difficulty preference", () => {
  const {session}=discoveryFixture(false,false,true);
  session.state.keptExerciseRootIdsBySelectionGroupId={};
  session.state.scores[30]=10000;
  session.state.workoutHistory[0].decisions.push({rootExerciseId:30,outcome:"hard",decidedAtUnixMilliseconds:2});
  session.startWorkout(3,0);
  for(const id of [20,30]) assert.ok(session.state.activeWorkoutSession.initialSelections.some(s=>s.rootExerciseId===id));
});

for (const outcome of preferences.difficultyResponses) {
  test("latest difficulty broadens a trapped slot without inventing scores: " + outcome, () => {
    const state = {...createDefaultState(), workoutHistory: [{sessionId:1, status:"Interrupted", blocks:[],
      decisions:[{rootExerciseId:918,outcome,decidedAtUnixMilliseconds:now()-3600000}]}]};
    const session = new WorkoutSession(catalog, state, seededRandom(5), now);
    session.initialize();
    const history = structuredClone(session.state.workoutHistory);
    const scores = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
    session.startWorkout(45, 5232);
    assert.ok(session.state.activeWorkoutSession.initialSelections.every(selection => selection.rootExerciseId !== 918));
    assert.ok(session.state.activeDurationSelectionGroupIds.includes("r15.abdominal-pelvic-floor"));
    assert.equal(session.getActiveGroups().length, 45);
    assert.deepEqual(session.state.workoutHistory, history);
    assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, scores);
    const activeSelections = () => session.getActiveGroups().map(group => [group.id, session.getSelectedExercise(group).id]);
    const selected = activeSelections();
    const log = structuredClone(session.state.activeWorkoutSession);
    session.restoreAfterReopen();
    assert.deepEqual(activeSelections(), selected);
    assert.deepEqual(session.state.activeWorkoutSession, log);
  });
}

test("eligible loves first, then eligible new movements only, including after cold reopen", () => {
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
  assert.ok(session.state.nextWorkoutExcludedExerciseIds.includes(1));
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase.Warmup[3], undefined, "no invented alias vote");
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
  test(`zone selection respects Light ${light} and recovery ${recovering}`, () => {
    const {session, ratings} = discoveryFixture(true);
    for (const id of [3,4,5]) ratings.set(id, structuredClone(ratings.get(1)));
    session.state.scores[3] = 1000;
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
  vm.runInNewContext(read("../workout-preparation-worker.js").replace(/^import .*;\r?\n/gm, ""), {
    WorkoutSession: Session, parseFitnessRatings,
    self: {addEventListener: (_, callback) => { receive = callback; },
      postMessage: value => { sent = value; }},
  });
  receive({data:{generation:1, exercises:catalog, state:createDefaultState(),
    fitnessRatings:structuredClone(serializeFitnessRatings(ratings)), minutes:3, modifiers}});
  assert.deepEqual([...args[4]], [...ratings]);
  assert.deepEqual([...args[4].trainingCharacteristics], [...ratings.trainingCharacteristics]);
  assert.equal(sent.generation, 1);
  assert.ok(!sent.error);
  const app = read("../app.js");
  assert.match(app, /exercises: exerciseCatalog,\s+fitnessRatings: serializeFitnessRatings\(fitnessRatings\),/);
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
    test("bootstrap planning and restoration preserve " + minutes + " minutes, Light " + light, () => {
      const state = createDefaultState();
      const session = new WorkoutSession(catalog, state, seededRandom(minutes), now, ratings);
      session.initialize();
      const anchor = catalog.find(e => e.id === 218);
      const slot = getResolution(30).groups.find(g => g.canonicalGroups.includes(anchor.primaryCanonicalGroup));
      session.state.keptExerciseRootIdsBySelectionGroupId[slot.id] = [anchor.id];
      const kept = structuredClone(session.state.keptExerciseRootIdsBySelectionGroupId);
      assert.equal(calculateFavouriteFitnessProfile(catalog, session.state, ratings)[1], 2);
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

test("a coverage hole fails explicitly instead of admitting an out-of-zone or unrated movement", () => {
  const {session, ratings} = discoveryFixture();
  ratings.set(30, Array.from({length:10}, () => []));
  assert.throws(() => session.startWorkout(3, 0), /No complete workout fits your current training zone/);
});

test("overlapping ranges apply to duration and modifier edits while current work stays fixed", () => {
  const {session, ratings} = discoveryFixture();
  ratings.get(20)[1] = [1,2,3,4,5];
  ratings.get(30)[1] = [2,3];
  session.startWorkout(3, 0);
  const current = session.getNextGroup(), id = session.getSelectedExercise(current).id;
  session.pauseMovement(current, 27000, true);
  session.resizeActiveWorkout(5);
  session.reconfigureActiveWorkout(M.Wall, session.getNextGroup().id);
  assert.equal(session.getSelectedExercise(session.getNextGroup()).id, id);
  const eligible = session.getFitnessEligibility();
  assert.ok(session.getActiveGroups().every(group => eligible.get(session.getSelectedExercise(group).id)));
  assert.equal(session.getActiveGroups().length, 5);
});

test("easy or hard removes favourite priority across aliases without dislike scores", () => {
  for (const outcome of ["easy", "hard"]) {
    const {session, ratings} = discoveryFixture();
    // Another axis still qualifies; removal cannot be credited to the filter alone.
    session.state.workoutHistory[0].decisions.push({rootExerciseId:6, outcome,
      decidedAtUnixMilliseconds:2});
    session.state.keptExerciseRootIdsBySelectionGroupId[getResolution(3).groups[0].id].push(3,6);
    const before = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
    session.removeDifficultyKeeps();
    assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId[getResolution(3).groups[0].id], [1,2]);
    assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, before);
    session.state.workoutHistory[0].decisions.push({rootExerciseId:3, outcome:"tick", decidedAtUnixMilliseconds:3});
    session.state.keptExerciseRootIdsBySelectionGroupId[getResolution(3).groups[0].id].push(3);
    session.removeDifficultyKeeps();
    assert.ok(session.state.keptExerciseRootIdsBySelectionGroupId[getResolution(3).groups[0].id].includes(3));
  }
});

test("new difficulty evidence does not regenerate an unfinished lineup or leak through shuffle", () => {
  const {session, exercises, ratings} = discoveryFixture();
  session.startWorkout(3, 0);
  const selected = structuredClone(session.state.selectedExerciseIds);
  // Simulate a newer response changing the zone while preserving the unfinished plan.
  session.state.activeWorkoutSession.decisions.push({rootExerciseId:1, outcome:"easy", decidedAtUnixMilliseconds:2});
  session.state.activeWorkoutSession.decisions.push({rootExerciseId:2, outcome:"easy", decidedAtUnixMilliseconds:3});
  const restored = new WorkoutSession(exercises, structuredClone(session.state), () => 0, now, ratings);
  restored.restoreAfterReopen();
  assert.deepEqual(restored.state.selectedExerciseIds, selected);
  const eligible = restored.getFitnessEligibility();
  const next = restored.getNextGroup();
  if (restored.canShuffleNextExercise(next)) {
    restored.shuffleNextExercise(next);
    assert.equal(eligible.get(restored.getSelectedExercise(next).id), true);
  }
});
