# Anterior/lateral lower-leg and dorsal-foot candidate investigation

**Correction:** `AnteriorLateralLowerLegAndDorsalFoot` is a regional muscle
bucket, not a synonym for tibialis anterior or a shin-only target. The earlier
"shin-only slot" description was an investigation error. Neither the candidate
count nor the grouping's provenance establishes that the regional bucket is
wrong or that additional tibialis exercises are required.

Correcting Wall Tibialis Raise's clothing eligibility restores one usable
choice within that bucket. The separate issue is whether catalog assignments
truthfully represent the entire anatomical region.

## What the anatomical region includes

The [Texas Tech anatomy tables](https://anatomy.ttuhscep.edu/musculoskeletal_system/leg_tables.html)
distinguish the following muscles and actions:

| Part of the region | Muscles | Relevant actions |
| --- | --- | --- |
| Anterior lower leg | Tibialis anterior, extensor hallucis longus, extensor digitorum longus, fibularis tertius | Ankle dorsiflexion, toe extension, and muscle-specific inversion or eversion |
| Lateral lower leg | Fibularis/peroneus longus and brevis | Foot eversion and plantarflexion |
| Dorsal intrinsic foot muscles | Extensor digitorum brevis and extensor hallucis brevis | Toe extension |

In particular, lateral-compartment muscles are plantarflexors as well as
evertors. An argument that a movement does not train dorsiflexors cannot, on
its own, exclude meaningful work for this entire bucket.

The selection predicate tests the full canonical-group association. It does
not impose a separate tibialis-only or dorsiflexion-only rule. The existing
candidate pool includes Side-to-Side Ankle Rocks (284) and ankle-circle family
288, whose reviewed actions include inversion and eversion. Their eligibility
already demonstrates that the slot is broader than the earlier shorthand.

## Where the regional bucket entered the product

The available original requests and Git history establish this sequence:

1. The 2 August request supplied a fixed list of twenty muscle groups, with
   Shins at position 20. Short workouts used a prefix of that list. Its
   ten-minute plan did not include its separately named Shins entry. That
   historical label is not an anatomical definition of today's broader bucket.
2. The attached implementation brief received on 3 August at 01:52 UTC changed
   the product to seven resolutions. Under `10 GROUPS — 10% TARGET`, it
   explicitly listed bucket 10 as `Anterior/lateral lower leg and dorsal foot`.
   Bucket 9 was `Upper limbs`. This regional grouping was already present
   in the brief before the implementation began.
3. Commit [`cf05cb13`, Add mass grouping ladder](https://github.com/nopara73/NomadicMethod/commit/cf05cb135a81b6ba56b104552484973cab2ed9ef)
   on 3 August implemented those entries in
   `Flux/Services/MassGroupingTaxonomy.cs` and pinned them in its tests.
   An exact source comparison shows that **all seven grouping declaration
   tables are unchanged** in today's `NomadicMethod/Services/MassGroupingTaxonomy.cs`.
4. Commit [`8d489bc7`, Reverse workout order](https://github.com/nopara73/NomadicMethod/commit/8d489bc7db6ad78f06f05f45e00e4784adbc7efc)
   on 4 August reversed traversal and reassigned order numbers. It did not
   change bucket membership. The later Nomadic Method rename also retained
   the tables. Web uses the same memberships.

This is a change from the earlier twenty-group prefix model, introduced with
the first mass-resolution implementation. There is no later regrouping commit
that narrowed the membership of the regional bucket to tibialis anterior.
The available evidence identifies the incoming brief as the point at which
the current table entered the implementation; it does not establish who
originally generated that brief.

## What was never established by the implementation

The brief called the ten-group percentage a nominal target and warned against
claiming exact muscle masses. However, the implementation stores no estimated
bucket masses or percentage calculation. Its validator checks the declared
group count and exactly-once coverage of the thirty leaves.

The tests named `ResolutionSchedulesRunFromSmallestToLargestMass` and
`LeafToBucketMappingsAndAscendingScheduleOrdersAreStable` compare the
implementation against copied group and order tables. They protect the
configuration from drift; they do not establish the configuration's mass
ordering or proximity to its stated percentage targets. The separate workload
balancer sums training-load units into these same buckets; it does not measure
muscle mass or choose their membership.

The resolutions also are not a pure nested split/merge tree: the seven-minute
`head-neck-upper-limbs` bucket crosses the five-minute `upper-limbs` and
`head-neck-shoulder-girdle` boundaries, and the ten-minute
`head-neck-scapular-chest-breathing` bucket crosses the seven-minute `torso`
and `head-neck-upper-limbs` boundaries. Those choices are present in the
original table too. This is a description of the configuration, not a newly
introduced requirement that the existing product must use a nested tree.

The mass-target rationale is not quantitatively established by these tests.
That observation does not prove this particular regional grouping is invalid.
The investigation must not use its own erroneous "shin-only" interpretation
as grounds to replace the original regional taxonomy.

## Count the same thing at each stage

At catalog revision 94, the Android source contains 909 records, 797 distinct
`SessionMovementId` identities, and 768 identities with schedulable sequence
roots. Records include aliases, direction members and cue protocols; they are
not independent workout choices.

For `AnteriorLateralLowerLegAndDorsalFoot`, there are 13 records, 12 sequence
roots and **10 movement identities**. Six identities have a primary claim for
this region; four more have an approved secondary regional claim. All ten are
present in the runtime planner's anatomical placement pool for this bucket.
There is no additional loss between the declared training claims and that
placement pool.

| Identity | Movement | Regional claim |
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

## Anatomical inventory is descriptive

These are distinct identities with a
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
summed into a total number of exercises. Differences between these counts do
not establish how much workout time each target should receive or how many
movements a valid catalog must contain for that target.

The earlier training-claim review recorded **45 removed claims for this region** in
[`training-claim-corrections-2026-09-05.json`](training-claim-corrections-2026-09-05.json).
That is a count of reviewed records, not 45 distinct available movements.
Several recorded reasons considered the anterior compartment without
establishing whether lateral-compartment work was meaningful:

- Record 16's old "Split-Stance Toe Raises" footage raises the heels: it is
  plantar-flexor work, now correctly named Split-Stance Calf Raises. This
  supports correcting its primary target, but does not by itself decide a
  lateral-compartment secondary claim.
- Record 187's old "Standing Heel-and-Toe Raises" footage never raises the
  forefeet: it is now Standing Heel Raise. Absence of dorsiflexion does not
  settle whether a lateral plantarflexor receives meaningful work.
- Records 252 and 253 describe dorsiflexors as antagonists or postural
  controllers. That reasoning cannot cover the entire region, because the
  region also contains lateral plantarflexors.
- Ordinary foot clearance and incidental postural coactivation still do not
  qualify under the training-claim rubric. The anatomical correction does not
  justify restoring every removed association.

Consequently, the earlier blanket conclusion that none of these exclusions
needed reconsideration is withdrawn. Existing heel-raise variants warrant
review for lateral-compartment training, while toe-extensor work warrants its
own review against the exact demonstrations.

[Akuzawa et al. (2017)](https://pubmed.ncbi.nlm.nih.gov/28950148/)
measured differing peroneus-longus recruitment across heel-raise foot positions.
[Kim et al. (2022)](https://www.nature.com/articles/s41598-022-14313-8)
found ankle orientation mattered during heel raises intended to recruit that
muscle. These studies support reviewing exact demonstrated variants; they do
not establish that every calf movement qualifies. EMG activation alone also
does not establish the meaningful trainable stimulus required by the catalog.

No runtime training claim has been added based solely on this anatomical
correction. An unresolved candidate requires its exact movement and complete
work interval to be assessed before a claim is changed. The ten-identity count
is the current metadata result, not a proven upper limit on the region's
eligible exercises.

## What the existing coverage gates establish

The five-choice availability gate checks complete atomic sequences under the
approved modifier-pair policy. Fitness-level coverage is global across the
catalog. These checks operate on the configured buckets. They do not validate
the rationale for those buckets. The separate primary invariant requires at
least one genuine primary movement per canonical muscle. All these tests can
pass while the underlying partition remains poorly justified.

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

The classification correction resolves the erroneous clothing exclusion. The
grouping policy remains unchanged. The remaining investigation concerns catalog
training claims across the full anterior/lateral lower-leg and dorsal-foot
region, rather than replacing that region because it was mistakenly described
as a shin-only target.
