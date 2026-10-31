import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import test from 'node:test';
import {WorkoutSession,createDefaultState,getResolution,getSessionMovementId,WORKOUT_MODIFIERS as M} from '../workout.js';

const cases=JSON.parse(await readFile(new URL('fixtures/workout-variety-cases.json',import.meta.url),'utf8')).cases;
const now=()=>Date.parse('2026-10-31T01:00:00Z');
function catalogFor(item) {
  return getResolution(30).groups.flatMap((group,index)=>Array.from({length:item.copies},(_,copy)=>{
    const id=1000+index*10+copy, blocks=index===0?item.firstBlocks:item.otherBlocks;
    return {id,name:`Exercise ${id}`,video:`exercise_${id}.mp4`,primaryCanonicalGroup:group.canonicalGroups[0],secondaryCanonicalGroups:[],
      sessionMovementId:item.aliases?1000+index*10:id,score:0,muscularDemand:item.light&&copy===1?2:0,
      practice:'Test',motionProfile:'Test',mode:'Repetition',presentation:'Motion',holdFramePercent:0,
      sideSequence:blocks===2?'ScreenLeftThenRight':'Continuous',
      sequenceBlocks:Array.from({length:blocks},(_,b)=>({exerciseId:id,sideCue:blocks===2?(b?'ScreenRight':'ScreenLeft'):'None',directionCue:'None',mirrorMedia:b===1,mediaSegment:'Full'})),
      onlyFeetTouchGround:true,floorContactPoints:'Feet',shoeAgnostic:true,maxSpaceMeters:2,equipment:'None',silent:true,
      mirrorRelationship:'Agnostic',minimumMirrorCoverage:'None',directionSequence:'None',locomotionSpace:'None',
      hardFloorCompatibility:'Compatible',upperBodyClothingRequirement:'Agnostic',shyCompatibility:'Compatible'};
  }));
}
function start(item) {
  const catalog=catalogFor(item), state=createDefaultState(), group=getResolution(30).groups[0];
  state.keptExerciseRootIdsBySelectionGroupId[group.id]=[1000];
  state.exerciseScoreAdjustmentsByPhase={Warmup:{1000:10},PeakPerformance:{1000:10},Fatigued:{1000:10}};
  if(item.phaseFeedback) for(const exercise of catalog) {
    const first=exercise.id%10===0;
    state.exerciseScoreAdjustmentsByPhase.Warmup[exercise.id]=first?3:0;
    state.exerciseScoreAdjustmentsByPhase.PeakPerformance[exercise.id]=first?-2:3;
    state.exerciseScoreAdjustmentsByPhase.Fatigued[exercise.id]=first?-2:3;
  }
  const session=new WorkoutSession(catalog,state,()=>0.5,now);
  session.startWorkout(item.minutes,item.light?M.Light:M.None);
  return {session,catalog};
}
for(const item of cases) test(item.name,()=>{
  const {session}=start(item), selections=session.state.activeWorkoutSession.initialSelections;
  assert.equal(session.getActiveGroups().length,item.minutes);
  assert.equal(selections.length,item.expectedSelections);
  assert.equal(Math.max(...selections.map(s=>s.setCount)),item.expectedMaxSets);
  assert.equal(selections.find(s=>s.rootExerciseId===1000)?.setCount,item.expectedFirstSets);
  assert.equal(new Set(selections.map(s=>getSessionMovementId(session.exercisesById.get(s.rootExerciseId)))).size,selections.length);
  if(item.light) assert.ok(session.getActiveGroups().every(g=>session.getSelectedExercise(g).muscularDemand===0));
  assert.ok(session.state.keptExerciseRootIdsBySelectionGroupId[getResolution(30).groups[0].id].includes(1000));
});

test('additional distinct selections and Ready survive a cold reopen unchanged',()=>{
  const {session,catalog}=start(cases[0]);
  const rounds=JSON.stringify(session.getActiveGroups()), log=JSON.stringify(session.state.activeWorkoutSession);
  const restored=new WorkoutSession(catalog,structuredClone(session.state),()=>0.9,()=>now()+7*86400000);
  restored.initialize();
  assert.equal(restored.state.activeCoverageAccountingVersion,2);
  assert.equal(JSON.stringify(restored.getActiveGroups()),rounds);
  assert.equal(JSON.stringify(restored.state.activeWorkoutSession),log);
  assert.equal(restored.state.pendingMovementGroupId,null);
});

test('older allocation versions are retained on reopen',()=>{
  for(const version of [0,1]) {
    const {session,catalog}=start(cases[1]);
    session.state.activeCoverageAccountingVersion=version;
    const rounds=JSON.stringify(session.getActiveGroups());
    const restored=new WorkoutSession(catalog,structuredClone(session.state),()=>0.9,now);
    restored.initialize();
    assert.equal(restored.state.activeCoverageAccountingVersion,version);
    assert.equal(JSON.stringify(restored.getActiveGroups()),rounds);
  }
});

test('a prepared distinct lineup survives reopening before activation',()=>{
  const catalog=catalogFor(cases[0]), session=new WorkoutSession(catalog,createDefaultState(),()=>0.5,now);
  session.prepareWorkout(45,M.None);
  const rounds=JSON.stringify(session.getActiveGroups());
  const restored=new WorkoutSession(catalog,structuredClone(session.state),()=>0.9,now);
  restored.initialize();
  assert.equal(JSON.stringify(restored.getActiveGroups()),rounds);
  assert.equal(restored.state.activeWorkoutSession.blocks.length,0);
});
