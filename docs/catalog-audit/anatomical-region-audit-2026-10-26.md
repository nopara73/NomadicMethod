# Anatomical region assignment audit — 26 October 2026

Catalog revision 97 corrects 85 secondary associations on 84 existing records. The current groupings, primary targets, demand and fitness ratings, movement identities, physical restrictions and execution sequences are retained.

## Scope and findings

The audit screened the small regional inventories against the catalog and the earlier training-claim removal rationales, then inspected 136 exact packaged demonstrations (35 contact sheets), plus the four actual arm-reaction clips and their preview frames. It reviewed adduction/stretching, arm elevation and rotation, elbow movement, hand/forearm action, breathing and pelvic-floor candidates. It is a targeted audit of plausible exclusions, not a claim that every association on all 909 records was reassessed.

| Existing anatomical group | Before | After |
| --- | ---: | ---: |
| Accessory hip adductors | 8 | 20 |
| Rotator cuff | 16 | 68 |
| Forearm flexors and pronators | 18 | 19 |
| Forearm extensors and supinators | 18 | 19 |
| Elbow flexors | 15 | 15 |
| Shoulder adductors and extensors | 23 | 23 |
| Intrinsic hand | 6 | 6 |
| Breathing muscles | 8 | 8 |
| Pelvic floor and perineum | 1 | 1 |

Counts use distinct schedulable SessionMovementId values across complete sequence membership, before modifiers, ability, recovery, preferences or duration filtering. The cuff has 67 directly assigned root identities; the linked 392/399/400 sequence supplies the additional identity through corrected member 400. Repeated sets, sides, directions, aliases and reaction variants do not inflate these counts.

The main errors were incomplete anatomical interpretations. Earlier cuff rationales rejected arm circles because they were not axial humeral rotation, omitting supraspinatus abduction work. Several floor adduction exercises and straight-knee groin stretches credited only the major adductors, omitting the accessory region. Exercise 570 explicitly closes and reopens the fist, yet lacked the forearm mobility associations already admitted for 289 and 1007.

## Evidence and decision boundaries

[University of Washington anatomy](https://rad.uw.edu/muscle-atlas/supraspinatus) identifies supraspinatus as an abduction agonist. [Edwards et al.](https://ijspt.scholasticahq.com/article/29513.pdf) measured substantial supraspinatus activity during active elevation against gravity. Deliberate repeated abduction and goalpost holds can therefore train cuff tissue without being rotation drills. Prone Y/T and W work also received explicit review, informed by the original [prone-elevation](https://pubmed.ncbi.nlm.nih.gov/18174934/) and [external-rotation](https://pubmed.ncbi.nlm.nih.gov/15296366/) studies. This does not assign cuff work to every arm posture or shoulder exercise.

[Texas Tech anatomy](https://anatomy.ttuhscep.edu/musculoskeletal_system/thigh_tables.html) distinguishes gracilis/pectineus adduction from adductor-magnus hip extension. [Lovell et al.](https://pubmed.ncbi.nlm.nih.gov/22814446/) measured both accessory muscles during adduction tests. Gracilis also crosses the knee, so the straight-knee hip-abduction stretches were reviewed separately from bent-knee positions. Assignments are inferences from the demonstrated mechanics; the cited studies did not test these exact catalog clips.

Retained exclusions include ordinary pelvic tilts/bridges (916/1093) for pelvic floor; fist support or extrinsic finger motion (1256/289/570) for intrinsic hand; generic coordinated breathing or an unshown respiratory protocol (392/399/400/407/1238); and unloaded downward arm returns (31/169/389/485) for elbow flexors and shoulder adductors/extensors. Ambiguous flexion-dominant shoulder and bent-knee groin-stretch candidates were not expanded on speculation.

## Implementation and verification

The canonical assignments, exact secondary-claim allowlist and Android runtime catalog change together. Web uses that same catalog. The new revision refreshes Android's catalog database without changing exercise identity or resetting valid saved selections, Keeps, downvotes or history.

Shared membership fixtures cover the restored assignments and the retained negative controls. Both platforms test complete atomic placement, physical requirements and preservation of revision-96 sessions. The review manifest binds each correction to its old and new metadata fingerprints, exact media and a real-time replay of its whole atomic selection in the actual web workout. Earlier media approvals are retained only for matching binary hashes; earlier anatomy reviews remain immutable and can be superseded only through an explicit matching previous-metadata fingerprint.

- [Exact review manifest](anatomical-region-review-2026-10-26.json)
- [Inspected packaged demonstrations](evidence/2026-10-26-anatomical-regions/)
- [Current availability and complete-lineup gate](modifier_coverage_deficits_current.json)
- [Current distinct movement inventory](slot_variety_sourcing_targets_current.json)

Validation, publication and physical-device installation are separate release receipts. The audit does not imply a phone install.
