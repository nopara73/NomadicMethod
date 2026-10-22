# Homepage fitness profile

Android and web draw the same ten-axis spiderweb below the setup controls. The only visible text is the lowercase characteristic labels: cardio, strength, muscle endurance, flexibility, agility, balance, coordination, power, reaction and speed. Four rings represent levels 1–4 on the fixed 1–5 scale; expert level 5 extends outside the web into reserved space. Fractional averages remain precise internally; displayed vertices and accessibility descriptions round to the nearest whole level, with halves rounding upward (2.4 → 2, 2.5 → 3). An unknown zero stays at the centre. The scale never normalizes to the user's current highest value. The profile uses the app's charcoal fill and bright green outline and dots, with a faint green web inside the fill and a muted charcoal web outside it. Measured label widths and outward text alignment make room for a larger web without enlarging the 12sp/0.75rem labels. There are no colored outer arcs, health/athletic distinction, visible numbers, title, legend, assessment controls or extra feedback questions.

The calculation reads current saved Keeps, globally across slots and equipment settings. Slot-scoped Keeps are authoritative; the legacy kept-ID list is a fallback when no slot map exists. It never reads historical votes, dates, completion counts or preference scores, and never modifies workout state.

For each currently kept exercise:

1. Find its strongest rating across all ten characteristics, using the largest supported level.
2. Contribute only to characteristics whose complete rating is exactly that one level. All precise ties qualify. Ambiguous sets never contribute, and an ambiguous stronger characteristic does not promote a lower secondary characteristic.
3. Deduplicate SessionMovementId across slots, sides, directions and aliases. If kept variants of one movement have different qualifying values, retain the highest qualifying value in each characteristic once.
4. Average the qualifying values independently in each characteristic. For speed-focused movements rated 2, 3 and 4, the speed result is 3. An empty group has radius zero.

These are the agreed NM estimates from hearted exercise ratings, not measured physiological abilities. A zero means no qualifying current Keep. Exercise training ratings remain provisional and unchanged.

The canonical runtime asset is NomadicMethod/Assets/fitness_ratings.json. It is generated from the reviewed explicit level sets by tools/generate-exercise-fitness-ceiling-scores.mjs and verified by its --check gate. The web build copies and fingerprints that Android asset; it has no independently authored ratings. Shared fixture cases exercise both implementations.

## Limits of precise contributions

At revision 89, 620 of 890 records have no qualifying precise strongest rating.
Six additions open new contributions without changing any of the previous 884
records or narrowing their training ranges:

| Exercise | Precise strongest contributions |
| --- | --- |
| Arm raise reactions · single cue | reaction 1 |
| Floor tuck planche hold | strength 3; balance 3 |
| Crow hold | balance 3 |
| One-arm push-ups | strength 4 |
| Bent-arm press to handstand | strength 4; balance 4; coordination 4 |
| Full floor planche hold | strength 5 |

These are provisional task-specific NM judgments, not validated physiological
thresholds or proof that a hearted exercise was performed successfully. The
planche rating refers to the actual straight-arm, straight-joined-leg horizontal
hold; its tucked entry is not rated as expert strength. The single-cue reaction
task uses the existing unpredictable Initiation protocol, not a predictable loop.
It shares movement identity 266 with the arm-raise family and cannot multiply it.

Remaining possible contributions are cardio 2–3; strength 2–5; muscle endurance
1–4; flexibility 1–3; agility 2; balance 1–4; coordination 1, 2 and 4; power 2–4;
reaction 1; and speed 2. Adding drills with adjustable cadence does not establish
a precise advanced speed or agility tier. Retain those limits instead of
narrowing honest ranges. The generated
[coverage report](catalog-audit/acsm-fitness-ceiling-gaps.md) reports usable
spiderweb contributors separately from the five-per-training-tier catalog gate.

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
schema, visible control, assessment question or change to the spiderweb.
