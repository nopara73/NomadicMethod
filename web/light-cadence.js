// Shared by the instant startup controls, workout service, and preparation
// worker. Keep this small and independent of the catalog or browser DOM.
(() => {
  const dailyCap = 60;
  const threshold = 180;
  const fullCreditCompletionPercent = 85;
  const lightFlag = 256;
  const positive = (value) => Number.isSafeInteger(value) && value > 0;
  const objects = (value) => Array.isArray(value)
    ? value.filter((item) => item && typeof item === "object")
    : [];

  function localDay(timestamp) {
    if (!positive(timestamp)) return null;
    const date = new Date(timestamp);
    if (Number.isNaN(date.getTime())) return null;
    return Math.trunc(Date.UTC(
      date.getFullYear(), date.getMonth(), date.getDate(),
    ) / 86_400_000);
  }

  function accumulatedMinutes(history, now, legacyDays = []) {
    const today = localDay(now);
    if (today === null) {
      throw new RangeError("Current workout time must be positive Unix milliseconds.");
    }
    const activityByDay = new Map();
    function addActivity(at, minutes, completedLight = false) {
      const day = at <= now ? localDay(at) : null;
      if (day === null) return;
      const activity = activityByDay.get(day) ?? {
        regularMinutes: 0, hasCompletedLightWorkout: false,
      };
      activityByDay.set(day, {
        regularMinutes: Math.min(dailyCap, activity.regularMinutes + minutes),
        hasCompletedLightWorkout:
          activity.hasCompletedLightWorkout || completedLight,
      });
    }

    for (const session of objects(history)) {
      const startedAt = positive(session.startedAtUnixMilliseconds)
        ? session.startedAtUnixMilliseconds : session.endedAtUnixMilliseconds;
      if (localDay(startedAt) === null) continue;
      const changes = objects(session.modifierChanges)
        .filter((change) => positive(change.changedAtUnixMilliseconds))
        .sort((a, b) => a.changedAtUnixMilliseconds - b.changedAtUnixMilliseconds);
      function isLightAt(at) {
        let light = session.isLightDay === true ||
          (session.modifiers & lightFlag) !== 0;
        for (const change of changes) {
          if (change.changedAtUnixMilliseconds > at) break;
          light = (change.newModifiers & lightFlag) !== 0;
        }
        return light;
      }
      const blocks = objects(session.blocks);
      const endedAt = positive(session.endedAtUnixMilliseconds)
        ? session.endedAtUnixMilliseconds : startedAt;
      let completedRegularBlocks = 0;
      for (const block of blocks) {
        const at = positive(block.completedAtUnixMilliseconds)
          ? block.completedAtUnixMilliseconds : startedAt;
        const light = isLightAt(at);
        addActivity(at, light ? 0 : 1);
        if (!light && positive(at) && at <= endedAt && at <= now) {
          completedRegularBlocks += 1;
        }
      }
      if (session.status === "Completed") {
        if (isLightAt(endedAt)) {
          addActivity(endedAt, 0, true);
        } else if (blocks.length === 0 && (session.startedBeforeLogging ||
            (objects(session.initialSelections).length === 0 &&
             objects(session.decisions).length === 0))) {
          addActivity(endedAt, positive(session.workoutMinutes)
            ? Math.min(session.workoutMinutes, dailyCap) : 0);
        } else if (positive(session.workoutMinutes) &&
            completedRegularBlocks < session.workoutMinutes &&
            completedRegularBlocks * 100 >=
              session.workoutMinutes * fullCreditCompletionPercent &&
            session.isLightDay !== true &&
            (session.modifiers & lightFlag) === 0 &&
            !changes.some((change) => change.changedAtUnixMilliseconds <= endedAt &&
              (change.newModifiers & lightFlag) !== 0)) {
          // Cadence credit only: logs and muscle recovery still reflect actual
          // work. Keep block dates; add only the top-up on the completion date.
          addActivity(endedAt, Math.min(dailyCap,
            session.workoutMinutes - completedRegularBlocks));
        }
      }
    }
    for (const timestamp of Array.isArray(legacyDays) ? legacyDays : []) {
      const day = timestamp <= now ? localDay(timestamp) : null;
      if (day !== null && !activityByDay.has(day)) {
        activityByDay.set(day, {
          regularMinutes: dailyCap, hasCompletedLightWorkout: false,
        });
      }
    }

    let day = activityByDay.has(today) ? today : today - 1;
    let minutes = 0;
    while (activityByDay.has(day)) {
      const activity = activityByDay.get(day);
      // The completed Light day resets tomorrow; it remains locked today.
      if (day < today && activity.hasCompletedLightWorkout) break;
      minutes += Math.min(dailyCap, activity.regularMinutes);
      if (minutes >= threshold) return threshold;
      day -= 1;
    }
    return minutes;
  }

  function isDue(history, now, legacyDays = []) {
    return accumulatedMinutes(history, now, legacyDays) >= threshold;
  }

  function workoutsRemaining(history, duration, now, legacyDays = []) {
    if (!positive(duration)) {
      throw new RangeError("Prospective workout minutes must be positive.");
    }
    return Math.ceil(Math.max(0, threshold -
      accumulatedMinutes(history, now, legacyDays)) / Math.min(duration, dailyCap));
  }

  globalThis.nomadicMethodLightCadence = Object.freeze({
    dailyCap, threshold, fullCreditCompletionPercent, isDue, workoutsRemaining,
  });
})();
