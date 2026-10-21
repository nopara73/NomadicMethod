import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const dimensions = [
  'cardiorespiratoryEndurance',
  'muscularStrength',
  'muscularEndurance',
  'flexibility',
  'agility',
  'balance',
  'coordination',
  'power',
  'reactionTime',
  'speed',
];

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, '..');
const defaultCatalogPath = path.join(repositoryRoot, 'NomadicMethod', 'Assets', 'exercises.json');
const defaultOutputPath = path.join(
  repositoryRoot,
  'docs',
  'catalog-audit',
  'acsm-fitness-ceiling-scores.csv',
);
const defaultOverridePath = path.join(scriptDirectory, 'ExerciseFitnessCeilingOverrides.json');

function argumentValue(name, fallback) {
  const index = process.argv.indexOf(name);
  return index >= 0 && process.argv[index + 1] ? path.resolve(process.argv[index + 1]) : fallback;
}

const catalogPath = argumentValue('--catalog', defaultCatalogPath);
const outputPath = argumentValue('--output', defaultOutputPath);
const overridePath = argumentValue('--overrides', defaultOverridePath);
const checkOnly = process.argv.includes('--check');

function normalize(value) {
  return String(value ?? '')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[^a-zA-Z0-9]+/g, ' ')
    .trim()
    .toLowerCase();
}

function csv(value) {
  const text = String(value ?? '');
  return /[",\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

function maximum(current, proposed) {
  return Math.max(current, Math.min(5, proposed));
}

function scoreExercise(exercise) {
  const name = normalize(exercise.name);
  const practice = normalize(exercise.practice);
  const motion = normalize(exercise.motionProfile);
  const text = `${name} ${practice} ${motion}`;
  const activity = `${name} ${practice}`;
  const demand = Number(exercise.muscularDemand);
  const isRepetition = exercise.mode === 'Repetition';
  const isHold = exercise.mode === 'Hold';
  const isSupported = /\b(?:wall|chair|hand|forearm) supported\b|\bsupported\b/.test(text);
  const isSingleLeg = /\b(?:single|one) leg\b|\bpistol\b|\bhip airplane\b|\btree pose\b/.test(text);
  const isPlyometric = /\bplyometric\b|\bjump\b|\bhop\b|\bbound\b|\bskipping\b|\bsprint\b|\bclapping push up\b/.test(text);
  const isWholeBodyConditioning = /\bcardio\b|\baerobic\b|\baerobics\b|\blocomotor conditioning\b|\brunning drills\b|\brope free\b|\bjumping jack\b|\bmountain climber\b|\bburpee\b|\bplank jack\b/.test(activity);
  const isResistanceIntent = /\bstrength\b|\bstrengthening\b|\bisometric\b|\bself resistance\b|\bbodyweight\b|\bcalf strengthening\b|\bhip stability\b|\bglute strengthening\b|\bpilates\b|\bconditioning\b/.test(text);
  const isMobilityIntent = /\bstretch\b|\bstretching\b|\bmobility\b|\bfunctional range\b|\bjoint loosening\b|\bpnf\b|\bcircle\b/.test(text)
    || (/\brehabilitation\b/.test(practice) && demand === 0);
  const isBalanceIntent = /\bbalance\b|\bpostural control\b|\bgait retraining\b|\btandem\b|\bheel to toe\b|\btiptoe walking\b|\bstick landing\b/.test(text);
  const scores = Object.fromEntries(dimensions.map((dimension) => [dimension, 0]));
  const basis = new Set();

  const assign = (dimension, value, signal) => {
    const next = maximum(scores[dimension], value);
    if (next !== scores[dimension]) {
      scores[dimension] = next;
      if (signal) basis.add(signal);
    }
  };

  // Cardiorespiratory endurance: sustained rhythmic whole-body work. A single
  // 45-second block limits the ceiling; only the hardest repeatable intervals
  // reach level 4 and no catalog exercise is assigned level 5.
  if (isRepetition && demand >= 1 && /\bconditioning\b|\bcalisthenics\b|\bboxing\b|\bdance\b|\bbhangra\b|\bcapoeira\b|\bmarch\b|\bstep\b|\bsquat\b|\blunge\b/.test(activity)) {
    assign('cardiorespiratoryEndurance', 1, 'rhythmic-loading');
  }
  if (isWholeBodyConditioning || /\brun\b|\bjump rope\b|\bskater\b|\bpower skipping\b/.test(activity)) {
    assign('cardiorespiratoryEndurance', 2, 'whole-body-conditioning');
  }
  if (/\bsprint\b|\bhigh knee run\b|\brunning high knee\b|\bhopping high knee\b|\bmountain climber\b|\btuck jump\b|\bjumping lunge\b|\bpower jack\b|\bsquat jack\b|\bjump squat\b|\bplank hops?\b|\bburpee\b/.test(activity)) {
    assign('cardiorespiratoryEndurance', 3, 'vigorous-interval');
  }
  if (/\bsprinting\b|\bburpees with plank jacks\b|\bburpee broad jumps\b|\bknee push up burpees\b/.test(activity)) {
    assign('cardiorespiratoryEndurance', 4, 'maximal-repeatable-interval');
  }

  // Strength: maximum force potential for the directly loaded movement. Low
  // resistance conditioning is capped below demanding unilateral calisthenics.
  if (demand === 1 && isResistanceIntent && !isWholeBodyConditioning) {
    assign('muscularStrength', 1, 'meaningful-resistance');
  }
  if (demand === 2) {
    assign('muscularStrength', 2, 'force-limited-block');
  }
  if (/\bself resist(?:ed|ance)\b/.test(text)) {
    assign('muscularStrength', 2, 'scalable-self-resistance');
  }
  if (/\bsingle leg (?:squat|deadlift|calf raise)\b|\bpistol squat\b|\breverse nordic\b|\barcher push up\b|\bpike push up\b|\bdiamond push up\b|\bone arm high plank\b|\blong lever plank\b/.test(text)) {
    assign('muscularStrength', 3, 'advanced-bodyweight-leverage');
  }
  if (/\bpistol squat with bottom pause\b|\bbottom pistol squat hold\b|\bclapping push up\b|\bside lying one arm push up\b/.test(text)) {
    assign('muscularStrength', 4, 'high-skill-bodyweight-strength');
  }

  // Muscular endurance: ability of the directly loaded muscles to sustain the
  // fixed movement or hold. Forty-five seconds is treated as one repeatable set.
  if (demand === 1) {
    assign('muscularEndurance', 1, 'local-loading');
  }
  if (demand === 2) {
    assign('muscularEndurance', 2, 'local-fatigue');
  }
  if (demand === 1 && (isHold || (isRepetition && isResistanceIntent))) {
    assign('muscularEndurance', 2, 'sustained-local-work');
  }
  if (demand === 2 && (isHold || isRepetition)) {
    assign('muscularEndurance', 3, 'hard-45-second-set');
  }
  if (demand === 2 && /\bburpee\b|\bmountain climber\b|\bplank jack\b|\bplank hops?\b|\bup down plank\b|\bhover push up\b/.test(text)) {
    assign('muscularEndurance', 4, 'advanced-whole-body-endurance');
  }

  // Flexibility: joint-specific usable range. Incidental movement receives no
  // score; explicit mobility work begins at level 1.
  if (isMobilityIntent || (/\byoga\b/.test(practice) && !/\bbreath/.test(text))) {
    assign('flexibility', 1, 'range-of-motion-practice');
  }
  if (isMobilityIntent && /\bstretch\b|\brotation\b|\bside bend\b|\bflexion\b|\bextension\b|\bcircle\b|\bswing\b|\breach\b|\bslide\b|\bfold\b/.test(text)) {
    assign('flexibility', 2, 'directed-mobility');
  }
  if ((isHold && /\bstretch\b|\bmobility\b|\byoga\b/.test(text)) || /\bdeep squat mobility\b|\bcalf stretch\b|\bhamstring stretch\b|\bhip flexor stretch\b|\badductor stretch\b|\bthoracic rotation\b/.test(text)) {
    assign('flexibility', 3, 'sustained-end-range');
  }
  if (/\bstanding split\b|\bmiddle split\b|\bfront split\b|\bpancake\b|\bwheel pose\b|\bpigeon stretch\b|\bhands on floor forward fold\b|\bdeep squat thoracic rotation\b/.test(text)) {
    assign('flexibility', 4, 'advanced-end-range');
  }

  // Agility uses the traditional ACSM sense: rapid, accurate repositioning.
  // Predetermined drills can score here even though they do not train reaction.
  if (/\bfootwork\b|\bside step\b|\bskater\b|\bcapoeira ginga\b|\bbackward running\b|\bsideways\b/.test(activity)) {
    assign('agility', 1, 'body-repositioning');
  }
  if (/\bshuffle\b|\bcrossover\b|\bdiagonal\b|\bzig zag\b|\blateral (?:bound|hop|step)\b|\bturn\b|\bpivot\b|\bspin\b|\bcurtsy\b/.test(activity)) {
    assign('agility', 2, 'planned-direction-change');
  }
  if (/\b180 degree\b|\bquarter turn jump\b|\brotational hop\b|\bmulti directional\b|\bline hops?\b/.test(activity)) {
    assign('agility', 3, 'fast-multiplanar-repositioning');
  }

  // Balance is task-specific. Scores describe the demonstrated stance and
  // perturbation demands rather than a transferable general balance capacity.
  if (/\bweight shift\b|\bnarrow stance\b|\btiptoe\b|\bheel raise\b|\blunge\b|\bkick\b/.test(text)) {
    assign('balance', 1, 'moving-base-control');
  }
  if (isBalanceIntent || isSingleLeg || /\bwarrior (?:ii|iii|2|3)\b|\bairplane\b|\btree pose\b/.test(text)) {
    assign('balance', 2, 'reduced-base-control');
  }
  if (/\beyes closed\b|\bhead turn\b|\bgaze stabilization\b|\bvestibular\b/.test(text)) {
    assign('balance', 3, 'sensory-challenged-balance');
  }
  if (isSingleLeg && /\bdeadlift\b|\bpistol\b|\bhop\b|\bjump\b|\brotat\b|\bwindmill\b/.test(text)) {
    assign('balance', 3, 'dynamic-single-leg-control');
  }
  if (/\bsingle leg rotational hops\b|\bsingle leg quarter turn jumps\b/.test(text)) {
    assign('balance', 4, 'rotational-single-leg-landing');
  }
  if (isSupported && !/\bunsupported\b/.test(text)) {
    scores.balance = Math.min(scores.balance, 1);
  }

  // Coordination requires a nontrivial temporal or inter-limb pattern. Merely
  // alternating sides is not enough unless the movement combines actions.
  if (/\bcoordination\b|\btracking\b|\bgaze\b|\boculomotor\b|\bvestibular\b|\bhand seal\b|\bfinger wave\b/.test(text)) {
    assign('coordination', 2, 'explicit-sensorimotor-pattern');
  }
  if (/\bdance\b|\bballet\b|\bbhangra\b|\bcapoeira\b|\bkarate\b|\btaekwondo\b|\bmuay thai\b|\btai chi\b|\bqigong\b|\btutting\b|\bpopping and waving\b/.test(text)) {
    assign('coordination', 2, 'sequenced-movement-form');
  }
  if (/\balternating\b/.test(name) && /\bwith\b|\bto\b|\bcross\b|\bopposite\b|\boverhead\b|\bunder\b/.test(name)) {
    assign('coordination', 1, 'combined-alternating-actions');
  }
  if (/\bcross body\b|\bopposite arm\b|\bopposite leg\b|\bsequence\b|\bcombo\b|\bclock face\b|\bfigure eight\b|\b180 degree\b/.test(text)) {
    assign('coordination', 3, 'complex-interlimb-sequence');
  }
  if (/\btutting box sequence\b|\bpopping and waving\b|\bforty four hand seal\b/.test(text)) {
    assign('coordination', 4, 'advanced-sequenced-coordination');
  }

  // Power: rapid force production. Ordinary cyclical jacks are excluded unless
  // the exercise explicitly adds a forceful jump or landing demand.
  if (/\bpunch\b|\bkick\b|\bstomp\b/.test(activity)) {
    assign('power', 1, 'unloaded-ballistic-action');
  }
  if (isPlyometric && !/^jumping jacks$/.test(name)) {
    assign('power', 2, 'ballistic-action');
  }
  if (/\bpower\b|\btuck jump\b|\bbroad jump\b|\bvertical jump\b|\bsquat jump\b|\bjumping lunge\b|\bhinge jump\b|\bclapping push up\b|\bsprinting\b/.test(text)) {
    assign('power', 3, 'high-force-ballistic-action');
  }
  if ((isSingleLeg && /\bjump\b|\bhop\b/.test(text)) || /\bburpee broad jumps\b|\bclapping push ups\b/.test(text)) {
    assign('power', 4, 'advanced-ballistic-control');
  }

  // Reaction time remains zero: every current exercise follows a known loop and
  // contains no unpredictable external cue to which the user must respond.

  // Speed: rapid cyclic or ballistic movement. Only actual sprinting maps to the
  // level-5 flying-sprint benchmark used by this provisional standard.
  if (/\bjumping jack\b|\bpunch\b|\bkick\b|\bfast feet\b|\bmarch\b/.test(activity)) {
    assign('speed', 1, 'rapid-limb-action');
  }
  if (/\bhop\b|\bjump\b|\bbound\b|\bshuffle\b|\bskip\b|\bboxing\b|\bkarate\b|\btaekwondo\b|\bmuay thai\b|\brunning drills\b/.test(activity)) {
    assign('speed', 2, 'rapid-whole-body-action');
  }
  if (/\bsprinting\b|\bbackward running\b|\bhigh knee run\b|\brunning high knee\b|\bhopping high knee\b|\bmountain climber\b|\bburpee\b|\bpower skipping\b/.test(activity)) {
    assign('speed', 3, 'high-cadence-locomotion');
  }
  if (/\bsprinting\b/.test(activity)) {
    assign('speed', 5, 'maximal-sprinting');
  }

  return { scores, basis: [...basis].sort() };
}

const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf8'));
const overrideDocument = JSON.parse(fs.readFileSync(overridePath, 'utf8'));
const workoutSource = fs.readFileSync(path.join(repositoryRoot, 'web', 'workout.js'), 'utf8');
const revisionMatch = workoutSource.match(/CURRENT_CATALOG_REVISION\s*=\s*(\d+)/);
if (!revisionMatch) throw new Error('Could not read CURRENT_CATALOG_REVISION from web/workout.js.');
const catalogRevision = Number(revisionMatch[1]);

if (!Array.isArray(catalog) || catalog.length === 0) throw new Error('Exercise catalog is empty or invalid.');
const ids = catalog.map((exercise) => Number(exercise.id));
if (new Set(ids).size !== ids.length) throw new Error('Exercise catalog contains duplicate IDs.');

const knownIds = new Set(ids.map(String));
for (const overrideId of Object.keys(overrideDocument.overrides)) {
  if (!knownIds.has(overrideId)) throw new Error(`Score override ${overrideId} is not present in the catalog.`);
}

const rows = catalog
  .map((exercise) => {
    const derived = scoreExercise(exercise);
    const override = overrideDocument.overrides[String(exercise.id)];
    const scores = override ? override.scores : derived.scores;
    for (const dimension of dimensions) {
      if (!Number.isInteger(scores[dimension]) || scores[dimension] < 0 || scores[dimension] > 5) {
        throw new Error(`Exercise ${exercise.id} has an invalid ${dimension} score.`);
      }
    }
    return {
      scoringVersion: overrideDocument.scoringVersion,
      catalogRevision,
      id: Number(exercise.id),
      name: exercise.name,
      ...scores,
      reviewStatus: override ? 'anchor-reviewed' : 'rule-derived',
      basis: override ? override.reason : derived.basis.join(';'),
    };
  })
  .sort((left, right) => left.id - right.id);

const headers = [
  'scoringVersion',
  'catalogRevision',
  'id',
  'name',
  ...dimensions,
  'reviewStatus',
  'basis',
];
const generated = [
  headers.join(','),
  ...rows.map((row) => headers.map((header) => csv(row[header])).join(',')),
  '',
].join('\n');

if (checkOnly) {
  if (!fs.existsSync(outputPath)) throw new Error(`Score file is missing: ${outputPath}`);
  const existing = fs.readFileSync(outputPath, 'utf8').replaceAll('\r\n', '\n');
  if (existing !== generated) {
    throw new Error('Fitness-ceiling scores are stale. Run the generator without --check.');
  }
  process.stdout.write(`Verified ${rows.length} exercise fitness-ceiling rows for catalog revision ${catalogRevision}.\n`);
} else {
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, generated, 'utf8');
  process.stdout.write(`Wrote ${rows.length} exercise fitness-ceiling rows to ${outputPath}.\n`);
}
