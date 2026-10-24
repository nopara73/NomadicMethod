# Exercise feedback

After the final block of an atomic exercise sequence, the whole feedback screen accepts swipes, including its header, footer and empty margins. Pause, settings and other controls retain their normal taps. The single actual exercise card follows the gesture and its last scene stays visible. Intermediate blocks retain their existing automatic continuation and next-block preview.

| Gesture | Visible response | Saved meaning |
|---|---|---|
| Left | 💩 | Meh; existing phase-local rejection |
| Right | ❤️ | Love; existing Keep |
| Up | 🥱 | Difficulty feedback only |
| Down | 💀 | Difficulty feedback only |

Too easy and too hard are distinct recorded outcomes. They do not downvote an exercise, add or remove a Keep, change the heart-derived fitness profile, or introduce another planner ranking rule. Historic Keep/reject data retains its meaning.

The card follows the pointer in both axes. The current displacement selects a direction, with a 12 dp/pixel centre dead zone and 20% axis hysteresis. Crossing back through the centre or directly to the opposite side changes the preview; the initial direction is never locked. Releasing beyond 38% of the card width (bounded to 96–144 dp/pixels) confirms once. Short drags return without a response. Direction hints are hidden at rest and appear during a deliberate drag. A 40 dp/pixel leftward pull, brief hold and spring back every 4.2 seconds demonstrates the gesture, revealing 💩 briefly without recording feedback. Grabbing it transfers the current position to the finger.

Touching and holding pauses the rest clock, including a stationary hold. Releasing resumes only if it was running and the user has not manually paused it. The separate pause control remains available. Backgrounding cancels direct manipulation and leaves the saved rest paused. Reduced-motion settings suppress instructional and confirmation movement without removing the feedback actions.

Direction hints have no circles. They begin at 64 dp/pixels; the selected emoji grows continuously with the pull, travelling to the centre and reaching 90% of the shorter screen dimension at the release threshold. The other choices fade away. Reversing the drag shrinks the old choice and grows the new one. The glyph is rendered at a large base size to avoid magnifying a small raster emoji. Confirming saves the response and freezes the clock, then gives the emoji a short 6% overshoot, a soft single opacity pulse and a settle in 640 ms before advancing immediately. No rapid flashing or wait for the remaining rest countdown. The single last exercise frame stays underneath the transition. Unanswered rest still displays “N seconds to meh”; timeout pulls the card left and runs the same emoji transition for Meh. Reduced motion suppresses growth and advances without the zoom. A manually chosen response never also receives the default response.

Backgrounding interrupts the transition without advancing a hidden workout. The saved response, frozen frame and remaining time survive. On reopen, the paused screen offers Undo or the existing play control to finish the saved response; choosing play repeats the transition and advances immediately. This also handles responses saved by older versions. Undo is only available on this recovered checkpoint, not an additional post-selection wait.

Pending feedback survives a cold reopen and workout-setting changes. Explicitly ending a session retains an already chosen response, but does not invent one when no response was given. Finalized decisions are recorded once for the whole sequence, alongside existing session history. The additive pending field falls back to the old `PendingRestKept` / `pendingRestKept` flag for existing saved workouts.

The four responses also have screen-reader actions. Web keyboard users can focus the card and use arrow keys, or use the accessible response buttons. Visible emoji labels have descriptive accessible names.

Shared gesture fixtures: `web/tests/fixtures/exercise-feedback-cases.json`. Android and web tests cover reversals, diagonal hysteresis, thresholds, undo, restored pending feedback, exact outcome history, and separation of difficulty from rejection scoring.

Native UI verification also requires repeated cold launches into a saved paused rest, real pause/settings taps, swipes from outside the media card, reversal, and a stationary hold. Check both the saved outcome and the visible screen. An ANR dialog or a crashing emulator system process invalidates the run: never dismiss it automatically and count the subsequent interaction as a pass. Use a stable isolated emulator and repeat the checks; emulator results do not imply a phone installation.
