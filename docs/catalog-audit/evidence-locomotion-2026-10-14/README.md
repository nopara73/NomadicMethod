# Final locomotion and hand-position review

These are direct Chrome captures of the actual web workout, using isolated
local review sessions and Android's canonical runtime media. They show 47
movement blocks across 39 affected exercises. Ready, 45-second movement and
15-second rest were separately exercised in 141 checkpoints; their visible
accessibility state and media/mirroring readbacks are included in
[`locomotion-review-2026-10-14.json`](../locomotion-review-2026-10-14.json).

- [New room movements and their two lead positions](movement-01.jpg)
- [Remaining room movements and field running](movement-02.jpg)
- [Field bounds, existing travel and interlaced-finger holds](movement-03.jpg)
- [Both hand configurations and the Shy/Insect corrections](movement-04.jpg)
- [No locomotion / Room / Field setup states](setup-three-states.jpg)
- [Desktop setup with all nine controls visible](setup-desktop-field.jpg)

All nine controls fit at 280, 360, 480, 1000, 1280 and 1920 px widths;
[`responsive-checks.json`](responsive-checks.json) records the rendered bounds.

The `runtime-new-*.jpg` contact sheets sample the complete final loops of all
24 new movements. They supplement the workout views; successful decoding and
hash equality alone did not establish approval. `lunge-floor-clearance.png`
shows the original [Marine Corps lunge-to-power-skip demonstration](https://www.dvidshub.net/video/558582/lunge-power-skip)
at 13.5 seconds, with the knee hovering above the floor. The matching provenance
and trimmed source are preserved in `tools/ReviewedSourceMedia`.

The review JSON binds every explicit verdict to the current metadata and all
applicable MP4/hold-frame assets, as well as the Android parity and web source
hashes. These are web QA sessions and do not represent user workout history or
proof of installation on an Android phone.
