# Exercise execution corrections — 20 October 2026

Status: implementation validated. Publication and device-installation receipts
are recorded separately from this source audit.

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

The catalog now contains 815 exercises. The 814 existing records retain their
identity, anatomy, muscular-demand, equipment and compatibility claims. A reviewed
Standing Body Circles variant supplies a fitting member of existing ankle-circle
family 288; it does not create another movement identity. Its source, admission,
anatomy and physical review are recorded in standing-body-circles-2026-10-20.md.
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

## Validation

The release includes the published planner correction at e3c0f13c. Complete timed
sides retain their existing capacity rules; pure directional exercises may use
their first reviewed direction below 30 minutes. The body-circle variant uses that
existing rule and shares SessionMovementId 288 with the ankle-circle family.
The five fitting lower-leg/foot movements are 191, 284, 1029, 1295 and family 288.

The current revision-83 ledger has zero pairwise, compatible-hard-floor,
materiality and complete-lineup deficits. No quota was reduced and no exception
was added. The separate demand-2 and distinct-lineup diagnostics remain diagnostics,
as required by the existing contract. The ledger was refreshed only after the
independent availability checks reached zero deficits.

All 1,062 Android checks were validated: the full suite passed 1,061 checks, then
the sole remaining metadata-population test passed after its exact catalog pins
were reviewed. All 618 web checks were validated: the full suite passed 615,
including all eight production workout-profile batches; the three metadata-pin
checks subsequently passed after deliberate count and direction-list updates.
No production behavior changed between the full run and those focused reruns.

The production web build succeeds with 815 exercises, 1,000 files and no GIFs.
The Debug Android build succeeds with zero warnings and zero errors. The reviewed
native/web migration filter and shared admission fixture preserve pre-83 sessions.
The parity lock covers 144 native source files, SHA-256 f24b2086814f� .

Source audits cover all 815 records: 1,287 exact secondary training claims, genuine
primary work for every canonical group, the complete physical/equipment partitions,
and 60 identity families covering 149 records. The 18 corrected clips and the new
body-circle clip pass decoding/timing checks. Inventory, source assignment,
primary hashes and identity checks still cover the entire catalog. The original
18 workout clips remain byte-identical in the revision82 archive.

Actual-workout visual review covers 142 screens for the 18 corrections and 13
screens for the body-circle addition: Ready, preparation, every movement/rest
phase and the final Keep control. All inspected media loaded successfully; final
Keep controls were visible and enabled. The source loops, opposite-direction
clips and mirrored sides matched their phases. The body-circle loop was refined
before packaging; its complete circular path and both feet remain visible in
short and long workouts at normal playback speed.

The synthetic review workouts were created through the normal workout API and
contain no user data. Local screenshots, DOM/media observations and build/test
logs are under .artifacts/sequence-audit-20261020/. No phone installation is claimed:
ADB discovers no connected or wireless device.
