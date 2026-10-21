import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import {
  CURRENT_CATALOG_REVISION,
  REACTION_CUE_CATALOG_REVISION,
  getNextReactionCueDurationMilliseconds,
  getReactionCueTiming,
  isReactionCueEnabled,
} from "../workout.js";

const catalog = JSON.parse(await readFile(
  new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url),
  "utf8",
));

test("each reaction level has at least six distinct safe motion exercises", () => {
  assert.equal(REACTION_CUE_CATALOG_REVISION, CURRENT_CATALOG_REVISION);
  for (let level = 1; level <= 5; level++) {
    const reactive = catalog.filter(exercise => exercise.reactionCueLevel === level);
    const movementIds = new Set(reactive.map(exercise =>
      exercise.sessionMovementId > 0 ? exercise.sessionMovementId : exercise.id));
    assert.ok(movementIds.size >= 6);
    assert.ok(reactive.every(exercise =>
      exercise.mode === "Repetition" && exercise.presentation === "Motion"));
  }
});

test("cue ranges get denser without becoming deterministic", () => {
  const expected = [
    [5500, 8000, 1600, 2200],
    [4000, 6500, 1200, 1800],
    [3000, 5000, 900, 1400],
    [2000, 3500, 700, 1100],
    [1200, 2500, 450, 850],
  ];
  expected.forEach(([minimumMove, maximumMove, minimumFreeze, maximumFreeze], index) => {
    const level = index + 1;
    assert.deepEqual(getReactionCueTiming(level), {
      minimumMoveMilliseconds: minimumMove,
      maximumMoveMilliseconds: maximumMove,
      minimumFreezeMilliseconds: minimumFreeze,
      maximumFreezeMilliseconds: maximumFreeze,
    });
    assert.equal(getNextReactionCueDurationMilliseconds(level, true, 0), minimumMove);
    assert.equal(
      getNextReactionCueDurationMilliseconds(level, true, 1 - Number.EPSILON),
      maximumMove,
    );
    assert.equal(getNextReactionCueDurationMilliseconds(level, false, 0), minimumFreeze);
    assert.equal(
      getNextReactionCueDurationMilliseconds(level, false, 1 - Number.EPSILON),
      maximumFreeze,
    );
  });
});

test("legacy active workouts retain their original continuous playback", () => {
  const exercise = { reactionCueLevel: 5 };
  assert.equal(isReactionCueEnabled(exercise, REACTION_CUE_CATALOG_REVISION - 1), false);
  assert.equal(isReactionCueEnabled(exercise, REACTION_CUE_CATALOG_REVISION), true);
  assert.equal(isReactionCueEnabled({ reactionCueLevel: 0 }, CURRENT_CATALOG_REVISION), false);
});
