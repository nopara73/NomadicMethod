import { createHash } from "node:crypto";
import { cp, mkdir, readFile, readdir, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  CURRENT_CATALOG_REVISION,
  MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  findHardFloorCategoryCoverageDeficiencies,
  findMuscularDemandCoverageDeficiencies,
  findSoleWallContactRequiredCatalogDeficiencies,
  findWallRequiredCatalogDeficiencies,
  findWorkoutModifierMaterialityDeficiencies,
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

if (!outputRoot.startsWith(`${webRoot}${path.sep}`)) {
  throw new Error("Refusing to build outside the web project.");
}

await rm(outputRoot, { recursive: true, force: true });
await mkdir(outputRoot, { recursive: true });

const lightCadenceSource = await readFile(path.join(webRoot, "light-cadence.js"), "utf8");
const lightCadenceOutputName = fingerprintedName("light-cadence", "js", lightCadenceSource);
await writeFile(path.join(outputRoot, lightCadenceOutputName), lightCadenceSource, "utf8");
const workoutSource = (await readFile(path.join(webRoot, "workout.js"), "utf8"))
  .replace('./light-cadence.js', `./${lightCadenceOutputName}`);
if (!workoutSource.includes(`./${lightCadenceOutputName}`) ||
    workoutSource.includes('./light-cadence.js')) {
  throw new Error("Could not content-address the shared Light cadence policy.");
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
);
if (fingerprintedPreparationWorkerSource === preparationWorkerSource ||
    !fingerprintedPreparationWorkerSource.includes(workoutOutputName)) {
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
const softFloorIconPath = path.join(
  repositoryRoot,
  "NomadicMethod",
  "Resources",
  "drawable-xxhdpi",
  "ic_soft_floor.png",
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
const softFloorIconSource = await readFile(softFloorIconPath);
const wallNoSoleIconSource = await readFile(wallNoSoleIconPath);
const lightWorkoutIconSource = await readFile(lightWorkoutIconPath);
const hardFloorIconName = fingerprintedName(
  "ic_hard_floor",
  "png",
  hardFloorIconSource,
);
const softFloorIconName = fingerprintedName(
  "ic_soft_floor",
  "png",
  softFloorIconSource,
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
  .replaceAll("./assets/ic_soft_floor.png", `./assets/${softFloorIconName}`)
  .replaceAll("./assets/ic_wall_no_sole.png", `./assets/${wallNoSoleIconName}`)
  .replaceAll("./assets/ic_light_workout.png", `./assets/${lightWorkoutIconName}`);
if (stylesSource === stylesTemplate ||
    stylesSource.includes("./assets/ic_hard_floor.png") ||
    stylesSource.includes("./assets/ic_soft_floor.png") ||
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
  softFloorIconPath,
  path.join(outputRoot, "assets", softFloorIconName),
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
await copyInto(
  path.join(repositoryRoot, "NomadicMethod", "Assets", "exercises.json"),
  path.join(outputRoot, "data", "exercises.json"),
);
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
  await readFile(path.join(outputRoot, "data", "exercises.json"), "utf8"),
);

if (!Array.isArray(catalog) || catalog.length !== 545) {
  throw new Error(`Expected 545 exercises, found ${catalog?.length ?? "invalid data"}.`);
}

const pairwiseDeficiencies =
  findWorkoutModifierPairCoverageDeficiencies(catalog);
const hardFloorCategoryDeficiencies =
  findHardFloorCategoryCoverageDeficiencies(catalog);
const muscularDemandDeficiencies =
  findMuscularDemandCoverageDeficiencies(catalog);
const materialityDeficiencies =
  findWorkoutModifierMaterialityDeficiencies(catalog);
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
  distinctLineupDeficiencyCount: distinctLineupDeficiencies.length,
  completeLineupDeficiencyCount: completeLineupDeficiencies.length,
};
const integrityDebtMatches =
  integrityDeficitReport.catalogRevision === CURRENT_CATALOG_REVISION &&
  integrityDeficitReport.catalogRecordCount === catalog.length &&
  integrityDeficitReport.catalogSha256 === catalogSha256 &&
  integrityDeficitReport.policy
    ?.muscularDemandMinimumPerCategoryPerGroup ===
      MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP &&
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

// Demand-category and percentage materiality results above are diagnostic.
// Availability and complete atomic lineups remain release requirements.
const catalogInvariantChecks = [
  ["modifier metadata completeness", isModifierMetadataComplete(catalog)],
  ["session movement metadata", isSessionMovementMetadataValid(catalog)],
  ["hierarchical modifier-pair coverage", pairwiseDeficiencies.length === 0],
  ["hierarchical hard-floor category coverage",
    hardFloorCategoryDeficiencies.length === 0],
  ["complete workout lineups", completeLineupDeficiencies.length === 0],
  ["wall-required session-movement floor", wallCatalogDeficiencies.length === 0],
  ["sole-wall session-movement floor", soleWallCatalogDeficiencies.length === 0],
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

for (const exercise of catalog) {
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
}

const assetVersions = {};
for (const file of await walk(path.join(outputRoot, "assets"))) {
  const relativePath = path
    .relative(path.join(outputRoot, "assets"), file)
    .split(path.sep)
    .join("/");
  assetVersions[relativePath] = createHash("sha256")
    .update(await readFile(file))
    .digest("hex");
}
const assetVersionsSource = `${JSON.stringify(assetVersions, null, 2)}\n`;
const assetVersionsVersion = contentFingerprint(assetVersionsSource);
await writeFile(
  path.join(outputRoot, "data", "asset-versions.json"),
  assetVersionsSource,
  "utf8",
);

const appSource = await readFile(path.join(webRoot, "app.js"), "utf8");
const fingerprintedAppSource = appSource
  .replace(
    'from "./workout.js";',
    `from "./${workoutOutputName}";`,
  )
  .replace(
    '"data/exercises.json"',
    `"data/exercises.json?v=${catalogVersion}"`,
  )
  .replace(
    '"data/asset-versions.json"',
    `"data/asset-versions.json?v=${assetVersionsVersion}"`,
  )
  .replaceAll(
    '"./workout-preparation-worker.js"',
    `"./${preparationWorkerOutputName}"`,
  );
if (fingerprintedAppSource === appSource ||
    !fingerprintedAppSource.includes(workoutOutputName) ||
    !fingerprintedAppSource.includes(preparationWorkerOutputName) ||
    !fingerprintedAppSource.includes(`exercises.json?v=${catalogVersion}`) ||
    !fingerprintedAppSource.includes(
      `asset-versions.json?v=${assetVersionsVersion}`)) {
  throw new Error("Could not content-address the web runtime dependencies.");
}
const appOutputName = fingerprintedName("app", "js", fingerprintedAppSource);
await writeFile(path.join(outputRoot, appOutputName), fingerprintedAppSource, "utf8");

const indexSource = await readFile(path.join(webRoot, "index.html"), "utf8");
const instantControlsSource = await readFile(
  path.join(webRoot, "instant-controls.js"),
  "utf8",
);
if (instantControlsSource.includes("</script") || lightCadenceSource.includes("</script")) {
  throw new Error("The inline startup controls contain a closing script tag.");
}
const fingerprintedIndex = indexSource
  .replace(
    '<script src="./light-cadence.js"></script>',
    `<script>\n${lightCadenceSource}\n</script>`,
  )
  .replace('./styles.css', `./${stylesOutputName}`)
  .replace('./app.js', `./${appOutputName}`)
  .replace(
    './data/exercises.json',
    `./data/exercises.json?v=${catalogVersion}`,
  )
  .replace(
    './data/asset-versions.json',
    `./data/asset-versions.json?v=${assetVersionsVersion}`,
  )
  .replace(
    '<script src="./instant-controls.js"></script>',
    `<script>\n${instantControlsSource}\n</script>`,
  );
if (fingerprintedIndex === indexSource ||
    !fingerprintedIndex.includes(stylesOutputName) ||
    !fingerprintedIndex.includes(appOutputName) ||
    !fingerprintedIndex.includes(`exercises.json?v=${catalogVersion}`) ||
    !fingerprintedIndex.includes(
      `asset-versions.json?v=${assetVersionsVersion}`) ||
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
  await Promise.all(outputFiles.map(async (file) => (await stat(file)).size))
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
  await mkdir(path.dirname(destination), { recursive: true });
  await cp(source, destination, { recursive: true });
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
