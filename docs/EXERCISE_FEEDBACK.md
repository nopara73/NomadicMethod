# Exercise feedback

After the final block of an atomic exercise sequence, the whole feedback screen accepts swipes, including its header, footer and empty margins. Pause, settings and other controls retain their normal taps. The single actual exercise card follows the gesture and its last scene stays visible. Intermediate blocks retain their existing automatic continuation and next-block preview.

| Gesture | Visible response | Saved meaning |
|---|---|---|
| Left | 💩 | Meh; existing phase-local rejection |
| Right | ❤️ | Love; existing Keep |
| Up | 🥱 | Difficulty feedback only |
| Down | 💀 | Difficulty feedback only |

Too easy and too hard are distinct recorded outcomes. They do not downvote an exercise, add or remove a Keep, change the heart-derived fitness profile, or introduce another planner ranking rule. Historic Keep/reject data retains its meaning.

The card follows the pointer in both axes. The current displacement selects a direction, with a 12 dp/pixel centre dead zone and 20% axis hysteresis. Crossing back through the centre or directly to the opposite side changes the preview; the initial direction is never locked. Releasing beyond 23% of the card width (bounded to 56–88 dp/pixels) confirms once. Short drags return without a response. Direction hints are hidden at rest and appear during a deliberate drag. A quiet leftward nudge every 4.2 seconds hints at the gesture while awaiting a response; it does not record feedback.

Touching and holding pauses the rest clock, including a stationary hold. Releasing resumes only if it was running and the user has not manually paused it. The separate pause control remains available. Backgrounding cancels direct manipulation and leaves the saved rest paused. Reduced-motion settings suppress instructional and confirmation movement without removing the feedback actions.

All four direction hints use 104 px/sp emoji in 128 px/dp targets. They stay the same size during the drag; the selected target changes colour at the commit threshold. Confirming a response freezes the clock, saves the response, and brings that emoji to the centre in 620 ms before advancing immediately to the next exercise (or workout completion). Its final size is 68% of the shorter screen dimension, so the whole emoji remains readable rather than zooming beyond the screen. There is no wait for the remaining rest countdown. The single last exercise frame stays underneath the transition. Unanswered rest still displays “N seconds to meh”; timeout nudges the card left and runs the same emoji transition for Meh. Reduced motion advances without the zoom. A manually chosen response never also receives the default response.

Backgrounding interrupts the transition without advancing a hidden workout. The saved response, frozen frame and remaining time survive. On reopen, the paused screen offers Undo or the existing play control to finish the saved response; choosing play repeats the transition and advances immediately. This also handles responses saved by older versions. Undo is only available on this recovered checkpoint, not an additional post-selection wait.

Pending feedback survives a cold reopen and workout-setting changes. Explicitly ending a session retains an already chosen response, but does not invent one when no response was given. Finalized decisions are recorded once for the whole sequence, alongside existing session history. The additive pending field falls back to the old `PendingRestKept` / `pendingRestKept` flag for existing saved workouts.

The four responses also have screen-reader actions. Web keyboard users can focus the card and use arrow keys, or use the accessible response buttons. Visible emoji labels have descriptive accessible names.

Shared gesture fixtures: `web/tests/fixtures/exercise-feedback-cases.json`. Android and web tests cover reversals, diagonal hysteresis, thresholds, undo, restored pending feedback, exact outcome history, and separation of difficulty from rejection scoring.
