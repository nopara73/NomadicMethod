# Provisional exercise training levels

Version 4, 21 October 2026. Each exercise now has **an explicit set of levels it
can meaningfully train in each characteristic**. A single maximum is insufficient:
an isometric palm press can use gentle or very strong opposing force without
changing the exercise. Its strength and local-endurance sets are `[1,2,3,4,5]`.
This corrects the earlier inference that unmeasured resistance meant low resistance.

The ten characteristics remain cardiorespiratory endurance, muscular strength,
muscular endurance, flexibility, agility, balance, coordination, power, reaction
time and speed. Body composition and gait-speed biomarkers are not exercise
scoring dimensions. ACSM supplies the component framework; the five NM ability
tiers use the familiar Dreyfus names. **Neither source validates these exercise
assignments or supplies physiological cutoffs for the NM numbers.**

## Interpret a set

Numbers 1–5 mean novice, advanced beginner, competent, proficient and expert
capacity **in the directly trained action**. These are fixed, provisional ability
categories, without age/sex percentiles. They are not entry requirements, current
user abilities, workout preference scores, or guarantees of adaptation.

- `[]`: no supported direct training claim. This replaces the old scalar zero.
- `[1,2,3,4,5]`: the actual exercise can be scaled to meaningfully train all five.
- `[3,4]`: two explicitly assessed useful levels; lower levels are not inferred.
- `[1,3,4,5]`: a legitimate noncontiguous set. The context-choice reaction task
  trains initiation and response selection, but contains no stop/no-go requirement
  establishing inhibition level 2.

The maximum can be derived for display. It must never be used to reconstruct the
set or count unlisted lower levels. Omitting a level means its training claim is
not established in this assessment, rather than proof of a biological impossibility.

## Training conditions

Every exercise block remains **45 seconds**. Clean repetitions, shorter holds,
controlled force buildup, and deliberate recovery can share that opportunity.
Repeated appearances across workouts are permitted. A side/direction sequence
still consists of separate complete 45-second blocks.

Keep the actual movement, range, support, resistance mechanism and equipment.
Appropriate effort and execution pace may vary; a teaching demonstration is not
a maximum-effort prescription. No hidden weights, harder leverage, closed eyes,
unstable surfaces, new movements or unshown assistance are inferred. Physical
prerequisites still matter even when an exercise supports several levels.

| Mechanism | Meaningful scaling | Limit on the claim |
| --- | --- | --- |
| Opposing palms, contralateral resistance, or pushing against a fixed wall | Voluntary opposing force; brief high-effort contractions with recovery for strength, or lower sustained/repeated force for endurance | Not 45 seconds of uninterrupted maximal force. Force is not measured. Specialist capability in a different exercise is not guaranteed. |
| Fixed bodyweight loading | Appropriate clean repetition/hold dose within the existing movement | A pistol squat is assessed at strength `[3,4]`; it does not automatically supply novice loading or become a weighted pistol. |
| Conditioning movements | Appropriate cadence and repeated weekly exposure | One breathless 45-second appearance does not establish aerobic adaptation. Breathing drills alone do not earn aerobic ratings. |
| Power and speed | Fresh high-intent efforts with deliberate recovery | Continuous fatigued maximal output is not required; technical range and movement identity remain intact. |
| Flexibility and balance | Practice the actual demonstrated range/support task using tolerable, clean attempts | Ordinary stretches do not silently become full splits; ordinary stance does not silently become inverted or eyes-closed balance. Transfer is task-specific. |
| Coordination | Acquisition through fluent, precise execution of the actual pattern | Tutting and arm waves can support specialist coordination without requiring an aerial skill. No transfer to unrelated patterns is assumed. |
| Reaction | An implemented unpredictable cue and its real response task | Cue density does not establish faster latency. The app does not measure response accuracy or reaction time. |

Nine existing identities receive strength/endurance `[1,2,3,4,5]`: 241, 519,
560, 614, 745, 149, 585, 586 and 587. IDs 269, 1001, 1105 and 1106 receive
`[1,2,3,4]` because the resisting limb, bracing or position may limit the task.
The four self-resisted neck exercises retain `[1,2,3]`; their current protocol
does not establish a specialist maximal-loading prescription.

Internally generated and isometric loading can produce training adaptations,
but the studies do **not** validate the NM level assignments. See the primary
[no-external-load experiment](https://pubmed.ncbi.nlm.nih.gov/27329807/) and
[isometric training experiment](https://doi.org/10.1152/japplphysiol.00091.2016).
The [balance-transfer experiment](https://pubmed.ncbi.nlm.nih.gov/26298214/)
also cautions against treating task improvement as universal balance improvement.

## Coverage and admissions

Require **at least five distinct approved session movements that can train each
characteristic-level combination**. One exercise may count at several supported
levels and for several characteristics. Deduplicate `SessionMovementId`
independently in every cell. Sides, directions, sets, aliases and renamed
duplicates never multiply identity. This is a global catalog rule, not an
additional quota for every modifier combination.

Reassessing the original 853 records reduced the deficits from 21 cells / 92
placements to 7 cells / 33 placements before any admissions. Existing adjustable
resistance removed the need to source specialist strength/endurance exercises.
The selected release adds 16 actual movement records and 15 choice protocols,
rather than the earlier pending 48-record package. Five of the movements also
provide advanced balance, and three supply the remaining specialist-coordination
claims. Revision 88 contains 884 records; revision 89 adds the six reviewed tasks
described below, bringing the catalog to 890 records.

The existing 30 stop/go records retain their original playback and supply
reaction levels `[1,2]`. The new five physical response families each provide
two-choice `[1,3]`, four-choice `[1,3,4]`, and context-choice `[1,3,4,5]` protocols.
Revision 89 adds one explicit arm-raise Initiation protocol for a precise tier-1
reaction contribution. All protocol variants of a physical
response family share one session identity, including the original physical
source where applicable. Lower-level coverage does not manufacture a new response task.

New protocols use the canonical Android `reaction_drills.json` asset and exact
human response clips. Every response and preview is prepared before cueing; the
context is displayed before the unpredictable target, and feedback follows the
decision window. A paused or interrupted trial restarts from Ready. No new cue
is issued unless its complete response and reset fit in the remaining block.
Existing unfinished workouts retain their prior catalog and selections.

## Precise profile contributors, revision 89

The training-set quota and the spiderweb use different evidence. A set such as
`[4,5]` legitimately counts in both training cells, but cannot supply a precise
profile value. The new admissions retain all previous ranges and ratings:

- One-arm push-ups: fixed unilateral floor press, strength `[4]`.
- Floor tuck planche: unsupported straight-arm tucked hold, strength and balance `[3]`.
- Crow: bent-arm hold with knees supported on the upper arms, balance `[3]`.
- Full floor planche: fixed straight-joined-leg horizontal lever, strength `[5]`.
- Bent-arm press to handstand: the demonstrated bent-arm entry, not a straight-arm
  press; strength, balance and coordination `[4]`.
- Arm raise reactions · single cue: unpredictable initiation `[1]`, with no
  response-choice, inhibition, measured-latency or conditioning claim.

The narrower task descriptions establish the provisional NM categories; no
source supplies validated physiological cutoffs. All dose-dependent endurance
claims retain ranges. Clean attempts or short holds with recovery fit inside the
same 45-second opportunity. Expert strength does not require a 45-second full
planche. Sources and exact media hashes are recorded in
[`profile-admissions-2026-10-22.json`](catalog-audit/profile-admissions-2026-10-22.json).
The generated gap report now includes a separate diagnostic table computed by
the actual profile policy, so broad training coverage cannot conceal empty
spiderweb tiers. It creates no quota to justify artificially precise ratings.

## Data and checks

- [`ExerciseFitnessCeilingScores.json`](../tools/ExerciseFitnessCeilingScores.json):
  explicit level sets, rationale, training conditions and assigned stable IDs.
- [CSV export](catalog-audit/acsm-fitness-ceiling-scores.csv): supported numeric
  levels separated by semicolons; an empty cell has no claim. Includes scope
  through the primary muscle, rationale and training conditions.
- [Coverage JSON](catalog-audit/acsm-fitness-ceiling-coverage.json) and
  [readable report](catalog-audit/acsm-fitness-ceiling-gaps.md): supported-level
  membership, deduplicated identities and every remaining deficit.
- [Coverage policy](../tools/ExerciseFitnessCeilingCoveragePolicy.json): minimum
  five, explicit supported-level matching and session-movement identity.
- [Media and admission audit](catalog-audit/acsm-fitness-ceiling-candidates.json):
  source provenance, review results and deferred candidates.

Historical filenames retain “ceiling” for continuity. Version 4 data uses
`representation: "trainable-level-sets"`. Nothing infers ratings from names,
muscular demand, or a coverage quota. The normalized-LF catalog hash requires
deliberate reassessment after catalog changes. Fitness ratings do not alter
preference scores, Keeps, muscle-recovery timestamps or Light ranking.

```powershell
node tools/generate-exercise-fitness-ceiling-scores.mjs
node tools/generate-exercise-fitness-ceiling-scores.mjs --check
node tools/generate-reaction-drill-media.mjs --check
node --test web/tests/fitness-ceiling-scores.test.js web/tests/reaction-drills.test.js
```

Generation and `--check` reject any coverage deficit. `--report-only` can write
an honest failing sourcing report, but cannot be combined with `--check`.
Validation also rejects absent/duplicate IDs, missing dimensions or conditions,
invalid/unsorted/repeated levels, unsupported reaction tasks, power/speed claims
for still holds, changed response assets, and stale exports.

Framework references: [ACSM roundtable](https://doi.org/10.1249/MSS.0000000000003997),
[Dreyfus](https://www.bumc.bu.edu/facdev-medicine/files/2012/03/Dreyfus-skill-level.pdf),
[NSCA speed-session design](https://www.nsca.com/education/articles/kinetic-select/designing-speed-training-sessions/).
