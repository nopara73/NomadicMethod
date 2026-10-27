import { readFile, writeFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import { WorkoutSession, createDefaultState, SUPPORTED_MINUTES, SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES,
  WORKOUT_MODIFIERS, WORKOUT_MODIFIER_VALIDATION_PROFILES, getSessionMovementId,
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
const levelCells = FITNESS_DIMENSIONS.flatMap((characteristic, dimension) =>
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
const singleAxisAvailability = [];
// This diagnostic audits short-workout variety, not the duration selector.
for (const minutes of SUPPORTED_MINUTES.filter(value => value <= SHORT_WORKOUT_COVERAGE_MAXIMUM_MINUTES)) {
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
    for (const [dimension, characteristic] of FITNESS_DIMENSIONS.entries()) {
      for (const level of [1, 2, 3, 4, 5]) {
        const zones = FITNESS_DIMENSIONS.map((_, index) => index === dimension ? [level] : []);
        const matching = eligiblePlacements.filter(row => isFitnessSequenceEligible(row.members, zones));
        const slots = groups.map(group => ({ groupId: group.id, name: group.displayName,
          distinctMovements: new Set(matching.filter(row => row.slots.includes(group.id))
            .map(row => getSessionMovementId(row.exercise))).size }));
        singleAxisAvailability.push({ minutes, modifiers, characteristic, level,
          emptySlots: slots.filter(slot => slot.distinctMovements === 0).length,
          belowFiveSlots: slots.filter(slot => slot.distinctMovements < 5).length,
          missingSlots: slots.filter(slot => slot.distinctMovements < 5) });
      }
    }
    for (const level of [1, 2, 3, 4, 5]) {
      const zones = FITNESS_DIMENSIONS.map(() => [level]);
      const matching = eligiblePlacements.filter(row => isFitnessSequenceEligible(row.members, zones));
      const slots = groups.map(group => {
        const count = new Set(matching.filter(row => row.slots.includes(group.id))
          .map(row => getSessionMovementId(row.exercise))).size;
        return { groupId: group.id, name: group.displayName, distinctMovements: count };
      });
      const modifierNames = Object.entries(WORKOUT_MODIFIERS)
        .filter(([, bit]) => bit > 0 && (modifiers & bit) === bit).map(([name]) => name);
      availability.push({ minutes, modifiers, modifierNames, uniformLevel: level, slots,
        emptySlots: slots.filter(slot => slot.distinctMovements === 0).length,
        belowFiveSlots: slots.filter(slot => slot.distinctMovements < 5).length });
    }
  }
}
// Fixed synthetic zones exercise the normal planner without manufacturing a
// user's feedback history. Every other selection rule remains the product rule.
const completeLineups = [];
if (process.argv.includes("--verify-lineups")) {
  for (const profile of availability.filter(row => row.emptySlots === 0)) {
    const zones = FITNESS_DIMENSIONS.map(() => [profile.uniformLevel]);
    const planner = new WorkoutSession(catalog, createDefaultState(), () => 0.42, () => 1, ratings);
    planner.getFitnessEligibility = () => new Map([...planner.sequenceExercisesByRootId]
      .map(([id, members]) => [id,
        isFitnessSequenceEligible(members.map(member => ratings.get(member.id)), zones)]));
    try {
      planner.prepareWorkout(profile.minutes, profile.modifiers);
      completeLineups.push({ minutes: profile.minutes, modifiers: profile.modifiers,
        uniformLevel: profile.uniformLevel, success: true });
    } catch (error) {
      completeLineups.push({ minutes: profile.minutes, modifiers: profile.modifiers,
        uniformLevel: profile.uniformLevel, success: false, error: error.message });
    }
  }
}
const report = {
  schemaVersion: 2, selectionRule: "overlap",
  catalogRevision: CURRENT_CATALOG_REVISION, catalogRecords: catalog.length,
  catalogSha256: createHash("sha256").update(source.replaceAll("\r\n", "\n")).digest("hex"),
  ratingsSha256: createHash("sha256").update(ratingsSource.replaceAll("\r\n", "\n")).digest("hex"),
  policy: "Any explicitly rated level overlaps the corresponding known zone; every atomic member qualifies; SessionMovementId deduplicated.",
  scope: "50 single-axis singleton cells globally under the overlap rule, plus all five uniform singleton profiles across short-workout durations and the existing single/pair physical validation profiles. It also checks each single known characteristic/level against the same physical/duration profiles. These are representative profiles, not every possible mixed user zone. Slot availability does not prove a simultaneous complete lineup; --verify-lineups additionally runs the normal planner with fixed synthetic zones on uniform-level profiles without an empty slot.",
  summary: {
    levelCellsBelowFive: levelCells.filter(row => row.missingToFive > 0).length,
    levelCellsEmpty: levelCells.filter(row => row.distinctMovements === 0).length,
    physicalProfiles: profiles.length, workoutProfilesChecked: availability.length,
    workoutProfilesWithEmptySlots: availability.filter(row => row.emptySlots > 0).length,
    workoutProfilesBelowFiveChoices: availability.filter(row => row.belowFiveSlots > 0).length,
    completeLineupChecks: completeLineups.length,
    additionalLineupFailuresWithoutEmptySlots: completeLineups.filter(row => !row.success).length,
    singleAxisProfilesChecked: singleAxisAvailability.length,
    bySingleKnownCharacteristic: FITNESS_DIMENSIONS.map(characteristic => ({ characteristic,
      levels: [1,2,3,4,5].map(level => ({ level,
        profiles: singleAxisAvailability.filter(row => row.characteristic === characteristic && row.level === level).length,
        withEmptySlots: singleAxisAvailability.filter(row => row.characteristic === characteristic && row.level === level && row.emptySlots > 0).length,
        belowFiveChoices: singleAxisAvailability.filter(row => row.characteristic === characteristic && row.level === level && row.belowFiveSlots > 0).length,
      })),
    })),
    byUniformLevel: [1,2,3,4,5].map(level => ({ level,
      profiles: availability.filter(row => row.uniformLevel === level).length,
      withEmptySlots: availability.filter(row => row.uniformLevel === level && row.emptySlots > 0).length,
      belowFiveChoices: availability.filter(row => row.uniformLevel === level && row.belowFiveSlots > 0).length,
    })),
  },
  levelCells, availability, singleAxisAvailability, completeLineups,
};
const json = JSON.stringify(report, null, 2) + "\n";
const outputIndex = process.argv.indexOf("--output");
if (outputIndex !== -1) {
  if (!process.argv[outputIndex + 1]) throw new Error("--output needs a path");
  await writeFile(process.argv[outputIndex + 1], json);
  console.log(JSON.stringify(report.summary, null, 2));
} else console.log(json);
