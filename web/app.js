import { ExerciseFeedbackView } from "./exercise-feedback.js";
import { FitnessProfileView, inspectFitnessProfile, parseFitnessRatings, FITNESS_EVIDENCE_VERSION, FITNESS_UNAVAILABLE_MESSAGE } from "./fitness-profile.js";
import { renderCompletionProfile } from "./completion-profile.js";
import { resolveReactionDrills, ReactionDrill } from "./reaction-drills.js";
import { ReactionDrillView } from "./reaction-drill-view.js";
import {
  DEFAULT_WORKOUT_MODIFIERS,
  LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP,
  LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT,
  MIRROR_EQUIPMENT,
  WALL_EQUIPMENT,
  ELEVATION_EQUIPMENT,
  LOCOMOTION_SPACE,
  REST_DURATION_MS,
  NEW_WORKOUT_MINUTES,
  getWorkoutMinuteOptions,
  normalizeSetupMinutes,
  WORKOUT_MODIFIERS,
  WorkoutSession,
  findSoleWallContactRequiredCatalogDeficiencies,
  findWallRequiredCatalogDeficiencies,
  findElevationCatalogDeficiencies,
  findLocomotionCatalogDeficiencies,
  findFloorCatalogDeficiencies,
  findFloorAndWallCatalogDeficiencies,
  findFloorAndLocomotionCatalogDeficiencies,
  getExerciseVideoPath,
  getHoldFramePath,
  getMovementCountdownDurationMs,
  getMovementDurationMs,
  getMovementPhaseState,
  getMovementPresentation,
  getNextReactionCueDurationMilliseconds,
  getMirrorEquipment,
  getWallEquipment,
  getElevationEquipment,
  getLocomotionSpace,
  getWorkoutDisplayProgress,
  getWorkoutExecutionTimeline,
  isModifierMetadataComplete,
  isSessionMovementMetadataValid,
  isReactionCueEnabled,
  parseStoredState,
  withMirrorEquipment,
  withWallEquipment,
  withElevationEquipment,
  withLocomotionSpace,
} from "./workout.js";

const STORAGE_KEY = "nomadic-method.workout.state.v1";
const fitnessProfileView = new FitnessProfileView(document.getElementById("fitness-profile"));
let fitnessRatings = new Map();
const TIMER_INTERVAL_MS = 100;
const MEDIA_RECOVERY_TIMEOUT_MS = 12_000;
const MODIFIER_FEEDBACK_DURATION_MS = 2_040;
const EXERCISE_NAME_LONG_PRESS_MS = 500;
const EXERCISE_NAME_LONG_PRESS_MOVE_TOLERANCE_PX = 12;
const REACTION_CUE_MOVE_FLASH_MS = 560;
const MODIFIER_FEEDBACK_LABELS = Object.freeze({
  floorEnabled: "floor ON",
  floorDisabled: "floor OFF",
  upperBodyClothingEnabled: "upper-body clothing ON",
  upperBodyClothingDisabled: "upper-body clothing OFF",
  hardFloorEnabled: "hard floor ON",
  hardFloorDisabled: "hard floor OFF",
  insectEnabled: "insect mode ON",
  insectDisabled: "insect mode OFF",
  noisyEnabled: "noisy exercises ENABLED",
  noisyDisabled: "noisy exercises DISABLED",
  shyEnabled: "shy mode ON",
  shyDisabled: "shy mode OFF",
  lightEnabled: "light mode ON",
  lightDisabled: "light mode OFF",
  lightLocked: "rest, you must",
  wallSolesStayOff: "equipment ON: wall · no feet on wall",
  wallSolesMayTouch: "equipment ON: wall",
  wallDisabled: "equipment OFF: wall",
  compactMirrorEnabled: "equipment ON: compact mirror",
  tallMirrorEnabled: "equipment ON: tall mirror",
  mirrorDisabled: "equipment OFF: mirror",
});

const elements = {
  durationScreen: byId("duration-screen"),
  durationDial: byId("duration-dial"),
  durationValue: byId("duration-value"),
  durationDecrease: byId("duration-decrease"),
  durationIncrease: byId("duration-increase"),
  durationRange: byId("duration-range"),
  durationLabels: [...byId("duration-labels").children],
  beginWorkout: byId("begin-workout"),
  upperBodyClothingModifier: byId("upper-body-clothing-modifier"),
  hardFloorModifier: byId("hard-floor-modifier"),
  floorModifier: byId("floor-modifier"),
  insectModifier: byId("insect-modifier"),
  silenceModifier: byId("silence-modifier"),
  shyModifier: byId("shy-modifier"),
  lightModifier: byId("light-workout-modifier"),
  lightCountdown: byId("light-workout-countdown"),
  wallModifier: byId("wall-modifier"),
  locomotionModifier: byId("locomotion-modifier"),
  mirrorModifier: byId("mirror-modifier"),
  modifierFeedback: byId("modifier-feedback"),
  workoutScreen: byId("workout-screen"),
  phaseSurface: byId("phase-surface"),
  phaseLeft: byId("phase-left"),
  phaseRight: byId("phase-right"),
  workoutHeader: byId("workout-header"),
  workoutSetup: byId("workout-setup"),
  workoutProgressText: byId("workout-progress-text"),
  workoutProgressFill: byId("workout-progress-fill"),
  exerciseName: byId("exercise-name"),
  mediaCard: byId("exercise-media-card"),
  video: byId("exercise-video"),
  holdFrame: byId("hold-frame"),
  holdBadge: byId("hold-badge"),
  reactionCue: byId("reaction-cue"),
  executionSignifier: byId("execution-signifier"),
  executionBlockTrack: byId("execution-block-track"),
  mediaScrim: byId("media-scrim"),
  mediaError: byId("media-error"),
  mediaRetry: byId("media-retry"),
  readyPanel: byId("ready-panel"),
  shuffleExercise: byId("shuffle-exercise"),
  startMovement: byId("start-movement"),
  movePanel: byId("move-panel"),
  repeatExercise: byId("repeat-exercise"),
  playbackToggle: byId("toggle-playback"),
  nextExercise: byId("next-exercise"),
  movementCountdown: byId("movement-countdown"),
  movementProgressFill: byId("movement-progress-fill"),
  restPanel: byId("rest-panel"),
  restPlaybackToggle: byId("toggle-rest"),
  restCountdown: byId("rest-countdown"),
  restProgressFill: byId("rest-progress-fill"),
  feedbackFooter: byId("exercise-feedback-footer"),
  completionScreen: byId("completion-screen"),
  completionChanges: byId("completion-changes"),
  doneButton: byId("done-button"),
  status: byId("status"),
};

const sounds = Object.fromEntries(
  ["start", "rest", "complete"].map((name) => {
    const audio = new Audio(new URL(`audio/whistle_${name}.ogg`, document.baseURI));
    audio.preload = "none";
    return [name, audio];
  }),
);

const startupControls = window.nomadicMethodStartupControls ?? null;
let session = null;
let exerciseCatalog = null;
let assetVersions = Object.freeze({});
let selectedMinutes = startupControls?.selectedMinutes ?? 10;
let selectedModifiers = startupControls?.selectedModifiers ??
  DEFAULT_WORKOUT_MODIFIERS;
let startupSelectionChanged = startupControls?.selectionChanged ?? false;
let automaticLightModePresented = false;
let startWorkoutWhenReady = false;
let currentGroup = null;
let currentExercise = null;
let mediaGroup = null;
let mediaExercise = null;
let previewingUpcomingSequenceBlock = false;
let mediaGeneration = 0;
let mediaReady = false;
let mediaRecoveryTimer = null;
let lastMovementPhase = null;
let reactionDrillPlans = new Map();
let reactionDrill = null;
let reactionDrillView = null;
let reactionCueActive = false;
let reactionCueMoving = true;
let reactionCueEndsAt = 0;
let reactionCueMoveFlashEndsAt = 0;
let movementTimer = null;
let movementEndsAt = 0;
let movementRemaining = 0;
let movementRunning = false;
let movementPauseReason = null;
let automaticSequenceStartPending = false;
let restTimer = null;
let feedbackHeld = false;
let feedbackResumeAfterHold = false;
let feedbackExpiring = false;
const feedbackView = new ExerciseFeedbackView(elements.mediaCard, elements.feedbackFooter, {
  choose: value => {
    if (!session?.setPendingRestFeedback(value)) return false;
    if (value) {
      if (!session.state.pendingRestPausedByUser)
        session.pauseRest(currentGroup, Math.max(1, session.getPendingRestMillisecondsRemaining(Date.now())));
      clearInterval(restTimer); restTimer = null;
      feedbackHeld = feedbackResumeAfterHold = false;
    }
    persistState(); updateRest(); renderRestPlaybackToggle(); return true;
  },
  hold: holdFeedbackClock,
  running: () => restActive && !session?.state.pendingRestPausedByUser,
  completed: () => { if (!document.hidden && restActive) { feedbackExpiring = false; completeRest(true); } },
});
let restActive = false;
let wakeLock = null;
let wakeLockRequestPending = false;
let wakeLockGeneration = 0;
let modifierFeedbackTimer = null;
let workoutPreparationGeneration = 0;
let workoutPreparationWorker = null;
let workoutPreparationPromise = null;
let workoutPreparationError = null;
let workoutPreparationResolve = null;
let preparingWorkoutMinutes = 0;
let preparingWorkoutModifiers = WORKOUT_MODIFIERS.None;
let preparedWorkout = null;
let preparingWorkoutIsReconfiguration = false;
let preparingCurrentWorkoutGroupId = null;
let activeWorkoutSetup = false;
let workoutSetupReturnPhase = "ready";
let workoutSetupShouldResume = false;
let workoutSetupCurrentGroupId = null;
let exerciseNameLongPress = null;
let lastExerciseNameCopyRequestAt = Number.NEGATIVE_INFINITY;

bindEvents();
renderDuration(selectedMinutes, false);
renderWorkoutModifiers();
bootstrap();

async function bootstrap() {
  try {
    const [catalogResponse, assetVersionsResponse, reactionResponse, fitnessResponse] = await Promise.all([
      fetch(new URL("data/exercises.json", document.baseURI)),
      fetch(new URL("data/asset-versions.json", document.baseURI)),
      fetch(new URL("data/reaction_drills.json", document.baseURI)),
      fetch(new URL("data/fitness_ratings.json", document.baseURI)),
    ]);
    if (!catalogResponse.ok) {
      throw new Error(`Catalog request failed with ${catalogResponse.status}.`);
    }
    if (!assetVersionsResponse.ok) {
      throw new Error(
        `Asset-version request failed with ${assetVersionsResponse.status}.`,
      );
    }
    const [exercises, loadedAssetVersions] = await Promise.all([
      catalogResponse.json(),
      assetVersionsResponse.json(),
    ]);
    if (!Array.isArray(exercises)) {
      throw new Error("Reload Nomadic Method to use the current exercise catalog.");
    }
    exerciseCatalog = exercises;
    if (!fitnessResponse.ok) throw new Error("Fitness rating request failed.");
    fitnessRatings = parseFitnessRatings(await fitnessResponse.json());
    if (!reactionResponse.ok) throw new Error("Reaction drill request failed.");
    reactionDrillPlans = resolveReactionDrills(await reactionResponse.json(), exercises);
    if (!loadedAssetVersions || Array.isArray(loadedAssetVersions) ||
        typeof loadedAssetVersions !== "object") {
      throw new Error("Asset-version manifest is invalid.");
    }
    assetVersions = Object.freeze({ ...loadedAssetVersions });
    const wallCatalogDeficiencies =
      findWallRequiredCatalogDeficiencies(exercises);
    const soleWallCatalogDeficiencies =
      findSoleWallContactRequiredCatalogDeficiencies(exercises);
    if (!isModifierMetadataComplete(exercises) ||
        !isSessionMovementMetadataValid(exercises) ||
        wallCatalogDeficiencies.length > 0 ||
        soleWallCatalogDeficiencies.length > 0 ||
        findLocomotionCatalogDeficiencies(exercises).length > 0 ||
        findFloorCatalogDeficiencies(exercises).length > 0 ||
        findFloorAndWallCatalogDeficiencies(exercises).length > 0 ||
        findFloorAndLocomotionCatalogDeficiencies(exercises).length > 0 ||
        findElevationCatalogDeficiencies(exercises).length > 0) {
      throw new Error("Catalog does not satisfy workout invariants.");
    }
    session = new WorkoutSession(exercises, loadState(), undefined, undefined, fitnessRatings);
    session.restoreAfterReopen();
    const pendingRestGroup = session.getPendingRestGroup();
    const pendingMovementGroup = session.getPendingMovementGroup();
    persistState();
    if (!startupSelectionChanged) {
      selectedMinutes = session.state.lastWorkoutMinutes;
      selectedModifiers = session.getDefaultWorkoutModifiers();
    }
    renderDuration(selectedMinutes, false);
    renderWorkoutModifiers();
    startupControls?.setSelection(selectedMinutes, selectedModifiers);
    performance.mark?.("nomadic-method-session-ready");

    if (session.state.workoutCompleted && !session.state.completionAcknowledged) {
      cancelQueuedWorkoutStart();
      showCompletion(false);
    } else if (pendingRestGroup) {
      cancelQueuedWorkoutStart();
      restorePendingRest();
    } else if (pendingMovementGroup) {
      cancelQueuedWorkoutStart();
      restorePendingMovement();
    } else if (session.state.activeWorkoutMinutes !== 0) {
      cancelQueuedWorkoutStart();
      showNextExercise();
    } else {
      showDuration({ preserveSelection: startupSelectionChanged });
    }
  } catch (error) {
    console.error(error);
    elements.beginWorkout.disabled = true;
    elements.status.textContent = "Nomadic Method is unavailable.";
    startupControls?.fail("Nomadic Method is unavailable.");
  }
}

function bindEvents() {
  document.getElementById("end-session").addEventListener("click", () => {
    if (activeWorkoutSetup && !elements.beginWorkout.disabled) {
      const dialog = document.getElementById("end-session-dialog");
      dialog.returnValue = "cancel";
      dialog.showModal();
    }
  });
  document.getElementById("end-session-dialog").addEventListener("close", (event) => {
    if (event.target.returnValue === "end") endActiveWorkout();
  });
  if (startupControls) {
    startupControls.connect({
      selectionChanged(nextSelection, userInitiated) {
        selectedMinutes = nextSelection.selectedMinutes;
        selectedModifiers = nextSelection.selectedModifiers;
        if (userInitiated && !session) {
          startupSelectionChanged = true;
        }
        if (userInitiated) {
          renderWorkoutModifiers();
        }
        if (userInitiated &&
            (session?.state.activeWorkoutMinutes === 0 || activeWorkoutSetup)) {
          queueWorkoutPreparation();
        }
      },
      startRequested: startWorkout,
    });
  } else {
    elements.durationDecrease.addEventListener("click", () => stepDuration(-1));
    elements.durationIncrease.addEventListener("click", () => stepDuration(1));
    elements.durationRange.addEventListener("input", () => {
      selectDurationByIndex(Number(elements.durationRange.value), true);
    });
    elements.beginWorkout.addEventListener("click", startWorkout);
    for (const { element, flag } of workoutModifierTiles()) {
      element.addEventListener("click", () => toggleWorkoutModifier(flag));
    }
    elements.wallModifier.addEventListener("click", cycleWallEquipment);
    elements.locomotionModifier.addEventListener("click", cycleLocomotionSpace);
    elements.mirrorModifier.addEventListener("click", cycleMirrorEquipment);
    document.getElementById("elevation-equipment").addEventListener("click", cycleElevationEquipment);
  }
  elements.shuffleExercise.addEventListener("click", shuffleCurrentExercise);
  elements.workoutSetup.addEventListener("click", showActiveWorkoutSetup);
  elements.exerciseName.addEventListener(
    "pointerdown",
    beginExerciseNameLongPress,
  );
  elements.exerciseName.addEventListener(
    "pointermove",
    moveExerciseNameLongPress,
  );
  for (const eventName of ["pointerup", "pointercancel"]) {
    elements.exerciseName.addEventListener(
      eventName,
      finishExerciseNameLongPress,
    );
  }
  elements.exerciseName.addEventListener(
    "contextmenu",
    copyExerciseNameFromContextMenu,
  );
  elements.startMovement.addEventListener("click", startMovement);
  elements.repeatExercise.addEventListener("click", repeatMovement);
  elements.playbackToggle.addEventListener("click", toggleMovementPlayback);
  elements.nextExercise.addEventListener("click", goToNextExercise);
  elements.restPlaybackToggle.addEventListener("click", toggleRestPlayback);

  elements.mediaRetry.addEventListener("click", retryMedia);
  elements.doneButton.addEventListener("click", closeCompletion);
  document.addEventListener("visibilitychange", handleVisibilityChange);
  window.addEventListener("pagehide", handlePageHide);
}

function byId(id) {
  const element = document.getElementById(id);
  if (!element) {
    throw new Error(`Missing element #${id}.`);
  }
  return element;
}

function beginExerciseNameLongPress(event) {
  if (event.pointerType !== "touch" && event.pointerType !== "pen") {
    return;
  }

  cancelExerciseNameLongPress();
  const pointerId = event.pointerId;
  exerciseNameLongPress = {
    pointerId,
    startX: event.clientX,
    startY: event.clientY,
    timer: setTimeout(() => {
      if (exerciseNameLongPress?.pointerId !== pointerId) {
        return;
      }
      exerciseNameLongPress = null;
      requestExerciseNameCopy();
    }, EXERCISE_NAME_LONG_PRESS_MS),
  };
}

function moveExerciseNameLongPress(event) {
  if (exerciseNameLongPress?.pointerId !== event.pointerId) {
    return;
  }

  const distance = Math.hypot(
    event.clientX - exerciseNameLongPress.startX,
    event.clientY - exerciseNameLongPress.startY,
  );
  if (distance > EXERCISE_NAME_LONG_PRESS_MOVE_TOLERANCE_PX) {
    cancelExerciseNameLongPress();
  }
}

function finishExerciseNameLongPress(event) {
  if (exerciseNameLongPress?.pointerId === event.pointerId) {
    cancelExerciseNameLongPress();
  }
}

function cancelExerciseNameLongPress() {
  clearTimeout(exerciseNameLongPress?.timer);
  exerciseNameLongPress = null;
}

function copyExerciseNameFromContextMenu(event) {
  event.preventDefault();
  cancelExerciseNameLongPress();
  requestExerciseNameCopy();
}

function requestExerciseNameCopy() {
  const now = performance.now();
  if (now - lastExerciseNameCopyRequestAt < EXERCISE_NAME_LONG_PRESS_MS) {
    return;
  }
  lastExerciseNameCopyRequestAt = now;
  void copyDisplayedExerciseName();
}

async function copyDisplayedExerciseName() {
  const exerciseName = elements.exerciseName.textContent.trim();
  if (!exerciseName) {
    return;
  }

  try {
    await writeClipboardText(exerciseName);
    elements.status.textContent = `${exerciseName} copied.`;
  } catch (error) {
    console.error("Unable to copy exercise name.", error);
    elements.status.textContent = "Unable to copy exercise name.";
  }
}

async function writeClipboardText(text) {
  if (navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return;
    } catch {
      // A touch long-press can outlive transient clipboard permission on some
      // browsers. Continue with the gesture-scoped selection fallback.
    }
  }

  const helper = document.createElement("textarea");
  helper.value = text;
  helper.readOnly = true;
  helper.style.position = "fixed";
  helper.style.left = "-9999px";
  document.body.append(helper);
  helper.select();
  helper.setSelectionRange(0, helper.value.length);
  const copied = document.execCommand("copy");
  helper.remove();
  if (!copied) {
    throw new Error("The browser rejected the clipboard operation.");
  }
}

function loadState() {
  try {
    return parseStoredState(localStorage.getItem(STORAGE_KEY));
  } catch {
    return parseStoredState(null);
  }
}

function persistState() {
  if (!session) {
    return;
  }
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session.state));
  } catch (error) {
    console.error("Unable to save Nomadic Method state.", error);
  }
}

function workoutMinuteOptions() {
  return getWorkoutMinuteOptions(activeWorkoutSetup ? session?.state.activeWorkoutMinutes : 0);
}

function stepDuration(direction) {
  const currentIndex = workoutMinuteOptions().indexOf(selectedMinutes);
  selectDurationByIndex(
    Math.max(0, Math.min(workoutMinuteOptions().length - 1, currentIndex + direction)),
    true,
  );
}

function selectDurationByIndex(index, userInitiated) {
  const minutes = workoutMinuteOptions()[index];
  if (minutes !== undefined) {
    renderDuration(minutes, userInitiated);
  }
}

function renderDuration(minutes, userInitiated) {
  const previousMinutes = selectedMinutes;
  minutes = normalizeSetupMinutes(minutes, activeWorkoutSetup ? session?.state.activeWorkoutMinutes : 0);
  if (activeWorkoutSetup && session)
    minutes = Math.max(minutes, session.getMinimumActiveWorkoutMinutes());
  selectedMinutes = minutes;
  const index = workoutMinuteOptions().indexOf(minutes);
  const progress = `${(index / (workoutMinuteOptions().length - 1)) * 100}%`;

  elements.durationValue.value = String(minutes);
  elements.durationValue.textContent = String(minutes);
  elements.durationValue.setAttribute("aria-label", `${minutes} minutes selected`);
  const options = workoutMinuteOptions();
  elements.durationRange.max = String(options.length - 1);
  if (elements.durationLabels.length !== options.length) {
    const container = byId("duration-labels");
    container.replaceChildren(...options.map(value => { const label = document.createElement("span"); label.textContent = value; return label; }));
    elements.durationLabels = [...container.children];
  }
  byId("duration-labels").style.setProperty("--duration-option-count", options.length);
  elements.durationRange.value = String(index);
  elements.durationRange.style.setProperty("--range-progress", progress);
  elements.durationRange.setAttribute(
    "aria-valuetext",
    `${minutes} minutes. Options: ${options.join(", ")} minutes`,
  );
  elements.durationDecrease.disabled = activeWorkoutSetup && session
    ? minutes <= session.getMinimumActiveWorkoutMinutes() : index === 0;
  elements.durationIncrease.disabled = index === workoutMinuteOptions().length - 1;
  elements.beginWorkout.setAttribute("aria-label", `Start a ${minutes} minute workout`);

  elements.durationLabels.forEach((label, labelIndex) => {
    label.classList.toggle("selected", labelIndex === index);
  });
  renderLightModeCountdown();

  if (userInitiated && previousMinutes !== minutes) {
    if (!session) {
      startupSelectionChanged = true;
    }
    elements.durationDial.classList.remove("pulse");
    requestAnimationFrame(() => elements.durationDial.classList.add("pulse"));
    queueWorkoutPreparation();
  }
}

function workoutModifierTiles() {
  return [
    { element: elements.floorModifier, flag: WORKOUT_MODIFIERS.Floor },
    {
      element: elements.upperBodyClothingModifier,
      flag: WORKOUT_MODIFIERS.UpperBodyClothing,
      enabledLabel: "Upper-body clothing: worn",
      disabledLabel: "Upper-body clothing: not worn",
    },
    {
      element: elements.hardFloorModifier,
      flag: WORKOUT_MODIFIERS.HardFloor,
    },
    { element: elements.insectModifier, flag: WORKOUT_MODIFIERS.Insect },
    {
      element: elements.silenceModifier,
      flag: WORKOUT_MODIFIERS.Silence,
      enabledLabel: "Quiet exercise filter: quiet exercises only",
      disabledLabel: "Quiet exercise filter: noisy exercises allowed",
    },
    {
      element: elements.shyModifier,
      flag: WORKOUT_MODIFIERS.Shy,
      enabledLabel: "Shy mode: less conspicuous exercises only",
      disabledLabel: "Shy mode: off; all exercises allowed",
    },
    {
      element: elements.lightModifier,
      flag: WORKOUT_MODIFIERS.Light,
      enabledLabel: "Workout intensity: light workout",
      disabledLabel: "Workout intensity: regular workout",
    },
  ];
}

function toggleWorkoutModifier(flag) {
  if (flag === WORKOUT_MODIFIERS.Light &&
      (isRecoveryLightModeActive() || isAutomaticLightModeLocked())) {
    renderWorkoutModifiers();
    showWorkoutModifierFeedback(MODIFIER_FEEDBACK_LABELS.lightLocked,
      isAutomaticLightModeLocked() ? "Training cycle complete" : "Muscles recovering");
    return;
  }
  selectedModifiers ^= flag;
  if (!session) {
    startupSelectionChanged = true;
  }
  renderWorkoutModifiers();
  const enabled = (selectedModifiers & flag) !== 0;
  showWorkoutModifierFeedback(workoutModifierFeedbackLabel(flag, enabled));
  queueWorkoutPreparation();
}

function workoutModifierFeedbackLabel(flag, enabled) {
  if (flag === WORKOUT_MODIFIERS.Floor)
    return MODIFIER_FEEDBACK_LABELS[enabled ? "floorEnabled" : "floorDisabled"];
  if (flag === WORKOUT_MODIFIERS.UpperBodyClothing) {
    return MODIFIER_FEEDBACK_LABELS[
      enabled ? "upperBodyClothingEnabled" : "upperBodyClothingDisabled"
    ];
  }
  if (flag === WORKOUT_MODIFIERS.HardFloor) {
    return MODIFIER_FEEDBACK_LABELS[
      enabled ? "hardFloorEnabled" : "hardFloorDisabled"
    ];
  }
  if (flag === WORKOUT_MODIFIERS.Insect) {
    return MODIFIER_FEEDBACK_LABELS[
      enabled ? "insectEnabled" : "insectDisabled"
    ];
  }
  if (flag === WORKOUT_MODIFIERS.Silence) {
    return MODIFIER_FEEDBACK_LABELS[
      enabled ? "noisyDisabled" : "noisyEnabled"
    ];
  }
  if (flag === WORKOUT_MODIFIERS.Shy) {
    return MODIFIER_FEEDBACK_LABELS[
      enabled ? "shyEnabled" : "shyDisabled"
    ];
  }
  if (flag === WORKOUT_MODIFIERS.Light) {
    return MODIFIER_FEEDBACK_LABELS[
      enabled ? "lightEnabled" : "lightDisabled"
    ];
  }
  throw new RangeError(`Unknown workout modifier: ${flag}`);
}

function cycleLocomotionSpace() {
  const current = getLocomotionSpace(selectedModifiers);
  const next = current === LOCOMOTION_SPACE.None ? LOCOMOTION_SPACE.Room
    : current === LOCOMOTION_SPACE.Room ? LOCOMOTION_SPACE.Field : LOCOMOTION_SPACE.None;
  selectedModifiers = withLocomotionSpace(selectedModifiers, next);
  if (!session) startupSelectionChanged = true;
  renderWorkoutModifiers();
  showWorkoutModifierFeedback(locomotionFeedbackLabel(next));
  queueWorkoutPreparation();
}

function locomotionFeedbackLabel(space) {
  return space === LOCOMOTION_SPACE.Room ? "locomotion: room length"
    : space === LOCOMOTION_SPACE.Field ? "locomotion: field size" : "locomotion OFF";
}

function cycleWallEquipment() {
  const currentEquipment = getWallEquipment(selectedModifiers);
  const nextEquipment = currentEquipment === WALL_EQUIPMENT.None
    ? WALL_EQUIPMENT.SolesMayTouch
    : currentEquipment === WALL_EQUIPMENT.SolesMayTouch
      ? WALL_EQUIPMENT.SolesStayOff
      : WALL_EQUIPMENT.None;
  selectedModifiers = withWallEquipment(selectedModifiers, nextEquipment);
  if (!session) {
    startupSelectionChanged = true;
  }
  renderWorkoutModifiers();
  showWorkoutModifierFeedback(wallEquipmentFeedbackLabel(nextEquipment));
  queueWorkoutPreparation();
}

function wallEquipmentFeedbackLabel(equipment) {
  if (equipment === WALL_EQUIPMENT.SolesStayOff) {
    return MODIFIER_FEEDBACK_LABELS.wallSolesStayOff;
  }
  if (equipment === WALL_EQUIPMENT.SolesMayTouch) {
    return MODIFIER_FEEDBACK_LABELS.wallSolesMayTouch;
  }
  return MODIFIER_FEEDBACK_LABELS.wallDisabled;
}

function cycleMirrorEquipment() {
  const nextEquipment = getMirrorEquipment(selectedModifiers) === MIRROR_EQUIPMENT.None
    ? MIRROR_EQUIPMENT.Tall
    : getMirrorEquipment(selectedModifiers) === MIRROR_EQUIPMENT.Tall
      ? MIRROR_EQUIPMENT.Compact
      : MIRROR_EQUIPMENT.None;
  selectedModifiers = withMirrorEquipment(selectedModifiers, nextEquipment);
  if (!session) {
    startupSelectionChanged = true;
  }
  renderWorkoutModifiers();
  showWorkoutModifierFeedback(mirrorEquipmentFeedbackLabel(nextEquipment));
  queueWorkoutPreparation();
}

function mirrorEquipmentFeedbackLabel(equipment) {
  if (equipment === MIRROR_EQUIPMENT.Compact) {
    return MODIFIER_FEEDBACK_LABELS.compactMirrorEnabled;
  }
  if (equipment === MIRROR_EQUIPMENT.Tall) {
    return MODIFIER_FEEDBACK_LABELS.tallMirrorEnabled;
  }
  return MODIFIER_FEEDBACK_LABELS.mirrorDisabled;
}

function showWorkoutModifierFeedback(message, detail = "") {
  clearTimeout(modifierFeedbackTimer);
  elements.modifierFeedback.classList.remove("show");
  elements.modifierFeedback.hidden = false;
  elements.modifierFeedback.textContent = message;
  elements.modifierFeedback.setAttribute("data-detail", detail);
  elements.modifierFeedback.setAttribute("aria-label", detail ? `${message}: ${detail}` : message);
  void elements.modifierFeedback.offsetWidth;
  elements.modifierFeedback.classList.add("show");
  modifierFeedbackTimer = setTimeout(() => {
    elements.modifierFeedback.classList.remove("show");
    elements.modifierFeedback.hidden = true;
    modifierFeedbackTimer = null;
  }, detail ? 3240 : MODIFIER_FEEDBACK_DURATION_MS);
}

function elevationFeedbackLabel() {
  const equipment = getElevationEquipment(selectedModifiers);
  return equipment === ELEVATION_EQUIPMENT.None ? "equipment OFF: elevation"
    : equipment === ELEVATION_EQUIPMENT.Support ? "equipment ON: support"
    : "equipment ON: jump platform";
}

function cycleElevationEquipment() {
  const current = getElevationEquipment(selectedModifiers);
  const next = current === ELEVATION_EQUIPMENT.None ? ELEVATION_EQUIPMENT.Support
    : current === ELEVATION_EQUIPMENT.Support ? ELEVATION_EQUIPMENT.JumpPlatform
    : ELEVATION_EQUIPMENT.None;
  selectedModifiers = withElevationEquipment(selectedModifiers, next);
  if (!session) startupSelectionChanged = true;
  renderWorkoutModifiers();
  showWorkoutModifierFeedback(elevationFeedbackLabel());
  queueWorkoutPreparation();
}

function renderElevationEquipment() {
  const element = document.getElementById("elevation-equipment");
  const equipment = getElevationEquipment(selectedModifiers);
  element.setAttribute("aria-pressed", String(equipment !== ELEVATION_EQUIPMENT.None));
  element.dataset.elevationEquipment = equipment === ELEVATION_EQUIPMENT.JumpPlatform ? "platform"
    : equipment === ELEVATION_EQUIPMENT.Support ? "support" : "none";
  element.setAttribute("aria-label", elevationFeedbackLabel());
  element.setAttribute("title", elevationFeedbackLabel());
}

function renderWorkoutModifiers() {
  renderElevationEquipment();
  const automatic = isAutomaticLightModeLocked();
  if (automatic) {
    selectedModifiers |= WORKOUT_MODIFIERS.Light;
  } else if (automaticLightModePresented && !activeWorkoutSetup) {
    selectedModifiers &= ~WORKOUT_MODIFIERS.Light;
  }
  automaticLightModePresented = automatic;
  for (const { element, flag, enabledLabel, disabledLabel } of
    workoutModifierTiles()) {
    const explicitlyEnabled = (selectedModifiers & flag) !== 0;
    const recoveryLightMode = flag === WORKOUT_MODIFIERS.Light &&
      isRecoveryLightModeActive();
    const automaticLightMode = flag === WORKOUT_MODIFIERS.Light &&
      isAutomaticLightModeLocked();
    const enabled = explicitlyEnabled || recoveryLightMode ||
      automaticLightMode;
    element.setAttribute("aria-pressed", String(enabled));
    element.setAttribute("title", workoutModifierFeedbackLabel(flag, enabled));
    if (flag === WORKOUT_MODIFIERS.Light) {
      const locked = recoveryLightMode || automaticLightMode;
      // aria-disabled describes the unavailable toggle, while taps/keyboard
      // activation can still explain why it is locked.
      element.disabled = false;
      element.setAttribute("aria-disabled", String(locked));
      if (locked) element.setAttribute("title", MODIFIER_FEEDBACK_LABELS.lightLocked);
    }
    if (flag === WORKOUT_MODIFIERS.Floor) {
      const hard = (selectedModifiers & WORKOUT_MODIFIERS.HardFloor) !== 0;
      const clothing = (selectedModifiers & WORKOUT_MODIFIERS.UpperBodyClothing) !== 0;
      element.dataset.floorPresentation = enabled ? "on" : "off";
      element.setAttribute("aria-label", !enabled ? "Floor contact: feet only"
        : hard ? "Floor exercises allowed; no knee, elbow, head, torso, or shoulder contact"
          : clothing ? "Floor exercises allowed"
            : "Floor exercises allowed; no torso or shoulder contact");
    } else if (flag === WORKOUT_MODIFIERS.HardFloor) {
      element.dataset.hardFloor = enabled ? "hard" : "soft";
      element.setAttribute(
        "aria-label",
        enabled
          ? "Floor surface: hard and slippery floor"
          : "Floor surface: stable soft floor",
      );
    } else if (enabledLabel && disabledLabel) {
      element.setAttribute("aria-label", enabled ? enabledLabel : disabledLabel);
    }
  }

  renderLightModeCountdown();

  const wallEquipment = getWallEquipment(selectedModifiers);
  const wallEnabled = wallEquipment !== WALL_EQUIPMENT.None;
  elements.wallModifier.setAttribute("aria-pressed", String(wallEnabled));
  elements.wallModifier.setAttribute(
    "title",
    wallEquipmentFeedbackLabel(wallEquipment),
  );
  elements.wallModifier.setAttribute(
    "aria-label",
    wallEquipment === WALL_EQUIPMENT.None
      ? "Wall equipment: no wall available"
      : wallEquipment === WALL_EQUIPMENT.SolesStayOff
        ? "Wall equipment: wall available; no feet on wall"
        : "Wall equipment: wall available; feet on wall allowed",
  );
  elements.wallModifier.dataset.wallEquipment =
    wallEquipment === WALL_EQUIPMENT.None
      ? "none"
      : wallEquipment === WALL_EQUIPMENT.SolesStayOff
        ? "soles-stay-off"
        : "soles-may-touch";

  const mirrorEquipment = getMirrorEquipment(selectedModifiers);
  const mirrorEnabled = mirrorEquipment !== MIRROR_EQUIPMENT.None;
  elements.mirrorModifier.setAttribute("aria-pressed", String(mirrorEnabled));
  elements.mirrorModifier.setAttribute(
    "title",
    mirrorEquipmentFeedbackLabel(mirrorEquipment),
  );
  elements.mirrorModifier.setAttribute(
    "aria-label",
    mirrorEquipment === MIRROR_EQUIPMENT.None
      ? "Mirror equipment: no mirror available"
      : `Mirror equipment: ${mirrorEquipment.toLowerCase()} mirror available`,
  );
  elements.mirrorModifier.dataset.mirrorEquipment = mirrorEquipment.toLowerCase();
  const space = getLocomotionSpace(selectedModifiers);
  elements.locomotionModifier.dataset.locomotionSpace = space.toLowerCase();
  elements.locomotionModifier.setAttribute("aria-pressed", String(space !== LOCOMOTION_SPACE.None));
  elements.locomotionModifier.setAttribute("aria-label", `Locomotion space: ${locomotionFeedbackLabel(space)}`);
  elements.locomotionModifier.setAttribute("title", locomotionFeedbackLabel(space));
}

function renderLightModeCountdown() {
  const recoveryLightMode = isRecoveryLightModeActive();
  const automaticLightMode = isAutomaticLightModeLocked();
  const enabled = (selectedModifiers & WORKOUT_MODIFIERS.Light) !== 0 ||
    recoveryLightMode || automaticLightMode;
  const candidateWorkoutsRemaining = session
    ? session.getWorkoutsUntilLightWorkout(selectedMinutes)
    : startupControls?.lightWorkoutsRemaining;
  const maximumWorkoutCount = Math.ceil(
    LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT / Math.min(...NEW_WORKOUT_MINUTES));
  const workoutsRemaining = Number.isInteger(candidateWorkoutsRemaining) &&
      candidateWorkoutsRemaining >= 0 &&
      candidateWorkoutsRemaining <= maximumWorkoutCount
    ? candidateWorkoutsRemaining
    : Math.ceil(LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT /
      Math.min(selectedMinutes, LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP));
  if (startupControls?.lightWorkoutsRemaining !== workoutsRemaining) {
    startupControls?.setLightWorkoutsRemaining?.(workoutsRemaining);
  }
  elements.lightCountdown.textContent = String(workoutsRemaining);
  elements.lightCountdown.hidden = enabled;
  startupControls?.setRecoveryLightMode?.(recoveryLightMode);
  startupControls?.setAutomaticLightMode?.(automaticLightMode);
  if (automaticLightMode) {
    elements.lightModifier.setAttribute(
      "aria-label",
      "Workout intensity: automatic light mode is required today",
    );
    return;
  }
  if (recoveryLightMode) {
    elements.lightModifier.setAttribute(
      "aria-label",
      "Workout intensity: effectively light while muscles recover",
    );
    return;
  }
  if (!enabled) {
    const scheduleDescription =
      `Approximately ${workoutsRemaining} workout${workoutsRemaining === 1
        ? ""
        : "s"} at the selected duration until automatic light mode.`;
    elements.lightModifier.setAttribute(
      "aria-label",
      `Workout intensity: regular workout. ${scheduleDescription}`,
    );
  }
}

function isRecoveryLightModeActive() {
  return session?.getRecoveryLightStatus(selectedModifiers).isActive === true;
}

function isAutomaticLightModeLocked() {
  return session
    ? session.isAutomaticLightDayDue()
    : startupControls?.automaticLightMode === true;
}

function showScreen(screen) {
  if (screen === "duration" && session)
    fitnessProfileView.setProfile(inspectFitnessProfile(exerciseCatalog, session.state, fitnessRatings));
  elements.durationScreen.hidden = screen !== "duration";
  elements.workoutScreen.hidden = screen !== "workout";
  elements.completionScreen.hidden = screen !== "completion";
}

function showDuration({ preserveSelection = false } = {}) {
  activeWorkoutSetup = false;
  workoutSetupCurrentGroupId = null;
  elements.durationScreen.classList.remove("active-workout-setup");
  startupControls?.setActiveWorkoutSetup(false);
  releaseWakeLock();
  stopRuntimeTimers();
  clearExerciseMedia();
  resetMovementVisuals();
  currentGroup = null;
  currentExercise = null;
  if (!preserveSelection) {
    selectedMinutes = session?.state.lastWorkoutMinutes ?? selectedMinutes;
    selectedModifiers = session?.getDefaultWorkoutModifiers() ?? selectedModifiers;
  }
  renderDuration(selectedMinutes, false);
  renderWorkoutModifiers();
  startupControls?.setSelection(selectedMinutes, selectedModifiers);
  queueWorkoutPreparation();
  elements.beginWorkout.disabled = false;
  startupControls?.markReady();
  showScreen("duration");
  if (startWorkoutWhenReady) {
    startWorkoutWhenReady = false;
    startupControls?.consumeStartRequest();
    elements.beginWorkout.disabled = false;
    startWorkout();
  }
}

function showActiveWorkoutSetup() {
  if (activeWorkoutSetup || !session || !currentGroup ||
      session.state.activeWorkoutMinutes === 0 ||
      session.state.workoutCompleted) {
    return;
  }

  workoutSetupCurrentGroupId = currentGroup.id;
  workoutSetupShouldResume = false;
  if (restActive) {
    workoutSetupReturnPhase = "rest";
    workoutSetupShouldResume = !session.state.pendingRestPausedByUser;
    if (workoutSetupShouldResume) {
      const remaining = session.getPendingRestMillisecondsRemaining(Date.now());
      if (remaining <= 0) {
        completeRest();
        return;
      }
      session.pauseRest(currentGroup, remaining);
      persistState();
    }
    clearInterval(restTimer);
    restTimer = null;
  } else if (movementRunning || movementPauseReason) {
    workoutSetupReturnPhase = "movement";
    workoutSetupShouldResume = movementRunning;
    if (movementRunning) {
      pauseMovement("setup");
    }
  } else {
    workoutSetupReturnPhase = "ready";
  }

  elements.video.pause();
  releaseWakeLock();
  activeWorkoutSetup = true;
  selectedMinutes = session.state.activeWorkoutMinutes;
  selectedModifiers = session.state.activeWorkoutModifiers;
  renderDuration(selectedMinutes, false);
  renderWorkoutModifiers();
  startupControls?.setActiveWorkoutSetup(true, session.getMinimumActiveWorkoutMinutes(), session.state.activeWorkoutMinutes);
  startupControls?.setSelection(selectedMinutes, selectedModifiers);
  elements.durationScreen.classList.add("active-workout-setup");
  if (!startupControls) {
    elements.durationDecrease.disabled = selectedMinutes <= session.getMinimumActiveWorkoutMinutes();
    elements.durationIncrease.disabled = selectedMinutes === workoutMinuteOptions().at(-1);
    elements.durationRange.disabled = false;
    elements.beginWorkout.setAttribute("aria-label", "Resume workout");
  }
  elements.beginWorkout.disabled = false;
  startupControls?.markReady();
  showScreen("duration");
  queueWorkoutPreparation();
}

function endActiveWorkout() {
  if (!activeWorkoutSetup || elements.beginWorkout.disabled) return;
  elements.beginWorkout.disabled = true;
  const button = document.getElementById("end-session");
  button.disabled = true;
  cancelWorkoutPreparation();
  const state = cloneWorkoutState(session.state);
  try {
    const next = new WorkoutSession(exerciseCatalog, state, undefined, undefined, fitnessRatings);
    next.endActiveWorkout();
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next.state));
    session = next;
    activeWorkoutSetup = false;
    workoutSetupCurrentGroupId = null;
    workoutSetupShouldResume = false;
    elements.durationScreen.classList.remove("active-workout-setup");
    startupControls?.setActiveWorkoutSetup(false);
    stopRuntimeTimers();
    cancelQueuedWorkoutStart();
    showDuration();
  } catch (error) {
    console.error(error);
    elements.status.textContent = "Could not end the session. Your progress is safe.";
  } finally {
    elements.beginWorkout.disabled = false;
    button.disabled = false;
  }
}

function restoreWorkoutAfterSetup() {
  const returnPhase = workoutSetupReturnPhase;
  const shouldResume = workoutSetupShouldResume;
  activeWorkoutSetup = false;
  elements.durationScreen.classList.remove("active-workout-setup");
  startupControls?.setActiveWorkoutSetup(false);

  if (returnPhase === "rest") {
    const pendingGroup = session.getPendingRestGroup();
    if (!pendingGroup) {
      throw new Error("The paused rest could not be restored.");
    }
    if (shouldResume && session.state.pendingRestPausedByUser) {
      const now = Date.now();
      const remaining = session.getPendingRestMillisecondsRemaining(now);
      session.resumeRest(pendingGroup, now + remaining);
      persistState();
    }
    restorePendingRest();
  } else if (returnPhase === "movement") {
    if (!restorePendingMovement()) {
      // The modifier profile selected a different current exercise. Present
      // its replacement from Ready instead of restoring the obsolete
      // countdown.
      showNextExercise();
    }
  } else {
    showNextExercise();
  }

  workoutSetupCurrentGroupId = null;
  workoutSetupShouldResume = false;
}

function queueWorkoutPreparation(
  minutes = selectedMinutes,
  modifiers = selectedModifiers,
) {
  const isReconfiguration = activeWorkoutSetup;
  const currentWorkoutGroupId = workoutSetupCurrentGroupId;
  if (!session || !exerciseCatalog ||
      (isReconfiguration
        ? !currentWorkoutGroupId || session.state.activeWorkoutMinutes === 0
        : session.state.activeWorkoutMinutes !== 0)) {
    return null;
  }
  if (preparedWorkout?.minutes === minutes &&
      preparedWorkout.modifiers === modifiers &&
      preparedWorkout.isReconfiguration === isReconfiguration &&
      preparedWorkout.currentWorkoutGroupId === currentWorkoutGroupId) {
    return Promise.resolve(preparedWorkout);
  }
  if (workoutPreparationPromise &&
      preparingWorkoutMinutes === minutes &&
      preparingWorkoutModifiers === modifiers &&
      preparingWorkoutIsReconfiguration === isReconfiguration &&
      preparingCurrentWorkoutGroupId === currentWorkoutGroupId) {
    return workoutPreparationPromise;
  }

  cancelWorkoutPreparation();
  const generation = ++workoutPreparationGeneration;
  workoutPreparationError = null;
  preparingWorkoutMinutes = minutes;
  preparingWorkoutModifiers = modifiers;
  preparingWorkoutIsReconfiguration = isReconfiguration;
  preparingCurrentWorkoutGroupId = currentWorkoutGroupId;
  workoutPreparationPromise = new Promise((resolve) => {
    workoutPreparationResolve = resolve;
  });
  let fallbackStarted = false;

  const complete = (result) => {
    if (generation !== workoutPreparationGeneration) {
      return;
    }
    workoutPreparationWorker?.terminate();
    workoutPreparationWorker = null;
    preparedWorkout = result;
    if (result) {
      performance.mark?.("nomadic-method-workout-prepared");
    }
    workoutPreparationResolve?.(result);
    workoutPreparationResolve = null;
    workoutPreparationPromise = null;
  };
  const prepareOnMainThread = () => {
    if (fallbackStarted) {
      return;
    }
    fallbackStarted = true;
    setTimeout(() => {
      if (generation !== workoutPreparationGeneration) {
        return;
      }
      try {
        const preparedSession = new WorkoutSession(
          exerciseCatalog,
          cloneWorkoutState(session.state),
          undefined,
          undefined,
          fitnessRatings,
        );
        if (isReconfiguration) {
          preparedSession.reconfigureActiveWorkout(
            modifiers,
            currentWorkoutGroupId,
          );
          preparedSession.resizeActiveWorkout(minutes);
        } else {
          preparedSession.prepareWorkout(minutes, modifiers);
        }
        complete({
          minutes,
          modifiers,
          state: preparedSession.state,
          isReconfiguration,
          currentWorkoutGroupId,
        });
      } catch (error) {
        console.error(error);
        workoutPreparationError = error instanceof Error ? error.message : String(error);
        complete(null);
      }
    }, 0);
  };

  try {
    workoutPreparationWorker = new Worker(
      new URL("./workout-preparation-worker.js", import.meta.url),
      { type: "module" },
    );
    workoutPreparationWorker.addEventListener("message", (event) => {
      if (event.data?.generation !== generation) {
        return;
      }
      if (event.data.error) {
        if (event.data.error.startsWith(FITNESS_UNAVAILABLE_MESSAGE)) {
          workoutPreparationError = event.data.error;
          complete(null);
          return;
        }
        console.warn(`Background workout preparation failed: ${event.data.error}`);
        workoutPreparationWorker?.terminate();
        workoutPreparationWorker = null;
        prepareOnMainThread();
        return;
      }
      complete({
        minutes,
        modifiers,
        state: event.data.state,
        isReconfiguration,
        currentWorkoutGroupId,
      });
    });
    workoutPreparationWorker.addEventListener("error", (error) => {
      if (generation !== workoutPreparationGeneration) {
        return;
      }
      console.warn("Background workout preparation failed.", error);
      workoutPreparationWorker?.terminate();
      workoutPreparationWorker = null;
      prepareOnMainThread();
    }, { once: true });
    workoutPreparationWorker.postMessage({
      generation,
      exercises: exerciseCatalog,
      fitnessRatings,
      state: session.state,
      minutes,
      modifiers,
      mode: isReconfiguration ? "reconfigure" : "prepare",
      currentWorkoutGroupId,
    });
  } catch (error) {
    console.warn("Background workout preparation is unavailable.", error);
    prepareOnMainThread();
  }
  return workoutPreparationPromise;
}

async function ensureWorkoutPrepared(
  minutes,
  modifiers,
  isReconfiguration = activeWorkoutSetup,
  currentWorkoutGroupId = workoutSetupCurrentGroupId,
) {
  if (preparedWorkout?.minutes === minutes &&
      preparedWorkout.modifiers === modifiers &&
      preparedWorkout.isReconfiguration === isReconfiguration &&
      preparedWorkout.currentWorkoutGroupId === currentWorkoutGroupId) {
    return preparedWorkout;
  }
  if (!workoutPreparationPromise ||
      preparingWorkoutMinutes !== minutes ||
      preparingWorkoutModifiers !== modifiers ||
      preparingWorkoutIsReconfiguration !== isReconfiguration ||
      preparingCurrentWorkoutGroupId !== currentWorkoutGroupId) {
    queueWorkoutPreparation(minutes, modifiers);
  }
  return workoutPreparationPromise
    ? await workoutPreparationPromise
    : null;
}

function cancelWorkoutPreparation() {
  workoutPreparationGeneration += 1;
  workoutPreparationWorker?.terminate();
  workoutPreparationWorker = null;
  workoutPreparationResolve?.(null);
  workoutPreparationResolve = null;
  workoutPreparationPromise = null;
  preparedWorkout = null;
  preparingWorkoutMinutes = 0;
  preparingWorkoutModifiers = WORKOUT_MODIFIERS.None;
  preparingWorkoutIsReconfiguration = false;
  preparingCurrentWorkoutGroupId = null;
}

function cloneWorkoutState(state) {
  return typeof structuredClone === "function"
    ? structuredClone(state)
    : JSON.parse(JSON.stringify(state));
}

async function startWorkout() {
  performance.mark?.("nomadic-method-workout-start-requested");
  if (!session) {
    startWorkoutWhenReady = true;
    elements.beginWorkout.disabled = true;
    return;
  }
  const queuedByStartupControls =
    startupControls?.consumeStartRequest() ?? false;
  if (elements.beginWorkout.disabled && !queuedByStartupControls) {
    return;
  }
  elements.beginWorkout.disabled = true;
  try {
    const minutes = selectedMinutes;
    const modifiers = selectedModifiers;
    const isReconfiguration = activeWorkoutSetup;
    const currentWorkoutGroupId = workoutSetupCurrentGroupId;
    const prepared = await ensureWorkoutPrepared(
      minutes,
      modifiers,
      isReconfiguration,
      currentWorkoutGroupId,
    );
    if (!prepared) {
      throw new Error(workoutPreparationError ?? "The selected workout could not be prepared.");
    }
    if (minutes !== selectedMinutes || modifiers !== selectedModifiers ||
        isReconfiguration !== activeWorkoutSetup || currentWorkoutGroupId !== workoutSetupCurrentGroupId) {
      elements.beginWorkout.disabled = false;
      return;
    }
    cancelWorkoutPreparation();
    const nextSession = new WorkoutSession(exerciseCatalog, prepared.state, undefined, undefined, fitnessRatings);
    if (prepared.isReconfiguration) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(nextSession.state));
      session = nextSession;
      restoreWorkoutAfterSetup();
      return;
    }
    nextSession.activatePreparedWorkout();
    localStorage.setItem(STORAGE_KEY, JSON.stringify(nextSession.state));
    session = nextSession;
    showNextExercise();
    performance.mark?.("nomadic-method-workout-visible");
    performance.measure?.(
      "nomadic-method-workout-start-latency",
      "nomadic-method-workout-start-requested",
      "nomadic-method-workout-visible",
    );
  } catch (error) {
    console.error(error);
    elements.beginWorkout.disabled = false;
    startupControls?.markReady();
    showWorkoutModifierFeedback("workout unavailable", error.message?.startsWith(FITNESS_UNAVAILABLE_MESSAGE)
      ? FITNESS_UNAVAILABLE_MESSAGE : "Could not prepare this workout. Your progress is safe.");
  }
}

function cancelQueuedWorkoutStart() {
  startWorkoutWhenReady = false;
  startupControls?.cancelStartRequest();
}

function showNextExercise({ preservePendingMovement = false } = {}) {
  if (!session) {
    return;
  }
  const nextGroup = session.getNextGroup();
  if (!nextGroup) {
    showCompletion(true);
    return;
  }

  currentGroup = nextGroup;
  currentExercise = session.getSelectedExercise(nextGroup);
  const activeGroups = session.getActiveGroups();
  const { position, total } = getWorkoutDisplayProgress(
    activeGroups,
    nextGroup,
  );

  elements.workoutProgressText.textContent =
    `${String(position).padStart(2, "0")}  /  ${String(total).padStart(2, "0")}`;
  elements.workoutProgressText.setAttribute(
    "aria-label",
    `Exercise ${position} of ${total}`,
  );
  elements.workoutProgressFill.style.transform = `scaleX(${position / total})`;
  renderExerciseIdentity(currentExercise);
  renderExecutionTimeline(currentGroup);
  elements.status.textContent =
    `Exercise ${position} of ${total}. ${currentExercise.name}.`;

  stopRuntimeTimers(preservePendingMovement);
  resetMovementVisuals();
  showReadyPanel();
  showScreen("workout");
  requestWakeLock();
  loadExerciseMedia(currentExercise, currentGroup);
}

function renderExerciseIdentity(exercise, upcoming = false) {
  elements.exerciseName.textContent = exercise.name;
  const mode = exercise.mode === "Hold" ? "Static" : "Repetition";
  elements.exerciseName.setAttribute(
    "aria-label",
    `${upcoming ? "Next block: " : ""}${exercise.name}. ${mode}.`,
  );
  elements.exerciseName.setAttribute(
    "aria-description",
    "Long press to copy exercise name.",
  );
  elements.holdBadge.hidden = exercise.mode !== "Hold";
}

function restorePendingMovement() {
  if (!session) {
    return false;
  }
  const pendingGroup = session.getPendingMovementGroup();
  if (!pendingGroup) {
    return false;
  }

  showNextExercise({ preservePendingMovement: true });
  if (!currentGroup || currentGroup.id !== pendingGroup.id || !currentExercise) {
    throw new Error("The persisted movement is not the next workout round.");
  }

  movementRemaining = session.getPendingMovementMillisecondsRemaining(Date.now());
  movementEndsAt = 0;
  movementRunning = false;
  movementPauseReason = session.state.pendingMovementPausedByUser
    ? "user"
    : "restore";
  automaticSequenceStartPending =
    movementPauseReason !== "user" &&
    session.isSequenceContinuationBlock(currentGroup) &&
    movementRemaining === getMovementDurationMs(currentGroup);
  session.pauseMovement(
    currentGroup,
    movementRemaining,
    movementPauseReason === "user",
  );
  persistState();
  lastMovementPhase = null;
  showMovePanel();
  renderPersistedMovementCountdown();
  setPlaybackControlsEnabled(
    movementPauseReason === "user" && mediaReady,
  );
  renderPlaybackToggle();
  return true;
}

function restorePendingRest() {
  if (!session) {
    return false;
  }
  const pendingGroup = session.getPendingRestGroup();
  if (!pendingGroup) {
    return false;
  }

  showNextExercise();
  if (!currentGroup || currentGroup.id !== pendingGroup.id || !currentExercise) {
    throw new Error("The persisted rest is not for the next workout round.");
  }

  restActive = true;
  elements.video.pause();
  setMediaMirrored(getMovementPresentation(currentGroup, "Continuous").mirrorMedia);
  setFullPhaseSurface("rest");
  elements.mediaCard.classList.add("resting");
  showRestPanel();
  elements.status.textContent = restStatusMessage();
  startRestTimer();
  return true;
}

function renderPersistedMovementCountdown() {
  const movementDuration = getMovementCountdownDurationMs(currentGroup);
  const state = getMovementPhaseState(
    movementRemaining,
    !session.isSequenceContinuationBlock(currentGroup),
  );
  elements.movementCountdown.value = String(state.secondsRemaining);
  elements.movementCountdown.textContent = String(state.secondsRemaining);
  elements.movementProgressFill.style.transform =
    `scaleX(${movementRemaining / movementDuration})`;
}

function renderExecutionTimeline(group, selectUpcomingBlock = false) {
  if (!session || !group) {
    return;
  }
  const timeline = getWorkoutExecutionTimeline(
    session.getActiveGroups(),
    group,
    selectUpcomingBlock,
  );
  const blockCount = timeline.blocks.length;
  const currentSetIndex = timeline.setStartBlockIndices
    .findLastIndex((index) => index <= timeline.currentBlockIndex);
  const currentSetStart = timeline.setStartBlockIndices[currentSetIndex];
  const currentSetEnd = timeline.setStartBlockIndices[currentSetIndex + 1] ??
    blockCount;
  const currentBlockPosition = timeline.currentBlockIndex - currentSetStart + 1;
  const currentSetBlockCount = currentSetEnd - currentSetStart;
  const setElements = timeline.setStartBlockIndices.map((startIndex, setIndex) => {
    const endIndex = timeline.setStartBlockIndices[setIndex + 1] ?? blockCount;
    const set = document.createElement("span");
    set.className = "execution-set";
    set.style.setProperty(
      "--execution-set-block-count",
      String(endIndex - startIndex),
    );
    set.append(...timeline.blocks.slice(startIndex, endIndex).map(
      (accent, indexWithinSet) => {
        const block = document.createElement("span");
        block.className = `execution-work-block ${accent}`;
        if (startIndex + indexWithinSet === timeline.currentBlockIndex) {
          block.classList.add("current");
        }
        return block;
      },
    ));
    return set;
  });
  elements.executionBlockTrack.replaceChildren(
    ...setElements,
  );
  const setCount = timeline.setStartBlockIndices.length;
  const timelineWidth = Math.min(166, Math.max(
    42,
    setCount * 12 + blockCount * 21 +
      (blockCount - setCount) * 3 + (setCount - 1) * 5,
  ));
  elements.executionSignifier.style.width = `${timelineWidth}px`;
  const setDescription = setCount > 1
    ? `Set ${currentSetIndex + 1} of ${setCount}. `
    : "";
  const description = setDescription +
    `Work block ${currentBlockPosition} of ${currentSetBlockCount}. ` +
    "Each colored segment is one 45-second work block. " +
    "The 15-second transitions are shown separately.";
  elements.executionSignifier.setAttribute("aria-label", description);
  elements.executionSignifier.title =
    setDescription +
    `Work block ${currentBlockPosition} of ${currentSetBlockCount}`;
}

function showReadyPanel() {
  automaticSequenceStartPending = false;
  elements.readyPanel.hidden = false;
  elements.movePanel.hidden = true;
  elements.restPanel.hidden = true;
  elements.shuffleExercise.hidden = !session || !currentGroup ||
    !session.canShuffleNextExercise(currentGroup);
  elements.startMovement.disabled = !mediaReady;
}

function shuffleCurrentExercise() {
  if (!session || !currentGroup || elements.readyPanel.hidden ||
      elements.shuffleExercise.hidden) {
    return;
  }

  elements.shuffleExercise.disabled = true;
  const result = session.shuffleNextExercise(currentGroup);
  if (!result) {
    elements.shuffleExercise.hidden = true;
    elements.shuffleExercise.disabled = false;
    return;
  }

  persistState();
  showNextExercise();
  elements.status.textContent =
    `Rejected ${result.rejectedExercise.name}. ` +
    `Changed to ${result.replacementExercise.name}.`;
  elements.shuffleExercise.disabled = false;
}

function showMovePanel() {
  elements.readyPanel.hidden = true;
  elements.movePanel.hidden = false;
  elements.restPanel.hidden = true;
}

function setPlaybackControlsEnabled(enabled) {
  elements.repeatExercise.disabled = !enabled;
  elements.playbackToggle.disabled = !enabled;

  // Broken or buffering media can disable playback, but it must never trap
  // the workout. A paused movement is still safe to reject and advance.
  elements.nextExercise.disabled = !movementRunning && !movementPauseReason;
}

function renderPlaybackToggle() {
  const paused = movementPauseReason === "user";
  const label = paused ? "Resume exercise" : "Pause exercise";
  elements.playbackToggle.dataset.state = paused ? "paused" : "playing";
  elements.playbackToggle.setAttribute("aria-label", label);
  elements.playbackToggle.title = label;
}

function showRestPanel() {
  feedbackView.cancelGesture(false);
  elements.readyPanel.hidden = true;
  elements.movePanel.hidden = true;
  elements.restPanel.hidden = false;
  const nextBlock = currentGroup
    ? session?.getNextSequenceBlock(currentGroup) ?? null
    : null;
  const isIntermediateBlock = nextBlock !== null;
  if (nextBlock) {
    showUpcomingSequenceBlockPreview(nextBlock);
  } else {
    previewingUpcomingSequenceBlock = false;
  }
  if (currentGroup) {
    renderExecutionTimeline(currentGroup, isIntermediateBlock);
  }
  renderRestPlaybackToggle();
  feedbackExpiring = false;
  feedbackView.setActive(!isIntermediateBlock, session?.getPendingRestFeedback());
  elements.workoutScreen.classList.toggle("feedback-screen", !isIntermediateBlock);
}

function showUpcomingSequenceBlockPreview(nextBlock) {
  const nextExercise = session.getSelectedExercise(nextBlock);
  renderExerciseIdentity(nextExercise, true);
  loadExerciseMedia(nextExercise, nextBlock, true);
  setMediaMirrored(
    getMovementPresentation(nextBlock, "Continuous").mirrorMedia,
  );
}

function assetUrl(path) {
  const url = new URL(`assets/${path}`, document.baseURI);
  const fingerprint = assetVersions[path];
  if (fingerprint) {
    url.searchParams.set("v", fingerprint);
  }
  return url.href;
}

function loadExerciseMedia(
  exercise = currentExercise,
  group = currentGroup,
  isUpcomingSequencePreview = false,
) {
  if (!exercise || !group) {
    return;
  }

  reactionDrill = null;
  reactionDrillView?.dispose();
  reactionDrillView = null;
  mediaExercise = exercise;
  mediaGroup = group;
  previewingUpcomingSequenceBlock = isUpcomingSequencePreview;

  const generation = ++mediaGeneration;
  const drillPlan = reactionDrillPlans.get(exercise.id);
  clearMediaRecoveryTimer();
  mediaReady = false;
  elements.startMovement.disabled = true;
  setPlaybackControlsEnabled(false);
  elements.mediaError.hidden = true;
  elements.mediaScrim.hidden = false;
  elements.mediaScrim.classList.remove("revealed");
  elements.holdFrame.hidden = true;
  elements.holdFrame.removeAttribute("src");
  resetVideoElement();
  setMediaMirrored(group.mirrorSequenceMedia === true);

  if (drillPlan) {
    elements.video.hidden = true;
    reactionDrillView = new ReactionDrillView(elements.mediaCard, drillPlan, assetUrl,
      () => { if (generation === mediaGeneration) markMediaReady(generation); },
      () => { if (generation === mediaGeneration) showMediaError(generation); });
    setMediaMirrored(group.mirrorSequenceMedia === true);
    return;
  }

  if (exercise.presentation === "Still") {
    elements.video.hidden = true;
    elements.holdFrame.onload = () => markMediaReady(generation);
    elements.holdFrame.onerror = () => showMediaError(generation);
    elements.holdFrame.src = assetUrl(getHoldFramePath(exercise));
    elements.holdFrame.hidden = false;
    return;
  }

  elements.video.hidden = false;
  elements.video.preload = "auto";
  elements.video.loop = true;
  elements.video.onloadedmetadata = () => prepareSequenceMediaSegment();
  elements.video.oncanplay = () => {
    prepareSequenceMediaSegment();
    markMediaReady(generation, false);
  };
  elements.video.onplaying = () => markMediaReady(generation, true);
  elements.video.onwaiting = () => handleVideoWaiting(generation);
  elements.video.onstalled = () => {
    if (!elements.video.paused && elements.video.readyState < 3) {
      handleVideoWaiting(generation);
    }
  };
  elements.video.onprogress = () => {
    if (!mediaReady && elements.video.readyState >= 3) {
      markMediaReady(generation, false);
    }
  };
  elements.video.onerror = () => showMediaError(generation);
  elements.video.onended = handleVideoEnded;
  elements.video.src = assetUrl(getExerciseVideoPath(
    exercise,
    group.sequenceMediaSegment ?? "Full",
  ));
  elements.video.load();
}

function resetVideoElement() {
  elements.video.oncanplay = null;
  elements.video.onloadedmetadata = null;
  elements.video.onplaying = null;
  elements.video.onwaiting = null;
  elements.video.onstalled = null;
  elements.video.onprogress = null;
  elements.video.onerror = null;
  elements.video.onended = null;
  elements.video.ontimeupdate = null;
  elements.video.pause();
  elements.video.removeAttribute("src");
  elements.video.load();
  elements.video.loop = false;
  elements.video.style.setProperty("--media-scale-x", "1");
}

function markMediaReady(generation, playbackConfirmed = false) {
  if (generation !== mediaGeneration) {
    return;
  }
  if (reactionDrillView && !reactionDrillView.ready) return;
  if (reactionDrillView) playbackConfirmed = true;
  const newlyReady = !mediaReady;
  mediaReady = true;
  elements.mediaError.hidden = true;
  elements.mediaScrim.classList.add("revealed");
  elements.startMovement.disabled = false;
  const manuallyPaused = movementPauseReason === "user";
  setPlaybackControlsEnabled(movementRunning || manuallyPaused);

  if (activeWorkoutSetup) {
    elements.video.pause();
    return;
  }

  if (playbackConfirmed || !movementPauseReason || manuallyPaused) {
    clearMediaRecoveryTimer();
  }

  if (manuallyPaused) {
    elements.video.pause();
    renderPlaybackToggle();
    return;
  }

  if (movementPauseReason && !document.hidden) {
    if (playbackConfirmed) {
      resumeMovement();
    } else {
      resumePausedMovementWhenVisible();
    }
    return;
  }

  if (
    newlyReady &&
    !document.hidden &&
    (!elements.readyPanel.hidden ||
      (restActive && previewingUpcomingSequenceBlock)) &&
    mediaExercise?.presentation !== "Still"
  ) {
    playVideo();
  }
}

function handleVideoWaiting(generation) {
  if (generation !== mediaGeneration || mediaExercise?.presentation === "Still") {
    return;
  }
  mediaReady = false;
  elements.startMovement.disabled = true;
  setPlaybackControlsEnabled(false);
  elements.mediaError.hidden = true;
  elements.mediaScrim.hidden = false;
  elements.mediaScrim.classList.remove("revealed");
  if (movementRunning) {
    pauseMovement("buffering");
  }
  scheduleMediaRecoveryFailure(generation);
}

function showMediaError(generation = mediaGeneration) {
  if (generation !== mediaGeneration) {
    return;
  }
  clearMediaRecoveryTimer();
  mediaReady = false;
  elements.video.pause();
  if (movementRunning) {
    pauseMovement("media");
  } else if (movementPauseReason && movementPauseReason !== "user") {
    movementPauseReason = "media";
  }
  if (movementPauseReason === "media") {
    lastMovementPhase = null;
  }
  elements.startMovement.disabled = true;
  setPlaybackControlsEnabled(false);
  elements.mediaScrim.classList.remove("revealed");
  elements.mediaScrim.hidden = true;
  elements.mediaError.hidden = false;
  elements.status.textContent = "Demonstration unavailable.";
}

function retryMedia() {
  if (!mediaExercise || !mediaGroup) {
    return;
  }
  loadExerciseMedia(
    mediaExercise,
    mediaGroup,
    previewingUpcomingSequenceBlock,
  );
}

function clearExerciseMedia() {
  reactionDrill = null;
  reactionDrillView?.dispose();
  reactionDrillView = null;
  clearMediaRecoveryTimer();
  mediaReady = false;
  mediaGeneration++;
  resetVideoElement();
  elements.holdFrame.onload = null;
  elements.holdFrame.onerror = null;
  elements.holdFrame.hidden = true;
  elements.holdFrame.removeAttribute("src");
  elements.mediaError.hidden = true;
  mediaExercise = null;
  mediaGroup = null;
  previewingUpcomingSequenceBlock = false;
}

function startMovement() {
  if (
    !currentExercise ||
    !currentGroup ||
    !mediaReady ||
    movementRunning ||
    movementPauseReason
  ) {
    return;
  }
  requestWakeLock();
  movementRemaining = getMovementCountdownDurationMs(currentGroup);
  movementPauseReason = null;
  lastMovementPhase = null;
  showMovePanel();
  setMovementDeadline(movementRemaining);
}

function setMovementDeadline(remainingMilliseconds) {
  clearInterval(movementTimer);
  movementRemaining = remainingMilliseconds;
  movementEndsAt = performance.now() + remainingMilliseconds;
  movementRunning = true;
  movementPauseReason = null;
  if (session && currentGroup) {
    const persistedRemaining = Math.max(1, Math.trunc(remainingMilliseconds));
    session.beginMovement(
      currentGroup,
      persistedRemaining,
      Date.now() + persistedRemaining,
    );
    persistState();
  }
  setPlaybackControlsEnabled(true);
  renderPlaybackToggle();
  const cueAutomaticSequenceStart = automaticSequenceStartPending;
  automaticSequenceStartPending = false;
  updateMovement();
  if (cueAutomaticSequenceStart) {
    playSound("start");
  }
  movementTimer = setInterval(updateMovement, TIMER_INTERVAL_MS);
}

function updateMovement() {
  if (!movementRunning || !currentExercise) {
    return;
  }
  movementRemaining = Math.max(0, movementEndsAt - performance.now());
  const movementDuration = getMovementCountdownDurationMs(currentGroup);
  const state = getMovementPhaseState(
    movementRemaining,
    !session.isSequenceContinuationBlock(currentGroup),
  );
  elements.movementCountdown.value = String(state.secondsRemaining);
  elements.movementCountdown.textContent = String(state.secondsRemaining);
  elements.movementProgressFill.style.transform =
    `scaleX(${movementRemaining / movementDuration})`;

  if (state.phase !== lastMovementPhase && state.phase !== "Complete") {
    applyMovementPhase(state.phase);
  }
  updateReactionCue();
  if (movementRemaining <= 0) {
    completeMovement();
  }
}

function applyMovementPhase(phase) {
  if (!currentExercise) {
    return;
  }
  const previousPhase = lastMovementPhase;
  lastMovementPhase = phase;
  elements.movePanel.classList.toggle(
    "change",
    phase === "Preparation",
  );

  if (phase === "Preparation") {
    clearReactionCue();
    setMediaMirrored(false);
    setFullPhaseSurface("rest");
    elements.mediaCard.classList.add("resting");
    elements.video.pause();
    elements.status.textContent = "Prepare, 5 seconds.";
    return;
  }

  const presentation = getMovementPresentation(currentGroup, phase);
  const description = movementCueDescription(
    presentation.sideCue,
    presentation.directionCue,
  );

  elements.mediaCard.classList.remove("resting");
  setMediaMirrored(presentation.mirrorMedia);
  setFullPhaseSurface("move");
  restartMediaForPhase();

  elements.status.textContent = reactionDrillPlans.has(currentExercise.id)
    ? "Match the number. Complete the action, then reset. 45 seconds."
    : isCurrentReactionCueEnabled()
    ? `${description}. Move on green; freeze on red. 45 seconds.`
    : `${description}, 45 seconds.`;

  if (previousPhase === "Preparation") {
    playSound("start");
  }
}

function restartMediaForPhase() {
  if (!mediaExercise) {
    return;
  }
  if (mediaExercise.presentation === "Still") {
    elements.holdFrame.hidden = false;
    return;
  }

  elements.holdFrame.hidden = true;
  elements.video.hidden = false;
  elements.video.loop =
    mediaExercise.mode !== "Hold";
  prepareSequenceMediaSegment(true);
  playVideo();
  startReactionCue();
}

function isCurrentReactionCueEnabled() {
  return Boolean(currentExercise && !reactionDrillPlans.has(currentExercise.id) && session && isReactionCueEnabled(
    currentExercise,
    session.state.activeCatalogRevision,
  ));
}

function startReactionCue(now = performance.now()) {
  clearReactionCue();
  if (!movementRunning || !isCurrentReactionCueEnabled() ||
      lastMovementPhase !== "Continuous") {
    return;
  }
  reactionCueActive = true;
  reactionCueMoving = true;
  reactionCueEndsAt = now + getNextReactionCueDurationMilliseconds(
    currentExercise.reactionCueLevel,
    true,
  );
  reactionCueMoveFlashEndsAt = now + REACTION_CUE_MOVE_FLASH_MS;
  renderReactionCue(now);
}

function updateReactionCue(now = performance.now()) {
  const plan = reactionDrillPlans.get(currentExercise?.id);
  if (plan) {
    elements.reactionCue.hidden = true;
    if (!movementRunning || lastMovementPhase !== "Continuous") {
      reactionDrill = null;
      reactionDrillView?.reset();
      return;
    }
    reactionDrill ??= new ReactionDrill(plan);
    if (reactionDrill.advance(now, movementRemaining, reactionDrillView?.ready === true)) {
      if (reactionDrill.phase === "Reset") reactionDrillView.reset(false);
      reactionDrillView?.render(reactionDrill);
    }
    return;
  }
  if (!movementRunning || lastMovementPhase !== "Continuous" ||
      !isCurrentReactionCueEnabled()) {
    clearReactionCue();
    return;
  }
  if (!reactionCueActive) {
    startReactionCue(now);
    return;
  }

  let changed = false;
  let transitions = 0;
  while (now >= reactionCueEndsAt && transitions < 8) {
    const transitionAt = reactionCueEndsAt;
    reactionCueMoving = !reactionCueMoving;
    reactionCueEndsAt = transitionAt + getNextReactionCueDurationMilliseconds(
      currentExercise.reactionCueLevel,
      reactionCueMoving,
    );
    if (reactionCueMoving) {
      reactionCueMoveFlashEndsAt = now + REACTION_CUE_MOVE_FLASH_MS;
    }
    changed = true;
    transitions++;
  }

  if (changed) {
    if (reactionCueMoving) {
      playVideo();
    } else {
      elements.video.pause();
    }
  }
  renderReactionCue(now);
}

function renderReactionCue(now) {
  const visible = reactionCueActive &&
    (!reactionCueMoving || now < reactionCueMoveFlashEndsAt);
  elements.reactionCue.hidden = !visible;
  if (!visible) {
    return;
  }
  const state = reactionCueMoving ? "move" : "freeze";
  elements.reactionCue.dataset.state = state;
  elements.reactionCue.setAttribute(
    "aria-label",
    reactionCueMoving ? "Move" : "Freeze",
  );
}

function clearReactionCue(preserveFinalFrame = false) {
  reactionDrill = null;
  if (preserveFinalFrame) reactionDrillView?.freeze();
  else reactionDrillView?.reset();
  reactionCueActive = false;
  reactionCueMoving = true;
  reactionCueEndsAt = 0;
  reactionCueMoveFlashEndsAt = 0;
  elements.reactionCue.hidden = true;
  delete elements.reactionCue.dataset.state;
}

function prepareSequenceMediaSegment(force = false) {
  if (restActive && !previewingUpcomingSequenceBlock) {
    const position = (session?.state.pendingRestMediaPositionMilliseconds ?? 0) / 1000;
    if (Number.isFinite(elements.video.duration) && position > 0 && Math.abs(elements.video.currentTime - position) > .05)
      elements.video.currentTime = Math.min(position, Math.max(0, elements.video.duration - .01));
    return;
  }
  if ((mediaGroup?.sequenceMediaSegment ?? "Full") === "Full" ||
      !Number.isFinite(elements.video.duration)) {
    return;
  }
  const segmentStart = 0;
  if (!force && Math.abs(elements.video.currentTime - segmentStart) < 0.05) {
    return;
  }
  try {
    elements.video.currentTime = segmentStart;
  } catch {
    // The loaded media will seek again once metadata is available.
  }
}

function handleVideoEnded() {
  if (mediaExercise?.mode === "Hold" && !elements.movePanel.hidden) {
    showReviewedHoldFrame();
  }
}

function showReviewedHoldFrame() {
  if (!mediaExercise || mediaExercise.mode !== "Hold") {
    return;
  }
  const generation = mediaGeneration;
  elements.holdFrame.onload = () => {
    if (generation === mediaGeneration) {
      elements.holdFrame.hidden = false;
      elements.video.pause();
    }
  };
  elements.holdFrame.onerror = () => showMediaError(generation);
  if (!elements.holdFrame.src) {
    elements.holdFrame.src = assetUrl(getHoldFramePath(mediaExercise));
  } else if (elements.holdFrame.complete && elements.holdFrame.naturalWidth > 0) {
    elements.holdFrame.hidden = false;
    elements.video.pause();
  }
}

function pauseMovement(reason) {
  if (!movementRunning) {
    return;
  }
  movementRemaining = Math.max(1, movementEndsAt - performance.now());
  clearInterval(movementTimer);
  movementTimer = null;
  movementRunning = false;
  movementPauseReason = reason;
  clearReactionCue();
  if (session && currentGroup) {
    session.pauseMovement(
      currentGroup,
      Math.max(1, Math.trunc(movementRemaining)),
      reason === "user",
    );
    persistState();
  }
  elements.video.pause();
  setPlaybackControlsEnabled(reason === "user" && mediaReady);
  renderPlaybackToggle();
}

function resumeMovement() {
  if (!movementPauseReason || movementRemaining <= 0 || !mediaReady || document.hidden) {
    return;
  }
  setMovementDeadline(movementRemaining);
}

function resumePausedMovementWhenVisible() {
  if (
    !movementPauseReason ||
    movementPauseReason === "user" ||
    !currentExercise ||
    document.hidden
  ) {
    return;
  }

  const phaseState = getMovementPhaseState(
    movementRemaining,
    !session.isSequenceContinuationBlock(currentGroup),
  );
  if (
    currentExercise.presentation === "Still" ||
    phaseState.phase === "Preparation"
  ) {
    resumeMovement();
    return;
  }

  scheduleMediaRecoveryFailure(mediaGeneration);
  if (movementPauseReason === "media" && lastMovementPhase !== phaseState.phase) {
    restorePausedMovementMedia(phaseState.phase);
  } else {
    playVideo();
  }
}

function restorePausedMovementMedia(phase) {
  const presentation = getMovementPresentation(currentGroup, phase);
  lastMovementPhase = phase;
  elements.movePanel.classList.remove("change");
  elements.mediaCard.classList.remove("resting");
  setMediaMirrored(presentation.mirrorMedia);
  setFullPhaseSurface("move");
  restartMediaForPhase();
}

function toggleMovementPlayback() {
  if (movementPauseReason === "user") {
    if (!mediaReady) {
      return;
    }
    resumeMovement();
    elements.status.textContent = "Exercise resumed.";
    return;
  }
  if (!movementRunning) {
    return;
  }
  pauseMovement("user");
  elements.status.textContent = "Exercise paused.";
}

function repeatMovement() {
  if (
    !currentExercise ||
    !currentGroup ||
    !mediaReady ||
    (!movementRunning && movementPauseReason !== "user")
  ) {
    return;
  }

  clearInterval(movementTimer);
  movementTimer = null;
  movementRunning = false;
  movementPauseReason = null;
  movementRemaining = getMovementCountdownDurationMs(currentGroup);
  movementEndsAt = 0;
  lastMovementPhase = null;
  elements.video.pause();
  setMediaMirrored(false);
  if (currentExercise.presentation !== "Still") {
    elements.holdFrame.hidden = true;
    elements.video.hidden = false;
    try {
      elements.video.currentTime = 0;
    } catch {
      // Metadata readiness is already guarded; the phase restart will seek again.
    }
  }
  setMovementDeadline(movementRemaining);
  elements.status.textContent = "Exercise restarted from the beginning.";
}

function goToNextExercise() {
  if (
    (!movementRunning && !movementPauseReason) ||
    !session ||
    !currentGroup
  ) {
    return;
  }
  stopMovementTimer();
  session.rejectCurrentSequence(currentGroup);
  persistState();
  if (session.state.workoutCompleted) {
    showCompletion(true);
  } else {
    showNextExercise();
  }
}

function completeMovement() {
  if (!movementRunning || !session || !currentGroup) {
    return;
  }
  stopMovementTimer(true, true);
  elements.movementCountdown.value = "0";
  elements.movementCountdown.textContent = "0";
  elements.movementProgressFill.style.transform = "scaleX(0)";
  if (currentExercise?.mode === "Hold") {
    showReviewedHoldFrame();
  }
  playSound("rest");
  session.beginRest(currentGroup, Date.now() + REST_DURATION_MS);
  session.state.pendingRestMediaPositionMilliseconds = Math.max(0, Math.round(elements.video.currentTime * 1000));
  persistState();
  restActive = true;
  elements.video.pause();
  setMediaMirrored(getMovementPresentation(currentGroup, "Continuous").mirrorMedia);
  setFullPhaseSurface("rest");
  elements.mediaCard.classList.add("resting");
  showRestPanel();
  elements.status.textContent = restStatusMessage();
  startRestTimer();
}

function restStatusMessage() {
  const seconds = session
    ? Math.ceil(session.getPendingRestMillisecondsRemaining(Date.now()) / 1000)
    : 15;
  if (session?.state.pendingRestPausedByUser) {
    return `Rest paused, ${seconds} seconds remaining.`;
  }
  const nextBlock = currentGroup
    ? session?.getNextSequenceBlock(currentGroup) ?? null
    : null;
  if (nextBlock) {
    const nextExercise = session.getSelectedExercise(nextBlock);
    return `Rest, ${seconds} seconds. Next block: ${nextExercise.name}. ` +
      "It starts automatically.";
  }
  return `${seconds} seconds to meh. Swipe the exercise to respond, or pause the timer.`;
}

function startRestTimer() {
  clearInterval(restTimer);
  restTimer = null;
  updateRest();
  if (restActive && feedbackView.active && session?.getPendingRestFeedback()) {
    void feedbackView.finishSelection();
    return;
  }
  if (restActive && !session?.state.pendingRestPausedByUser) {
    restTimer = setInterval(updateRest, TIMER_INTERVAL_MS);
  }
}

function updateRest() {
  if (!restActive || !session) {
    return;
  }
  const confirming = feedbackView.active && !!session.getPendingRestFeedback();
  elements.restCountdown.style.visibility = confirming ? "hidden" : "";
  elements.restProgressFill.parentElement.style.visibility = confirming ? "hidden" : "";
  elements.restPlaybackToggle.style.visibility = confirming ? "hidden" : "";
  elements.restPlaybackToggle.disabled = confirming;
  if (confirming) return;
  const remaining = session.getPendingRestMillisecondsRemaining(Date.now());
  const seconds = Math.ceil(remaining / 1000);
  elements.restCountdown.value = String(seconds);
  const awaiting = feedbackView.active && !session.getPendingRestFeedback();
  elements.restCountdown.textContent = `${seconds} seconds${awaiting ? " to meh" : ""}`;
  elements.restProgressFill.style.transform = `scaleX(${remaining / REST_DURATION_MS})`;
  if (remaining <= 0) {
    completeRest();
  }
}

function holdFeedbackClock(held, resume = true) {
  if (!restActive || !session || !currentGroup) return;
  if (held) {
    feedbackResumeAfterHold = !session.state.pendingRestPausedByUser;
    feedbackHeld = true;
    const remaining = session.getPendingRestMillisecondsRemaining(Date.now());
    if (feedbackResumeAfterHold && remaining > 0) session.pauseRest(currentGroup, remaining);
    clearInterval(restTimer); restTimer = null; persistState();
  } else {
    const shouldResume = feedbackHeld && feedbackResumeAfterHold && resume && !document.hidden;
    feedbackHeld = false; feedbackResumeAfterHold = false;
    if (shouldResume && session.state.pendingRestPausedByUser) {
      const remaining = session.getPendingRestMillisecondsRemaining(Date.now());
      session.resumeRest(currentGroup, Date.now() + Math.max(1, remaining));
      persistState(); startRestTimer();
    }
  }
  renderRestPlaybackToggle();
}

function toggleRestPlayback() {
  if (feedbackExpiring || feedbackView.busy) return;
  if (restActive && feedbackView.active && session?.getPendingRestFeedback()) {
    feedbackView.finishSelection(); return;
  }
  if (feedbackHeld) {
    feedbackResumeAfterHold = !feedbackResumeAfterHold;
    renderRestPlaybackToggle(); return;
  }
  if (!restActive || !session || !currentGroup ||
      session.state.pendingRestGroupId !== currentGroup.id) {
    return;
  }

  const now = Date.now();
  const remaining = session.getPendingRestMillisecondsRemaining(now);
  if (remaining <= 0) {
    completeRest();
    return;
  }

  if (session.state.pendingRestPausedByUser) {
    session.resumeRest(currentGroup, now + remaining);
    persistState();
    renderRestPlaybackToggle();
    elements.status.textContent =
      `Rest resumed, ${Math.ceil(remaining / 1000)} seconds remaining.`;
    startRestTimer();
    return;
  }

  session.pauseRest(currentGroup, remaining);
  persistState();
  clearInterval(restTimer);
  restTimer = null;
  updateRest();
  renderRestPlaybackToggle();
  elements.status.textContent =
    `Rest paused, ${Math.ceil(remaining / 1000)} seconds remaining.`;
}

function renderRestPlaybackToggle() {
  const paused = feedbackHeld ? !feedbackResumeAfterHold : session?.state.pendingRestPausedByUser === true;
  const label = paused ? "Resume rest" : "Pause rest";
  elements.restPlaybackToggle.dataset.state = paused ? "paused" : "playing";
  elements.restPlaybackToggle.setAttribute("aria-label", label);
  elements.restPlaybackToggle.title = label;
}

function completeRest(afterAnimation = false) {
  if (!restActive || !session || !currentGroup) {
    return;
  }
  if ((feedbackExpiring || feedbackView.busy) && !afterAnimation) return;
  if (!afterAnimation && !session.isIntermediateSequenceBlock(currentGroup) && !session.getPendingRestFeedback()) {
    feedbackExpiring = true;
    clearInterval(restTimer); restTimer = null;
    // Keep a rest checkpoint while the automatic response settles. Backgrounding
    // interrupts the animation without finalizing a hidden exercise.
    session.pauseRest(currentGroup, 1); persistState();
    feedbackView.commit("x", undefined, true);
    return;
  }
  feedbackView.stop();
  restActive = false;
  clearInterval(restTimer);
  restTimer = null;
  if (session.isIntermediateSequenceBlock(currentGroup)) {
    session.advanceSequence(currentGroup);
    session.clearPendingRest();
    const nextBlock = session.getNextGroup();
    if (!nextBlock) {
      throw new Error("An intermediate sequence block has no following block.");
    }
    session.pauseMovement(
      nextBlock,
      getMovementDurationMs(nextBlock),
      false,
    );
    persistState();
    restorePendingMovement();
    return;
  }
  session.recordOutcome(currentGroup, session.getPendingRestFeedback() ?? "x");
  session.clearPendingRest();
  persistState();
  if (session.state.workoutCompleted) {
    showCompletion(true);
  } else {
    showNextExercise();
  }
}

function showCompletion(playCue) {
  releaseWakeLock();
  stopRuntimeTimers();
  clearExerciseMedia();
  resetMovementVisuals();
  currentGroup = null;
  currentExercise = null;
  renderCompletionProfile(elements.completionChanges,
    session.state.fitnessProfileEvidenceVersion === FITNESS_EVIDENCE_VERSION ? session.state.activeFitnessProfileBaseline : null,
    session.state.completionFitnessProfileSnapshot);
  showScreen("completion");
  if (playCue) {
    playSound("complete");
  }
  elements.status.textContent = "Workout complete.";
}

function closeCompletion() {
  if (!session?.state.workoutCompleted) {
    return;
  }
  session.acknowledgeCompletion();
  persistState();
  showDuration();
}

function stopMovementTimer(clearPendingMovement = true, preserveFinalFrame = false) {
  clearInterval(movementTimer);
  movementTimer = null;
  movementRunning = false;
  movementPauseReason = null;
  movementEndsAt = 0;
  movementRemaining = 0;
  clearReactionCue(preserveFinalFrame);
  if (clearPendingMovement && session?.state.pendingMovementGroupId) {
    session.clearPendingMovement();
    persistState();
  }
  setPlaybackControlsEnabled(false);
  renderPlaybackToggle();
}

function stopRuntimeTimers(preservePendingMovement = false) {
  feedbackView.stop();
  feedbackExpiring = false;
  stopMovementTimer(!preservePendingMovement);
  restActive = false;
  clearInterval(restTimer);
  restTimer = null;
}

function resetMovementVisuals() {
  feedbackView.stop();
  elements.workoutScreen.classList.remove("feedback-screen");
  clearReactionCue();
  lastMovementPhase = null;
  movementPauseReason = null;
  setPlaybackControlsEnabled(false);
  renderPlaybackToggle();
  elements.phaseSurface.classList.remove("visible");
  elements.phaseLeft.style.backgroundColor = "";
  elements.phaseRight.style.backgroundColor = "";
  setWorkoutPhaseClass(null);
  elements.movePanel.classList.remove("change");
  elements.mediaCard.classList.remove("resting");
  elements.movementCountdown.value = "45";
  elements.movementCountdown.textContent = "45";
  elements.movementProgressFill.style.transform = "scaleX(1)";
  elements.restCountdown.value = "15";
  elements.restCountdown.textContent = "15";
  elements.restProgressFill.style.transform = "scaleX(1)";
  setMediaMirrored(false);
}

function setFullPhaseSurface(kind) {
  const color = kind === "move" ? "var(--move-surface)" : "var(--rest-surface)";
  elements.phaseLeft.style.backgroundColor = color;
  elements.phaseRight.style.backgroundColor = color;
  setWorkoutPhaseClass(kind);
  elements.phaseSurface.classList.add("visible");
}

function setWorkoutPhaseClass(kind) {
  elements.workoutScreen.classList.toggle("phase-move", kind === "move");
  elements.workoutScreen.classList.toggle("phase-rest", kind === "rest");
}

function setMediaMirrored(mirrored) {
  reactionDrillView?.setMirror(mirrored);
  const scale = mirrored ? "-1" : "1";
  elements.video.style.setProperty("--media-scale-x", scale);
  elements.holdFrame.style.setProperty("--media-scale-x", scale);
}

function movementCueDescription(sideCue, directionCue) {
  const labels = {
    ScreenLeft: "Left side",
    ScreenRight: "Right side",
    ShownLeadStance: "Shown lead stance",
    OppositeLeadStance: "Opposite lead stance",
    ShownHandPosition: "Shown hand position",
    OppositeHandPosition: "Opposite hand position",
    Forward: "Forward",
    Backward: "Backward",
    Clockwise: "Clockwise",
    Counterclockwise: "Counterclockwise",
    Inward: "Inward",
    Outward: "Outward",
  };
  const parts = [labels[sideCue], labels[directionCue]].filter(Boolean);
  return parts.length > 0 ? parts.join(", ") : "Move";
}

function playVideo() {
  if (reactionDrillView) {
    elements.video.pause();
    reactionDrillView.setPreviewEnabled(!document.hidden && !movementRunning &&
      (!elements.readyPanel.hidden || (restActive && previewingUpcomingSequenceBlock)));
    return;
  }
  const playback = elements.video.play();
  if (playback && typeof playback.catch === "function") {
    playback.catch(() => {
      if (movementRunning) {
        pauseMovement("buffering");
      }
      if (movementPauseReason) {
        scheduleMediaRecoveryFailure(mediaGeneration);
      }
    });
  }
}

function playSound(name) {
  const audio = sounds[name];
  if (!audio) {
    return;
  }
  try {
    audio.currentTime = 0;
    const playback = audio.play();
    if (playback && typeof playback.catch === "function") {
      playback.catch(() => {});
    }
  } catch {
    // Cues are best-effort and never block a workout.
  }
}

function pauseActiveWorkoutForBackground() {
  feedbackView.interrupt();
  feedbackExpiring = false;
  if (restActive && !previewingUpcomingSequenceBlock) reactionDrillView?.freeze();
  else { reactionDrillView?.setPreviewEnabled(false); reactionDrillView?.reset(); }
  if (movementRunning) {
    pauseMovement("user");
  }

  if (
    restActive &&
    session &&
    currentGroup &&
    session.state.pendingRestGroupId === currentGroup.id &&
    !session.state.pendingRestPausedByUser
  ) {
    const remaining = session.getPendingRestMillisecondsRemaining(Date.now());
    if (remaining > 0) {
      session.pauseRest(currentGroup, remaining);
      persistState();
      updateRest();
      renderRestPlaybackToggle();
    }
  }

  clearInterval(restTimer);
  restTimer = null;
  elements.video.pause();
}

function handleVisibilityChange() {
  if (document.hidden) {
    cancelExerciseNameLongPress();
    clearMediaRecoveryTimer();
    pauseActiveWorkoutForBackground();
    return;
  }

  if (activeWorkoutSetup) {
    renderWorkoutModifiers();
    elements.video.pause();
    return;
  }

  if (!elements.durationScreen.hidden) {
    renderWorkoutModifiers();
  }

  if (restActive) {
    startRestTimer();
    if (
      previewingUpcomingSequenceBlock &&
      mediaReady &&
      mediaExercise?.presentation !== "Still"
    ) {
      playVideo();
    }
  } else if (movementPauseReason && movementPauseReason !== "user") {
    resumePausedMovementWhenVisible();
  } else if (!elements.readyPanel.hidden && mediaReady && mediaExercise?.presentation !== "Still") {
    playVideo();
  }
  if (!elements.workoutScreen.hidden) {
    requestWakeLock();
  }
}

function handlePageHide() {
  cancelExerciseNameLongPress();
  clearMediaRecoveryTimer();
  pauseActiveWorkoutForBackground();
}

function scheduleMediaRecoveryFailure(generation) {
  if (document.hidden || mediaRecoveryTimer) {
    return;
  }
  mediaRecoveryTimer = setTimeout(() => {
    mediaRecoveryTimer = null;
    if (generation === mediaGeneration && !document.hidden && !movementRunning) {
      showMediaError(generation);
    }
  }, MEDIA_RECOVERY_TIMEOUT_MS);
}

function clearMediaRecoveryTimer() {
  clearTimeout(mediaRecoveryTimer);
  mediaRecoveryTimer = null;
}

async function requestWakeLock() {
  if (
    wakeLock ||
    wakeLockRequestPending ||
    document.hidden ||
    elements.workoutScreen.hidden ||
    !navigator.wakeLock?.request
  ) {
    return;
  }

  const generation = wakeLockGeneration;
  wakeLockRequestPending = true;
  try {
    const requestedLock = await navigator.wakeLock.request("screen");
    if (generation !== wakeLockGeneration || elements.workoutScreen.hidden) {
      await requestedLock.release();
      return;
    }
    wakeLock = requestedLock;
    requestedLock.addEventListener("release", () => {
      if (wakeLock === requestedLock) {
        wakeLock = null;
      }
    });
  } catch {
    // Unsupported or denied wake locks do not block a workout.
  } finally {
    wakeLockRequestPending = false;
  }
}

function releaseWakeLock() {
  wakeLockGeneration++;
  const activeLock = wakeLock;
  wakeLock = null;
  if (activeLock) {
    activeLock.release().catch(() => {});
  }
}
