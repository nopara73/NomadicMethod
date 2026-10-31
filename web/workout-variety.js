export const REQUIRED_WORKOUT_MOVEMENT_CHOICES = 5;
export const WORKOUT_VARIETY_UNAVAILABLE_MESSAGE = "Some muscle groups have fewer than five options for these settings and your training zone.";

// Inputs already passed anatomy, settings and every atomic member's zone. The
// production solver then proves exact duration and whole-workout feasibility.
export function findWorkoutVarietyDeficiencies(groupCount, minutes, sourceCandidates, solve,
  required = REQUIRED_WORKOUT_MOVEMENT_CHOICES, groupsToValidate = (1n << BigInt(groupCount)) - 1n) {
  if (!Number.isInteger(groupCount) || groupCount < 1 || groupCount > 63 ||
      !Number.isInteger(minutes) || minutes < 1 || !Number.isInteger(required) || required < 1)
    throw new RangeError("Invalid workout variety requirement.");
  const candidates = sourceCandidates.map(candidate => ({...candidate,
    utilitiesByGroup: Array(groupCount).fill(0n)}));
  const families = new Map();
  for (const candidate of candidates) {
    if (!families.has(candidate.movementId)) families.set(candidate.movementId, []);
    families.get(candidate.movementId).push(candidate);
  }
  // Entire family placement/cost sets must match before a witness can be reused.
  const signatures = new Map([...families].map(([id, family]) => [id,
    [...new Set(family.map(candidate => `${candidate.coverageMask}:${candidate.blockCount}`))].sort().join("|")]));
  const feasible = new Map();
  const deficits = [];
  for (let index = 0; index < groupCount; index++) {
    const bit = 1n << BigInt(index);
    if ((groupsToValidate & bit) === 0n) continue;
    let count = 0;
    for (const [id, family] of families) {
      let selectable = false;
      for (const pinned of family.filter(candidate => (candidate.coverageMask & bit) !== 0n)) {
        const key = `${signatures.get(id)}@${pinned.coverageMask}:${pinned.blockCount}`;
        if (!feasible.has(key)) {
          const remaining = candidates.filter(candidate => candidate.movementId !== pinned.movementId &&
            (candidate.coverageMask & pinned.coverageMask) === 0n);
          feasible.set(key, solve(groupCount, minutes, [pinned, ...remaining]) !== null);
        }
        if (feasible.get(key)) { selectable = true; break; }
      }
      if (selectable && ++count >= required) break;
    }
    if (count < required) deficits.push({groupIndex:index, distinctMovementCount:count, requiredCount:required});
  }
  return deficits;
}
