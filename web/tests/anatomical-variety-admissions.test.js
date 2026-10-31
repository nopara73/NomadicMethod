import { beforeSurfaceReview } from "./catalog-review-history.js";
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';
import test from 'node:test';
import { getWorkoutCatalog, getAnatomicalGroupVarietyInventory, getSessionMovementId } from '../workout.js';

const load = name => JSON.parse(readFileSync(new URL(name, import.meta.url), 'utf8'));
const catalog = load('../../NomadicMethod/Assets/exercises.json');
const admissions = load('../../docs/catalog-audit/anatomical-variety-admissions-2026-10-26.json');
// Historical anatomy evidence keeps its reviewed protocol; the later pose audit owns revision 100.
const anatomyReviewCatalog = getWorkoutCatalog(catalog, 99).map(e => beforeSurfaceReview(e, 98));
const review = load('../../docs/catalog-audit/anatomical-variety-review-2026-10-26.json');
const hash = data => createHash('sha256').update(data).digest('hex');
const dimensions = ['Metadata', 'FullLoop', 'Crop', 'LoopSeam', 'PlaybackSpeed', 'Mirroring', 'Travel', 'HoldFrame', 'Equipment', 'Workout'];

test('the floor is supplied by ten new identities and five reviewed anatomy corrections', () => {
  assert.equal(admissions.catalogRevision, 98);
  assert.equal(catalog.length, 934);
  assert.deepEqual(admissions.newExerciseIds, Array.from({ length: 10 }, (_, i) => 1720 + i));
  assert.deepEqual(admissions.anatomyCorrectionIds, [1069, 1071, 1209, 1216, 1292]);
  assert.equal(new Set(catalog.filter(e => admissions.newExerciseIds.includes(e.id)).map(getSessionMovementId)).size, 10);
  const baseline = structuredClone(catalog.filter(e => e.id < 1730 && !admissions.newExerciseIds.includes(e.id)));
  for (const e of baseline) if (admissions.anatomyCorrectionIds.includes(e.id))
    e.secondaryCanonicalGroups = e.secondaryCanonicalGroups.filter(g => g !== 'PelvicFloorAndPerineum');
  const inventory = getAnatomicalGroupVarietyInventory(baseline);
  assert.equal(inventory.length, 90);
  for (const shortage of admissions.baselineShortfalls)
    assert.equal(inventory.find(r => r.groupId === shortage.groupId).matchingExerciseCount, shortage.count);
  assert.ok(getAnatomicalGroupVarietyInventory(catalog).every(r => r.matchingExerciseCount >= 10));
});

test('every changed exercise has an explicit current review and complete workout replay', () => {
  assert.equal(review.status, 'PASS');
  const ids = [...admissions.newExerciseIds, ...admissions.anatomyCorrectionIds].sort((a, b) => a - b);
  assert.deepEqual(review.entries.map(e => e.id).sort((a, b) => a - b), ids);
  for (const entry of review.entries) {
    const e = anatomyReviewCatalog.find(e => e.id === entry.id);
    assert.equal(hash(JSON.stringify(e)), entry.metadataSha256, String(e.id));
    if (entry.kind === 'AnatomyCorrection') {
      const previous = structuredClone(e);
      previous.secondaryCanonicalGroups = previous.secondaryCanonicalGroups.filter(g => g !== 'PelvicFloorAndPerineum');
      assert.equal(hash(JSON.stringify(previous)), entry.previousMetadataSha256);
    }
    const assets = [e.video, ...(e.mode === 'Hold' ? [`exercise_hold_frames/exercise_${e.id}.png`] : [])];
    assert.deepEqual(Object.keys(entry.assets).sort(), assets.sort());
    for (const [asset, expected] of Object.entries(entry.assets))
      assert.equal(hash(readFileSync(new URL('../../NomadicMethod/Assets/' + asset, import.meta.url))), expected, asset);
    assert.ok(entry.method.length > 0 && entry.observedAction.length > 0 && entry.anatomySources.length > 0);
    assert.ok(Number.isFinite(Date.parse(entry.reviewedAt)));
    assert.deepEqual(Object.keys(entry.checks).sort(), [...dimensions].sort());
    for (const [dimension, check] of Object.entries(entry.checks)) {
      assert.ok(check.reason.length > 0);
      assert.ok(check.status === 'PASS' || (dimension === 'HoldFrame' && e.mode !== 'Hold' && check.status === 'NOT_APPLICABLE'), `${e.id}: ${dimension}`);
    }
    assert.ok(entry.evidence.length > 0);
    for (const evidence of entry.evidence) assert.ok(existsSync(new URL('../../' + evidence, import.meta.url)), evidence);
    assert.equal(entry.workoutReplay.rootId, e.id);
    assert.equal(entry.workoutReplay.realTimePlayback, true);
    assert.deepEqual(entry.workoutReplay.errors, []);
    assert.ok(entry.workoutReplay.blocks.length >= e.sequenceBlocks.length);
    for (const block of entry.workoutReplay.blocks) {
      assert.equal(block.id, e.id);
      assert.ok(block.elapsedMilliseconds >= 40000);
      if (e.mode !== 'Hold') assert.equal(block.sample.rate, 1);
    }
  }
});
