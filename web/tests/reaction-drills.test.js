import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { ReactionDrill, resolveReactionDrills } from '../reaction-drills.js';
import { getWorkoutCatalog, FITNESS_COVERAGE_EXERCISE_IDS } from '../workout.js';
const json = relative => JSON.parse(readFileSync(new URL(relative, import.meta.url), 'utf8'));
const catalog = json('../../NomadicMethod/Assets/exercises.json');
const document = json('../../NomadicMethod/Assets/reaction_drills.json');
const trace = json('./fixtures/reaction-drill-trace.json');
const plans = resolveReactionDrills(document, catalog);
const stopTrace = json('./fixtures/reaction-stop-only-trace.json');
const stopPlan = { protocol: 'StopOnly', actions: [{ label: 'Known action', durationSeconds: 1 }] };
const plan = { protocol: 'ContextChoice', actions: Array.from({ length: 4 }, (_, i) => ({ label: `Action ${i}`, durationSeconds: 1 })) };

test('Android and web use the same delayed-callback and context-mapping trace', () => {
  let index = 0;
  const drill = new ReactionDrill(plan, () => trace.random[index++]);
  for (const step of trace.steps) {
    drill.advance(step.now, 45000 - step.now);
    for (const key of ['phase', 'reverse', 'number', 'actionIndex', 'deadline']) assert.equal(drill[key], step[key], `${step.now}: ${key}`);
    assert.equal(drill.demoActionIndex, step.phase === 'Feedback' ? step.actionIndex : -1);
  }
});

test('shared stop-only trace has predictable starts and never reveals a cancelled response', () => {
  let index = 0;
  const drill = new ReactionDrill(stopPlan, () => stopTrace.random[index++]);
  for (const step of stopTrace.steps) {
    assert.equal(drill.advance(step.now, 45000 - step.now), step.changed);
    for (const key of ['phase', 'number', 'deadline', 'cue', 'demoActionIndex']) {
      assert.equal(drill[key], step[key], `${step.now}: ${key}`);
    }
    assert.equal(drill.reverse, false);
  }
});

test('stop-only buffering, delayed callbacks, restart and final-trial budget fail closed', () => {
  const drill = new ReactionDrill(stopPlan, () => 0.9);
  assert.equal(drill.advance(0, 45000, false), false);
  assert.equal(drill.phase, 'Idle');
  drill.advance(0, 45000);
  assert.equal(drill.advance(5000, 40000, false), false);
  assert.equal(drill.number, 3);
  assert.equal(drill.advance(5000, 40000), true);
  assert.equal(drill.number, 2);
  assert.equal(drill.deadline, 6000);
  assert.equal(drill.demoActionIndex, -1);
  const reopened = new ReactionDrill(stopPlan, () => 0);
  reopened.advance(5000, 40000);
  assert.equal(reopened.number, 3);
  assert.equal(reopened.deadline, 6000);
  const short = new ReactionDrill(stopPlan, () => 0);
  short.advance(0, 5649);
  assert.equal(short.phase, 'Complete');
  assert.equal(short.demoActionIndex, -1);
  const delayed = new ReactionDrill(stopPlan, () => 0.9);
  delayed.advance(0, 45000); delayed.advance(1000, 44000); delayed.advance(2000, 43000);
  delayed.advance(3000, 2199);
  assert.equal(delayed.phase, 'Complete');
  assert.equal(delayed.demoActionIndex, -1);
});
test('unready media cannot start or reveal a response; a paused trial restarts cleanly', () => {
  const drill = new ReactionDrill(plan, () => 0);
  assert.equal(drill.advance(0, 45000, false), false);
  assert.equal(drill.phase, 'Idle');
  drill.advance(0, 45000);
  assert.equal(drill.advance(5000, 40000, false), false);
  assert.equal(drill.phase, 'Ready');
  const resumed = new ReactionDrill(plan, () => 0);
  resumed.advance(5000, 40000);
  assert.equal(resumed.phase, 'Ready');
  assert.equal(resumed.number, 0);
  assert.equal(resumed.deadline, 6200);
});
test('a complete decision, real action and reset must fit before another cue', () => {
  const drill = new ReactionDrill(plan, () => 0);
  drill.advance(0, 4049);
  assert.equal(drill.phase, 'Complete');
  const delayed = new ReactionDrill(plan, () => 0);
  delayed.advance(0, 45000);
  delayed.advance(1200, 2500);
  assert.equal(delayed.phase, 'Complete');
  assert.equal(delayed.demoActionIndex, -1);
});
test('twenty-one explicit protocols retain five physical movement families', () => {
  assert.equal(plans.size, 21);
  for (const family of document.families) {
    const members = [...plans.values()].filter(p => p.familyId === family.id);
    assert.deepEqual(members.map(p => p.actions.length), family.id === 'arm' ? [1, 2, 4, 4, 1] : [2, 4, 4, 1]);
    assert.equal(new Set(members.map(p => catalog.find(e => e.id === p.exerciseId).sessionMovementId)).size, 1);
    assert.ok(members.every(p => catalog.find(e => e.id === p.exerciseId).insectCompatibility === 'Incompatible'));
  }
});
test('unavailable, malformed or duplicate response definitions fail closed', () => {
  const copy = structuredClone(document);
  copy.plans.push(copy.plans[0]);
  assert.throws(() => resolveReactionDrills(copy, catalog), /Invalid reaction plan/);
  copy.plans.pop();
  copy.families[0].actions[0].video = 'reaction_videos/../exercise_0278.mp4';
  assert.throws(() => resolveReactionDrills(copy, catalog), /Invalid reaction action/);
  assert.throws(() => resolveReactionDrills(document, catalog.filter(e => e.id !== 1602)), /Invalid reaction plan/);
  assert.throws(() => resolveReactionDrills({ version: 1, families: [], plans: [] }, catalog), /Missing reaction plan/);
  const missing = structuredClone(document);
  missing.plans.pop();
  assert.throws(() => resolveReactionDrills(missing, catalog), /Missing reaction plan/);
  const mismatched = structuredClone(document);
  mismatched.families[0].actions[0].sourceId = 151;
  assert.throws(() => resolveReactionDrills(mismatched, catalog), /Invalid reaction training claim/);
  const ordinaryCatalog = catalog.map(item => item.id === 1602 ? { ...item, practice: 'Boxing' } : item);
  assert.throws(() => resolveReactionDrills(document, ordinaryCatalog), /Invalid reaction plan/);
  const old = getWorkoutCatalog(catalog, 87);
  assert.equal(old.length, 864);
  assert.ok(old.every(item => !FITNESS_COVERAGE_EXERCISE_IDS.includes(item.id)));
  assert.equal(getWorkoutCatalog(catalog, 88).length, 895);
  assert.equal(getWorkoutCatalog(catalog, 89).length, 901);
  assert.equal(getWorkoutCatalog(catalog, 90).length, 917);
});

test('single-cue initiation waits unpredictably and reveals only the prepared arm raise', () => {
  const initiation = plans.get(1621);
  assert.equal(initiation.protocol, 'Initiation');
  assert.equal(initiation.actions.length, 1);
  assert.equal(initiation.actions[0].sourceId, 266);
  for (const random of [0, 0.5, 0.999]) {
    const drill = new ReactionDrill(initiation, () => random);
    drill.advance(0, 45000);
    assert.equal(drill.phase, 'Ready');
    assert.ok(drill.deadline >= 1200 && drill.deadline <= 2800);
    assert.equal(drill.demoActionIndex, -1);
    drill.advance(drill.deadline, 42000);
    assert.equal(drill.phase, 'Prompt');
    assert.equal(drill.actionIndex, 0);
    assert.equal(drill.demoActionIndex, -1);
    drill.advance(drill.deadline, 41000);
    assert.equal(drill.phase, 'Feedback');
    assert.equal(drill.demoActionIndex, 0);
  }
});
