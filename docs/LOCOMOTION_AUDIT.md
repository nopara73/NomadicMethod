# Locomotion and sequence review — 14 October 2026

Catalog revision 80 adds an inclusive, three-state space allowance to Android
and web. The setup tile cycles **None → Room → Field → None**, using a standing
figure, a short lane and a field icon. Its explanation uses the existing brief
modifier animation; the setup screen gains no permanent instructional text.

| Space | Eligible travel | Recorded maximum lane | Distinct session movements | Required minimum |
| --- | --- | ---: | ---: | ---: |
| None | Planted movements within the existing 2 m × 2 m area | 2 m | 538 catalog records | — |
| Room | Planted work plus short walking, shuffling, hopping and skipping passes | 6 m | 26 | 25 |
| Field | Planted and room work plus running and distance bounds | 50 m | 5 | 5 |

The lane figures are conservative space classifications, not instructions to
complete a fixed distance. The existing 45-second work blocks remain unchanged.
Room metadata must be 3–10 m; Field metadata must be above 10 m and at most 100 m.
Stationary running, marching and isolated side steps remain None. Travel is
admitted only where the name and actual demonstration clearly require it.

The exhaustive partition is `tools/ExerciseLocomotionSpaces.psd1`. Its generator
and audit reject missing or duplicated IDs, mismatched aliases and undersized
Room or Field catalogs. Android database validation, web startup and the
production builder independently enforce the 25/5 floors on compatible whole
sequences. Every `SessionMovementId` counts once: sides, hand configurations,
sets, blocks, aliases and renamed duplicates add nothing to the inventory.

## Approved travel inventory

Seven existing traveling movements receive their truthful Room classification;
19 new Room and five new Field movements bring the catalog from 545 to 569.

| ID | Exercise | Space |
| ---: | --- | --- |
| 32 | Tandem Walk Forward and Back | Room |
| 37 | Side Shuffle | Room |
| 119 | Tiptoe Walking Back and Forth | Room |
| 142 | Lateral Squat Walk | Room |
| 194 | Heel Walks | Room |
| 240 | Grapevine Step | Room |
| 999 | Deep-Squat Walk | Room |
| 1033 | Walking Knee Hugs | Room |
| 1034 | Walking Quad Stretch | Room |
| 1035 | Walking Figure-Four Stretch | Room |
| 1036 | Walking Straight-Leg Kicks | Room |
| 1037 | Walking Lunges | Room |
| 1038 | Walking Lunges with Trunk Twist | Room |
| 1039 | Walking Lunges with Overhead Side Reach | Room |
| 1040 | Walking Hip Circles | Room |
| 1041 | Lateral Skipping | Room |
| 1042 | Lateral Crossover Skipping | Room |
| 1043 | Side Shuffle with Arm Swings | Room |
| 1044 | Carioca with Knee Drive | Room |
| 1045 | Crossover Squat Walk | Room |
| 1046 | Diagonal Walking Lunges | Room |
| 1047 | Walking Crossover Lunges | Room |
| 1048 | Power Skipping for Height | Room |
| 1049 | Walking Lunge to Power Skip | Room |
| 1050 | Backward Walking | Room |
| 1051 | Forward Two-Foot Hops | Room |
| 1052 | Sprinting | Field |
| 1053 | Backward Running | Field |
| 1054 | Straight-Leg Bounds | Field |
| 1055 | Alternating Forward Bounds | Field |
| 1056 | Power Skipping for Distance | Field |

## Demonstrations and training claims

All new footage shows actual people performing the named established movement
at normal speed. Reviewed, immutable source excerpts and their public source
pages are recorded in `tools/ReviewedSourceMedia/provenance.json` and
`tools/ExternalExerciseMedia.psd1`; Android runtime MP4s remain the web's only
exercise-media source. The relevant sources include the U.S. Marine Corps
[Force Fitness demonstrations](https://www.dvidshub.net/video/642014/power-skip-distance),
NHS physiotherapy, Jr. NBA, Runner's World, Strength Running and runcoach.

Review covers the full packaged loop, crop, seam, normal speed, feet-only contact,
shoes, equipment, travel, and every Ready/45-second movement/15-second rest
presentation. Carioca and lateral crossover skipping use two complete, mirrored
lead blocks. No reverse playback or synthetic movement is used. Travel resets
to the beginning of the filmed pass at a visible loop boundary.

The distance-skipping excerpt ends before the athlete approaches close enough
to leave the frame; its centered front-view crop removes the other camera and
title banner. The bounding excerpt similarly removes the instructor overlay.
Lunge footage retains clear knee-to-floor separation. These decisions were
checked again in the actual web workout with isolated review checkpoints.

Anatomy remains a training claim. Walking-lunge twists and side reaches receive
no abdominal secondary claim merely for turning or bracing. The exact secondary
allowlist contains 1,097 claims, and all 30 canonical muscles retain genuine
primary session movements. Demand ratings and mirror relationships are reviewed
on the action itself; locomotion quotas never justify changing them.

## Related category corrections

The same review corrects six fixed crossed-arm or interlaced-hand configurations:
32, 216, 224, 510, 740 and 1012. Each selection completes the shown hand position
and its opposite in two adjacent blocks, with the existing blue/red cues and
correct mirrored footage or hold frame. Both configurations still count as one
exercise. The broader Shy review changes 29 existing movements and excludes six
conspicuous new drills; its exact partition and reasons are in
[`SHY_MODE_AUDIT.md`](SHY_MODE_AUDIT.md).

Goalpost Jumping Jacks, Scarecrow Jacks and Arm-Circle Jumping Jacks become three
independent existing movements. This lets Shy retain the familiar goalpost drill
and exclude the conspicuous variants. Alternating Palm-Up Shoulder Rotations
(683) becomes Insect-compatible because its final demo shows continuous torso
and hip turns with weight shifts. Its primary rotator-cuff claim remains intact;
incidental activation during jumping jacks supplies no substitute claim.

The old series' whole-selection Keep and phase feedback transfer to all three
previously chosen movements. Existing individual feedback is preserved. This
translation runs only when a revision 73–79 preference enters the current
catalog, including after an unfinished frozen workout ends. Later standalone
choices never grant a Keep or copy feedback to other variations.

## Persistence and validation

Database version 96 adds the travel enum and validates its space range. Catalog
revision 80 preserves scores and anatomically valid Keeps. Old unfinished
revision 73–79 workouts retain their original hand blocks, jack series and Shy
semantics until completion or explicit ending. A persisted active catalog
revision keeps that view stable across repeated cold reopenings. A saved old
travel selection gains only the active Room allowance needed to preserve it;
future setup still defaults to None. Session IDs, completed work, frozen Light
decisions, and paused movement/rest time survive the update.

All 44 quadratic single/pair profiles pass availability and complete-lineup
validation. Locomotion uses its explicit global movement floors rather than a
per-muscle travel quota. The pre-existing accepted Silence/Hard-Floor materiality
exception retains the same 13 affected movements, seven muscle buckets and
14/3 requirements. Its baseline and modified denominators become 267/254 because
the default None allowance excludes an existing traveling movement; no new
deficit is waived. Demand and distinct-lineup inventories remain diagnostics.

Validation: 900 Android tests, 457 web tests, native Debug build, parity lock,
production web build, catalog definition/side/secondary/mirror/Hard Floor/session
movement/locomotion audits, and the current catalog-bound deficit ledger.
Visual evidence is recorded in
[`locomotion-review-2026-10-14.json`](catalog-audit/locomotion-review-2026-10-14.json)
and the [workout capture index](catalog-audit/evidence-locomotion-2026-10-14/README.md)
for 39 affected exercises and 141 isolated workout checkpoints. Physical Android installation requires a connected
phone and is reported independently from this build validation.
