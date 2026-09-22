import { createHash } from "node:crypto";
import { readFile, readdir, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(scriptDirectory, "..");
const repositoryRoot = path.resolve(webRoot, "..");
const manifestPath = path.join(webRoot, "mobile-parity.json");
const sourceTargets = [
  "NomadicMethod/NomadicMethod.csproj",
  "NomadicMethod/MainActivity.cs",
  // Oura is intentionally Android-only (owner decision, docs/OURA_RECOVERY.md).
  // Track its bridge and permission surface even though web has no health UI.
  "NomadicMethod/MainActivity.Recovery.cs",
  "NomadicMethod/RecoveryPrivacyActivity.cs",
  "NomadicMethod/AndroidManifest.xml",
  "NomadicMethod/WorkoutBlockTimelineView.cs",
  "NomadicMethod/Data",
  "NomadicMethod/Models",
  "NomadicMethod/Services",
  "NomadicMethod/Resources/color",
  "NomadicMethod/Resources/drawable",
  "NomadicMethod/Resources/drawable-xxhdpi",
  "NomadicMethod/Resources/layout",
  "NomadicMethod/Resources/values",
];

const sourceFiles = [];
for (const target of sourceTargets) {
  await collectSourceFiles(path.join(repositoryRoot, target));
}
sourceFiles.sort();

const hash = createHash("sha256");
for (const relativePath of sourceFiles) {
  const contents = await readFile(path.join(repositoryRoot, relativePath));
  hash.update(relativePath.replaceAll(path.sep, "/"));
  hash.update("\0");
  hash.update(isTextSource(relativePath)
    ? contents.toString("utf8").replaceAll("\r\n", "\n")
    : contents);
  hash.update("\0");
}

const actual = {
  schemaVersion: 1,
  sourceCount: sourceFiles.length,
  sha256: hash.digest("hex"),
};

if (process.argv.includes("--print")) {
  console.log(JSON.stringify(actual, null, 2));
  process.exit(0);
}

const expected = JSON.parse(await readFile(manifestPath, "utf8"));
if (
  expected.schemaVersion !== actual.schemaVersion ||
  expected.sourceCount !== actual.sourceCount ||
  expected.sha256 !== actual.sha256
) {
  console.error("The mobile UI or workout contract changed without a reviewed web parity update.");
  console.error(`Expected ${JSON.stringify(expected)}`);
  console.error(`Current  ${JSON.stringify(actual)}`);
  console.error("Update the web implementation, then refresh web/mobile-parity.json.");
  process.exit(1);
}

console.log(`Mobile parity locked to ${actual.sourceCount} source files (${actual.sha256.slice(0, 12)}).`);

async function collectSourceFiles(target) {
  const information = await stat(target);
  if (information.isFile()) {
    if (isParitySource(target)) {
      sourceFiles.push(path.relative(repositoryRoot, target));
    }
    return;
  }

  const entries = await readdir(target, { withFileTypes: true });
  for (const entry of entries) {
    const child = path.join(target, entry.name);
    if (entry.isDirectory()) {
      await collectSourceFiles(child);
    } else if (entry.isFile() && isParitySource(entry.name)) {
      sourceFiles.push(path.relative(repositoryRoot, child));
    }
  }
}

function isParitySource(file) {
  return /\.(?:cs|csproj|png|xml)$/i.test(file);
}

function isTextSource(file) {
  return /\.(?:cs|csproj|xml)$/i.test(file);
}
