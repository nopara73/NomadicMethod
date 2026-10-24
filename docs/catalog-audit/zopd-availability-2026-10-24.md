# ZoPD catalog availability audit - 24 October 2026

Release: `01143d67` (Android 3.45 / web), catalog revision 91, 907 catalog records.

## Finding

The stricter selection rule exposes catalog gaps. Of the 50 single-characteristic, single-level cases, **21 have fewer than five distinct qualifying movements; 12 have none**. These are selection-availability gaps, not missing rating records. A movement rated 2-3 still has valid catalog ratings, but cannot serve a zone containing only 3 under the agreed whole-set rule.

## Exact-level availability

Each cell counts distinct `SessionMovementId` values globally, before equipment restrictions. A case knows only that characteristic at that level. Every distinct member of the complete atomic sequence must qualify. Counts below five are bold.

| Characteristic | 1 | 2 | 3 | 4 | 5 |
|---|---:|---:|---:|---:|---:|
| cardio | 114 | 33 | 11 | **0** | **0** |
| strength | 187 | 50 | 6 | **3** | **2** |
| muscle endurance | 26 | 150 | 54 | **3** | **0** |
| flexibility | 74 | 30 | 5 | **1** | **1** |
| agility | 14 | 5 | **0** | **0** | **0** |
| balance | 87 | 48 | 8 | 5 | **0** |
| coordination | 232 | 191 | 8 | 12 | **1** |
| power | 50 | 22 | 23 | **3** | **1** |
| reaction | **1** | 5 | **0** | **0** | **0** |
| speed | 36 | 35 | 6 | **0** | **0** |

The empty cases are cardio 4-5; muscle endurance 5; agility 3-5; balance 5; reaction 3-5; and speed 4-5. Reaction 1 has only one qualifying movement; reaction 2 has sufficient global choices.

## Physical setup and duration availability

The audit also checks 1,350 combinations: five uniform level profiles x five new-workout durations (3, 5, 7, 10 and 15 minutes) x 54 existing physical-modifier validation profiles. Uniform level means every known characteristic has the same singleton zone. Equipment restrictions, anatomical slot eligibility, accepted structural exceptions, complete atomic sequences and duration fit all apply.

| Uniform level | Combinations checked | At least one empty slot | At least one slot below five choices |
|---|---:|---:|---:|
| 1 | 270 | 0 | 5 |
| 2 | 270 | 0 | 89 |
| 3 | 270 | 173 | 243 |
| 4 | 270 | 262 | 268 |
| 5 | 270 | 263 | 270 |

**698 of 1,350 tested combinations have an empty slot and cannot form a complete workout.** Another 177 have no empty slot but still miss the five-choice target in at least one slot. This counts deliberately constructed test combinations, not the percentage of real users affected.

No empty slot does not prove that all slots can be filled simultaneously: two slots may depend on the same movement. Complete-lineup replays of the current device snapshot separately passed at all five supported new-workout durations on both Android and web. Tests also cover failure without saved-state mutation and restoration of unfinished sessions.

Broader best-fit zones can admit ranged exercises and mixed characteristic zones can provide additional matches. This audit does not enumerate every mixed zone or every possible feedback history. Unknown profiles continue to use ordinary bootstrap selection; known profiles never silently widen their zones.

## Reproduce

```powershell
node web/scripts/report-zopd-coverage.mjs --output .artifacts/zopd-coverage.json
```

The full generated report includes each of the 50 cells and its movement IDs, plus every tested physical/duration profile and per-slot counts. It contains no user history.

Catalog SHA-256 (LF-normalized): `22f91855a640dd41c97fa2c1d5d202b59b978d4c5d4554655a672c0c6b334a3d`.

Ratings SHA-256 (LF-normalized): `fe1acb549fb07e987a339a2531922ff34082474e95d9d8032c08756303d9400a`.

No ratings, exercises or eligibility rules were changed to improve these counts.

## Follow-up: catalog revision 92

Two reviewed additions, Floor Straddle Planche Hold (1718) and Floor Tuck Planche Push-Ups (1719), bring strict singleton strength 4 from **3 to 5 distinct movements**. Balance 3 grows from 8 to 10; coordination 3 grows from 8 to 9. No previous rating changed.

The same audit now reports **20 of 50 cells below five; 12 empty**. The 1,350 physical/duration profiles still include 698 with empty slots and 875 below five choices. This closes a global cell, not the remaining whole-body coverage shortages. The original tables above remain the revision-91 baseline.

[Admission rationale and source records](zopd-admissions-2026-10-24.json) and [actual-workout review gallery](evidence/2026-10-24/zopd-admissions/gallery.html). Re-run the command above against revision 92 for the full current report.

Current catalog SHA-256 (LF-normalized): `047a9f99604524c6537400cda490d34102a11e72c09636b423fdc52229675bce`.
Current ratings SHA-256 (LF-normalized): `08a1d2e3c60e87ca588150424e77dbd25207c0a7f285323c9d421818bebb9cbd`.
