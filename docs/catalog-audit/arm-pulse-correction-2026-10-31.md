# Arm-pulse name and fixed-stance correction

Exercise 264's complete packaged 2.1-second loop shows short arm pulses with a
fixed asymmetric foot angle, knee bend and facing direction. It does not show
complete arm circles. The owner identified the naming and stance errors while
performing its linked three-block sequence.

Revision 102 renames 264 to **Staggered-Stance Arm Pulses** and uses
`ShownFootPositionThenOpposite`. Its complete atomic sequence is now:

1. Arm pulses in the shown stance, 45 seconds.
2. Arm pulses with the whole demonstration horizontally mirrored, 45 seconds.
3. Standing Wheel Arm Circles (406), clockwise, 45 seconds.
4. Standing Wheel Arm Circles (406), counterclockwise, 45 seconds.

Ordinary rest intervals remain between blocks. The planner must fit the complete
four-block selection; neither stance is a separate selectable movement or a new
coverage entry. Both members retain their existing IDs, media bytes, anatomical
claims, muscular demand and fitness levels. Review of 264's existing
`arm-endurance` assessment found that its short arm pulses still match that
profile; the foot-position correction supplies no additional fitness claim.

The whole-pose review for 264 now explicitly requires opposite blocks, including
inside its owning linked sequence. Source catalog generation reproduces the
name, motion profile, stance protocol and four blocks. No runtime media was
replaced or synthesized.

Unfinished pre-102 workouts retain the original three-block protocol through an
additional versioned protocol snapshot. The pre-100 snapshots remain intact and
are applied after newer snapshots when restoring older workouts. The exact
`Standing Arm Circles` name correction preserves scores and Keeps; older
retired identities retain their existing migration behavior.

The shared `arm-pulses.json` contract covers both platforms. Regression checks
exercise both stances and wheel directions, whole-sequence budgeting, preserved
media, historical protocols, feedback migration and Ready/movement/rest
restoration after a year away and subsequent shortening without cutting the
current three-block legacy selection. Visual verification uses a synthetic local
workout, separately from the user's saved workout data.

The 390-by-844 browser replay verified the complete natural 45-second work
blocks and 15-second transitions. The full name fits, both feet remain visible,
the second block mirrors the entire stance, and both wheel directions follow
without an intermediate outcome or sequence replacement. Counterclockwise
playback uses the reviewed 7.75-second directional loop at playback rate 1.

Initial validation covered 1,269 Android cases and 1,061 web cases across the exhaustive
runs and passing reruns of updated historical-version assertions. The Android
SQLite migration checks include upgrades from schemas 115 through 117 to
118. The production web build, optimized debuggable Android build, two-movement
media audit, generator regression checks, whole-pose audit, parity lock and
fitness assessment gate passed. The regenerated catalog has zero admission
deficits; the four-block movement no longer counts as fitting a three-minute
workout.

The final release also retains the concurrently published equipment grouping
and push-up naming corrections. That combined source passed 505 Android checks,
428 web checks and both builds. Additional passing protocol reruns verify
shortening after restoration from Ready, movement and rest.
