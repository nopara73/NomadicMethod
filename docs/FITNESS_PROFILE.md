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

## Preferences for fresh choices

Fitness changes only the planner's existing random ordering among otherwise
equally suitable choices. Equipment, anatomy, complete atomic sequences, Light,
recovery, saved feedback and Keep priorities retain their existing precedence.
Workout restoration and chart updates do not regenerate a persisted lineup.

Round known user averages with the display rule, then find the smallest absolute
distance between any known axis and any explicitly supported level of the
candidate. Empty cells and unknown user axes do not participate. Ranges and
noncontiguous sets participate by their actual members, without interpolation.
One matching characteristic suffices; additional matches do not add weight.

Distances 0–4 receive relative weights 1, 1/2, 1/4, 1/8 and 1/16. A candidate
without a comparable rating receives neutral weight 1. Every otherwise eligible
candidate remains available, including the two entirely unrated breathing drills.
These are relative preferences, not admission percentages or evidence of adaptation.

For an atomic sequence, use the strongest weight among its distinct members.
Group eligible roots by SessionMovementId, take the maximum eligible variant
weight for one family draw, then order variants within each family by their
weights. Sides, aliases, placements and repeated sets do not buy family draws.
Only complete placements that fit the duration may affect these weights.
Generate weighted exponential keys once per selection operation:
`-ln(U) / weight`, ascending, with `U = 1 - random()` in (0,1].
Reuse that order for every branch of the lineup solver and its repeat fallback.
The foreground and preparation workers receive the same canonical read-only
rating dependency. Fitness introduces no saved-state schema or new user input.
