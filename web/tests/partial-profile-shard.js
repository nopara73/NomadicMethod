import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { WorkoutSession, createDefaultState, WORKOUT_MODIFIER_VALIDATION_PROFILES } from "../workout.js";
import { FITNESS_DIMENSIONS, parseFitnessRatings, isFitnessSequenceEligible } from "../fitness-profile.js";

const catalog = JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/exercises.json", import.meta.url), "utf8"));
const ratings = parseFitnessRatings(JSON.parse(readFileSync(new URL("../../NomadicMethod/Assets/fitness_ratings.json", import.meta.url), "utf8")));

export function registerPartialProfileShard(shard, total) {
  FITNESS_DIMENSIONS.forEach((characteristic, dimension) => {
    if (dimension % total !== shard) return;
    for (const level of [1, 2, 3, 4, 5]) {
      test(`partial ${characteristic}=${level}: complete whole-body workout for every physical validation profile`, () => {
        const zones = FITNESS_DIMENSIONS.map((_, index) => index === dimension ? [level] : []);
        for (const modifiers of WORKOUT_MODIFIER_VALIDATION_PROFILES) {
          const session = WorkoutSession.createForApplication(catalog, createDefaultState(), () => 0.42, () => 1, ratings);
          session.getFitnessSelectionZones = () => zones;
          assert.doesNotThrow(() => session.prepareWorkout(3, modifiers), `physical profile ${modifiers}`);
          const groups = session.getActiveGroups();
          assert.equal(groups.length, 3, `physical profile ${modifiers}`);
          for (const group of groups) {
            const members = session.getSequenceExercises(session.getSequenceRoot(session.getSelectedExercise(group)));
            assert.ok(isFitnessSequenceEligible(members.map(member => ratings.get(member.id)), zones,
              members.map(member => ratings.trainingCharacteristics.get(member.id))), `strict eligibility ${modifiers}`);
          }
        }
      });
    }
  });
}
