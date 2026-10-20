import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
import test from "node:test";
import { createDefaultState, parseStoredState, WorkoutSession } from "../workout.js";

const source = (file) => readFileSync(new URL(file, import.meta.url), "utf8");

test("elevation is an optional saved capability with no catalog or profile changes", () => {
  assert.equal(createDefaultState().elevationEquipmentEnabled, false);
  for (const enabled of [true, false]) {
    const state = createDefaultState();
    state.elevationEquipmentEnabled = enabled;
    const restored = new WorkoutSession([], parseStoredState(JSON.stringify(state))).state;
    assert.equal(restored.elevationEquipmentEnabled, enabled);
    restored.elevationEquipmentEnabled = false;
    assert.deepEqual(restored, new WorkoutSession([], createDefaultState()).state);
  }
  for (const invalid of ["true", 1, null, {}, undefined]) {
    assert.equal(parseStoredState(JSON.stringify({
      ...createDefaultState(), elevationEquipmentEnabled: invalid,
    })).elevationEquipmentEnabled, false);
  }
});

function instantControls(state = createDefaultState()) {
  const elements = new Map();
  const element = (id) => {
    if (!elements.has(id)) elements.set(id, {
      textContent: id === "duration-value" ? "10" : "",
      dataset: {}, children: [], handlers: {}, attributes: {},
      style: { setProperty() {} },
      classList: { add() {}, remove() {}, toggle() {} },
      addEventListener(name, handler) { this.handlers[name] = handler; },
      setAttribute(name, value) { this.attributes[name] = value; },
      getAttribute(name) { return this.attributes[name] ?? null; },
      replaceChildren(...children) { this.children = children; },
    });
    return elements.get(id);
  };
  const context = {
    document: { getElementById: element, createElement: () => element(Symbol()) },
    window: {}, performance: { mark() {} },
    localStorage: { getItem: () => JSON.stringify(state) },
    nomadicMethodLightCadence: { workoutsRemaining: () => 18, isDue: () => false },
    setTimeout: () => 1, clearTimeout() {}, requestAnimationFrame: (callback) => callback(),
  };
  vm.runInNewContext(source("../instant-controls.js"), context);
  return { control: context.window.nomadicMethodStartupControls,
    button: element("elevation-equipment"), feedback: element("modifier-feedback") };
}

test("instant elevation toggles before startup, hands off its value, and never prepares a lineup", () => {
  const { control, button, feedback } = instantControls();
  assert.equal(button.attributes["aria-pressed"], "false");
  assert.equal(button.attributes["aria-label"], "equipment OFF: elevation");
  const modifiers = control.selectedModifiers;
  button.handlers.click();
  assert.equal(control.elevationEquipmentEnabled, true);
  assert.equal(feedback.textContent, "equipment ON: elevation");
  let preparations = 0;
  const changed = [];
  control.connect({ selectionChanged: () => preparations++,
    elevationEquipmentChanged: (enabled) => changed.push(enabled) });
  assert.equal(control.elevationEquipmentEnabled, true);
  preparations = 0;
  button.handlers.click();
  button.handlers.click();
  assert.deepEqual(changed, [false, true]);
  assert.equal(preparations, 0);
  assert.equal(control.selectedModifiers, modifiers);
  assert.equal(control.selectionChanged, false);
  assert.equal(button.attributes["title"], "equipment ON: elevation");
  control.setSelection(5, modifiers);
  assert.equal(button.attributes["aria-pressed"], "true");
});

test("instant controls restore elevation independently of the workout", () => {
  const state = { ...createDefaultState(), elevationEquipmentEnabled: true };
  const { control, button } = instantControls(state);
  assert.equal(control.elevationEquipmentEnabled, true);
  assert.equal(button.attributes["aria-label"], "equipment ON: elevation");
  button.handlers.click();
  assert.equal(button.attributes["aria-pressed"], "false");
  assert.equal(button.attributes["aria-label"], "equipment OFF: elevation");
});

test("Android and web retain the same labels and protect a background prepared workout", () => {
  const nativeState = source("../../NomadicMethod/Models/WorkoutState.cs");
  const activity = source("../../NomadicMethod/MainActivity.cs");
  const strings = source("../../NomadicMethod/Resources/values/strings.xml");
  const layout = source("../../NomadicMethod/Resources/layout/screen_duration.xml");
  const html = source("../index.html");
  const app = source("../app.js");
  assert.match(nativeState, /public bool ElevationEquipmentEnabled/);
  for (const state of ["ON", "OFF"]) assert.ok(strings.includes(`equipment ${state}: elevation`));
  assert.match(layout, /duration_equipment_modifier_group[\s\S]*elevation_equipment_button/);
  assert.match(html, /modifier-equipment-group[\s\S]*id="elevation-equipment"/);
  assert.match(activity, /nextState\.ElevationEquipmentEnabled = _state\.ElevationEquipmentEnabled/);
  assert.match(activity, /ended\.ElevationEquipmentEnabled = _state\.ElevationEquipmentEnabled/);
  assert.match(activity, /if \(_applicationStartupCompleted\) _stateStore\.Save\(_state\);\s*else _elevationEquipmentChangedDuringStartup = true/);
  assert.match(app, /nextSession\.state\.elevationEquipmentEnabled = elevationEquipmentEnabled/);
  const setter = activity.split("private void SetElevationEquipmentEnabled")[1]
    .split("private void SetSelectedWorkoutModifier")[0];
  assert.doesNotMatch(setter, /QueueWorkoutPreparation/);
});
