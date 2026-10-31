# Shared workout coverage

New workouts credit each complete selected movement to every existing anatomical group it meaningfully trains. Primary and reviewed secondary claims both count. Group definitions, catalog ratings, identities, scores and stored Keeps are unchanged.

The planner starts from a feasible preference-ranked lineup, transfers redundant coverage to a preferred retained movement only when an exact-duration allocation remains possible and whole-workout load does not worsen, then considers complete balanced replacements. Light, phase feedback, recovery and contextual Keep preferences still apply.

Extra sets count the actual timed blocks of every sequence, including sides and directions, with the existing primary/secondary demand weights. Projected balance is compared from the broadest existing grouping to the finest, followed by marginal muscle load and existing tie breakers. A weak fine bucket cannot pull extra sets into an already loaded broader region. Only complete sequences fill the selected duration.

The persisted ActiveCoverageAccountingVersion marker is set to 1 for new plans. Missing or zero markers retain legacy placement and allocation behavior. Reopening never upgrades an unfinished plan. Current/completed selections, their sets, round IDs, history, score and Keep records survive restoration and explicit duration changes.

If every eligible movement for a fine anatomical slot has been rejected in its scheduled workout phase, preparation can merge complete neighboring slots into the smallest existing broader target with an unrejected option. The ordinary planner still checks fitness eligibility, equipment, anatomy, complete sequences and exact duration. The broader plan is retained only when its final scheduled blocks contain less rejected work; Light must retain at least as many demand-zero blocks. Old global negative scores continue to rank movements but do not trigger anatomical broadening; this preserves existing contextual Keep priority. No exercise is rerated or assigned new anatomy. Scores, Keeps and historical decisions remain intact.

Explicit duration edits apply the same fallback only to future slots. Completed work and the whole current atomic selection, including its allocated sets and stable round IDs, remain protected. Cold reopening never triggers this fallback. Native and web share `rejected-slot-fallback-cases.json`, including the hard-floor pelvic-floor bottleneck, longer saved durations, Light and ordinary fine-slot alternatives.

Android and web use the same shared-coverage fixtures across every taxonomy resolution; real-catalog regressions cover Heel Raise with Walking Arms, stored Keeps, exact duration and reopening.
