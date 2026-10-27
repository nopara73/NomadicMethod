import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import test from 'node:test';
import {consolidateWorkoutCoverage,getAdditionalSetLoad,getMaximumDistinctLineupSize,getResolution,WorkoutSession,createDefaultState,WORKOUT_MODIFIERS as M} from '../workout.js';
const cases=JSON.parse(await readFile(new URL('fixtures/shared-coverage-cases.json',import.meta.url),'utf8'));
for(const minutes of cases.coverageResolutions) test('shared coverage in every '+minutes+'-minute anatomical group',()=>{
 const groups=getResolution(minutes).groups;
 for(let first=0;first<groups.length;first++) for(const blockCount of [1,2]) {
  const second=(first+1)%groups.length;
  const roots=groups.map((g,i)=>({id:1000+i,primaryCanonicalGroup:g.canonicalGroups[0],secondaryCanonicalGroups:i===first?[groups[second].canonicalGroups[0]]:[],sequenceBlocks:Array.from({length:i===first?blockCount:1},()=>({exerciseId:1000+i}))}));
  const lineup=new Map(groups.map((g,i)=>[g.id,roots[i].id]));
  const result=consolidateWorkoutCoverage(lineup,groups,new Map(roots.map(e=>[e.id,e])),new Map(roots.map((e,i)=>[e.id,i===first?100n:1n])),minutes);
  assert.equal(result.get(groups[first].id),roots[first].id);
  assert.equal(result.get(groups[second].id),roots[first].id);
  assert.equal(new Set(result.values()).size,groups.length-1);
  assert.equal(result.size,groups.length);
 }
});
for(const item of cases.loadCases) test(item.name,()=>{
 const scheduled=new Map(Object.entries(item.scheduled));
 const high=getAdditionalSetLoad(scheduled,new Map(Object.entries(item.overloaded)));
 const low=getAdditionalSetLoad(scheduled,new Map(Object.entries(item.preferred)));
 assert.ok(low.numerator*high.denominator<high.numerator*low.denominator);
});
test('a kept compound credits both groups, keeps duration and survives reopen',async()=>{
 const catalog=JSON.parse(await readFile(new URL('../../NomadicMethod/Assets/exercises.json',import.meta.url),'utf8'));
 const state=createDefaultState();state.keptExerciseRootIdsBySelectionGroupId={'r10.posterior-lower-leg-plantar-foot':[555]};
 const now=()=>Date.parse('2026-10-27T12:00:00Z');
 let session=new WorkoutSession(catalog,state,()=>0.5,now);session.initialize();
 session.startWorkout(10,M.HardFloor|M.Wall|M.SoleWallContact|M.RoomLocomotion|M.Floor|M.Elevation);
 const log=session.state.activeWorkoutSession;
 const calf=log.initialSelections.find(s=>s.rootExerciseId===555);
 assert.ok(calf);assert.ok(calf.wasKeptAtWorkoutStart);
 assert.deepEqual(calf.coveredWorkoutGroupIds,['r10.anterior-lateral-lower-leg-dorsal-foot','r10.posterior-lower-leg-plantar-foot']);
 assert.ok(calf.setCount<=2,'regional allocation must not return the freed time as four calf sets');
 assert.equal(session.getActiveGroups().length,10);
 const rounds=JSON.stringify(session.getActiveGroups());const before=JSON.stringify(log);
 const saved=structuredClone(session.state);
 session=new WorkoutSession(catalog,saved,()=>0.9,()=>now()+86400000);session.initialize();
 assert.equal(JSON.stringify(session.getActiveGroups()),rounds);
 assert.equal(JSON.stringify(session.state.activeWorkoutSession),before);
 assert.ok(session.state.keptExerciseRootIdsBySelectionGroupId['r10.posterior-lower-leg-plantar-foot'].includes(555));
});

test('one real compound can cover two groups without repeating its movement',async()=>{
 const catalog=JSON.parse(await readFile(new URL('../../NomadicMethod/Assets/exercises.json',import.meta.url),'utf8'));
 const root=catalog.find(e=>e.id===555);
 const groups=getResolution(10).groups.filter(g=>g.id.includes('lower-leg'));
 assert.equal(getMaximumDistinctLineupSize([root],groups,M.None,10),2);
});
