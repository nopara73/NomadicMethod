# ACSM fitness-ceiling scoring

Status: provisional catalog audit, scoring version 2. The ten ceiling scores do
not affect workout selection, preference scores, or recovery. Reaction-time
placements are backed by the Android/web cue protocol described below.

This audit scores every exercise against the traditional ACSM fitness
components except body composition:

1. cardiorespiratory endurance;
2. muscular strength;
3. muscular endurance;
4. flexibility;
5. agility;
6. balance;
7. coordination;
8. power;
9. reaction time; and
10. speed.

The component names and definitions come from the traditional ACSM framework.
The five levels do not. Dreyfus describes stages of acquiring a skill, not
universal physical-performance thresholds. Nomadic Method therefore uses the
numbers as an explicit product hypothesis:

| Score | Label | Meaning in this audit |
|---:|---|---|
| 0 | No meaningful stimulus | The demonstrated exercise does not materially train this component. |
| 1 | Novice | Estimated to contribute meaningfully through the first fixed benchmark tier. |
| 2 | Advanced beginner | Estimated ceiling is the second benchmark tier. |
| 3 | Competent | Estimated ceiling is the third benchmark tier. |
| 4 | Proficient | Estimated ceiling is the fourth benchmark tier. |
| 5 | Expert | Estimated ceiling is the fifth benchmark tier. |

A score is an estimated **training ceiling**, not the skill required to start
the exercise, its difficulty, its value, or a promise that one block produces
the result. It asks how far repeated use of that particular exercise could
plausibly contribute to the directly matched capacity.

## Fixed interpretation

- Every work block is 45 seconds.
- Form, range, cadence, resistance, support, equipment, and cues are those in
  the packaged demonstration.
- Repeated appearances in ordinary Nomadic Method workouts are allowed, but no
  external load or harder unshown variation is assumed.
- Scores are fixed abilities rather than age- or sex-relative percentiles.
- Strength, endurance, flexibility, balance, coordination, power, and speed are
  interpreted in the exercise's directly trained movement, joint, limb, or
  task. A calf stretch does not claim whole-body flexibility, and a tree pose
  does not claim universal balance transfer.
- Cardiorespiratory endurance is whole-body. Local fatigue alone does not earn
  a cardiorespiratory score.
- Agility uses the traditional planned-repositioning definition. A rehearsed
  turn can train agility without training reaction time.
- Reaction time requires an unpredictable external cue. A repeating video by
  itself receives zero. Thirty exercises now add a random visual stop/go cue:
  the demonstrator pauses under a red pause symbol and resumes under a green
  play symbol. Existing workouts from before catalog revision 87 retain their
  original continuous playback.

Training outcomes still depend on weekly dose, intensity, recovery, starting
ability, technique, and individual response. The catalog has no longitudinal
intervention data proving these ceilings. The values are structured estimates
for product discussion and later review.

## Version 2 method

The generated audit is
[`catalog-audit/acsm-fitness-ceiling-scores.csv`](catalog-audit/acsm-fitness-ceiling-scores.csv).
It covers catalog revision 87, with 853 records and catalog SHA-256 after
normalizing CRLF line endings to LF
`22eef7cef689707c629fb58a5acb041eebb0ed8f2be8ae810b7e13e61cd4b657`.

Seven exercises previously discussed are explicit reviewed anchors. Their ten
scores live in
[`../tools/ExerciseFitnessCeilingOverrides.json`](../tools/ExerciseFitnessCeilingOverrides.json):

| Exercise | Cardio | Strength | Muscular endurance | Flexibility | Agility | Balance | Coordination | Power | Reaction | Speed |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Wall Sit | 0 | 2 | 3 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Tree Pose Hold | 0 | 0 | 1 | 0 | 0 | 3 | 0 | 0 | 0 | 0 |
| Jumping Jacks | 2 | 0 | 2 | 0 | 0 | 0 | 2 | 0 | 0 | 1 |
| 180-Degree Squat Jumps | 3 | 2 | 3 | 0 | 3 | 2 | 3 | 3 | 0 | 2 |
| Lateral Line Hops | 2 | 0 | 2 | 0 | 3 | 2 | 2 | 2 | 0 | 2 |
| Toes-on-Wall Calf Stretch | 0 | 0 | 0 | 3 | 0 | 0 | 0 | 0 | 0 | 0 |
| Wall Push-Up | 0 | 1 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

Another 115 exercises have reviewed exact-level placements in
[`../tools/ExerciseFitnessCeilingCoverageOverrides.json`](../tools/ExerciseFitnessCeilingCoverageOverrides.json).
The remaining 731 rows are rule-derived from the canonical catalog's name,
practice, motion profile, mode, muscular demand, support, and locomotor cues.
The rules are readable in
[`../tools/generate-exercise-fitness-ceiling-scores.mjs`](../tools/generate-exercise-fitness-ceiling-scores.mjs),
and each CSV row records the rules that fired in its `basis` field. Reviewed
exercise-specific judgments can replace derived values by adding an override.

## Exact-level catalog rule

Every one of the 50 exact characteristic-level cells must contain at least six
distinct session movements. A level-5 movement does not count again at levels
1–4, and aliases or members of the same `SessionMovementId` count once. This is
a Nomadic Method catalog-diversity rule; it is not an ACSM recommendation and
does not make six a physiological threshold.

The generated
[`catalog-audit/acsm-fitness-ceiling-coverage.json`](catalog-audit/acsm-fitness-ceiling-coverage.json)
records both exercise rows and deduplicated session-movement IDs. Generation
and CI fail on any cell below six. Version 2 currently has no deficits.

High-tier placements remain task-specific. For example, level-5 pistol work is
a claim about the directly trained unilateral squat task, not a claim that it
creates level-5 strength in every muscle or movement.

## Reaction cue levels

The 30 cue-backed exercises contain six distinct session movements at each
level. Each 45-second block alternates random move and freeze windows:

| Level | Move window | Freeze window |
|---:|---:|---:|
| 1 | 5.5–8.0 s | 1.6–2.2 s |
| 2 | 4.0–6.5 s | 1.2–1.8 s |
| 3 | 3.0–5.0 s | 0.9–1.4 s |
| 4 | 2.0–3.5 s | 0.7–1.1 s |
| 5 | 1.2–2.5 s | 0.45–0.85 s |

The base movements are all reviewed motion/repetition exercises; no hold or
still-image exercise receives a reaction score. The cue density supplies the
level distinction, while the random transition supplies the external stimulus.

## Rebuild and verify

From the repository root:

```powershell
node tools/generate-exercise-fitness-ceiling-scores.mjs
node tools/generate-exercise-fitness-ceiling-scores.mjs --check
```

The check fails if the catalog changes, an override points to a missing
exercise, a score leaves the 0–5 range, a reaction score differs from the
runtime cue level, a characteristic-level cell has fewer than six distinct
session movements, or either committed audit file is stale.

Before any runtime use, every rule-derived row should receive exercise-specific
review, the benchmark battery should be finalized for each relevant movement
domain, and the resulting ceiling model should be tested against observed
training data.

## References

- American College of Sports Medicine, [The Components of Physical Fitness: A
  Roundtable Statement](https://doi.org/10.1249/MSS.0000000000003997).
- Dreyfus and Dreyfus, [A Five-Stage Model of the Mental Activities Involved in
  Directed Skill Acquisition](https://www.bumc.bu.edu/facdev-medicine/files/2012/03/Dreyfus-skill-level.pdf).
- Kümmel et al., [Specificity of Balance Training in Healthy Individuals: A
  Systematic Review and Meta-Analysis](https://pubmed.ncbi.nlm.nih.gov/26993132/).
- American College of Sports Medicine, [2026 resistance-training position-stand
  summary](https://acsm.org/resistance-training-guidelines-update-2026/).
