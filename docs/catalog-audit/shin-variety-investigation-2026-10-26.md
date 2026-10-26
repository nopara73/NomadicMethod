# Shin movement supply investigation

The small starting pool is a catalog supply imbalance. It is already small
before modifiers, fitness, recovery, scores, Keeps or duration are consulted.
Correcting Wall Tibialis Raise's clothing eligibility restores one usable
choice, but does not expand that underlying supply.

## Count the same thing at each stage

At catalog revision 94, the Android source contains 909 records, 797 distinct
`SessionMovementId` identities, and 768 identities with schedulable sequence
roots. Records include aliases, direction members and cue protocols; they are
not independent workout choices.

For `AnteriorLateralLowerLegAndDorsalFoot`, there are 13 records, 12 sequence
roots and **10 movement identities**. Six identities have primary shin work;
four more have an approved secondary shin claim. All ten are present in the
runtime planner's anatomical placement pool for the ten-minute shin slot.
There is no additional loss between the declared training claims and that
placement pool.

| Identity | Movement | Shin claim |
| --- | --- | --- |
| 165 | Wall Tibialis Raise | Primary |
| 191 | Unsupported Standing Toe Raises | Primary |
| 194 | Heel Walks | Primary |
| 284 | Side-to-Side Ankle Rocks | Primary |
| 288 | Ankle-circle family | Primary |
| 1418 | Foot-on-Chair Shin Stretch | Primary |
| 1029 | Alternating Heel Digs | Secondary |
| 1210 | Kneeling Sit-on-Heels Hold | Secondary |
| 1295 | Heel-to-Toe Rocking | Secondary |
| 1410 | Seated Heel-to-Toe Rocking | Secondary |

The circle family comprises records 288, 758, 1299 and 1303. Record 758 is a
direction member, not an independent sequence root. Its other variants and
directions intentionally share identity 288; counting their names separately
would manufacture variety. The existing identity rule was reviewed in
[`standing-body-circles-2026-10-20.md`](standing-body-circles-2026-10-20.md).

## Why the catalog total is misleading

The supply is distributed very unevenly. These are distinct identities with a
primary or meaningful secondary training claim, before contextual filtering:

| Canonical training target | Primary identities | All trained identities |
| --- | ---: | ---: |
| Anterior/lateral lower leg and dorsal foot | 6 | 10 |
| Elbow flexors | 6 | 15 |
| Calf, deep posterior leg and plantar foot | 29 | 109 |
| Abdominal wall | 89 | 131 |
| Gluteal extensors | 47 | 211 |
| Shoulder abductors | 77 | 227 |

A movement can appear in several target inventories. The columns cannot be
summed into a total number of exercises.

The earlier training-claim review recorded **45 removed shin claims** in
[`training-claim-corrections-2026-09-05.json`](training-claim-corrections-2026-09-05.json).
That is a count of reviewed records, not 45 distinct available movements.
Examples explain why restoring those claims would be incorrect:

- Record 16's old "Split-Stance Toe Raises" footage raises the heels: it is
  plantar-flexor work, now correctly named Split-Stance Calf Raises.
- Record 187's old "Standing Heel-and-Toe Raises" footage never raises the
  forefeet: it is now Standing Heel Raise.
- Marching, walking, hopping, squatting and balancing previously received shin
  credit for foot clearance or postural coactivation. Those actions are not
  independent shin training claims under the current catalog rubric.

The source review found no omitted existing direct shin movement to reinstate.
The catalog has not replaced the lost apparent variety with comparable genuine
shin supply. Most later catalog growth went to other targets or fitness skills.

## What the existing coverage gates establish

The five-choice availability gate checks complete atomic sequences under the
approved modifier-pair policy. Fitness-level coverage is global across the
catalog. Neither establishes a balanced supply of primary work per muscle, nor
a large per-muscle supply at every fitness profile and combination of enabled
controls. The separate primary invariant requires at least one genuine primary
movement per canonical muscle. Passing these gates therefore did not reveal
this concentration of choices.

The slot-variety report now includes the complete raw anatomical inventory,
including primary, trained, schedulable and one-block identity counts. Run
`npm run audit:slot-variety` from `web/` after refreshing the deficit ledger.
This is a diagnostic; no quota, anatomy, rating, family identity or accepted
coverage exception was weakened or inflated.

## Clothing correction and saved-setup replay

Wall Tibialis Raise (165) is now `Agnostic` for upper-body clothing, following
the owner's correction. Its existing `HardFloorCompatibility.Compatible`
classification is retained. Wall remains required, and Insect remains
incompatible. The same Android runtime catalog supplies the web product.

The existing packaged demonstration shows a static back-to-wall lean, rather
than the hand-supported variation described in the report. The clothing
correction does not substitute a different demonstration or change the
movement's training claims.

Replaying the saved physical setup and fitness profile against the corrected
catalog yields 10 raw anatomical identities, 8 after physical compatibility,
6 after fitness eligibility, and 5 complete choices fitting the ten-minute
workout. Record 165 has saved score 0 and is now usable, alongside the existing
four choices with negative scores. No score, Keep or physical setup was edited
to obtain this result.

A seeded replay through the Android planner selects Wall Tibialis Raise for
the saved setup and does not select Heel-to-Toe Rocking. The correction passed
1,183 Android tests, the web checks (including the deliberately updated clothing
inventory expectations), the production web build and the Debug Android build.
The packaged exercise was reviewed through Ready, the normal movement timer
and rest with upper-body clothing off.

The classification correction resolves the erroneous exclusion. The underlying
ten-identity supply remains a catalog sourcing issue: expanding it requires
additional established movements with exact reviewed demonstrations and
truthful anatomy, rather than restoring incidental claims or splitting aliases.
