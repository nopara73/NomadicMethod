import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { exerciseIdentityName } from "../exercise-names.js";

const names = JSON.parse(readFileSync(new URL(
  "../../NomadicMethod/Assets/exercise_names.json", import.meta.url), "utf8"));
const reactionLabels = new Map(names.reactionLabels.map(entry => [entry.name, entry.identityName]));

export const beforeNameReview = exercise => ({
  ...exercise, name: exerciseIdentityName(exercise.id, exercise.name),
});

export function beforeReactionLabelReview(document) {
  const previous = structuredClone(document);
  for (const family of previous.families)
    for (const action of family.actions)
      action.label = reactionLabels.get(action.label) ?? action.label;
  return previous;
}

export function beforeReactionLabelReviewSource(source) {
  for (const [current, previous] of reactionLabels)
    source = source.replaceAll(`"label": ${JSON.stringify(current)}`, `"label": ${JSON.stringify(previous)}`);
  return source;
}

const surfaceReview = JSON.parse(readFileSync(new URL(
  "../../docs/catalog-audit/hard-floor-locomotion-review-2026-10-31.json", import.meta.url), "utf8"));
const corrections = new Map(surfaceReview.entries
  .filter(entry => entry.compatibility !== entry.previousCompatibility)
  .map(entry => [entry.id, entry]));
const varietyReview = JSON.parse(readFileSync(new URL(
  "../../docs/catalog-audit/post-filter-variety-review-2026-10-31.json", import.meta.url), "utf8"));

// Dated fingerprints retain every field. Restore only the separately reviewed
// later surface correction when comparing metadata from an earlier review.
export function beforeSurfaceReview(exercise, reviewedRevision) {
  exercise = beforeNameReview(exercise);
  const amendment = varietyReview.metadataAmendments.find(entry => entry.id === exercise.id);
  if (amendment && reviewedRevision < varietyReview.catalogRevision) {
    for (const [field, value] of Object.entries(amendment.currentFields)) assert.equal(exercise[field], value);
    exercise = {...exercise, ...amendment.previousFields};
    for (const field of amendment.previousAbsentFields) delete exercise[field];
  }
  const correction = corrections.get(exercise.id);
  if (!correction || reviewedRevision >= surfaceReview.catalogRevision) return exercise;
  assert.equal(exercise.hardFloorCompatibility, correction.compatibility);
  return { ...exercise, hardFloorCompatibility: correction.previousCompatibility };
}
