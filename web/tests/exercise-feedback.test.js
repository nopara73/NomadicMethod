import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { feedbackDirection, feedbackThreshold, feedbackDistance, feedbackSymbols, feedbackVisual, ExerciseFeedbackView } from "../exercise-feedback.js";
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M } from "../workout.js";

const fixtures = JSON.parse(await readFile(new URL("./fixtures/exercise-feedback-cases.json", import.meta.url)));
const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url)));
test("direction emoji agree with Android's shared feedback vocabulary", () => assert.deepEqual(feedbackSymbols, fixtures.symbols));

// A controllable animation clock checks the real view's completion/lifecycle
// contract independently of the workout timer or a browser's frame rate.
function transitionView(reduced = false) {
  const animations = [], choices = [], completions = [];
  const animate = frames => {
    let resolve, reject;
    const finished = new Promise((yes, no) => { resolve = yes; reject = no; });
    const animation = { frames, finished, finish: resolve, cancel: () => reject(new Error("interrupted")) };
    animations.push(animation); return animation;
  };
  const view = Object.assign(Object.create(ExerciseFeedbackView.prototype), {
    epoch: 0, active: true, selected: null, busy: false,
    hints: { querySelector: () => ({ getBoundingClientRect: () => ({ x: 0, y: 200, width: 80, height: 80 }) }) },
    card: { style: {} }, footer: { style: {} }, burst: { hidden: true, animate }, burstEmoji: { animate },
    reduced: () => reduced, preview: () => {}, animate: async () => {}, cancelGesture: () => {}, cancelAnimation: () => {},
    render(value) { this.selected = value; },
    hooks: { choose: value => { choices.push(value); return true; }, completed: () => completions.push(true) }
  });
  return { view, choices, completions, animations, finish: () => animations.forEach(animation => animation.finish()) };
}
for (const [width, height] of [[320,568], [390,844], [844,390], [768,1024]])
  test(`confirmation fills the shorter screen dimension at ${width} x ${height}`, async () => {
    globalThis.document = { hidden: false }; globalThis.innerWidth = width; globalThis.innerHeight = height;
    const { view, animations, finish } = transitionView();
    const pending = view.commit("hard");
    const frames = animations[0].frames;
    const scale = Number(frames.at(-1).transform.match(/scale\(([^)]+)\)/)[1]);
    assert.ok(Math.abs(scale * 256 - Math.min(width, height) * .9) < .001);
    assert.ok(animations[0].frames.some(frame => frame.opacity === .82), "one soft confirmation pulse");
    finish(); await pending;
  });

test("screen gestures leave interactive controls untouched", () => {
  const view = Object.assign(Object.create(ExerciseFeedbackView.prototype), { active: true });
  for (const control of ["button", "a", "input", "select", "textarea", "[role=button]", "dialog", "[contenteditable=true]"]) {
    view.down({ isPrimary: true, button: 0, target: { closest: selector => selector.includes(control) },
      preventDefault: () => assert.fail("a control tap must not become feedback") });
    assert.equal(view.gesture, undefined);
  }
});
for (const response of fixtures.responses) test(`${response} advances exactly once when its animation ends, without waiting for rest`, async () => {
  globalThis.document = { hidden: false }; globalThis.innerWidth = 390; globalThis.innerHeight = 844;
  const { view, choices, completions, finish } = transitionView();
  const pending = view.commit(response);
  await view.commit("x");
  assert.deepEqual(choices, [response]); assert.equal(completions.length, 0);
  finish(); await pending;
  assert.equal(completions.length, 1); assert.equal(view.burst.hidden, true);
});
test("interrupted feedback retains its response and resumes once without recording twice", async () => {
  globalThis.document = { hidden: false }; globalThis.innerWidth = 390; globalThis.innerHeight = 844;
  const { view, choices, completions, finish } = transitionView();
  const pending = view.commit("hard"); view.interrupt(); await pending;
  assert.equal(completions.length, 0); assert.equal(view.selected, "hard"); assert.equal(view.burst.hidden, true);
  const resumed = view.finishSelection(); finish(); await resumed;
  assert.deepEqual(choices, ["hard"]); assert.equal(completions.length, 1);
});
test("reduced motion skips the zoom and advances immediately", async () => {
  globalThis.document = { hidden: false }; globalThis.innerWidth = 390; globalThis.innerHeight = 844;
  const { view, completions } = transitionView(true);
  await view.commit("easy"); assert.equal(completions.length, 1); assert.equal(view.burst.hidden, true);
});
for (const trace of fixtures.traces) test(trace.name, () => {
  let direction = null;
  for (const [x, y, expected] of trace.points) {
    direction = feedbackDirection(x, y, direction);
    assert.equal(direction, expected);
  }
});
test("commit threshold scales within thumb-sized bounds", () => {
  for (const [width, expected] of fixtures.thresholds) assert.equal(feedbackThreshold(width), expected);
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
  session.recordOutcome(session.getNextGroup(), response); session.clearPendingRest();
  assert.equal(session.state.activeWorkoutSession.decisions.at(-1).outcome, response);
  if (response !== "x") assert.deepEqual(session.state.exerciseScoreAdjustmentsByPhase, before);
  else assert.notDeepEqual(session.state.exerciseScoreAdjustmentsByPhase, before);
  session = new WorkoutSession(catalog, structuredClone(session.state), () => .4);
  session.restoreAfterReopen();
  assert.equal(session.state.outcomes[groupId], response);
  assert.equal(session.state.activeWorkoutSession.decisions.at(-1).outcome, response);
});
for (const response of fixtures.responses) test(`explicit End preserves ${response} without inventing another response`, () => {
  const session = setup(); session.setPendingRestFeedback(response); session.endActiveWorkout();
  assert.equal(session.state.workoutHistory.at(-1).decisions.at(-1).outcome, response);
  assert.equal(session.state.activeWorkoutMinutes, 0);
});

for (const [progress, shortEdge, travel, size, others] of fixtures.visuals)
  test(`drag growth stays bounded and reversible at ${progress}`, () => {
    const actual = feedbackVisual(progress, shortEdge);
    assert.ok(Math.abs(actual.travel - travel) < .0001);
    assert.ok(Math.abs(actual.size - size) < .0001);
    assert.ok(Math.abs(actual.others - others) < .0001);
  });
