import { readFile, writeFile } from "node:fs/promises";

const contractUrl = new URL("../../NomadicMethod/Assets/workout_durations.json", import.meta.url);
const outputUrl = new URL("../workout-duration.js", import.meta.url);

export async function durationPolicySource() {
  const { minutes, defaultMinutes } = JSON.parse(await readFile(contractUrl, "utf8"));
  if (!Array.isArray(minutes) || !minutes.length ||
      minutes.some((value, index) => !Number.isSafeInteger(value) || value <= 0 ||
        (index > 0 && value <= minutes[index - 1])) || !minutes.includes(defaultMinutes)) {
    throw new Error("Invalid workout duration contract.");
  }
  return `// Generated from NomadicMethod/Assets/workout_durations.json.
// Regenerate with: node web/scripts/workout-duration-policy.mjs --write
(() => {
  if (globalThis.nomadicMethodDuration) return;
  const minutes = Object.freeze(${JSON.stringify(minutes)});
  const defaultMinutes = ${defaultMinutes};
  globalThis.nomadicMethodDuration = Object.freeze({
    minutes,
    defaultMinutes,
    normalize(value) {
      const requested = Number(value);
      if (!Number.isFinite(requested)) return defaultMinutes;
      return [...minutes].sort((left, right) =>
        Math.abs(left - requested) - Math.abs(right - requested) || right - left)[0];
    },
  });
})();
`;
}

export async function checkDurationPolicy() {
  if ((await readFile(outputUrl, "utf8")).replaceAll("\r\n", "\n") !== await durationPolicySource()) {
    throw new Error("Duration policy is stale. Run node web/scripts/workout-duration-policy.mjs --write.");
  }
}

if (process.argv.includes("--write")) await writeFile(outputUrl, await durationPolicySource());
