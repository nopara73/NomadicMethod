import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { completionProfileChanges, copyProfileSnapshot } from "../completion-profile.js";
import { calculateFitnessProfile, parseFitnessRatings } from "../fitness-profile.js";
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M } from "../workout.js";

const read = path => readFileSync(new URL(path, import.meta.url), "utf8");
const fixture = JSON.parse(read("./fixtures/completion-profile-cases.json"));
const catalog = JSON.parse(read("../../NomadicMethod/Assets/exercises.json"));
const ratings = parseFitnessRatings(JSON.parse(read("../../NomadicMethod/Assets/fitness_ratings.json")));
const now = Date.parse("2026-10-23T04:00:00Z");
const reload = state => JSON.parse(JSON.stringify(state));
const create = (state = createDefaultState(), profileRatings = ratings) =>
  new WorkoutSession(catalog, state, () => 0.4, () => now, profileRatings);

for (const item of fixture.cases) test("completion profile: " + item.name, () => {
  const before = JSON.stringify(item);
  const rows = completionProfileChanges(item.before, item.after);
  assert.deepEqual(rows.map(row => [row.label, row.before, row.after]), item.expected);
  assert.ok(rows.every(row => row.before !== row.after));
  assert.ok(rows.filter(row => [row.before, row.after].includes("—"))
    .every(row => row.description.includes("unknown")));
  assert.equal(JSON.stringify(item), before);
});

test("malformed snapshots are absent, never measured zero", () => {
  for (const value of [undefined, [], [1], new Array(10).fill(null), new Array(10).fill(NaN),
    new Array(10).fill(-1), new Array(10).fill(0.5)]) assert.equal(copyProfileSnapshot(value), null);
  const values = new Array(10).fill(2);
  assert.notEqual(copyProfileSnapshot(values), values);
});

test("snapshots survive preparation, cold movement restore, duration and modifier edits, completion and Done", () => {
  let session = create();
  session.initialize();
  session.prepareWorkout(3);
  assert.equal(session.state.activeFitnessProfileBaseline, null);
  session.activatePreparedWorkout();
  const baseline = [...session.state.activeFitnessProfileBaseline];
  assert.deepEqual(baseline, calculateFitnessProfile(catalog, session.state, ratings));
  const sessionId = session.state.activeWorkoutSession.sessionId;
  session.beginMovement(session.getNextGroup(), 45_000, now + 45_000);
  session = create(reload(session.state));
  session.restoreAfterReopen();
  assert.deepEqual(session.state.activeFitnessProfileBaseline, baseline);
  assert.equal(session.state.activeWorkoutSession.sessionId, sessionId);
  assert.equal(session.state.pendingMovementPausedByUser, true);
  session.pauseMovement(session.getNextGroup(), 30_000, true);
  session.resizeActiveWorkout(5);
  session.reconfigureActiveWorkout(session.state.activeWorkoutModifiers ^ M.Silence, session.getNextGroup().id);
  session = create(reload(session.state));
  session.restoreAfterReopen();
  assert.deepEqual(session.state.activeFitnessProfileBaseline, baseline);
  while (session.getNextGroup()) {
    const group = session.getNextGroup();
    session.beginRest(group, now + 15_000);
    if (session.isIntermediateSequenceBlock(group)) session.advanceSequence(group);
    else session.recordOutcome(group, true);
    session.clearPendingRest();
  }
  assert.equal(session.state.workoutCompleted, true);
  const final = [...session.state.completionFitnessProfileSnapshot];
  assert.ok(final.some(value => value > 0));
  assert.ok(completionProfileChanges(baseline, final).length > 0);
  assert.deepEqual(final, calculateFitnessProfile(catalog, session.state, ratings));
  assert.equal(session.state.workoutHistory.length, 1);
  const history = JSON.stringify(session.state.workoutHistory);
  session = create(reload(session.state), new Map()); // A changed ratings asset cannot rewrite the summary.
  session.restoreAfterReopen();
  assert.deepEqual(session.state.activeFitnessProfileBaseline, baseline);
  assert.deepEqual(session.state.completionFitnessProfileSnapshot, final);
  assert.equal(JSON.stringify(session.state.workoutHistory), history);
  session.acknowledgeCompletion();
  assert.equal(session.state.activeFitnessProfileBaseline, null);
  assert.equal(session.state.completionFitnessProfileSnapshot, null);
  assert.deepEqual(calculateFitnessProfile(catalog, session.state, ratings), final);
});

test("legacy and explicit end never invent a comparison", () => {
  let session = create();
  session.initialize();
  session.startWorkout(3);
  delete session.state.activeFitnessProfileBaseline;
  session = create(reload(session.state));
  session.restoreAfterReopen();
  assert.equal(session.state.activeFitnessProfileBaseline, null);
  while (session.getNextGroup()) session.recordOutcome(session.getNextGroup(), true);
  assert.deepEqual(completionProfileChanges(session.state.activeFitnessProfileBaseline,
    session.state.completionFitnessProfileSnapshot), []);
  session.acknowledgeCompletion();
  session.startWorkout(3);
  session.endActiveWorkout();
  assert.equal(session.state.workoutCompleted, false);
  assert.equal(session.state.activeFitnessProfileBaseline, null);
  assert.equal(session.state.completionFitnessProfileSnapshot, null);
  assert.equal(session.state.workoutHistory.at(-1).status, "Interrupted");
});

test("both completion surfaces consume frozen snapshots and expose the same concise list", () => {
  const native = read("../../NomadicMethod/MainActivity.cs");
  const app = read("../app.js");
  const layout = read("../../NomadicMethod/Resources/layout/screen_completion.xml");
  const html = read("../index.html").split('id="completion-screen"')[1];
  assert.match(native, /CompletionProfilePolicy.Changes\(\s*_state.ActiveFitnessProfileBaseline, _state.CompletionFitnessProfileSnapshot/);
  assert.match(app, /renderCompletionProfile\(elements.completionChanges, session.state.activeFitnessProfileBaseline,\s*session.state.completionFitnessProfileSnapshot/);
  assert.ok(layout.includes('id="@+id/completion_changes"'));
  assert.ok(html.includes('id="completion-changes"'));
  assert.doesNotMatch(layout + html, /FitnessProfileView|fitness-profile|muscle.region|minutes.finished/);
  assert.match(read("../scripts/build.mjs"), /completion-profile/);
});
