import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import test from "node:test";
import { beforeSurfaceReview } from "./catalog-review-history.js";
import { checkFixedPositionReviews, findPositionCoverageDeficiencies } from "../../tools/check-exercise-position-reviews.mjs";
import { frozenWorkoutProtocols } from "../workout-protocol-history.js";
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIERS as M, getWorkoutCatalog, getSequenceBlockCount, getWorkoutBlockAccent, canUseSingleDirection } from "../workout.js";

const catalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const cases = JSON.parse(readFileSync(new URL("./fixtures/catalog-position-cases.json", import.meta.url), "utf8"));

test("the pose review changes only execution geometry and retains the historical review chain", () => {
  const review = JSON.parse(readFileSync(new URL("../../docs/catalog-audit/fixed-position-review-2026-10-29.json", import.meta.url), "utf8"));
  const previousCatalog = getWorkoutCatalog(catalog, 99);
  const hash = value => createHash("sha256").update(JSON.stringify(value)).digest("hex");
  assert.equal(review.entries.length, 71);
  assert.deepEqual(review.entries.filter(e => e.kind === "FixedPositionCorrection").map(e => e.id), cases.map(e => e.id));
  for (const entry of review.entries) {
    const current = catalog.find(e => e.id === entry.id);
    const previous = previousCatalog.find(e => e.id === entry.id);
    assert.equal(hash(beforeSurfaceReview(previous, review.catalogRevision)), entry.previousMetadataSha256, String(entry.id));
    assert.equal(hash(beforeSurfaceReview(current, review.catalogRevision)), entry.metadataSha256, String(entry.id));
    const geometry = ["sideSequence", "directionSequence", "sequenceBlocks"];
    const withoutGeometry = e => Object.fromEntries(Object.entries(e).filter(([key]) => !geometry.includes(key)));
    assert.deepEqual(withoutGeometry(current), withoutGeometry(previous), String(entry.id));
  }
});

test("all catalog poses and runtime media have current independent reviews", async () => {
  assert.deepEqual(await checkFixedPositionReviews(), { exercises: 930, oppositeBlocks: 408, reactionClips: 20 });
});

test("the pose gate rejects alternating motion with a missing opposite linked/direction position", () => {
  const make = (mirrorMedia, mediaSegment) => ({ exerciseId: 2, mirrorMedia, mediaSegment });
  const reviews = [{ id: 2, coverage: "OppositeBlocks" }];
  const owner = { id: 1, sideSequence: "Alternating", sequenceBlocks: [make(false,"FirstDirection"), make(true,"FirstDirection"), make(false,"SecondDirection")] };
  assert.deepEqual(findPositionCoverageDeficiencies([owner], reviews), [{ id: 2, ownerId: 1, segment: "SecondDirection", reason: "Missing opposite position" }]);
  owner.sequenceBlocks.push(make(true,"SecondDirection"));
  assert.deepEqual(findPositionCoverageDeficiencies([owner], reviews), []);
});

for (const c of cases) test(`${c.id} retains every fixed position in its owning atomic sequence`, () => {
  const exercise = catalog.find(e => e.id === c.id);
  assert.equal(exercise.sideSequence, c.sideSequence);
  for (const owner of catalog.filter(e => e.sequenceBlocks.some(b => b.exerciseId === c.id))) {
    const pair = owner.sequenceBlocks.filter(b => b.exerciseId === c.id);
    assert.deepEqual(pair.map(b => b.mirrorMedia), [false, true]);
    assert.deepEqual(pair.map(b => getWorkoutBlockAccent({sequenceSideCue:b.sideCue})), ["blue", "red"]);
    assert.equal(canUseSingleDirection(owner), false);
    for (const minutes of [3,5,7,10,15,20,30,60,90])
      assert.equal(getSequenceBlockCount(owner, minutes), owner.sequenceBlocks.length);
  }
});

test("revision99 retains all changed protocols, including zero-block linked members", () => {
  const restored = getWorkoutCatalog(catalog, 99);
  for (const old of frozenWorkoutProtocols.exercises) {
    const actual = restored.find(e => e.id === old.id);
    for (const field of ["sideSequence", "directionSequence", "sequenceBlocks"])
      assert.deepEqual(actual[field], old[field], `${old.id} ${field}`);
  }
  assert.deepEqual(getWorkoutCatalog(catalog,102), catalog);
});

for (const id of [103, 1144, 307]) for (const phase of ["Ready", "Movement", "Rest"])
  test("revision99 preserves " + id + " at the " + phase + " command boundary", () => {
    const now = Date.parse("2026-10-29T00:00:00Z");
    const legacy = getWorkoutCatalog(catalog, 99);
    const state = createDefaultState();
    for (const owner of legacy.filter(e => e.sequenceBlocks.some(b => b.exerciseId === id)))
      for (const block of owner.sequenceBlocks) state.scores[block.exerciseId] = 100000;
    for (const scorePhase of ['Warmup','PeakPerformance','Fatigued'])
      state.exerciseScoreAdjustmentsByPhase[scorePhase] = Object.fromEntries(legacy
        .filter(e => e.sequenceBlocks.some(b => b.exerciseId === id)).map(e => [e.id,100000]));
    const old = new WorkoutSession(legacy, state, () => 0.4, () => now);
    old.initialize();
    old.startWorkout(60, M.Floor | M.Elevation | M.UpperBodyClothing);
    const current = old.getActiveGroups().find(g => old.getSelectedExercise(g).id === id);
    assert.ok(current);
    while (old.getNextGroup().id !== current.id) {
      const next = old.getNextGroup();
      old.beginRest(next, now + 15000);
      if (old.isIntermediateSequenceBlock(next)) old.advanceSequence(next);
      else old.recordOutcome(next, true);
      old.clearPendingRest();
    }
    if (phase === "Movement") old.beginMovement(current, 28000, now + 28000);
    if (phase === "Rest") old.beginRest(current, now + 15000);
    old.state.catalogRevision = 99;
    old.state.activeCatalogRevision = 0;
    old.state.keptExerciseRootIdsBySelectionGroupId[current.selectionGroupId ?? current.id] = [id];
    const rounds = old.getActiveGroups();
    const saved = structuredClone(old.state);
    const restored = new WorkoutSession(catalog, saved, () => 0.8, () => now + 86400000);
    restored.state.activeCatalogRevision = 0;
    const beforeRead = structuredClone(restored.state);
    assert.deepEqual(restored.getActiveGroups(), rounds);
    assert.equal(restored.getNextGroup().id, current.id);
    assert.deepEqual(restored.state, beforeRead);
    restored.restoreAfterReopen();
    assert.equal(restored.state.activeCatalogRevision, 99);
    assert.deepEqual(restored.getActiveGroups(), rounds);
    for (const key of ["activeWorkoutSession", "workoutHistory", "scores", "keptExerciseRootIdsBySelectionGroupId", "outcomes"])
      assert.deepEqual(restored.state[key], saved[key], key);
    assert.deepEqual(restored.exercisesById.get(id).sequenceBlocks,
      legacy.find(e => e.id === id).sequenceBlocks);
    if (phase === "Movement") {
      assert.equal(restored.state.pendingMovementPausedByUser, true);
      assert.equal(restored.state.pendingMovementMillisecondsRemaining, 28000);
    }
    if (phase === "Rest") {
      assert.equal(restored.state.pendingRestPausedByUser, true);
      assert.equal(restored.state.pendingRestMillisecondsRemaining, 15000);
    }
    const minimum = restored.getMinimumActiveWorkoutMinutes();
    restored.resizeActiveWorkout(minimum);
    assert.equal(restored.getNextGroup().id, current.id);
    assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId,
      saved.keptExerciseRootIdsBySelectionGroupId);
    assert.equal(restored.state.activeWorkoutSession.durationChanges.length, minimum < 60 ? 1 : 0);
  });
