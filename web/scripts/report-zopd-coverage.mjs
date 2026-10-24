import { readFile, writeFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import { WorkoutSession, createDefaultState, NEW_WORKOUT_MINUTES,
  WORKOUT_MODIFIER_VALIDATION_PROFILES, getSessionMovementId,
  CURRENT_CATALOG_REVISION } from "../workout.js";
import { FITNESS_DIMENSIONS, isFitnessSequenceEligible, parseFitnessRatings } from "../fitness-profile.js";

const root = new URL("../../", import.meta.url);
const source = await readFile(new URL("NomadicMethod/Assets/exercises.json", root), "utf8");
const catalog = JSON.parse(source);
const ratingsSource = await readFile(new URL("NomadicMethod/Assets/fitness_ratings.json", root), "utf8");
const ratings = parseFitnessRatings(JSON.parse(ratingsSource));
const session = new WorkoutSession(catalog, createDefaultState(), () => 0.42, () => 1, ratings);
session.state.activeWorkoutMinutes = 3;
session.state.activeCatalogRevision = CURRENT_CATALOG_REVISION;
session.updateSequenceCatalog(3);
const roots = [...session.sequenceExercisesByRootId].map(([id, members]) => ({
  exercise: session.exercisesById.get(id), members: members.map(member => ratings.get(member.id)),
}));
const strictCells = FITNESS_DIMENSIONS.flatMap((characteristic, dimension) =>
  [1, 2, 3, 4, 5].map(level => {
    const zones = FITNESS_DIMENSIONS.map((_, index) => index === dimension ? [level] : []);
    const movementIds = [...new Set(roots.filter(row => isFitnessSequenceEligible(row.members, zones))
      .map(row => getSessionMovementId(row.exercise)))].sort((a, b) => a - b);
    return { characteristic, level, distinctMovements: movementIds.length,
      missingToFive: Math.max(0, 5 - movementIds.length), movementIds };
  }));

// Count actual complete, duration-fitting placement options after the same
// physical predicates as the planner. Availability is necessary but not a proof
// that all of a workout's slots can be solved simultaneously.
const profiles = [...new Set(WORKOUT_MODIFIER_VALIDATION_PROFILES)];
const availability = [];
for (const minutes of NEW_WORKOUT_MINUTES) {
  session.state.activeWorkoutMinutes = minutes;
  for (const modifiers of profiles) {
    session.state.activeWorkoutModifiers = modifiers;
    const groups = session.getSelectionGroups();
    const eligiblePlacements = roots.flatMap(row => {
      if (!session.getSequenceExercises(row.exercise).every(member => session.isCompatibleWithModifiers(member, modifiers))) return [];
      return session.getSequencePlacementOptions(row.exercise, groups)
        .filter(option => row.exercise.sequenceBlocks.length + groups.length - option.length <= minutes)
        .map(option => ({ ...row, slots: option.map(group => group.id) }));
    });
    for (const level of [1, 2, 3, 4, 5]) {
      const zones = FITNESS_DIMENSIONS.map(() => [level]);
      const matching = eligiblePlacements.filter(row => isFitnessSequenceEligible(row.members, zones));
      const slots = groups.map(group => {
        const count = new Set(matching.filter(row => row.slots.includes(group.id))
          .map(row => getSessionMovementId(row.exercise))).size;
        return { groupId: group.id, name: group.displayName, distinctMovements: count };
      });
      availability.push({ minutes, modifiers, uniformLevel: level, slots,
        emptySlots: slots.filter(slot => slot.distinctMovements === 0).length,
        belowFiveSlots: slots.filter(slot => slot.distinctMovements < 5).length });
    }
  }
}
const report = {
  catalogRevision: CURRENT_CATALOG_REVISION, catalogRecords: catalog.length,
  catalogSha256: createHash("sha256").update(source.replaceAll("\r\n", "\n")).digest("hex"),
  ratingsSha256: createHash("sha256").update(ratingsSource.replaceAll("\r\n", "\n")).digest("hex"),
  policy: "Entire nonempty rating set contained in at least one known zone; every atomic member qualifies; SessionMovementId deduplicated.",
  scope: "50 strict single-axis singleton cells globally, plus all five uniform singleton profiles across new workout durations and the existing single/pair physical validation profiles. These are representative profiles, not every possible mixed user zone. Slot availability does not prove a simultaneous complete lineup.",
  summary: {
    strictCellsBelowFive: strictCells.filter(row => row.missingToFive > 0).length,
    strictCellsEmpty: strictCells.filter(row => row.distinctMovements === 0).length,
    physicalProfiles: profiles.length, workoutProfilesChecked: availability.length,
    workoutProfilesWithEmptySlots: availability.filter(row => row.emptySlots > 0).length,
    workoutProfilesBelowFiveChoices: availability.filter(row => row.belowFiveSlots > 0).length,
    byUniformLevel: [1,2,3,4,5].map(level => ({ level,
      profiles: availability.filter(row => row.uniformLevel === level).length,
      withEmptySlots: availability.filter(row => row.uniformLevel === level && row.emptySlots > 0).length,
      belowFiveChoices: availability.filter(row => row.uniformLevel === level && row.belowFiveSlots > 0).length,
    })),
  },
  strictCells, availability,
};
const json = JSON.stringify(report, null, 2) + "\n";
const outputIndex = process.argv.indexOf("--output");
if (outputIndex !== -1) {
  if (!process.argv[outputIndex + 1]) throw new Error("--output needs a path");
  await writeFile(process.argv[outputIndex + 1], json);
  console.log(JSON.stringify(report.summary, null, 2));
} else console.log(json);
