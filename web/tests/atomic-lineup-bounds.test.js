import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import test from 'node:test';

// Exercise the actual private solver, keeping the product API unchanged.
const sourceUrl = new URL('../workout.js', import.meta.url);
let source = readFileSync(sourceUrl, 'utf8').replace(
  /(from\s*|import\s*)(["'])(\.\/[^"']+)\2/g,
  (_, prefix, quote, relative) => prefix + quote + new URL(relative, sourceUrl).href + quote);
source += '\nexport {solveAtomicSequenceLineup};\n';
const {solveAtomicSequenceLineup} = await import('data:text/javascript,' + encodeURIComponent(source));
const cases = JSON.parse(readFileSync(new URL('./fixtures/atomic-lineup-bounds.json', import.meta.url), 'utf8'));
for (const entry of cases) test(entry.name, () => {
  const candidates = entry.candidates.map((candidate, tieOrder) => ({
    exerciseId: candidate.id, movementId: candidate.movement,
    coverageMask: candidate.groups.reduce((mask, group) => mask | (1n << BigInt(group)), 0n),
    blockCount: candidate.blocks, utilitiesByGroup: candidate.utilities.map(BigInt), tieOrder,
  }));
  const started = performance.now();
  const result = solveAtomicSequenceLineup(entry.groups, entry.minutes, candidates);
  assert.ok(result);
  assert.equal(result.utility, BigInt(entry.expectedUtility));
  assert.ok(performance.now() - started < 5000, 'Impossible branches must be pruned before matching');
});
