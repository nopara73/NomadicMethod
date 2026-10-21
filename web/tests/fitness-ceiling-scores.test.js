import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import test from 'node:test';
import { buildAudit, dimensions, renderScores } from '../../tools/generate-exercise-fitness-ceiling-scores.mjs';

const catalogSource = readFileSync(new URL('../../NomadicMethod/Assets/exercises.json', import.meta.url), 'utf8')
  .replaceAll('\r\n', '\n');
const catalog = JSON.parse(catalogSource);
const assessment = JSON.parse(readFileSync(new URL('../../tools/ExerciseFitnessCeilingScores.json', import.meta.url), 'utf8'));
const context = {
  catalogRevision: assessment.catalogRevision,
  catalogSha256: createHash('sha256').update(catalogSource).digest('hex'),
};
const cueConfiguration = JSON.parse(readFileSync(new URL('../../tools/ExerciseReactionCues.json', import.meta.url), 'utf8'));

test('every catalog exercise has one explicit provisional assessment across all ten dimensions', () => {
  const audit = buildAudit(catalog, assessment, context);
  assert.equal(audit.rows.length, catalog.length);
  assert.deepEqual(audit.rows.map(row => row.id), catalog.map(exercise => exercise.id).sort((a, b) => a - b));
  assert.ok(audit.rows.every(row => row.reviewStatus === 'provisional-metadata-assessment'));
  assert.ok(audit.rows.every(row => dimensions.every(key => Number.isInteger(row[key]))));
  const committed = readFileSync(new URL('../../docs/catalog-audit/acsm-fitness-ceiling-scores.csv', import.meta.url), 'utf8');
  assert.equal(renderScores(audit.rows), committed.replaceAll('\r\n', '\n'));
});

test('empty high tiers pass and aliases do not inflate reported movement counts', () => {
  const { summary } = buildAudit(catalog, assessment, context);
  assert.equal(summary.coveragePolicy, 'diagnostic-only; empty levels permitted');
  assert.equal('deficits' in summary, false);
  for (const key of dimensions) {
    assert.equal(summary.coverage[key][5].count, 0);
    for (let level = 1; level <= 5; level++) {
      const cell = summary.coverage[key][level];
      assert.equal(cell.count, new Set(cell.sessionMovementIds).size);
      assert.ok(cell.count <= cell.exerciseCount);
    }
  }
  assert.ok(summary.coverage.muscularEndurance[3].count < summary.coverage.muscularEndurance[3].exerciseCount);
});

test('new exercise IDs require an assessment rather than inheriting name-based ratings', () => {
  const unassessed = { ...catalog[0], id: 999999, name: 'Explosive Elite Sprint Jump Burpee' };
  assert.throws(() => buildAudit([...catalog, unassessed], assessment, context), /Missing explicit assessments: 999999/);
  assert.throws(() => buildAudit(catalog, assessment, { ...context, catalogSha256: 'changed' }), /Catalog changed/);
});

test('duplicate assignments and invalid or incomplete score vectors fail', () => {
  const copy = structuredClone(assessment);
  const profile = Object.values(copy.profiles)[0];
  profile.exerciseIds.push(profile.exerciseIds[0]);
  assert.throws(() => buildAudit(catalog, copy, context), /assessed more than once/);
  profile.exerciseIds.pop();
  profile.scores.power = 6;
  assert.throws(() => buildAudit(catalog, copy, context), /Invalid power score/);
  profile.scores.power = 1.5;
  assert.throws(() => buildAudit(catalog, copy, context), /Invalid power score/);
  delete profile.scores.power;
  assert.throws(() => buildAudit(catalog, copy, context), /all ten characteristics/);
});

test('reaction scores depend on an actual unpredictable cue and not cue-density levels', () => {
  const { rows } = buildAudit(catalog, assessment, context);
  for (const exercise of catalog) {
    assert.equal(cueConfiguration.cueLevelByExerciseId[String(exercise.id)] ?? 0, exercise.reactionCueLevel ?? 0);
    const row = rows.find(candidate => candidate.id === exercise.id);
    assert.equal(row.reactionTime, exercise.reactionCueLevel > 0 ? 2 : 0);
  }
  assert.equal(Object.keys(cueConfiguration.cueLevelByExerciseId).length, catalog.filter(exercise => exercise.reactionCueLevel > 0).length);
  const copy = structuredClone(assessment);
  const profile = Object.values(copy.profiles).find(candidate => candidate.exerciseIds.includes(134));
  profile.scores.reactionTime = 1;
  assert.throws(() => buildAudit(catalog, copy, context), /unpredictable external cue/);
});

test('still holds cannot acquire speed or ballistic power scores', () => {
  const copy = structuredClone(assessment);
  const profile = Object.values(copy.profiles).find(candidate => candidate.exerciseIds.includes(134));
  profile.scores.power = 1;
  assert.throws(() => buildAudit(catalog, copy, context), /Static exercise .* cannot train/);
});
