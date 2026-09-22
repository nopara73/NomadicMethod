# Muscular-demand audit

The 540 catalog records were individually reviewed against one frozen,
three-level rubric. The rating estimates the demonstrated movement's inherent
local muscular demand when an average healthy adult repeats the shown range and
cadence continuously for 45 seconds. It is not a personalized RPE prediction.

| Rating | Contract | Current count |
| --- | --- | ---: |
| `0` | Muscular loading is incidental; mobility, motor control, balance skill, breathing, or relaxation is the principal demand. | 117 |
| `1` | Muscular loading is meaningful, but local force or fatigue is not expected to be the principal limiter. | 288 |
| `2` | Hard muscular work; local force or fatigue is expected to be the principal limiter. | 135 |

Ratings were assigned exercise by exercise. There is no desired overall
distribution or balancing target. Stretching and mobility are not promoted merely
because a muscle is named; unloaded striking and rhythmic cardio do not become
`2` merely because they are fast; demanding squat/lunge patterns, substantial
bodyweight isometrics, self-resistance, and repeated plyometric work qualify
when local force or fatigue is the limiting demand.

The 2026-08-29 demonstration-integrity review is a historical checkpoint.
Later reviews follow the final packaged action rather than inheriting a rating
from an older name, source or classification. For example, the current mini-squat
calf raises with forward reach (565) and march with torso twist (305) are both
`1`. The current per-exercise evidence is bound to metadata and runtime-media
hashes in
[`catalog-audit/review-evidence-2026-09-06.json`](catalog-audit/review-evidence-2026-09-06.json).
No distribution was targeted.

The authoritative reviewed ID lists and rubric live in
[`tools/ExerciseMuscularDemand.psd1`](../tools/ExerciseMuscularDemand.psd1).
The generated rating for each named exercise is shipped as `muscularDemand` in
[`Flux/Assets/exercises.json`](../Flux/Assets/exercises.json). Catalog generation
fails if a retained exercise is missing, duplicated across ratings, or assigned
outside `0..2`; linked opposite-direction exercises must agree.

The 2026-09-11 owner-directed completion policy treats demand-category
populations as diagnostics, not admission quotas or release gates. The audit
still reports demand 0 and demand 2 across the deduplicated single/pair modifier
profiles and three broad regions. An all-light sequence counts only when every
distinct member is demand 0 and a member's primary muscle belongs to the region.
A hard sequence counts only when a demand-2 member has its primary muscle in the
region. Deduplicate SessionMovementId; never inflate counts with sides, sets,
names or blocks. Light remains outside the modifier axes.

The current inventory results remain visible in the
[diagnostic ledger](catalog-audit/modifier_coverage_deficits_current.json).
Actual selectable availability, the established trained-muscle breadth rule
and complete atomic workout lineups remain release requirements. A category
shortfall never justifies invented movements or altered anatomy/demand ratings.
Light remains the established session ranking preference.

`muscularDemand` is intentionally independent of the mutable user-preference
`score`; it never creates hardness points or rewrites votes. Completing a
rating-`1` exercise starts a rolling 18-hour soft recovery preference for its
primary canonical muscle, during which same-score rating-`0` work is preferred.
Completing a rating-`2` exercise starts both that 18-hour window and the
separate 36-hour hard-work window, during which same-score non-hard work is
preferred to rating-`2` work for the same primary muscle.

A fresh rating-`2` keep, or a fresh rating-`2` exercise already in the highest
available saved-score bucket, is preferred when its primary canonical muscle
belongs to the current workout group. Recovering exercises remain selectable,
and their keeps remain saved for later; a higher user score still wins. Among
otherwise equivalent fresh hard choices, the longest-rested primary muscle is
preferred. Available-equipment relevance for Wall and Mirror is a lower-order
tie-break, and a rejected
lower-score exercise is never pulled upward by recovery rotation.

Automatic Light uses accumulated completed regular work, not a fixed three-day
or three-session rule. One fully completed 45-second exercise block contributes
one nominal workout minute (Nomadic Method's existing block-plus-rest approximation).
Merely selected duration and uncompleted timer time do not contribute.
Completed blocks in interrupted sessions do contribute. Classify
each block using the Light setting at its completion, including recorded
mid-workout modifier changes. Recovery-derived Light presentation does not
change that classification.

For cadence only, a **completed, wholly regular session with at least 85% of
its planned blocks actually completed** receives its full planned duration.
This is a completion tolerance, not a physiological measurement or a new
hardness score. It prevents a few skipped blocks from postponing Light after
otherwise complete workouts: 53/60, 56/60, and 55/60 each credit 60 minutes.
The exact 60-minute boundary is 51 completed blocks; 50 still credits only 50.
Apply the same percentage to other durations; for example 6/7 credits 7,
whereas 2/3 credits only 2. Short sessions still aggregate across each date.

Do not round up an interrupted, in-progress, all-skipped, or mixed-Light
session. A session that enabled Light at any point before completion is mixed
even if it ends in regular mode; physical-only modifier changes do not prevent
credit. Never grant a top-up before the recorded completion time. Actual
blocks retain their own local dates, with only the missing cadence credit
added on the completion date, before applying the shared daily cap. Do not
rewrite logs, add fake blocks, or modify scores, Keeps, or either muscle-recovery
timestamp map. Recalculate from existing logs so the correction works on
already-installed devices without changing persisted data.

Sum those minutes across all sessions on each block's local calendar date,
then cap that date at **60 minutes**. Accumulate the daily credits across
consecutive training dates. At **180 minutes**, the next workout must use Light.
An overnight gap is not a rest day; a complete date without training resets
the accumulation. A completed Light workout also resets it, but only on the
following local date. Completing a three-minute Light workout cannot unlock
regular workouts again that same day. When an existing older history contains
regular workouts beyond the threshold, Light remains due rather than wrapping
the total around and silently skipping recovery.

Examples, assuming uninterrupted daily training:

| Regular training per day | Regular days before Light |
|---|---:|
| One 60- or 90-minute workout | 3 |
| One 30-minute workout | 6 |
| Ten 3-minute workouts | 6 |
| One 3-minute workout | 60 |

These are transparent scheduling heuristics, not a clinically measured fatigue
model. The 60-minute daily cap preserves the agreed cadence for longer workouts;
it does not claim that 60 and 90 minutes cause identical physiological fatigue.
Existing demand-specific 18/36-hour muscle recovery remains independent. No
additional hardness points or weights are introduced.

Due automatic Light is shown ON and cannot be disabled before or during a
workout; enforce this in the session service as well as the controls and check
again when activating a prepared workout. Manual Light can still be toggled
on ordinary days. Nomadic Method first
maximizes sequences whose every distinct member is demand `0`. Saved score,
Keep, recovery, and equipment preferences then arbitrate among those light
choices. A harder sequence fills a slot only when the compatible catalog cannot
cover it with demand-`0` work; displaced Keeps and user scores remain persisted
unchanged.

Persisted session history supplies this calculation without adding a mutable
counter or rewriting history. For old completed records without block history,
use the recorded duration only if the record predates logging or contains no
selection/decision audit trail. Modern all-skipped sessions contribute zero.
An already-inferred legacy training date contributes 60 minutes only when no
reconstructable activity exists for that date; never add it to logged work.
No score, Keep, session ID, or completed progress is migrated by this change.

Light itself is session-scoped and is not copied into the next session's
remembered physical setup. When OFF, its tile estimates the remaining workouts
at the selected duration as `ceil((180 - creditedMinutes) / min(duration, 60))`.
This is not a claim that all those workouts can be done on one date: the daily
cap still applies. The badge updates after recorded work and duration changes;
it can range from `1` to `60` and is hidden whenever Light is ON. Due Light is
locked ON, so an exposed `0` is no longer a valid visible state. A two-digit
badge must fit without overlapping or shrinking the modifier icon.

The compact `web/light-cadence.js` policy is shared by the instant startup
controls, main workout module, and preparation worker. Do not copy a divergent
early-loading cadence into the controls. Android and web regression tests cover
short-session aggregation, daily caps, completed versus skipped work, midnight,
legacy data, due-day locking, Light completion, and rest-day reset.

The separate within-session muscle rebalancer uses the same reviewed rating
without changing it:

| `muscularDemand` | Primary canonical muscle | Each distinct secondary |
|---:|---:|---:|
| `0` | 0.25 | 0.125 |
| `1` | 0.5 | 0.25 |
| `2` | 1 | 0.5 |

Actual repeated sets multiply those contributions. Multiple side or direction
blocks of the same exercise identity within one sequence set count once;
different linked identities count separately. The resulting canonical loads
are summed into every existing 3-, 5-, 7-, 10-, 15-, 20-, and 30-minute muscle
resolution independently.

At each resolution, the weakest bucket's share of the strongest is evaluated.
The soft goal is at least 25% at all seven resolutions. Nomadic Method chooses one legal
replacement that lexicographically improves the sorted resolution shares,
weakest first, recalculates, and repeats until all goals are met, no improvement
exists, a lineup repeats, or 30 passes complete. Exact-slot Keeps remain frozen,
and no candidate may have a lower saved score than a displaced selection in any
covered slot. The process preserves modifier eligibility, recovery rotation,
atomic sequences, unique session movements, exact duration, and repeated-set
allocation. It changes neither `muscularDemand` nor persisted user scores, and
it may deliberately stop below the goal when the catalog has no valid better
lineup.
