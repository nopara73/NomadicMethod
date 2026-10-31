import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import test from 'node:test';
import {WorkoutSession,createDefaultState,getSessionMovementId,getWorkoutCatalog} from '../workout.js';
import {parseFitnessRatings,FITNESS_DIMENSIONS} from '../fitness-profile.js';
import {resolveReactionDrills} from '../reaction-drills.js';

const read = path => JSON.parse(readFileSync(new URL(path,import.meta.url),'utf8'));
const catalog=read('../../NomadicMethod/Assets/exercises.json');
const ratings=parseFitnessRatings(read('../../NomadicMethod/Assets/fitness_ratings.json'));
const document=read('../../NomadicMethod/Assets/reaction_drills.json');
const frozen=read('../../docs/catalog-audit/reaction-protocols-through106.json');
const cases=read('./fixtures/context-response-cases.json');
const plans=resolveReactionDrills(document,catalog);

test('context admissions preserve all prior ratings, claims and complete reaction definitions',()=>{
  const rows=[...ratings].filter(([id])=>id<1741).sort(([a],[b])=>a-b)
    .map(([id,levels])=>[id,levels,ratings.trainingCharacteristics.get(id)]);
  assert.equal(rows.length,cases.previousRecordCount);
  assert.equal(createHash('sha256').update(JSON.stringify(rows)).digest('hex'),cases.previousRatingsAndClaimsSha256);
  assert.deepEqual({...document,
    families:document.families.filter(f=>frozen.families.some(old=>old.id===f.id)),
    plans:document.plans.filter(p=>frozen.plans.some(old=>old.exerciseId===p.exerciseId))},frozen);
});

for(const entry of cases.families) test(`context family ${entry.family} uses exact real responses and its existing identity`,()=>{
  const exercise=catalog.find(e=>e.id===entry.id),plan=plans.get(entry.id);
  assert.equal(getSessionMovementId(exercise),entry.movementId);
  assert.equal(plan.protocol,'ContextChoice');
  assert.deepEqual(plan.actions.map(a=>a.sourceId),entry.sources);
  assert.equal(exercise.insectCompatibility,'Incompatible');
  assert.equal(exercise.hardFloorCompatibility,'Compatible');
  const claims=[exercise.primaryCanonicalGroup,...exercise.secondaryCanonicalGroups];
  for(const action of plan.actions){
    const source=catalog.find(e=>e.id===action.sourceId);
    assert.ok(claims.every(group=>[source.primaryCanonicalGroup,...source.secondaryCanonicalGroups].includes(group)));
    const bytes=readFileSync(new URL('../../NomadicMethod/Assets/'+source.video,import.meta.url));
    assert.equal(createHash('sha256').update(bytes).digest('hex'),action.sourceVideoSha256);
  }
  assert.deepEqual(ratings.get(entry.id)[8],[1,3,4,5]);
});

for(const profile of cases.closedProfiles) test(`fully assessed level 5 has five complete choices under ${profile.modifiers}`,()=>{
  for(const minutes of cases.durations){
    const session=WorkoutSession.createForApplication(catalog,createDefaultState(),()=>0.42,()=>1,ratings);
    session.getFitnessSelectionZones=()=>FITNESS_DIMENSIONS.map(()=>[profile.level]);
    session.startWorkout(minutes,profile.modifiers);
    assert.equal(session.getActiveGroups().length,minutes);
    const eligibility=session.getFitnessEligibility();
    assert.ok(session.getSelectedSequencePlacements().every(p=>eligibility.get(p.root.id)));
    session.ensureSelectableWorkoutVariety(minutes,profile.modifiers,session.getSelectionGroups());
  }
});

test('revision-106 paused work keeps its frozen pool, choices and checkpoint',()=>{
  const original=new WorkoutSession(getWorkoutCatalog(catalog,106),createDefaultState(),()=>0.42,()=>1,ratings);
  original.startWorkout(3,0);
  original.state.catalogRevision=106;
  original.state.activeCatalogRevision=106;
  original.pauseMovement(original.getNextGroup(),27000,true);
  const saved=structuredClone(original.state);
  const restored=WorkoutSession.createForApplication(catalog,structuredClone(saved),()=>0.9,()=>2,ratings);
  restored.restoreAfterReopen();
  assert.deepEqual(restored.state.selectedExerciseIds,saved.selectedExerciseIds);
  assert.deepEqual(restored.state.activeWorkoutSession,saved.activeWorkoutSession);
  assert.equal(restored.state.pendingMovementMillisecondsRemaining,27000);
  assert.equal(restored.getNextGroup().id,original.getNextGroup().id);
  assert.equal(getWorkoutCatalog(catalog,106).length,930);
  assert.ok(getWorkoutCatalog(catalog,106).every(e=>e.id<1741));
});
