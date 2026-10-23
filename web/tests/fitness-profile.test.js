import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { getWorkoutCatalog, isCompatibleWithWorkoutModifiers, WORKOUT_MODIFIERS, WorkoutSession, createDefaultState } from "../workout.js";
import { FITNESS_DIMENSIONS, FITNESS_MAXIMUM_LEVEL, FITNESS_WEB_BOUNDARY_LEVEL,
  calculateFitnessProfile, parseFitnessRatings, fitnessProfileRadiusFraction, roundFitnessLevel } from "../fitness-profile.js";

const read = file => readFileSync(new URL(file, import.meta.url), "utf8");
const fixture = JSON.parse(read("./fixtures/fitness-profile-cases.json"));

for (const fixtureName of ["fitness-profile-admissions.json", "fitness-precise-admissions.json", "fitness-ability-admissions.json"])
test("reviewed catalog admissions contribute precise tiers and preserve the older workout catalog: " + fixtureName, () => {
  const admissions = JSON.parse(read("./fixtures/" + fixtureName));
  const catalog = JSON.parse(read("../../NomadicMethod/Assets/exercises.json"));
  const ratings = parseFitnessRatings(JSON.parse(read("../../NomadicMethod/Assets/fitness_ratings.json")));
  for (const entry of admissions.cases) {
    const state = { lastKeptExerciseIds: entry.kept };
    const before = JSON.stringify(state);
    assert.deepEqual(calculateFitnessProfile(catalog, state, ratings), entry.expected, entry.name);
    assert.equal(JSON.stringify(state), before);
  }
  assert.ok(getWorkoutCatalog(catalog, admissions.catalogRevision - 1).every(e => !admissions.exerciseIds.includes(e.id)));
  assert.ok(admissions.exerciseIds.every(id => getWorkoutCatalog(catalog, admissions.catalogRevision).some(e => e.id === id)));
  for (const entry of admissions.compatibilityCases ?? []) {
    const exercise = catalog.find(e => e.id === entry.id);
    const modifiers = entry.modifiers.reduce((mask, name) => mask | WORKOUT_MODIFIERS[name], 0);
    assert.equal(isCompatibleWithWorkoutModifiers(exercise, modifiers), entry.allowed, entry.name);
  }
  for (const entry of admissions.planningCases ?? []) {
    const state = createDefaultState();
    state.keptExerciseRootIdsBySelectionGroupId[entry.keepSlot] = [entry.id];
    const session = new WorkoutSession(catalog, state, () => 0.42, () => Date.UTC(2026, 9, 23), ratings);
    const modifiers = entry.modifiers.reduce((mask, name) => mask | WORKOUT_MODIFIERS[name], 0);
    session.startWorkout(entry.minutes, modifiers);
    assert.equal(session.getActiveGroups().length, entry.minutes);
    assert.ok(session.getActiveGroups().some(group => session.getSelectedExercise(group).id === entry.id),
      `Admission ${entry.id} must fit a complete short workout`);
  }
  assert.equal(catalog.find(e => e.id === 1621).sessionMovementId, 266);
  assert.equal(catalog.find(e => e.id === 1701).sequenceBlocks.length, 2);
});
test("raw profile fractions remain precise and expert level extends beyond the web", () => {
  assert.equal(FITNESS_MAXIMUM_LEVEL, fixture.chartScale.maximumLevel);
  assert.equal(FITNESS_WEB_BOUNDARY_LEVEL, fixture.chartScale.webBoundaryLevel);
  for (const entry of fixture.chartScale.cases)
    assert.ok(Math.abs(fitnessProfileRadiusFraction(entry.level) - entry.radiusFraction) < 1e-12);
  assert.ok(fitnessProfileRadiusFraction(5) > fitnessProfileRadiusFraction(4));
});

test("display and matching share half-up rounding while raw profiles remain fractional", () => {
  const cases = JSON.parse(read("./fixtures/fitness-selection-cases.json"));
  for (const entry of cases.rounding)
    assert.equal(roundFitnessLevel(entry.level), entry.rounded);
  const fractional = fixture.cases.find(entry => entry.expected.some(value => value % 1));
  assert.ok(fractional);
  assert.ok(fractional.expected.some(value => value !== roundFitnessLevel(value)));
});

test("native and web use the app palette and fixed-size labels for the larger spiderweb", () => {
  const native = read("../../NomadicMethod/FitnessProfileView.cs");
  const colors = read("../../NomadicMethod/Resources/values/colors.xml");
  const web = read("../fitness-profile.js");
  const css = read("../styles.css");
  for (const [resource, token] of [["brand_graphite", "graphite"], ["brand_chartreuse", "chartreuse"]]) {
    const color = colors.match(new RegExp('name="' + resource + '">(#[0-9a-f]+)<', 'i'))?.[1].toLowerCase();
    assert.ok(color && css.includes("--" + token + ": " + color));
    assert.ok(native.includes("Resource.Color." + resource));
  }
  assert.match(native, /level <= FitnessProfilePolicy\.WebBoundaryLevel/);
  assert.match(native, /ComplexUnitType.Sp, 12f/);
  assert.match(css, /\.fitness-profile \{[^}]*font-size: 0\.75rem/);
  assert.match(web, /level <= FITNESS_WEB_BOUNDARY_LEVEL/);
  assert.match(css, /\.fitness-area \{ fill: var\(--graphite\)/);
  assert.match(css, /\.fitness-shape \{[^}]*stroke: var\(--chartreuse\)/);
  assert.match(css, /\.fitness-point \{ fill: var\(--chartreuse\)/);
  assert.doesNotMatch(web, /health|athletic|outer.arc/i);
});
for (const entry of fixture.cases) {
  test("fitness profile: " + entry.name, () => {
    const source = { schemaVersion: 1, dimensions: fixture.dimensions,
      ratings: Object.fromEntries(entry.exercises.map(row => [row.id, row.levels])) };
    const before = JSON.stringify(entry);
    assert.deepEqual(calculateFitnessProfile(entry.exercises, entry.state, parseFitnessRatings(source)), entry.expected);
    assert.equal(JSON.stringify(entry), before, "profile calculation must not modify saved workout state");
  });
}
test("canonical runtime fitness ratings cover every Android catalog identity", () => {
  const catalog = JSON.parse(read("../../NomadicMethod/Assets/exercises.json"));
  const ratings = parseFitnessRatings(JSON.parse(read("../../NomadicMethod/Assets/fitness_ratings.json")));
  assert.equal(ratings.size, catalog.length);
  assert.ok(catalog.every(exercise => ratings.has(exercise.id)));
  assert.deepEqual(FITNESS_DIMENSIONS, fixture.dimensions);
});
test("invalid fitness rating sets are rejected", () => {
  for (const set of [[0], [6], [2,1], [1,1], [1.5]]) {
    const levels = FITNESS_DIMENSIONS.map(() => []);
    levels[0] = set;
    assert.throws(() => parseFitnessRatings({schemaVersion:1, dimensions:FITNESS_DIMENSIONS, ratings: {1:levels}}));
  }
});
test("homepage profile reads canonical data with the same native and web contract", () => {
  const native = read("../../NomadicMethod/MainActivity.cs");
  const app = read("../app.js");
  const layout = read("../../NomadicMethod/Resources/layout/screen_duration.xml");
  const html = read("../index.html");
  const build = read("../scripts/build.mjs");
  assert.ok(native.includes('Assets!.Open("fitness_ratings.json")'));
  assert.ok(native.includes("FitnessProfilePolicy.Calculate("));
  assert.ok(layout.includes("NomadicMethod.FitnessProfileView"));
  assert.ok(app.includes('parseFitnessRatings(await fitnessResponse.json())'));
  assert.ok(app.includes("calculateFitnessProfile(exerciseCatalog, session.state, fitnessRatings)"));
  assert.ok(html.indexOf('id="fitness-profile"') > html.indexOf('id="elevation-equipment"'));
  assert.ok(html.indexOf('id="fitness-profile"') < html.indexOf('class="duration-action-bar"'));
  assert.ok(build.includes("NomadicMethod/Assets/fitness_ratings.json"));
  assert.ok(build.includes('fingerprintedAppSource.includes(fitnessOutputName)'));
});
