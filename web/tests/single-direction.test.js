import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { WorkoutSession, createDefaultState, getWorkoutCatalog, canUseSingleDirection,
  getSequenceBlockCount, WORKOUT_MODIFIERS as M } from "../workout.js";

const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const contract = JSON.parse(await readFile(new URL("./fixtures/single-direction-cases.json", import.meta.url), "utf8"));
const now = Date.UTC(2026, 9, 20, 4);
const sessionFor = (minutes) => {
  const session = new WorkoutSession(catalog, { ...createDefaultState(),
    scores: { [contract.preferredRoot]: 10000 } }, () => 0.5, () => now);
  session.initialize();
  session.startWorkout(minutes, M.None);
  return session;
};
function finish(session) {
  const round = session.getNextGroup();
  session.beginRest(round, now + 15000);
  if (session.isIntermediateSequenceBlock(round)) session.advanceSequence(round);
  else session.recordOutcome(round, true);
  session.clearPendingRest();
}

function assertCompleteSides(session) {
  for (const key of new Set(session.getActiveGroups().map(g => g.selectionGroupId ?? g.id))) {
    const rounds = session.getActiveGroups().filter(g => (g.selectionGroupId ?? g.id) === key);
    const root = session.getSelectedExercise(rounds[0]);
    if (!root.sequenceBlocks.some(b => b.sideCue !== "None")) continue;
    assert.equal(rounds.length, root.sequenceBlocks.length * rounds[0].setCount);
    assert.deepEqual(rounds.filter(g => g.setNumber === 1).map(g => g.sequenceSideCue),
      root.sequenceBlocks.map(b => b.sideCue));
  }
}

test("shared directions retain their identity and both reviewed media segments", () => {
  assert.ok(contract.directionalRoots.every(id => canUseSingleDirection(catalog.find(e => e.id === id))));
  for (const minutes of [...contract.shortMinutes, ...contract.completeMinutes]) {
    for (const id of contract.directionalRoots) {
      assert.equal(getSequenceBlockCount(catalog.find(e => e.id === id), minutes), minutes < 30 ? 1 : 2);
    }
  }
  const paired = catalog.filter(e => e.sequenceBlocks.some(b => b.sideCue !== "None"));
  assert.ok(paired.length > 0);
  assert.ok(paired.every(e => !canUseSingleDirection(e)));
  for (const exercise of paired) for (const minutes of contract.shortMinutes) {
    assert.equal(getSequenceBlockCount(exercise, minutes), exercise.sequenceBlocks.length);
  }
});

for (const minutes of contract.shortMinutes) test(`${minutes} minutes selects one complete direction and keeps sides complete`, () => {
  const session = sessionFor(minutes);
  const rounds = session.getActiveGroups();
  const selected = rounds.filter(g => session.getSelectedExercise(g).id === contract.preferredRoot);
  assert.equal(rounds.length, minutes);
  assert.equal(selected.length, 1);
  assert.equal(selected[0].sequenceBlockCount ?? 1, 1);
  assert.equal(selected[0].sequenceDirectionCue, contract.directionCue);
  assert.equal(selected[0].sequenceMediaSegment, contract.mediaSegment);
  assertCompleteSides(session);
  assert.equal(session.state.scores[contract.preferredRoot], 10000);
  assert.equal(catalog.find(e => e.id === contract.preferredRoot).sequenceBlocks.length, 2);
  session.shuffleNextExercise(session.getNextGroup());
  session.reconfigureActiveWorkout(M.Silence, session.getNextGroup().id);
  assert.equal(session.getActiveGroups().length, minutes);
  assertCompleteSides(session);
});

for (const phase of ["ready", "movement", "rest"]) test(`single direction survives ${phase} reopening with the original history`, () => {
  let session = sessionFor(5);
  while (session.getSelectedExercise(session.getNextGroup()).id !== contract.preferredRoot) finish(session);
  const current = session.getNextGroup();
  if (phase === "movement") session.beginMovement(current, 27000, now + 27000);
  if (phase === "rest") session.beginRest(current, now + 15000);
  const saved = structuredClone(session.state);
  const rounds = session.getActiveGroups();
  session = new WorkoutSession(catalog, structuredClone(saved), () => 0.1, () => now + 864000000);
  session.restoreAfterReopen();
  assert.deepEqual(session.getActiveGroups(), rounds);
  assert.deepEqual(session.state.activeWorkoutSession, saved.activeWorkoutSession);
  assert.deepEqual(session.state.outcomes, saved.outcomes);
  assert.equal(session.getNextGroup().id, current.id);
  if (phase === "movement") assert.equal(session.state.pendingMovementMillisecondsRemaining, 27000);
  finish(session);
  const block = session.state.activeWorkoutSession.blocks.find(b => b.exerciseId === contract.preferredRoot);
  assert.equal(block.directionCue, contract.directionCue);
  assert.equal(session.state.scores[contract.preferredRoot], 10000);
});

test("duration edits freeze the whole current direction pair and use single directions for the remainder", () => {
  let session = sessionFor(45);
  while (session.getSelectedExercise(session.getNextGroup()).id !== contract.preferredRoot) finish(session);
  const current = session.getNextGroup();
  const protectedRounds = session.getActiveGroups().filter(g => g.order <= current.order ||
    g.selectionGroupId === current.selectionGroupId);
  session.resizeActiveWorkout(20);
  assert.deepEqual(session.getActiveGroups().slice(0, protectedRounds.length), protectedRounds);
  assert.equal(session.getSelectedExercise(current).sequenceBlocks.length, 2);
  assert.ok(!session.state.activeSingleDirectionExerciseIds.includes(contract.preferredRoot));
  assert.ok(session.state.activeSingleDirectionExerciseIds.includes(409));
  const saved = structuredClone(session.state);
  session = new WorkoutSession(catalog, saved, () => 0.1, () => now);
  session.restoreAfterReopen();
  assert.equal(session.getActiveGroups().length, 20);
  assert.equal(session.getSelectedExercise(session.getNextGroup()).sequenceBlocks.length, 2);
});

test("extra short-workout capacity retains complete side pairs", () => {
  const sidedIds = catalog.filter(e => e.sequenceBlocks.some(b => b.sideCue !== "None")).map(e => e.id);
  const session = new WorkoutSession(catalog, { ...createDefaultState(),
    scores: Object.fromEntries(sidedIds.map(id => [id, 10000])) }, () => 0.5, () => now);
  session.initialize();
  session.startWorkout(10, M.Insect | M.Shy);
  assert.ok(session.state.activeWorkoutSession.initialSelections.length < 10);
  assert.ok(session.state.activeWorkoutSession.initialSelections.some(s => sidedIds.includes(s.rootExerciseId)));
  assert.equal(session.getActiveGroups().length, 10);
  assertCompleteSides(session);
});

test("extending a workout preserves its current single direction and restores pairs elsewhere", () => {
  let session = sessionFor(5);
  while (session.getSelectedExercise(session.getNextGroup()).id !== contract.preferredRoot) finish(session);
  const current = session.getNextGroup();
  session.resizeActiveWorkout(45);
  assert.equal(session.getNextGroup().id, current.id);
  assert.ok(session.state.activeSingleDirectionExerciseIds.includes(contract.preferredRoot));
  assert.ok(!session.state.activeSingleDirectionExerciseIds.includes(409));
  session = new WorkoutSession(catalog, structuredClone(session.state), () => 0.1, () => now);
  session.restoreAfterReopen();
  assert.equal(session.getSelectedExercise(session.getNextGroup()).sequenceBlocks.length, 1);
  assert.equal(session.exercisesById.get(409).sequenceBlocks.length, 2);
});

test("an older unfinished workout keeps its complete direction pairs", () => {
  let session = sessionFor(45);
  const rounds = session.getActiveGroups();
  const saved = structuredClone(session.state);
  delete saved.activeSingleDirectionExerciseIds;
  session = new WorkoutSession(catalog, saved, () => 0.1, () => now);
  session.restoreAfterReopen();
  assert.deepEqual(session.getActiveGroups(), rounds);
  assert.equal(session.exercisesById.get(contract.preferredRoot).sequenceBlocks.length, 2);
  assert.equal(getWorkoutCatalog(catalog, 82).find(e => e.id === contract.preferredRoot).sequenceBlocks.length, 2);
});

test("shortening and reopening preserves the current whole side pair", () => {
  const scores = Object.fromEntries(catalog.filter(e => e.sequenceBlocks.some(b => b.sideCue !== "None"))
    .map(e => [e.id, 10000]));
  let session = new WorkoutSession(catalog, { ...createDefaultState(), scores }, () => 0.5, () => now);
  session.initialize();
  session.startWorkout(45, M.None);
  const target = session.getActiveGroups().find(g => g.sequenceSideCue && g.sequenceSideCue !== "None" &&
    g.order <= 13 && g.sequenceBlockCount === 2 && g.setCount === 1);
  assert.ok(target);
  while (session.getNextGroup().id !== target.id) finish(session);
  const current = session.getNextGroup();
  const pairedRoot = session.getSelectedExercise(current).id;
  const pair = session.getActiveGroups().filter(g => g.selectionGroupId === current.selectionGroupId);
  const prefix = session.getActiveGroups().filter(g => g.order <= current.order ||
    g.selectionGroupId === current.selectionGroupId);
  assert.equal(pair.length, 2);
  session.beginMovement(current, 27000, now + 27000);
  session.resizeActiveWorkout(15);
  assert.deepEqual(session.getActiveGroups().slice(0, prefix.length), prefix);
  assert.equal(session.getSelectedExercise(session.getNextGroup()).id, pairedRoot);
  session = new WorkoutSession(catalog, structuredClone(session.state), () => 0.1, () => now);
  session.restoreAfterReopen();
  assert.equal(session.getSelectedExercise(session.getNextGroup()).id, pairedRoot);
  assert.equal(session.state.pendingMovementMillisecondsRemaining, 27000);
  assert.equal(session.getActiveGroups().filter(g => g.selectionGroupId === current.selectionGroupId).length, 2);
});
