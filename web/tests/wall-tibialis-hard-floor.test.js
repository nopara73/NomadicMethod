import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { WorkoutSession, createDefaultState, getResolution,
  isCompatibleWithWorkoutModifiers, WORKOUT_MODIFIERS as M } from "../workout.js";

const read = name => JSON.parse(readFileSync(new URL(name, import.meta.url), "utf8"));
const catalog = read("../../NomadicMethod/Assets/exercises.json");
const tibialis = catalog.find(exercise => exercise.id === 165);
const cases = read("./fixtures/wall-tibialis-hard-floor-cases.json");
const now = () => Date.parse("2026-10-26T00:00:00Z");
const profile = M.HardFloor | M.Wall | M.Silence;
const slot = getResolution(10).groups.find(group =>
  group.id === "r10.anterior-lateral-lower-leg-dorsal-foot");

for (const item of cases) {
  test("wall tibialis: " + item.name, () => {
    const modifiers = item.modifiers.reduce((flags, name) => flags | M[name], 0);
    assert.equal(isCompatibleWithWorkoutModifiers(tibialis, modifiers), item.eligible);
  });
}

test("a saved wall tibialis Keep is available for the hard-floor shin slot", () => {
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[slot.id] = [165];
  const session = new WorkoutSession(catalog, state, () => 0, now);
  session.initialize();
  session.startWorkout(10, profile);
  assert.equal(session.state.activeWorkoutSession.initialSelections.find(selection =>
    selection.coveredWorkoutGroupIds.includes(slot.id)).rootExerciseId, 165);
  assert.equal(session.getActiveGroups().length, 10);
  assert.deepEqual(session.state.keptExerciseRootIdsBySelectionGroupId[slot.id], [165]);
});

for (const revision of [92, 93]) {
test(`the compatibility correction preserves an unfinished revision-${revision} lineup and feedback`, () => {
  const legacyCatalog = structuredClone(catalog);
  const legacyTibialis = legacyCatalog.find(exercise => exercise.id === 165);
  legacyTibialis.upperBodyClothingRequirement = "ClothingRequired";
  if (revision === 92) legacyTibialis.hardFloorCompatibility = "Incompatible";
  const state = createDefaultState();
  state.keptExerciseRootIdsBySelectionGroupId[slot.id] = [165];
  state.exerciseScoreAdjustmentsByPhase.Warmup = {1295: -2};
  state.scores[165] = 4;
  const previous = new WorkoutSession(legacyCatalog, state, () => 0, now);
  previous.initialize();
  previous.startWorkout(10, profile);
  previous.state.catalogRevision = revision;
  previous.state.activeCatalogRevision = revision;
  const saved = structuredClone(previous.state);
  const restored = new WorkoutSession(catalog, structuredClone(saved), () => 0, now);
  restored.restoreAfterReopen();
  assert.deepEqual(restored.state.selectedExerciseIds, saved.selectedExerciseIds);
  assert.deepEqual(restored.state.keptExerciseRootIdsBySelectionGroupId, saved.keptExerciseRootIdsBySelectionGroupId);
  assert.deepEqual(restored.state.exerciseScoreAdjustmentsByPhase, saved.exerciseScoreAdjustmentsByPhase);
  assert.deepEqual(restored.state.workoutHistory, saved.workoutHistory);
  assert.equal(restored.state.scores[165], 4);
  assert.equal(restored.state.activeWorkoutSession.sessionId, saved.activeWorkoutSession.sessionId);
});
}
