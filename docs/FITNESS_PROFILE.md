# Homepage fitness profile

Android and web draw the same ten-axis spiderweb below the setup controls. Its lowercase labels are cardio, strength, muscle endurance, flexibility, agility, balance, coordination, power, reaction and speed. Four rings represent levels 1–4 on the fixed 1–5 scale; expert level 5 extends outside the web into reserved space. Fractional averages remain precise internally; displayed vertices and accessibility descriptions round to the nearest whole level, with halves rounding upward (2.4 → 2, 2.5 → 3). The scale never normalizes to the user's current highest value. The profile uses the app's charcoal fill and bright green outline and dots, with a faint green web inside the fill and a muted charcoal web outside it.

An axis without qualifying evidence has a dashed charcoal spoke and hollow marker at the web boundary, with no green value dot. Its numeric calculation still uses the existing empty-group sentinel at the centre; the hollow marker is an unknown indicator, never a level-4 value. A known low level retains its green dot at the correct radius. Accessibility announces “no evidence yet” rather than level zero or measured inability.

Tapping an axis or its label opens a read-only details dialog. Labels are real keyboard and screen-reader buttons with at least 48dp/CSS-pixel targets. The dialog shows the rounded level, mean (at most two decimal places, with an ellipsis for further digits; display never rounds across a half-level boundary), and each contributing favourite movement with its exact counted level. It shows one saved representative per SessionMovementId: the highest qualifying variant for that axis, breaking equal-level ties by lowest exercise ID. Rows are ordered by movement ID on both platforms. Ambiguous, weaker, unsaved, retired and duplicate claims never become extra rows. The calculation and inspector share the same contribution pipeline. Unknown axes explain that no current favourite has a qualifying rating, without suggesting inability.

Closing details returns focus to its axis. Opening, reading and closing never write workout state, prepare a lineup, change Keeps or scores, or update recovery. The overlay also works while editing an unfinished workout's settings. Measured 12sp/0.75rem labels retain the radial layout where they fit; at enlarged text sizes they move into two columns beneath the intact web. Numbers and explanation appear only on demand. There is no title, legend, assessment question or extra setup feedback.

The calculation reads current saved Keeps, globally across slots and equipment settings. Slot-scoped Keeps are authoritative; the legacy kept-ID list is a fallback when no slot map exists. It never reads historical votes, dates, completion counts or preference scores, and never modifies workout state.

For each currently kept exercise:

1. Find its strongest rating across all ten characteristics, using the largest supported level.
2. Contribute only to characteristics whose complete rating is exactly that one level. All precise ties qualify. Ambiguous sets never contribute, and an ambiguous stronger characteristic does not promote a lower secondary characteristic.
3. Deduplicate SessionMovementId across slots, sides, directions and aliases. If kept variants of one movement have different qualifying values, retain the highest qualifying value in each characteristic once.
4. Average the qualifying values independently in each characteristic. For speed-focused movements rated 2, 3 and 4, the speed result is 3. An empty group has radius zero.

These are the agreed NM estimates from hearted exercise ratings, not measured physiological abilities. A zero means no qualifying current Keep. Existing exercise training ratings remain provisional and unchanged.

The canonical runtime asset is NomadicMethod/Assets/fitness_ratings.json. It is generated from the reviewed explicit level sets by tools/generate-exercise-fitness-ceiling-scores.mjs and verified by its --check gate. The web build copies and fingerprints that Android asset; it has no independently authored ratings. Shared fixture cases exercise both implementations.

## Completion changes

The normal completion screen shows only changed favourite-derived estimates,
as a small list such as `agility 1.5 → 1.6`. It uses the unrounded means above,
with one decimal and half-up rounding. Changes that produce the same displayed
decimal are omitted. A dash means no qualifying evidence, including when an
axis gains or loses its last qualifying Keep. Decreases are shown as directly
as increases. The chart's whole-number rounding remains unchanged.

Activation captures the baseline after preparation, before any workout feedback.
The last decision settles the recorded Keeps using the same rule as Done, then
freezes the final profile. Both arrays persist with workout state through cold
reopen, pauses, duration and modifier edits. Reopening unacknowledged completion
uses those saved values, even after a ratings update. Done clears the snapshots.
Legacy sessions with no baseline show no comparison; neither missing evidence
nor a missing baseline is reconstructed as a measured zero. Explicit End keeps
its existing interrupted-session path. No progress message appears for an empty
list. These changes describe estimates from favourites, not physiological gains.

## Limits of precise contributions

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

Possible precise contributions are now cardio 2–3; strength 2–5; muscle
endurance 1–4; flexibility 1–5; agility 2; balance 1–4; coordination 1–5;
power 2–5; reaction 1–2; and speed 2. Cardio, endurance, agility, balance, reaction and
speed still have no precise level-5 contributor. Adding equipment alone cannot
close those gaps; it must enable a genuinely supported precise assessment.
The unchanged averaging rule also means that retaining lower-level contributors
can keep an axis below 5 even when a level-5 movement is kept. Cadence-dependent drills
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
preference. The profile calculation and its exclusion of ambiguous ranges are
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
schema or assessment question. The axis inspector above explains the existing profile without changing selection.
