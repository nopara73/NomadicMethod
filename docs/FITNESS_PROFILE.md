# Homepage fitness profile

Android and web draw the same ten-axis spiderweb below the setup controls. Its lowercase labels are cardio, strength, muscle endurance, flexibility, agility, balance, coordination, power, reaction and speed. Four rings represent levels 1–4 on the fixed 1–5 scale; expert level 5 extends outside the web into reserved space. Fractional averages remain precise internally; displayed vertices and accessibility descriptions round to the nearest whole level, with halves rounding upward (2.4 → 2, 2.5 → 3). The scale never normalizes to the user's current highest value. The profile uses the app's charcoal fill and bright green outline and dots, with a faint green web inside the fill and a muted charcoal web outside it.

An axis without qualifying evidence has a dashed charcoal spoke and hollow marker at the web boundary, with no green value dot. Its numeric calculation still uses the existing empty-group sentinel at the centre; the hollow marker is an unknown indicator, never a level-4 value. A known low level retains its green dot at the correct radius. Accessibility announces “no evidence yet” rather than level zero or measured inability.

Tapping an axis or its label opens a read-only details dialog. Labels retain their keyboard, screen-reader and 48dp/CSS-pixel touch targets. The dialog shows the rounded level, the equally best-fitting levels and one contributing movement per SessionMovementId. Each row shows the exact exercise rating, latest love/easy/hard response and supported levels. Unknown axes distinguish absent evidence from feedback that still fits every level. Opening and closing the inspector never writes workout state or prepares a lineup. Chart layout, scale, colours and fixed-size labels are unchanged.

## Difficulty evidence

The chart reads recorded love (`Tick`), too-easy and too-hard decisions from saved workout history and the active session, including interrupted sessions. Meh (`X`), timeouts, neutral outcomes and pending gestures neither contribute nor erase the last informative response. Saved Keeps without a recorded response do not imply difficulty evidence. Existing history is read without mutation.

1. Select the latest love/easy/hard response per SessionMovementId across all slots, sides and aliases. Order by decision timestamp; equal timestamps use session ID and then recorded decision order. The atomic selection root supplies one response; sides, blocks and sets never multiply it.
2. Use that exact exercise's current canonical ratings. Find the highest supported level across its ten characteristics. Every nonempty characteristic whose maximum equals that level qualifies, including ranges and ties. Weaker characteristics remain excluded. A newer unrated alias replaces old evidence rather than reviving stale ratings.
3. Interpret feedback against the whole rated set: love supports exactly its listed levels; too easy supports all levels above its maximum; too hard supports all levels below its minimum. Saturate too-easy-at-5 to `[5]` and too-hard-at-1 to `[1]`. Thus `[2,3]` yields `[4,5]`, `[2,3]` or `[1]` for easy, love or hard respectively. `[1,2,3,4,5]` yields `[5]`, all five, or `[1]`.
4. For each candidate integer level 1–5, sum the squared distance to the nearest supported member of each movement's evidence set. Movements have equal weight. Keep every candidate with the minimum sum; average those equally good candidates and round half upward only for display. Noncontiguous sets retain their holes. The average is a summary and need not itself be a member of a noncontiguous best-fit set.
5. No evidence, or equal support for all five candidates, remains unknown (internal zero). In particular, full-range love alone does not fabricate level 3; when combined with informative evidence it adds zero penalty everywhere and cannot dilute that evidence.

Examples: easy at 2 and hard at 4 agree on 3. Love at 2 and love at 4 disagree, with squared-distance compromise 3. Easy at 3 and hard at 4 tie at 3 and 4, giving 3.5 internally and 4 on the web. Repeated feedback replaces the same movement's response without adding votes. There is no age decay in this version.

These are provisional training-level estimates under the owner's feedback interpretation, not measured ability. In particular, the chosen whole-range rule deliberately maps a full-range self-resisted exercise marked easy to 5 and hard to 1; the app does not measure applied resistance.

The canonical ratings asset remains `NomadicMethod/Assets/fitness_ratings.json`. No catalog ratings or selection rules change. Shared `fitness-difficulty-cases.json` fixtures verify both platforms' ranges, noncontiguous sets, conflicts, ties, endpoint saturation, unknowns, latest-response replacement and movement deduplication.

## Completion changes

New workouts capture the same difficulty-derived profile at activation and freeze it after final feedback. The existing completion list compares fractional means to one decimal; it introduces no new assessment UI. Snapshots carry an evidence version so an old favourites-based baseline is never compared with a difficulty-derived value. A workout started before this change has no comparable baseline. Completed older snapshots remain stored, but their obsolete comparison is not displayed. Cold reopening retains all workout progress and frozen snapshots. Love can update the profile; meh alone cannot. Evidence version 2 prevents comparisons with either the old favourites baseline (version 0) or the previous singleton-only easy/hard baseline (version 1).

## Selection remains unchanged

This release changes the displayed estimates only. The planner retains its existing favourites-based calculation, including precise strongest ratings, movement deduplication and mean rounding. It does not consume the new chart values. Keeps, scores, recovery, equipment restrictions, fresh-choice tiers and persisted unfinished selections retain their previous behaviour. The discovery order below still describes that separate favourites-based input.

## Catalog diagnostics for the unchanged favourites-based planner

Revision 90 contains 906 records. It adds 11 reviewed physical movements and
five stop-only reaction protocols; all 890 previous records and assessments are
unchanged. Every nonempty rating cell on these additions is a singleton. Empty
cells mean no supported claim, not an assigned level zero.

| New exercise | Precise strongest profile contributions |
| --- | --- |
| Straight-leg cartwheels | coordination 3 |
| One-arm cartwheels | coordination 4 |
| Side aerials | coordination 5 |
| Straight-leg firefly hold | flexibility 3; balance 3; coordination 3 |
| Eight-angle hold | strength 3; balance 3; coordination 3 |
| Freestanding forearm stand hold | balance 4 |
| Floor 90-degree handstand push-ups | strength 5 |
| Kneeling full pigeon hold | flexibility 4 |
| Feet-to-head handstand scorpion hold | flexibility 5 |
| Aztec push-ups | power 5 |
| Superman push-ups | power 4 |
| Straight punch, knee lift, arm raise, front kick and crouching reach stops | reaction 2, five distinct existing physical families |

Full ten-characteristic assessments, original sources, exact media hashes,
observed mechanics and rejected footage are in the
[admission audit](catalog-audit/precise-admissions-2026-10-23.json).
The [workout gallery](catalog-audit/evidence/2026-10-23/precise-admissions/gallery.html)
distinguishes actual workout stills from separately played supporting movies.
Its isolated synthetic fixtures never touch phone data.

These remain provisional NM task categories, not physiological measurements or
proof that a hearted movement was performed successfully. Physical additions
prescribe quality attempts, brief holds or fresh explosive efforts with recovery
inside 45 seconds. They do not infer a duration-dependent endurance grade.
Previously assessed ranges remain intact.

Stop-only trials prepare one known action at the end of a predictable 3, 2, 1.
An occasional unpredictable STOP cancels it. This separates inhibition from
unpredictable initiation and response choice; it claims reaction 2 only.
The [anticipated-response inhibition research](https://pmc.ncbi.nlm.nih.gov/articles/PMC9170665/)
supports that task distinction, not the validity of NM's whole-body adaptation
or its numeric tier. No latency or accuracy is measured. Each protocol retains
its source's movement identity and complete side requirements.

Revision 91 adds the straight-arm straddle press to handstand while retaining
all 906 previous exercises and assessments unchanged. It contributes strength 4,
balance 4 and coordination 4, increasing each of these sparse contributor sets
from two to three movements. It requires floor access and excludes Hard Floor.
The [admission audit](catalog-audit/ability-admissions-2026-10-23.json)
records all ten characteristic claims, original footage, equipment and anatomy.
The [workout review](catalog-audit/evidence/2026-10-23/ability-admissions/gallery.html)
covers the final packaged media and timer phases. Unfinished older workouts
retain their original catalog; the press becomes available to new workouts.
An elevated one-arm flag candidate was deferred: its complete mirrored pair
needs two blocks in one primary slot and cannot fit a new 15-minute whole-body
lineup. Secondary muscle claims cannot turn it into a two-primary-slot sequence.
No side, anatomy or scheduling rule was weakened to admit it.

Possible precise catalog ratings (before feedback adjustment) are now cardio 2–3; strength 2–5; muscle
endurance 1–4; flexibility 1–5; agility 2; balance 1–4; coordination 1–5;
power 2–5; reaction 1–2; and speed 2. Cardio, endurance, agility, balance, reaction and
speed still have no precise level-5 contributor. Adding equipment alone cannot
close those gaps; it must enable a genuinely supported precise assessment.
The averaging rule also means that retaining lower adjusted contributions
can keep an axis below 5 even with a contribution of 5. Cadence-dependent drills
do not acquire a fixed speed/cardio tier merely because they look difficult.

The generated [coverage report](catalog-audit/acsm-fitness-ceiling-gaps.md)
separates usable spiderweb evidence from training-set coverage. The latter
continues to meet five distinct movements in every one of the 50 cells.
It does not authorize narrowing an honest range or manufacturing precise scores.
## Discovery order

Eligible saved Keeps take priority over discovering a new movement, including
fresh hard work. Light still prioritizes complete demand-zero sequences; a
recovering or phase-downvoted Keep retains its saved identity but does not get
the ordinary fresh Keep preference. Restoring an unfinished workout, opening
the chart or changing hearts does not regenerate its persisted selections.

For new choices with usable profile evidence, exhaust these tiers in order:

1. One level above any known, rounded user characteristic.
2. The current level in any known characteristic.
3. One level below, then two, three and four levels below.
4. No comparable rating, then larger upward jumps (+2, +3, +4), as coverage fallbacks.

An exercise belongs to its earliest matching tier, even if it matches several
abilities. Ranges participate through their explicit members; noncontiguous
sets never imply missing levels. Unknown user axes and empty rating cells do
not count as level zero. Level 5 cannot generate a level-6 target. If the profile
provides no comparison, ordinary planner priorities apply without a fitness
preference. The planner’s separate favourites-based calculation and its exclusion of ambiguous ranges are
unchanged. A supported range is a training option, not a claim that the app
prescribes a particular resistance, cadence or more difficult execution.

Refusing a movement allows +1 discovery to continue in any other ability. There
is no retry cap. Shuffle excludes the entire SessionMovementId family for the
rest of the active search, including aliases whose ratings would otherwise put
them in another tier. Those exclusions survive cold reopen. Saved phase-local
feedback also follows the family when ranking new choices: unrefused families
precede refused ones, then lesser refusal depth precedes greater depth. Existing
legacy negative scores remain evidence of refusal. This reads the existing
feedback without inventing votes for aliases or rewriting scores, Keeps or
history. A no-heart decision after performing a sequence updates feedback for
future choices; it does not insert another performed block.

Equipment, anatomical coverage and complete sequences that fit the duration
remain prerequisites. Recovery outranks upward exploration. The exact lineup
solver maximizes eligible Keeps and +1 opportunities before current/lower tiers,
with saved-score and equipment preferences resolving remaining choices. No
fitness tier is a compatibility ban: if completing the lineup requires an
otherwise less-preferred movement, coverage remains possible. After every
family has previously been refused, least-refused families can be considered
again in a future search; current-search shuffle exclusions are never recycled.
Muscle balancing cannot replace a selection with a worse fitness tier or a
more-refused family. Completed and current atomic work remains protected during
duration/modifier changes.

Randomness only breaks ties: one uniform draw per movement family, then variant
draws, reused throughout one solver operation. Sides, placements, aliases and
repeated sets do not buy additional family draws. Canonical read-only ratings
are shared by Android, web and the preparation worker. There is no new saved-state
schema or assessment question. The axis inspector explains difficulty feedback; selection retains its separate favourites-based input.
