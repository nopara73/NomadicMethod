import { createHash } from "node:crypto";
import { readFile, rename, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

import {
  ACCEPTED_COVERAGE_EXCEPTIONS,
  BROAD_COVERAGE_RESOLUTION_MINUTES,
  CURRENT_CATALOG_REVISION,
  MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  findHardFloorCategoryCoverageDeficiencies,
  findMuscularDemandCoverageDeficiencies,
  findWorkoutModifierMaterialityDeficiencies,
  findWorkoutModifierPairCoverageDeficiencies,
  findWorkoutProfileLineupDeficiencies,
  findCompleteWorkoutProfileLineupDeficiencies,
} from "../workout.js";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(webRoot, "..");
const catalogPath = path.join(
  repositoryRoot,
  "NomadicMethod",
  "Assets",
  "exercises.json",
);
const outputPath = path.join(
  repositoryRoot,
  "docs",
  "catalog-audit",
  "modifier_coverage_deficits_current.json",
);

const catalogSource = await readFile(catalogPath, "utf8");
const catalog = JSON.parse(catalogSource);
const pairwise = findWorkoutModifierPairCoverageDeficiencies(catalog);
const hardFloorCategory =
  findHardFloorCategoryCoverageDeficiencies(catalog);
const muscularDemand = findMuscularDemandCoverageDeficiencies(catalog);
const materiality = findWorkoutModifierMaterialityDeficiencies(catalog);
const distinctLineup = findWorkoutProfileLineupDeficiencies(catalog);
const completeLineup = findCompleteWorkoutProfileLineupDeficiencies(catalog);

const report = {
  catalogRevision: CURRENT_CATALOG_REVISION,
  catalogRecordCount: catalog.length,
  catalogSha256: createHash("sha256")
    .update(catalogSource.replaceAll("\r\n", "\n"))
    .digest("hex"),
  policy: {
    treatment: "Availability and complete atomic lineups must have zero deficits outside the exact owner-accepted coverage exceptions. Affected slots are omitted and the selected workout duration is preserved. Repeated complete movements are allowed. Distinct-lineup, demand-category and percentage materiality arrays are diagnostics, not release gates.",
    acceptedCoverageExceptions: ACCEPTED_COVERAGE_EXCEPTIONS,
    diagnosticOnly: ["muscularDemand", "materiality", "distinctLineup"],
    broadCoverageResolutionMinutes: BROAD_COVERAGE_RESOLUTION_MINUTES,
    broadModifierPairMinimumPerStatePerGroup:
      MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
    fineModifierPairMinimumPerStatePerGroup:
      MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
    muscularDemandMinimumPerCategoryPerGroup:
      MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  },
  summary: {
    pairwiseDeficiencyCount: pairwise.length,
    pairwiseAffectedGroupCount: affectedGroupCount(pairwise),
    hardFloorCategoryDeficiencyCount: hardFloorCategory.length,
    hardFloorCategoryAffectedGroupCount:
      affectedGroupCount(hardFloorCategory),
    muscularDemandDeficiencyCount: muscularDemand.length,
    muscularDemandAffectedGroupCount: affectedGroupCount(muscularDemand),
    demandZeroDeficiencyCount: muscularDemand.filter((item) =>
      item.muscularDemand === 0).length,
    demandZeroAffectedGroupCount: affectedGroupCount(
      muscularDemand.filter((item) => item.muscularDemand === 0),
    ),
    demandTwoDeficiencyCount: muscularDemand.filter((item) =>
      item.muscularDemand === 2).length,
    demandTwoAffectedGroupCount: affectedGroupCount(
      muscularDemand.filter((item) => item.muscularDemand === 2),
    ),
    materialityDeficiencyCount: materiality.length,
    distinctLineupDeficiencyCount: distinctLineup.length,
    completeLineupDeficiencyCount: completeLineup.length,
  },
  pairwise,
  hardFloorCategory,
  muscularDemand,
  materiality,
  distinctLineup,
  completeLineup,
};

// Publish a complete ledger even while an editor or preview is reading it.
// In-place writes can fail on Windows or expose a partially written report.
const temporaryPath = `${outputPath}.${process.pid}.tmp`;
try {
  await writeFile(temporaryPath, `${JSON.stringify(report, null, 2)}\n`,
    { encoding: "utf8", flag: "wx" });
  await rename(temporaryPath, outputPath);
} finally {
  await rm(temporaryPath, { force: true });
}
console.log(`Catalog deficit ledger: ${outputPath}`);

function affectedGroupCount(deficiencies) {
  return new Set(deficiencies.map((deficiency) => deficiency.groupId)).size;
}
