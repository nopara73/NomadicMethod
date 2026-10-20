import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import test from "node:test";
import { getWorkoutCatalog, EXECUTION_CORRECTION_EXERCISE_IDS,
  WorkoutSession, createDefaultState } from "../workout.js";

const catalog = JSON.parse(await readFile(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const cases = JSON.parse(await readFile(new URL("./fixtures/execution-protocol-cases.json", import.meta.url), "utf8"));

test("reviewed execution protocols include complete sides and directions", () => {
  assert.deepEqual(cases.map(c => c.id), EXECUTION_CORRECTION_EXERCISE_IDS);
  for (const c of cases) {
    const exercise = catalog.find(e => e.id === c.id);
    assert.equal(exercise.sideSequence, c.side, `${c.id} sides`);
    assert.equal(exercise.directionSequence, c.direction, `${c.id} directions`);
    assert.equal(exercise.sequenceBlocks.length, c.blocks, `${c.id} phases`);
    assert.ok(exercise.sequenceBlocks.every(b => b.exerciseId === c.id));
    if (c.direction !== "None") {
      for (let index = 0; index < c.blocks; index += 2) {
        const [first, second] = exercise.sequenceBlocks.slice(index, index + 2);
        assert.equal(first.sideCue, second.sideCue);
        assert.notEqual(first.directionCue, second.directionCue);
        assert.equal(first.mediaSegment, "FirstDirection");
        assert.equal(second.mediaSegment, "SecondDirection");
      }
    }
    if (c.side.startsWith("Screen")) {
      assert.equal(exercise.sequenceBlocks.filter(b => b.mirrorMedia).length, c.blocks / 2);
      assert.equal(new Set(exercise.sequenceBlocks.map(b => b.sideCue)).size, 2);
    }
  }
  const ankle = catalog.find(e => e.id === 1299);
  assert.deepEqual(ankle.sequenceBlocks.map(b => [b.sideCue, b.directionCue, b.mirrorMedia]), [
    ["ScreenLeft", "Clockwise", false], ["ScreenLeft", "Counterclockwise", false],
    ["ScreenRight", "Counterclockwise", true], ["ScreenRight", "Clockwise", true],
  ]);
});

test("unfinished revision 82 workouts retain original phases and media", async () => {
  for (const revision of [81, 82]) {
    const old = getWorkoutCatalog(catalog, revision);
    for (const c of cases.filter(c => revision === 82 || c.id < 1294)) {
      const e = old.find(e => e.id === c.id);
      assert.equal(e.sideSequence, c.previousSide);
      assert.equal(e.directionSequence, "None");
      assert.deepEqual(e.sequenceBlocks, [{ exerciseId: c.id, sideCue: "None",
        directionCue: "None", mirrorMedia: false, mediaSegment: "Full" }]);
      assert.equal(e.video, `exercise_videos/revision82/exercise_${String(c.id).padStart(4, "0")}.mp4`);
      const original = await readFile(new URL(`../../NomadicMethod/Assets/${e.video}`, import.meta.url));
      assert.equal(createHash("sha256").update(original).digest("hex"), c.previousVideoSha256);
    }
  }
  assert.equal(getWorkoutCatalog(catalog, 83).find(e => e.id === 1299).sequenceBlocks.length, 4);
});

for (const phase of ["ready", "movement", "rest"]) test(`existing ankle workout preserves its ${phase} checkpoint and history`, async () => {
  const fixture = JSON.parse(await readFile(new URL("./fixtures/legacy-execution-session.json", import.meta.url), "utf8"));
  let now = Date.UTC(2026, 9, 20);
  let s = new WorkoutSession(catalog, fixture.state, () => 0.4, () => now);
  s.initialize();
  const current = s.getNextGroup();
  assert.equal(current.id, fixture.currentId);
  assert.equal(s.getSelectedExercise(current).id, 1299);
  if (phase === "movement") s.beginMovement(current, 28000, now + 28000);
  if (phase === "rest") s.beginRest(current, now + 15000);
  const original = structuredClone(s.state);
  for (let reopen = 0; reopen < 2; reopen++) {
    now += 365 * 86400000;
    s = new WorkoutSession(catalog, structuredClone(s.state), () => 0.1, () => now);
    s.restoreAfterReopen();
    assert.deepEqual(s.getActiveGroups().map(g => g.id), fixture.roundIds);
    assert.deepEqual(s.getActiveGroups().map(g => s.getSelectedExercise(g).id), fixture.selectedIds);
    assert.equal(s.getNextGroup().id, fixture.currentId);
    assert.equal(s.state.activeCatalogRevision, 82);
    assert.equal(s.state.scores[1299], fixture.state.scores[1299]);
    assert.equal(s.state.catalogIdentities[1299], fixture.state.catalogIdentities[1299]);
    for (const key of ["activeWorkoutSession", "workoutHistory", "exerciseScoreAdjustmentsByPhase",
      "keptExerciseRootIdsBySelectionGroupId", "activeWorkoutIsLightDay"])
      assert.deepEqual(s.state[key], original[key], key);
    assert.equal(s.getSelectedExercise(s.getNextGroup()).sequenceBlocks.length, 1);
    assert.match(s.getSelectedExercise(s.getNextGroup()).video, /revision82/);
    if (phase === "movement") {
      assert.equal(s.state.pendingMovementPausedByUser, true);
      assert.equal(s.state.pendingMovementMillisecondsRemaining, 28000);
    }
    if (phase === "rest") {
      assert.equal(s.state.pendingRestPausedByUser, true);
      assert.equal(s.state.pendingRestMillisecondsRemaining, 15000);
    }
  }
});

test("new short workouts cannot admit the corrected four-block ankle sequence", () => {
  const state = createDefaultState(); state.scores[1299] = 100000;
  const session = new WorkoutSession(catalog, state, () => 0.4);
  session.initialize(); session.startWorkout(10, 146);
  assert.ok(session.getActiveGroups().every(g => session.getSelectedExercise(g).id !== 1299));
});
