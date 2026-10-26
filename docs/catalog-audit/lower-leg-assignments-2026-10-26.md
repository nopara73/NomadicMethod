# Full-region assignment correction — catalog revision 95

The existing anterior/lateral lower-leg and dorsal-foot grouping is retained at
every duration. The runtime catalog and canonical source now restore 40 reviewed
secondary associations for deliberately loaded plantarflexion. This adds 30
selectable movement identities: the region grows from 10 to 40, with 53 catalog
records. There are 41 raw identities; the component-only head-tilt member 416
cannot be selected independently. Existing families and aliases still count once.

## Anatomical decision

The [Texas Tech leg anatomy table](https://anatomy.ttuhscep.edu/musculoskeletal_system/leg_tables.html)
places fibularis/peroneus longus and brevis in the lateral compartment. Both
plantarflex and evert. The region also contains anterior-compartment dorsiflexors
and toe extensors and the dorsal intrinsic toe extensors. It is not a
dorsiflexor-only category.

The old correction ledger rejected several heel raises because the forefoot
never lifted or because dorsiflexors were antagonists. That was insufficient to
reject the **lateral plantarflexors**. Their contribution to repeated loaded
heel raises is a synergistic training action, distinct from ordinary standing
balance. The primary calf/soleus/squat/head target remains unchanged.

Evidence used in the assignment review:

- [Akuzawa et al., 2017](https://doi.org/10.1016/j.ptsp.2017.08.077): measured
  peroneus longus during neutral, adducted and abducted heel raises; abduction
  increased its recruitment. Toe angle changes emphasis, rather than making
  all heel raises exclusively posterior-compartment work.
- [Kim et al., 2022](https://www.nature.com/articles/s41598-022-14313-8): heel
  raises are used for peroneus-longus strengthening; rolling into inversion
  reduces recruitment. The packaged demonstrations were inspected for the
  actual planted forefoot, heel lift/hold and return, rather than inferring a
  target from an exercise title alone.
- [NHS Bexley peroneal rehabilitation](https://msk-bexley.nhs.uk/conditions/foot-and-ankle-pain/peroneal-tendon-pain)
  includes supported heel raises and progression to unsupported raises.
- [NHS BNSSG peroneal rehabilitation](https://myjointhealthhub.bnssg.nhs.uk/foot-ankle-pain/peroneal-tendinopathy/)
  includes bent-knee seated heel raises, supporting plantarflexion work rather
  than a straight-knee or dorsiflexion-only interpretation.

These are anatomical and clinical support for the reviewed training claims,
not a claim that every movement has measured EMG or identical stimulus.

## Corrected assignments

All IDs below gain only `AnteriorLateralLowerLegAndDorsalFoot` as a secondary
association, identically in `ExerciseCanonicalGroups.psd1`, the exact secondary
allowlist and the Android runtime catalog used by web.

| Demonstrated action | Reviewed catalog IDs |
| --- | --- |
| Repeated straight-knee standing, split-stance, wall-leaning, single-leg or step heel raise | 16, 162, 187, 252, 253, 254, 562, 566, 569, 581, 582, 1425 |
| Bent-knee repeated heel raise or deliberate raised-heel hold, including a held squat | 139, 141, 255, 272, 273, 295, 565, 987 |
| Repeated heel raise with arm movement | 389, 549, 552, 555, 557, 570 |
| Squat/hinge followed by a deliberate heel raise, or a squat on sustained raised heels | 138, 140, 197, 198, 201, 550, 551, 571 |
| Deliberate sustained tiptoe walking/turning or raised-heel gaze work | 119, 414, 415, 416, 561 |
| Repeated heel raises against the wall in a held bridge | 1285 |

The exact list does not credit ordinary squats, flat-footed balance, foot
clearance in gait, or unrelated knee/hip motion. Posterior calf stretches do
not gain the region merely because it contains some plantarflexors. No new
exercise, rating, sequence, physical compatibility or movement identity was
created. Dorsiflexion, eversion, ankle circles and existing anterior-leg
stretches retain their previous truthful claims.

## Runtime and preservation checks

Replaying the owner's saved settings and fitness profile yields:

| Distinct identities at each stage | Revision 94 | Revision 95 |
| --- | ---: | ---: |
| Anatomical placement pool | 10 | 40 |
| Physical modifiers | 8 | 27 |
| Physical modifiers and fitness | 6 | 25 |
| Complete sequences fitting the ten-minute lineup | 5 | 19 |

The shared membership fixture checks the complete region at 10, 15 and 30
minutes and includes negative controls. Native and web planner regressions
require a newly admitted movement to fill the existing regional round and
preserve a revision-94 unfinished workout, active catalog revision, selections,
Keeps, phase/scoped scores, global scores and session history. The catalog
refresh is additive; it introduces no score or preference invalidation.

Packaged-player review evidence is recorded in
`lower-leg-review-2026-10-26.json`, bound to the corrected metadata and unchanged
runtime media. Publication additionally requires the full native/web tests,
catalog gates and production build.
