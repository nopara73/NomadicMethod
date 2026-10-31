# Hard-floor compatibility audit

Hard Floor is the second workout-context modifier and is enabled by default. It is one
combined surface contract: the floor is both rigid and slippery. Slipperiness
is not a separate modifier or optional interpretation. Its two UI states are:

- **Hard floor ON:** only exercises reviewed for both hard-floor ergonomics and
  low-traction execution are selectable.
- **Hard floor OFF:** the user has a stable soft floor, so both `Compatible` and
  `Incompatible` exercises are selectable.

"Soft" means an ordinary stable padded exercise surface or carpet. It does not
mean a mattress, unstable foam, or deep pile that compromises footing.

## Review standard

Every catalog ID must appear exactly once in
`tools/ExerciseHardFloorCompatibility.psd1`. A new or reactivated exercise is
`Unreviewed` until that explicit decision is added; generation, Android tests,
and web tests reject an incomplete partition.

An exercise is `Incompatible` when its demonstrated execution makes a rigid,
slippery floor meaningfully less ergonomic or requires dependable traction
through one of these audited mechanisms:

- heel or forefoot loading that actually requires cushioning or dependable
  traction in the demonstrated execution; ordinary controlled vertical calf
  raises do not become incompatible merely because the heel lifts;
- repeated jumping or landing;
- running or rapid foot impact;
- deliberate stomping;
- forceful lateral push-offs, rapid reversals or loaded direction changes that depend on traction;
- a traction-loaded wide, split, lunge, or staggered stance;
- pivoting strikes, stance-driven boxing movements, or forceful kicks; or
- wall or balance loading that depends on reliable floor traction.

Ordinary planted standing, controlled straight-line stepping, vertical
squatting and calf raises, static single-leg balance, mobility, and upper-body work remains
`Compatible` unless the actual demonstration meets one of those mechanisms.
The slippery-floor review does not assume a wet, oily, or otherwise acutely
unsafe surface on which nobody should exercise. Classification follows the
final packaged movement, not its name or a coverage target. Every mandatory
sequence must use one consistent floor classification across all of its blocks.

Controlled short hand-and-foot steps are not excluded merely for traveling. Review their actual impact, contacts and shear against already-admitted controlled walking, push-ups and reverse tabletop work. Source footage using a mat does not by itself establish a padding requirement.

See the [44-movement locomotion review](HARD_FLOOR_LOCOMOTION_REVIEW_2026_10_31.md) for the explicit 31 October 2026 decisions.

## Current result

- `Compatible`: 427 exercises
- `Incompatible`: 492 exercises
- `Unreviewed`: 0 exercises

## Earlier migrations

Catalog revision 53 rebuilds cached placements for the 97 reclassified
exercise IDs only when the saved profile has Hard Floor enabled. Soft Floor
placements remain valid. Scores, keeps, phase feedback, history, and recovery
state are preserved on both platforms. Android SQLite schema 75 applies the
same catalog refresh without deleting stored scores.

Catalog revision 59 additionally corrects exercise 565 from a supposed
heel-down mini squat to the demonstrated mini-squat calf raises. Its cached
placement is rebuilt while its user score remains intact; Android SQLite
schema 80 refreshes the corrected identity, anatomy, demand, and floor verdict.

Catalog revision 63 restores the truthful pogo-bounce identities of exercises
439, 442, and 444. Their unchanged demonstrations combine continuous compact
pogo bouncing with fixed-gaze head turns, nods, or tilts; they are therefore
classified under `RepeatedJumpingOrLanding` and cannot run on the rigid,
slippery Hard Floor profile. Their impact anatomy, moderate demand, and natural
execution noise are restored at the same time. Cached placements are rebuilt,
while scores, Keeps, phase feedback, history, and recovery remain intact.
Android SQLite schema 84 refreshes the corrected metadata in place.

The audit also rejects any honestly named or profiled jump, hop, pogo, bounce,
jack, or bound if it is marked Hard Floor compatible. This guards the entire
current airborne-impact catalog rather than relying only on the three corrected
IDs.

## Coverage requirements

The counts above are audit results. Wall remains outside pairwise quotas;
equipment gating excludes WallRequired exercises when no wall is available.
Physical floor classifications remain unchanged.

The 2026-09-11 completion policy requires nonempty Compatible availability in
every applicable broad and fine bucket, together with complete atomic lineups.
It retires the five-choice broad population target. No Incompatible counterpart
is required. Hard Floor off still admits both truthful categories. Keep the
established breadth rule, physical predicates and exact Insect exceptions.
Percentage materiality remains diagnostic.

Two mixed-floor mandatory sequences were separated during the current audit:
327/546 (pivoting and planted elbow strikes) and 414/418 (tiptoe and flat-footed
gaze tasks). Their media and truthful floor classifications are unchanged.
All four revised placements passed actual-workout review. Revision 73 discards
obsolete cached rounds for those IDs while preserving scores and valid Keeps
on Android and web. The two elbow variants retain one session-movement identity.

The [current deficit ledger](catalog-audit/modifier_coverage_deficits_current.json)
records every failing profile and group. CI must reject every live deficit;
refreshing this diagnostic ledger never waives the rule. Validation remains
quadratic, without an all-modifier power set. The
[2026-08-29 deficit report](catalog-audit/modifier_coverage_deficits_2026-08-29.json)
remains the historical pre-slipperiness baseline.
