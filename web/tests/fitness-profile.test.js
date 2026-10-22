import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { FITNESS_DIMENSIONS, calculateFitnessProfile, parseFitnessRatings } from "../fitness-profile.js";

const read = file => readFileSync(new URL(file, import.meta.url), "utf8");
const fixture = JSON.parse(read("./fixtures/fitness-profile-cases.json"));
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
