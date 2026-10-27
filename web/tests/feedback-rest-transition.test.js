import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import vm from "node:vm";

const source = await readFile(new URL("../app.js", import.meta.url), "utf8");
const functions = ["startRestTimer", "updateRest"].map(name =>
  source.match(new RegExp(`function ${name}\\(\\) \\{[\\s\\S]*?\\n\\}`))[0]).join("\n");

function restScreen(response, paused = true) {
  let timers = 0, confirmations = 0, completed = 0, cleared = 0;
  const control = () => ({ style: {}, disabled: false });
  const elements = {
    restCountdown: control(), restPlaybackToggle: control(),
    restProgressFill: { style: {}, parentElement: control() },
  };
  const session = {
    state: { pendingRestPausedByUser: paused },
    getPendingRestFeedback: () => response,
    getPendingRestMillisecondsRemaining: () => 8941,
  };
  const context = vm.createContext({ elements, session, restActive: true, restTimer: 7,
    feedbackView: { active: true, finishSelection: () => confirmations++ },
    REST_DURATION_MS: 15000, TIMER_INTERVAL_MS: 250,
    clearInterval: () => cleared++, setInterval: () => ++timers,
    completeRest: () => completed++, Date,
  });
  vm.runInContext(functions, context);
  return { context, elements, session, counts: () => ({ timers, confirmations, completed, cleared }) };
}

for (const response of ["tick", "x", "easy", "hard"]) {
  for (const paused of [true, false]) {
    test(`${response}: restored acceptance confirms without a rest timer (paused=${paused})`, () => {
      const screen = restScreen(response, paused);
      vm.runInContext("startRestTimer()", screen.context);
      assert.deepEqual(screen.counts(), { timers: 0, confirmations: 1, completed: 0, cleared: 1 });
      assert.equal(screen.elements.restPlaybackToggle.disabled, true);
      for (const control of [screen.elements.restCountdown, screen.elements.restPlaybackToggle,
        screen.elements.restProgressFill.parentElement]) assert.equal(control.style.visibility, "hidden");
      // A queued old timer tick must not restore the outgoing countdown.
      vm.runInContext("updateRest()", screen.context);
      assert.equal(screen.elements.restCountdown.textContent, undefined);
      assert.equal(screen.counts().completed, 0);
    });
  }
}

test("ordinary rest retains pause and countdown; the next rest restores its controls", () => {
  const screen = restScreen("tick");
  vm.runInContext("startRestTimer()", screen.context);
  screen.session.getPendingRestFeedback = () => null;
  screen.session.state.pendingRestPausedByUser = false;
  vm.runInContext("startRestTimer()", screen.context);
  assert.equal(screen.counts().timers, 1);
  assert.equal(screen.elements.restPlaybackToggle.disabled, false);
  assert.equal(screen.elements.restPlaybackToggle.style.visibility, "");
  assert.equal(screen.elements.restCountdown.textContent, "9 seconds to meh");
});

test("Android also resumes accepted confirmation before creating a rest timer", async () => {
  const android = await readFile(new URL("../../NomadicMethod/MainActivity.cs", import.meta.url), "utf8");
  const resume = android.slice(android.indexOf("private void ResumeRestCountdown()"),
    android.indexOf("private void UpdateRestCountdownText()"));
  assert.ok(resume.indexOf("AnimateFeedbackSelection(response)") >= 0);
  assert.ok(resume.indexOf("AnimateFeedbackSelection(response)") < resume.indexOf("new WorkoutCountDownTimer"));
  assert.match(resume, /AnimateFeedbackSelection\(response\);\s*return;/);
  assert.match(android, /_restPlaybackAction\.Enabled = !confirming;\s*if \(confirming\) return;/);
});
