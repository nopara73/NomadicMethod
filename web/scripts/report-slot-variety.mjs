import { createHash } from "node:crypto";
import { readFile, writeFile, rename, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import {
  CURRENT_CATALOG_REVISION, NEW_WORKOUT_MINUTES, RESOLUTIONS,
  SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES,
  MINIMUM_EXERCISES_PER_SHORT_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_LEGACY_MODIFIER_PAIR_STATE_PER_GROUP,
  getMinimumExercisesPerModifierPairStatePerGroup,
  getSessionMovementId,
} from "../workout.js";

const root = new URL("../../", import.meta.url);
const catalogSource = await readFile(new URL("NomadicMethod/Assets/exercises.json", root), "utf8");
const catalog = JSON.parse(catalogSource);
const ledger = JSON.parse(await readFile(new URL(
  "docs/catalog-audit/modifier_coverage_deficits_current.json", root), "utf8"));
const catalogSha256 = createHash("sha256")
  .update(catalogSource.replaceAll("\r\n", "\n")).digest("hex");
const policy = {
  shortWorkoutCoverageMaximumMinutes: SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES,
  shortWorkoutModifierPairMinimumPerStatePerGroup:
    MINIMUM_EXERCISES_PER_SHORT_MODIFIER_PAIR_STATE_PER_GROUP,
  legacyWorkoutModifierPairMinimumPerStatePerGroup:
    MINIMUM_EXERCISES_PER_LEGACY_MODIFIER_PAIR_STATE_PER_GROUP,
};
if (ledger.catalogRevision !== CURRENT_CATALOG_REVISION ||
    ledger.catalogSha256 !== catalogSha256 || ledger.catalogRecordCount !== catalog.length ||
    Object.entries(policy).some(([key, value]) => ledger.policy[key] !== value)) {
  throw new Error("Refresh the production catalog deficit ledger before reporting slot variety.");
}

const targets = new Map();
// Show the supply before contextual filtering as well as release-gate deficits.
// A passing pairwise floor does not establish balanced anatomical variety.
const movementCount = records => new Set(records.map(getSessionMovementId)).size;
const roots = catalog.filter(exercise => exercise.sequenceBlocks?.length > 0);
const byId = new Map(catalog.map(exercise => [exercise.id, exercise]));
const trained = exercise => [exercise.primaryCanonicalGroup,
  ...(exercise.secondaryCanonicalGroups ?? [])];
const trainedByRoot = new Map(roots.map(root => [root.id, new Set(
  root.sequenceBlocks.flatMap(block => trained(byId.get(block.exerciseId))))]));
const anatomicalInventory = [...new Set(catalog.flatMap(trained))].map(muscle => ({
  muscle,
  primaryMovementCount: movementCount(catalog.filter(exercise =>
    exercise.primaryCanonicalGroup === muscle)),
  trainedMovementCount: movementCount(catalog.filter(exercise =>
    trained(exercise).includes(muscle))),
  schedulableMovementCount: movementCount(roots.filter(root =>
    trainedByRoot.get(root.id).has(muscle))),
  singleBlockMovementCount: movementCount(roots.filter(root =>
    root.sequenceBlocks.length === 1 && trainedByRoot.get(root.id).has(muscle))),
})).sort((left, right) => left.trainedMovementCount - right.trainedMovementCount ||
  left.muscle.localeCompare(right.muscle));
for (const row of ledger.pairwise) {
  const group = RESOLUTIONS.get(row.minutes)?.groups.find(g => g.id === row.groupId);
  if (!group || row.requiredExerciseCount !== getMinimumExercisesPerModifierPairStatePerGroup(row.minutes) ||
      !Number.isInteger(row.matchingExerciseCount) || row.matchingExerciseCount < 0 ||
      row.matchingExerciseCount >= row.requiredExerciseCount) {
    throw new Error(`Invalid availability deficiency for ${row.groupId}.`);
  }
  if (row.minutes > SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES) continue;
  const target = targets.get(row.groupId) ?? {
    groupId: row.groupId, name: group.displayName, muscles: group.canonicalGroups,
    maximumMissingChoices: 0, deficientProfileCount: 0,
  };
  target.maximumMissingChoices = Math.max(target.maximumMissingChoices,
    row.requiredExerciseCount - row.matchingExerciseCount);
  target.deficientProfileCount++;
  targets.set(row.groupId, target);
}
const report = {
  catalogRevision: CURRENT_CATALOG_REVISION, catalogSha256, catalogRecordCount: catalog.length,
  catalogSessionMovementCount: movementCount(catalog),
  schedulableSessionMovementCount: movementCount(roots),
  anatomicalInventory,
  newWorkoutMinutes: NEW_WORKOUT_MINUTES, policy,
  acceptedCoverageExceptions: ledger.policy.acceptedCoverageExceptions,
  gateSummary: ledger.summary,
  shortWorkoutDeficiencyCount: ledger.pairwise.filter(r => r.minutes <= SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES).length,
  legacyCompatibilityDeficiencyCount: ledger.pairwise.filter(r => r.minutes > SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES).length,
  remainingShortWorkoutSlotCount: targets.size,
  targets: [...targets.values()],
  interpretation: [
    "Anatomical inventory counts distinct movement identities before modifiers, fitness, recovery, preference or duration filtering; it is a supply diagnostic, not a new admission quota.",
    "New workouts offer 3, 5, 7, 10 and 15 minutes. Longer durations remain only for existing sessions and history.",
    "Each available short-workout slot requires five distinct selectable complete session movements, except the exact owner-approved omissions.",
    "A grouped slot accepts a genuine primary or meaningful secondary association to any member muscle. It does not require every member muscle at once.",
    "The maximum shortage in a slot is a sourcing target, not a count of approved new exercises. Overlapping slots and movement families must not be summed as independent additions.",
    "Secondary balance, posture and incidental bracing never supply training credit. Refreshing this report never waives a release gate.",
  ],
};
const json = `${JSON.stringify(report, null, 2)}\n`;
if (process.argv.includes("--write")) {
  const output = new URL("docs/catalog-audit/slot_variety_sourcing_targets_current.json", root);
  const temporary = new URL(`${output.href}.${process.pid}.tmp`);
  try {
    await writeFile(temporary, json, { encoding: "utf8", flag: "wx" });
    await rename(temporary, output);
  } finally { await rm(temporary, { force: true }); }
  console.log(`Slot variety report: ${fileURLToPath(output)}`);
} else { console.log(json); }
