# Provisional exercise training ceilings

Status: **fresh assessment, version 3, 21 October 2026**. All 853 exercises in
catalog revision 87 have explicit assessments across ten characteristics.
The prior ratings, anchor scores, keyword scoring rules, coverage overrides,
and requirement to fill every characteristic/level cell have been discarded.
These estimates are saved for later discussion; they do not rank workouts,
change user preference scores, or alter recovery or exercise playback.

## Read the scores

A score estimates the highest ability tier to which repeated practice of this
specific exercise could meaningfully contribute. It is **not an entry
requirement**, a difficulty score, a user's current ability, or a guaranteed
outcome. Every work block lasts **45 seconds**, at the demonstrated range,
cadence, support and resistance. Repeated appearances in normal workouts are
allowed; added weights, faster unshown execution and harder variations are not.
An atomic side/direction sequence still consists of separate 45-second blocks;
its duration does not silently become an uninterrupted endurance set.

| Value | Interpretation |
| ---: | --- |
| 0 | No meaningful direct training claim on this characteristic. This is absence, not a sixth ability tier. |
| 1 | Novice: elementary capacity in the directly trained task. |
| 2 | Advanced beginner: useful basic capacity beyond initial familiarization. |
| 3 | Competent: substantial, repeatable task-specific capacity. |
| 4 | Proficient: advanced capacity in that particular movement or task. |
| 5 | Expert: specialist capacity requiring a particularly strong training case. |

The same criteria apply to everyone, without age/sex percentiles. These are
judgment-based NM tiers. ACSM supplies the component definitions and Dreyfus
supplies the familiar five names; neither validates these ceilings. The earlier
proposed numerical test cutoffs are not used to justify or regenerate ratings.
No exercise currently receives a 5. Empty high tiers are an honest outcome of
this pass and do not constitute catalog deficits.

## Characteristic-specific interpretation

| Characteristic | What earns a rating | How this pass distinguishes higher tiers |
| --- | --- | --- |
| Cardiorespiratory endurance | Repeated work involving substantial muscle mass and sustained oxygen demand. | Gentle rhythmic steps begin at 1; jacks/shuffles at 2; vigorous running/jumping/full-body intervals at 3; actual sprinting or especially demanding whole-body transitions can reach 4. Breathing exercises and isolated muscle fatigue do not automatically qualify. |
| Muscular strength | Meaningful direct resistance for the named local action. | Mild supported/unloaded resistance starts at 1, ordinary loaded bodyweight actions at 2, demanding unilateral/asymmetric leverage at 3, deep pistol work at 4. Merely coordinating an advanced movement does not establish high strength. |
| Muscular endurance | Repeated or sustained meaningful local force during the block. | Mild local work is 1–2; substantial bodyweight sets/holds generally reach 3. Particularly demanding repeated whole-body resistance can reach 4. This estimates local trainability, not a promise of a multi-minute hold. |
| Flexibility | Deliberate practice of joint range. | Gentle joint motion is 1; active directed mobility is 2; sustained ordinary end-range work is 3; comparatively large specific hip/fold positions can reach 4. More range is not universally better. |
| Agility | Rapid, accurate planned repositioning under the traditional definition. | Basic footwork is 1, quick lateral steps/small hops 2, deliberate airborne turns or forceful lateral landings 3. Slow lunges and merely changing an arm's direction do not qualify. |
| Balance | Meaningful control of a reduced/changing support base. | Basic shifting support is 1, ordinary unsupported single-leg/tandem tasks 2, substantial dynamic single-leg or head-motion challenges 3. Wall support reduces the claim; ordinary bracing is omitted. |
| Coordination | Deliberate timing or independent control of multiple segments. | Simple coupled patterns are 1, independent/cross-body patterns 2, linked technical sequences or isolated waves 3, the precise tutting sequence 4. Coordination remains task-specific. |
| Power | Deliberate rapid force production. | Unloaded strikes/small jacks are 1; elastic short hops 2; substantial jumps 3; demanding unilateral takeoffs/bounds or ballistic pushing 4. Holds and slow resistance receive 0. |
| Reaction time | Responding to an unpredictable external event. | A predictable demonstration loop earns 0. The existing random visual stop/go protocol earns a provisional 2 for its elementary response task. A denser cue schedule does not demonstrate faster response latency or justify tiers 3–5. |
| Speed | Deliberate rapid limb cycles or locomotion. | Basic cadence is 1, rapid limb/footwork patterns 2, traveling running/bounding 3, actual sprinting 4. High knees in place do not establish maximal overground sprint ability. |

These distinctions are explicit assessment choices, not physiological laws or
validated thresholds. Strength, endurance, flexibility, balance, coordination,
power and speed refer to the particular joint, muscle group, support condition
or action being trained. A calf stretch does not confer whole-body flexibility;
a finger pattern does not confer boxing coordination; a floor-supported trunk
task does not automatically confer standing balance. The CSV includes the
catalog's primary muscle group for context, not as a substitute for these
task-specific scopes.

## Source and review status

- [`catalog-audit/acsm-fitness-ceiling-scores.csv`](catalog-audit/acsm-fitness-ceiling-scores.csv)
  is the readable per-exercise export: stable ID, current name, all ten scores,
  assessment profile, primary muscle group, provisional status, and rationale.
- [`../tools/ExerciseFitnessCeilingScores.json`](../tools/ExerciseFitnessCeilingScores.json)
  is the source. Every ID is explicitly assigned to one rationale/score profile.
  Similar movements can share a judgment, but an unassigned new ID fails
  validation rather than inheriting ratings from its name or practice label.
- [`catalog-audit/acsm-fitness-ceiling-coverage.json`](catalog-audit/acsm-fitness-ceiling-coverage.json)
  reports exact-level counts and deduplicates session movement identities.
  Counts are diagnostic only: there are no required populations or deficits.

Every row is marked `provisional-metadata-assessment`. This pass considered
the current exercise names and movement/load/support metadata, and the actual
presence of runtime reaction cues. It is **not** a new full video review,
human expert approval, or longitudinal training validation. Shared profiles do
not mean their members have identical mechanics or physiological effects.

The assessment pins the normalized-LF catalog SHA-256. Any catalog change
requires deliberate reassessment and a snapshot update; regenerating the CSV
alone cannot silently award ratings to a changed movement. Existing canonical
anatomy, muscular-demand ratings, media and scheduler behavior are independent.

## Existing reaction playback

The 30 existing stop/go exercises retain precisely their existing playback
levels. Their independent configuration is now
[`../tools/ExerciseReactionCues.json`](../tools/ExerciseReactionCues.json), sourced
from the current runtime catalog. `Generate-ExerciseCatalog.ps1` reads this
playback configuration rather than discarded fitness-score overrides.
`reactionCueLevel` is a cue-density setting, **not a physical ability tier**.
This assessment adds no cues and changes no cue timings or session behavior.

## Rebuild and check

From the repository root:

```powershell
node tools/generate-exercise-fitness-ceiling-scores.mjs
node tools/generate-exercise-fitness-ceiling-scores.mjs --check
node --test web/tests/fitness-ceiling-scores.test.js
```

Checks reject stale catalog snapshots, absent/duplicate/unknown IDs, missing
characteristics, fractional/out-of-range scores, unsupported reaction claims,
power/speed claims for still holds, and stale exports. Later revisions should
review individual assignments and rationale profiles together. Before product
use, finalize the relevant task benchmarks and test the ceiling estimates
against observed adaptations and actual weekly exposure.

## Framework references

- ACSM, [The Components of Physical Fitness: A Roundtable Statement](https://doi.org/10.1249/MSS.0000000000003997).
- Dreyfus, [The Five-Stage Model of Adult Skill Acquisition](https://www.bumc.bu.edu/facdev-medicine/files/2012/03/Dreyfus-skill-level.pdf).
- Kümmel et al., [Specificity of Balance Training in Healthy Individuals](https://pubmed.ncbi.nlm.nih.gov/26993132/).
- ACSM, [2026 resistance-training guidance](https://acsm.org/resistance-training-guidelines-update-2026/).
