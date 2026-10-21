# Workout command boundaries

The saved workout owns its catalog revision, single-direction choices, selections,
sets, checkpoints, recorded work and automatic-Light decision. A planner's previous
operation must not determine how a different saved workout is interpreted.

Every public Android service operation that accepts a `WorkoutState` binds its
derived catalog from that state before reading or changing it. The web session
uses the same boundary. The binding cache contains a value copy of the revision
and direction choices; it does not retain a mutable collection owned by the state.
Only construction and that boundary may replace the service's catalog maps.

Starting, reading, shuffling, recording decisions, checkpointing, editing, ending
and reopening therefore work through a fresh service instance. Preparation workers
do not require a hidden call to `Initialize` before editing an existing session.
Initialization remains responsible for explicit migrations and normalization.

An explicit End archives the existing session log and clears active checkpoints.
It neither replans unfinished selections nor invents completed work or rejection
feedback. An incomplete future lineup cannot prevent ending a logged session.
Android commits this bounded operation before returning from the confirmation
callback, so leaving or rotating the activity cannot discard a background End.
Legacy sessions without a session log retain their existing finalization path.

Duration settings expose the supported range on Android, web and instant startup
controls. Completed blocks and every set/side/direction of the current selection
set the minimum. The planner allocates only the remaining budget. Applying an edit
preserves stable slot IDs, checkpoints, earned preferences and actual work, and
records the duration change in the same session history.

`WorkoutCommandBoundaryTests` and `web/tests/command-boundary.test.js` cover Ready,
paused movement and rest through independent workers, duration edits, explicit
End, cold reopening and missing unfinished selections. The Android tests also
complete workouts through a fresh service and persistence boundary at each step.
The persistence test double must retain every persistent state field; live Oura
context remains intentionally excluded from preferences.

The web suite caps concurrent test files at four. This retains every assertion
while bounding the memory used by simultaneous catalog/planning audits.

Saved personal-state replays belong in ignored `.artifacts/` only. They must not
be committed as public fixtures. Native domain tests and signed Debug builds can
be validated without connecting or changing the user's phone.
