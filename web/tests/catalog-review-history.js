import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const surfaceReview = JSON.parse(readFileSync(new URL(
  "../../docs/catalog-audit/hard-floor-locomotion-review-2026-10-31.json", import.meta.url), "utf8"));
const corrections = new Map(surfaceReview.entries
  .filter(entry => entry.compatibility !== entry.previousCompatibility)
  .map(entry => [entry.id, entry]));

// Dated fingerprints retain every field. Restore only the separately reviewed
// later surface correction when comparing metadata from an earlier review.
export function beforeSurfaceReview(exercise, reviewedRevision) {
  const correction = corrections.get(exercise.id);
  if (!correction || reviewedRevision >= surfaceReview.catalogRevision) return exercise;
  assert.equal(exercise.hardFloorCompatibility, correction.compatibility);
  return { ...exercise, hardFloorCompatibility: correction.previousCompatibility };
}
