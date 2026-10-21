import fs from 'node:fs';
import { createHash } from 'node:crypto';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

export const dimensions = Object.freeze([
  'cardiorespiratoryEndurance', 'muscularStrength', 'muscularEndurance',
  'flexibility', 'agility', 'balance', 'coordination', 'power', 'reactionTime', 'speed',
]);

// Validate and export explicit assessments. Never score a new exercise from its
// name, practice label, muscular demand, or a coverage quota.
export function buildAudit(catalog, assessment, { catalogRevision, catalogSha256 }) {
  if (!Array.isArray(catalog) || catalog.length === 0) {
    throw new Error('Exercise catalog is empty or invalid.');
  }
  if (assessment.scoringVersion !== 3 || assessment.status !== 'provisional' ||
      assessment.blockSeconds !== 45 ||
      JSON.stringify(assessment.dimensions) !== JSON.stringify(dimensions)) {
    throw new Error('Invalid version-3, provisional, 45-second assessment schema.');
  }
  if (assessment.catalogRevision !== catalogRevision ||
      assessment.catalogSha256 !== catalogSha256) {
    throw new Error('Catalog changed: reassess affected exercises before updating the assessment snapshot.');
  }
  const byId = new Map(catalog.map(exercise => [exercise.id, exercise]));
  if (byId.size !== catalog.length || catalog.some(exercise =>
    !Number.isInteger(exercise.id) || exercise.id <= 0)) {
    throw new Error('Catalog exercise IDs must be distinct positive integers.');
  }
  const assigned = new Map();
  if (!assessment.profiles || typeof assessment.profiles !== 'object' ||
      Array.isArray(assessment.profiles)) {
    throw new Error('Explicit assessment profiles are required.');
  }
  for (const [profileName, profile] of Object.entries(assessment.profiles)) {
    if (typeof profile.basis !== 'string' || profile.basis.trim().length < 30 ||
        !Array.isArray(profile.exerciseIds) || profile.exerciseIds.length === 0) {
      throw new Error(`Assessment profile ${profileName} needs a rationale and exercise IDs.`);
    }
    if (JSON.stringify(Object.keys(profile.scores ?? {}).sort()) !==
        JSON.stringify([...dimensions].sort())) {
      throw new Error(`Assessment profile ${profileName} must specify all ten characteristics.`);
    }
    for (const dimension of dimensions) {
      const score = profile.scores[dimension];
      if (!Number.isInteger(score) || score < 0 || score > 5) {
        throw new Error(`Invalid ${dimension} score in ${profileName}; expected an integer from 0 to 5.`);
      }
    }
    for (const id of profile.exerciseIds) {
      if (!byId.has(id)) throw new Error(`Unknown exercise ${id} in ${profileName}.`);
      if (assigned.has(id)) throw new Error(`Exercise ${id} is assessed more than once.`);
      const exercise = byId.get(id);
      if (profile.scores.reactionTime > 0 && !(exercise.reactionCueLevel > 0)) {
        throw new Error(`Exercise ${id} needs an unpredictable external cue for a reaction-time claim.`);
      }
      if ((exercise.mode === 'Hold' || exercise.presentation === 'Still') &&
          (profile.scores.power > 0 || profile.scores.speed > 0)) {
        throw new Error(`Static exercise ${id} cannot train ballistic power or movement speed.`);
      }
      assigned.set(id, {
        scoringVersion: assessment.scoringVersion,
        catalogRevision,
        id,
        sessionMovementId: exercise.sessionMovementId > 0 ? exercise.sessionMovementId : id,
        name: exercise.name,
        ...profile.scores,
        reviewStatus: 'provisional-metadata-assessment',
        assessmentProfile: profileName,
        primaryCanonicalGroup: exercise.primaryCanonicalGroup,
        basis: profile.basis,
      });
    }
  }
  const missing = catalog.filter(exercise => !assigned.has(exercise.id));
  if (missing.length) {
    throw new Error(`Missing explicit assessments: ${missing.map(exercise => exercise.id).join(', ')}.`);
  }
  const rows = [...assigned.values()].sort((a, b) => a.id - b.id);
  const coverage = Object.fromEntries(dimensions.map(dimension => [dimension,
    Object.fromEntries([1, 2, 3, 4, 5].map(level => {
      const matching = rows.filter(row => row[dimension] === level);
      const sessionMovementIds = [...new Set(matching.map(row => row.sessionMovementId))]
        .sort((a, b) => a - b);
      return [level, {
        count: sessionMovementIds.length,
        exerciseCount: matching.length,
        exerciseIds: matching.map(row => row.id),
        sessionMovementIds,
      }];
    })),
  ]));
  // Empty levels are informative. There is no population minimum, deficit
  // calculation, or rule that promotes ratings to populate an empty cell.
  return {
    rows,
    summary: {
      scoringVersion: assessment.scoringVersion,
      status: assessment.status,
      assessedOn: assessment.assessedOn,
      blockSeconds: assessment.blockSeconds,
      catalogRevision,
      catalogSha256,
      exerciseCount: rows.length,
      profileCount: Object.keys(assessment.profiles).length,
      coveragePolicy: 'diagnostic-only; empty levels permitted',
      coverage,
    },
  };
}

export function renderScores(rows) {
  const headers = [
    'scoringVersion', 'catalogRevision', 'id', 'sessionMovementId', 'name',
    ...dimensions, 'reviewStatus', 'assessmentProfile', 'primaryCanonicalGroup', 'basis',
  ];
  const csv = value => {
    const text = String(value ?? '');
    return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
  };
  return `${[
    headers.join(','),
    ...rows.map(row => headers.map(header => csv(row[header])).join(',')),
  ].join('\n')}\n`;
}

function main() {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
  const options = new Map([
    ['--catalog', path.join(root, 'NomadicMethod/Assets/exercises.json')],
    ['--assessments', path.join(root, 'tools/ExerciseFitnessCeilingScores.json')],
    ['--output', path.join(root, 'docs/catalog-audit/acsm-fitness-ceiling-scores.csv')],
    ['--coverage-output', path.join(root, 'docs/catalog-audit/acsm-fitness-ceiling-coverage.json')],
  ]);
  let check = false;
  for (let index = 2; index < process.argv.length; index++) {
    const option = process.argv[index];
    if (option === '--check') { check = true; continue; }
    if (!options.has(option) || !process.argv[index + 1] || process.argv[index + 1].startsWith('--')) {
      throw new Error(`Unknown option or missing value: ${option}`);
    }
    options.set(option, path.resolve(process.argv[++index]));
  }
  const catalogSource = fs.readFileSync(options.get('--catalog'), 'utf8').replaceAll('\r\n', '\n');
  const catalogSha256 = createHash('sha256').update(catalogSource).digest('hex');
  const workoutSource = fs.readFileSync(path.join(root, 'web/workout.js'), 'utf8');
  const revision = workoutSource.match(/CURRENT_CATALOG_REVISION\s*=\s*(\d+)/);
  if (!revision) throw new Error('Could not read CURRENT_CATALOG_REVISION.');
  const audit = buildAudit(
    JSON.parse(catalogSource),
    JSON.parse(fs.readFileSync(options.get('--assessments'), 'utf8')),
    { catalogRevision: Number(revision[1]), catalogSha256 },
  );
  const outputs = [
    [options.get('--output'), renderScores(audit.rows)],
    [options.get('--coverage-output'), `${JSON.stringify(audit.summary, null, 2)}\n`],
  ];
  for (const [output, content] of outputs) {
    if (check) {
      if (!fs.existsSync(output) || fs.readFileSync(output, 'utf8').replaceAll('\r\n', '\n') !== content) {
        throw new Error(`Fitness-ceiling audit is stale or missing: ${output}`);
      }
    } else {
      fs.mkdirSync(path.dirname(output), { recursive: true });
      fs.writeFileSync(output, content, 'utf8');
    }
  }
  process.stdout.write(`${check ? 'Verified' : 'Wrote'} ${audit.rows.length} explicit provisional exercise assessments for revision ${revision[1]}.\n`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
