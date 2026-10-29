# Whole-pose execution audit

Catalog revision 100 reviews all 919 entries after the revision-99 Russian-twist and seated-arm-crossing repair. It corrects 65 further demonstrations: 57 fixed hand/arm positions, six fixed foot positions, and two involving both. Six other owning records also change because their linked members now have complete pairs.

The defect was treating the main action's alternating motion as proof that the entire pose alternates. Squats, twists, kicks and arm movements can leave the same clasp, upper crossing limb, support hand or lead foot throughout the demonstration. The final execution overlay now assigns a complete shown/opposite pair independently of the action's name. Foot-only pairs have their own append-only enum and cue values on Android and web. Existing blue/red timeline accents and mirrored demonstrations communicate the positions.

## Review evidence

[ExercisePositionReviews.json](../../tools/ExercisePositionReviews.json) records an independent whole-pose disposition and exact runtime-media hashes for every entry, including held frames and all 20 opposite-direction clips. It also binds the 20 reaction response clips. Existing 334 paired entries already provide both mirror states for every media segment in every owning sequence; the remaining 585 demonstrations were screened for residual fixed positions, with 132 candidates inspected more closely. Symmetric palms and clips that visibly exchange all relevant limb roles retain their existing single-block protocol. No demonstration bytes, names, training claims, ratings or movement identities change.

[fixed-position-review-2026-10-29.json](fixed-position-review-2026-10-29.json) records all 71 metadata amendments, their reasons, and original/corrected hashes. Historical anatomy-review hashes remain checked against the preserved revision-99 protocol. The current pose audit separately checks the new geometry.

The catalog generator and production web build run [check-exercise-position-reviews.mjs](../../tools/check-exercise-position-reviews.mjs). It rejects missing or stale media reviews and any fixed-position member lacking both mirrors in any owning sequence or direction segment. A reviewed source replacement therefore requires a fresh pose review.

## Existing workouts

[The shared protocol snapshot](../../NomadicMethod/Assets/catalog_protocol_history.json) freezes the original side, direction and sequence geometry for every affected record in unfinished workouts predating revision 100. It composes with earlier revision policies. Both platforms infer a missing active revision from the saved catalog revision before reading or migrating a live workout; initialization persists that frozen revision. Read-only commands do not mutate state.

Regression cases cover Ready, movement and Rest checkpoints, the revision fallback before initialization, unchanged preferences/history, and duration edits that retain every existing set in the current atomic selection. The correction adds no catalog invalidation or feedback reset.

## Validation

The independent gate catches all 65 missing opposite positions in the previous catalog. The corrected catalog has 399 paired entries and passes the unchanged availability, modifier materiality, anatomical, distinct-lineup and complete-lineup gates without any release-blocking deficit. Shared Android/web tests verify each corrected member's complete owning sequence, cues, accents and indivisibility. Final packaged workout renders review both positions, held frames, preparation, movement and Rest, including the exact upcoming linked/side/set preview.

Local validation: 1,242 Android tests passed, followed by 12 focused history/loading regressions after replacing reflection-based parsing; 1,038 web tests and the additional review-chain test passed. All CI catalog scripts and all 65 focused MP4/hold checks passed. The production build contains 919 exercises with zero blocking coverage deficits. The packaged UI review contains 523 Ready/preparation/movement/Rest captures across both positions of every correction, with representative desktop/light renders as well as all mobile/dark cases.
