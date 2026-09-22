# Oura recovery (Android only)

Owner decision, 2026-09-11: implement this **only on the phone**, where Oura and
Health Connect run. Do not add browser OAuth, snapshot imports, a web recovery
toggle or cloud uploads. This is an explicit exception to shared product parity;
web keeps its existing Light policy. The mobile parity lock still tracks the
Android implementation and its permission surface.

## Contract

This is a first-version training heuristic, not a clinical recovery measure.
Only `com.ouraring.oura` records are read: sleep sessions/stages, heart-rate
samples and RMSSD HRV. Readiness, Sleep and Activity scores are not used. There
are exactly three read permissions, no write/background/history permission.
The native integration requires Android 14+; older devices use the countdown.

Use available Oura data automatically. There is no Nomadic Method opt-in, connection tile,
status dialog, refresh button or disconnect control. Do not add any of these.
On Android 14+ with Oura installed, request the three permissions once from
Android's Health Connect screen after normal setup has loaded. Never interrupt
a restored or running workout with a permission request. Denial does not cause
repeated prompts; Android settings remain the authority for granting/revoking
access. Already granted access is used immediately, including after an upgrade
from the short-lived opt-in implementation; its old `enabled` flag is ignored.
Missing,
denied or revoked access, unavailable service, failed/partial reads, bad data and
stale measurements all leave the existing cadence in charge. A successful read
replaces the snapshot, including deletions. Foreground refresh is asynchronous,
normally at most once every five minutes; granting permission triggers a read. Start
does not wait for network, sensor or Health Connect I/O.

## Evidence and algorithm

- Read the preceding 30 days, within Health Connect's initial grant window.
- Deduplicate source records and sample timestamps. Find the longest completed
  staged sleep per recorded local wake date. With no main-sleep/nap label in
  Health Connect, principal sleep requires >=3h asleep, or >=90m ending at
  04:00–12:59 local time. Other short sleeps are ambiguous, not proof of severe
  sleep loss. This conservative inference also permits longer daytime sleep.
- Sleep duration excludes awake stages. Unknown/gapped stages make duration
  unreliable (only a one-second endpoint tolerance). Add the union of asleep
  intervals, including naps, in the preceding 24 hours. The latest total ends
  at assessment time (so an afternoon nap counts immediately); historical
  nightly totals end at principal sleep end.
  Overlapping sessions never double-count sleep.
- Both HR and HRV need >=70% of known asleep time. Each sample covers at most
  +/-2.5 minutes, clipped to asleep intervals; gaps are not interpolated.
- The latest main sleep must end less than 18h ago. Require 3 usable nights in
  the latest 4 wake dates and >=14 **earlier** usable nights in the preceding
  28 wake dates, excluding the recent three.
- Per night: average asleep HR; average asleep HRV then take its natural log;
  actual 24h sleep duration. Compare three-night averages against baseline
  nightly means and sample SD (between nights, denominator n-1).

Warnings:

1. Three-night log-HRV mean is >1 baseline SD below baseline mean.
2. Sleeping HR mean is >=5 bpm above baseline **and** >1 baseline SD above it.
3. Latest sleep total <6h **or** three-night average sleep <7h.

Two or three warnings: **Light required**. Zero: **regular permitted**, replacing
only the cadence gate. One: **unknown**, cadence decides. Reliable actual sleep
<5h independently requires Light, even without enough HRV/baseline data.
Unusually high HRV (>2 baseline SD above mean) makes a would-be regular/unknown
decision unknown; it cannot erase two other warnings. No missing sleep is
interpreted as zero sleep. A tiny 1e-9 comparison tolerance prevents numerical
noise around flat baselines; no physiological threshold is silently added.

## Interactions and persistence

Oura regular clearance expires immediately for the **next** workout after
meaningful/hard work performed after that sleep. It does not wipe history or
pretend the later work has recovered. Manual Light and the existing 18h/36h
muscle recovery rules and >=80% recovery presentation remain independent.

Automatic requirement at workout start is recorded as a nullable Boolean in the
session log and frozen through that workout, including equipment changes and
process restarts. It contains no measurements. Historical logs lacking the
field keep their existing fallback; no log, score, Keep or recovery timestamp is
rewritten. The next workout reevaluates current evidence. Light selected manually
is not removed merely because an automatic requirement expires.

When Oura produces a decisive result, hide the cadence estimate: it cannot
predict when future Oura readings will require Light. Unknown data uses the
usual counter. Locked Light keeps the headline `rest, you must`, with a smaller,
brief reason underneath. Oura shows only the warnings that actually contributed
to the decision: RHR/HRV compare the personal baseline on the left to the recent
three-night value on the right (bpm and ms respectively; HRV is converted back
from log units). Sleep shows actual duration against its applicable boundary.
Do not infer a warning from the averages in the UI: use the policy's recorded
warning flags. Do not display rounded equal values as an inequality. No warnings
or unknown historical evidence means a plain recovery explanation, not invented
measurements. An active workout uses its private start-decision audit, never a
newer reading as an explanation for the frozen lock. No health measurements are
added to workout history. Cadence and muscle-derived locks use short non-Oura
reasons on both platforms. The explanation never changes any selection or state.

Raw Health Connect records are not retained. Nightly summaries and at most 120
decision audit entries live in `NoBackupFilesDir/oura-recovery.json`; they are
not included in the backed-up workout state or logcat. The live context is
`JsonIgnore` and explicitly copied to background lineup preparation. Android
permission revocation is checked on every foreground return and before consuming
a snapshot; it removes the cached summaries and decision log without clearing
workouts. Only the permission-request-attempt marker remains, avoiding repeated
prompts. The platform's required health-data privacy page is not a Nomadic Method feature
screen and offers no application-specific opt-out.

## Tests and future changes

`Flux.Tests/Fixtures/oura-recovery-cases.json` pins boundary and fallback cases.
`OuraRecoveryPolicyTests` covers the policy, aggregation, persistence, active
session gate and interaction with cadence. Tests must remain truthful: do not
weaken data sufficiency to make the phone return a decisive result. Missing
baseline/HRV is an expected safe outcome. Validate source origin and permission
UI on the actual connected phone, and inspect its local decision log before
claiming Oura is governing the workout. Never start a workout merely to verify
integration without the user's agreement.
