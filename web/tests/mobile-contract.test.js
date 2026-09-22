import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";

test("Oura is an explicit Android-only capability with a read-only private bridge", async () => {
  const root = new URL("../../", import.meta.url);
  const manifest = await readFile(new URL("NomadicMethod/AndroidManifest.xml", root), "utf8");
  assert.equal((manifest.match(/android.permission.health.READ_/g) ?? []).length, 3);
  assert.doesNotMatch(manifest, /android.permission.health.WRITE_|READ_HEALTH_DATA_IN_BACKGROUND/);
  const lock = await readFile(new URL("web/scripts/check-mobile-parity.mjs", root), "utf8");
  for (const file of ["NomadicMethod/MainActivity.Recovery.cs", "NomadicMethod/AndroidManifest.xml", "NomadicMethod/RecoveryPrivacyActivity.cs"])
    assert.ok(lock.includes(file));
  const native = await readFile(new URL("NomadicMethod/MainActivity.Recovery.cs", root), "utf8");
  assert.match(native, /NoBackupFilesDir/);
  assert.match(native, /HasOuraPermission \? _ouraCache\.Snapshot : null/);
  assert.match(manifest, /<package android:name="com\.ouraring\.oura"/);
  assert.match(native, /!_ouraCache\.PermissionRequestAttempted && _applicationStartupCompleted/);
  assert.match(native, /_appScreen == AppScreen\.Duration && _state\.ActiveWorkoutSession is null/);
  assert.match(native, /RequestPermissions\(OuraHealthConnectReader\.Permissions, OuraPermissionRequest\)/);
  assert.doesNotMatch(native, /AlertDialog|Toast|Disconnect|\.Enabled|oura_recovery_button/);
  const layout = await readFile(new URL("NomadicMethod/Resources/layout/screen_duration.xml", root), "utf8");
  assert.doesNotMatch(layout, /oura_recovery_button|ic_recovery_link/);
  const store = await readFile(new URL("NomadicMethod/Data/OuraRecoveryStore.cs", root), "utf8");
  assert.doesNotMatch(store, /bool Enabled|Disconnect/);
  const page = await readFile(new URL("web/index.html", root), "utf8");
  assert.doesNotMatch(page, /oura-recovery|oura-import|Connect Oura/);
});

import {
  ACCEPTED_COVERAGE_EXCEPTIONS,
  APPROVED_EXERCISE_CORRECTIONS,
  BROAD_COVERAGE_RESOLUTION_MINUTES,
  CURRENT_CATALOG_REVISION,
  CURRENT_WORKOUT_STATE_VERSION,
  DEFAULT_WORKOUT_MODIFIERS,
  EXERCISE_HARD_FLOOR_COMPATIBILITY,
  EXERCISE_SHY_COMPATIBILITY,
  EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT,
  EXERCISE_INSECT_COMPATIBILITY,
  EXERCISE_MIRROR_COVERAGE,
  HARD_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
  HARD_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
  HARD_MUSCULAR_DEMAND,
  HARD_RECOVERY_WINDOW_MS,
  HARD_ROTATION_STATUS,
  LAST_CUMULATIVE_CATALOG_REVISION,
  LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP,
  LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT,
  MINIMUM_LEGACY_HARD_PRIMARY_MUSCLES,
  MAXIMUM_MUSCULAR_DEMAND,
  MODERATE_MUSCULAR_DEMAND,
  MODERATE_RECOVERY_WINDOW_MS,
  MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
  MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
  MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS,
  MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS,
  MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
  MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT,
  MINIMUM_MODIFIER_MATERIALITY_PERCENT,
  MINIMUM_MUSCULAR_DEMAND,
  MINIMUM_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MINIMUM_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MODERATE_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MODERATE_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
  MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR,
  MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR,
  MUSCLE_BALANCE_MAX_REBALANCE_PASSES,
  MIRROR_EQUIPMENT,
  MOVEMENT_DURATION_MS,
  PREPARATION_DURATION_MS,
  RESOLUTIONS,
  REST_DURATION_MS,
  SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
  SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
  SUPPORTED_MINUTES,
  WORKOUT_MODIFIERS,
  WORKOUT_EXERCISE_PHASE,
  getMuscularDemandSchedulePriority,
  getWorkoutExercisePhase,
} from "../workout.js";

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(testDirectory, "..", "..");
const [
  sessionService,
  workoutState,
  workoutGroup,
  taxonomy,
  movementSchedule,
  mainActivity,
  webApp,
  preparationWorker,
  instantControls,
  workoutModule,
  catalogMigrationRules,
  catalogJson,
  workoutModifiers,
  exerciseModel,
  modifierPolicy,
  muscleBalancePolicy,
  recoveryPolicy,
  recoveryLightPolicy,
  lightDayPolicy,
  exerciseDatabase,
  catalogInvariantTests,
  exerciseDatabaseVersionPolicy,
  workoutSessionLog,
  workoutExercisePhase,
  durationLayout,
  workoutLayout,
  androidColors,
  androidStyles,
  strings,
  webIndex,
  webStyles,
  webBuild,
  mirrorEquipmentModel,
  wallEquipmentModel,
  mirrorCoverageModel,
  hardFloorCompatibilityModel,
  upperBodyClothingRequirementModel,
  shyCompatibilityModel,
  sequenceBlockModel,
  movementPresentationPolicy,
  workoutDisplayPolicy,
  workoutTimelineView,
  compactMirrorIcon,
  tallMirrorIcon,
  hardFloorIcon,
  softFloorIcon,
  upperBodyClothingIcon,
  shyIcon,
  lightWorkoutIcon,
  wallIcon,
  wallOffIcon,
  wallNoSoleIcon,
  atomicSequenceLineupSolver,
  workoutSequencePolicy,
  workoutSchedulePolicy,
  lightWorkoutCountdownBadge,
  lightCadenceModule,
] = await Promise.all([
  source("NomadicMethod", "Services", "ExerciseSessionService.cs"),
  source("NomadicMethod", "Models", "WorkoutState.cs"),
  source("NomadicMethod", "Models", "WorkoutGroup.cs"),
  source("NomadicMethod", "Services", "MassGroupingTaxonomy.cs"),
  source("NomadicMethod", "Services", "MovementPhaseSchedule.cs"),
  source("NomadicMethod", "MainActivity.cs"),
  source("web", "app.js"),
  source("web", "workout-preparation-worker.js"),
  source("web", "instant-controls.js"),
  source("web", "workout.js"),
  source("NomadicMethod", "Services", "CatalogMigrationRules.cs"),
  source("NomadicMethod", "Assets", "exercises.json"),
  source("NomadicMethod", "Models", "WorkoutModifiers.cs"),
  source("NomadicMethod", "Models", "Exercise.cs"),
  source("NomadicMethod", "Services", "WorkoutModifierPolicy.cs"),
  source("NomadicMethod", "Services", "WorkoutMuscleBalancePolicy.cs"),
  source("NomadicMethod", "Services", "WorkoutRecoveryPolicy.cs"),
  source("NomadicMethod", "Services", "WorkoutRecoveryLightPolicy.cs"),
  source("NomadicMethod", "Services", "WorkoutLightDayPolicy.cs"),
  source("NomadicMethod", "Data", "SqliteExerciseDatabase.cs"),
  source("NomadicMethod.Tests", "CatalogInvariantTests.cs"),
  source("NomadicMethod", "Data", "ExerciseDatabaseVersionPolicy.cs"),
  source("NomadicMethod", "Models", "WorkoutSessionLog.cs"),
  source("NomadicMethod", "Models", "WorkoutExercisePhase.cs"),
  source("NomadicMethod", "Resources", "layout", "screen_duration.xml"),
  source("NomadicMethod", "Resources", "layout", "screen_workout.xml"),
  source("NomadicMethod", "Resources", "values", "colors.xml"),
  source("NomadicMethod", "Resources", "values", "styles.xml"),
  source("NomadicMethod", "Resources", "values", "strings.xml"),
  source("web", "index.html"),
  source("web", "styles.css"),
  source("web", "scripts", "build.mjs"),
  source("NomadicMethod", "Models", "MirrorEquipment.cs"),
  source("NomadicMethod", "Models", "WallEquipment.cs"),
  source("NomadicMethod", "Models", "ExerciseMirrorCoverage.cs"),
  source("NomadicMethod", "Models", "ExerciseHardFloorCompatibility.cs"),
  source("NomadicMethod", "Models", "ExerciseUpperBodyClothingRequirement.cs"),
  source("NomadicMethod", "Models", "ExerciseShyCompatibility.cs"),
  source("NomadicMethod", "Models", "ExerciseSequenceBlock.cs"),
  source("NomadicMethod", "Services", "MovementPhasePresentationPolicy.cs"),
  source("NomadicMethod", "Services", "WorkoutDisplayPolicy.cs"),
  source("NomadicMethod", "WorkoutBlockTimelineView.cs"),
  source("NomadicMethod", "Resources", "drawable", "ic_mirror_compact.xml"),
  source("NomadicMethod", "Resources", "drawable", "ic_mirror_tall.xml"),
  binarySource("NomadicMethod", "Resources", "drawable-xxhdpi", "ic_hard_floor.png"),
  binarySource("NomadicMethod", "Resources", "drawable-xxhdpi", "ic_soft_floor.png"),
  source("NomadicMethod", "Resources", "drawable", "ic_upper_body_clothing.xml"),
  source("NomadicMethod", "Resources", "drawable", "ic_shy.xml"),
  binarySource("NomadicMethod", "Resources", "drawable-xxhdpi", "ic_light_workout.png"),
  source("NomadicMethod", "Resources", "drawable", "ic_wall.xml"),
  source("NomadicMethod", "Resources", "drawable", "ic_wall_off.xml"),
  binarySource("NomadicMethod", "Resources", "drawable-xxhdpi", "ic_wall_no_sole.png"),
  source("NomadicMethod", "Services", "AtomicSequenceLineupSolver.cs"),
  source("NomadicMethod", "Services", "WorkoutSequencePolicy.cs"),
  source("NomadicMethod", "Services", "WorkoutSchedulePolicy.cs"),
  source("NomadicMethod", "Resources", "drawable", "light_workout_countdown_badge.xml"),
  source("web", "light-cadence.js"),
]);
const catalog = JSON.parse(catalogJson);

test("accepted coverage exceptions have the exact same native and web conditions", () => {
  const nativeRules = [...modifierPolicy.matchAll(/new\("(r\d+\.[^"]+)", (WorkoutModifiers\.[^\r\n]+)\),/g)]
    .map(([, groupId, expression]) => ({
      groupId,
      requiredModifiers: expression.split("|").reduce((mask, term) =>
        mask | WORKOUT_MODIFIERS[term.trim().replace("WorkoutModifiers.", "")], 0),
    }));
  assert.deepEqual(nativeRules, ACCEPTED_COVERAGE_EXCEPTIONS);
  assert.equal(nativeRules.length, 7);
});

test("web duration choices match the mobile workout contract", () => {
  assert.deepEqual(
    SUPPORTED_MINUTES,
    integerArray(sessionService, "WorkoutMinutes"),
  );
  assert.deepEqual(
    [...RESOLUTIONS.keys()],
    integerArray(taxonomy, "SupportedMinutes"),
  );
});

test("web and mobile persist the same complete workout audit trail", () => {
  assert.equal(WORKOUT_MODIFIERS.Light, 256);
  assert.match(workoutModifiers, /Light\s*=\s*256/);
  for (const field of [
    "SessionId",
    "StartedAtUnixMilliseconds",
    "EndedAtUnixMilliseconds",
    "WorkoutMinutes",
    "Modifiers",
    "IsLightDay",
    "KeptExerciseIdsAtStart",
    "KeptExerciseRootIdsBySelectionGroupIdAtStart",
    "InitialSelections",
    "SelectionChanges",
    "Blocks",
    "Decisions",
  ]) {
    assert.match(workoutSessionLog, new RegExp(`\\b${field}\\b`));
  }
  assert.match(workoutState, /NextWorkoutSessionId[\s\S]*ActiveWorkoutSession[\s\S]*WorkoutHistory/);
  assert.match(workoutState, /ActiveWorkoutIsLightDay/);
  assert.match(workoutModule, /activeWorkoutIsLightDay/);
  assert.match(
    sessionService,
    /RecordCompletedWorkoutBlock[\s\S]*RecordWorkoutDecision[\s\S]*FinalizeActiveWorkoutSession/,
  );
  assert.match(
    workoutModule,
    /recordCompletedWorkoutBlock[\s\S]*recordWorkoutDecision[\s\S]*finalizeActiveWorkoutSession/,
  );
  assert.match(
    workoutModule,
    /workoutHistory[\s\S]*activeWorkoutSession[\s\S]*selectionChanges[\s\S]*blocks[\s\S]*decisions/,
  );
  assert.match(workoutSessionLog, /WorkoutExercisePhase ExercisePhase/);
  assert.match(workoutModule, /exercisePhase/);
  assert.equal(
    LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP,
    integerConstant(lightDayPolicy, "DailyRegularMinutesCap"),
  );
  assert.equal(
    LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT,
    integerConstant(lightDayPolicy, "RegularMinutesBeforeLightDay"),
  );
  assert.equal(globalThis.nomadicMethodLightCadence.dailyCap, LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP);
  assert.equal(globalThis.nomadicMethodLightCadence.threshold, LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT);
  assert.equal(globalThis.nomadicMethodLightCadence.fullCreditCompletionPercent,
    integerConstant(lightDayPolicy, "MinimumRegularCompletionPercentForFullCredit"));
  assert.equal(
    MINIMUM_LEGACY_HARD_PRIMARY_MUSCLES,
    integerConstant(lightDayPolicy, "MinimumLegacyHardPrimaryMuscles"),
  );
  assert.match(workoutState, /LegacyCompletedTrainingDayUnixMilliseconds/);
  assert.match(sessionService, /MigrateLegacyCompletedTrainingDays/);
  assert.match(workoutModule, /inferLegacyCompletedTrainingDays/);
  assert.match(sessionService, /ActiveWorkoutIsLightDay/);
  assert.match(sessionService, /IsLightDayDue/);
  assert.match(sessionService, /GetWorkoutsUntilLightDay/);
  assert.match(sessionService, /GetDefaultWorkoutModifiers/);
  assert.match(sessionService, /GetPersistentSetupModifiers/);
  assert.match(workoutModule, /getDefaultWorkoutModifiers/);
  assert.match(workoutModule, /getWorkoutsUntilLightWorkout/);
  assert.match(workoutModule, /getPersistentSetupModifiers/);
  assert.match(lightDayPolicy, /GetWorkoutsUntilLightDay/);
  assert.match(sessionService, /lightDayOpportunityWeight/);
  assert.match(sessionService, /DominantLightModeStateVersion\s*=\s*29/);
  assert.match(workoutModule, /DOMINANT_LIGHT_MODE_STATE_VERSION\s*=\s*26/);
  assert.match(sessionService, /MigrateActiveLightLineup/);
  assert.match(workoutModule, /migrateActiveLightLineup/);
  assert.match(
    workoutModule,
    /activeWorkoutIsLightDay[\s\S]*isLightWorkoutDayDue[\s\S]*lightDayOpportunityWeight/,
  );
});

test("locked Light explains taps without changing the workout on either platform", () => {
  assert.match(strings, /<string name="light_workout_locked_feedback">rest, you must<\/string>/);
  assert.match(webApp, /lightLocked: "rest, you must"/);
  assert.match(instantControls, /showFeedback\("rest, you must",/);
  const androidClick = mainActivity.slice(
    mainActivity.indexOf("_lightModifierButton.Click +="),
    mainActivity.indexOf("_wallModifierButton.Click +="),
  );
  assert.match(androidClick, /UpdateLightModifierPresentation[\s\S]*if \(_lightModifierLocked\)[\s\S]*ShowModifierFeedback\(Resource.String.light_workout_locked_feedback, GetRestFeedbackReason\(\)\);\s*return;\s*}\s*SetSelectedWorkoutModifier/);
  for (const source of [webApp, instantControls]) {
    assert.match(source, /"Training cycle complete" : "Muscles recovering"/);
    assert.match(source, /setAttribute\("data-detail", detail\)/);
  }
  assert.match(mainActivity, /RelativeSizeSpan\(0\.65f\)/);
  assert.match(mainActivity, /_lightModifierButton.Checked = effectivelyEnabled/);
  assert.match(mainActivity, /TooltipText = GetString\(_lightModifierLocked\s*\? Resource.String.light_workout_locked_feedback/);
});

test("work-based automatic Light is locked in both services and both startup surfaces", () => {
  assert.match(sessionService, /IsAutomaticLightDayDue/);
  assert.match(workoutModule, /isAutomaticLightDayDue/);
  for (const entry of ["PrepareWorkout", "ActivatePreparedWorkout", "ReconfigureActiveWorkout"]) {
    assert.match(sessionService, new RegExp(`${entry}[\\s\\S]*IsLightDayDue`));
  }
  for (const entry of ["prepareWorkout", "activatePreparedWorkout", "reconfigureActiveWorkout"]) {
    assert.match(workoutModule, new RegExp(`${entry}[\\s\\S]*is(?:LightWorkoutDayDue|AutomaticLightDayDue)`));
  }
  assert.match(mainActivity, /_lightModifierLocked = recoveryLightMode \|\| automaticLightMode/);
  assert.match(mainActivity, /_lightModifierButton.Enabled = true/);
  for (const source of [webApp, instantControls]) {
    assert.match(source, /const locked = recoveryLightMode \|\| automaticLightMode/);
    assert.match(source, /element.disabled = false;\s*element.setAttribute\("aria-disabled", String\(locked\)\)/);
  }
  assert.match(workoutModule, /import "\.\/light-cadence.js"/);
  assert.match(webIndex, /light-cadence.js[\s\S]*instant-controls.js/);
  assert.match(lightCadenceModule, /day < today && activity.hasCompletedLightWorkout/);
  assert.match(lightDayPolicy, /activity.HasCompletedLightWorkout && day < today/);
  assert.match(lightCadenceModule, /completedAtUnixMilliseconds[\s\S]*isLightAt/);
  assert.match(lightDayPolicy, /CompletedAtUnixMilliseconds[\s\S]*IsLightAt/);
  assert.match(webBuild, /lightCadenceOutputName[\s\S]*workoutOutputName/);
});

test("web and mobile persist hard-first block-aware workout allocation", () => {
  assert.match(workoutState, /HashSet<int> LastKeptExerciseIds/);
  assert.match(
    workoutState,
    /Dictionary<string, HashSet<int>>[\s\S]*KeptExerciseRootIdsBySelectionGroupId/,
  );
  assert.match(workoutState, /HashSet<string> ActiveExtraSetSelectionGroupIds/);
  assert.match(workoutState, /Dictionary<string, int> ActiveSetCountsBySelectionGroupId/);
  assert.match(
    sessionService,
    /OrderByDescending\(placement =>[\s\S]*GetSequenceExercises\(placement\.Root\)[\s\S]*Any\(WorkoutRecoveryPolicy\.IsHardExercise\)[\s\S]*ThenByDescending\(placement => IsSequenceKept\([\s\S]*state,[\s\S]*placement\.Anchor\.Id,[\s\S]*placement\.Root\)\)[\s\S]*ThenByDescending\(placement => placement\.Anchor\.Order\)/,
  );
  assert.match(
    workoutModule,
    /rightMembers\.some\(\(member\) =>[\s\S]*HARD_MUSCULAR_DEMAND[\s\S]*leftMembers\.some[\s\S]*isSequenceKept\(right\.anchor\.id, right\.root\)[\s\S]*isSequenceKept\(left\.anchor\.id, left\.root\)[\s\S]*right\.anchor\.order - left\.anchor\.order/,
  );
  assert.match(
    sessionService,
    /blockCostByGroup[\s\S]*SequenceBlocks\.Length[\s\S]*remainingMinutes[\s\S]*var fillable[\s\S]*repeatableCosts\.Any/,
  );
  assert.match(
    workoutModule,
    /blockCostByGroup[\s\S]*sequenceBlocks\.length[\s\S]*remainingMinutes[\s\S]*const fillable[\s\S]*repeatableCosts\.some/,
  );
  assert.match(
    sessionService,
    /GetLongWorkoutAllocationPlacementKey[\s\S]*hasPhaseScoreAdjustments[\s\S]*Warmup[\s\S]*PeakPerformance[\s\S]*Fatigued/,
  );
  assert.match(
    workoutModule,
    /getLongWorkoutAllocationPlacementKey[\s\S]*hasPhaseScoreAdjustments[\s\S]*Warmup[\s\S]*PeakPerformance[\s\S]*Fatigued/,
  );
  assert.match(
    sessionService,
    /KeepSequenceInSlot\(state, selectionGroup\.Id, root\)[\s\S]*rejectedSelections\.Add\(\(selectionGroup\.Id, root\.Id\)\)[\s\S]*SyncLegacyKeptExerciseIds\(state\)/,
  );
});

test("web and mobile carry keeps across duration resolutions", () => {
  assert.match(
    sessionService,
    /PrepareWorkout\([\s\S]*CarrySlotPreferencesForward\(state\);[\s\S]*RepairActiveLineup\(\s*state,\s*preserveCurrentSelections: !modifiers\.HasFlag\(\s*WorkoutModifiers\.Light\)\);/,
  );
  assert.match(
    sessionService,
    /CarrySlotPreferencesForward\([\s\S]*BuildCrossResolutionKeepPreferences[\s\S]*ChooseBestDistinctLineup/,
  );
  assert.match(
    workoutModule,
    /carrySlotPreferencesForward\(\)[\s\S]*buildCrossResolutionKeepPreferences[\s\S]*chooseBestDistinctLineup/,
  );
});

test("web and mobile scope rejection feedback to the same workout phases", () => {
  assert.equal(getWorkoutExercisePhase(15), WORKOUT_EXERCISE_PHASE.Warmup);
  assert.equal(getWorkoutExercisePhase(16),
    WORKOUT_EXERCISE_PHASE.PeakPerformance);
  assert.equal(getWorkoutExercisePhase(45),
    WORKOUT_EXERCISE_PHASE.PeakPerformance);
  assert.equal(getWorkoutExercisePhase(46), WORKOUT_EXERCISE_PHASE.Fatigued);
  assert.match(
    workoutExercisePhase,
    /WarmupFinalBlock\s*=\s*15[\s\S]*PeakPerformanceFinalBlock\s*=\s*45/,
  );
  assert.match(
    workoutState,
    /Dictionary<WorkoutExercisePhase, Dictionary<int, int>>[\s\S]*ExerciseScoreAdjustmentsByPhase/,
  );
  assert.match(workoutModule, /exerciseScoreAdjustmentsByPhase/);
  assert.match(
    sessionService,
    /SetActiveLongWorkoutAllocation\(state\);[\s\S]*ReconcileLineupWithScheduledPhases\(state\);/,
  );
  assert.match(
    sessionService,
    /ReconcileLineupWithScheduledPhases[\s\S]*GetScheduledPhaseByGroupId[\s\S]*scheduledPhaseByGroupId:\s*scheduledPhaseByGroupId/,
  );
  assert.match(
    workoutModule,
    /this\.setActiveLongWorkoutAllocation\(\);[\s\S]*this\.reconcileLineupWithScheduledPhases\(\);/,
  );
  assert.match(
    workoutModule,
    /reconcileLineupWithScheduledPhases[\s\S]*getScheduledPhaseByGroupId[\s\S]*scheduledPhaseByGroupId/,
  );
});

test("web and mobile apply the same multi-resolution muscle balancing", () => {
  assert.equal(
    MINIMUM_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
    integerConstant(muscleBalancePolicy, "MinimumPrimaryLoadEighthUnits"),
  );
  assert.equal(
    MINIMUM_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
    integerConstant(muscleBalancePolicy, "MinimumSecondaryLoadEighthUnits"),
  );
  assert.equal(
    MODERATE_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
    integerConstant(muscleBalancePolicy, "ModeratePrimaryLoadEighthUnits"),
  );
  assert.equal(
    MODERATE_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
    integerConstant(muscleBalancePolicy, "ModerateSecondaryLoadEighthUnits"),
  );
  assert.equal(
    HARD_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS,
    integerConstant(muscleBalancePolicy, "HardPrimaryLoadEighthUnits"),
  );
  assert.equal(
    HARD_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS,
    integerConstant(muscleBalancePolicy, "HardSecondaryLoadEighthUnits"),
  );
  assert.equal(
    MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR,
    integerConstant(muscleBalancePolicy, "MinimumBalancedShareNumerator"),
  );
  assert.equal(
    MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR,
    integerConstant(muscleBalancePolicy, "MinimumBalancedShareDenominator"),
  );
  assert.equal(
    MUSCLE_BALANCE_MAX_REBALANCE_PASSES,
    integerConstant(muscleBalancePolicy, "MaximumRebalancePasses"),
  );
  assert.match(
    sessionService,
    /RepairActiveLineup\(\s*state,\s*preserveCurrentSelections: !modifiers\.HasFlag\(\s*WorkoutModifiers\.Light\)\);[\s\S]*RebalanceNewExercisesByMuscleBalance\(state\);[\s\S]*SetActiveLongWorkoutAllocation\(state\);/,
  );
  assert.match(
    workoutModule,
    /this\.repairActiveLineup\(\s*\(modifiers & WORKOUT_MODIFIERS\.Light\) === 0,\s*\);[\s\S]*this\.rebalanceNewExercisesByMuscleBalance\(\);[\s\S]*this\.setActiveLongWorkoutAllocation\(\);/,
  );
  assert.match(
    muscleBalancePolicy,
    /MinimumMuscularDemand => MinimumPrimaryLoadEighthUnits[\s\S]*ModerateMuscularDemand => ModeratePrimaryLoadEighthUnits[\s\S]*MaximumMuscularDemand => HardPrimaryLoadEighthUnits/,
  );
  assert.match(
    muscleBalancePolicy,
    /MinimumMuscularDemand => MinimumSecondaryLoadEighthUnits[\s\S]*ModerateMuscularDemand => ModerateSecondaryLoadEighthUnits[\s\S]*MaximumMuscularDemand => HardSecondaryLoadEighthUnits/,
  );
  assert.match(
    workoutModule,
    /case MINIMUM_MUSCULAR_DEMAND:[\s\S]*MINIMUM_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS[\s\S]*case MODERATE_MUSCULAR_DEMAND:[\s\S]*MODERATE_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS[\s\S]*case HARD_MUSCULAR_DEMAND:[\s\S]*HARD_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS/,
  );
  assert.match(
    sessionService,
    /CalculateScheduledCanonicalLoadEighthUnits[\s\S]*WorkoutMuscleBalancePolicy\.AddExerciseLoad/,
  );
  assert.match(
    workoutModule,
    /calculateScheduledCanonicalLoadEighthUnits[\s\S]*addExerciseMuscleLoadEighthUnits/,
  );
  assert.match(
    sessionService,
    /currentBalance\.IsBalanced[\s\S]*removedPlacements\.Any\(placement => IsSequenceKept[\s\S]*GetCachedSelectionScore\(candidate, group\.Id\) >=[\s\S]*GetCachedSelectionScore\(displacedRoot, group\.Id\)/,
  );
  assert.match(
    workoutModule,
    /currentBalance\.isBalanced[\s\S]*removedPlacements\.some\(\(placement\) =>[\s\S]*this\.isSequenceKept[\s\S]*getCachedSelectionScore\(candidate, group\.id\) >=[\s\S]*getCachedSelectionScore\(displacedRoot, group\.id\)/,
  );
  assert.match(
    muscleBalancePolicy,
    /MassGroupingTaxonomy[\s\S]*SupportedMinutes[\s\S]*group\.CanonicalGroups\.Sum/,
  );
  assert.match(
    workoutModule,
    /\[\.\.\.RESOLUTIONS\]\.map[\s\S]*group\.canonicalGroups\.reduce/,
  );
  assert.match(workoutState, /HashSet<int> NextWorkoutExcludedExerciseIds/);
});

test("web and mobile persist one combined duration and modifier selection context", () => {
  assert.equal(WORKOUT_MODIFIERS.Insect, 1);
  assert.equal(WORKOUT_MODIFIERS.Silence, 2);
  assert.equal(WORKOUT_MODIFIERS.Mirror, 4);
  assert.equal(WORKOUT_MODIFIERS.TallMirror, 8);
  assert.equal(WORKOUT_MODIFIERS.HardFloor, 16);
  assert.equal(WORKOUT_MODIFIERS.Shy, 512);
  assert.equal(WORKOUT_MODIFIERS.Wall, 32);
  assert.equal(WORKOUT_MODIFIERS.SoleWallContact, 64);
  assert.equal(WORKOUT_MODIFIERS.UpperBodyClothing, 128);
  assert.deepEqual(MIRROR_EQUIPMENT, {
    None: "None",
    Compact: "Compact",
    Tall: "Tall",
  });
  assert.equal(
    DEFAULT_WORKOUT_MODIFIERS,
    WORKOUT_MODIFIERS.UpperBodyClothing |
      WORKOUT_MODIFIERS.HardFloor |
      WORKOUT_MODIFIERS.Silence,
  );
  assert.match(workoutModifiers, /Insect\s*=\s*1/);
  assert.match(workoutModifiers, /Silence\s*=\s*2/);
  assert.match(workoutModifiers, /Mirror\s*=\s*4/);
  assert.match(workoutModifiers, /TallMirror\s*=\s*8/);
  assert.match(workoutModifiers, /HardFloor\s*=\s*16/);
  assert.match(workoutModifiers, /Wall\s*=\s*32/);
  assert.match(workoutModifiers, /UpperBodyClothing\s*=\s*128/);
  assert.match(workoutModifiers, /Shy\s*=\s*512/);
  assert.match(instantControls, /shy:\s*512/);
  assert.match(webApp, /flag:\s*WORKOUT_MODIFIERS\.Shy/);
  assert.match(mirrorEquipmentModel, /None[\s\S]*Compact[\s\S]*Tall/);
  assert.match(mirrorCoverageModel, /None[\s\S]*UpperBody[\s\S]*FullBody/);
  assert.deepEqual(EXERCISE_HARD_FLOOR_COMPATIBILITY, {
    Unreviewed: "Unreviewed",
    Compatible: "Compatible",
    Incompatible: "Incompatible",
  });
  assert.match(
    hardFloorCompatibilityModel,
    /Unreviewed[\s\S]*Compatible[\s\S]*Incompatible/,
  );
  assert.deepEqual(EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT, {
    Unreviewed: "Unreviewed",
    ClothingRequired: "ClothingRequired",
    BareUpperBodyRequired: "BareUpperBodyRequired",
    Agnostic: "Agnostic",
  });
  assert.match(
    upperBodyClothingRequirementModel,
    /Unreviewed[\s\S]*ClothingRequired[\s\S]*BareUpperBodyRequired[\s\S]*Agnostic/,
  );
  assert.deepEqual(EXERCISE_SHY_COMPATIBILITY, {
    Unreviewed: "Unreviewed",
    Compatible: "Compatible",
    Incompatible: "Incompatible",
  });
  assert.match(
    shyCompatibilityModel,
    /Unreviewed[\s\S]*Compatible[\s\S]*Incompatible/,
  );
  assert.equal(CURRENT_WORKOUT_STATE_VERSION, 27);
  assert.match(workoutState, /public int Version[^=]*=\s*30/);
  assert.match(workoutState, /KeptExerciseRootIdsBySelectionGroupId/);
  assert.match(workoutState, /ExerciseScoreAdjustmentsBySelectionGroupId/);
  assert.match(workoutState, /ExerciseScoreAdjustmentsByPhase/);
  assert.match(workoutState, /PendingRestMillisecondsRemaining/);
  assert.match(workoutState, /PendingRestPausedByUser/);
  assert.match(workoutModule, /pendingRestMillisecondsRemaining/);
  assert.match(workoutModule, /pendingRestPausedByUser/);
  assert.match(
    workoutState,
    /LastWorkoutModifiers[^=]*=[\s\S]*WorkoutModifiers\.UpperBodyClothing\s*\|\s*[\s\S]*WorkoutModifiers\.HardFloor\s*\|\s*WorkoutModifiers\.Silence/,
  );
  assert.match(
    sessionService,
    /DefaultWorkoutModifiers\s*=[\s\S]*WorkoutModifiers\.UpperBodyClothing\s*\|\s*[\s\S]*WorkoutModifiers\.HardFloor\s*\|\s*WorkoutModifiers\.Silence/,
  );
  assert.match(workoutState, /WorkoutModifiers LastWorkoutModifiers/);
  assert.match(workoutState, /WorkoutModifiers ActiveWorkoutModifiers/);
  assert.match(workoutState, /ActiveModifierRetainedSelectionGroupIds/);
  assert.match(workoutModule, /activeModifierRetainedSelectionGroupIds/);
  assert.match(
    sessionService,
    /PrepareWorkout\([\s\S]*GetPersistentSetupModifiers\(modifiers\)[\s\S]*state\.ActiveWorkoutModifiers\s*=\s*modifiers;/,
  );
  assert.match(
    workoutModule,
    /prepareWorkout\(minutes, modifiers[\s\S]*getPersistentSetupModifiers\(modifiers\)[\s\S]*this\.state\.activeWorkoutModifiers\s*=\s*modifiers;/,
  );
  assert.match(
    sessionService,
    /ChooseBestDistinctLineup\([\s\S]*IsWorkoutSelectionCandidate\(\s*state,\s*exercise,\s*group,\s*modifiers,\s*groups\)/,
  );
  assert.match(exerciseModel, /int SessionMovementId/);
  assert.match(
    exerciseModel,
    /ExerciseHardFloorCompatibility HardFloorCompatibility/,
  );
  assert.match(
    exerciseModel,
    /ExerciseUpperBodyClothingRequirement UpperBodyClothingRequirement/,
  );
  assert.match(exerciseModel, /ExerciseShyCompatibility ShyCompatibility/);
  assert.match(
    modifierPolicy,
    /WorkoutModifiers\.HardFloor[\s\S]*HardFloorCompatibility[\s\S]*Compatible/,
  );
  assert.match(
    modifierPolicy,
    /WorkoutModifiers\.Shy[\s\S]*ShyCompatibility[\s\S]*Compatible/,
  );
  assert.match(
    modifierPolicy,
    /GetSessionMovementId[\s\S]*SessionMovementId > 0[\s\S]*exercise\.Id/,
  );
  assert.match(
    modifierPolicy,
    /GetMaximumDistinctLineupSize[\s\S]*WorkoutSequencePolicy\.GetPlacementOptions[\s\S]*GetSessionMovementId\(exercise\)[\s\S]*AtomicSequenceLineupSolver\.Solve/,
  );
  assert.match(
    sessionService,
    /ChooseBestDistinctLineup[\s\S]*GetSessionMovementId\(candidate\)[\s\S]*AtomicSequenceLineupSolver\.Solve/,
  );
  assert.match(
    workoutModule,
    /chooseBestDistinctLineup[\s\S]*getSessionMovementId\(candidate\)[\s\S]*solveAtomicSequenceLineup/,
  );
  assert.equal((sessionService.match(/unavailableMovementIds/g) ?? []).length, 2);
  assert.equal((workoutModule.match(/unavailableMovementIds/g) ?? []).length, 2);
  assert.match(
    modifierPolicy,
    /WorkoutCoveragePolicy\.IsSelectable\(exercise, group\)[\s\S]*IsCompatible\(exercise, profile\)/,
  );
  assert.match(
    modifierPolicy,
    /BroadCoverageResolutionMinutes\s*=\s*3[\s\S]*MinimumExercisesPerBroadPairStatePerGroup\s*=\s*1[\s\S]*MinimumExercisesPerFinePairStatePerGroup\s*=\s*1[\s\S]*FindPairwiseCoverageDeficiencies[\s\S]*FindMaterialityDeficiencies/,
  );
  assert.match(
    modifierPolicy,
    /MinimumExercisesPerMuscularDemandCategoryPerGroup\s*=\s*1[\s\S]*FindMuscularDemandCoverageDeficiencies[\s\S]*Exercise\.MinimumMuscularDemand[\s\S]*Exercise\.MaximumMuscularDemand/,
  );
  assert.doesNotMatch(modifierPolicy, /requiresMirrorRelevance/);
  assert.match(
    modifierPolicy,
    /GetRuleStateProfiles[\s\S]*WorkoutModifiers\.Mirror \| WorkoutModifiers\.TallMirror/,
  );
  assert.equal(
    BROAD_COVERAGE_RESOLUTION_MINUTES,
    integerConstant(modifierPolicy, "BroadCoverageResolutionMinutes"),
  );
  assert.equal(
    MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP,
    integerConstant(modifierPolicy, "MinimumExercisesPerBroadPairStatePerGroup"),
  );
  assert.equal(
    MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP,
    integerConstant(modifierPolicy, "MinimumExercisesPerFinePairStatePerGroup"),
  );
  assert.equal(
    MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
    integerConstant(
      modifierPolicy,
      "MinimumExercisesPerMuscularDemandCategoryPerGroup",
    ),
  );
  assert.equal(
    MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS,
    integerConstant(modifierPolicy, "MinimumWallRequiredSessionMovements"),
  );
  assert.equal(
    MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS,
    integerConstant(
      modifierPolicy,
      "MinimumSoleWallContactRequiredSessionMovements",
    ),
  );
  assert.match(
    modifierPolicy,
    /Rules[\s\S]*WorkoutModifiers\.UpperBodyClothing[\s\S]*SupportedModifierMask[\s\S]*WorkoutModifiers\.TallMirror \|[\s\S]*WorkoutModifiers\.Wall \|[\s\S]*WorkoutModifiers\.SoleWallContact/,
  );
  assert.doesNotMatch(
    modifierPolicy,
    /new\(\s*WorkoutModifiers\.Wall/,
  );
  assert.match(
    modifierPolicy,
    /FindWallRequiredCatalogDeficiencies[\s\S]*WallRequired[\s\S]*GetSessionMovementId[\s\S]*Distinct/,
  );
  assert.match(
    modifierPolicy,
    /FindSoleWallContactRequiredCatalogDeficiencies[\s\S]*SoleWallContactRequired[\s\S]*GetSessionMovementId[\s\S]*Distinct/,
  );
  assert.match(modifierPolicy, /IsMirrorMetadataReviewed/);
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
    integerConstant(modifierPolicy, "MinimumMaterialExercises"),
  );
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_PERCENT,
    integerConstant(modifierPolicy, "MinimumMaterialExercisePercent"),
  );
  assert.equal(
    MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT,
    integerConstant(modifierPolicy, "MinimumAffectedBucketPercent"),
  );
  assert.match(
    modifierPolicy,
    /FindCompleteLineupDeficiencies[\s\S]*GetMaximumCompleteLineupSize/,
  );
  assert.match(
    workoutModule,
    /MODIFIER_RULES[\s\S]*BROAD_COVERAGE_RESOLUTION_MINUTES\s*=\s*3[\s\S]*MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP\s*=\s*1[\s\S]*MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP\s*=\s*1[\s\S]*findWorkoutModifierPairCoverageDeficiencies[\s\S]*findWorkoutModifierMaterialityDeficiencies/,
  );
  assert.match(
    workoutModule,
    /MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP\s*=\s*1[\s\S]*findMuscularDemandCoverageDeficiencies[\s\S]*MINIMUM_MUSCULAR_DEMAND[\s\S]*MAXIMUM_MUSCULAR_DEMAND/,
  );
  assert.match(
    workoutModule,
    /matchingExerciseCount:[\s\S]*MODIFIER_RULES\.every\(\(rule\)\s*=>\s*rule\.isReviewed\(exercise\)\)/,
  );
  assert.doesNotMatch(workoutModule, /requiresMirrorRelevance/);
  assert.doesNotMatch(modifierPolicy, /1\s*<<\s*Rules\.Length/);
  assert.doesNotMatch(workoutModule, /1\s*<<\s*MODIFIER_RULES\.length/);
  assert.match(
    workoutModule,
    /findCompleteWorkoutProfileLineupDeficiencies[\s\S]*getMaximumCompleteLineupSize/,
  );
  for (const heavyCatalogInvariant of [
    "findWorkoutModifierPairCoverageDeficiencies",
    "findMuscularDemandCoverageDeficiencies",
    "findWorkoutModifierMaterialityDeficiencies",
    "findCompleteWorkoutProfileLineupDeficiencies",
  ]) {
    assert.match(webBuild, new RegExp(heavyCatalogInvariant));
    assert.doesNotMatch(webApp, new RegExp(heavyCatalogInvariant));
  }
  assert.match(webBuild, /failedCatalogInvariants[\s\S]*Catalog failed build-time invariants/);
  assert.match(
    webBuild,
    /hierarchical modifier-pair coverage"\s*,\s*pairwiseDeficiencies\.length === 0/,
  );
  assert.match(
    webBuild,
    /hierarchical hard-floor category coverage"\s*,[\s\S]*hardFloorCategoryDeficiencies\.length === 0/,
  );
  assert.doesNotMatch(webBuild, /muscularDemandDeficiencies\.length === 0/);
  assert.doesNotMatch(webBuild, /materialityDeficiencies\.length === 0/);
  assert.doesNotMatch(webBuild, /distinctLineupDeficiencies\.length === 0/);
  assert.match(webBuild, /exactlyEqual\(integrityDeficitReport\.materiality, materialityDeficiencies\)/);
  assert.doesNotMatch(webApp, /findMirrorCategoryDeficiencies/);
  assert.match(webApp, /findWallRequiredCatalogDeficiencies/);
  assert.match(webApp, /isModifierMetadataComplete/);
  assert.match(webBuild, /"complete workout lineups", completeLineupDeficiencies.length === 0/);
  assert.match(webApp, /isSessionMovementMetadataValid/);
  for (const heavyCatalogInvariant of [
    "FindPairwiseCoverageDeficiencies",
    "FindCompleteLineupDeficiencies",
  ]) {
    assert.match(catalogInvariantTests, new RegExp(heavyCatalogInvariant));
    assert.doesNotMatch(exerciseDatabase, new RegExp(heavyCatalogInvariant));
  }
  assert.match(
    catalogInvariantTests,
    /Assert\.Empty\(pairwiseDeficiencies\)[\s\S]*Assert\.Empty\(hardFloorCategoryDeficiencies\)[\s\S]*Assert\.Empty\(lineupDeficiencies\)/,
  );
  assert.doesNotMatch(exerciseDatabase, /FindMirrorCategoryDeficiencies/);
  assert.match(exerciseDatabase, /FindWallRequiredCatalogDeficiencies/);
  assert.match(
    exerciseDatabase,
    /FindSoleWallContactRequiredCatalogDeficiencies/,
  );
  assert.match(exerciseModel, /ExerciseInsectCompatibility InsectCompatibility/);
  assert.match(exerciseModel, /ExerciseMirrorRelationship MirrorRelationship/);
  assert.match(exerciseModel, /ExerciseMirrorCoverage MinimumMirrorCoverage/);
  assert.match(exerciseModel, /bool WallRequired/);
  assert.match(exerciseModel, /bool SoleWallContactRequired/);
  assert.match(
    wallEquipmentModel,
    /None\s*=\s*0[\s\S]*SolesStayOff\s*=\s*1[\s\S]*SolesMayTouch\s*=\s*2/,
  );
  assert.ok(catalog.every((exercise) => typeof exercise.silent === "boolean"));
  assert.ok(catalog.every((exercise) => typeof exercise.wallRequired === "boolean"));
  assert.ok(catalog.every((exercise) =>
    typeof exercise.soleWallContactRequired === "boolean"));
  assert.ok(catalog.every((exercise) =>
    !exercise.soleWallContactRequired || exercise.wallRequired));
  assert.equal(catalog.filter((exercise) => exercise.wallRequired).length, 35);
  assert.equal(catalog.filter((exercise) =>
    exercise.wallRequired && !exercise.soleWallContactRequired).length, 29);
  assert.deepEqual(
    new Set(catalog.filter((exercise) => exercise.soleWallContactRequired)
      .map((exercise) => exercise.id)),
    new Set([563, 564, 567, 568, 574, 633]),
  );
  assert.ok(catalog.every((exercise) =>
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible ||
    exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Incompatible));
  assert.ok(catalog.every((exercise) =>
    exercise.mirrorRelationship === "MirrorOnly" ||
    exercise.mirrorRelationship === "BenefitsGreatly" ||
    exercise.mirrorRelationship === "Agnostic"));
  assert.ok(catalog.every((exercise) =>
    exercise.mirrorRelationship === "MirrorOnly"
      ? exercise.equipment === "Mirror" &&
        (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
          exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody)
      : exercise.equipment === "None"));
  assert.ok(catalog.every((exercise) =>
    exercise.mirrorRelationship === "Agnostic"
      ? exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.None
      : exercise.minimumMirrorCoverage !== EXERCISE_MIRROR_COVERAGE.None));
  assert.ok(catalog.every((exercise) =>
    exercise.hardFloorCompatibility === "Compatible" ||
    exercise.hardFloorCompatibility === "Incompatible"));
  assert.ok(catalog.every((exercise) =>
    exercise.upperBodyClothingRequirement === "ClothingRequired" ||
    exercise.upperBodyClothingRequirement === "BareUpperBodyRequired" ||
    exercise.upperBodyClothingRequirement === "Agnostic"));
  assert.ok(catalog.every((exercise) =>
    exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Compatible ||
    exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Incompatible));
  assert.equal(catalog.filter((exercise) =>
    exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Compatible).length, 428);
  assert.equal(catalog.filter((exercise) =>
    exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Incompatible).length, 117);
  assert.deepEqual(
    new Set(catalog.filter((exercise) =>
      exercise.upperBodyClothingRequirement === "ClothingRequired")
      .map((exercise) => exercise.id)),
    new Set([134, 137, 165, 175, 579, 580, 801, 913]),
  );
  assert.deepEqual(
    new Set(catalog.filter((exercise) =>
      exercise.upperBodyClothingRequirement === "BareUpperBodyRequired")
      .map((exercise) => exercise.id)),
    new Set([524, 525, 526, 527, 528, 790]),
  );
  assert.match(
    webApp,
    /await ensureWorkoutPrepared\([\s\S]*nextSession\.activatePreparedWorkout\(\)/,
  );
  assert.match(durationLayout, /@\+id\/upper_body_clothing_modifier_button/);
  assert.match(durationLayout, /@\+id\/hard_floor_modifier_button/);
  assert.match(durationLayout, /@\+id\/insect_modifier_button/);
  assert.match(durationLayout, /@\+id\/silence_modifier_button/);
  assert.match(durationLayout, /@\+id\/shy_modifier_button/);
  assert.match(durationLayout, /@\+id\/light_workout_modifier_button/);
  assert.match(durationLayout, /@\+id\/wall_modifier_button/);
  assert.match(durationLayout, /@\+id\/mirror_modifier_button/);
  assert.match(durationLayout, /@drawable\/ic_mirror/);
  assert.ok(
    durationLayout.indexOf("@+id/upper_body_clothing_modifier_button") <
      durationLayout.indexOf("@+id/hard_floor_modifier_button"),
  );
  assert.ok(
    webIndex.indexOf('id="upper-body-clothing-modifier"') <
      webIndex.indexOf('id="hard-floor-modifier"'),
  );
  assert.ok(
    durationLayout.indexOf("@+id/hard_floor_modifier_button") <
      durationLayout.indexOf("@+id/insect_modifier_button"),
  );
  assert.ok(
    webIndex.indexOf('id="hard-floor-modifier"') <
      webIndex.indexOf('id="insect-modifier"'),
  );
  assert.ok(
    durationLayout.indexOf("@+id/silence_modifier_button") <
      durationLayout.indexOf("@+id/shy_modifier_button") &&
      durationLayout.indexOf("@+id/shy_modifier_button") <
      durationLayout.indexOf("@+id/duration_intensity_modifier_group") &&
      durationLayout.indexOf("@+id/duration_intensity_modifier_group") <
      durationLayout.indexOf("@+id/light_workout_modifier_button") &&
      durationLayout.indexOf("@+id/light_workout_modifier_button") <
      durationLayout.indexOf("@+id/duration_equipment_modifier_group") &&
      durationLayout.indexOf("@+id/duration_equipment_modifier_group") <
      durationLayout.indexOf("@+id/wall_modifier_button") &&
      durationLayout.indexOf("@+id/wall_modifier_button") <
      durationLayout.indexOf("@+id/mirror_modifier_button"),
  );
  assert.ok(
    webIndex.indexOf('id="silence-modifier"') <
      webIndex.indexOf('id="shy-modifier"') &&
      webIndex.indexOf('id="shy-modifier"') <
      webIndex.indexOf("modifier-intensity-group") &&
      webIndex.indexOf("modifier-intensity-group") <
      webIndex.indexOf('id="light-workout-modifier"') &&
      webIndex.indexOf('id="light-workout-modifier"') <
      webIndex.indexOf("modifier-equipment-group") &&
      webIndex.indexOf("modifier-equipment-group") <
      webIndex.indexOf('id="wall-modifier"') &&
      webIndex.indexOf('id="wall-modifier"') <
      webIndex.indexOf('id="mirror-modifier"'),
  );
  assert.match(durationLayout, /@drawable\/ic_wall_off/);
  assert.match(wallIcon, /<vector[\s\S]*pathData=/);
  assert.match(wallOffIcon, /<vector[\s\S]*pathData=/);
  assert.deepEqual(
    [...wallNoSoleIcon.subarray(0, 8)],
    [137, 80, 78, 71, 13, 10, 26, 10],
  );
  assert.notEqual(wallIcon, wallOffIcon);
  assert.match(
    mainActivity,
    /UpdateWallModifierPresentation[\s\S]*WallEquipment\.None\s*=>\s*Resource\.Drawable\.ic_wall_off[\s\S]*WallEquipment\.SolesStayOff\s*=>\s*Resource\.Drawable\.ic_wall_no_sole[\s\S]*WallEquipment\.SolesMayTouch\s*=>\s*Resource\.Drawable\.ic_wall/,
  );
  assert.match(
    webStyles,
    /data-wall-equipment="none"[\s\S]*wall-glyph-none[\s\S]*data-wall-equipment="soles-stay-off"[\s\S]*wall-glyph-soles-stay-off[\s\S]*data-wall-equipment="soles-may-touch"[\s\S]*wall-glyph-soles-may-touch/,
  );
  assert.match(
    webIndex,
    /wall-glyph-soles-stay-off[\s\S]*wall-no-sole-mask[\s\S]*wall-glyph-soles-may-touch[\s\S]*M3 4h18v16H3/,
  );
  assert.match(
    webStyles,
    /wall-no-sole-mask[\s\S]*mask-image:\s*url\("\.\/assets\/ic_wall_no_sole\.png"\)/,
  );
  assert.match(durationLayout, /@drawable\/ic_hard_floor/);
  assert.match(durationLayout, /@drawable\/ic_upper_body_clothing/);
  assert.match(durationLayout, /@drawable\/ic_shy/);
  assert.match(durationLayout, /@drawable\/ic_light_workout/);
  assert.match(upperBodyClothingIcon, /<vector[\s\S]*pathData=/);
  assert.match(shyIcon, /<vector[\s\S]*pathData=/);
  assert.match(shyIcon, /viewportWidth="32"[\s\S]*M16,0\.9C11\.43,0\.9/);
  assert.match(shyIcon, /android:scaleX="-1"[\s\S]*android:translateX="32"/);
  assert.match(webIndex, /viewBox="0 0 32 32"[\s\S]*M16 \.9C11\.43\.9/);
  assert.match(webIndex, /id="shy-hand"[\s\S]*translate\(32 0\) scale\(-1 1\)/);
  assert.deepEqual(
    [...lightWorkoutIcon.subarray(0, 8)],
    [137, 80, 78, 71, 13, 10, 26, 10],
  );
  assert.equal(lightWorkoutIcon.readUInt32BE(16), 96);
  assert.equal(lightWorkoutIcon.readUInt32BE(20), 96);
  assert.equal(lightWorkoutIcon[25], 6);
  assert.match(webIndex, /class="modifier-icon light-mode-glyph"/);
  assert.doesNotMatch(webIndex, /M20\.24 12\.24a6 6/);
  assert.match(
    webStyles,
    /\.light-mode-glyph[\s\S]*mask-image:\s*url\("\.\/assets\/ic_light_workout\.png"\)/,
  );
  assert.notDeepEqual(hardFloorIcon, softFloorIcon);
  assert.deepEqual([...hardFloorIcon.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  assert.deepEqual([...softFloorIcon.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
  assert.match(
    mainActivity,
    /UpdateHardFloorModifierPresentation[\s\S]*Resource\.Drawable\.ic_hard_floor[\s\S]*Resource\.Drawable\.ic_soft_floor/,
  );
  assert.match(webIndex, /class="floor-glyph floor-glyph-hard"/);
  assert.match(webIndex, /class="floor-glyph floor-glyph-soft"/);
  assert.match(webStyles, /data-hard-floor="hard"[\s\S]*floor-glyph-hard[\s\S]*data-hard-floor="soft"[\s\S]*floor-glyph-soft/);
  assert.match(webStyles, /mask-image:\s*url\("\.\/assets\/ic_hard_floor\.png"\)/);
  assert.match(webStyles, /mask-image:\s*url\("\.\/assets\/ic_soft_floor\.png"\)/);
  assert.match(webBuild, /drawable-xxhdpi[\s\S]*ic_hard_floor\.png[\s\S]*ic_soft_floor\.png[\s\S]*ic_wall_no_sole\.png[\s\S]*ic_light_workout\.png/);
  assert.match(webBuild, /fingerprintedName\([\s\S]*"ic_hard_floor"[\s\S]*"ic_soft_floor"[\s\S]*"ic_wall_no_sole"[\s\S]*"ic_light_workout"/);
  assert.match(
    durationLayout,
    /mirror_modifier_button(?:(?!\/>)[\s\S])*drawableTop="@drawable\/ic_mirror"/,
  );
  assert.doesNotMatch(
    durationLayout,
    /mirror_modifier_button(?:(?!\/>)[\s\S])*foreground="@drawable\/ic_mirror"/,
  );
  assert.match(
    mainActivity,
    /UpdateMirrorModifierPresentation[\s\S]*MirrorEquipment\.Compact\s*=>\s*Resource\.Drawable\.ic_mirror_compact[\s\S]*MirrorEquipment\.Tall\s*=>\s*Resource\.Drawable\.ic_mirror_tall[\s\S]*Resource\.Drawable\.ic_mirror/,
  );
  assert.doesNotMatch(mainActivity, /_mirrorModifierButton\.Text\s*=\s*equipment\s+switch/);
  assert.notEqual(compactMirrorIcon, tallMirrorIcon);
  assert.match(compactMirrorIcon, /pathData="M12,3\.5C7\.2,3\.5/);
  assert.match(tallMirrorIcon, /pathData="M8,2\.5H16/);
  assert.match(
    webStyles,
    /data-mirror-equipment="none"[\s\S]*mirror-glyph-none[\s\S]*data-mirror-equipment="compact"[\s\S]*mirror-glyph-compact[\s\S]*data-mirror-equipment="tall"[\s\S]*mirror-glyph-tall/,
  );
  assert.match(durationLayout, /@drawable\/ic_no_clap/);
  assert.doesNotMatch(
    durationLayout,
    /silence_modifier_button(?:(?!\/>)[\s\S])*foregroundTint=/,
  );
  assert.match(webIndex, /class="modifier-icon no-clap-icon"/);
  assert.match(webIndex, /viewBox="0 0 256 256"/);
  assert.match(webIndex, /Hands-clapping silhouette adapted from Phosphor Icons/);
  assert.match(webIndex, /class="no-clap-slash-cutout"/);
  assert.match(webIndex, /class="no-clap-slash"/);
  assert.match(webStyles, /\.no-clap-slash-cutout[\s\S]*stroke-width: 34/);
  assert.match(webStyles, /\.no-clap-slash[\s\S]*stroke-width: 18/);
  assert.doesNotMatch(webIndex, /class="modifier-icon shhh-icon"/);
  assert.doesNotMatch(durationLayout, /@drawable\/ic_volume_off/);
  assert.doesNotMatch(durationLayout, /@drawable\/ic_quiet_movement/);
  assert.match(strings, /<string name="silence_modifier_description">Quiet exercise filter<\/string>/);
  assert.match(webIndex, /id="insect-modifier"/);
  assert.match(webIndex, /id="upper-body-clothing-modifier"/);
  assert.match(webIndex, /id="hard-floor-modifier"/);
  assert.match(webIndex, /id="silence-modifier"/);
  assert.match(webIndex, /id="shy-modifier"/);
  assert.match(webIndex, /id="light-workout-modifier"/);
  assert.match(webIndex, /id="light-workout-countdown"/);
  assert.match(webIndex, /id="wall-modifier"/);
  assert.match(webIndex, /id="mirror-modifier"/);
  assert.match(webIndex, /class="mirror-glyph mirror-glyph-compact"/);
  assert.match(webIndex, /class="mirror-glyph mirror-glyph-tall"/);
  assert.doesNotMatch(webIndex, /mirror-mode-label/);
  assert.doesNotMatch(webApp, /mirrorModeLabel/);
  assert.match(webIndex, /Quiet exercise filter: quiet exercises only/);
  assert.match(webIndex, /aria-label="Workout intensity"/);
  assert.match(webIndex, /Workout intensity: regular workout/);
  assert.match(durationLayout, /@\+id\/duration_modifier_feedback/);
  assert.match(durationLayout, /@drawable\/duration_modifier_feedback_background/);
  assert.match(webIndex, /id="modifier-feedback"[\s\S]*role="status"[\s\S]*aria-live="polite"/);
  assert.match(strings, /<string name="insect_mode_enabled_feedback">insect mode ON<\/string>/);
  assert.match(strings, /<string name="insect_mode_disabled_feedback">insect mode OFF<\/string>/);
  assert.match(strings, /<string name="noisy_exercises_enabled_feedback">noisy exercises ENABLED<\/string>/);
  assert.match(strings, /<string name="noisy_exercises_disabled_feedback">noisy exercises DISABLED<\/string>/);
  assert.match(strings, /<string name="shy_mode_enabled_feedback">shy mode ON<\/string>/);
  assert.match(strings, /<string name="shy_mode_disabled_feedback">shy mode OFF<\/string>/);
  assert.match(strings, /<string name="shy_modifier_on">Less conspicuous exercises only<\/string>/);
  assert.match(webApp, /Shy mode: less conspicuous exercises only/);
  assert.match(instantControls, /Shy mode: less conspicuous exercises only/);
  assert.match(strings, /<string name="light_workout_enabled_feedback">light mode ON<\/string>/);
  assert.match(strings, /<string name="light_workout_disabled_feedback">light mode OFF<\/string>/);
  assert.match(strings, /<string name="wall_equipment_enabled_feedback">equipment ON: wall · no feet on wall<\/string>/);
  assert.match(strings, /<string name="wall_sole_contact_enabled_feedback">equipment ON: wall<\/string>/);
  assert.match(strings, /<string name="wall_equipment_disabled_feedback">equipment OFF: wall<\/string>/);
  assert.match(strings, /<string name="compact_mirror_equipment_enabled_feedback">equipment ON: compact mirror<\/string>/);
  assert.match(strings, /<string name="tall_mirror_equipment_enabled_feedback">equipment ON: tall mirror<\/string>/);
  assert.match(strings, /<string name="mirror_equipment_disabled_feedback">equipment OFF: mirror<\/string>/);
  assert.match(strings, /<string name="hard_floor_enabled_feedback">hard floor ON<\/string>/);
  assert.match(strings, /<string name="hard_floor_disabled_feedback">hard floor OFF<\/string>/);
  assert.match(strings, /<string name="hard_floor_modifier_on">Hard and slippery floor<\/string>/);
  assert.match(strings, /<string name="hard_floor_modifier_off">Stable soft floor<\/string>/);
  assert.match(strings, /<string name="upper_body_clothing_enabled_feedback">upper-body clothing ON<\/string>/);
  assert.match(strings, /<string name="upper_body_clothing_disabled_feedback">upper-body clothing OFF<\/string>/);
  assert.match(webIndex, /Floor surface: hard and slippery floor/);
  for (const label of [
    "upper-body clothing ON",
    "upper-body clothing OFF",
    "hard floor ON",
    "hard floor OFF",
    "insect mode ON",
    "insect mode OFF",
    "noisy exercises ENABLED",
    "noisy exercises DISABLED",
    "shy mode ON",
    "shy mode OFF",
    "light mode ON",
    "light mode OFF",
    "equipment ON: wall · no feet on wall",
    "equipment ON: wall",
    "equipment OFF: wall",
    "equipment ON: compact mirror",
    "equipment ON: tall mirror",
    "equipment OFF: mirror",
  ]) {
    assert.match(webApp, new RegExp(label));
  }
  assert.match(
    instantControls,
    /return `light mode \$\{enabled \|\| recoveryLightMode \|\| automaticLightMode \? "ON" : "OFF"\}`/,
  );
  assert.doesNotMatch(instantControls, /light workout \$\{enabled/);
  assert.match(mainActivity, /button\.TooltipText = GetString\([\s\S]*GetModifierFeedbackResourceId\(modifier, enabled\)/);
  assert.match(mainActivity, /GetMirrorFeedbackResourceId[\s\S]*MirrorEquipment\.Compact[\s\S]*compact_mirror_equipment_enabled_feedback[\s\S]*MirrorEquipment\.Tall[\s\S]*tall_mirror_equipment_enabled_feedback/);
  assert.match(mainActivity, /MirrorEquipment\.None\s*=>\s*MirrorEquipment\.Tall[\s\S]*MirrorEquipment\.Tall\s*=>\s*MirrorEquipment\.Compact[\s\S]*MirrorEquipment\.Compact\s*=>\s*MirrorEquipment\.None/);
  assert.match(mainActivity, /WallEquipment\.None\s*=>\s*WallEquipment\.SolesMayTouch[\s\S]*WallEquipment\.SolesMayTouch\s*=>\s*WallEquipment\.SolesStayOff[\s\S]*WallEquipment\.SolesStayOff\s*=>\s*WallEquipment\.None/);
  assert.match(durationLayout, /insect_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/insect_mode_disabled_feedback"/);
  assert.match(durationLayout, /upper_body_clothing_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/upper_body_clothing_enabled_feedback"/);
  assert.match(durationLayout, /hard_floor_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/hard_floor_enabled_feedback"/);
  assert.match(durationLayout, /silence_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/noisy_exercises_disabled_feedback"/);
  assert.match(durationLayout, /shy_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/shy_mode_disabled_feedback"/);
  assert.match(durationLayout, /light_workout_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/light_workout_disabled_feedback"/);
  assert.match(durationLayout, /wall_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/wall_equipment_disabled_feedback"/);
  assert.match(durationLayout, /mirror_modifier_button(?:(?!\/>)[\s\S])*tooltipText="@string\/mirror_equipment_disabled_feedback"/);
  assert.match(webApp, /showWorkoutModifierFeedback\(workoutModifierFeedbackLabel\(flag, enabled\)\)/);
  assert.match(webApp, /setAttribute\("title", workoutModifierFeedbackLabel\(flag, enabled\)\)/);
  assert.match(
    instantControls,
    /name === "hardFloor"[\s\S]*Floor surface: hard and slippery floor[\s\S]*Floor surface: stable soft floor/,
  );
  assert.match(
    instantControls,
    /readPersistedSetup[\s\S]*nomadicMethodLightCadence\.workoutsRemaining[\s\S]*nomadicMethodLightCadence\.isDue/,
  );
  assert.match(
    lightDayPolicy,
    /GetWorkoutsUntilLightDay[\s\S]*RegularMinutesBeforeLightDay - accumulatedMinutes[\s\S]*Dictionary<int, TrainingDayActivity>[\s\S]*ContainsKey\(today\)/,
  );
  assert.match(
    mainActivity,
    /UpdateLightModifierPresentation[\s\S]*GetWorkoutsUntilLightMode[\s\S]*ViewStates\.Gone[\s\S]*ViewStates\.Visible/,
  );
  assert.match(
    durationLayout,
    /light_workout_modifier_container[\s\S]*light_workout_modifier_button[\s\S]*light_workout_countdown_badge/,
  );
  assert.match(
    lightWorkoutCountdownBadge,
    /android:shape="rectangle"[\s\S]*android:radius="9dp"[\s\S]*brand_chartreuse[\s\S]*brand_graphite/,
  );
  assert.match(
    webApp,
    /renderLightModeCountdown[\s\S]*lightCountdown\.hidden = enabled/,
  );
  assert.match(
    instantControls,
    /lightWorkoutsRemaining[\s\S]*lightCountdown\.hidden = enabled/,
  );
  assert.match(
    webStyles,
    /\.light-mode-countdown[\s\S]*position:\s*absolute[\s\S]*border-radius:\s*999px[\s\S]*\.light-mode-countdown\[hidden\]/,
  );
  assert.match(
    instantControls,
    /renderBinaryModifier\(elements\.light, "light"\)/,
  );
  assert.match(webApp, /cycleMirrorEquipment[\s\S]*MIRROR_EQUIPMENT\.None[\s\S]*MIRROR_EQUIPMENT\.Tall[\s\S]*MIRROR_EQUIPMENT\.Compact/);
  assert.match(webApp, /cycleWallEquipment[\s\S]*WALL_EQUIPMENT\.None[\s\S]*WALL_EQUIPMENT\.SolesMayTouch[\s\S]*WALL_EQUIPMENT\.SolesStayOff/);
  assert.match(instantControls, /cycleMirrorEquipment[\s\S]*!hasMirror[\s\S]*mirror \| modifierFlags\.tallMirror[\s\S]*hasTallMirror[\s\S]*selectedModifiers \|= modifierFlags\.mirror/);
  assert.match(instantControls, /cycleWallEquipment[\s\S]*!hasWall[\s\S]*wall \| modifierFlags\.soleWallContact[\s\S]*solesMayTouch[\s\S]*selectedModifiers \|= modifierFlags\.wall/);
  assert.match(
    mainActivity,
    /ModifierFeedbackEnterDurationMilliseconds\s*=\s*140L[\s\S]*ModifierFeedbackHoldMilliseconds\s*=\s*1_200L[\s\S]*ModifierFeedbackFadeDurationMilliseconds\s*=\s*700L/,
  );
  assert.match(mainActivity, /SetDuration\(ModifierFeedbackEnterDurationMilliseconds\)/);
  assert.match(mainActivity, /SetDuration\(ModifierFeedbackFadeDurationMilliseconds\)/);
  assert.match(mainActivity, /PostDelayed\([\s\S]*ModifierFeedbackHoldMilliseconds/);
  assert.match(
    webApp,
    /MODIFIER_FEEDBACK_DURATION_MS\s*=\s*2_040[\s\S]*setTimeout\([\s\S]*MODIFIER_FEEDBACK_DURATION_MS/,
  );
  assert.match(
    webStyles,
    /\.modifier-feedback\.show[\s\S]*2040ms[\s\S]*@keyframes modifier-feedback-blink[\s\S]*7%[\s\S]*11%[\s\S]*66%[\s\S]*100%[\s\S]*opacity:\s*0[\s\S]*scale\(1\.08\)/,
  );
  assert.match(
    webIndex,
    /id="light-workout-modifier"[\s\S]{0,500}class="modifier-icon light-mode-glyph"/,
  );
  assert.doesNotMatch(webIndex, /M3\.27 2 2 3\.27/);
  assert.match(
    exerciseDatabase,
    /DatabaseVersion\s*=\s*ExerciseDatabaseVersionPolicy\.CurrentVersion/,
  );
  assert.match(exerciseDatabaseVersionPolicy, /CurrentVersion\s*=\s*95/);
  assert.match(
    exerciseDatabase,
    /ExerciseDatabaseVersionPolicy\.IsSupportedNonDestructiveUpgrade\([\s\S]*oldVersion,[\s\S]*newVersion/,
  );
  assert.match(exerciseDatabase, /CHECK \(silent IN \(0, 1\)\)/);
  assert.match(
    exerciseDatabase,
    /hard_floor_compatibility TEXT NOT NULL[\s\S]*Compatible[\s\S]*Incompatible/,
  );
  assert.match(
    exerciseDatabase,
    /upper_body_clothing_requirement TEXT NOT NULL[\s\S]*ClothingRequired[\s\S]*BareUpperBodyRequired[\s\S]*Agnostic/,
  );
  assert.match(
    exerciseDatabase,
    /shy_compatibility TEXT NOT NULL[\s\S]*Compatible[\s\S]*Incompatible/,
  );
  assert.match(
    exerciseDatabase,
    /values\.Put\(\s*"upper_body_clothing_requirement"/,
  );
  assert.match(exerciseDatabase, /values\.Put\("shy_compatibility"/);
  assert.match(
    exerciseDatabase,
    /muscular_demand INTEGER NOT NULL[\s\S]*CHECK \(muscular_demand BETWEEN 0 AND 2\)/,
  );
  assert.match(exerciseDatabase, /max_space_meters > 0 AND max_space_meters <= 2/);
  assert.match(exerciseDatabase, /equipment IN \('None', 'Mirror'\)/);
  assert.match(exerciseDatabase, /mirror_relationship TEXT NOT NULL/);
  assert.match(exerciseDatabase, /mirror_coverage TEXT NOT NULL/);
  assert.match(
    exerciseDatabase,
    /wall_required INTEGER NOT NULL DEFAULT 0[\s\S]*CHECK \(wall_required IN \(0, 1\)\)/,
  );
  assert.match(exerciseDatabase, /values\.Put\("wall_required"/);
  assert.match(
    exerciseDatabase,
    /session_movement_id INTEGER NOT NULL DEFAULT 0[\s\S]*CHECK \(session_movement_id >= 0\)/,
  );
  assert.match(exerciseDatabase, /values\.Put\("session_movement_id"/);
  assert.match(
    exerciseDatabase,
    /ScreenLeftLeadThenRightLead[\s\S]*ScreenRightLeadThenLeftLead/,
  );
});

test("muscular demand is a separate reviewed catalog score on both platforms", () => {
  assert.equal(MINIMUM_MUSCULAR_DEMAND, 0);
  assert.equal(MAXIMUM_MUSCULAR_DEMAND, 2);
  assert.match(exerciseModel, /MinimumMuscularDemand\s*=\s*0/);
  assert.match(exerciseModel, /MaximumMuscularDemand\s*=\s*2/);
  assert.match(exerciseModel, /int MuscularDemand/);
  assert.match(exerciseModel, /int Score/);
  assert.match(workoutModule, /hasReviewedMuscularDemand/);
  assert.ok(catalog.every((exercise) =>
    Number.isInteger(exercise.muscularDemand) &&
    exercise.muscularDemand >= MINIMUM_MUSCULAR_DEMAND &&
    exercise.muscularDemand <= MAXIMUM_MUSCULAR_DEMAND));
  assert.ok(catalog.every((exercise) => exercise.score === 0));
});

test("web and mobile schedule demand zero then two then one before muscle order", () => {
  assert.deepEqual(
    [
      MINIMUM_MUSCULAR_DEMAND,
      MAXIMUM_MUSCULAR_DEMAND,
      MODERATE_MUSCULAR_DEMAND,
    ].map(getMuscularDemandSchedulePriority),
    [0, 1, 2],
  );
  assert.match(
    workoutSchedulePolicy,
    /MinimumMuscularDemand\s*=>\s*0[\s\S]*MaximumMuscularDemand\s*=>\s*1[\s\S]*ModerateMuscularDemand\s*=>\s*2/,
  );
  assert.match(
    workoutSchedulePolicy,
    /GetSequenceMuscularDemand[\s\S]*SequenceBlocks[\s\S]*\.Max\(\)/,
  );
  assert.match(
    sessionService,
    /GetScheduleOrderedPlacements[\s\S]*GetSequenceMuscularDemand[\s\S]*ThenBy\(placement => placement\.Anchor\.Order\)/,
  );
  assert.match(
    sessionService,
    /CreateWorkoutSchedule[\s\S]*GetScheduleOrderedPlacements/,
  );
  assert.match(
    workoutModule,
    /orderSelectedSequencePlacementsForSchedule[\s\S]*getSequenceMuscularDemand[\s\S]*left\.anchor\.order - right\.anchor\.order/,
  );
  assert.match(
    workoutModule,
    /createWorkoutSchedule[\s\S]*orderSelectedSequencePlacementsForSchedule/,
  );
});

test("web and mobile apply rolling muscular recovery by primary muscle", () => {
  assert.equal(MODERATE_MUSCULAR_DEMAND, 1);
  assert.equal(HARD_MUSCULAR_DEMAND, MAXIMUM_MUSCULAR_DEMAND);
  assert.equal(MODERATE_RECOVERY_WINDOW_MS, 18 * 60 * 60 * 1000);
  assert.equal(HARD_RECOVERY_WINDOW_MS, 36 * 60 * 60 * 1000);
  assert.deepEqual(HARD_ROTATION_STATUS, {
    RecoveringHard: "RecoveringHard",
    Neutral: "Neutral",
    FreshHard: "FreshHard",
  });
  assert.match(
    recoveryPolicy,
    /ModerateMuscularDemand\s*=\s*Exercise\.ModerateMuscularDemand/,
  );
  assert.match(
    recoveryPolicy,
    /HardMuscularDemand\s*=\s*Exercise\.MaximumMuscularDemand/,
  );
  assert.match(recoveryPolicy, /ModerateRecoveryWindowMilliseconds[\s\S]*18L/);
  assert.match(recoveryPolicy, /HardRecoveryWindowMilliseconds[\s\S]*36L/);
  assert.match(
    workoutState,
    /Dictionary<string, long>[\s\S]*LastHardWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.match(
    workoutState,
    /Dictionary<string, long>[\s\S]*LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.doesNotMatch(workoutState, /LastKeptLocalDateByExerciseId/);
  assert.doesNotMatch(workoutState, /ActiveRecoveryExcludedExerciseIds/);
  assert.match(
    recoveryPolicy,
    /GetRotationStatus[\s\S]*IsPrimaryMuscleRecovering[\s\S]*HardExerciseRotationStatus\.FreshHard/,
  );
  assert.match(
    workoutModule,
    /getHardRotationStatus[\s\S]*isPrimaryMuscleRecovering[\s\S]*HARD_ROTATION_STATUS\.FreshHard/,
  );
  assert.match(
    recoveryPolicy,
    /IsModerateExerciseRecovering[\s\S]*IsPrimaryMuscleInModerateRecovery/,
  );
  assert.match(
    workoutModule,
    /isModerateExerciseRecovering[\s\S]*isPrimaryMuscleInModerateRecovery/,
  );
  assert.match(
    sessionService,
    /BeginRest\([\s\S]*RecordCompletedMuscularWork\([\s\S]*LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[\s\S]*LastHardWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.match(
    workoutModule,
    /beginRest\([\s\S]*lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[\s\S]*lastHardWorkUnixMillisecondsByPrimaryMuscle/,
  );
  assert.match(mainActivity, /_sessionService\.BeginRest\(/);
  assert.match(
    sessionService,
    /equipmentPreferenceWeight[\s\S]*moderateRecoveryAvoidanceWeight[\s\S]*hardRecoveryAvoidanceWeight[\s\S]*freshHardWeight[\s\S]*scoreWeight/,
  );
  assert.match(
    workoutModule,
    /equipmentPreferenceWeight[\s\S]*moderateRecoveryAvoidanceWeight[\s\S]*hardRecoveryAvoidanceWeight[\s\S]*freshHardWeight[\s\S]*scoreWeight/,
  );
  assert.match(
    sessionService,
    /var baseUtilities = new BigInteger\[groups\.Count, candidates\.Count\][\s\S]*var anchorUtilities = new BigInteger\[groups\.Count, candidates\.Count\][\s\S]*utilitiesByGroup/,
  );
  assert.match(
    workoutModule,
    /const baseUtilities = groups\.map\(\(\) => candidates\.map\(\(\) => 0n\)\)[\s\S]*const anchorUtilities = groups\.map\(\(\) => candidates\.map\(\(\) => 0n\)\)[\s\S]*utilitiesByGroup/,
  );
  assert.match(
    recoveryLightPolicy,
    /MinimumRecoveryShareNumerator\s*=\s*4[\s\S]*MinimumRecoveryShareDenominator\s*=\s*5/,
  );
  assert.match(
    recoveryLightPolicy,
    /Exercise\.ModerateMuscularDemand\s*=>/,
  );
  assert.match(recoveryLightPolicy, /IsPrimaryMuscle.*ModerateRecovery/);
  assert.match(
    recoveryLightPolicy,
    /Exercise\.MaximumMuscularDemand\s*=>/,
  );
  assert.match(recoveryLightPolicy, /IsPrimaryMuscleRecovering/);
  assert.match(
    sessionService,
    /GetRecoveryLightStatus[\s\S]*modifiers & ~WorkoutModifiers\.Light[\s\S]*IsCompatibleWithModifiers/,
  );
  assert.match(
    workoutModule,
    /evaluateRecoveryLightMode[\s\S]*modifiers & ~WORKOUT_MODIFIERS\.Light[\s\S]*isCompatibleWithWorkoutModifiers/,
  );
  assert.match(mainActivity, /_lightModifierLocked = recoveryLightMode \|\| automaticLightMode/);
  assert.match(webApp, /element\.setAttribute\("aria-disabled", String\(locked\)\)/);
});

test("runtime media and the deployable web shell are content-addressed", () => {
  assert.match(mainActivity, /SHA256\.HashData/);
  assert.match(mainActivity, /assetFingerprint/);
  assert.match(webApp, /data\/exercises\.json[\s\S]*data\/asset-versions\.json/);
  assert.doesNotMatch(webApp, /cache:\s*"no-store"/);
  assert.match(webApp, /assetVersions\[path\][\s\S]*searchParams\.set\("v", fingerprint\)/);
  assert.match(webBuild, /createHash\("sha256"\)/);
  assert.match(
    webBuild,
    /catalogVersion[\s\S]*assetVersionsVersion[\s\S]*exercises\.json\?v=\$\{catalogVersion\}[\s\S]*asset-versions\.json\?v=\$\{assetVersionsVersion\}/,
  );
  assert.match(webBuild, /fingerprintedName\("workout", "js"/);
  assert.match(webBuild, /fingerprintedName\("app", "js"/);
  assert.match(webBuild, /fingerprintedName\("styles", "css"/);
  assert.match(webBuild, /from \"\.\/\$\{workoutOutputName\}\"/);
  assert.match(webBuild, /replace\('\.\/styles\.css'/);
  assert.match(webBuild, /replace\('\.\/app\.js'/);
  assert.match(
    webIndex,
    /rel="preload" href="\.\/data\/exercises\.json"[\s\S]*rel="preload" href="\.\/data\/asset-versions\.json"/,
  );
});

test("movement and rest phases use pronounced accents across the surface, media, and actions", () => {
  for (const [name, value] of [
    ["move_surface", "#F0C7CC"],
    ["move_text", "#681E27"],
    ["move_accent", "#A42E3A"],
    ["move_track", "#D9959E"],
    ["rest_surface", "#CBE1F2"],
    ["rest_text", "#194F77"],
    ["rest_accent", "#2D6F9F"],
    ["rest_track", "#98C5E3"],
  ]) {
    assert.match(androidColors, new RegExp(`<color name="${name}">${value}<\\/color>`));
    assert.match(webStyles, new RegExp(`--${name.replaceAll("_", "-")}: ${value.toLowerCase()}`));
  }

  assert.doesNotMatch(mainActivity, /RenderSplitWorkoutPhase|SetWorkoutPhaseHalf/);
  assert.match(
    mainActivity,
    /ApplyMovementPhase[\s\S]*RenderFullWorkoutPhase\(Resource\.Color\.move_surface\)/,
  );
  assert.match(
    mainActivity,
    /SetExerciseMediaPhase[\s\S]*media_card_rest_background[\s\S]*media_card_move_background/,
  );
  assert.match(mainActivity, /phase_rest_chip_background/);
  assert.match(mainActivity, /phase_move_chip_background/);
  assert.match(
    androidStyles,
    /NomadicMethodKeepButton[\s\S]*@drawable\/rest_button_background/,
  );
  assert.match(
    workoutLayout,
    /<ImageButton[\s\S]*@\+id\/keep_button[\s\S]*@drawable\/ic_love[\s\S]*@color\/white/,
  );
  assert.match(
    workoutLayout,
    /<ImageButton[\s\S]*@\+id\/rest_playback_action[\s\S]*@string\/pause_rest_description[\s\S]*@drawable\/ic_phase_pause/,
  );
  assert.match(
    webIndex,
    /<button[\s\S]*id="toggle-rest"[\s\S]*data-state="playing"[\s\S]*rest-pause-icon[\s\S]*rest-play-icon/,
  );
  assert.match(mainActivity, /ToggleRestPlayback/);
  assert.match(mainActivity, /_sessionService\.PauseRest/);
  assert.match(mainActivity, /_sessionService\.ResumeRest/);
  assert.match(mainActivity, /UpdateRestPlaybackActionVisual/);
  assert.match(webApp, /toggleRestPlayback/);
  assert.match(webApp, /session\.pauseRest/);
  assert.match(webApp, /session\.resumeRest/);
  assert.match(webApp, /renderRestPlaybackToggle/);
  assert.match(
    mainActivity,
    /PendingRestKept[\s\S]*rest_button_background[\s\S]*SetColorFilter[\s\S]*Resource\.Color\.white/,
  );
  assert.match(
    webApp,
    /setFullPhaseSurface[\s\S]*setWorkoutPhaseClass\(kind\)[\s\S]*classList\.toggle\("phase-move"[\s\S]*classList\.toggle\("phase-rest"/,
  );
  assert.doesNotMatch(webApp, /setSplitPhaseSurface|activeScreenSide/);
  assert.match(
    webStyles,
    /\.workout-screen\.phase-move \.exercise-media-card[\s\S]*\.workout-screen\.phase-rest \.exercise-media-card[\s\S]*\.move-panel\.change \.media-control[\s\S]*\.keep-button/,
  );
});

test("Android landscape keeps controls left and the demonstration on the right", () => {
  const workoutLayoutMethod = methodBody(
    mainActivity,
    "private void ApplyWorkoutLayout(",
    "private void ApplyCompletionLayout(",
  );
  assert.match(
    mainActivity,
    /_workoutControlColumn = new LinearLayout\(this\)[\s\S]*Orientation = Orientation\.Vertical[\s\S]*LayoutDirection = LayoutDirection\.Ltr/,
  );
  assert.match(
    workoutLayoutMethod,
    /if \(landscape\)[\s\S]*MoveWorkoutView\(_workoutHeader, _workoutControlColumn, 0\)[\s\S]*MoveWorkoutView\(_workoutActionHost, _workoutControlColumn, 1\)[\s\S]*MoveWorkoutView\(_workoutControlColumn, _workoutInsetContent, 0\)[\s\S]*MoveWorkoutView\(_exerciseMediaArea, _workoutInsetContent, 1\)/,
  );
  assert.match(
    workoutLayoutMethod,
    /MoveWorkoutView\(_workoutHeader, _workoutInsetContent, 0\)[\s\S]*MoveWorkoutView\(_exerciseMediaArea, _workoutInsetContent, 1\)[\s\S]*MoveWorkoutView\(_workoutActionHost, _workoutInsetContent, 2\)/,
  );
  assert.match(
    webStyles,
    /\.workout-header[\s\S]*grid-column: 1[\s\S]*\.exercise-media-area[\s\S]*grid-column: 2[\s\S]*\.workout-action-host[\s\S]*grid-column: 1/,
  );
});

test("exercise names copy from a mobile long press on both platforms", () => {
  assert.match(
    workoutLayout,
    /android:id="@\+id\/exercise_name"[\s\S]*android:longClickable="true"/,
  );
  assert.match(
    strings,
    /copy_exercise_name_description">Long press to copy exercise name\.<\/string>/,
  );
  assert.match(
    mainActivity,
    /_exerciseName\.LongClick[\s\S]*CopyDisplayedExerciseName\(\)[\s\S]*eventArgs\.Handled = true/,
  );
  assert.match(
    mainActivity,
    /CopyDisplayedExerciseName\(\)[\s\S]*ClipboardManager[\s\S]*ClipData\.NewPlainText[\s\S]*FeedbackConstants\.LongPress/,
  );
  assert.match(
    webApp,
    /exerciseName\.addEventListener\([\s\S]*"pointerdown"[\s\S]*beginExerciseNameLongPress[\s\S]*"contextmenu"[\s\S]*copyExerciseNameFromContextMenu/,
  );
  assert.match(
    webApp,
    /Math\.hypot[\s\S]*EXERCISE_NAME_LONG_PRESS_MOVE_TOLERANCE_PX[\s\S]*navigator\.clipboard\?\.writeText[\s\S]*document\.execCommand\("copy"\)/,
  );
  assert.match(
    webStyles,
    /\.exercise-name \{[\s\S]*-webkit-touch-callout: none;[\s\S]*user-select: none;/,
  );
});

test("static exercises use the same user-facing mode label on both platforms", () => {
  assert.match(strings, /<string name="hold_badge">STATIC<\/string>/);
  assert.match(
    workoutLayout,
    /android:id="@\+id\/exercise_mode_badge"[\s\S]*android:text="@string\/hold_badge"/,
  );
  assert.match(
    mainActivity,
    /exercise\.Mode == ExerciseMode\.Hold \? "Static\." : "Repetition\."/,
  );
  assert.match(webIndex, /id="hold-badge"[^>]*>STATIC<\/span>/);
  assert.match(
    webApp,
    /exercise\.mode === "Hold" \? "Static" : "Repetition"/,
  );
});

test("literal work-block timelines and logical exercise progress match", () => {
  const fullNeckCircles = catalog.find((exercise) => exercise.id === 409);
  assert.equal(fullNeckCircles.name, "Full Neck Circles");
  assert.equal(fullNeckCircles.sequenceBlocks.length, 2);
  assert.match(workoutLayout, /@\+id\/execution_signifier/);
  assert.doesNotMatch(workoutLayout, /sequence_signifier_icon|set_signifier_icon/);
  assert.match(webIndex, /id="workout-progress-text"[\s\S]*id="execution-signifier"[\s\S]*id="exercise-name"[\s\S]*id="exercise-media-card"/);
  assert.match(webIndex, /id="execution-block-track"/);
  assert.doesNotMatch(webIndex, /id="execution-playhead"/);
  assert.doesNotMatch(webIndex, /sequence-signifier-icon|set-signifier-icon/);
  assert.doesNotMatch(webIndex, /unilateral-signifier|bidirectional-signifier/);
  assert.match(
    mainActivity,
    /WorkoutDisplayPolicy\.GetProgress[\s\S]*RenderExecutionTimeline\(\)/,
  );
  const androidTimeline = methodBody(
    mainActivity,
    "private void RenderExecutionTimeline(",
    "private void AnimateExerciseChange(",
  );
  assert.match(
    androidTimeline,
    /WorkoutDisplayPolicy\.GetTimeline[\s\S]*SetTimeline[\s\S]*Work block/,
  );
  assert.match(
    mainActivity,
    /ShowRestPanel\(\)[\s\S]*RenderExecutionTimeline\([\s\S]*selectUpcomingBlock/,
  );
  assert.match(
    workoutDisplayPolicy,
    /Distinct\(StringComparer\.Ordinal\)[\s\S]*GetTimeline[\s\S]*SetNumber != blocks[\s\S]*UsesThreeDistinctExercisePalette/,
  );
  assert.match(
    workoutDisplayPolicy,
    /SequenceBlockCount == 3[\s\S]*ExerciseOverrideId > 0[\s\S]*GetAccent\(group\) == WorkoutBlockAccent\.Neutral[\s\S]*Distinct\(\)[\s\S]*Count\(\) == 3/,
  );
  assert.match(
    workoutDisplayPolicy,
    /ScreenRight[\s\S]*Blue[\s\S]*ScreenLeft[\s\S]*Red[\s\S]*Neutral/,
  );
  assert.match(
    workoutTimelineView,
    /_setStartBlockIndices[\s\S]*for \(int setIndex = 0; setIndex < setCount; setIndex\+\+\)[\s\S]*DrawRoundRect[\s\S]*_currentBlockIndex[\s\S]*DrawPath/,
  );
  const webTimeline = methodBody(
    webApp,
    "function renderExecutionTimeline(",
    "function showReadyPanel()",
  );
  assert.match(
    webTimeline,
    /getWorkoutExecutionTimeline[\s\S]*setStartBlockIndices[\s\S]*execution-set[\s\S]*execution-work-block[\s\S]*classList\.add\("current"\)/,
  );
  assert.match(
    webApp,
    /getWorkoutDisplayProgress\([\s\S]*Exercise \$\{position\} of \$\{total\}/,
  );
  assert.match(
    workoutModule,
    /getWorkoutDisplayProgress[\s\S]*getWorkoutExecutionTimeline[\s\S]*usesThreeDistinctExercisePalette[\s\S]*getThreeDistinctExerciseAccent[\s\S]*getWorkoutBlockAccent/,
  );
  assert.match(webStyles, /\.execution-block-track[\s\S]*\.execution-set[\s\S]*grid-template-columns[\s\S]*\.execution-work-block\.blue[\s\S]*var\(--rest-accent\)[\s\S]*\.execution-work-block\.red[\s\S]*var\(--move-accent\)[\s\S]*\.execution-work-block\.neutral[\s\S]*var\(--chartreuse\)/);
  assert.match(webStyles, /\.execution-work-block\.current::before[\s\S]*border-top: 7px solid var\(--graphite\)/);
  assert.doesNotMatch(workoutLayout, /side_phase_label|countdown_phase_icon/);
  assert.doesNotMatch(mainActivity, /_sidePhaseLabel|_countdownPhaseIcon|GetMovementCueIcon/);
  assert.doesNotMatch(webIndex, /side-phase-label|movement-cue|BIDIRECTIONAL|UNILATERAL/);
  assert.doesNotMatch(
    webApp,
    /sidePhaseLabel|elements\.movementCue|function cueSymbol|BIDIRECTIONAL|UNILATERAL/,
  );
  assert.doesNotMatch(webStyles, /\.side-phase-label|\.movement-cue/);
  assert.doesNotMatch(workoutLayout, /two_sided_badge|ic_two_sides/);
  assert.doesNotMatch(mainActivity, /_twoSidedBadge/);
  assert.doesNotMatch(webIndex, /two-sided-badge|BOTH SIDES/);
  assert.doesNotMatch(webApp, /twoSidedBadge/);
  assert.doesNotMatch(webStyles, /\.two-sided-(?:badge|icon)/);
});

test("Android device builds embed their managed assemblies", async () => {
  const project = await source("NomadicMethod", "NomadicMethod.csproj");
  assert.match(project, /<EmbedAssembliesIntoApk>true<\/EmbedAssembliesIntoApk>/);
});

test("duration controls do not wait for catalog startup on either platform", () => {
  const mobileCreate = methodBody(
    mainActivity,
    "protected override void OnCreate(Bundle? savedInstanceState)",
    "private async Task InitializeApplicationAsync()",
  );
  const mobileStart = methodBody(
    mainActivity,
    "private async void StartSelectedWorkout()",
    "private static void RecoverPendingScoreUpdate(",
  );
  assert.match(
    mobileCreate,
    /_stateStore\s*=\s*new SharedPreferencesWorkoutStateStore[\s\S]*_state\s*=\s*_stateStore\.Load\(\)[\s\S]*ShowDurationSelection\(\)[\s\S]*InitializeApplicationAsync\(\)/,
  );
  assert.doesNotMatch(mobileCreate, /new SqliteExerciseDatabase|\.Exercises/);
  assert.match(
    mainActivity,
    /Task\.Run\(\(\) => InitializeApplication\(context\)\)[\s\S]*CompleteApplicationStartup/,
  );
  assert.match(
    mobileStart,
    /!_applicationStartupCompleted[\s\S]*_startWorkoutWhenReady\s*=\s*true[\s\S]*_beginWorkoutButton\.Enabled\s*=\s*false/,
  );

  assert.match(
    webIndex,
    /id="begin-workout"(?:(?!disabled)[\s\S])*?<\/button>/,
  );
  assert.match(webIndex, /<script src="\.\/instant-controls\.js"><\/script>/);
  assert.match(
    instantControls,
    /nomadic-method-controls-ready[\s\S]*durationDecrease|duration-decrease[\s\S]*requestStart/,
  );
  assert.match(
    instantControls,
    /startQueued\s*=\s*true[\s\S]*elements\.begin\.disabled\s*=\s*true[\s\S]*startRequested/,
  );
  assert.match(
    webApp,
    /startupControls\.connect[\s\S]*startRequested:\s*startWorkout[\s\S]*if \(!session\)[\s\S]*startWorkoutWhenReady\s*=\s*true/,
  );
  assert.match(
    webBuild,
    /instant-controls\.js[\s\S]*instantControlsSource[\s\S]*<script>/,
  );
});

test("Start activates an off-thread prepared workout on Android and web", () => {
  assert.match(
    sessionService,
    /StartWorkout\([\s\S]*PrepareWorkout\(state, minutes, modifiers\);[\s\S]*ActivatePreparedWorkout\(state\);/,
  );
  assert.match(
    workoutModule,
    /startWorkout\(minutes, modifiers[\s\S]*this\.prepareWorkout\(minutes, modifiers\);[\s\S]*this\.activatePreparedWorkout\(\);/,
  );
  assert.match(
    mainActivity,
    /QueueWorkoutPreparation\([\s\S]*Task\.Run\([\s\S]*PrepareWorkout\([\s\S]*StartSelectedWorkout\([\s\S]*ActivatePreparedWorkout/,
  );
  assert.match(
    webApp,
    /queueWorkoutPreparation\([\s\S]*new Worker\([\s\S]*ensureWorkoutPrepared\([\s\S]*activatePreparedWorkout\(\)/,
  );
  assert.match(
    preparationWorker,
    /new WorkoutSession\(exercises, state\)[\s\S]*prepareWorkout\(minutes, modifiers\)[\s\S]*postMessage/,
  );
  assert.match(
    webBuild,
    /workout-preparation-worker\.js[\s\S]*preparationWorkerOutputName[\s\S]*fingerprintedPreparationWorkerSource/,
  );
});

test("Android persistence rejects malformed stored shapes without crashing launch", async () => {
  const stateStore = await source(
    "NomadicMethod",
    "Data",
    "SharedPreferencesWorkoutStateStore.cs",
  );
  assert.match(
    stateStore,
    /RootElement\.ValueKind != JsonValueKind\.Object[\s\S]*return new WorkoutState\(\)/,
  );
  assert.match(
    stateStore,
    /versionElement\.ValueKind != JsonValueKind\.Number[\s\S]*!versionElement\.TryGetInt32\(out version\)[\s\S]*return new WorkoutState\(\)/,
  );
  assert.match(stateStore, /catch \(JsonException\)/);
});

test("workout transport controls are functional and muscle labels stay hidden", async () => {
  const workoutLayout = await source("NomadicMethod", "Resources", "layout", "screen_workout.xml");
  const startControl = [...workoutLayout.matchAll(/<ImageButton[\s\S]*?\/>/g)]
    .map((match) => match[0])
    .find((control) => control.includes('android:id="@+id/start_button"')) ?? "";
  assert.match(startControl, /@drawable\/ic_phase_active/);
  assert.doesNotMatch(startControl, /android:text=/);
  assert.match(workoutLayout, /@\+id\/shuffle_button[\s\S]*@drawable\/ic_shuffle/);
  assert.match(workoutLayout, /@\+id\/repeat_action[\s\S]*@drawable\/ic_repeat/);
  assert.match(workoutLayout, /@\+id\/playback_action[\s\S]*@drawable\/ic_phase_pause/);
  assert.match(workoutLayout, /@\+id\/next_action[\s\S]*@drawable\/ic_next/);
  assert.doesNotMatch(workoutLayout, /workout_group_name|muscle_chip_background|skip_action/);
  assert.doesNotMatch(webIndex, /workout-group-name|skip-exercise|>\s*Start\s*</);
  assert.match(webIndex, /id="shuffle-exercise"[\s\S]*id="start-movement"/);
  assert.match(
    strings,
    /shuffle_exercise_description">Reject and replace this exercise</,
  );
  assert.match(
    webIndex,
    /id="shuffle-exercise"[\s\S]*aria-label="Reject and replace this exercise"[\s\S]*title="Reject and replace this exercise"/,
  );
  assert.doesNotMatch(webIndex, /Shuffle exercise \(-1 vote\)/);
  assert.match(webIndex, /id="start-movement"[\s\S]*start-playback-icon/);
  assert.match(webIndex, /id="repeat-exercise"[\s\S]*id="toggle-playback"[\s\S]*id="next-exercise"/);

  const mobilePause = methodBody(
    mainActivity,
    "private void TogglePlayback()",
    "private void RepeatExercise()",
  );
  const mobileRepeat = methodBody(
    mainActivity,
    "private void RepeatExercise()",
    "private void GoToNextExercise()",
  );
  const mobileNext = methodBody(
    mainActivity,
    "private void GoToNextExercise()",
    "private void CompleteCountdown()",
  );
  assert.match(mobilePause, /ResumeCountdown\(\)/);
  assert.match(mobilePause, /_countdownPausedByUser = true[\s\S]*PauseCountdown\(\)/);
  assert.match(mobileRepeat, /StopCountdownTimer\(\)[\s\S]*StartCountdownTimer\(GetCurrentCountdownDurationMilliseconds\(\)\)/);
  assert.doesNotMatch(mobileRepeat, /FinalizeCurrentRound|RecordOutcome/);
  assert.match(
    mobileNext,
    /RejectCurrentSequenceWithScoreUpdates[\s\S]*SaveStateAndScores/,
  );
  assert.match(mobileNext, /!_countdownActive && !_countdownPaused/);
  const mobileAvailability = methodBody(
    mainActivity,
    "private void SetPlaybackControlsAvailability(bool available)",
    "private static void SetPlaybackControlAvailability(",
  );
  assert.match(mobileAvailability, /_repeatAction, available/);
  assert.match(mobileAvailability, /_playbackAction, available/);
  assert.match(
    mobileAvailability,
    /_workoutPhase == WorkoutPhase\.Move[\s\S]*_countdownActive \|\| _countdownPaused[\s\S]*_nextAction, nextAvailable/,
  );
  const mobileShuffle = methodBody(
    mainActivity,
    "private void ShuffleCurrentExercise()",
    "private void StartCountdown()",
  );
  assert.match(mobileShuffle, /ShuffleNextExercise[\s\S]*SaveStateAndScores[\s\S]*ShowNextExercise/);
  assert.doesNotMatch(mobileShuffle, /FinalizeCurrentRound|RecordOutcome/);
  assert.match(mainActivity, /_repeatAction\.Click[\s\S]*_playbackAction\.Click[\s\S]*_nextAction\.Click/);

  const webPause = methodBody(
    webApp,
    "function toggleMovementPlayback()",
    "function repeatMovement()",
  );
  const webRepeat = methodBody(
    webApp,
    "function repeatMovement()",
    "function goToNextExercise()",
  );
  const webNext = methodBody(
    webApp,
    "function goToNextExercise()",
    "function completeMovement()",
  );
  assert.match(webPause, /resumeMovement\(\)/);
  assert.match(webPause, /pauseMovement\("user"\)/);
  assert.match(webRepeat, /getMovementCountdownDurationMs\(currentGroup\)[\s\S]*setMovementDeadline\(movementRemaining\)/);
  assert.doesNotMatch(webRepeat, /recordOutcome|persistState/);
  assert.match(webNext, /rejectCurrentSequence\(currentGroup\)[\s\S]*persistState/);
  assert.match(webNext, /!movementRunning && !movementPauseReason/);
  const webAvailability = methodBody(
    webApp,
    "function setPlaybackControlsEnabled(enabled)",
    "function renderPlaybackToggle()",
  );
  assert.match(webAvailability, /repeatExercise\.disabled = !enabled/);
  assert.match(webAvailability, /playbackToggle\.disabled = !enabled/);
  assert.match(
    webAvailability,
    /nextExercise\.disabled = !movementRunning && !movementPauseReason/,
  );
  const webShuffle = methodBody(
    webApp,
    "function shuffleCurrentExercise()",
    "function showMovePanel()",
  );
  assert.match(webShuffle, /shuffleNextExercise\(currentGroup\)[\s\S]*persistState\(\)[\s\S]*showNextExercise\(\)/);
  assert.doesNotMatch(webShuffle, /recordOutcome/);
  assert.match(webApp, /repeatExercise\.addEventListener[\s\S]*playbackToggle\.addEventListener[\s\S]*nextExercise\.addEventListener/);
  assert.match(
    workoutModule,
    /shuffleNextExercise\(group\)[\s\S]*getCompatibleShuffleCandidates[\s\S]*applyShuffleRejection\([\s\S]*selectionGroupId,[\s\S]*this\.getExercisePhase\(group\),[\s\S]*rejectedRoot,[\s\S]*scoreUpdates/,
  );
  assert.match(
    workoutModule,
    /applyShuffleRejection\(selectionGroupId, phase, rejectedRoot, exercises\)[\s\S]*downvoteSequenceInPhase\(phase, rejectedRoot\)/,
  );
  assert.match(
    sessionService,
    /ShuffleNextExercise\([\s\S]*ApplyShuffleRejection\([\s\S]*group\.SelectionKey,[\s\S]*GetExercisePhase\(group\),[\s\S]*rejectedRoot,[\s\S]*scoreUpdates[\s\S]*DownvoteSequenceInPhase/,
  );
  assert.match(
    sessionService,
    /Shuffle\(candidates\);[\s\S]*ShuffleCandidate selected = candidates\[0\]/,
  );
  assert.match(
    workoutModule,
    /this\.shuffle\(replacementCandidates\);[\s\S]*const selected = replacementCandidates\[0\]/,
  );
  assert.match(
    sessionService,
    /ChooseLongWorkoutAllocation\([\s\S]*startedSelectionGroupIds/,
  );
  assert.match(
    workoutModule,
    /chooseLongWorkoutAllocation\(startedSelectionGroupIds\)/,
  );
  assert.doesNotMatch(mainActivity, /_workoutGroupName/);
  assert.doesNotMatch(webApp, /workoutGroupName/);
});

test("mid-workout modifiers revalidate the active exercise on both platforms", () => {
  assert.match(
    workoutLayout,
    /@\+id\/workout_setup_button[\s\S]*@drawable\/ic_tune/,
  );
  assert.doesNotMatch(
    workoutLayout,
    /upper_body_clothing_modifier_button|hard_floor_modifier_button|insect_modifier_button|silence_modifier_button|shy_modifier_button|wall_modifier_button|mirror_modifier_button/,
  );
  assert.match(durationLayout, /@\+id\/duration_lock_icon[\s\S]*@drawable\/ic_lock/);
  assert.match(durationLayout, /@\+id\/duration_action_icon/);
  assert.match(webIndex, /id="workout-setup"[\s\S]*Change workout setup/);
  assert.match(webIndex, /class="duration-lock"/);

  const mobileSetup = methodBody(
    mainActivity,
    "private void ShowActiveWorkoutSetup()",
    "private void ConfigureDurationScreenForActiveWorkout(bool editing)",
  );
  assert.match(mobileSetup, /PauseCountdown\(\)/);
  assert.match(mobileSetup, /PauseRest[\s\S]*PauseRestCountdown\(\)/);
  assert.match(mobileSetup, /ShowAppScreen\(AppScreen\.Duration\)/);
  assert.match(mobileSetup, /_state\.ActiveWorkoutMinutes/);
  assert.match(mobileSetup, /QueueWorkoutPreparation\(\)/);
  const mobileRestore = methodBody(
    mainActivity,
    "private void RestoreWorkoutAfterSetup()",
    "private void SetSelectedWorkoutModifier(",
  );
  assert.match(
    mobileRestore,
    /RestorePendingRest[\s\S]*RestorePendingMovement[\s\S]*ShowNextExercise/,
  );
  assert.match(
    mobileRestore,
    /pendingGroup is not null[\s\S]*RestorePendingMovement[\s\S]*else[\s\S]*StopCountdownTimer\(\)[\s\S]*ShowNextExercise/,
  );
  assert.match(mainActivity, /ReconfigureActiveWorkout\([\s\S]*currentWorkoutGroupId/);

  const webSetup = methodBody(
    webApp,
    "function showActiveWorkoutSetup()",
    "function restoreWorkoutAfterSetup()",
  );
  assert.match(webSetup, /pauseMovement\("setup"\)/);
  assert.match(webSetup, /pauseRest\(currentGroup, remaining\)/);
  assert.match(webSetup, /showScreen\("duration"\)/);
  assert.match(webSetup, /queueWorkoutPreparation\(\)/);
  const webRestore = methodBody(
    webApp,
    "function restoreWorkoutAfterSetup()",
    "function queueWorkoutPreparation(",
  );
  assert.match(
    webRestore,
    /restorePendingMovement\(\)[\s\S]*showNextExercise\(\)/,
  );
  assert.match(instantControls, /setActiveWorkoutSetup\(enabled, minimumMinutes = 3\)/);
  assert.match(instantControls, /elements\.range\.disabled = false/);
  assert.match(mainActivity, /_durationSeekBar.Enabled = true/);
  assert.match(preparationWorker, /mode === "reconfigure"[\s\S]*reconfigureActiveWorkout/);

  assert.match(workoutState, /ActiveSelectionGroupOrder/);
  assert.match(workoutState, /ActiveModifierProtectedSelectionGroupId/);
  assert.match(workoutSessionLog, /List<WorkoutModifierChangeLog> ModifierChanges/);
  assert.match(
    sessionService,
    /ReconfigureActiveWorkout\([\s\S]*lockedSelectionGroupIds[\s\S]*RebalanceNewExercisesByMuscleBalance\([\s\S]*lockedSelectionGroupIds/,
  );
  assert.match(
    sessionService,
    /preserveCompletedCurrentSelection[\s\S]*lockedSelectionGroupIds\.Remove[\s\S]*currentSelectionChanged[\s\S]*ClearPendingMovement/,
  );
  assert.doesNotMatch(sessionService, /currentSelectionFitsModifiers/);
  assert.match(
    workoutModule,
    /reconfigureActiveWorkout\([\s\S]*lockedSelectionGroupIds[\s\S]*rebalanceNewExercisesByMuscleBalance\(lockedSelectionGroupIds\)/,
  );
  assert.match(
    workoutModule,
    /preserveCompletedCurrentSelection[\s\S]*lockedSelectionGroupIds\.delete[\s\S]*currentSelectionChanged[\s\S]*clearPendingMovement/,
  );
  assert.doesNotMatch(workoutModule, /currentSelectionFitsModifiers/);
});

test("session continuity, editable duration, and explicit end stay in parity", async () => {
  const editing = await readFile(new URL("../../NomadicMethod/Services/WorkoutSessionEditing.cs", import.meta.url), "utf8");
  assert.match(mainActivity, /sessionService\.RestoreAfterReopen\(_state\)/);
  const startup = methodBody(mainActivity, "private ApplicationStartupResult InitializeApplication(", "private void CompleteApplicationStartup(");
  assert.doesNotMatch(startup, /FinishInterruptedWorkout|FinalizeCurrentWorkout|PrepareWorkout/);
  assert.doesNotMatch(webApp, /session\.finishInterruptedWorkout\(\)/);
  for (const name of ["RestoreAfterReopen", "ResizeActiveWorkout", "EndActiveWorkout"])
    assert.ok(editing.includes(name));
  assert.match(mainActivity, /ConfirmEndSession[\s\S]*SetNegativeButton[\s\S]*SetPositiveButton/);
  assert.match(webIndex, /<dialog id="end-session-dialog"/);
  assert.match(webApp, /returnValue === "end"\) endActiveWorkout/);
  const nativeEnd = methodBody(mainActivity, "private async void EndActiveWorkout()", "private void RestoreWorkoutAfterSetup()");
  assert.match(nativeEnd, /ShowDurationSelection\(\)/);
  assert.doesNotMatch(nativeEnd, /ActivatePreparedWorkout|PrepareWorkout|LogOuraDecision/);
  const webEnd = methodBody(webApp, "function endActiveWorkout()", "function restoreWorkoutAfterSetup()");
  assert.match(webEnd, /showDuration\(\)/);
  assert.doesNotMatch(webEnd, /activatePreparedWorkout|prepareWorkout|new Worker/);
  assert.match(preparationWorker, /resizeActiveWorkout\(minutes\)/);
  assert.match(workoutState, /ActiveDurationSelectionGroupIds/);
  assert.match(workoutSessionLog, /DurationChanges/);
});

test("duration modifiers separate workout context from available equipment", () => {
  assert.match(
    durationLayout,
    /HorizontalScrollView[\s\S]*@\+id\/duration_modifier_scroller[\s\S]*fillViewport="true"[\s\S]*@\+id\/duration_modifier_groups/,
  );
  assert.match(
    mainActivity,
    /FindRequiredView<HorizontalScrollView>[\s\S]*Resource\.Id\.duration_modifier_scroller/,
  );
  assert.match(
    mainActivity,
    /modifierScrollerLayout[\s\S]*_durationModifierScroller\.LayoutParameters[\s\S]*_durationModifierScroller\.ScrollTo\(0, 0\)/,
  );
  assert.match(
    durationLayout,
    /duration_context_modifier_group[\s\S]*upper_body_clothing_modifier_button[\s\S]*hard_floor_modifier_button[\s\S]*insect_modifier_button[\s\S]*silence_modifier_button[\s\S]*shy_modifier_button[\s\S]*duration_equipment_modifier_group[\s\S]*wall_modifier_button[\s\S]*mirror_modifier_button/,
  );
  assert.match(
    webIndex,
    /modifier-context-group[\s\S]*upper-body-clothing-modifier[\s\S]*hard-floor-modifier[\s\S]*insect-modifier[\s\S]*silence-modifier[\s\S]*shy-modifier[\s\S]*modifier-equipment-group[\s\S]*wall-modifier[\s\S]*mirror-modifier/,
  );
  assert.match(
    durationLayout,
    /duration_context_modifier_group[\s\S]*background="@drawable\/duration_modifier_group_background"[\s\S]*duration_equipment_modifier_group[\s\S]*background="@drawable\/duration_modifier_group_background"/,
  );
  assert.match(webStyles, /\.duration-modifiers[\s\S]*gap: 12px/);
  assert.match(
    webStyles,
    /\.modifier-group\s*\{[\s\S]*gap: 5px;[\s\S]*padding: 4px;[\s\S]*border: 1px solid #9f978d;[\s\S]*border-radius: 22px;[\s\S]*background: #e8e0d6;/,
  );
  assert.match(
    webStyles,
    /@media \(max-width: 340px\)[\s\S]*\.duration-modifiers[\s\S]*gap: 8px[\s\S]*\.modifier-group[\s\S]*gap: 4px/,
  );
  assert.match(
    webStyles,
    /@media \(orientation: landscape\)[\s\S]*\.duration-modifiers\s*\{[\s\S]*flex-wrap: nowrap;[\s\S]*overflow-x: auto;[\s\S]*touch-action: pan-x;/,
  );
});

test("active movement checkpoints and invalid media recovery match across platforms", async () => {
  const [stateStoreContract, stateStore] = await Promise.all([
    source("NomadicMethod", "Data", "IWorkoutStateStore.cs"),
    source("NomadicMethod", "Data", "SharedPreferencesWorkoutStateStore.cs"),
  ]);
  assert.match(workoutState, /PendingMovementGroupId/);
  assert.match(workoutState, /PendingMovementMillisecondsRemaining/);
  assert.match(workoutState, /PendingMovementEndsAtUnixMilliseconds/);
  assert.match(workoutState, /PendingMovementPausedByUser/);
  assert.match(workoutModule, /pendingMovementGroupId:\s*null/);
  assert.match(workoutModule, /pendingMovementMillisecondsRemaining:\s*0/);
  assert.match(workoutModule, /pendingMovementEndsAtUnixMilliseconds:\s*0/);
  assert.match(workoutModule, /pendingMovementPausedByUser:\s*false/);

  const mobileCreate = methodBody(
    mainActivity,
    "protected override void OnCreate(Bundle? savedInstanceState)",
    "protected override void OnResume()",
  );
  const mobilePause = methodBody(
    mainActivity,
    "private void PauseCountdown()",
    "private void ResumeCountdown()",
  );
  const mobileStart = methodBody(
    mainActivity,
    "private void StartCountdownTimer(long millisecondsRemaining)",
    "private void UpdateMoveCountdown(long millisecondsRemaining)",
  );
  const mobilePlaybackGuard = methodBody(
    mainActivity,
    "private void PlayHoldOnce()",
    "private void FreezeHoldOnFinalFrame()",
  );
  assert.match(
    mobileCreate,
    /RestoreAfterReopen[\s\S]*GetPendingMovementGroup[\s\S]*GetPendingRestGroup[\s\S]*RestorePendingMovement[\s\S]*RestorePendingRest[\s\S]*_state.ActiveWorkoutMinutes != 0[\s\S]*ShowNextExercise/,
  );
  assert.match(stateStoreContract, /void SaveDeferred\(WorkoutState state\)/);
  assert.match(
    stateStore,
    /SaveDeferred\(WorkoutState state\)[\s\S]*CreateEditor\(state\)\.Apply\(\)/,
  );
  assert.match(
    stateStore,
    /public void Save\(WorkoutState state\)[\s\S]*editor\.Commit\(\)/,
  );
  assert.match(mobilePause, /PauseMovement[\s\S]*_stateStore\.Save\(_state\)/);
  assert.match(
    mobileStart,
    /BeginMovement[\s\S]*_stateStore\.SaveDeferred\(_state\)/,
  );
  assert.match(
    mobilePlaybackGuard,
    /Looping = false[\s\S]*catch \(Java\.Lang\.IllegalStateException\)[\s\S]*RecoverInvalidMediaPlayerState/,
  );
  const mobilePhaseRestart = methodBody(mainActivity,
    "private void RestartExerciseMediaForPhase(MovementPhase phase)",
    "private void RecoverInvalidMediaPlayerState()");
  assert.match(mobilePhaseRestart, /positionMilliseconds = 0[\s\S]*SeekTo\(positionMilliseconds\)/);
  assert.match(webApp, /const segmentStart = 0[\s\S]*elements\.video\.currentTime = segmentStart/);
  assert.doesNotMatch(mainActivity, /EnforceDirectionMediaSegment/);

  assert.match(
    workoutModule,
    /initialize\(\)[\s\S]*normalizePendingRest\(\)[\s\S]*normalizePendingMovement\(\)[\s\S]*getPendingRestGroup\(\)[\s\S]*getPendingMovementGroup\(\)/,
  );
  assert.match(
    webApp,
    /getPendingRestGroup\(\)[\s\S]*restorePendingRest\(\)[\s\S]*getPendingMovementGroup\(\)[\s\S]*restorePendingMovement\(\)/,
  );
  assert.match(
    webApp,
    /session\.restoreAfterReopen\(\)[\s\S]*pendingRestGroup = session\.getPendingRestGroup\(\)[\s\S]*pendingMovementGroup = session\.getPendingMovementGroup\(\)[\s\S]*activeWorkoutMinutes !== 0[\s\S]*showNextExercise\(\)/,
  );
  assert.match(
    mainActivity,
    /RestorePendingRest\([\s\S]*ShowNextExercise\(\)[\s\S]*_restActive = true[\s\S]*ShowRestPanel\(\)[\s\S]*ResumeRestCountdown\(\)/,
  );
  assert.match(
    webApp,
    /function restorePendingRest\([\s\S]*showNextExercise\(\)[\s\S]*restActive = true[\s\S]*showRestPanel\(\)[\s\S]*startRestTimer\(\)/,
  );
  assert.match(
    webApp,
    /function setMovementDeadline[\s\S]*session\.beginMovement[\s\S]*persistState\(\)/,
  );
  assert.match(
    webApp,
    /function pauseMovement[\s\S]*session\.pauseMovement[\s\S]*persistState\(\)/,
  );
  assert.match(webApp, /visibilitychange[\s\S]*pagehide/);
});

test("backgrounding pauses movement and rest until playback is resumed", () => {
  const mobileLifecyclePause = methodBody(
    mainActivity,
    "protected override void OnPause()",
    "#pragma warning disable CA1422",
  );
  const mobileBackgroundPause = methodBody(
    mainActivity,
    "private void PauseActiveWorkoutForBackground()",
    "private void ResumeCountdown()",
  );
  assert.match(
    mobileLifecyclePause,
    /PauseActiveWorkoutForBackground\(\)[\s\S]*PauseRestCountdown\(\)/,
  );
  assert.match(
    mobileBackgroundPause,
    /_countdownPausedByUser = true[\s\S]*PauseCountdown\(\)[\s\S]*SetPlaybackControlsAvailability/,
  );
  assert.match(
    mobileBackgroundPause,
    /PendingRestPausedByUser[\s\S]*GetPendingRestMillisecondsRemaining[\s\S]*PauseRest[\s\S]*_stateStore\.Save/,
  );

  const webBackgroundPause = methodBody(
    webApp,
    "function pauseActiveWorkoutForBackground()",
    "function handleVisibilityChange()",
  );
  const webVisibility = methodBody(
    webApp,
    "function handleVisibilityChange()",
    "function handlePageHide()",
  );
  const webPageHide = methodBody(
    webApp,
    "function handlePageHide()",
    "function scheduleMediaRecoveryFailure(generation)",
  );
  assert.match(webBackgroundPause, /movementRunning[\s\S]*pauseMovement\("user"\)/);
  assert.match(
    webBackgroundPause,
    /pendingRestPausedByUser[\s\S]*getPendingRestMillisecondsRemaining[\s\S]*pauseRest[\s\S]*persistState/,
  );
  assert.match(webVisibility, /document\.hidden[\s\S]*pauseActiveWorkoutForBackground\(\)/);
  assert.match(webVisibility, /movementPauseReason !== "user"/);
  assert.match(webPageHide, /pauseActiveWorkoutForBackground\(\)/);
  assert.doesNotMatch(webApp, /pauseMovement\("visibility"\)/);
});

test("lead-stance exercises use the same two-block sequence cues on mobile and web", () => {
  const expectedLeadStanceIds = [
    204, 205, 245, 265, 279, 473, 528, 538, 575, 578, 583, 591,
    884, 885, 886, 887, 1024,
  ];
  assert.deepEqual(
    catalog
      .filter((exercise) => exercise.sideSequence.includes("LeadThen"))
      .map((exercise) => exercise.id),
    expectedLeadStanceIds,
  );
  for (const exercise of catalog.filter((item) => expectedLeadStanceIds.includes(item.id))) {
    assert.equal(exercise.sequenceBlocks.length, 2);
    assert.deepEqual(
      exercise.sequenceBlocks.map((block) => block.sideCue),
      ["ShownLeadStance", "OppositeLeadStance"],
    );
  }
  assert.match(
    sequenceBlockModel,
    /ShownLeadStance[\s\S]*OppositeLeadStance/,
  );
  assert.match(
    movementPresentationPolicy,
    /GetPresentation\([\s\S]*ExerciseSequenceSideCue sideCue[\s\S]*ExerciseSequenceDirectionCue directionCue[\s\S]*bool mirrorMedia[\s\S]*return new MovementPhasePresentation\([\s\S]*sideCue,[\s\S]*directionCue,[\s\S]*mirrorMedia/,
  );
  assert.match(workoutModule, /ShownLeadStance/);
  assert.match(workoutModule, /OppositeLeadStance/);
  assert.match(
    mainActivity,
    /ShownLeadStance => "Shown lead stance"[\s\S]*OppositeLeadStance => "Opposite lead stance"/,
  );
  assert.match(
    webApp,
    /ShownLeadStance:[\s\S]*"Shown lead stance"[\s\S]*OppositeLeadStance:[\s\S]*"Opposite lead stance"/,
  );
});

test("uppercut replacement is a clear one-block alternating floor-safe contract", () => {
  const uppercuts = catalog.find((exercise) => exercise.id === 287);
  assert.equal(uppercuts.name, "Wide-Stance Alternating Uppercuts");
  assert.equal(uppercuts.primaryCanonicalGroup, "ShoulderAbductors");
  assert.equal(uppercuts.sideSequence, "Alternating");
  assert.equal(uppercuts.sequenceBlocks.length, 1);
  assert.equal(uppercuts.hardFloorCompatibility, "Incompatible");
  assert.equal(
    uppercuts.secondaryCanonicalGroups.includes("AbdominalWall"),
    false,
  );
  assert.ok(
    uppercuts.secondaryCanonicalGroups.includes("MedialAndDeepKneeExtensors"),
  );
});

test("web and mobile preserve deployed keeps by catalog membership", () => {
  assert.doesNotMatch(
    catalogMigrationRules,
    /LastKeptExerciseIds\.RemoveWhere\(invalidatedExerciseIds\.Contains\)/,
  );
  assert.match(
    sessionService,
    /NormalizeSlotPreferences\([\s\S]*IsValidPreferenceRoot[\s\S]*SyncLegacyKeptExerciseIds/,
  );
});

test("web and mobile close workouts without preselecting a future lineup", () => {
  assert.match(
    sessionService,
    /AcknowledgeCompletion\([\s\S]*state\.CompletionAcknowledged\s*=\s*true;[\s\S]*FinalizeCurrentWorkout\(state\);/,
  );
  assert.match(
    workoutModule,
    /acknowledgeCompletion\(\)[\s\S]*this\.state\.completionAcknowledged\s*=\s*true;[\s\S]*this\.finalizeCurrentWorkout\(\);/,
  );
  const nativeClose = sessionService.split("private void FinalizeCurrentWorkout(")[1]
    .split("private Exercise? TryGetSelectedExercise(")[0];
  const webClose = workoutModule.split("  finalizeCurrentWorkout() {")[1]
    .split("  repairActiveLineup(")[0];
  assert.doesNotMatch(nativeClose, /(?:ChooseBestDistinctLineup|PrepareWorkout|DownvoteSequence)\(/);
  assert.doesNotMatch(webClose, /(?:chooseBestDistinctLineup|prepareWorkout|downvoteSequence)\(/);
  assert.match(nativeClose, /RemoveSavedSequenceCopiesForSlot/);
  assert.match(webClose, /removeSavedSequenceCopiesForSlot/);
  assert.doesNotMatch(sessionService, /excludedExerciseIdsByGroup/);
  assert.doesNotMatch(workoutModule, /excludedExerciseIdsByGroup/);
});

test("web movement and rest timing match the mobile workout contract", () => {
  assert.equal(
    MOVEMENT_DURATION_MS / 1000,
    integerConstant(movementSchedule, "TotalDurationSeconds"),
  );
  assert.equal(
    PREPARATION_DURATION_MS / 1000,
    integerConstant(movementSchedule, "PreparationDurationSeconds"),
  );
  assert.doesNotMatch(
    movementSchedule,
    /SideDurationSeconds|SideChangeDurationSeconds|FullSide/,
  );
  assert.match(
    mainActivity,
    /GetCountdownDurationSeconds\([\s\S]*includePreparation: !_sessionService\.IsSequenceContinuationBlock/,
  );
  assert.match(
    webApp,
    /getMovementPhaseState\([\s\S]*!session\.isSequenceContinuationBlock/,
  );
  assert.equal(
    REST_DURATION_MS / 1000,
    integerConstant(mainActivity, "RestSeconds"),
  );
});

test("web and mobile separate the exercise whistle from the final completion cue", () => {
  const mobileStart = methodBody(mainActivity, "private void StartCountdown()", "private void TogglePlayback()");
  const webStart = methodBody(webApp, "function startMovement()", "function setMovementDeadline(");
  assert.doesNotMatch(mobileStart, /PlayWhistleCue/);
  assert.doesNotMatch(webStart, /playSound/);
  assert.match(
    mainActivity,
    /previousPhase is null or MovementPhase\.Preparation[\s\S]*CueMovementRestart\(\)/,
  );
  assert.match(
    webApp,
    /previousPhase === "Preparation"[\s\S]*playSound\("start"\)/,
  );
  assert.match(
    mainActivity,
    /private void CompleteCountdown\(\)[\s\S]*PlayWhistleCue\(_restStartWhistleId\);[\s\S]*BeginRest\(\);/,
  );
  assert.match(
    mainActivity,
    /private void FinalizeCurrentRound\(bool keep\)[\s\S]*if \(_state\.WorkoutCompleted\)[\s\S]*PlayWhistleCue\(_workoutCompleteWhistleId\);[\s\S]*ShowCongratulations\(\);/,
  );
  assert.match(
    webApp,
    /function completeMovement\(\)[\s\S]*playSound\("rest"\);[\s\S]*session\.beginRest/,
  );
  assert.match(
    webApp,
    /function completeRest\(\)[\s\S]*session\.state\.workoutCompleted\)[\s\S]*showCompletion\(true\);/,
  );
  const mobileCueBodies = [
    methodBody(mainActivity, "private void CueMovementRestart()", "private void RenderCountdownPhase("),
    methodBody(mainActivity, "private void CompleteCountdown()", "private void CancelCountdown("),
    methodBody(mainActivity, "private void FinalizeCurrentRound(bool keep)", "private void ShowCongratulations()"),
    methodBody(mainActivity, "private void PlayWhistleCue(int soundId)", "[SuppressMessage("),
  ];
  const webCueBodies = [
    methodBody(webApp, "function applyMovementPhase(phase)", "function restartMediaForPhase("),
    methodBody(webApp, "function completeMovement()", "function startRestTimer()"),
    methodBody(webApp, "function showCompletion(playCue)", "function closeCompletion()"),
    methodBody(webApp, "function playSound(name)", "function handleVisibilityChange()"),
  ];
  for (const body of mobileCueBodies) {
    assert.doesNotMatch(body, /WorkoutModifiers\.Silence|_selectedWorkoutModifiers/);
  }
  for (const body of webCueBodies) {
    assert.doesNotMatch(body, /WORKOUT_MODIFIERS\.Silence|selectedModifiers/);
  }
});

test("Android volume keys always control the whistle media stream", () => {
  assert.match(
    mainActivity,
    /OnCreate\(Bundle\? savedInstanceState\)[\s\S]*VolumeControlStream\s*=\s*Android\.Media\.Stream\.Music;[\s\S]*ConfigureWhistleCues\(\);/,
  );
  assert.match(
    mainActivity,
    /SetUsage\(\s*Android\.Media\.AudioUsageKind\.Media\)/,
  );
});

test("all bilateral, directional, linked, and repeated work uses one sequence model", () => {
  assert.match(exerciseModel, /ExerciseSequenceBlock\[\] SequenceBlocks/);
  assert.doesNotMatch(exerciseModel, /DirectionPartnerExerciseId/);
  assert.match(
    sequenceBlockModel,
    /ExerciseSequenceSideCue[\s\S]*ExerciseSequenceDirectionCue[\s\S]*ExerciseSequenceMediaSegment[\s\S]*ExerciseId[\s\S]*MirrorMedia/,
  );
  assert.match(
    workoutGroup,
    /SequenceBlockIndex[\s\S]*SequenceBlockCount[\s\S]*SetNumber[\s\S]*SetCount[\s\S]*IsFinalSequenceRound/,
  );
  assert.doesNotMatch(workoutGroup, /PairedRoundId|IsPairDecisionRound/);
  assert.deepEqual(
    catalog
      .filter((exercise) => exercise.directionSequence !== "None")
      .map((exercise) => exercise.id),
    [406, 409, 561, 608, 611, 1018],
  );
  assert.ok(catalog.every((exercise) =>
    !Object.hasOwn(exercise, "directionPartnerExerciseId")));
  const ownerByExerciseId = new Map();
  for (const root of catalog.filter((exercise) => exercise.sequenceBlocks.length > 0)) {
    for (const exerciseId of new Set(root.sequenceBlocks.map((block) => block.exerciseId))) {
      assert.equal(ownerByExerciseId.has(exerciseId), false);
      ownerByExerciseId.set(exerciseId, root.id);
    }
  }
  assert.equal(ownerByExerciseId.size, catalog.length);
  assert.deepEqual(
    catalog
      .filter((root) => new Set(root.sequenceBlocks.map((block) => block.exerciseId)).size > 1)
      .map((root) => root.id),
    [
      96, 115, 178, 179, 180, 181,
      211, 214, 220, 223, 252, 264, 285, 288, 291, 302, 307,
      367, 392, 393, 415, 420, 459, 465, 491, 500, 502, 566, 610, 612,
      617, 742, 784, 834, 910, 948,
    ],
  );
});

test("atomic sequences are adjacent units that may satisfy multiple primary slots", () => {
  assert.match(workoutState, /Dictionary<string, int> ActiveSetCountsBySelectionGroupId/);
  assert.doesNotMatch(
    sessionService,
    /state\.ActiveWorkoutMinutes <= 30 &&[\s\S]*exercise\.SequenceBlocks\.Length > 1/,
  );
  assert.doesNotMatch(
    workoutModule,
    /workoutMinutes !== null && workoutMinutes <= 30[\s\S]*exercise\.sequenceBlocks\.length > 1/,
  );
  assert.match(
    workoutSequencePolicy,
    /GetPrimaryCoverageGroups[\s\S]*member\.PrimaryCanonicalGroup[\s\S]*coveredGroupIds\.Add/,
  );
  assert.match(
    atomicSequenceLineupSolver,
    /CoverageMask[\s\S]*BlockCount[\s\S]*workoutMinutes[\s\S]*ReduceToBlockCapacity/,
  );
  assert.match(
    sessionService,
    /candidate\.SequenceBlocks\.Length \+[\s\S]*groups\.Count - placementGroups\.Length[\s\S]*state\.ActiveWorkoutMinutes/,
  );
  assert.match(
    workoutModule,
    /candidate\.sequenceBlocks\.length \+[\s\S]*groups\.length - placementGroups\.length[\s\S]*this\.state\.activeWorkoutMinutes/,
  );
  assert.match(
    sessionService,
    /phaseAfterAddingSetByGroupId[\s\S]*Score: hasPhaseScoreAdjustments[\s\S]*GetPhaseScoreAdjustment\([\s\S]*SetCount: setCounts\[groupId\][\s\S]*Kept: IsSequenceKept\(state, groupId, placement\.Root\)[\s\S]*OneBlock: cost == 1[\s\S]*rank\.Score > selectedRank\.Value\.Score/,
  );
  assert.match(
    workoutModule,
    /phaseAfterAddingSetByGroupId[\s\S]*score = hasPhaseScoreAdjustments[\s\S]*getPhaseScoreAdjustment\([\s\S]*setCount < selectedMetadata\.setCount[\s\S]*Number\(kept\)[\s\S]*cost === 1/,
  );
  assert.match(
    sessionService,
    /selectedGroupsByRootId[\s\S]*GetSequencePlacementOptions[\s\S]*CoveredGroups/,
  );
  assert.match(
    workoutModule,
    /selectedGroupsByRootId[\s\S]*getSequencePlacementOptions[\s\S]*coveredGroups/,
  );
  assert.match(
    sessionService,
    /for \(int setNumber = 1;[\s\S]*for \(int blockIndex = 0;[\s\S]*\.set\{setNumber\}\.[\s\S]*block\{blockIndex \+ 1\}/,
  );
  assert.match(
    workoutModule,
    /for \(let setNumber = 1;[\s\S]*for \(let blockIndex = 0;[\s\S]*\.set\$\{setNumber\}\.block\$\{blockIndex \+ 1\}/,
  );
  assert.match(
    sessionService,
    /if \(!group\.IsFinalSequenceRound\)[\s\S]*only be rated after its final block/,
  );
  assert.match(
    workoutModule,
    /if \(!isFinalSequenceRound\(group\)\)[\s\S]*only be rated after its final block/,
  );
  assert.match(
    sessionService,
    /ApplySequenceOutcome[\s\S]*DownvoteSequenceInPhase[\s\S]*RecordWorkoutDecision/,
  );
  assert.match(
    workoutModule,
    /applySequenceOutcome[\s\S]*downvoteSequenceInPhase[\s\S]*recordWorkoutDecision/,
  );
  assert.match(
    mainActivity,
    /IsIntermediateSequenceBlock[\s\S]*_keepButton\.Visibility = ViewStates\.Gone/,
  );
  assert.match(
    webApp,
    /getNextSequenceBlock[\s\S]*elements\.keepExercise\.hidden = isIntermediateBlock/,
  );
  assert.match(
    webIndex,
    /id="keep-exercise"[\s\S]*aria-label="Keep exercise for the next session"[\s\S]*class="keep-love-icon"/,
  );
  assert.match(strings, /name="keep_exercise_description">Keep this exercise/);
  assert.doesNotMatch(mainActivity, /Tap to keep both|keep both directions|tap_to_keep_both/);
  assert.doesNotMatch(webApp, /Tap to keep both|keep both directions/);
  assert.doesNotMatch(strings, /tap_to_keep|Tap to keep/);
});

test("every intermediate block rests 15 seconds and continues automatically", () => {
  assert.match(
    sessionService,
    /GetNextSequenceBlock[\s\S]*activeGroups\[groupIndex \+ 1\][\s\S]*nextGroup\.SelectionKey == group\.SelectionKey/,
  );
  assert.match(
    workoutModule,
    /getNextSequenceBlock[\s\S]*activeGroups\[groupIndex \+ 1\][\s\S]*getSelectionKey\(nextGroup\)/,
  );
  assert.match(
    sessionService,
    /KeepPendingRest[\s\S]*IsIntermediateSequenceBlock[\s\S]*return false/,
  );
  assert.match(
    workoutModule,
    /keepPendingRest\(\)[\s\S]*isIntermediateSequenceBlock[\s\S]*return false/,
  );
  assert.match(
    sessionService,
    /AdvanceSequence[\s\S]*ExerciseOutcome\.Neutral[\s\S]*WorkoutCompleted = false/,
  );
  assert.match(
    workoutModule,
    /advanceSequence[\s\S]*"neutral"[\s\S]*workoutCompleted = false/,
  );
  assert.match(
    mainActivity,
    /Next block:[\s\S]*starts automatically[\s\S]*ContinueWithNextSequenceBlock[\s\S]*PauseMovement[\s\S]*RestorePendingMovement/i,
  );
  assert.match(
    webApp,
    /Next block:[\s\S]*starts automatically[\s\S]*advanceSequence[\s\S]*pauseMovement[\s\S]*restorePendingMovement/i,
  );
  assert.match(
    mainActivity,
    /ContinueWithNextSequenceBlock[\s\S]*TotalDurationSeconds \* 1_000/,
  );
  assert.match(
    webApp,
    /pauseMovement\([\s\S]*getMovementDurationMs\(nextBlock\)[\s\S]*false/,
  );
});

test("intermediate Rest previews the exact upcoming block without advancing it", () => {
  assert.match(
    mainActivity,
    /ShowRestPanel[\s\S]*GetNextSequenceBlock[\s\S]*ShowUpcomingSequenceBlockPreview/,
  );
  assert.match(
    mainActivity,
    /ShowUpcomingSequenceBlockPreview[\s\S]*GetSelectedExercise[\s\S]*RenderExerciseIdentity\(nextExercise, upcoming: true\)[\s\S]*LoadExerciseMedia\([\s\S]*nextExercise,[\s\S]*nextBlock,[\s\S]*previewingUpcomingSequenceBlock: true/,
  );
  assert.match(
    webApp,
    /showRestPanel[\s\S]*getNextSequenceBlock[\s\S]*showUpcomingSequenceBlockPreview/,
  );
  assert.match(
    webApp,
    /showUpcomingSequenceBlockPreview[\s\S]*getSelectedExercise[\s\S]*renderExerciseIdentity\(nextExercise, true\)[\s\S]*loadExerciseMedia\(nextExercise, nextBlock, true\)/,
  );
  assert.match(
    mainActivity,
    /WorkoutPhase\.Rest when _previewingUpcomingSequenceBlock[\s\S]*_mediaWorkoutGroup/,
  );
  assert.match(
    webApp,
    /restActive && previewingUpcomingSequenceBlock[\s\S]*mediaExercise\?\.presentation !== "Still"/,
  );
});

test("web catalog migration matches the mobile workout contract", () => {
  assert.equal(
    CURRENT_CATALOG_REVISION,
    integerConstant(catalogMigrationRules, "CurrentCatalogRevision"),
  );
  assert.equal(
    LAST_CUMULATIVE_CATALOG_REVISION,
    integerConstant(catalogMigrationRules, "LastCumulativeWorkoutStateRevision"),
  );
  assert.deepEqual(
    [...SCOPED_CATALOG_INVALIDATIONS_BY_REVISION].map(([revision, exerciseIds]) => [
      revision,
      [...exerciseIds],
    ]),
    scopedCatalogInvalidations(catalogMigrationRules),
  );
  assert.deepEqual(
    [...SCOPED_SCORE_INVALIDATIONS_BY_REVISION].map(([revision, exerciseIds]) => [
      revision,
      [...exerciseIds],
    ]),
    scopedCatalogInvalidations(
      catalogMigrationRules,
      "ScopedScoreInvalidationsByRevision =",
    ),
  );
  assert.deepEqual(
    [...APPROVED_EXERCISE_CORRECTIONS],
    approvedExerciseCorrections(catalogMigrationRules),
  );
  assert.deepEqual(
    catalog
      .filter((exercise) => typeof exercise.retiredName === "string" && exercise.retiredName)
      .map((exercise) => exercise.id)
      .sort((left, right) => left - right),
    integerCollection(catalogMigrationRules, "ReplacedExerciseIdSet")
      .filter(
        (exerciseId) =>
          !integerCollection(
            catalogMigrationRules,
            "PermanentlyRetiredExerciseIdSet",
          ).includes(exerciseId),
      )
      .sort((left, right) => left - right),
  );
});

async function source(...segments) {
  return readFile(path.join(repositoryRoot, ...segments), "utf8");
}

async function binarySource(...segments) {
  return readFile(path.join(repositoryRoot, ...segments));
}

function methodBody(contents, startMarker, endMarker) {
  const start = contents.indexOf(startMarker);
  const end = contents.indexOf(endMarker, start + startMarker.length);
  assert.ok(start >= 0 && end > start, `Could not isolate ${startMarker}.`);
  return contents.slice(start, end);
}

function integerArray(contents, name) {
  const match = contents.match(
    new RegExp(`${name}\\s*=\\s*Array\\.AsReadOnly\\(\\[([^\\]]+)\\]\\)`, "s"),
  );
  assert.ok(match, `Could not read mobile array ${name}.`);
  return [...match[1].matchAll(/\d+/g)].map((item) => Number(item[0]));
}

function integerConstant(contents, name) {
  const match = contents.match(new RegExp(`const\\s+int\\s+${name}\\s*=\\s*(\\d+)`));
  assert.ok(match, `Could not read mobile constant ${name}.`);
  return Number(match[1]);
}

function integerCollection(contents, name) {
  const match = contents.match(new RegExp(`${name}\\s*=\\s*\\[([^\\]]+)\\]`, "s"));
  assert.ok(match, `Could not read mobile collection ${name}.`);
  return [...match[1].matchAll(/\d+/g)].map((item) => Number(item[0]));
}

function approvedExerciseCorrections(contents) {
  const start = contents.indexOf("ApprovedExerciseCorrections =");
  const end = contents.indexOf("private static readonly", start);
  assert.ok(start >= 0 && end > start, "Could not read mobile approved exercise corrections.");
  return [...contents.slice(start, end).matchAll(
    /\[(\d+)\]\s*=\s*new\(\s*"([^"]+)",\s*"([^"]+)"\s*\)/g,
  )].map((item) => [Number(item[1]), [item[2], item[3]]]);
}

function scopedCatalogInvalidations(
  contents,
  name = "ScopedWorkoutStateInvalidationsByRevision =",
) {
  const start = contents.indexOf(name);
  const end = contents.indexOf("private static readonly", start);
  assert.ok(start >= 0 && end > start, "Could not read mobile scoped catalog invalidations.");
  return [...contents.slice(start, end).matchAll(
    /\[(\d+)\]\s*=\s*new HashSet<int>\s*\{([^}]+)\}/g,
  )].map((item) => [
    Number(item[1]),
    [...item[2].matchAll(/\d+/g)].map((exerciseId) => Number(exerciseId[0])),
  ]);
}
