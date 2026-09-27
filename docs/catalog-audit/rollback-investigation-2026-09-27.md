# Catalog and workout regression investigation — 27 September 2026

## Finding and immediate repair

The two-sided regression is in `1bfc2f4c` (26 September), not in a new catalog release. Its adaptive preparation compares complete anatomical partitions and accepts a coarser partition when it reduces rejected exercise blocks. A seven-minute workout can consequently use five muscle groups and spend the remaining two blocks on sides. Its original Android and web tests explicitly expected this change from resolution 7 to resolution 5.

The repair preserves the duration's complete partition at 3, 5 and 7 minutes. Rejections cannot free muscle-group blocks for sides. Adaptive preparation remains available at 10–30 minutes. Existing saved sessions retain their stored plans and completed work. No exercise, media, anatomy, score, Keep, recovery rule or persistence version is changed by this repair.

The focused tests reproduced six failures on each platform before the repair, then passed after it. Additional tests cover high-scoring paired movements, manual Light, shuffle and equipment changes at every short duration; the shared fixture also verifies that a ten-minute workout can still fit complete alternatives. Full release verification is required separately.

## Rollback checkpoints

The requested baseline is not yet confirmed, so both plausible checkpoints were examined directly from Git. Counts below compare with `1bfc2f4c`.

| Checkpoint | Catalog records | IDs still present | New IDs since checkpoint | Shared IDs with changed names | Shared IDs with changed primary / secondary anatomy |
| --- | ---: | ---: | ---: | ---: | ---: |
| `3215fbb9`, 4 August: immediately before the 6 August full rebuild | 328 | 326 | 219 | 258 | 202 / 305 |
| `eee9d517`, 5 September: before the large reviewed integration | 517 | 516 | 29 | 167 | 157 / 454 |
| `f5439c20`, 11 September: reviewed integration | 539 | 539 | 6 | 0 | 0 / 0 |

The current catalog contains 545 records, 502 sequence roots and 301 one-block roots. Records, sequence roots and distinct session movements are different quantities; a larger record count does not establish better selection or quality.

The 6 August rebuild (`7bd03613`) retained the 328-record count while changing identities and media within it. Of the 326 surviving pre-rebuild IDs, all now reference different packaged video bytes. That is a byte comparison, not proof that every demonstration depicts a different movement: re-encoding and other media edits also change bytes. Keeping an ID alone is insufficient evidence that an old score still describes the same exercise.

The September audit was also a substantial replacement, not merely an expansion. Its changes to existing records greatly outnumber its additions. The six exercises added after the integration leave the 539 integrated names and muscle associations unchanged.

## Confirmed loss of useful selection

The historical September engine/catalog and current engine/catalog were loaded independently. The comparison counts distinct compatible **one-block** movements, since those must fit the 3-, 5- and 7-minute partitions. It does not treat a two-block movement as an available one-minute alternative.

With tall Mirror, Hard Floor and Wall enabled (modifier value 60), and the other modifiers off:

| Workout group | Before September integration | Current |
| --- | ---: | ---: |
| 7-minute gluteals / deep hip | 7 | **1** |
| 7-minute hip flexors / adductors | 3 | 3 |
| 5-minute head / neck / shoulder girdle | 13 | 9 |
| 5-minute hips / thighs | 2 | 5 |
| 5-minute lower legs / feet | 8 | 15 |
| 3-minute upper body | 6 | 10 |

The seven-to-one glute decline also occurs with Silence, Hard Floor and upper-body clothing enabled and no equipment (modifier value 146). The only current one-block choice is ID 130, **Squat to Alternating Side Leg Lift**. Repeatedly rejecting it cannot create an alternative within the existing seven-minute partition. Yesterday's adaptive change concealed this shortage by changing the partition.

With Insect added to that equipment-free profile (147), the short upper-body group also has only one distinct one-block choice, ID 248, **Alternating Side-Tap Palm Pushes**. Some other groups gained choices; the deterioration is uneven.

The missing glute options illustrate why an indiscriminate metadata rollback is unsuitable:

- IDs 15 (standing hamstring curls), 94 (lateral weight shift), 102 (squat with arm sweep), 185 (cloud hands) and 609 (plié squat) lost secondary claims needed to meet the group's coverage threshold.
- ID 161 (gate opener) became a complete two-block sequence, so it no longer fits a one-block slot.
- ID 130 still meets the current coverage rule, leaving it as the sole choice.

These observations establish the selection bottleneck. They do not establish that all removed anatomy claims should be restored. Restoring a doubtful training claim solely to improve availability would repeat the original problem.

## Other changes that affect a rollback

The post-audit planning changes were substantial too. In particular, `c7f54826` reduced `MinimumExercisesPerBroadPairStatePerGroup` from five to one and moved materiality requirements to diagnostics. The current supplied/local `AGENTS.md` again requires five broad choices and materiality gates. The executable policy and that instruction therefore disagree. Passing the existing suite does not certify the stronger five-choice requirement. This pre-existing mismatch is recorded here, not silently waived or hidden by refreshing a ledger.

Later commits also contain independently valuable fixes that a repository reset would remove:

| Preserve | Evidence / commits |
| --- | --- |
| Unfinished sessions survive reopening; duration edits preserve completed and current atomic work; explicit end archives actual work | `8416d921` |
| Backgrounding pauses work instead of counting time outside the app | `72f6b8d3`, plus the later session-continuity changes |
| Finishing a workout does not crash while trying to prepare the next workout | `294d2d75`, integrated in `a476d087` |
| Debug-only in-place phone installation and signing checks | `9bff7486` and current deployment contract |
| Android/web synchronization and migration coverage | `65d77c6f`, `5458443b` and subsequent contract tests |
| Current Nomadic Method package/storage identity | `df3ab5de`, `df04b5ca`, `613ee102`, `d841e563` |

These are retention candidates with identifiable behavior, not a claim that every later change is good. Light/recovery policy, equipment options, ranking and presentation should be considered individually against the intended product; they should not all be carried over automatically.

## Recommended recovery

Use a selective rollback with the old product contract as the reference and the current app as the migration-safe delivery shell. A reset to the August checkout would also restore the old package/storage model and remove later continuity fixes. Copying the old JSON into the current application would mix incompatible identity and migration assumptions.

1. Ship the isolated short-duration repair, preserving the current catalog and user data.
2. Confirm the preferred historical baseline. Recover its workout behavior explicitly; do not treat every intervening algorithm change as an improvement.
3. Compare exercise identity, final media and honest anatomy record by record in the affected groups first. Keep worthwhile additions in a separate reviewed retention list. Restore a previous exercise only with its matching media, timing and identity migration, not merely its old name or muscle claims.
4. Resolve the choice/coverage contract explicitly. Restore useful choices through real exercises and truthful selection rules; do not broaden short workouts, inflate anatomy or lower validation to hide a shortage.
5. Validate the selected combination on Android and web, including the existing saved workout and feedback, before replacing the public catalog or installing it on the phone.

No catalog rollback, mass retirement, score reset, anatomy restoration or historical cherry-pick was performed during this investigation. The 29 post-5-September IDs and all earlier additions remain available for review rather than being discarded. A complete final-media quality judgment of 545 records is outside the evidence collected here.

## Reproduction

Historical inputs: `git show <ref>:Flux/Assets/exercises.json` and the matching `web/workout.js` / `web/light-cadence.js`; current inputs use `NomadicMethod/Assets/exercises.json`. Inventory comparisons filter roots to one `sequenceBlock`, use each historical engine's `isWorkoutSelectionCandidate`, and deduplicate by `sessionMovementId` (falling back to exercise ID). The local detailed snapshot comparisons and executable investigation are in ignored `TestResults/rollback-investigation/`; they contain no phone data. Private phone backups are separate under ignored `TestResults/release-2026-09-27/`.
