import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { isAcceptedMaterialityDeficiency } from "../workout.js";

test("the shared materiality exception never accepts a different profile or count", async () => {
  const accepted = JSON.parse(await readFile(
    new URL("./fixtures/accepted-materiality-exception.json", import.meta.url), "utf8"));
  assert.equal(isAcceptedMaterialityDeficiency(accepted), true);
  for (const key of Object.keys(accepted)) {
    for (const delta of [-1, 1]) {
      assert.equal(isAcceptedMaterialityDeficiency({
        ...accepted, [key]: accepted[key] + delta,
      }), false, `${key} must match the exact owner-approved deficit`);
    }
  }
});
