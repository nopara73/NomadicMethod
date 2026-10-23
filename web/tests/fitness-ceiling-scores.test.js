import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { resolveReactionDrills } from '../reaction-drills.js';
import { assertMinimumCoverage, buildAudit, coveragePolicy, dimensions, renderCoverageReport, renderScores } from '../../tools/generate-exercise-fitness-ceiling-scores.mjs';
const read = name => readFileSync(new URL(name, import.meta.url), 'utf8');
const catalogSource = read('../../NomadicMethod/Assets/exercises.json').replaceAll('\r\n', '\n');
const catalog = JSON.parse(catalogSource);
const assessment = JSON.parse(read('../../tools/ExerciseFitnessCeilingScores.json'));
const protocols = resolveReactionDrills(JSON.parse(read('../../NomadicMethod/Assets/reaction_drills.json')), catalog);
const context = { catalogRevision: assessment.catalogRevision,
  catalogSha256: createHash('sha256').update(catalogSource).digest('hex'), reactionProtocols: protocols };
const audit = () => buildAudit(catalog, assessment, context);
const profileFor = (document, id) => Object.values(document.profiles).find(p => p.exerciseIds.includes(id));

test('every exercise has an explicit conditional level set for all ten characteristics', () => {
  const { rows } = audit();
  assert.equal(rows.length, catalog.length);
  assert.deepEqual(rows.map(row => row.id), catalog.map(e => e.id).sort((a,b) => a-b));
  assert.ok(rows.every(row => row.reviewStatus === 'provisional-training-level-assessment'));
  assert.ok(rows.every(row => dimensions.every(d => Array.isArray(row[d]))));
  assert.equal(renderScores(rows), read('../../docs/catalog-audit/acsm-fitness-ceiling-scores.csv').replaceAll('\r\n','\n'));
});
test('palms and other actual adjustable-resistance actions support several strength and endurance levels', () => {
  const { rows } = audit();
  for (const id of [241,519,560,614,745,149,585,586,587]) {
    const row = rows.find(r => r.id === id);
    assert.deepEqual(row.muscularStrength, [1,2,3,4,5]);
    assert.deepEqual(row.muscularEndurance, [1,2,3,4,5]);
    assert.match(row.trainingConditions, /opposing force|sustained force/);
  }
  assert.deepEqual(rows.find(r => r.id === 115).muscularStrength, [3,4]);
  assert.deepEqual(rows.find(r => r.id === 518).cardiorespiratoryEndurance, []);
});
test('coverage counts supported sets, deduplicates within each cell and passes every minimum', () => {
  const { rows, summary } = audit();
  assert.equal(coveragePolicy.matching, 'explicit-trained-levels');
  assert.equal(coveragePolicy.minimumDistinctSessionMovements, 5);
  assert.doesNotThrow(() => assertMinimumCoverage(summary));
  for (const d of dimensions) for (const level of [1,2,3,4,5]) {
    const expected = [...new Set(rows.filter(r => r[d].includes(level)).map(r => r.sessionMovementId))].sort((a,b)=>a-b);
    assert.deepEqual(summary.coverage[d][level].sessionMovementIds, expected);
  }
  assert.equal(renderCoverageReport(summary), read('../../docs/catalog-audit/acsm-fitness-ceiling-gaps.md').replaceAll('\r\n','\n'));
});
test('new IDs and changed catalog snapshots require deliberate reassessment', () => {
  assert.throws(() => buildAudit([...catalog,{...catalog[0],id:999999}], assessment, context), /Missing explicit assessments/);
  assert.throws(() => buildAudit(catalog, assessment,{...context,catalogSha256:'changed'}), /Catalog changed/);
});

test('profile contributor diagnostics distinguish precise evidence from trainable ranges', () => {
  const { profileContributorCoverage: diagnostic } = audit().summary;
  const coverage = diagnostic.coverage;
  assert.deepEqual(coverage.reactionTime[1].sessionMovementIds, [266]);
  assert.equal(coverage.reactionTime[3].count, 0);
  assert.deepEqual(coverage.muscularStrength[3].exerciseIds, [1702,1710]);
  assert.deepEqual(coverage.muscularStrength[4].exerciseIds, [1701,1705]);
  assert.deepEqual(coverage.muscularStrength[5].exerciseIds, [1704,1712]);
  assert.deepEqual(coverage.balance[3].exerciseIds, [1702,1703,1709,1710]);
  assert.deepEqual(coverage.coordination[3].exerciseIds, [1706,1709,1710]);
  assert.deepEqual(coverage.coordination[4].exerciseIds, [1705,1707]);
  assert.deepEqual(coverage.coordination[5].exerciseIds, [1708]);
  assert.deepEqual(coverage.reactionTime[2].sessionMovementIds, [144,180,266,278,1516]);
  assert.deepEqual(coverage.flexibility[4].exerciseIds, [1713]);
  assert.deepEqual(coverage.flexibility[5].exerciseIds, [1714]);
  assert.deepEqual(coverage.power[5].exerciseIds, [1715]);
  assert.ok(!coverage.muscularStrength[5].exerciseIds.includes(241));
  assert.equal(coverage.speed[3].count, 0);
});
test('invalid, repeated, unsorted, zero and fractional levels are rejected', () => {
  for (const invalid of [5,[0],[6],[1,1],[3,2],[1.5]]) {
    const copy = structuredClone(assessment); Object.values(copy.profiles)[0].levels.power = invalid;
    assert.throws(() => buildAudit(catalog,copy,context), /Invalid power levels/);
  }
  const copy = structuredClone(assessment);const p=Object.values(copy.profiles)[0];
  p.exerciseIds.push(p.exerciseIds[0]);
  assert.throws(() => buildAudit(catalog,copy,context), /assessed more than once/);
  p.exerciseIds.pop();delete p.levels.power;
  assert.throws(() => buildAudit(catalog,copy,context), /all ten characteristics/);
});
test('training conditions are required and static holds cannot claim speed or power', () => {
  const copy=structuredClone(assessment);const p=profileFor(copy,134);
  p.trainingConditions='';assert.throws(()=>buildAudit(catalog,copy,context), /training conditions/);
  p.trainingConditions='A deliberately specified synthetic test condition.';p.levels.power=[1];
  assert.throws(()=>buildAudit(catalog,copy,context), /Static exercise/);
});
test('reaction support follows actual decision task, including noncontiguous choice sets', () => {
  const rows=audit().rows;
  assert.deepEqual(rows.find(r=>r.id===543).reactionTime,[1,2]);
  assert.deepEqual(rows.find(r=>r.id===1604).reactionTime,[1,3,4,5]);
  const copy=structuredClone(assessment);profileFor(copy,543).levels.reactionTime=[1,2,5];
  assert.throws(()=>buildAudit(catalog,copy,context), /matching actual reaction protocol/);
  const choice=structuredClone(assessment);profileFor(choice,1604).levels.reactionTime=[1,2,3,4,5];
  assert.throws(()=>buildAudit(catalog,choice,context), /matching actual reaction protocol/);
});
function fixture() {
  const exercises=[];const profiles={};const reactionProtocols=new Map();
  for(const level of [1,2,3,4,5]){
    const ids=[];
    for(let n=0;n<5;n++){
      const id=level*10+n;ids.push(id);exercises.push({id,name:`Synthetic ${id}`,mode:'Repetition',presentation:'Motion',reactionCueLevel:2});
      if(level!==2)reactionProtocols.set(id,{protocol:{1:'Initiation',3:'TwoChoice',4:'FourChoice',5:'ContextChoice'}[level]});
    }
    profiles[level]={levels:Object.fromEntries(dimensions.map(d=>[d,[level]])),exerciseIds:ids,
      basis:'Synthetic boundary fixture only, not an exercise assessment.',
      trainingConditions:'Synthetic boundary fixture only, not a physiological training claim.'};
  }
  return {exercises,document:{...structuredClone(assessment),profiles},context:{...context,reactionProtocols}};
}
test('five distinct identities pass; five alias records with only four identities fail',()=>{
  const f=fixture();assert.doesNotThrow(()=>assertMinimumCoverage(buildAudit(f.exercises,f.document,f.context).summary));
  f.exercises[1].sessionMovementId=f.exercises[0].id;
  const {summary}=buildAudit(f.exercises,f.document,f.context);
  assert.equal(summary.coverage.muscularStrength[1].exerciseCount,5);
  assert.equal(summary.coverage.muscularStrength[1].count,4);
  assert.throws(()=>assertMinimumCoverage(summary),/at least 5 distinct session movements/);
});
test('maximum level alone never populates an unlisted lower level',()=>{
  const f=fixture();f.document.profiles[1].exerciseIds.pop();f.exercises=f.exercises.filter(e=>e.id!==14);
  const {summary}=buildAudit(f.exercises,f.document,f.context);
  assert.equal(summary.coverage.power[1].count,4);assert.equal(summary.coverage.power[5].count,5);
  assert.throws(()=>assertMinimumCoverage(summary),/power 1: 4\/5/);
});
test('an explicit multi-level set counts in each supported cell without alias multiplication',()=>{
  const f=fixture();f.document.profiles[1].levels.muscularStrength=[1,2,3,4,5];
  const {summary}=buildAudit(f.exercises,f.document,f.context);
  assert.deepEqual([1,2,3,4,5].map(l=>summary.coverage.muscularStrength[l].count),[5,10,10,10,10]);
});
test('check cannot bypass a failed minimum using report-only',()=>{
  assert.throws(()=>execFileSync(process.execPath,[fileURLToPath(new URL('../../tools/generate-exercise-fitness-ceiling-scores.mjs',import.meta.url)),'--check','--report-only'],{encoding:'utf8',stdio:'pipe'}),e=>e.status!==0&&/cannot bypass/.test(e.stderr));
});
