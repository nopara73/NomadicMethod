# Stamp feedback

Web and Android implement the owner-selected Stamp preview. One frozen exercise
frame remains visible. The whole workout surface follows the gesture; the chosen
emoji moves independently toward the screen centre. Reversal is allowed until
release. Holding pauses the rest clock without clearing a manual pause.

The accepted preview uses these absolute phase times, not globally eased offsets:

| Time after release | Motion |
| --- | --- |
| 0–270 ms | Lift the emoji to y = −25, scale 1.32, rotation −12° |
| 270–390 ms | Land at y = 12, squash to 1.12 × 0.88, rotate to −7° |
| 390–610 ms | Recoil to the settled scale; brief surface shake and eight impact marks |
| 620–770 ms | Reveal the checkmark |
| 610–1700 ms | Hold the settled emoji; retain the same last frame |
| 1700–2120 ms | Advance exactly once and reveal the actual next screen |

The emoji uses a 248 px/dp font in a 300 px/dp box. Settled scale is
`clamp(shortEdge / 360, .78, 1)`. The wash is lime at 12% opacity, with the
underlying workout at 80% opacity. These deliberately replace the former opaque
wash and 640 ms pulse.

Timeout first performs a 240 ms anticipation, 850 ms left swipe and 180 ms hold,
then the same Stamp. It does not run the swipe and confirmation simultaneously.
Reduced motion keeps a stationary confirmation for 900 ms and fades to the next
screen in 180 ms, without shake, zoom or impact marks.

The response is persisted before motion. Interrupting before advancement preserves
the pending response and rest checkpoint; resuming confirms it without recording
twice. The confirmation's clock is independent of the remaining rest duration.

`ExerciseFeedbackPolicy` and `web/exercise-feedback.js` use the same phase model.
`web/tests/fixtures/exercise-feedback-cases.json` pins the approved timing and
poses for both platforms. Renderer checks also cover timeout, reversal, hold,
pause, interruption and advancement, including small and landscape screens.
