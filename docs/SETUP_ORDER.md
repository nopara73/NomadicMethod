# Workout setup order

New Android and web workouts prioritize finishing work at a physical station.
In a shared exercise space, returning to equipment can mean waiting for another
person or changing rooms again. Selection still uses the normal coverage,
Light, recovery, score and Keep rules; setup grouping arranges the resulting
complete selections without splitting sides, directions or allocated sets.

The scheduling policy reads every member of each atomic sequence. Its station
requirements combine wall (including soles), an available useful mirror,
elevation (including jump platforms), room/field locomotion, and a suitable
soft surface when hard-floor compatibility is absent. Floor contact beyond
the feet is a separate posture dimension. Enabled equipment does not by itself
place otherwise unrelated stations together.

Exact equipment requirements define contiguous groups, including equipment-free
work. Wall-only, mirror-only and combined wall/mirror work remain separate groups.
Only within the same equipment group may a less constrained selection share an
already required surface or travel space: hard-floor-compatible work may stay on
the softer surface; stationary and room work may stay in the larger travel area.
When several stations fit, prefer the current station, matching posture, fewer
extra requirements, then the earlier demand/anatomy rank. Equipment-free work
must never be absorbed into an equipment group and interleaved with its exercises.

Each equipment group gets one contiguous visit across all of its physical
stations, even when switching equipment would shorten the route. Replanning
finishes the current equipment group first when it has remaining work.
Each physical station gets one contiguous visit. Within it, standing and floor
work each get one contiguous run. Up to twelve stations, dynamic programming chooses the
route and each visit's posture orientation; beyond that a deterministic greedy
route bounds preparation time. Changing station costs 1,000 plus 100 per changed
requirement bit; a posture change costs 10. Initial posture is standing, with no
assumed initial equipment station. Remaining work after an edit begins from the
last protected selection's setup. Equal routes use demand 0, 2, 1 and anatomical
order. The algorithm never changes exercise metadata or training claims.

Persisted selection order takes precedence over routing, including after an
exercise shuffle or cold reopen. `ActiveSetupOrderVersion = 1` marks newly
prepared workouts. An older workout without a persisted order keeps the former
demand/anatomy fallback, including a pre-logging workout still at Ready. Explicit
duration or modifier changes can regroup only the remaining future selections;
the existing progress protection and duration-change history stay authoritative.
Phase-local selection and feedback use the final scheduled block positions.

Floor contacts describe the full demonstration, not exact start/end postures.
The first version therefore groups floor work coarsely; it does not invent
seated/lying/kneeling boundaries. Physical locations are inferred from actual
exercise requirements, not room maps. The app adds no setup control or prompt.

`web/tests/fixtures/setup-order-cases.json` is shared by native and web tests.
It covers station priority over demand, both floor states, compatible surfaces,
wall/mirror/elevation combinations, room/field travel, equipment-free work, linked
members, and replanning from the current station. Platform tests also cover
Ready/movement/rest restoration, pre-logging restoration, frozen order, duration
edits, modifier transitions and complete atomic sequences. Both routing strategies
also verify contiguous equipment groups across incompatible surface/travel needs.
