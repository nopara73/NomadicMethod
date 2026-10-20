# Exercise execution corrections — 20 October 2026

Status: implementation preserved as an unpublished draft. Catalog availability
validation fails; this is not a validated release.

## Correction

Standing Ankle Circles Both Ways (1299) requires four complete 45-second phases:
both circular directions on the first foot, followed by both on the other foot.
The former single-block `Alternating` metadata was incorrect. Its movement
identity remains 288, shared with the existing knee-and-ankle circle records.

A targeted media audit reviewed 108 suspicious loops: recent floor exercises
marked alternating, circle/roll exercises without direction metadata, and the
latest catalog additions. It found these 18 execution corrections:

| ID | Exercise | Complete protocol |
| --- | --- | --- |
| 236 | Bilateral Wrist Circles | 2 directions |
| 275 | Small Arm Circles | 2 directions |
| 516 | Bent-Elbow Shoulder Circles | 2 directions |
| 588 | Belly-Dance Alternating Shoulder Rolls | 2 directions, naturally alternating shoulders |
| 743 | Standing Large Arm Circles | 2 directions |
| 1097 | Lying Heel Slides | 2 timed sides |
| 1109 | Side-Kneeling Leg Circles | 2 sides × 2 directions |
| 1112 | Side-Lying Front-and-Back Kicks | 2 timed sides; each repetition includes front and back |
| 1117 | Side-Lying One-Arm Push-Ups | 2 timed sides |
| 1139 | Prone Hip Internal and External Rotations | 2 timed sides; each repetition includes both rotations |
| 1162 | Side V-Ups | 2 timed sides |
| 1177 | Lying Single-Leg Raises | 2 timed sides |
| 1180 | Lying Straight-Arm Circles | 2 directions |
| 1214 | Side-Lying Inner-Thigh Raises | 2 timed sides |
| 1225 | Lying Single-Leg Circles | 2 sides × 2 directions |
| 1230 | Side-Lying Leg Circles | 2 sides × 2 directions |
| 1244 | Quadruped Side Bends | 2 timed sides |
| 1299 | Standing Ankle Circles Both Ways | 2 sides × 2 directions |

The generator applies an explicit reviewed execution overlay after historical
replacement overrides, removing stale side and direction membership. Fixed-side
loops show one complete execution phase. Opposite circular directions use a
reviewed source segment or a reversed simple cyclic mobility loop. Mirrored
footage reverses screen-clockwise cues. The phase model remains shared between
Android and web.

The catalog still contains 814 exercises. No identity, anatomy, muscular-demand,
equipment or compatibility claims were changed to compensate for lost choices.
One obsolete unreferenced GIF (1023) was removed from the runtime inventory;
its original bytes are retained in the local audit artifacts.

## Existing workouts

Revision 83 applies the corrections to new workouts. Unfinished pre-83 workouts
retain their original phase geometry and byte-identical footage under
`exercise_videos/revision82/`. Their selections, current checkpoint, score,
Keeps, history and frozen Light decision remain intact. Browser catalog identity
uses the canonical media path even when an active workout plays archived media.

Shared fixtures cover revision 81/82 metadata, original video hashes and a
synthetic revision-82 ankle workout restored from Ready, movement and rest after
two year-long gaps. The fixture includes an existing Keep and phase score.

## Validation and release blocker

The validation checkout excludes concurrent short-workout planner changes from
another chat. Its Android suite passes 1,042 of 1,043 tests and its web suite
passes 597 of 598 tests. The single failing test on each platform is the required
catalog coverage gate. Focused execution/restoration tests pass on both
platforms. The 18 corrected primary clips and their directional media pass media
decoding/timing checks, while inventory and identity checks cover all 814 records.
Execution-overlay and mirrored four-phase generator regression checks pass.

Corrected availability is four rather than the required five distinct selectable
movements for `r10.anterior-lateral-lower-leg-dorsal-foot` and
`r15.anterior-lateral-lower-leg-dorsal-foot` in affected profiles. This produces
174 pairwise rows and eight compatible-hard-floor rows. Modifier materiality and
complete-workout lineup checks have no deficits. The four fitting movements are
unsupported toe raises (191), side-to-side ankle rocks (284), alternating heel
digs (1029) and heel-to-toe rocking (1295).

The production web build rejects those deficits and the stale diagnostic ledger.
The ledger was not refreshed to waive the gate. A fifth genuinely eligible
movement with verified media, or an explicit owner-approved rule change, is
still required. Additional head/neck and hip shortages observed with the other
chat's new short-workout policy are outside this isolated change.

Actual-workout visual verification is also pending: the preview browser could
not attach. No deployment or phone installation has been performed.

Local evidence is under `.artifacts/sequence-audit-20261020/`; the isolated
validation checkout and its parity lock are under that folder's `validation/`.
