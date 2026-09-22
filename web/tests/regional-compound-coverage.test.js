import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import {
  RESOLUTIONS, WORKOUT_MODIFIERS, isSelectable, isCompatibleWithWorkoutModifiers,
  getMaximumDistinctLineupSize,
} from "../workout.js";

const catalog = JSON.parse(await readFile(
  new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const byId = new Map(catalog.map((exercise) => [exercise.id, exercise]));
const cases = JSON.parse(await readFile(
  new URL("./fixtures/regional-compound-cases.json", import.meta.url), "utf8"));

for (const item of cases) {
  test(`reviewed compound and isolation contract: ${item.group}`, () => {
    const group = RESOLUTIONS.get(item.minutes).groups.find((candidate) => candidate.id === item.group);
    for (const id of item.accepted)
      assert.equal(isSelectable(byId.get(id), group), true, `${id} ${byId.get(id).name}`);
    for (const id of item.rejected)
      assert.equal(isSelectable(byId.get(id), group), false, `${id} ${byId.get(id).name}`);
  });
}

test("compound eligibility retains physical restrictions and both boxing lead blocks", () => {
  const profile = WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor |
    WORKOUT_MODIFIERS.Silence | WORKOUT_MODIFIERS.Shy;
  assert.equal(isCompatibleWithWorkoutModifiers(byId.get(248), profile), true);
  assert.equal(isCompatibleWithWorkoutModifiers(byId.get(591), profile), false);
  assert.equal(byId.get(248).sequenceBlocks.length, 1);
  assert.equal(byId.get(591).sequenceBlocks.length, 2);
});

test("a compound sequence cannot hide an isolated member in a broad round", () => {
  const upper = RESOLUTIONS.get(3).groups.find((group) => group.id === "r3.head-neck-upper-limbs");
  const compound = structuredClone(byId.get(248));
  const wrist = byId.get(239);
  assert.equal(getMaximumDistinctLineupSize([compound, wrist], [upper], 0, 2), 1);
  compound.sequenceBlocks.push({ ...compound.sequenceBlocks[0], exerciseId: wrist.id });
  assert.equal(isSelectable(compound, upper), true);
  assert.equal(getMaximumDistinctLineupSize([compound, wrist], [upper], 0, 2), 0);
});
