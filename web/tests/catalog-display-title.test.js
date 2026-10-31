import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { getAnatomicalGroupVarietyInventory } from "../workout.js";

test("shared display titles retain availability based on stable movement IDs", async () => {
  const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
  const fixture = JSON.parse(await readFile(new URL("./fixtures/catalog-display-title-case.json", import.meta.url), "utf8"));
  const distinctTitles = catalog.map(exercise => fixture.exerciseIds.includes(exercise.id)
    ? { ...exercise, name: `${fixture.title} (${exercise.id})` } : exercise);
  const sharedTitles = distinctTitles.map(exercise => fixture.exerciseIds.includes(exercise.id)
    ? { ...exercise, name: fixture.title } : exercise);

  assert.deepEqual(getAnatomicalGroupVarietyInventory(sharedTitles), getAnatomicalGroupVarietyInventory(distinctTitles));
  assert.deepEqual(sharedTitles.filter(exercise => fixture.exerciseIds.includes(exercise.id)).map(exercise => exercise.id), fixture.exerciseIds);
});
