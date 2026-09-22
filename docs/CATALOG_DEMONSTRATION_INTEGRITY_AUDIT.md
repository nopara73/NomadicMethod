# Catalog demonstration-to-metadata integrity audit

Audit date: 2026-08-29

This is a complete review of the **479-record pre-audit catalog**, not a
keyword scan or a review limited to known examples. The final packaged Android
media was treated as authoritative. Every retained record was checked against
the complete decoded loop for name, action, muscular demand, meaningful
anatomy, repetition/hold behavior, side and direction behavior, sequence
membership, crop, start/end content, loop seam, playback speed, mirroring,
travel, hold frame, equipment, and silent copyability. Android remains the
media source of truth for the web build.

The review used deterministic whole-loop contact sheets with ten samples from
the first through final decoded frame for all records, denser 25-frame sheets
and direct high-resolution adjudication for questionable records, plus the
catalog media validators for duration, dimensions, codec, audio removal,
duplicate renders, holds, sequence transforms, and packaged inventory.

- Baseline catalog SHA-256:
  `25110c03c70a5a133d0f847e14ece3c1de043c852766e8a1ad8e28baa95cc903`
- Corrected catalog SHA-256:
  `34337795a293048e78fcf05a7ef21ccbf16dc95cb0e9b5f7dacecce099179426`
- Complete row-level ledger:
  [`catalog-audit/demonstration_metadata_integrity_2026-08-29.csv`](catalog-audit/demonstration_metadata_integrity_2026-08-29.csv)
- Exact remaining modifier deficits:
  [`catalog-audit/modifier_coverage_deficits_2026-08-29.json`](catalog-audit/modifier_coverage_deficits_2026-08-29.json)

## Outcome

| Result | Count | Meaning |
| --- | ---: | --- |
| Individually reviewed | 479 | Every baseline record has one ledger row |
| Pass | 349 | No metadata or media correction required |
| Corrected and retained | 126 | Stable ID and user score preserved |
| Retired | 4 | Demonstration could not satisfy the exercise/equipment contract |
| Final retained catalog | 475 | 417 motion loops and 58 still endpoints |
| Renamed | 12 | Name now states the demonstrated action |
| `muscularDemand` reclassified | 3 | One `1 -> 0`; two `2 -> 1` |
| Primary muscle corrected | 40 | Primary now follows the visible mechanical action |
| Any muscle association corrected | 124 | Includes primary and/or secondary changes |
| Movement-structure corrected | 3 | One repetition-to-hold repair and two sequence/session-link repairs |
| Source demonstration replaced | 0 | No approximate media substitution was admitted |
| Packaged presentation corrected | 1 | ID 95 now uses its demonstrated hold endpoint |
| Runtime media retired | 4 | IDs 267, 553, 558, and 559 |

### Muscular-demand transitions

| Previous | Current | Count | IDs |
| ---: | ---: | ---: | --- |
| `1` | `0` | 1 | 556 |
| `2` | `1` | 2 | 193, 417 |

The final distribution is **122** demand-0, **217** demand-1, and **136**
demand-2 exercises. These are audit results; no category total was targeted.

## Corrections that changed an exercise identity description

- **95:** the loop holds a raised knee on one leg. It is now
  `Single-Leg Knee-Raise Hold`, a still hold with a reviewed 60% endpoint,
  rather than repeated pelvic control.
- **193:** the loop hinges from a wide stance and reaches overhead without a
  squat. It is now `Wide-Stance Floor-to-Overhead Reach`, demand 1, with
  posterior-chain/reach anatomy.
- **417:** the loop performs a narrow-stance overhead-to-floor reach without a
  thumb target. The inherited thumb-tracking and hard-squat identity was
  removed; demand is now 1.
- **556:** both heels remain down. The entry is now
  `Standing Fist Clench and Release`, demand 0, with hand/forearm anatomy.
- **561/562:** generic or inaccurate ballet labels now state the visible
  tiptoe-running/spotting and calf-raise/arm-sweep actions.
- **564/565/566/581/582:** silent footage cannot establish a voluntary
  pelvic-floor contraction. Their names and anatomy now describe the visible
  calf-raise mechanics; 565 specifically performs bent-knee calf raises while
  holding a mini squat and reaching forward.
- **615:** the name now states the demonstrated alternating hamstring curls.

The ledger records the exact before/after metadata and rationale for all 126
retained corrections, including association-only changes.

## Systemic anatomy findings

The largest defect class was inherited association inflation:

- Gross squats, marches, lunges, reaches, pivots, and arm patterns had often
  been classified primarily as breathing or gaze work even though the visible
  movement supplied the mechanical demand. Those actions now own the primary;
  breathing, neck, or visual-motor involvement remains secondary only when
  materially demonstrated.
- Large cardio, boxing, ballet, and calf records accumulated long lists of
  adjacent muscles without a visible reason. Secondary lists were reduced to
  meaningful contributors rather than preserving historical breadth.
- Pelvic-floor claims were removed when they depended on unverifiable silent
  instruction. Pelvic-floor involvement remains a secondary association for
  real gait, running, jumping, hopping, or single-leg support where published
  measurements support reflex activity. Relevant evidence includes studies of
  [jumping](https://pubmed.ncbi.nlm.nih.gov/29525943/),
  [walking and jogging](https://pmc.ncbi.nlm.nih.gov/articles/PMC9279930/), and
  [impact activity](https://pubmed.ncbi.nlm.nih.gov/28884367/).

No one movement is counted repeatedly merely because several related muscles
could be named; the catalog retains only meaningful canonical associations.

## Structure and media findings

- ID 95 changed from a moving repetition presentation to the actual static
  side-by-side hold protocol.
- The false ID 267 calf hold was removed from root 252's mandatory sequence,
  and the obsolete session-movement link on ID 253 was removed.
- No side-direction metadata or direction order required correction after full
  review. ID 615 required a naming clarification, not a timing change: its
  existing alternating protocol was already correct.
- No retained non-locomotor loop travels improperly, and all nine directional
  media variants remain coherent with their direction metadata.
- IDs 553, 558, and 559 were retired because the packaged demonstrations
  visibly require dumbbells or a ballet barre. ID 267 was retired because its
  claimed heel raise never occurs and the remaining T-arm hold is duplicative.
- No retained source demonstration needed replacement. ID 95 alone required a
  packaging/presentation correction; the four retired IDs had their runtime
  videos, GIFs, and obsolete hold frame removed.

## Migration and persistence

The catalog revision is **52** and the Android SQLite schema version is **71**.
Revision 52 invalidates prepared workout placements for exactly the 130
affected IDs while preserving persisted scores for retained identities. The
four retired IDs are removed during catalog reconciliation. Unaffected keeps,
downvotes, history, recovery timestamps, and lineups remain untouched.

## Genuine deficits left visible

Correcting false anatomy exposed real catalog coverage debt. It was not hidden
by inventing muscle associations, promoting mirror relationships, or weakening
the validator functions:

- **178 pairwise state/group deficiencies** across **19 distinct group IDs**;
- **112 hard-floor category deficiencies** across **44 distinct group IDs**;
- **1 materiality deficiency:** with Hard Floor enabled, Silence excludes 16
  exercises where the current threshold is 17, although it affects 19 groups;
- **0 mirror-category deficiencies**;
- **0 distinct-lineup deficiencies** for supported workout profiles.

The exact records, minute resolutions, modifier states, group names, actual
counts, and required counts are committed in the JSON deficit report. These
are catalog-growth targets only insofar as future independently worthwhile,
fully demonstrated exercises happen to close them. They are not permission to
admit filler or false anatomy.

## Reproducibility

- `tools/Generate-CatalogIntegrityContactSheets.py` renders deterministic
  whole-loop review sheets and a sampled-frame manifest.
- `tools/Generate-CatalogIntegrityCandidateSheet.ps1` renders denser candidate
  sheets for adjudication.
- `tools/Write-CatalogIntegrityAudit.ps1` reproduces the 479-row before/after
  ledger from the baseline and corrected catalogs.
- `web/scripts/write-catalog-integrity-deficits.mjs` runs the unchanged shared
  coverage validators and writes every detected deficit.

## Completed validation

- `tools/Generate-ExerciseCatalog.ps1 -OutputRoot NomadicMethod/Assets`: regenerated
  **475** records from the authoritative manifests.
- `tools/Write-CatalogIntegrityAudit.ps1`: reproduced all **479** baseline
  ledger rows and both pinned catalog hashes.
- `tools/Test-ExerciseVideos.ps1`: verified **475/475** MP4s, found no duplicate
  rendered-video hashes, and matched every hold frame to its reviewed target.
- Hard-floor, mirror, and session-movement audits: **384/91**, **5 + 5
  MirrorOnly / 27 + 50 BenefitsGreatly / 388 Agnostic**, and **15 families / 31
  records**, respectively.
- `dotnet test NomadicMethod.slnx --configuration Release --no-restore`: **526/526**
  Android tests passed.
- `npm test`: **189/189** web tests passed, including the synchronized source
  contract.
- `npm run build`: production output contains **556 files**, **475 exercises**,
  and **0 GIFs**.
- Explicit Android-to-web SHA-256 comparison matched the catalog plus all
  **475 videos**, **9 directional videos**, and **58 hold frames**. The shared
  source-contract lock covers **108 files** at
  `0c25b8c2e2c708231275045eaaab26501147a2d766298198d3a445188c77b01e`.
- Real-workout browser inspection covered corrected ID 475 and the then-current
  two-block treatment of ID 397. A later full-loop review established that 397's
  source clip already alternates both sides continuously, so the artificial
  mirrored second block and unsupported breathing wording were removed.

This audit does not deploy the web app or install an Android package.
