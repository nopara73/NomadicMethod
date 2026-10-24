# Exercise feedback

After the final block of an atomic exercise sequence, the actual exercise card is the feedback surface. Its last scene stays visible. Intermediate blocks retain their existing automatic continuation and next-block preview.

| Gesture | Visible response | Saved meaning |
|---|---|---|
| Left | 💩 | Meh; existing phase-local rejection |
| Right | ❤️ | Love; existing Keep |
| Up | ↑ too easy | Difficulty feedback only |
| Down | ↓ too hard | Difficulty feedback only |

Too easy and too hard are distinct recorded outcomes. They do not downvote an exercise, add or remove a Keep, change the heart-derived fitness profile, or introduce another planner ranking rule. Historic Keep/reject data retains its meaning.

The card follows the pointer in both axes. The current displacement selects a direction, with a 12 dp/pixel centre dead zone and 20% axis hysteresis. Crossing back through the centre or directly to the opposite side changes the preview; the initial direction is never locked. Releasing beyond 23% of the card width (bounded to 56–88 dp/pixels) confirms once. Short drags return without a response. Direction hints are hidden at rest and appear during a deliberate drag. A quiet leftward nudge every 4.2 seconds hints at the gesture while awaiting a response; it does not record feedback.

Touching and holding pauses the rest clock, including a stationary hold. Releasing resumes only if it was running and the user has not manually paused it. The separate pause control remains available. Backgrounding cancels direct manipulation and leaves the saved rest paused. Reduced-motion settings suppress instructional and confirmation movement without removing the feedback actions.

A chosen response is saved immediately as pending feedback, with a small receipt and Undo. The exercise stays visible for the rest of the original 15-second rest. Undo returns to awaiting a response; it does not reset the timer. Unanswered rest displays “N seconds to meh”. On expiry the same card animates left before the Meh outcome is finalized. A manually chosen response never also receives the default response. Pausing/backgrounding during the timeout animation retains a resumable checkpoint.

Pending feedback survives a cold reopen and workout-setting changes. Explicitly ending a session retains an already chosen response, but does not invent one when no response was given. Finalized decisions are recorded once for the whole sequence, alongside existing session history. The additive pending field falls back to the old `PendingRestKept` / `pendingRestKept` flag for existing saved workouts.

The four responses also have screen-reader actions. Web keyboard users can focus the card and use arrow keys, or use the accessible response buttons. Visible emoji labels have descriptive accessible names.

Shared gesture fixtures: `web/tests/fixtures/exercise-feedback-cases.json`. Android and web tests cover reversals, diagonal hysteresis, thresholds, undo, restored pending feedback, exact outcome history, and separation of difficulty from rejection scoring.
