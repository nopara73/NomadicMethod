import fs from 'node:fs';
import { createHash } from 'node:crypto';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';
import { resolveReactionDrills } from '../web/reaction-drills.js';
import { calculateFitnessProfile } from '../web/fitness-profile.js';

export const dimensions = Object.freeze([
  'cardiorespiratoryEndurance', 'muscularStrength', 'muscularEndurance',
  'flexibility', 'agility', 'balance', 'coordination', 'power', 'reactionTime', 'speed',
]);

export const coveragePolicy = Object.freeze(JSON.parse(fs.readFileSync(
  new URL('./ExerciseFitnessCeilingCoveragePolicy.json', import.meta.url), 'utf8',
)));
if (coveragePolicy.schemaVersion !== 2 || coveragePolicy.matching !== 'explicit-trained-levels' ||
    coveragePolicy.identity !== 'sessionMovementId' ||
    !Number.isInteger(coveragePolicy.minimumDistinctSessionMovements) ||
    coveragePolicy.minimumDistinctSessionMovements < 1 ||
    JSON.stringify(coveragePolicy.levels) !== '[1,2,3,4,5]') {
  throw new Error('Invalid fitness-ceiling coverage policy.');
}

export function assertMinimumCoverage(summary) {
  if (summary.deficits.length) {
    throw new Error(`Fitness catalog requires at least ${coveragePolicy.minimumDistinctSessionMovements} distinct session movements that can train each characteristic-level combination:\n` +
      summary.deficits.map(cell => `${cell.characteristic} ${cell.level}: ${cell.count}/${cell.required} (missing ${cell.missing})`).join('\n'));
  }
}

// Validate and export explicit assessments. Never score a new exercise from its
// name, practice label, muscular demand, or a coverage quota.
export function buildAudit(catalog, assessment, { catalogRevision, catalogSha256, reactionProtocols = new Map() }) {
  if (!Array.isArray(catalog) || catalog.length === 0) {
    throw new Error('Exercise catalog is empty or invalid.');
  }
  if (assessment.scoringVersion !== 4 || assessment.representation !== 'trainable-level-sets' || assessment.status !== 'provisional' ||
      assessment.blockSeconds !== 45 ||
      JSON.stringify(assessment.dimensions) !== JSON.stringify(dimensions)) {
    throw new Error('Invalid version-4, explicit-level-set, provisional, 45-second assessment schema.');
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
        typeof profile.trainingConditions !== 'string' || profile.trainingConditions.trim().length < 30 ||
        !Array.isArray(profile.exerciseIds) || profile.exerciseIds.length === 0) {
      throw new Error(`Assessment profile ${profileName} needs a rationale, training conditions and exercise IDs.`);
    }
    if (JSON.stringify(Object.keys(profile.levels ?? {}).sort()) !==
        JSON.stringify([...dimensions].sort())) {
      throw new Error(`Assessment profile ${profileName} must specify all ten characteristics.`);
    }
    for (const dimension of dimensions) {
      const levels = profile.levels[dimension];
      if (!Array.isArray(levels) || levels.some((level, index) =>
        !Number.isInteger(level) || level < 1 || level > 5 || (index > 0 && level <= levels[index - 1]))) {
        throw new Error(`Invalid ${dimension} levels in ${profileName}; expected distinct ascending integers from 1 to 5, or an empty array.`);
      }
    }
    for (const id of profile.exerciseIds) {
      if (!byId.has(id)) throw new Error(`Unknown exercise ${id} in ${profileName}.`);
      if (assigned.has(id)) throw new Error(`Exercise ${id} is assessed more than once.`);
      const exercise = byId.get(id);
      if (profile.levels.reactionTime.length > 0 && !(exercise.reactionCueLevel > 0)) {
        throw new Error(`Exercise ${id} needs an unpredictable external cue for a reaction-time claim.`);
      }
      const protocol = reactionProtocols.get(id)?.protocol;
      const reactionSupport = protocol
        ? ({ Initiation: [1], TwoChoice: [1,3], FourChoice: [1,3,4], ContextChoice: [1,3,4,5] })[protocol]
        : exercise.reactionCueLevel > 0 ? [1,2] : [];
      if (!reactionSupport || profile.levels.reactionTime.some(level => !reactionSupport.includes(level)) ||
          (protocol && !profile.levels.reactionTime.includes(reactionSupport.at(-1)))) {
        throw new Error(`Exercise ${id} needs the matching actual reaction protocol for its supported levels.`);
      }
      if ((exercise.mode === 'Hold' || exercise.presentation === 'Still') &&
          (profile.levels.power.length > 0 || profile.levels.speed.length > 0)) {
        throw new Error(`Static exercise ${id} cannot train ballistic power or movement speed.`);
      }
      assigned.set(id, {
        scoringVersion: assessment.scoringVersion,
        catalogRevision,
        id,
        sessionMovementId: exercise.sessionMovementId > 0 ? exercise.sessionMovementId : id,
        name: exercise.name,
        ...profile.levels,
        reviewStatus: 'provisional-training-level-assessment',
        assessmentProfile: profileName,
        primaryCanonicalGroup: exercise.primaryCanonicalGroup,
        basis: profile.basis,
        trainingConditions: profile.trainingConditions,
      });
    }
  }
  const missing = catalog.filter(exercise => !assigned.has(exercise.id));
  if (missing.length) {
    throw new Error(`Missing explicit assessments: ${missing.map(exercise => exercise.id).join(', ')}.`);
  }
  const rows = [...assigned.values()].sort((a, b) => a.id - b.id);
  // Training-set coverage and usable heart-derived evidence are different.
  // Use the product calculation so this diagnostic cannot silently count ranges.
  const ratings = new Map(rows.map(row => [row.id, dimensions.map(key => row[key])]));
  const contributions = new Map(rows.map(row => [row.id,
    calculateFitnessProfile([byId.get(row.id)], { lastKeptExerciseIds: [row.id] }, ratings)]));
  const profileContributorCoverage = {
    recordsWithoutContribution: rows.filter(row => contributions.get(row.id).every(value => value === 0)).length,
    coverage: Object.fromEntries(dimensions.map((dimension, index) => [dimension,
      Object.fromEntries(coveragePolicy.levels.map(level => {
        const matching = rows.filter(row => contributions.get(row.id)[index] === level);
        const sessionMovementIds = [...new Set(matching.map(row => row.sessionMovementId))].sort((a, b) => a - b);
        return [level, { count: sessionMovementIds.length,
          exerciseIds: matching.map(row => row.id), sessionMovementIds }];
      })),
    ])),
  };
  const coverage = Object.fromEntries(dimensions.map(dimension => [dimension,
    Object.fromEntries([1, 2, 3, 4, 5].map(level => {
      const matching = rows.filter(row => row[dimension].includes(level));
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
  // Count explicitly supported training levels, deduplicating aliases in each
  // cell. Neither lower levels nor ratings for a new ID are inferred.
  const required = coveragePolicy.minimumDistinctSessionMovements;
  const deficits = dimensions.flatMap(characteristic => coveragePolicy.levels.map(level => ({
    characteristic, level, count: coverage[characteristic][level].count, required,
    missing: Math.max(0, required - coverage[characteristic][level].count),
  }))).filter(cell => cell.missing > 0);
  return {
    rows,
    summary: {
      scoringVersion: assessment.scoringVersion,
      representation: assessment.representation,
      status: assessment.status,
      assessedOn: assessment.assessedOn,
      blockSeconds: assessment.blockSeconds,
      catalogRevision,
      catalogSha256,
      exerciseCount: rows.length,
      profileCount: Object.keys(assessment.profiles).length,
      coveragePolicy,
      coverage,
      profileContributorCoverage,
      deficits,
    },
  };
}

export function renderScores(rows) {
  const headers = [
    'scoringVersion', 'catalogRevision', 'id', 'sessionMovementId', 'name',
    ...dimensions, 'reviewStatus', 'assessmentProfile', 'primaryCanonicalGroup', 'basis', 'trainingConditions',
  ];
  const csv = value => {
    const text = Array.isArray(value) ? value.join(';') : String(value ?? '');
    return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
  };
  return `${[
    headers.join(','),
    ...rows.map(row => headers.map(header => csv(row[header])).join(',')),
  ].join('\n')}\n`;
}

export function renderCoverageReport(summary) {
  const labels = {
    cardiorespiratoryEndurance: 'Cardiorespiratory endurance',
    muscularStrength: 'Muscular strength', muscularEndurance: 'Muscular endurance',
    flexibility: 'Flexibility', agility: 'Agility', balance: 'Balance',
    coordination: 'Coordination', power: 'Power', reactionTime: 'Reaction time', speed: 'Speed',
  };
  const missing = summary.deficits.reduce((total, cell) => total + cell.missing, 0);
  return [
    '# Fitness training-level catalog coverage', '',
    `Catalog revision ${summary.catalogRevision}; ${summary.exerciseCount} assessed exercise records.`, '',
    `Required: **at least ${coveragePolicy.minimumDistinctSessionMovements} distinct approved session movements that can train each characteristic-level combination**.`,
    'An exercise can support multiple explicitly assessed levels. A maximum alone does not imply lower-level coverage. Sides, directions, sets, aliases and renamed duplicates do not multiply a movement.', '',
    `Gate: **${summary.deficits.length ? 'FAIL' : 'PASS'}**. ${summary.deficits.length} of 50 cells are below the minimum; ${missing} characteristic-level placements are missing. A movement may train more than one characteristic.`, '',
    '| Characteristic | 1 | 2 | 3 | 4 | 5 |',
    '| --- | ---: | ---: | ---: | ---: | ---: |',
    ...dimensions.map(key => `| ${labels[key]} | ${coveragePolicy.levels.map(level => {
      const count = summary.coverage[key][level].count;
      return count < coveragePolicy.minimumDistinctSessionMovements ? `**${count}**` : count;
    }).join(' | ')} |`), '',
    '## Missing combinations', '',
    ...(summary.deficits.length ? [
      '| Characteristic | Level | Current | Required | Missing |',
      '| --- | ---: | ---: | ---: | ---: |',
      ...summary.deficits.map(cell => `| ${labels[cell.characteristic]} | ${cell.level} | ${cell.count} | ${cell.required} | ${cell.missing} |`),
    ] : ['Every combination meets the minimum.']), '',
    'Ratings are provisional NM training-level judgments for repeated 45-second blocks. Each supported level needs a meaningful training case using the actual movement and its documented adjustments. The coverage requirement supplies no evidence for a rating and never authorizes score inflation or unreviewed admissions.', '',
    '## Spiderweb contributors (diagnostic)', '',
    `${summary.profileContributorCoverage.recordsWithoutContribution} records have no precise strongest contribution. The following counts use the actual heart-profile calculation and deduplicate movement identities. Training ranges above remain useful for exercise selection but do not count here.`, '',
    '| Characteristic | 1 | 2 | 3 | 4 | 5 |',
    '| --- | ---: | ---: | ---: | ---: | ---: |',
    ...dimensions.map(key => `| ${labels[key]} | ${coveragePolicy.levels.map(level => summary.profileContributorCoverage.coverage[key][level].count).join(' | ')} |`), '',
    'These are diagnostics, not additional admission quotas. An empty cell must not be filled by narrowing an honest range or inventing precision.', '',
    'Generated by `tools/generate-exercise-fitness-ceiling-scores.mjs`. Use `--report-only` while sourcing; it does not pass the catalog gate.', '',
  ].join('\n');
}

function main() {
  const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
  const options = new Map([
    ['--catalog', path.join(root, 'NomadicMethod/Assets/exercises.json')],
    ['--assessments', path.join(root, 'tools/ExerciseFitnessCeilingScores.json')],
    ['--output', path.join(root, 'docs/catalog-audit/acsm-fitness-ceiling-scores.csv')],
    ['--coverage-output', path.join(root, 'docs/catalog-audit/acsm-fitness-ceiling-coverage.json')],
    ['--gap-output', path.join(root, 'docs/catalog-audit/acsm-fitness-ceiling-gaps.md')],
    ['--runtime-output', path.join(root, 'NomadicMethod/Assets/fitness_ratings.json')],
  ]);
  let check = false;
  let reportOnly = false;
  for (let index = 2; index < process.argv.length; index++) {
    const option = process.argv[index];
    if (option === '--check') { check = true; continue; }
    if (option === '--report-only') { reportOnly = true; continue; }
    if (!options.has(option) || !process.argv[index + 1] || process.argv[index + 1].startsWith('--')) {
      throw new Error(`Unknown option or missing value: ${option}`);
    }
    options.set(option, path.resolve(process.argv[++index]));
  }
  if (check && reportOnly) throw new Error('--check cannot bypass the catalog gate with --report-only.');
  const catalogSource = fs.readFileSync(options.get('--catalog'), 'utf8').replaceAll('\r\n', '\n');
  const catalogSha256 = createHash('sha256').update(catalogSource).digest('hex');
  const workoutSource = fs.readFileSync(path.join(root, 'web/workout.js'), 'utf8');
  const revision = workoutSource.match(/CURRENT_CATALOG_REVISION\s*=\s*(\d+)/);
  if (!revision) throw new Error('Could not read CURRENT_CATALOG_REVISION.');
  const catalog = JSON.parse(catalogSource);
  const reactionPath = path.join(root, 'NomadicMethod/Assets/reaction_drills.json');
  const reactionProtocols = fs.existsSync(reactionPath)
    ? resolveReactionDrills(JSON.parse(fs.readFileSync(reactionPath, 'utf8')), catalog) : new Map();
  const audit = buildAudit(
    catalog,
    JSON.parse(fs.readFileSync(options.get('--assessments'), 'utf8')),
    { catalogRevision: Number(revision[1]), catalogSha256, reactionProtocols },
  );
  const outputs = [
    [options.get('--runtime-output'), JSON.stringify({
      schemaVersion: 1,
      dimensions,
      ratings: Object.fromEntries(audit.rows.map(row =>
        [row.id, dimensions.map(dimension => row[dimension])])),
    }) + '\n'],
    [options.get('--output'), renderScores(audit.rows)],
    [options.get('--coverage-output'), `${JSON.stringify(audit.summary, null, 2)}\n`],
    [options.get('--gap-output'), renderCoverageReport(audit.summary)],
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
  if (!reportOnly) assertMinimumCoverage(audit.summary);
  else process.stdout.write(`REPORT ONLY: ${audit.summary.deficits.length} deficient combinations; this is not a passing catalog gate.\n`);
  process.stdout.write(`${check ? 'Verified' : 'Wrote'} ${audit.rows.length} explicit provisional exercise assessments for revision ${revision[1]}.\n`);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
