# Shared workout coverage

New workouts credit each complete selected movement to every existing anatomical group it meaningfully trains. Primary and reviewed secondary claims both count. Group definitions, catalog ratings, identities, scores and stored Keeps are unchanged.

The planner starts from a feasible preference-ranked lineup, transfers redundant coverage to a preferred retained movement only when an exact-duration allocation remains possible and whole-workout load does not worsen, then considers complete balanced replacements. Light, phase feedback, recovery and contextual Keep preferences still apply.

New plans fill remaining time with distinct complete movements in available existing anatomical slots before repeating selected movements. Added slots use the same persisted cross-resolution mechanism as duration edits. Every distinct member of an added sequence must train at least half the slot's muscles. Existing selections are protected while each extra selection is fitted; phase feedback is then reconciled against the actual resulting schedule while preserving the placement partition and sequence lengths. The search preserves Light block opportunities and deduplicates session movement identities, including aliases.

If repeat sets are still needed, repeatable one-block sequences precede multi-block sequences, and the lowest set count wins within that tier. Phase feedback, projected regional/canonical workload and Keeps break subsequent ties. Workload balancing cannot give a favored exercise its fourth set before another eligible one-block exercise gets its second. All sides and directions remain atomic.

Distinct additions are planned before the balance pass. Balancing cannot shorten mandatory work to manufacture repeat time, and every replacement block must qualify for its actual scheduled group. Added-slot coverage and Light priorities remain constraints on balancing too.

The persisted ActiveCoverageAccountingVersion marker is set to 2 for new plans. Version 1 retains the original shared-coverage/load-first allocation; missing or zero markers retain the pre-shared-coverage behavior. Reopening never upgrades an unfinished plan. Current/completed selections, their sets, round IDs, history, score and Keep records survive restoration and explicit duration changes.

Android and web use the same shared-coverage fixtures across every taxonomy resolution; real-catalog regressions cover Heel Raise with Walking Arms, stored Keeps, exact duration and reopening.
