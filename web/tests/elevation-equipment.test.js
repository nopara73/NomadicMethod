import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import vm from "node:vm";
import test from "node:test";
import {
  createDefaultState, parseStoredState, getElevationEquipment, withElevationEquipment,
  ELEVATION_EQUIPMENT, WORKOUT_MODIFIERS, isCompatibleWithWorkoutModifiers,
  normalizeWorkoutModifiers, findElevationCatalogDeficiencies, getWorkoutCatalog,
} from "../workout.js";
const source = file => readFileSync(new URL(file, import.meta.url), "utf8");
const fixture = JSON.parse(source("../../NomadicMethod/Assets/exercises.json")).find(e => e.id === 701);
const movement = (id, extra = {}) => ({...fixture, id, sessionMovementId: id,
  elevationRequired: true, jumpPlatformRequired: false, wallRequired: false, soleWallContactRequired: false, ...extra});

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
  vm.runInNewContext(source("../workout-duration.js"), context);
  vm.runInNewContext(source("../instant-controls.js"), context);
  return { control: context.window.nomadicMethodStartupControls,
    button: element("elevation-equipment"), feedback: element("modifier-feedback") };
}


test("elevation cycles Off, Support, Platform and hands the actual profile to startup", () => {
  const {control, button, feedback} = instantControls();
  const baseline = control.selectedModifiers;
  assert.equal(button.attributes["aria-pressed"], "false");
  button.handlers.click();
  assert.equal(getElevationEquipment(control.selectedModifiers), ELEVATION_EQUIPMENT.Support);
  assert.equal(feedback.textContent, "equipment ON: support");
  const changes = [];
  control.connect({selectionChanged: (selection, user) => {
    if (user) changes.push(selection.selectedModifiers);
  }});
  button.handlers.click();
  assert.equal(getElevationEquipment(control.selectedModifiers), ELEVATION_EQUIPMENT.JumpPlatform);
  assert.equal(button.dataset.elevationEquipment, "platform");
  assert.equal(feedback.textContent, "equipment ON: jump platform");
  button.handlers.click();
  assert.equal(control.selectedModifiers, baseline);
  assert.equal(button.dataset.elevationEquipment, "none");
  assert.equal(button.attributes["aria-pressed"], "false");
  assert.equal(feedback.textContent, "equipment OFF: elevation");
  assert.equal(button.attributes["aria-label"], "equipment OFF: elevation");
  assert.equal(changes.length, 2);
  assert.equal(control.selectionChanged, true);
  control.setSelection(5, withElevationEquipment(baseline, ELEVATION_EQUIPMENT.JumpPlatform));
  assert.equal(button.dataset.elevationEquipment, "platform");
});

test("each elevation capability has the same distinct feedback on Android, startup and the loaded web app", () => {
  const strings = source("../../NomadicMethod/Resources/values/strings.xml");
  const android = source("../../NomadicMethod/MainActivity.cs");
  const feedbackFunction = source("../app.js").match(/function elevationFeedbackLabel\(\) \{[\s\S]*?\n\}/)[0];
  const {control, button, feedback} = instantControls();
  for (const [equipment, resource, label] of [
    [ELEVATION_EQUIPMENT.Support, "elevation_support_enabled_feedback", "equipment ON: support"],
    [ELEVATION_EQUIPMENT.JumpPlatform, "elevation_platform_enabled_feedback", "equipment ON: jump platform"],
    [ELEVATION_EQUIPMENT.None, "elevation_equipment_disabled_feedback", "equipment OFF: elevation"],
  ]) {
    button.handlers.click();
    assert.equal(getElevationEquipment(control.selectedModifiers), equipment);
    assert.equal(feedback.textContent, label);
    assert.equal(vm.runInNewContext(`${feedbackFunction}; elevationFeedbackLabel()`, {
      selectedModifiers: control.selectedModifiers, getElevationEquipment, ELEVATION_EQUIPMENT,
    }), label);
    assert.equal(strings.match(new RegExp(`<string name="${resource}">([^<]+)</string>`))[1], label);
    assert.ok(android.includes(`Resource.String.${resource}`), `Android must present ${equipment}`);
  }
});

test("legacy ON restores as Support without changing active checkpoints or history", () => {
  const raw = {...createDefaultState(), elevationEquipmentEnabled: true,
    activeWorkoutModifiers: WORKOUT_MODIFIERS.Wall,
    pendingMovementGroupId: "r10.upper-limbs", pendingMovementMillisecondsRemaining: 12345};
  const restored = parseStoredState(JSON.stringify(raw));
  assert.equal(getElevationEquipment(restored.lastWorkoutModifiers), ELEVATION_EQUIPMENT.Support);
  assert.equal(restored.elevationEquipmentEnabled, false);
  assert.equal(restored.activeWorkoutModifiers, WORKOUT_MODIFIERS.Wall);
  assert.equal(restored.pendingMovementMillisecondsRemaining, 12345);
  assert.deepEqual(parseStoredState(JSON.stringify(restored)), restored);
  const {control, button} = instantControls(raw);
  assert.equal(getElevationEquipment(control.selectedModifiers), ELEVATION_EQUIPMENT.Support);
  assert.equal(button.dataset.elevationEquipment, "support");
  for (const invalid of ["true", 1, null, {}, undefined])
    assert.equal(getElevationEquipment(parseStoredState(JSON.stringify({elevationEquipmentEnabled: invalid})).lastWorkoutModifiers), ELEVATION_EQUIPMENT.None);
});

test("elevation admits nested equipment capabilities and keeps Floor independent", () => {
  const ordinary = movement(1, {elevationRequired: false});
  const support = movement(2);
  const platform = movement(3, {jumpPlatformRequired: true});
  const floor = movement(4, {onlyFeetTouchGround: false, floorContactPoints: "Hands"});
  for (const [equipment, expectedSupport, expectedPlatform] of [
    [ELEVATION_EQUIPMENT.None, false, false],
    [ELEVATION_EQUIPMENT.Support, true, false],
    [ELEVATION_EQUIPMENT.JumpPlatform, true, true],
  ]) {
    const profile = withElevationEquipment(WORKOUT_MODIFIERS.UpperBodyClothing, equipment);
    assert.equal(isCompatibleWithWorkoutModifiers(ordinary, profile), true);
    assert.equal(isCompatibleWithWorkoutModifiers(support, profile), expectedSupport);
    assert.equal(isCompatibleWithWorkoutModifiers(platform, profile), expectedPlatform);
    assert.equal(isCompatibleWithWorkoutModifiers(floor, profile), false);
    assert.equal(isCompatibleWithWorkoutModifiers(floor, profile | WORKOUT_MODIFIERS.Floor), expectedSupport);
  }
  assert.equal(normalizeWorkoutModifiers(WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.JumpPlatform), WORKOUT_MODIFIERS.Silence);
});

test("20 support, 5 platform and 5 floor identities cannot be filled with aliases", () => {
  const catalog = Array.from({length: 19}, (_, i) => movement(i + 1));
  catalog.push(...Array.from({length: 5}, (_, i) => movement(20 + i, {jumpPlatformRequired: true})));
  catalog.push(...Array.from({length: 5}, (_, i) => movement(25 + i, {onlyFeetTouchGround: false, floorContactPoints: "Hands"})));
  assert.equal(findElevationCatalogDeficiencies(catalog)[0].pool, "Support");
  catalog.push(movement(30, {sessionMovementId: 1}));
  assert.equal(findElevationCatalogDeficiencies(catalog).length, 1);
  catalog.push(movement(31));
  assert.deepEqual(findElevationCatalogDeficiencies(catalog), []);
  catalog.splice(catalog.findIndex(e => e.id === 29), 1);
  catalog.push(movement(32, {sessionMovementId: 1, onlyFeetTouchGround: false, floorContactPoints: "Hands"}));
  assert.equal(findElevationCatalogDeficiencies(catalog)[0].pool, "FloorAndElevation");
});

test("old unfinished workouts retain their original catalog", () => {
  const catalog = [movement(1, {elevationRequired: false}), movement(2)];
  assert.equal(getWorkoutCatalog(catalog, 82).length, 1);
  assert.equal(getWorkoutCatalog(catalog, 83).length, 1);
  assert.equal(getWorkoutCatalog(catalog, 84).length, 2);
});

test("reviewed catalog retains exact demos and independent floor and platform requirements", () => {
  const catalog = JSON.parse(source("../../NomadicMethod/Assets/exercises.json"));
  const review = JSON.parse(source("./fixtures/elevation-admission.json"));
  assert.equal(catalog.filter(e => e.elevationRequired).length, 35);
  assert.deepEqual(findElevationCatalogDeficiencies(catalog), []);
  assert.ok(catalog.filter(e => e.wallRequired && !e.onlyFeetTouchGround).length >= 5);
  for (const item of review) {
    const exercise = catalog.find(e => e.id === item.id);
    assert.deepEqual({name: exercise.name, primary: exercise.primaryCanonicalGroup,
      side: exercise.sideSequence, floor: !exercise.onlyFeetTouchGround,
      platform: exercise.jumpPlatformRequired, hold: exercise.mode === "Hold"},
    {name: item.name, primary: item.primary, side: item.side, floor: item.floor,
      platform: item.platform, hold: item.hold});
    const base = WORKOUT_MODIFIERS.UpperBodyClothing | WORKOUT_MODIFIERS.Floor;
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, base), false);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, base | WORKOUT_MODIFIERS.Elevation), !item.platform);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, base | WORKOUT_MODIFIERS.Elevation | WORKOUT_MODIFIERS.JumpPlatform), true);
  }
});
