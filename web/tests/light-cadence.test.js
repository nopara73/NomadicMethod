import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import vm from "node:vm";

import "../light-cadence.js";

const cadence = globalThis.nomadicMethodLightCadence;
const minute = 60_000;
const light = 256;
const atDay = (day, hour = 8, minutes = 0) =>
  new Date(2026, 8, day, hour, minutes).getTime();

function completed(start, minutes, isLight = false) {
  return {
    startedAtUnixMilliseconds: start,
    endedAtUnixMilliseconds: start + minutes * minute,
    workoutMinutes: minutes,
    status: "Completed",
    isLightDay: isLight,
    modifiers: isLight ? light : 0,
    blocks: Array.from({ length: minutes }, (_, index) => ({
      completedAtUnixMilliseconds: start + (index + 1) * minute,
    })),
  };
}

for (const [duration, regularDays] of [
  [3, 60], [5, 36], [7, 26], [10, 18], [15, 12],
  [20, 9], [30, 6], [45, 4], [60, 3], [90, 3],
]) {
  test(`${duration} minutes daily reaches automatic Light after ${regularDays} regular days`, () => {
    const history = [];
    assert.equal(cadence.workoutsRemaining(history, duration, atDay(1)), regularDays);
    for (let day = 1; day <= regularDays; day += 1) {
      assert.equal(cadence.isDue(history, atDay(day)), false);
      history.push(completed(atDay(day), duration));
    }
    assert.equal(cadence.isDue(history, atDay(regularDays + 1)), true);
  });
}

test("ten 3-minute workouts daily accumulate in six days, including after reload", () => {
  const history = [];
  for (let day = 1; day <= 6; day += 1) {
    assert.equal(cadence.isDue(history, atDay(day)), false);
    for (let workout = 0; workout < 10; workout += 1) {
      history.push(completed(atDay(day, 8, workout * 10), 3));
    }
  }
  const restored = JSON.parse(JSON.stringify({
    workoutHistory: history,
    lastKeptExerciseIds: [507],
    exerciseScoreAdjustmentsByPhase: { Warmup: { 507: -2 } },
  }));
  assert.equal(restored.workoutHistory.length, 60);
  assert.equal(cadence.isDue(restored.workoutHistory, atDay(7)), true);
  assert.deepEqual(restored.lastKeptExerciseIds, [507]);
  assert.equal(restored.exerciseScoreAdjustmentsByPhase.Warmup[507], -2);
});

test("daily cap applies after summing all sessions, not separately to each", () => {
  const history = Array.from({ length: 10 }, (_, index) =>
    completed(atDay(1, 8, index * 10), 10));
  assert.equal(cadence.workoutsRemaining(history, 60, atDay(1, 11)), 2);
  assert.equal(cadence.workoutsRemaining(history, 3, atDay(1, 11)), 40);
});

test("interrupted work counts only completed blocks and all-skipped work counts zero", () => {
  const partial = { ...completed(atDay(1), 10), workoutMinutes: 90, status: "Interrupted" };
  const skipped = {
    ...completed(atDay(2), 0), workoutMinutes: 90,
    initialSelections: [{}], decisions: [{}],
  };
  assert.equal(cadence.workoutsRemaining([partial, skipped], 10, atDay(2, 10)), 17);
  assert.equal(cadence.workoutsRemaining([skipped], 10, atDay(2, 10)), 18);
});

test("brief completed Light keeps the due day locked until the next local date", () => {
  const history = [1, 2, 3].map((day) => completed(atDay(day), 60));
  history.push(completed(atDay(4), 3, true));
  assert.equal(cadence.isDue(history, atDay(4, 9)), true);
  assert.equal(cadence.isDue(history, atDay(4, 23, 59)), true);
  assert.equal(cadence.isDue(history, atDay(5)), false);
  assert.equal(cadence.workoutsRemaining(history, 30, atDay(5)), 6);
});

for (const [planned, done, credited] of [
  [3, 2, 2], [5, 4, 4], [7, 6, 7], [10, 8, 8], [10, 9, 10],
  [20, 16, 16], [20, 17, 20], [60, 50, 50], [60, 51, 60],
  [60, 53, 60], [60, 55, 60], [60, 56, 60], [90, 59, 59], [90, 77, 60],
]) {
  test(`${done}/${planned} completed regular blocks receive ${credited} cadence minutes`, () => {
    const session = { ...completed(atDay(1), done), workoutMinutes: planned };
    assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 10)), 180 - credited);
  });
}

function nearlyCompletedHourHistory() {
  return [53, 56, 55].map((count, index) => ({
    ...completed(atDay(index + 1), count), workoutMinutes: 60,
  }));
}

test("three nearly completed hours become due from existing history without rewriting data", () => {
  const saved = JSON.stringify({
    workoutHistory: nearlyCompletedHourHistory(),
    lastKeptExerciseIds: [507],
    exerciseScoreAdjustmentsByPhase: { Warmup: { 507: -2 } },
    lastHardWorkUnixMillisecondsByPrimaryMuscle: { AbdominalWall: atDay(1) },
  });
  const restored = JSON.parse(saved);
  assert.equal(cadence.isDue(restored.workoutHistory, atDay(3, 10)), true);
  assert.equal(cadence.workoutsRemaining(restored.workoutHistory, 60, atDay(3, 10)), 0);
  assert.equal(JSON.stringify(restored), saved);
});

test("interrupted and in-progress nearly completed sessions keep only actual credit", () => {
  for (const status of ["Interrupted", "InProgress"]) {
    const session = { ...completed(atDay(1), 55), workoutMinutes: 60, status };
    assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 10)), 125);
  }
});

test("mixed-Light work never rounds up while physical modifier changes can", () => {
  const session = {
    ...completed(atDay(1), 55), workoutMinutes: 60,
    modifierChanges: [
      { changedAtUnixMilliseconds: atDay(1, 8, 5), newModifiers: light },
      { changedAtUnixMilliseconds: atDay(1, 8, 6), newModifiers: 32 },
    ],
  };
  assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 10)), 126);
  session.modifierChanges[0].newModifiers = 32;
  assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 10)), 120);
  session.isLightDay = true;
  session.modifierChanges[0].changedAtUnixMilliseconds = atDay(1);
  assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 10)), 125);
});

test("completion credit waits for the recorded end and remains within the daily cap", () => {
  const session = {
    ...completed(atDay(1), 55), workoutMinutes: 60, endedAtUnixMilliseconds: atDay(1, 9),
  };
  assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 8, 56)), 125);
  assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 9)), 120);
  const another = { ...completed(atDay(1, 10), 55), workoutMinutes: 60 };
  assert.equal(cadence.workoutsRemaining([session, another], 1, atDay(1, 12)), 120);
});

test("midnight top-up does not move actual work into the completion date", () => {
  const earlier = completed(atDay(1), 60);
  const session = { ...completed(atDay(1, 23, 30), 53), workoutMinutes: 60 };
  assert.equal(cadence.workoutsRemaining([earlier, session], 1, atDay(2, 2)), 89);
});

test("Light after reaching the threshold today resets tomorrow, not immediately", () => {
  const history = [1, 2, 3].map((day) => completed(atDay(day), 60));
  history.push(completed(atDay(3, 10), 3, true));
  assert.equal(cadence.isDue(history, atDay(3, 11)), true);
  assert.equal(cadence.isDue(history, atDay(4)), false);
});

test("an overnight gap preserves accumulated work but a complete rest date resets it", () => {
  const history = [1, 2, 3].map((day) => completed(atDay(day), 60));
  assert.equal(cadence.isDue(history, atDay(4)), true);
  assert.equal(cadence.isDue(history, atDay(5)), false);
  assert.equal(cadence.workoutsRemaining(history, 3, atDay(5)), 60);
});

test("mode changes classify each completed block without rewriting earlier work", () => {
  const session = {
    ...completed(atDay(1), 30), status: "Interrupted",
    modifierChanges: [{
      changedAtUnixMilliseconds: atDay(1, 8, 10), newModifiers: light,
    }],
  };
  assert.equal(cadence.workoutsRemaining([session], 1, atDay(1, 9)), 171);
});

test("completed blocks crossing midnight count toward their own local date's cap", () => {
  const session = completed(atDay(1, 23, 30), 90);
  // 29 blocks before midnight, 61 after: 29 + capped 60 = 89 credited minutes.
  assert.equal(cadence.workoutsRemaining([session], 1, atDay(2, 2)), 91);
});

test("legacy histories remain readable without double-counting inferred dates", () => {
  const old = { ...completed(atDay(1), 30), blocks: [] };
  assert.equal(cadence.workoutsRemaining([old], 30, atDay(2)), 5);
  assert.equal(cadence.workoutsRemaining(
    [completed(atDay(2), 60)], 60, atDay(3), [atDay(1)],
  ), 1);
  assert.equal(cadence.workoutsRemaining([old], 30, atDay(2), [atDay(1)]), 5);
});

const instantSource = await readFile(new URL("../instant-controls.js", import.meta.url), "utf8");
const cadenceSource = await readFile(new URL("../light-cadence.js", import.meta.url), "utf8");

// Exercise the real early-loading controls with a minimal DOM. This runs before
// the catalog/module loads, when a stale or bypassable toggle would be visible.
function startup(history = [], duration = 10, now = atDay(4)) {
  const elements = new Map();
  const timers = new Map();
  let timerId = 0;
  function element(id) {
    if (elements.has(id)) return elements.get(id);
    const attributes = new Map();
    const listeners = new Map();
    const classes = new Set();
    const node = {
      textContent: id === "duration-value" ? "10" : "",
      children: [], dataset: {}, hidden: false, disabled: false,
      style: { setProperty() {} },
      classList: {
        add(name) { classes.add(name); },
        remove(name) { classes.delete(name); },
        toggle() {}, contains(name) { return classes.has(name); },
      },
      setAttribute(name, value) { attributes.set(name, value); },
      getAttribute(name) { return attributes.get(name); },
      addEventListener(name, callback) { listeners.set(name, callback); },
      fire(name) { listeners.get(name)?.(); },
    };
    elements.set(id, node);
    return node;
  }
  class ClockDate extends Date { static now() { return now; } }
  const context = vm.createContext({
    Date: ClockDate,
    document: { getElementById: element },
    localStorage: { getItem: () => JSON.stringify({
      workoutHistory: history, lastWorkoutMinutes: duration,
      lastWorkoutModifiers: 0,
    }) },
    performance: { mark() {} },
    requestAnimationFrame: (callback) => callback(),
    setTimeout(callback) { timers.set(++timerId, callback); return timerId; },
    clearTimeout(id) { timers.delete(id); },
  });
  context.window = context;
  vm.runInContext(cadenceSource, context);
  vm.runInContext(instantSource, context);
  return { controls: context.nomadicMethodStartupControls, element, timers };
}

function assertLockedTile(element) {
  const tile = element("light-workout-modifier");
  assert.equal(tile.disabled, false, "locked tiles must still receive activation");
  assert.equal(tile.getAttribute("aria-disabled"), "true");
  assert.equal(tile.getAttribute("aria-pressed"), "true");
  assert.equal(tile.getAttribute("title"), "rest, you must");
  assert.equal(element("light-workout-countdown").hidden, true);
}

test("startup locks due Light before module loading and never shows a zero badge", () => {
  const history = [1, 2, 3].map((day) => completed(atDay(day), 60));
  const { controls, element } = startup(history);
  assert.equal(controls.automaticLightMode, true);
  assert.equal(controls.selectedModifiers & light, light);
  assert.equal(element("light-workout-modifier").getAttribute("aria-pressed"), "true");
  assertLockedTile(element);
  assert.equal(element("light-workout-countdown").hidden, true);
  element("light-workout-modifier").fire("click");
  assert.equal(controls.selectedModifiers & light, light);
  controls.setActiveWorkoutSetup(true);
  element("light-workout-modifier").fire("click");
  assert.equal(controls.selectedModifiers & light, light);
});

test("startup locks Light for the existing nearly completed hour history", () => {
  const { controls, element } = startup(nearlyCompletedHourHistory(), 60, atDay(3, 10));
  assert.equal(controls.automaticLightMode, true);
  assertLockedTile(element);
  assert.equal(element("light-workout-countdown").hidden, true);
  element("light-workout-modifier").fire("click");
  assert.equal(controls.selectedModifiers & light, light);
  controls.setActiveWorkoutSetup(true);
  element("light-workout-modifier").fire("click");
  assert.equal(controls.selectedModifiers & light, light);
});

test("mid-workout settings allow duration edits without crossing the protected prefix", () => {
  const { controls, element } = startup([], 30);
  controls.setActiveWorkoutSetup(true, 15);
  assert.equal(element("duration-range").disabled, false);
  element("duration-decrease").fire("click");
  assert.equal(controls.selectedMinutes, 20);
  element("duration-decrease").fire("click");
  assert.equal(controls.selectedMinutes, 15);
  assert.equal(element("duration-decrease").disabled, true);
  element("duration-increase").fire("click");
  assert.equal(controls.selectedMinutes, 20);
  assert.equal(element("begin-workout").getAttribute("aria-label"), "Resume workout");
});

test("startup countdown responds to duration and manual Light hides it", () => {
  const { controls, element } = startup([], 10);
  assert.equal(element("light-workout-countdown").textContent, "18");
  element("duration-range").value = "0";
  element("duration-range").fire("input");
  assert.equal(controls.lightWorkoutsRemaining, 60);
  assert.equal(element("light-workout-countdown").textContent, "60");
  assert.equal(element("light-workout-countdown").hidden, false);
  element("light-workout-modifier").fire("click");
  assert.equal(element("light-workout-countdown").hidden, true);
  assert.equal(element("light-workout-modifier").disabled, false);
  element("light-workout-modifier").fire("click");
  assert.equal(element("light-workout-countdown").hidden, false);
});

test("recovery presentation never sets real Light or consumes a cadence reset", () => {
  const { controls, element } = startup();
  controls.setRecoveryLightMode(true);
  assert.equal(controls.selectedModifiers & light, 0);
  assertLockedTile(element);
  assert.equal(element("light-workout-modifier").getAttribute("aria-pressed"), "true");
  assert.equal(element("light-workout-countdown").hidden, true);
  controls.setRecoveryLightMode(false);
  assert.equal(controls.selectedModifiers & light, 0);
  assert.equal(element("light-workout-modifier").disabled, false);
  assert.equal(element("light-workout-countdown").hidden, false);
});

test("date rollover clears the expired automatic toggle without persisting it", () => {
  const { controls, element } = startup(
    [1, 2, 3].map((day) => completed(atDay(day), 60)),
  );
  controls.setAutomaticLightMode(false);
  controls.setLightWorkoutsRemaining(18);
  assert.equal(controls.selectedModifiers & light, 0);
  assert.equal(element("light-workout-modifier").disabled, false);
  assert.equal(element("light-workout-countdown").hidden, false);
});

for (const reason of ["cadence", "recovery", "both"]) {
  for (const editing of [false, true]) {
    test(`${reason}-locked Light explains repeated taps during ${editing ? "workout setup" : "startup"} without changing state`, () => {
      const { controls, element, timers } = startup(
        reason === "recovery" ? [] : nearlyCompletedHourHistory(),
      );
      controls.setRecoveryLightMode(reason !== "cadence");
      controls.setActiveWorkoutSetup(editing);
      const selections = [];
      controls.connect({ selectionChanged: (...args) => selections.push(args) });
      const before = controls.selectedModifiers;
      const notificationsBefore = selections.length;
      for (let tap = 0; tap < 3; tap += 1) {
        assertLockedTile(element);
        element("light-workout-modifier").fire("click");
        assert.equal(controls.selectedModifiers, before);
        assert.equal(controls.selectionChanged, false);
        assert.equal(selections.length, notificationsBefore);
        assert.equal(controls.startQueued, false);
        assert.equal(element("modifier-feedback").textContent, "rest, you must");
        assert.equal(element("modifier-feedback").getAttribute("data-detail"),
          reason === "recovery" ? "Muscles recovering" : "Training cycle complete");
        assert.equal(element("modifier-feedback").hidden, false);
        assert.equal(element("modifier-feedback").classList.contains("show"), true);
        assert.equal(timers.size, 1, "repeat taps restart, not stack, the animation");
      }
      timers.values().next().value();
      assert.equal(element("modifier-feedback").hidden, true);
      assert.equal(element("modifier-feedback").classList.contains("show"), false);
      assertLockedTile(element);
    });
  }
}

test("manual Light still toggles with ordinary ON/OFF feedback after recovery ends", () => {
  const { controls, element } = startup();
  controls.setRecoveryLightMode(true);
  element("light-workout-modifier").fire("click");
  controls.setRecoveryLightMode(false);
  assert.equal(element("light-workout-modifier").getAttribute("aria-disabled"), "false");
  element("light-workout-modifier").fire("click");
  assert.equal(element("modifier-feedback").textContent, "light mode ON");
  assert.equal(element("modifier-feedback").getAttribute("data-detail"), "");
  assert.equal(controls.selectedModifiers & light, light);
  element("light-workout-modifier").fire("click");
  assert.equal(element("modifier-feedback").textContent, "light mode OFF");
  assert.equal(controls.selectedModifiers & light, 0);
});

const appSource = await readFile(new URL("../app.js", import.meta.url), "utf8");
const toggleSource = appSource.slice(
  appSource.indexOf("function toggleWorkoutModifier("),
  appSource.indexOf("function workoutModifierFeedbackLabel("),
);
for (const [automatic, recovery] of [[true, false], [false, true], [true, true], [false, false]]) {
  test(`loaded module Light activation honors cadence=${automatic}, recovery=${recovery}`, () => {
    const messages = [];
    let preparations = 0;
    const context = vm.createContext({
      WORKOUT_MODIFIERS: { Light: light },
      MODIFIER_FEEDBACK_LABELS: { lightLocked: "rest, you must" },
      selectedModifiers: automatic ? light : 0,
      session: {},
      isRecoveryLightModeActive: () => recovery,
      isAutomaticLightModeLocked: () => automatic,
      renderWorkoutModifiers() {},
      showWorkoutModifierFeedback: (message, detail = "") => messages.push([message, detail]),
      workoutModifierFeedbackLabel: (_, enabled) => `light mode ${enabled ? "ON" : "OFF"}`,
      queueWorkoutPreparation: () => preparations += 1,
    });
    vm.runInContext(toggleSource, context);
    const before = context.selectedModifiers;
    vm.runInContext("toggleWorkoutModifier(WORKOUT_MODIFIERS.Light)", context);
    if (automatic || recovery) {
      assert.equal(context.selectedModifiers, before);
      assert.equal(preparations, 0);
      assert.deepEqual(messages, [["rest, you must", automatic ? "Training cycle complete" : "Muscles recovering"]]);
    } else {
      assert.equal(context.selectedModifiers & light, light);
      assert.equal(preparations, 1);
      assert.deepEqual(messages, [["light mode ON", ""]]);
    }
  });
}
