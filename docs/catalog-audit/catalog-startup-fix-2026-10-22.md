# Existing-installation startup failure

Debug 3.35.5 bundled catalog revision 89 but retained SQLite database version
104 from revision 88. Existing installations therefore kept their 884-record
catalog. Loading the new reaction plan for exercise 1621 failed before startup
completed, producing `Nomadic Method is unavailable.` No workout-state migration
or exercise-score update ran after that error.

Debug 3.35.6 advances the database version to 105 using the existing
non-destructive catalog synchronization. Database versions now derive from
`CurrentCatalogRevision` plus the historical schema offset (16), so a future
catalog revision cannot silently leave SQLite at the prior catalog. Schema-only
upgrades must increase that offset. Web already loads the complete bundled
catalog directly and needs no equivalent SQLite migration.

The regression test reproduces the failed reaction-plan dependency against the
884-record catalog and requires version-104 installations to take the supported
upgrade path. On-device verification must additionally confirm all 884 existing
rows and scores are unchanged, all six additions are present, and saved workout
state changes only by its catalog revision before treating the update as fixed.
