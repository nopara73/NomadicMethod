# ACSM fitness-ceiling scoring

Status: provisional catalog audit, scoring version 1. It is not used by workout
selection, scoring, recovery, or the Android/web runtime.

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
- Reaction time requires an unpredictable external cue. All current catalog
  demonstrations are known repeating loops, so all 853 reaction-time scores
  are zero.

Training outcomes still depend on weekly dose, intensity, recovery, starting
ability, technique, and individual response. The catalog has no longitudinal
intervention data proving these ceilings. The values are structured estimates
for product discussion and later review.

## Version 1 method

The generated audit is
[`catalog-audit/acsm-fitness-ceiling-scores.csv`](catalog-audit/acsm-fitness-ceiling-scores.csv).
It covers catalog revision 86 at source commit
`089172327c9dd16131988f9a8d30909eec03888a`, with 853 records and source-catalog
SHA-256 `ca3c19ef014457a0999b4cdfaec4cf7d3ae61d3ef2c48229ad8497ec4ff2035a`.

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

The remaining 846 rows are rule-derived from the canonical catalog's name,
practice, motion profile, mode, muscular demand, support, and locomotor cues.
The rules are readable in
[`../tools/generate-exercise-fitness-ceiling-scores.mjs`](../tools/generate-exercise-fitness-ceiling-scores.mjs),
and each CSV row records the rules that fired in its `basis` field. Reviewed
exercise-specific judgments can replace derived values by adding an override.

The first version intentionally leaves most dimensions below level 5. Fixed
45-second, largely unweighted exercises usually cannot justify an expert
ceiling. Sprinting is the only current direct match assigned level-5 speed.
An unused level is valid; scores are not quotas.

## Rebuild and verify

From the repository root:

```powershell
node tools/generate-exercise-fitness-ceiling-scores.mjs
node tools/generate-exercise-fitness-ceiling-scores.mjs --check
```

The check fails if the catalog changes, an override points to a missing
exercise, a score leaves the 0–5 range, or the committed CSV no longer matches
the scoring rules.

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
