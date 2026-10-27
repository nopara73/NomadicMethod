// Generated from NomadicMethod/Assets/workout_durations.json.
// Regenerate with: node web/scripts/workout-duration-policy.mjs --write
(() => {
  if (globalThis.nomadicMethodDuration) return;
  const minutes = Object.freeze([3,5,7,10,15,20,30,45,60,90]);
  const defaultMinutes = 10;
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
