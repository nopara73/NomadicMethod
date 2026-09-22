# Nomadic Method rebrand

The product, repository, solution, projects, namespaces, resources, tooling,
and public web address use Nomadic Method. The current addresses are:

- Repository: https://github.com/nopara73/NomadicMethod
- Web app: https://nopara73.github.io/NomadicMethod/

The original flowing emblem and exercise catalog are unchanged.

## Upgrade compatibility

These identifiers are intentionally stable. They identify existing installations
or saved data; they are not product branding.

| Identifier | Why it stays |
| --- | --- |
| `com.local.flux` | Android must update the installed package in place. Changing it would install a second app with separate data and permissions. |
| `crc648a276c800321e548.MainActivity` | Preserve the existing Android launcher component when the managed namespace and assembly change. |
| `com.local.flux.RecoveryPrivacyActivity` | Preserve the registered Android health-permission activity. |
| `flux_workout_state` | Existing Android preferences, including the active workout and Keeps. |
| `flux_exercises.db` | Existing exercise database and persisted scores. |
| `flux.workout.state.v1` | Existing web workout data. Both web addresses share the same HTTPS origin and storage. |
| `FluxExerciseSourceCache` | Existing downloaded exercise-source cache and recorded source-file references. |

Exercise IDs, media bytes, workout serialization, and catalog revisions are not
changed by the rename. Original review evidence retains the names and filesystem
paths recorded at the time of review. Historical Git commits and already-created
worktrees are also left intact.

The old web path `/Flux/` redirects to `/NomadicMethod/` through the account's
Pages compatibility site. GitHub's repository redirect remains available because
no replacement repository is created under the old repository name.
