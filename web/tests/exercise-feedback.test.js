import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { feedbackDirection, feedbackThreshold, feedbackDistance, feedbackSymbols, feedbackVisual, stampFrame, stampTiming, automaticMehX, ExerciseFeedbackView } from "../exercise-feedback.js";
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M } from "../workout.js";

const fixtures = JSON.parse(await readFile(new URL("./fixtures/exercise-feedback-cases.json", import.meta.url)));
const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url)));
test("direction emoji agree with Android's shared feedback vocabulary", () => assert.deepEqual(feedbackSymbols, fixtures.symbols));

// A controllable animation clock checks the real view's completion/lifecycle
// contract independently of the workout timer or a browser's frame rate.
function transitionView(reduced = false) {
  const jobs = [], choices = [], completions = [];
  const surface = { style: {} };
  globalThis.document = { hidden: false, querySelector: () => surface, getElementById: () => null,
    body: { classList: { add() {}, remove() {} } } };
  const view = Object.assign(Object.create(ExerciseFeedbackView.prototype), {
    epoch: 0, active: true, selected: null, busy: false, surface,
    card: { style: {} }, footer: { style: {} }, burst: { hidden: true, style: {} },
    burstEmoji: { style: {} }, burstImpact: { style: {} }, burstCheck: { style: {} },
    hintPose: () => ({ x: 0, y: 0, scale: 1.04 }),
    reduced: () => reduced, preview: () => {}, cancelGesture: () => {}, cancelAnimation: () => {},
    render(value) { this.selected = value; },
    timeline(duration, draw) {
      this.cancelTimeline(); draw(0);
      return new Promise(resolve => { this.job = { duration, draw, resolve }; jobs.push(this.job); });
    },
    cancelTimeline() { this.job?.resolve(false); this.job = null; },
    hooks: { choose: value => { choices.push(value); return true; }, completed: () => completions.push(true) }
  });
  const finish = async () => {
    for (let i = 0; i < 6; i++) {
      const job = view.job;
      if (job) { view.job = null; job.draw(job.duration); job.resolve(true); }
      await Promise.resolve();
    }
  };
  return { view, choices, completions, jobs, finish };
}
test("Stamp follows the approved preview's phase boundaries and sustained hold", () => {
  assert.deepEqual(stampTiming, fixtures.stampTiming);
  for (const {t, ...expected} of fixtures.stamp) {
    const actual = stampFrame(t, 360, {x:20,y:30,scale:.2});
    for (const [key, value] of Object.entries(expected)) assert.ok(Math.abs(actual[key]-value)<1e-8, `${t}: ${key}`);
  }
  for (const [t,x] of fixtures.automatic) assert.ok(Math.abs(automaticMehX(t,360)-x)<1e-8);
});
for (const [width, height] of [[320,568], [390,844], [844,390], [768,1024]])
  test(`Stamp remains legible at ${width} x ${height}`, async () => {
    globalThis.innerWidth = width; globalThis.innerHeight = height;
    const { view, jobs, finish } = transitionView();
    const pending = view.commit("hard");
    assert.equal(jobs[0].duration, 1700);
    jobs[0].draw(1699);
    assert.equal(view.burstCheck.style.opacity, 1);
    assert.equal(view.surface.style.opacity, ".8", "the last frame remains visible");
    await finish(); await pending;
  });
test("timeout performs the complete automatic swipe before starting the stamp", async () => {
  globalThis.innerWidth=360; globalThis.innerHeight=800;
  const {view,jobs,completions,finish}=transitionView();
  const pending=view.commit("x",undefined,true);
  assert.equal(jobs[0].duration,1270); assert.equal(completions.length,0);
  await finish(); await pending;
  assert.deepEqual(jobs.map(j=>j.duration),[1270,1700,420]);
  assert.equal(completions.length,1);
});

test("screen gestures leave interactive controls untouched", () => {
  const view = Object.assign(Object.create(ExerciseFeedbackView.prototype), { active: true });
  for (const control of ["button", "a", "input", "select", "textarea", "[role=button]", "dialog", "[contenteditable=true]"]) {
    view.down({ isPrimary: true, button: 0, target: { closest: selector => selector.includes(control) },
      preventDefault: () => assert.fail("a control tap must not become feedback") });
    assert.equal(view.gesture, undefined);
  }
});
test("instructional pull holds its full displacement before springing back", () => {
  globalThis.document = { hidden: false };
  let motion;
  const view = Object.assign(Object.create(ExerciseFeedbackView.prototype), {
    active: true, reduced: () => false, hooks: { running: () => true },
    hints: { querySelector: () => ({ style: {}, animate: () => ({}) }) },
    animate: (frames, duration, easing) => { motion = { frames, duration, easing }; }
  });
  view.nudge();
  assert.equal(motion.easing, "linear");
  assert.equal(motion.frames[1].transform, view.pose(-40));
  assert.equal(motion.frames[2].transform, view.pose(-40));
  assert.ok((motion.frames[2].offset - motion.frames[1].offset) * motion.duration >= 120,
    "the full pull should visibly hold for at least 120ms");
});
for (const response of fixtures.responses) test(`${response} advances exactly once when its animation ends, without waiting for rest`, async () => {
  globalThis.document = { hidden: false }; globalThis.innerWidth = 390; globalThis.innerHeight = 844;
  const { view, choices, completions, finish } = transitionView();
  const pending = view.commit(response);
  await view.commit("x");
  assert.deepEqual(choices, [response]); assert.equal(completions.length, 0);
  await finish(); await pending;
  assert.equal(completions.length, 1); assert.equal(view.burst.hidden, true);
});
test("interrupted feedback retains its response and resumes once without recording twice", async () => {
  globalThis.document = { hidden: false }; globalThis.innerWidth = 390; globalThis.innerHeight = 844;
  const { view, choices, completions, finish } = transitionView();
  const pending = view.commit("hard"); view.interrupt(); await pending;
  assert.equal(completions.length, 0); assert.equal(view.selected, "hard"); assert.equal(view.burst.hidden, true);
  const resumed = view.finishSelection(); await finish(); await resumed;
  assert.deepEqual(choices, ["hard"]); assert.equal(completions.length, 1);
});
test("reduced motion retains a readable confirmation without zoom or impact", async () => {
  globalThis.document = { hidden: false }; globalThis.innerWidth = 390; globalThis.innerHeight = 844;
  const { view, completions, jobs, finish } = transitionView(true);
  const pending = view.commit("easy"); assert.equal(jobs[0].duration,900);
  await finish(); await pending; assert.equal(completions.length, 1); assert.equal(view.burst.hidden, true);
});
for (const trace of fixtures.traces) test(trace.name, () => {
  let direction = null;
  for (const [x, y, expected] of trace.points) {
    direction = feedbackDirection(x, y, direction);
    assert.equal(direction, expected);
  }
});
test("commit threshold matches the preview's screen-relative displacement", () => {
  for (const [width, expected] of fixtures.thresholds) assert.ok(Math.abs(feedbackThreshold(width) - expected) < 1e-8);
  assert.equal(feedbackDistance(-90, 3, "x"), 90);
  assert.equal(feedbackDistance(90, 3, "x"), -90);
});
function setup() {
  const session = new WorkoutSession(catalog, createDefaultState(), () => .4);
  session.initialize(); session.startWorkout(3, M.None);
  while (session.isIntermediateSequenceBlock(session.getNextGroup())) {
    const group = session.getNextGroup(); session.beginRest(group, Date.now() + 15000);
    assert.equal(session.setPendingRestFeedback("easy"), false);
    session.advanceSequence(group); session.clearPendingRest();
  }
  session.beginRest(session.getNextGroup(), Date.now() + 15000);
  return session;
}
for (const response of fixtures.responses) test(`${response}: undo, cold restore, edit duration and record exactly once`, () => {
  let session = setup();
  const groupId = session.getNextGroup().id;
  session.setPendingRestFeedback(response);
  session.pauseRest(session.getNextGroup(), 12000);
  session.state.pendingRestMediaPositionMilliseconds = 1234;
  const saved = structuredClone(session.state);
  session = new WorkoutSession(catalog, saved, () => .4);
  session.restoreAfterReopen();
  assert.equal(session.getPendingRestFeedback(), response);
  assert.equal(session.getPendingRestMillisecondsRemaining(Date.now()), 12000);
  session.setPendingRestFeedback(null); assert.equal(session.getPendingRestFeedback(), null);
  session.setPendingRestFeedback(response);
  session.resizeActiveWorkout(15);
  assert.equal(session.getNextGroup().id, groupId);
  assert.equal(session.state.pendingRestMediaPositionMilliseconds, 1234);
  assert.equal(session.getPendingRestFeedback(), response);
  const before = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
  const group = session.getNextGroup();
  const root = session.getSequenceRoot(session.getSelectedExercise(group));
  const phase = session.getExercisePhase(group);
  session.recordOutcome(session.getNextGroup(), response); session.clearPendingRest();
  assert.equal(session.state.activeWorkoutSession.decisions.at(-1).outcome, response);
  const adjustment = fixtures.phaseScoreAdjustments[response];
  if (adjustment) {
    before[phase] ??= {};
    before[phase][root.id] = (before[phase][root.id] ?? 0) + adjustment;
  }
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, before);
  session = new WorkoutSession(catalog, structuredClone(session.state), () => .4);
  session.restoreAfterReopen();
  assert.equal(session.state.outcomes[groupId], response);
  assert.equal(session.state.activeWorkoutSession.decisions.at(-1).outcome, response);
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, before);
});
for (const response of fixtures.responses) test(`explicit End preserves ${response} without inventing another response`, () => {
  const session = setup();
  const group = session.getNextGroup();
  const root = session.getSequenceRoot(session.getSelectedExercise(group));
  const phase = session.getExercisePhase(group);
  session.setPendingRestFeedback(response); session.endActiveWorkout();
  assert.equal(session.state.workoutHistory.at(-1).decisions.at(-1).outcome, response);
  assert.equal(session.state.activeWorkoutMinutes, 0);
  assert.equal(session.state.exerciseScoreAdjustmentsByPhase[phase]?.[root.id] ?? 0,
    fixtures.phaseScoreAdjustments[response]);
  const before = structuredClone(session.state.exerciseScoreAdjustmentsByPhase);
  session.endActiveWorkout();
  assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, before);
});

for (const [progress, shortEdge, travel, size, others] of fixtures.visuals)
  test(`drag growth stays bounded and reversible at ${progress}`, () => {
    const actual = feedbackVisual(progress, shortEdge);
    assert.ok(Math.abs(actual.travel - travel) < .0001);
    assert.ok(Math.abs(actual.size - size) < .0001);
    assert.ok(Math.abs(actual.others - others) < .0001);
  });
