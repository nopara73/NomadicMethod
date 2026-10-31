# Homepage fitness profile

Android and web draw the same ten-axis spiderweb below the setup controls. Its lowercase labels are cardio, strength, muscle endurance, flexibility, agility, balance, coordination, power, reaction and speed. Four rings represent levels 1–4 on the fixed 1–5 scale; expert level 5 extends outside the web into reserved space. Fractional averages remain precise internally; displayed vertices and accessibility descriptions round to the nearest whole level, with halves rounding upward (2.4 → 2, 2.5 → 3). The scale never normalizes to the user's current highest value. The profile uses the app's charcoal fill and bright green outline and dots, with a faint green web inside the fill and a muted charcoal web outside it.

An axis without qualifying evidence has a dashed charcoal spoke and hollow marker at the web boundary, with no green value dot. Its numeric calculation still uses the existing empty-group sentinel at the centre; the hollow marker is an unknown indicator, never a level-4 value. A known low level retains its green dot at the correct radius. Accessibility announces “no evidence yet” rather than level zero or measured inability.

Tapping an axis or its label opens a read-only details dialog. Labels retain their keyboard, screen-reader and 48dp/CSS-pixel touch targets. The dialog shows the rounded level, the equally best-fitting levels and one contributing movement per SessionMovementId. Each row shows the exact exercise rating, latest love/easy/hard response and supported levels. Unknown axes distinguish absent evidence from feedback that still fits every level. Opening and closing the inspector never writes workout state or prepares a lineup. Chart layout, scale, colours and fixed-size labels are unchanged.

## Difficulty evidence

The chart reads recorded love (`Tick`), too-easy and too-hard decisions from saved workout history and the active session, including interrupted sessions. Meh (`X`), timeouts, neutral outcomes and pending gestures neither contribute nor erase the last informative response. Saved Keeps without a recorded response do not imply difficulty evidence. Existing history is read without mutation.

1. Select the latest love/easy/hard response per SessionMovementId across all slots, sides and aliases. Order by decision timestamp; equal timestamps use session ID and then recorded decision order. The atomic selection root supplies one response; sides, blocks and sets never multiply it.
2. Use that exact exercise's complete canonical 1–5 ratings and reviewed training-characteristic flags. Every axis has a nonempty rating. Find the highest supported level among its reviewed training characteristics. For love (just right) and too hard, only reviewed training characteristics whose maximum equals that level qualify, including ranges and ties. Too easy qualifies every reviewed training characteristic, including weaker trained demands. Minimum ratings on untrained axes do not create evidence. A newer alias with no reviewed training claims replaces old evidence rather than reviving stale claims.
3. Interpret feedback against the whole rated set: love supports exactly its listed levels; too easy supports all levels above its maximum; too hard supports all levels below its minimum. Saturate too-easy-at-5 to `[5]` and too-hard-at-1 to `[1]`. Thus `[2,3]` yields `[4,5]`, `[2,3]` or `[1]` for easy, love or hard respectively. `[1,2,3,4,5]` yields `[5]`, all five, or `[1]`.
4. For each candidate integer level 1–5, sum the squared distance to the nearest supported member of each movement's evidence set. Movements have equal weight. Keep every candidate with the minimum sum; average those equally good candidates and round half upward only for display. Noncontiguous sets retain their holes. The average is a summary and need not itself be a member of a noncontiguous best-fit set.
5. No evidence, or equal support for all five candidates, remains unknown (internal zero). In particular, full-range love alone does not fabricate level 3; when combined with informative evidence it adds zero penalty everywhere and cannot dilute that evidence.

Examples: easy at 2 and hard at 4 agree on 3. Love at 2 and love at 4 disagree, with squared-distance compromise 3. Easy at 3 and hard at 4 tie at 3 and 4, giving 3.5 internally and 4 on the web. Repeated feedback replaces the same movement's response without adding votes. There is no age decay in this version.

These are provisional training-level estimates under the owner's feedback interpretation, not measured ability. In particular, the chosen whole-range rule deliberately maps a full-range self-resisted exercise marked easy to 5 and hard to 1; the app does not measure applied resistance.

The canonical ratings asset remains `NomadicMethod/Assets/fitness_ratings.json`. Existing reviewed training levels are preserved; complete minimum ratings do not create additional training claims. Shared `fitness-difficulty-cases.json` fixtures verify both platforms' ranges, noncontiguous sets, conflicts, ties, endpoint saturation, unknowns, latest-response replacement and movement deduplication.

## Completion changes

New workouts capture the same difficulty-derived profile at activation and freeze it after final feedback. The existing completion list compares fractional means to one decimal; it introduces no new assessment UI. Snapshots carry an evidence version so an old favourites-based baseline is never compared with a difficulty-derived value. A workout started before this change has no comparable baseline. Completed older snapshots remain stored, but their obsolete comparison is not displayed. Cold reopening retains all workout progress and frozen snapshots. Love can update the profile; meh alone cannot. Evidence version 3 prevents comparisons with the old favourites baseline (version 0), the singleton-only easy/hard baseline (version 1), or the previous strongest-only treatment of too easy (version 2). Existing saved decisions are interpreted by the current rule without rewriting their history; unfinished sessions retain their existing selections and progress.

## Exercise selection

Selection now uses the same latest love/easy/hard evidence and exact best-fit level sets as the chart. Display rounding does not change membership. A movement qualifies when at least one known characteristic shares any explicitly rated level with that characteristic's best-fit set. For zone `[3,4]`, `[3]`, `[4]`, `[3,4]`, `[2,3]` and `[4,5]` all qualify; `[1,2]` and `[5]` do not. A user at `[5]` can select sprinting rated `[3,4,5]` through cardio. Ranges describe supported training options, so levels outside the user's zone do not disqualify the shared option. Noncontiguous holes remain holes. Every distinct member of an atomic sequence must qualify; sides and aliases never create extra family draws.

Unknown axes cannot qualify an exercise. If the entire profile is unknown (including only full-range love), ordinary selection bootstraps feedback. Missing, empty or invalid exercise ratings are rejected before planning. For exercises with training claims, only those characteristics can supply a match. Minimum-only exercises must match every known user characteristic, so minimum demand is never a universal fitness exemption. There is no +1 priority, nearest-level fallback, hidden relaxation or automatic reset of the profile.

Equipment, truthful anatomical coverage, complete sequences/durations, Light and recovery retain their existing constraints and priorities. Eligible loved movements keep repeat priority. A later easy/hard response removes the family's saved Keeps across slots without a dislike penalty or rewriting history; a later love can earn a Keep again. Meh retains its existing preference/refusal behavior and supplies no difficulty evidence.

The filter applies to new lineups, shuffle replacements, balancing replacements and newly planned portions of duration/modifier edits. Saved unfinished selections, completed work and the current atomic selection retain the existing preservation guarantees. A chart update does not regenerate them. If no complete eligible lineup exists, preparation fails without saving its speculative state and both platforms show: “No complete workout fits your current training zone with these settings.” Users can change their existing settings; the app does not invent a workout or alter their feedback to unblock it.

Shared validation rejects empty exercise ratings. Eligibility fixtures cover overlapping levels, unknown user axes, noncontiguous sets, any-characteristic matching and complete sequence membership. Planner tests cover favourite priority, family refusals, easy/hard Keep removal, Light/recovery, shuffle, no-solution behavior and unfinished-workout restoration. Canonical ratings remain read-only and the saved-state schema is unchanged. The [overlap coverage audit](catalog-audit/overlap-coverage-2026-10-25.md) records remaining global and whole-workout gaps.

## Historical catalog diagnostics

Revision 90 contains 906 records. It adds 11 reviewed physical movements and
five stop-only reaction protocols; all 890 previous records and assessments are
unchanged. Every reviewed training cell on these additions is a singleton.
Other characteristics carry an explicit minimum rating of 1 with their
training-characteristic verdict set to false; no exercise rating is empty.

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
## Candidate ordering

Within the eligible pool, the existing planner compares Light, contextual Keep preference, refusals, recovery, saved score, equipment and muscle coverage. Fitness is an eligibility rule rather than a scoring dimension. Randomness only breaks ties: one draw per movement family and variant draws within that family, reused throughout the solver. The same rating dependency reaches Android, web and the web preparation worker.
