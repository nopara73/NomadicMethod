# Training-zone overlap audit - 25 October 2026

Android 3.47 / web; catalog revision 92, 909 unchanged records and ratings.

## Selection and feedback rules

An exercise qualifies when any explicitly supported level overlaps the corresponding known user zone in at least one characteristic. Every distinct member of a complete atomic sequence must qualify. Unknown axes, empty ratings, equipment, truthful anatomy, complete duration, saved preferences, recovery and Light retain their existing meaning. Existing unfinished selections are preserved.

Love (just right) and too hard contribute evidence only through the exercise's strongest characteristics, including ties. Too easy contributes through every nonempty rated characteristic, using that characteristic's own maximum. Previous history is read without rewriting it. Evidence version 3 prevents a changed calculation being displayed as progress against an older workout baseline.

## Global coverage

**All 50 characteristic-level cells meet the five-distinct-movement target.** The previous 20 deficient cells and 12 empty cells came from whole-range containment. Counts deduplicate SessionMovementId and require every atomic member to qualify. Equipment restrictions apply in the separate workout checks below.

| Characteristic | 1 | 2 | 3 | 4 | 5 |
|---|---:|---:|---:|---:|---:|
| cardio | 128 | 75 | 43 | 31 | 7 |
| strength | 324 | 216 | 53 | 24 | 13 |
| muscle endurance | 294 | 447 | 228 | 24 | 11 |
| flexibility | 113 | 140 | 86 | 10 | 6 |
| agility | 14 | 13 | 13 | 13 | 5 |
| balance | 95 | 67 | 23 | 23 | 5 |
| coordination | 232 | 220 | 41 | 44 | 6 |
| power | 50 | 22 | 32 | 12 | 10 |
| reaction | 31 | 31 | 5 | 5 | 5 |
| speed | 36 | 65 | 44 | 38 | 23 |

## Whole-body workout gaps

The same 1,350 representative combinations cover five uniform singleton zones, five new-workout durations and 54 existing physical validation profiles. Uniform level means all ten known axes have that level. These are synthetic combinations, not a proportion of actual users.

**403 combinations have an empty required slot, down from 698.** Of the remaining 947 combinations, all 947 produced complete lineups through the normal planner with the fixed synthetic zones and deterministic draws. There were no additional planner failures without an empty slot. This is an observed planner check, not a mathematical guarantee for every seed, mixed zone or history.

668 combinations fall below five choices in at least one slot, down from 875; 403 of those have an empty slot and 265 have no empty slot. Candidate variety below five does not itself prevent a workout.

| All axes at level | Combinations | Empty required slot | Any slot below five |
|---|---:|---:|---:|
| 1 | 270 | 0 | 5 |
| 2 | 270 | 0 | 48 |
| 3 | 270 | 72 | 145 |
| 4 | 270 | 150 | 215 |
| 5 | 270 | 181 | 255 |

Representative remaining failures:

| Duration | All axes at level | Enabled modifiers | Empty slots |
|---|---:|---|---|
| 10 min | 3 | none | Anterior/lateral lower leg and dorsal foot |
| 15 min | 4 | none | Head and neck; Anterior/lateral lower leg and dorsal foot |
| 15 min | 5 | none | Head and neck; Anterior/lateral lower leg and dorsal foot; Hip flexors; Abdominal wall and pelvic floor; Lateral and deep hip stabilizers; Back and spinal stabilization; Hip adductors |
| 5 min | 4 | HardFloor | Lower legs and feet |
| 3 min | 5 | Insect, HardFloor | Lower limbs |

## Only one ability known

A further 13,500 slot-availability checks use each of the 50 single-known-axis singleton zones against the same 270 physical/duration combinations. Unknown axes cannot admit a movement. The following cells count combinations with an empty required slot, out of 270 in each cell. Complete lineups were not replayed for this additional grid.

| Only known characteristic | 1 | 2 | 3 | 4 | 5 |
|---|---:|---:|---:|---:|---:|
| cardio | 44 | 159 | 200 | 200 | 235 |
| strength | 24 | 88 | 252 | 263 | 270 |
| muscle endurance | 88 | 24 | 88 | 263 | 270 |
| flexibility | 48 | 136 | 201 | 263 | 263 |
| agility | 270 | 270 | 270 | 270 | 270 |
| balance | 47 | 98 | 261 | 261 | 270 |
| coordination | 0 | 112 | 226 | 226 | 270 |
| power | 184 | 208 | 200 | 267 | 267 |
| reaction | 184 | 184 | 270 | 270 | 270 |
| speed | 174 | 184 | 219 | 225 | 263 |

Reaction has 31, 31, 5, 5 and 5 distinct global movements at levels 1-5. However, when reaction at 3, 4 or 5 is the only known axis, all 270 tested physical/duration combinations have an empty body-part slot. Five response families do not cover every required anatomical region. A mixed user zone can qualify other exercises through other characteristics.

## User-state replay and reproducibility

A private copy of the phone's current state retained its unfinished 10-minute selections on reopen. Fresh local clones could plan complete 3-, 5-, 7-, 10- and 15-minute workouts with the current settings and difficulty-derived zones. These replays did not end, start or change a real phone workout.

```powershell
node web/scripts/report-zopd-coverage.mjs --verify-lineups --output .artifacts/overlap-coverage.json
```

[Machine-readable summary and gap examples](overlap-coverage-2026-10-25.json). The full generated report includes every movement identity, physical profile, slot count and uniform-profile planner outcome, without personal history.

The previous [containment audit](zopd-availability-2026-10-24.md) remains historical evidence and is superseded by this overlap audit.
