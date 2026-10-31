import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import test from "node:test";
import {createHash} from "node:crypto";
import {parseFitnessRatings, inspectFitnessProfile, fitnessZones} from "../fitness-profile.js";
import {findWorkoutVarietyDeficiencies, REQUIRED_WORKOUT_MOVEMENT_CHOICES} from "../workout-variety.js";
import {WorkoutSession, createDefaultState, getResolution, WORKOUT_MODIFIERS} from "../workout.js";

const cases = JSON.parse(readFileSync(new URL("./fixtures/post-filter-variety-cases.json", import.meta.url), "utf8"));
// The integration method uses the production solver through the application
// preflight. Expose that same solver to this structural fixture without creating
// a second implementation of assignment or exact block allocation.
const sourceUrl = new URL("../workout.js", import.meta.url);
const source = readFileSync(sourceUrl, "utf8").replace(/(from\s*|import\s*)(["'])(\.\/[^"']+)\2/g,
  (_, prefix, quote, relative) => prefix + quote + new URL(relative, sourceUrl).href + quote) +
  "\nexport {solveAtomicSequenceLineup};\n";
const {solveAtomicSequenceLineup} = await import("data:text/javascript," + encodeURIComponent(source));

for (const entry of cases) test("selectable variety: " + entry.name, () => {
  assert.equal(REQUIRED_WORKOUT_MOVEMENT_CHOICES, 5);
  const candidates = entry.candidates.map(([exerciseId,movementId,mask,blockCount]) => ({
    exerciseId,movementId,coverageMask:BigInt(mask),blockCount,utilitiesByGroup:Array(entry.groups).fill(0n),tieOrder:exerciseId,
  }));
  const actual = findWorkoutVarietyDeficiencies(entry.groups, entry.minutes, candidates, solveAtomicSequenceLineup, entry.required,
    entry.validateMask === undefined ? undefined : BigInt(entry.validateMask));
  assert.deepEqual(actual.map(row => [row.groupIndex,row.distinctMovementCount,row.requiredCount]), entry.expected);
});

test("every foreground, background and Android app factory requires five choices", () => {
  for (const file of ["../app.js", "../workout-preparation-worker.js"]) {
    const text = readFileSync(new URL(file, import.meta.url), "utf8");
    assert.doesNotMatch(text, /new WorkoutSession\(/);
    assert.match(text, /WorkoutSession\.createForApplication\(/);
  }
  const native = readFileSync(new URL("../../NomadicMethod/MainActivity.cs", import.meta.url), "utf8");
  assert.doesNotMatch(native, /new ExerciseSessionService\(/);
  assert.match(native, /ExerciseSessionService\.CreateForApplication\(/);
});

function fixture() {
  const exercises = getResolution(3).groups.flatMap((group,index) => Array.from({length:5},(_,choice) => {
    const id = 90000 + index*10 + choice;
    return {id,name:`Synthetic variety fixture ${index}/${choice}`,score:0,muscularDemand:0,
      primaryCanonicalGroup:group.canonicalGroups[0],secondaryCanonicalGroups:group.canonicalGroups.slice(1),
      sequenceBlocks:[{exerciseId:id,sideCue:"None",directionCue:"None",mediaSegment:"Full",mirrorMedia:false}],
      mode:"Repetition",presentation:"Motion",sideSequence:"Continuous",directionSequence:"None",holdFramePercent:0,
      onlyFeetTouchGround:true,shoeAgnostic:true,maxSpaceMeters:2,equipment:"None",silent:true,floorContactPoints:"Feet",
      mirrorRelationship:"Agnostic",minimumMirrorCoverage:"None",hardFloorCompatibility:"Compatible",
      insectCompatibility:"Compatible",shyCompatibility:"Compatible",upperBodyClothingRequirement:"Agnostic",locomotionSpace:"None"};
  }));
  const ratings = new Map(exercises.map(exercise => [exercise.id,Array.from({length:10},(_,axis)=>axis===6?[2]:[])]));
  const state = createDefaultState();
  state.workoutHistory = [{sessionId:1,status:"Interrupted",blocks:[],decisions:[{rootExerciseId:90000,outcome:"tick",decidedAtUnixMilliseconds:1}]}];
  return {exercises,ratings,state};
}

for (const reason of ["zone","equipment"]) test(`application counts the final ${reason}-filtered pool and preserves failed preparation`, () => {
  const {exercises,ratings,state} = fixture();
  if (reason === "zone") ratings.get(90004)[6] = [1];
  else exercises.find(exercise => exercise.id === 90004).elevationRequired = true;
  const session = WorkoutSession.createForApplication(exercises,state,()=>0,()=>1000,ratings);
  const before = structuredClone(session.state);
  assert.throws(()=>session.prepareWorkout(3,0), /Some muscle groups have fewer than five/);
  assert.deepEqual(session.state,before);
});

test("application preserves an unfinished workout with a smaller frozen pool", () => {
  const {exercises,ratings,state} = fixture();
  const session = WorkoutSession.createForApplication(exercises,state,()=>0,()=>1000,ratings);
  session.startWorkout(3,0);
  const first = session.getNextGroup();
  session.pauseMovement(first,17000,true);
  const selected = structuredClone(session.state.selectedExerciseIds);
  const ids = new Set(session.getActiveGroups().map(group=>session.getSelectedExercise(group).id));
  const restored = WorkoutSession.createForApplication(exercises.filter(exercise=>ids.has(exercise.id)),structuredClone(session.state),()=>0.9,()=>2000,ratings);
  restored.restoreAfterReopen();
  assert.deepEqual(restored.state.selectedExerciseIds,selected);
  assert.equal(restored.getNextGroup().id,first.id);
  assert.equal(restored.state.pendingMovementMillisecondsRemaining,17000);
});

test("partial assessment completes without counting incidental ratings as training choices", () => {
  const {exercises,ratings:original,state} = fixture();
  const claims = Object.fromEntries([...original].map(([id,levels])=>[id,levels.map(set=>set.length>0)]));
  const values = Object.fromEntries([...original].map(([id,levels])=>[id,levels.map(set=>set.length?set:[1])]));
  values[90000][1]=[1]; claims[90000][1]=true;
  values[90004][6]=[1];
  const ratings=parseFitnessRatings({schemaVersion:2,dimensions:[...actualRatingSource.dimensions],ratings:values,trainingCharacteristics:claims});
  const session=WorkoutSession.createForApplication(exercises,state,()=>0,()=>1000,ratings);
  session.startWorkout(3,0);
  assert.equal(session.getActiveGroups().length,3);
  assert.ok(session.getActiveGroups().every(group=>session.getSelectedExercise(group).id!==90004));
  assert.throws(()=>session.ensureSelectableWorkoutVariety(3,0), /Some muscle groups have fewer than five/);
  for (const exercise of exercises.filter(exercise=>exercise.id<90010)) exercise.elevationRequired=true;
  const unavailable=WorkoutSession.createForApplication(exercises,fixture().state,()=>0,()=>1000,ratings);
  const before=structuredClone(unavailable.state);
  assert.throws(()=>unavailable.prepareWorkout(3,0));
  assert.deepEqual(unavailable.state,before);
});

for (const edit of ["settings","duration"]) test(`failed active ${edit} edit preserves the whole workout`, () => {
  const {exercises,ratings,state}=fixture();
  if(edit==="settings") exercises.find(e=>e.id===90004).elevationRequired=true;
  const session=WorkoutSession.createForApplication(exercises,state,()=>0,()=>1000,ratings);
  session.startWorkout(3,edit==="settings"?WORKOUT_MODIFIERS.Elevation:0);
  const current=session.getNextGroup();
  if(edit==="duration") ratings.get(90004)[6]=[1];
  if(edit==="duration") {
    const rootId=session.getSequenceRoot(session.getSelectedExercise(current)).id;
    session.resizeActiveWorkout(5);
    assert.equal(session.getActiveGroups().length,5);
    assert.equal(session.getNextGroup().id,current.id);
    assert.equal(session.getSequenceRoot(session.getSelectedExercise(current)).id,rootId);
    session.ensureSelectableWorkoutVariety(5,0,session.getSelectionGroups(),new Map([[current.selectionGroupId??current.id,rootId]]));
    return;
  }
  const before=structuredClone(session.state);
  assert.throws(()=>edit==="settings"?session.reconfigureActiveWorkout(0,current.id):session.resizeActiveWorkout(5), /Some muscle groups have fewer than five/);
  assert.deepEqual(session.state,before);
});

const catalogProfiles = JSON.parse(readFileSync(new URL("./fixtures/post-filter-variety-profiles.json", import.meta.url), "utf8"));
const actualCatalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const actualRatingSource = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/fitness_ratings.json", import.meta.url), "utf8"));
const actualRatings = parseFitnessRatings(actualRatingSource);

test("all existing fitness ratings and training claims remain unchanged", () => {
  const rows = catalogProfiles.existingRatingIds.map(id => [id, actualRatings.get(id), actualRatings.trainingCharacteristics.get(id)]);
  assert.equal(createHash("sha256").update(JSON.stringify(rows)).digest("hex"), catalogProfiles.existingRatingsSha256);
});

for (const profile of catalogProfiles.profiles) test(`actual catalog has five feasible choices after all filters: ${profile.name}`, () => {
  const {exercises:evidence, ratings:evidenceRatings, state} = fixture();
  for (const exercise of evidence) exercise.elevationRequired = true;
  for (let axis = 0; axis < 10; axis++) evidenceRatings.set(evidence[axis].id,
    Array.from({length:10}, (_, index) => index === axis ? profile.zones[axis] : []));
  const ratings = new Map([...actualRatings, ...evidenceRatings]);
  Object.defineProperty(ratings, "trainingCharacteristics", {value: new Map([
    ...actualRatings.trainingCharacteristics,
    ...[...evidenceRatings].map(([id, levels]) => [id, levels.map(set => set.length > 0)]),
  ])});
  state.workoutHistory = [{sessionId:1,status:"Interrupted",blocks:[],decisions:evidence.slice(0,10).map((exercise,axis)=>
    ({rootExerciseId:exercise.id,outcome:"tick",decidedAtUnixMilliseconds:axis+1}))}];
  const session = WorkoutSession.createForApplication([...actualCatalog,...evidence], state, ()=>0, ()=>1000, ratings);
  assert.ok(evidence.every(exercise=>!session.isCompatibleWithModifiers(exercise,profile.modifiers)), "synthetic evidence cannot fill a catalog gap");
  assert.deepEqual(fitnessZones(inspectFitnessProfile(session.catalogExercises,session.state,ratings)),profile.zones);
  const before = structuredClone(session.state);
  for (const minutes of catalogProfiles.minutes) session.ensureSelectableWorkoutVariety(minutes,profile.modifiers);
  assert.deepEqual(session.state,before);
  for (const minutes of [15]) {
    const planned=WorkoutSession.createForApplication([...actualCatalog,...evidence],structuredClone(before),()=>0,()=>1000,ratings);
    planned.startWorkout(minutes,profile.modifiers);
    planned.ensureSelectableWorkoutVariety(minutes,profile.modifiers,planned.getSelectionGroups());
    assert.equal(planned.getActiveGroups().length,minutes);
  }
});

