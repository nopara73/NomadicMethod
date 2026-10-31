import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const policy = JSON.parse(await readFile(path.join(root, 'NomadicMethod/Assets/exercise_names.json'), 'utf8'));
const catalog = JSON.parse(await readFile(path.join(root, 'NomadicMethod/Assets/exercises.json'), 'utf8'));
const reactions = JSON.parse(await readFile(path.join(root, 'NomadicMethod/Assets/reaction_drills.json'), 'utf8'));
if (policy.version !== 1 || !Array.isArray(policy.names)) throw new Error('Invalid exercise-name policy.');
const ids = new Set();
for (const entry of policy.names) {
  if (!Number.isInteger(entry.id) || entry.id <= 0 || ids.has(entry.id) ||
      ![entry.name, entry.identityName, entry.sourceName, ...entry.previousNames]
        .every(name => typeof name === 'string' && name.trim() === name && name.length > 0) ||
      catalog.find(exercise => exercise.id === entry.id)?.name !== entry.name) {
    throw new Error(`Invalid or unsynchronized exercise name ${entry.id}.`);
  }
  ids.add(entry.id);
}
for (const entry of policy.reactionLabels) {
  const family = reactions.families.find(family => family.id === entry.familyId);
  if (!Number.isInteger(entry.actionIndex) || entry.actionIndex < 0 || entry.actionIndex > 3 ||
      typeof entry.identityName !== 'string' || !entry.identityName.trim() ||
      family?.actions[entry.actionIndex]?.label !== entry.name)
    throw new Error(`Unsynchronized reaction label ${entry.familyId}:${entry.actionIndex}.`);
}
const entries = policy.names.map(entry => JSON.stringify(
  [entry.id, [entry.identityName, entry.name, entry.previousNames]])).join(',\n');
const source = `// Generated from Android Assets/exercise_names.json; run tools/generate-exercise-names.mjs.\n` +
  `const reviewedNames = new Map([\n${entries}\n]);\n\n` +
  `function reviewedName(id, name, index) {\n` +
  `  const review = reviewedNames.get(id);\n` +
  `  return review && (name === review[0] || name === review[1] || review[2].includes(name))\n` +
  `    ? review[index] : name;\n}\n\n` +
  `export const exerciseIdentityName = (id, name) => reviewedName(id, name, 0);\n` +
  `export const exerciseDisplayName = (id, name) => reviewedName(id, name, 1);\n`;
const destination = path.join(root, 'web/exercise-names.js');
if (process.argv.includes('--check')) {
  const actual = (await readFile(destination, 'utf8')).replaceAll('\r\n', '\n');
  if (actual !== source) throw new Error('Generated exercise-name policy is stale.');
} else await writeFile(destination, source);
console.log(`Reviewed names synchronized: ${ids.size}.`);
