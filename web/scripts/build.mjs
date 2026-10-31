import { mapConcurrent } from "../../tools/map-concurrent.mjs";
import { checkFixedPositionReviews } from "../../tools/check-exercise-position-reviews.mjs";
import { checkDurationPolicy } from "./workout-duration-policy.mjs";
import { createHash } from "node:crypto";
import { cp, mkdir, readFile, readdir, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { resolveReactionDrills } from "../reaction-drills.js";

import {
  CURRENT_CATALOG_REVISION,
  MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  MINIMUM_SESSION_MOVEMENTS_PER_ANATOMICAL_GROUP,
  getAnatomicalGroupVarietyInventory,
  findHardFloorCategoryCoverageDeficiencies,
  findMuscularDemandCoverageDeficiencies,
  findSoleWallContactRequiredCatalogDeficiencies,
  findWallRequiredCatalogDeficiencies,
  findElevationCatalogDeficiencies,
  findLocomotionCatalogDeficiencies,
  findFloorCatalogDeficiencies,
  findFloorAndWallCatalogDeficiencies,
  findFloorAndLocomotionCatalogDeficiencies,
  findWorkoutModifierMaterialityDeficiencies,
  ACCEPTED_MATERIALITY_EXCEPTIONS,
  isAcceptedMaterialityDeficiency,
  findWorkoutModifierPairCoverageDeficiencies,
  findWorkoutProfileLineupDeficiencies,
  findCompleteWorkoutProfileLineupDeficiencies,
  isModifierMetadataComplete,
  isSessionMovementMetadataValid,
} from "../workout.js";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(webRoot, "..");
const outputRoot = path.resolve(webRoot, "dist");
const moduleOutputs = new Map();

if (!outputRoot.startsWith(`${webRoot}${path.sep}`)) {
  throw new Error("Refusing to build outside the web project.");
}

await checkDurationPolicy();
await checkFixedPositionReviews();

await rm(outputRoot, { recursive: true, force: true });
await mkdir(outputRoot, { recursive: true });
for (const stem of ['workout-protocol-history', 'reaction-drills', 'reaction-drill-view', 'fitness-profile', 'completion-profile', 'exercise-feedback']) {
  let source = await readFile(path.join(webRoot, `${stem}.js`), 'utf8');
  for (const [dependency, output] of moduleOutputs)
    source = source.replaceAll(`./${dependency}.js`, `./${output}`);
  const name = fingerprintedName(stem, 'js', source);
  moduleOutputs.set(stem, name);
  await writeFile(path.join(outputRoot, name), source);
}
const reactionSource = await readFile(path.join(repositoryRoot, 'NomadicMethod/Assets/reaction_drills.json'), 'utf8');
const reactionOutputName = fingerprintedName('reaction-drills', 'json', reactionSource);
await mkdir(path.join(outputRoot, 'data'), { recursive: true });
await writeFile(path.join(outputRoot, 'data', reactionOutputName), reactionSource);
const fitnessSource = await readFile(path.join(repositoryRoot, 'NomadicMethod/Assets/fitness_ratings.json'), 'utf8');
const fitnessOutputName = fingerprintedName('fitness-ratings', 'json', fitnessSource);
await writeFile(path.join(outputRoot, 'data', fitnessOutputName), fitnessSource);
for (const directory of ['reaction_videos', 'reaction_previews']) {
  await copyDirectory(path.join(repositoryRoot, 'NomadicMethod/Assets', directory), path.join(outputRoot, 'assets', directory));
}

const durationSource = await readFile(path.join(webRoot, "workout-duration.js"), "utf8");
const durationOutputName = fingerprintedName("workout-duration", "js", durationSource);
await writeFile(path.join(outputRoot, durationOutputName), durationSource, "utf8");
const lightCadenceSource = await readFile(path.join(webRoot, "light-cadence.js"), "utf8");
const lightCadenceOutputName = fingerprintedName("light-cadence", "js", lightCadenceSource);
await writeFile(path.join(outputRoot, lightCadenceOutputName), lightCadenceSource, "utf8");
const workoutSource = (await readFile(path.join(webRoot, "workout.js"), "utf8"))
  .replace('./workout-protocol-history.js', `./${moduleOutputs.get('workout-protocol-history')}`)
  .replace('./workout-duration.js', `./${durationOutputName}`)
  .replace('./light-cadence.js', `./${lightCadenceOutputName}`)
  .replace('./completion-profile.js', `./${moduleOutputs.get('completion-profile')}`)
  .replace('./fitness-profile.js', `./${moduleOutputs.get('fitness-profile')}`);
if (!workoutSource.includes(`./${durationOutputName}`) ||
    workoutSource.includes("./workout-duration.js") ||
    !workoutSource.includes(`./${lightCadenceOutputName}`) ||
    workoutSource.includes('./light-cadence.js')) {
  throw new Error("Could not content-address the shared Light cadence policy.");
}
if (!workoutSource.includes(moduleOutputs.get('fitness-profile')) ||
    workoutSource.includes('./fitness-profile.js')) {
  throw new Error("Could not content-address the shared fitness policy.");
}
if (!workoutSource.includes(moduleOutputs.get('workout-protocol-history')) ||
    workoutSource.includes('./workout-protocol-history.js')) {
  throw new Error("Could not content-address the saved workout protocol history.");
}
const workoutOutputName = fingerprintedName("workout", "js", workoutSource);
await writeFile(path.join(outputRoot, workoutOutputName), workoutSource, "utf8");
const preparationWorkerSource = await readFile(
  path.join(webRoot, "workout-preparation-worker.js"),
  "utf8",
);
const fingerprintedPreparationWorkerSource = preparationWorkerSource.replace(
  'from "./workout.js";',
  `from "./${workoutOutputName}";`,
).replace(
  'from "./fitness-profile.js";',
  `from "./${moduleOutputs.get('fitness-profile')}";`,
);
if (fingerprintedPreparationWorkerSource === preparationWorkerSource ||
    !fingerprintedPreparationWorkerSource.includes(workoutOutputName) ||
    !fingerprintedPreparationWorkerSource.includes(moduleOutputs.get('fitness-profile')) ||
    fingerprintedPreparationWorkerSource.includes('./fitness-profile.js')) {
  throw new Error("Could not content-address the workout preparation worker.");
}
const preparationWorkerOutputName = fingerprintedName(
  "workout-preparation-worker",
  "js",
  fingerprintedPreparationWorkerSource,
);
await writeFile(
  path.join(outputRoot, preparationWorkerOutputName),
  fingerprintedPreparationWorkerSource,
  "utf8",
);

const hardFloorIconPath = path.join(
  repositoryRoot,
  "NomadicMethod",
  "Resources",
  "drawable-xxhdpi",
  "ic_hard_floor.png",
);
const wallNoSoleIconPath = path.join(
  repositoryRoot,
  "NomadicMethod",
  "Resources",
  "drawable-xxhdpi",
  "ic_wall_no_sole.png",
);
const lightWorkoutIconPath = path.join(
  repositoryRoot,
  "NomadicMethod",
  "Resources",
  "drawable-xxhdpi",
  "ic_light_workout.png",
);
const hardFloorIconSource = await readFile(hardFloorIconPath);
const wallNoSoleIconSource = await readFile(wallNoSoleIconPath);
const lightWorkoutIconSource = await readFile(lightWorkoutIconPath);
const hardFloorIconName = fingerprintedName(
  "ic_hard_floor",
  "png",
  hardFloorIconSource,
);
const wallNoSoleIconName = fingerprintedName(
  "ic_wall_no_sole",
  "png",
  wallNoSoleIconSource,
);
const lightWorkoutIconName = fingerprintedName(
  "ic_light_workout",
  "png",
  lightWorkoutIconSource,
);
const stylesTemplate = await readFile(path.join(webRoot, "styles.css"), "utf8");
const stylesSource = stylesTemplate
  .replaceAll("./assets/ic_hard_floor.png", `./assets/${hardFloorIconName}`)
  .replaceAll("./assets/ic_wall_no_sole.png", `./assets/${wallNoSoleIconName}`)
  .replaceAll("./assets/ic_light_workout.png", `./assets/${lightWorkoutIconName}`);
if (stylesSource === stylesTemplate ||
    stylesSource.includes("./assets/ic_hard_floor.png") ||
    stylesSource.includes("./assets/ic_wall_no_sole.png") ||
    stylesSource.includes("./assets/ic_light_workout.png")) {
  throw new Error("Could not fingerprint the modifier icons.");
}
const stylesOutputName = fingerprintedName("styles", "css", stylesSource);
await writeFile(path.join(outputRoot, stylesOutputName), stylesSource, "utf8");
await copyInto(
  hardFloorIconPath,
  path.join(outputRoot, "assets", hardFloorIconName),
);
await copyInto(
  wallNoSoleIconPath,
  path.join(outputRoot, "assets", wallNoSoleIconName),
);
await copyInto(
  lightWorkoutIconPath,
  path.join(outputRoot, "assets", lightWorkoutIconName),
);

const catalogSource = await readFile(
  path.join(repositoryRoot, "NomadicMethod", "Assets", "exercises.json"),
  "utf8",
);
const catalogVersion = contentFingerprint(catalogSource);
const catalogOutputName = `exercises.${catalogVersion}.json`;
await copyInto(
  path.join(repositoryRoot, "NomadicMethod", "Assets", "exercises.json"),
  path.join(outputRoot, "data", catalogOutputName),
);
// A cached pre-Floor runtime does not understand the new contact predicates.
// Fail before it loads or migrates workout state instead of offering floor work.
await writeFile(path.join(outputRoot, "data", "exercises.json"),
  `${JSON.stringify({ reloadRequired: true, catalogRevision: CURRENT_CATALOG_REVISION })}\n`,
  "utf8");
await copyInto(
  path.join(repositoryRoot, "tools", "nomadic_method_appicon.svg"),
  path.join(outputRoot, "assets", "nomadic_method_appicon.svg"),
);
await copyDirectory(
  path.join(repositoryRoot, "NomadicMethod", "Assets", "exercise_videos"),
  path.join(outputRoot, "assets", "exercise_videos"),
);
await copyOptionalDirectory(
  path.join(repositoryRoot, "NomadicMethod", "Assets", "exercise_direction_videos"),
  path.join(outputRoot, "assets", "exercise_direction_videos"),
);
await copyDirectory(
  path.join(repositoryRoot, "NomadicMethod", "Assets", "exercise_hold_frames"),
  path.join(outputRoot, "assets", "exercise_hold_frames"),
);
await copyDirectory(
  path.join(repositoryRoot, "NomadicMethod", "Resources", "raw"),
  path.join(outputRoot, "audio"),
);
await writeFile(path.join(outputRoot, ".nojekyll"), "", "utf8");

const catalog = JSON.parse(
  await readFile(path.join(outputRoot, "data", catalogOutputName), "utf8"),
);

if (!Array.isArray(catalog) || catalog.length !== 919) {
  throw new Error(`Expected 919 exercises, found ${catalog?.length ?? "invalid data"}.`);
}
const reactionPlans = resolveReactionDrills(JSON.parse(reactionSource), catalog);
if (reactionPlans.size !== 21) throw new Error('Expected twenty-one reviewed reaction protocols.');
for (const plan of reactionPlans.values()) {
  for (const action of plan.actions) {
    for (const [relative, expected] of [[action.video, action.videoSha256], [action.preview, action.previewSha256]]) {
      const file = path.join(outputRoot, 'assets', relative);
      await requireFile(file);
      if (createHash('sha256').update(await readFile(file)).digest('hex') !== expected) {
        throw new Error(`Reaction asset failed its reviewed hash: ${relative}`);
      }
    }
  }
}

const anatomicalGroupInventory = getAnatomicalGroupVarietyInventory(catalog);
const anatomicalGroupVarietyDeficiencies = anatomicalGroupInventory.filter(row => row.matchingExerciseCount < row.requiredExerciseCount);
const pairwiseDeficiencies =
  findWorkoutModifierPairCoverageDeficiencies(catalog);
const hardFloorCategoryDeficiencies =
  findHardFloorCategoryCoverageDeficiencies(catalog);
const muscularDemandDeficiencies =
  findMuscularDemandCoverageDeficiencies(catalog);
const materialityDeficiencies =
  findWorkoutModifierMaterialityDeficiencies(catalog);
const unacceptedMaterialityDeficiencies = materialityDeficiencies
  .filter((deficiency) => !isAcceptedMaterialityDeficiency(deficiency));
const wallCatalogDeficiencies = findWallRequiredCatalogDeficiencies(catalog);
const soleWallCatalogDeficiencies =
  findSoleWallContactRequiredCatalogDeficiencies(catalog);
const distinctLineupDeficiencies =
  findWorkoutProfileLineupDeficiencies(catalog);
const completeLineupDeficiencies =
  findCompleteWorkoutProfileLineupDeficiencies(catalog);
const integrityDeficitReport = JSON.parse(await readFile(
  path.join(
    repositoryRoot,
    "docs",
    "catalog-audit",
    "modifier_coverage_deficits_current.json",
  ),
  "utf8",
));
const catalogSha256 = createHash("sha256")
  .update(normalizeLineEndings(catalogSource))
  .digest("hex");
const expectedIntegritySummary = {
  anatomicalGroupVarietyDeficiencyCount: anatomicalGroupVarietyDeficiencies.length,
  pairwiseDeficiencyCount: pairwiseDeficiencies.length,
  pairwiseAffectedGroupCount: affectedGroupCount(pairwiseDeficiencies),
  hardFloorCategoryDeficiencyCount: hardFloorCategoryDeficiencies.length,
  hardFloorCategoryAffectedGroupCount:
    affectedGroupCount(hardFloorCategoryDeficiencies),
  muscularDemandDeficiencyCount: muscularDemandDeficiencies.length,
  muscularDemandAffectedGroupCount:
    affectedGroupCount(muscularDemandDeficiencies),
  demandZeroDeficiencyCount: muscularDemandDeficiencies.filter((item) =>
    item.muscularDemand === 0).length,
  demandZeroAffectedGroupCount: affectedGroupCount(
    muscularDemandDeficiencies.filter((item) => item.muscularDemand === 0),
  ),
  demandTwoDeficiencyCount: muscularDemandDeficiencies.filter((item) =>
    item.muscularDemand === 2).length,
  demandTwoAffectedGroupCount: affectedGroupCount(
    muscularDemandDeficiencies.filter((item) => item.muscularDemand === 2),
  ),
  materialityDeficiencyCount: materialityDeficiencies.length,
  unacceptedMaterialityDeficiencyCount: unacceptedMaterialityDeficiencies.length,
  distinctLineupDeficiencyCount: distinctLineupDeficiencies.length,
  completeLineupDeficiencyCount: completeLineupDeficiencies.length,
};
const integrityDebtMatches =
  integrityDeficitReport.catalogRevision === CURRENT_CATALOG_REVISION &&
  integrityDeficitReport.catalogRecordCount === catalog.length &&
  integrityDeficitReport.catalogSha256 === catalogSha256 &&
  exactlyEqual(integrityDeficitReport.policy?.acceptedMaterialityExceptions,
    ACCEPTED_MATERIALITY_EXCEPTIONS) &&
  integrityDeficitReport.policy
    ?.muscularDemandMinimumPerCategoryPerGroup ===
      MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP &&
  integrityDeficitReport.policy?.anatomicalGroupMinimum === MINIMUM_SESSION_MOVEMENTS_PER_ANATOMICAL_GROUP &&
  exactlyEqual(integrityDeficitReport.anatomicalGroupInventory, anatomicalGroupInventory) &&
  exactlyEqual(integrityDeficitReport.anatomicalGroupVariety, anatomicalGroupVarietyDeficiencies) &&
  exactlyEqual(integrityDeficitReport.summary, expectedIntegritySummary) &&
  exactlyEqual(integrityDeficitReport.pairwise, pairwiseDeficiencies) &&
  exactlyEqual(
    integrityDeficitReport.hardFloorCategory,
    hardFloorCategoryDeficiencies,
  ) &&
  exactlyEqual(
    integrityDeficitReport.muscularDemand,
    muscularDemandDeficiencies,
  ) &&
  exactlyEqual(integrityDeficitReport.materiality, materialityDeficiencies) &&
  exactlyEqual(integrityDeficitReport.distinctLineup, distinctLineupDeficiencies) &&
  exactlyEqual(integrityDeficitReport.completeLineup, completeLineupDeficiencies);

// Demand-category and distinct-lineup inventories are diagnostic. Every other
// deficit must satisfy the release requirements or an exact owner exception.
const catalogInvariantChecks = [
  ["modifier metadata completeness", isModifierMetadataComplete(catalog)],
  ["session movement metadata", isSessionMovementMetadataValid(catalog)],
  ["anatomical group minimum of ten distinct selectable movements", anatomicalGroupVarietyDeficiencies.length === 0],
  ["hierarchical modifier-pair coverage", pairwiseDeficiencies.length === 0],
  ["hierarchical hard-floor category coverage",
    hardFloorCategoryDeficiencies.length === 0],
  ["complete workout lineups", completeLineupDeficiencies.length === 0],
  ["modifier materiality", unacceptedMaterialityDeficiencies.length === 0],
  ["wall-required session-movement floor", wallCatalogDeficiencies.length === 0],
  ["elevation support, platform and floor session-movement minimums", findElevationCatalogDeficiencies(catalog).length === 0],
  ["sole-wall session-movement floor", soleWallCatalogDeficiencies.length === 0],
  ["locomotion session-movement floors", findLocomotionCatalogDeficiencies(catalog).length === 0],
  ["floor session-movement minimum", findFloorCatalogDeficiencies(catalog).length === 0],
  ["floor-and-wall session-movement minimum", findFloorAndWallCatalogDeficiencies(catalog).length === 0],
  ["floor-and-locomotion session-movement minimum", findFloorAndLocomotionCatalogDeficiencies(catalog).length === 0],
  ["explicit catalog-integrity deficit ledger", integrityDebtMatches],
];
const failedCatalogInvariants = catalogInvariantChecks
  .filter(([, valid]) => !valid)
  .map(([name]) => name);
if (failedCatalogInvariants.length > 0) {
  throw new Error(
    `Catalog failed build-time invariants: ${failedCatalogInvariants.join(", ")}.`,
  );
}

await mapConcurrent(catalog, 8, async (exercise) => {
  await requireFile(path.join(outputRoot, "assets", exercise.video));

  if (exercise.mode === "Hold") {
    await requireFile(
      path.join(
        outputRoot,
        "assets",
        "exercise_hold_frames",
        `exercise_${String(exercise.id).padStart(4, "0")}.png`,
      ),
    );
  }

  if (exercise.directionSequence !== "None") {
    await requireFile(
      path.join(
        outputRoot,
        "assets",
        "exercise_direction_videos",
        `exercise_${String(exercise.id).padStart(4, "0")}.mp4`,
      ),
    );
  }
});

const assetFiles = (await walk(path.join(outputRoot, "assets"))).sort();
const assetVersions = Object.fromEntries(await mapConcurrent(assetFiles, 8, async (file) => {
  const relativePath = path
    .relative(path.join(outputRoot, "assets"), file)
    .split(path.sep)
    .join("/");
  return [relativePath, createHash("sha256").update(await readFile(file)).digest("hex")];
}));
const assetVersionsSource = `${JSON.stringify(assetVersions, null, 2)}\n`;
const assetVersionsVersion = contentFingerprint(assetVersionsSource);
const assetVersionsOutputName = `asset-versions.${assetVersionsVersion}.json`;
await writeFile(
  path.join(outputRoot, "data", assetVersionsOutputName),
  assetVersionsSource,
  "utf8",
);

const appSource = await readFile(path.join(webRoot, "app.js"), "utf8");
const fingerprintedAppSource = appSource
  .replace('"./exercise-feedback.js"', `"./${moduleOutputs.get('exercise-feedback')}"`)
  .replace('"./reaction-drills.js"', `"./${moduleOutputs.get('reaction-drills')}"`)
  .replace('"./reaction-drill-view.js"', `"./${moduleOutputs.get('reaction-drill-view')}"`)
  .replace('"./fitness-profile.js"', `"./${moduleOutputs.get('fitness-profile')}"`)
  .replace('"./completion-profile.js"', `"./${moduleOutputs.get('completion-profile')}"`)
  .replace('"data/fitness_ratings.json"', `"data/${fitnessOutputName}"`)
  .replace('"data/reaction_drills.json"', `"data/${reactionOutputName}"`)
  .replace(
    'from "./workout.js";',
    `from "./${workoutOutputName}";`,
  )
  .replace(
    '"data/exercises.json"',
    `"data/${catalogOutputName}"`,
  )
  .replace(
    '"data/asset-versions.json"',
    `"data/${assetVersionsOutputName}"`,
  )
  .replaceAll(
    '"./workout-preparation-worker.js"',
    `"./${preparationWorkerOutputName}"`,
  );
if (fingerprintedAppSource === appSource ||
    !fingerprintedAppSource.includes(workoutOutputName) ||
    !fingerprintedAppSource.includes(reactionOutputName) ||
    [...moduleOutputs].some(([stem, name]) =>
      stem !== 'workout-protocol-history' && !fingerprintedAppSource.includes(name)) ||
    !fingerprintedAppSource.includes(fitnessOutputName) ||
    !fingerprintedAppSource.includes(preparationWorkerOutputName) ||
    !fingerprintedAppSource.includes(catalogOutputName) ||
    !fingerprintedAppSource.includes(
      assetVersionsOutputName)) {
  throw new Error("Could not content-address the web runtime dependencies.");
}
const appOutputName = fingerprintedName("app", "js", fingerprintedAppSource);
await writeFile(path.join(outputRoot, appOutputName), fingerprintedAppSource, "utf8");

const indexSource = await readFile(path.join(webRoot, "index.html"), "utf8");
const instantControlsSource = await readFile(
  path.join(webRoot, "instant-controls.js"),
  "utf8",
);
if (instantControlsSource.includes("</script") || lightCadenceSource.includes("</script") || durationSource.includes("</script")) {
  throw new Error("The inline startup controls contain a closing script tag.");
}
const fingerprintedIndex = indexSource
  .replace(
    '<script src="./workout-duration.js"></script>',
    `<script>\n${durationSource}\n</script>`,
  )
  .replace(
    '<script src="./light-cadence.js"></script>',
    `<script>\n${lightCadenceSource}\n</script>`,
  )
  .replace('./styles.css', `./${stylesOutputName}`)
  .replace('./app.js', `./${appOutputName}`)
  .replace(
    './data/exercises.json',
    `./data/${catalogOutputName}`,
  )
  .replace(
    './data/asset-versions.json',
    `./data/${assetVersionsOutputName}`,
  )
  .replace(
    '<script src="./instant-controls.js"></script>',
    `<script>\n${instantControlsSource}\n</script>`,
  );
if (fingerprintedIndex === indexSource ||
    !fingerprintedIndex.includes(stylesOutputName) ||
    !fingerprintedIndex.includes(appOutputName) ||
    !fingerprintedIndex.includes(catalogOutputName) ||
    !fingerprintedIndex.includes(
      assetVersionsOutputName) ||
    fingerprintedIndex.includes('./workout-duration.js') ||
    !fingerprintedIndex.includes(durationSource) ||
    fingerprintedIndex.includes('./instant-controls.js') ||
    fingerprintedIndex.includes('./light-cadence.js') ||
    !fingerprintedIndex.includes(lightCadenceSource)) {
  throw new Error("Could not fingerprint the web shell references.");
}
await writeFile(path.join(outputRoot, "index.html"), fingerprintedIndex, "utf8");

const outputFiles = await walk(outputRoot);
const forbiddenGifs = outputFiles.filter((file) => file.toLowerCase().endsWith(".gif"));
if (forbiddenGifs.length > 0) {
  throw new Error(`The web build must not contain GIF intermediates: ${forbiddenGifs[0]}`);
}

const outputBytes = (
  await mapConcurrent(outputFiles, 8, async (file) => (await stat(file)).size)
).reduce((total, size) => total + size, 0);

console.log(
  `Built ${outputFiles.length} files (${(outputBytes / 1024 / 1024).toFixed(2)} MiB), ` +
    `${catalog.length} exercises, 0 GIFs.`,
);

async function copyInto(source, destination) {
  await mkdir(path.dirname(destination), { recursive: true });
  await cp(source, destination);
}

async function copyDirectory(source, destination) {
  await mkdir(destination, { recursive: true });
  const entries = await readdir(source, { withFileTypes: true });
  await mapConcurrent(entries, 8, async (entry) => {
    const from = path.join(source, entry.name), to = path.join(destination, entry.name);
    if (entry.isDirectory()) await copyDirectory(from, to);
    else await cp(from, to);
  });
}

async function copyOptionalDirectory(source, destination) {
  try {
    await copyDirectory(source, destination);
  } catch (error) {
    if (error?.code !== "ENOENT") {
      throw error;
    }
    await mkdir(destination, { recursive: true });
  }
}

async function requireFile(file) {
  const information = await stat(file);
  if (!information.isFile() || information.size === 0) {
    throw new Error(`Missing runtime asset: ${file}`);
  }
}

function fingerprintedName(stem, extension, content) {
  return `${stem}.${contentFingerprint(content)}.${extension}`;
}

function contentFingerprint(content) {
  return createHash("sha256").update(content).digest("hex").slice(0, 12);
}

function normalizeLineEndings(text) {
  return text.replace(/\r\n?/g, "\n");
}

function exactlyEqual(left, right) {
  return JSON.stringify(left) === JSON.stringify(right);
}

function affectedGroupCount(deficiencies) {
  return new Set(deficiencies.map((deficiency) => deficiency.groupId)).size;
}

async function walk(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await walk(target)));
    } else if (entry.isFile()) {
      files.push(target);
    }
  }
  return files;
}
