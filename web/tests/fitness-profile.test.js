import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { FITNESS_DIMENSIONS, FITNESS_MAXIMUM_LEVEL, FITNESS_WEB_BOUNDARY_LEVEL,
  calculateFitnessProfile, parseFitnessRatings, fitnessProfileRadiusFraction } from "../fitness-profile.js";

const read = file => readFileSync(new URL(file, import.meta.url), "utf8");
const fixture = JSON.parse(read("./fixtures/fitness-profile-cases.json"));
test("expert level extends beyond the web without rounding fractional levels", () => {
  assert.equal(FITNESS_MAXIMUM_LEVEL, fixture.chartScale.maximumLevel);
  assert.equal(FITNESS_WEB_BOUNDARY_LEVEL, fixture.chartScale.webBoundaryLevel);
  for (const entry of fixture.chartScale.cases)
    assert.ok(Math.abs(fitnessProfileRadiusFraction(entry.level) - entry.radiusFraction) < 1e-12);
  assert.ok(fitnessProfileRadiusFraction(5) > fitnessProfileRadiusFraction(4));
});

test("native and web retain a four-ring green spiderweb without category styling", () => {
  const native = read("../../NomadicMethod/FitnessProfileView.cs");
  const colors = read("../../NomadicMethod/Resources/values/colors.xml");
  const web = read("../fitness-profile.js");
  const css = read("../styles.css");
  const green = colors.match(/name="fitness_profile_green">(#[0-9a-f]+)</i)?.[1].toLowerCase();
  assert.equal(green, "#c8dc63");
  assert.ok(css.includes("--fitness-green: " + green));
  assert.match(native, /level <= FitnessProfilePolicy\.WebBoundaryLevel/);
  assert.match(native, /Resource\.Color\.fitness_profile_green/);
  assert.match(web, /level <= FITNESS_WEB_BOUNDARY_LEVEL/);
  assert.match(css, /\.fitness-shape \{[^}]*stroke: var\(--fitness-green\)/);
  assert.match(css, /\.fitness-point \{ fill: var\(--fitness-green\)/);
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
