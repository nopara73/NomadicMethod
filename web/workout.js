import "./light-cadence.js";

export const SUPPORTED_MINUTES = Object.freeze([3, 5, 7, 10, 15, 20, 30, 45, 60, 90]);
export const WORKOUT_MODIFIERS = Object.freeze({
  None: 0,
  Insect: 1,
  Silence: 2,
  Mirror: 4,
  TallMirror: 8,
  HardFloor: 16,
  Wall: 32,
  SoleWallContact: 64,
  UpperBodyClothing: 128,
  Light: 256,
  Shy: 512,
});
export const MIRROR_EQUIPMENT = Object.freeze({
  None: "None",
  Compact: "Compact",
  Tall: "Tall",
});
export const WALL_EQUIPMENT = Object.freeze({
  None: "None",
  SolesStayOff: "SolesStayOff",
  SolesMayTouch: "SolesMayTouch",
});
export const RECOVERY_LIGHT_MINIMUM_SHARE_NUMERATOR = 4;
export const RECOVERY_LIGHT_MINIMUM_SHARE_DENOMINATOR = 5;
export const EXERCISE_INSECT_COMPATIBILITY = Object.freeze({
  Unreviewed: "Unreviewed",
  Compatible: "Compatible",
  Incompatible: "Incompatible",
});
// "Hard Floor" is one combined contract: rigid and slippery.
// Compatible movements must satisfy both impact-comfort and traction safety.
export const EXERCISE_HARD_FLOOR_COMPATIBILITY = Object.freeze({
  Unreviewed: "Unreviewed",
  Compatible: "Compatible",
  Incompatible: "Incompatible",
});
export const EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT = Object.freeze({
  Unreviewed: "Unreviewed",
  ClothingRequired: "ClothingRequired",
  BareUpperBodyRequired: "BareUpperBodyRequired",
  Agnostic: "Agnostic",
});
export const EXERCISE_SHY_COMPATIBILITY = Object.freeze({
  Unreviewed: "Unreviewed",
  Compatible: "Compatible",
  Incompatible: "Incompatible",
});
export const EXERCISE_MIRROR_RELATIONSHIP = Object.freeze({
  Unreviewed: "Unreviewed",
  MirrorOnly: "MirrorOnly",
  BenefitsGreatly: "BenefitsGreatly",
  Agnostic: "Agnostic",
});
export const EXERCISE_MIRROR_COVERAGE = Object.freeze({
  None: "None",
  UpperBody: "UpperBody",
  FullBody: "FullBody",
});

export function getSessionMovementId(exercise) {
  return Number.isInteger(exercise?.sessionMovementId) &&
    exercise.sessionMovementId > 0
    ? exercise.sessionMovementId
    : exercise.id;
}

const MODIFIER_RULES = Object.freeze([
  Object.freeze({
    flag: WORKOUT_MODIFIERS.UpperBodyClothing,
    isReviewed: (exercise) =>
      exercise.upperBodyClothingRequirement ===
        EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.ClothingRequired ||
      exercise.upperBodyClothingRequirement ===
        EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.BareUpperBodyRequired ||
      exercise.upperBodyClothingRequirement ===
        EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.Agnostic,
    isCompatibleForProfile: (exercise, profile) => {
      const clothingOn =
        (profile & WORKOUT_MODIFIERS.UpperBodyClothing) !== 0;
      if (exercise.upperBodyClothingRequirement ===
          EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.ClothingRequired) {
        return clothingOn;
      }
      if (exercise.upperBodyClothingRequirement ===
          EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.BareUpperBodyRequired) {
        return !clothingOn;
      }
      return exercise.upperBodyClothingRequirement ===
        EXERCISE_UPPER_BODY_CLOTHING_REQUIREMENT.Agnostic;
    },
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.HardFloor,
    isReviewed: (exercise) =>
      exercise.hardFloorCompatibility ===
        EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible ||
      exercise.hardFloorCompatibility ===
        EXERCISE_HARD_FLOOR_COMPATIBILITY.Incompatible,
    isCompatibleForProfile: (exercise, profile) =>
      (profile & WORKOUT_MODIFIERS.HardFloor) === 0 ||
      exercise.hardFloorCompatibility ===
        EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible,
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Insect,
    isReviewed: (exercise) =>
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible ||
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Incompatible,
    isCompatibleForProfile: (exercise, profile) =>
      (profile & WORKOUT_MODIFIERS.Insect) === 0 ||
      exercise.insectCompatibility === EXERCISE_INSECT_COMPATIBILITY.Compatible,
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Silence,
    isReviewed: (exercise) => typeof exercise.silent === "boolean",
    isCompatibleForProfile: (exercise, profile) =>
      (profile & WORKOUT_MODIFIERS.Silence) === 0 || exercise.silent === true,
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Shy,
    isReviewed: (exercise) =>
      exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Compatible ||
      exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Incompatible,
    isCompatibleForProfile: (exercise, profile) =>
      (profile & WORKOUT_MODIFIERS.Shy) === 0 ||
      exercise.shyCompatibility === EXERCISE_SHY_COMPATIBILITY.Compatible,
  }),
  Object.freeze({
    flag: WORKOUT_MODIFIERS.Mirror,
    isReviewed: isMirrorMetadataReviewed,
    isCompatibleForProfile: isMirrorCompatible,
  }),
]);

function isMirrorMetadataReviewed(exercise) {
  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly) {
    return exercise.equipment === "Mirror" &&
      (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
       exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody);
  }

  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly) {
    return exercise.equipment === "None" &&
      (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
       exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.FullBody);
  }

  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.Agnostic) {
    return exercise.equipment === "None" &&
      exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.None;
  }

  return false;
}

function isMirrorCompatible(exercise, profile) {
  if (exercise.mirrorRelationship !== EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly) {
    return true;
  }

  const equipment = getMirrorEquipment(profile);
  if (equipment === MIRROR_EQUIPMENT.None) {
    return false;
  }
  return equipment === MIRROR_EQUIPMENT.Tall ||
    exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody;
}

function getModifierRuleStateProfiles(rule) {
  return rule.flag === WORKOUT_MODIFIERS.Mirror
    ? [
        WORKOUT_MODIFIERS.None,
        WORKOUT_MODIFIERS.Mirror,
        WORKOUT_MODIFIERS.Mirror | WORKOUT_MODIFIERS.TallMirror,
      ]
    : [WORKOUT_MODIFIERS.None, rule.flag];
}

function createWorkoutModifierValidationProfiles() {
  const profiles = [WORKOUT_MODIFIERS.None];
  for (const rule of MODIFIER_RULES) {
    profiles.push(...getModifierRuleStateProfiles(rule));
  }
  for (let firstIndex = 0; firstIndex < MODIFIER_RULES.length - 1; firstIndex += 1) {
    for (let secondIndex = firstIndex + 1;
      secondIndex < MODIFIER_RULES.length;
      secondIndex += 1) {
      for (const firstState of getModifierRuleStateProfiles(MODIFIER_RULES[firstIndex])) {
        for (const secondState of getModifierRuleStateProfiles(MODIFIER_RULES[secondIndex])) {
          profiles.push(normalizeWorkoutModifiers(firstState | secondState));
        }
      }
    }
  }
  return profiles.filter((profile, index) => profiles.indexOf(profile) === index);
}
export const SUPPORTED_WORKOUT_MODIFIER_MASK = MODIFIER_RULES.reduce(
  (mask, rule) => mask | rule.flag,
  WORKOUT_MODIFIERS.TallMirror |
    WORKOUT_MODIFIERS.Wall |
    WORKOUT_MODIFIERS.SoleWallContact |
    WORKOUT_MODIFIERS.Light,
);
export const WORKOUT_MODIFIER_VALIDATION_PROFILES = Object.freeze(
  createWorkoutModifierValidationProfiles(),
);
const SELECTION_PROFILE_PREFIX = "p";
const SELECTION_PROFILE_SEPARATOR = "|";
const MINIMUM_CANONICAL_COVERAGE_PERCENT = 50;
export const BROAD_COVERAGE_RESOLUTION_MINUTES = 3;
export const MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP = 1;
export const MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP = 1;
export const MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP = 1;
export const MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS = 20;
export const MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS = 5;
export const MINIMUM_MODIFIER_MATERIALITY_EXERCISES = 5;
export const MINIMUM_MODIFIER_MATERIALITY_PERCENT = 5;
export const MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT = 10;
export const MINIMUM_MUSCULAR_DEMAND = 0;
export const MODERATE_MUSCULAR_DEMAND = 1;
export const MAXIMUM_MUSCULAR_DEMAND = 2;

// Insect mode requires visible continuous whole-body movement. Pelvic-floor
// isolation cannot honestly meet that contract under NomadicMethod's feet-only rules.
// Intrinsic-hand work can meet it only when a wall is available. Keep these
// exceptions exact instead of manufacturing secondary claims or artificial
// marching combinations.
export const INSECT_FINE_COVERAGE_EXCEPTIONS = Object.freeze([
  "PelvicFloorAndPerineum",
]);
const INSECT_FINE_COVERAGE_EXCEPTION_SET = new Set(
  INSECT_FINE_COVERAGE_EXCEPTIONS,
);
export const WALL_FREE_INSECT_FINE_COVERAGE_EXCEPTIONS = Object.freeze([
  "IntrinsicHand",
]);
const WALL_FREE_INSECT_FINE_COVERAGE_EXCEPTION_SET = new Set(
  WALL_FREE_INSECT_FINE_COVERAGE_EXCEPTIONS,
);

// Exact catalog gaps accepted by the owner on 12 September 2026. The normal
// round allocator preserves the selected duration using the remaining slots.
export const ACCEPTED_COVERAGE_EXCEPTIONS = Object.freeze([
  ["r15.scapular-chest-breathing", WORKOUT_MODIFIERS.Insect],
  ["r15.shoulder", WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor],
  ["r20.shoulder-adduction-extension", WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor],
  ["r30.rotator-cuff", WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor],
  ["r30.shoulder-adductors-extensors", WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.HardFloor],
  ["r30.breathing-muscles", WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Silence],
  ["r30.breathing-muscles", WORKOUT_MODIFIERS.Insect | WORKOUT_MODIFIERS.Shy],
].map(([groupId, requiredModifiers]) => Object.freeze({ groupId, requiredModifiers })));

function isCoverageException(group, profile) {
  if (ACCEPTED_COVERAGE_EXCEPTIONS.some((exception) =>
    exception.groupId === getSelectionKey(group) &&
    (profile & exception.requiredModifiers) === exception.requiredModifiers)) {
    return true;
  }
  if ((profile & WORKOUT_MODIFIERS.Insect) === 0 ||
      group.canonicalGroups.length === 0) {
    return false;
  }

  const wallAvailable = (profile & WORKOUT_MODIFIERS.Wall) !== 0;
  return group.canonicalGroups.every((canonicalGroup) =>
    INSECT_FINE_COVERAGE_EXCEPTION_SET.has(canonicalGroup) ||
    (!wallAvailable &&
      WALL_FREE_INSECT_FINE_COVERAGE_EXCEPTION_SET.has(canonicalGroup)));
}

export function isSelectionGroupAvailable(group, profile) {
  return !isCoverageException(group, profile);
}


export function getMinimumExercisesPerModifierPairStatePerGroup(minutes) {
  return minutes === BROAD_COVERAGE_RESOLUTION_MINUTES
    ? MINIMUM_EXERCISES_PER_BROAD_MODIFIER_PAIR_STATE_PER_GROUP
    : MINIMUM_EXERCISES_PER_FINE_MODIFIER_PAIR_STATE_PER_GROUP;
}
export const HARD_MUSCULAR_DEMAND = MAXIMUM_MUSCULAR_DEMAND;

export function getMuscularDemandSchedulePriority(muscularDemand) {
  switch (muscularDemand) {
    case MINIMUM_MUSCULAR_DEMAND:
      return 0;
    case MAXIMUM_MUSCULAR_DEMAND:
      return 1;
    case MODERATE_MUSCULAR_DEMAND:
      return 2;
    default:
      throw new RangeError("Muscular demand must be 0, 1, or 2.");
  }
}

export const MODERATE_RECOVERY_WINDOW_MS = 18 * 60 * 60 * 1000;
export const HARD_RECOVERY_WINDOW_MS = 36 * 60 * 60 * 1000;
export const HARD_ROTATION_STATUS = Object.freeze({
  RecoveringHard: "RecoveringHard",
  Neutral: "Neutral",
  FreshHard: "FreshHard",
});
export const WORKOUT_EXERCISE_PHASE = Object.freeze({
  Unknown: "Unknown",
  Warmup: "Warmup",
  PeakPerformance: "PeakPerformance",
  Fatigued: "Fatigued",
});
export const WARMUP_FINAL_BLOCK = 15;
export const PEAK_PERFORMANCE_FINAL_BLOCK = 45;

export function getWorkoutExercisePhase(oneBasedBlockOrder) {
  if (!Number.isInteger(oneBasedBlockOrder) || oneBasedBlockOrder <= 0) {
    throw new RangeError("Exercise block order must be a positive integer.");
  }
  if (oneBasedBlockOrder <= WARMUP_FINAL_BLOCK) {
    return WORKOUT_EXERCISE_PHASE.Warmup;
  }
  return oneBasedBlockOrder <= PEAK_PERFORMANCE_FINAL_BLOCK
    ? WORKOUT_EXERCISE_PHASE.PeakPerformance
    : WORKOUT_EXERCISE_PHASE.Fatigued;
}

function isPersistableExercisePhase(phase) {
  return phase === WORKOUT_EXERCISE_PHASE.Warmup ||
    phase === WORKOUT_EXERCISE_PHASE.PeakPerformance ||
    phase === WORKOUT_EXERCISE_PHASE.Fatigued;
}
export const MINIMUM_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS = 2;
export const MINIMUM_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS = 1;
export const MODERATE_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS = 4;
export const MODERATE_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS = 2;
export const HARD_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS = 8;
export const HARD_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS = 4;
export const MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR = 1;
export const MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR = 4;
export const MUSCLE_BALANCE_MAX_REBALANCE_PASSES = 30;
export const DEFAULT_WORKOUT_MODIFIERS =
  WORKOUT_MODIFIERS.UpperBodyClothing |
  WORKOUT_MODIFIERS.HardFloor |
  WORKOUT_MODIFIERS.Silence;
export const CURRENT_WORKOUT_STATE_VERSION = 27;
const DOMINANT_LIGHT_MODE_STATE_VERSION = 26;
const EXPLICIT_LIGHT_MODE_STATE_VERSION = 25;
const IMPLICIT_UPPER_BODY_CLOTHING_STATE_VERSION = 24;
const LEGACY_TRAINING_DAY_INFERENCE_STATE_VERSION = 22;
const PERSISTED_LIGHT_DAY_STATE_VERSION = 21;
const PHASE_SCOPED_DOWNVOTE_STATE_VERSION = 20;
const SLOT_SCOPED_PREFERENCE_STATE_VERSION = 19;
const IMPLICIT_HARD_FLOOR_STATE_VERSION = 18;
const EXPLICIT_MIRROR_EQUIPMENT_STATE_VERSION = 9;
const IMPLICIT_SILENCE_STATE_VERSION = 5;
const SOURCE_STATE_VERSION = Symbol("sourceStateVersion");
export const MOVEMENT_DURATION_MS = 45_000;
export const PREPARATION_DURATION_MS = 5_000;
export const REST_DURATION_MS = 15_000;
export const LIGHT_DAY_DAILY_REGULAR_MINUTES_CAP = 60;
export const LIGHT_DAY_REGULAR_MINUTES_BEFORE_LIGHT = 180;
export const MINIMUM_LEGACY_HARD_PRIMARY_MUSCLES = 3;
// Revisions 71-72 correct overlooked arm positions, lead stances, and one-way
// demonstrations to complete atomic side/direction sequences. Rebuild affected
// lineups without discarding saved feedback.
export const CURRENT_CATALOG_REVISION = 79;
const HARD_FLOOR_SLIPPERINESS_CATALOG_REVISION = 53;
const REUSED_SHY_AUDIT_CATALOG_REVISION = 70;
const REUSED_SHY_AUDIT_EXERCISE_IDS = new Set([202, 204, 205]);
// Anatomy migrations revalidate only saved slot preferences containing an
// exercise whose training claims changed. They never erase the exercise's
// global or phase score merely because a secondary association was fixed.
const TRAINING_CLAIM_ASSOCIATION_CHANGES_BY_REVISION = new Map([
  [67, new Set([
    31, 41, 56, 94, 95, 98, 99, 100, 113, 114, 115, 118, 119, 120, 126,
    129, 133, 134, 135, 137, 143, 144, 146, 149, 151, 153, 154, 159, 166,
    167, 168, 169, 172, 173, 175, 184, 192, 195, 196, 201, 212, 217, 218,
    230, 231, 232, 237, 242, 245, 251, 256, 260, 263, 265, 266, 269, 271,
    272, 274, 275, 276, 280, 282, 283, 287, 288, 291, 294, 296, 326, 327,
    338, 367, 393, 396, 403, 406, 428, 429, 430, 431, 432, 433, 434, 435,
    437, 440, 443, 448, 452, 457, 461, 472, 473, 484, 487, 508, 509, 529,
    532, 537, 538, 546, 556, 560, 584, 591, 603, 608, 611, 613, 616, 617,
    620, 632, 636, 647, 654, 681, 685, 701, 702, 703, 758, 816, 818, 831,
    845, 886, 887, 915, 939, 943, 958, 969, 971, 986, 996, 997,
  ])],
  [69, new Set([
    15, 21, 32, 47, 58, 92, 119, 167, 168, 169, 194, 211, 213,
    216, 220, 225, 231, 233, 236, 239, 241, 245, 248, 258, 269,
    274, 277, 282, 283, 285, 286, 290, 291, 294, 295, 296, 326, 327, 390,
    393, 403, 404, 406, 413, 417, 420, 428, 431, 432, 433, 434,
    435, 439, 440, 441, 442, 443, 444, 445, 448, 450, 452, 458,
    462, 463, 464, 465, 469, 471, 472, 475, 476, 488, 523, 524,
    537, 541, 543, 545, 546, 548, 556, 561, 573, 575, 578, 583, 613,
    681, 685, 712, 745, 790,
  ])],
  [73, new Set([
    15, 16, 17, 19, 20, 21, 31, 32, 37, 41, 47, 58, 59,
    60, 93, 94, 95, 96, 97, 98, 99, 100, 102, 103, 104, 105,
    107, 108, 109, 112, 113, 114, 115, 116, 117, 119, 120, 121, 122,
    123, 124, 125, 126, 127, 128, 129, 130, 133, 134, 135, 136, 137,
    138, 140, 142, 143, 145, 146, 147, 148, 149, 150, 152, 153, 154,
    156, 159, 160, 163, 165, 166, 167, 168, 169, 170, 171, 172, 174,
    175, 176, 177, 178, 179, 181, 182, 183, 184, 185, 186, 187, 188,
    190, 192, 193, 195, 196, 197, 198, 199, 200, 201, 203, 204, 205,
    214, 217, 218, 219, 220, 223, 227, 228, 230, 231, 232, 234, 237,
    238, 240, 241, 242, 245, 246, 248, 251, 252, 253, 254, 256, 257,
    258, 260, 261, 262, 263, 264, 265, 266, 268, 269, 270, 271, 272,
    274, 275, 276, 277, 278, 279, 280, 281, 282, 283, 284, 285, 286,
    287, 288, 289, 290, 291, 292, 293, 294, 295, 296, 301, 302, 303,
    304, 305, 307, 308, 309, 310, 311, 314, 315, 321, 326, 327, 329,
    338, 340, 341, 367, 377, 379, 389, 390, 391, 392, 393, 394, 395,
    396, 397, 398, 399, 400, 401, 402, 403, 404, 405, 406, 407, 408,
    410, 411, 412, 413, 414, 415, 416, 417, 418, 419, 420, 421, 424,
    426, 427, 428, 429, 430, 431, 432, 433, 434, 435, 436, 437, 438,
    439, 440, 441, 442, 443, 444, 445, 446, 447, 448, 449, 452, 453,
    454, 455, 456, 457, 458, 459, 460, 461, 462, 463, 464, 465, 466,
    467, 468, 469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
    486, 487, 488, 489, 490, 491, 492, 493, 494, 495, 496, 499, 501,
    502, 503, 504, 507, 508, 509, 510, 512, 513, 517, 518, 519, 520,
    522, 523, 524, 525, 526, 527, 528, 529, 530, 531, 532, 533, 535,
    536, 537, 538, 539, 540, 541, 542, 543, 544, 545, 546, 547, 548,
    549, 550, 551, 552, 554, 555, 556, 557, 560, 561, 562, 563, 564,
    565, 567, 568, 569, 570, 572, 574, 575, 576, 577, 578, 579, 580,
    583, 584, 585, 586, 587, 588, 591, 603, 608, 609, 610, 611, 612,
    613, 614, 615, 616, 617, 618, 619, 620, 625, 626, 632, 633, 636,
    647, 649, 654, 666, 677, 678, 681, 683, 684, 685, 686, 687, 701,
    702, 703, 704, 712, 733, 740, 741, 742, 743, 744, 745, 746, 747,
    748, 750, 752, 755, 756, 758, 784, 790, 801, 804, 816, 818, 825,
    831, 834, 835, 843, 845, 884, 885, 886, 887, 905, 906, 910, 911,
    913, 914, 915, 916, 919, 939, 943, 948, 949, 954, 958, 960, 962,
    969, 971, 973, 986, 987, 993, 996, 997, 998, 999, 1000,
  ])],
]);
export const LAST_CUMULATIVE_CATALOG_REVISION = 3;
export const SCOPED_CATALOG_INVALIDATIONS_BY_REVISION = new Map([
  [4, new Set([591])],
  [5, new Set([266])],
  [6, new Set([266])],
  [7, new Set([326])],
  [8, new Set([211, 212, 213, 214, 232, 233, 234, 236])],
  [9, new Set([195])],
  [10, new Set([126, 135, 338, 686])],
  [11, new Set([
    211, 213, 214, 215, 216, 217, 218, 232,
    233, 234, 236, 237, 240, 241, 283, 289,
  ])],
  [12, new Set([513, 843])],
  [13, new Set([223, 224, 225, 245, 246])],
  [16, new Set([234, 239, 240])],
  [18, new Set([115, 119, 140, 212, 260, 326, 340, 512, 649])],
  [20, new Set([
    211, 213, 214, 215, 218, 223, 224,
    236, 237, 241, 242, 245, 283, 289,
  ])],
  [21, new Set([
    15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
    179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
    256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
    283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
    394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
    615, 618, 677, 683, 685, 745, 816, 834,
  ])],
  [22, new Set([
    117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
    263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
  ])],
  [23, new Set([
    407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
  ])],
  [24, new Set([
    420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
  ])],
  [25, new Set([
    31, 219, 248, 282, 390, 394, 395,
    397, 508, 576, 577, 618, 816, 834,
  ])],
  [26, new Set([31, 282, 391, 507, 508, 577])],
  [27, new Set([231, 685, 687])],
  [28, new Set([251])],
  [29, new Set([
    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
    486, 487, 488, 489, 494, 496, 517, 518, 519,
  ])],
  [30, new Set([
    229, 467, 474, 481, 483, 491, 493, 495, 497, 499,
    501, 504, 513, 516,
  ])],
  [31, new Set([414, 415, 416, 418, 419])],
  [32, new Set([31, 219, 395, 507, 577, 618, 654, 834])],
  [33, new Set([
    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
  ])],
  [34, new Set([98, 390, 508, 576, 816])],
  [35, new Set([219])],
  [36, new Set([684])],
  [37, new Set([31, 176, 195, 391, 413, 884, 885])],
  [41, new Set([500])],
  [42, new Set([105, 107, 108, 245, 280, 591, 884, 885, 905])],
  [43, new Set([90, 94, 95, 99, 100, 497, 498, 511, 514])],
  [44, new Set([90, 94, 95, 99, 100, 497, 498, 500, 511, 514])],
  [45, new Set([
    264, 275, 406, 409, 460, 588, 608, 611, 617, 620, 743,
    757, 759, 760, 761, 762, 763, 764,
  ])],
  [46, new Set([
    265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
  ])],
  [47, new Set([
    198, 398, 421, 427, 468, 512, 515,
  ])],
  [49, new Set([
    520, 521, 529, 530, 531, 532, 533, 534, 535, 536, 537,
    538, 539, 540, 541, 542, 543, 545, 546,
  ])],
  [50, new Set([31, 169, 219, 547, 548])],
  [51, new Set([
    439, 442, 444, 478,
    549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
    559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
    569, 570, 571, 574, 575, 578, 581, 582, 583,
  ])],
  [52, new Set([
    32, 58, 92, 95, 104, 105, 107, 108, 109, 119, 167, 168,
    169, 190, 193, 195, 252, 253, 267, 282, 295, 296, 390,
    391, 392, 393, 394, 395, 397, 398, 399, 400, 401, 407,
    408, 410, 411, 412, 413, 417, 420, 424, 426, 427, 428,
    431, 432, 433, 434, 435, 436, 437, 438, 440, 441, 443,
    445, 448, 450, 451, 452, 455, 456, 457, 458, 459, 460,
    461, 462, 463, 464, 465, 469, 471, 472, 475, 476, 478,
    479, 480, 484, 487, 488, 517, 530, 537, 548, 549, 550,
    551, 552, 553, 554, 555, 556, 557, 558, 559, 560, 561,
    562, 563, 564, 565, 566, 567, 568, 569, 570, 571, 574,
    575, 578, 581, 582, 583, 591, 609, 610, 611, 612, 613,
    615, 616, 619, 687, 884, 885, 886, 887,
  ])],
  [53, new Set([
    17, 19, 37, 41, 58, 60, 92, 93, 97, 103, 104, 105,
    107, 108, 109, 112, 116, 117, 120, 121, 122, 123, 124, 125,
    126, 127, 128, 129, 133, 136, 142, 143, 150, 156, 163, 174,
    178, 180, 181, 182, 183, 184, 190, 192, 193, 195, 199, 203,
    231, 232, 245, 278, 279, 280, 282, 303, 311, 314, 315,
    326, 340, 404, 408, 412, 478, 484, 508, 509, 534, 535,
    536, 538, 572, 576, 591, 610, 611, 626, 633, 636, 685, 687,
    733, 746, 748, 750, 816, 884, 885, 886, 887, 905, 915, 971,
    973, 986, 999,
  ])],
  [54, new Set([563, 564, 567, 568, 574])],
  [55, new Set([790, 993])],
  [56, new Set([
    218, 234, 237, 239, 240, 241, 242, 283, 291, 556,
  ])],
  [57, new Set([287])],
  [58, new Set([
    218, 234, 237, 239, 241, 283, 291, 294, 556,
  ])],
  [59, new Set([565])],
  [60, new Set([397])],
  [61, new Set([302, 304, 305, 307, 308, 309, 310])],
  [62, new Set([248, 281, 286, 367, 393, 529, 537, 545])],
  [63, new Set([439, 442, 444])],
  [64, new Set([
    104, 113, 117, 120, 123, 135, 177, 184, 186, 199,
    256, 261, 626, 677, 845, 996, 997,
  ])],
  [65, new Set([507])],
  [66, new Set([524, 525, 526, 527, 528, 790])],
  [67, new Set([911, 913, 916, 917])],
  [69, new Set([918, 919])],
  [70, new Set([
    56, 59, 98, 108, 176, 185, 188, 190, 202, 203, 204, 205,
    220, 224, 231, 258, 269, 283, 289, 290, 377, 379, 392,
    398, 399, 400, 401, 402, 403, 404, 405, 410, 474, 481,
    498, 543, 557, 608, 609, 678, 685, 687,
  ])],
  [71, new Set([32, 483, 493])],
  [72, new Set([307, 490, 491, 492, 495, 499, 501, 520, 528, 561, 958])],
  [73, new Set([
    16, 20, 21, 32, 47, 95, 97, 99, 100, 117, 123, 125, 126,
    128, 129, 131, 133, 143, 144, 145, 151, 152, 153, 160, 161, 170,
    171, 173, 178, 181, 182, 195, 198, 203, 215, 219, 220, 223, 228,
    233, 245, 258, 261, 264, 266, 274, 275, 279, 280, 283, 285, 286,
    289, 290, 291, 292, 301, 307, 327, 329, 379, 395, 414, 418, 422, 423, 460, 473,
    483, 485, 486, 488, 491, 500, 515, 519, 520, 521, 531, 533, 538,
    541, 542, 546, 556, 560, 569, 570, 572, 585, 587, 588, 610, 614, 618,
    633, 636, 654, 677, 681, 741, 743, 744, 745, 747, 748, 751, 834,
    948,
  ])],
]);
export const SCOPED_SCORE_INVALIDATIONS_BY_REVISION = new Map([
  [4, new Set([591])],
  [5, new Set([266])],
  [6, new Set([266])],
  [7, new Set([326])],
  [8, new Set([211, 212, 213, 214, 232, 233, 234, 236])],
  [9, new Set([195])],
  [10, new Set([126, 135, 338, 686])],
  [11, new Set([
    211, 213, 214, 215, 216, 217, 218, 232,
    233, 234, 236, 237, 240, 241, 283, 289,
  ])],
  [12, new Set([513, 843])],
  [13, new Set([223, 224, 225, 245, 246])],
  [16, new Set([234, 239, 240])],
  [18, new Set([115, 212, 260, 512, 649])],
  [20, new Set([
    211, 213, 214, 215, 218, 223, 224,
    236, 237, 241, 242, 245, 283, 289,
  ])],
  [21, new Set([
    15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
    179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
    256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
    283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
    394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
    615, 618, 677, 683, 685, 745, 816, 834,
  ])],
  [22, new Set([
    117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
    263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
  ])],
  [23, new Set([
    407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
  ])],
  [24, new Set([
    420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
  ])],
  [27, new Set([687])],
  [28, new Set([251])],
  [29, new Set([
    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
    486, 487, 488, 489, 494, 496, 517, 518, 519,
  ])],
  [30, new Set([229, 497, 501, 504, 513])],
  [31, new Set([414, 415, 416, 418, 419])],
  [32, new Set([31, 219, 395, 507, 577, 618, 654, 834])],
  [33, new Set([
    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
  ])],
  [36, new Set([684])],
  [43, new Set([90, 94, 95, 99, 100, 497, 498, 511, 514])],
  [44, new Set([90])],
  [45, new Set([
    264, 275, 406, 409, 460, 588, 608, 611, 743,
    757, 759, 760, 761, 762, 763, 764,
  ])],
  [49, new Set([
    520, 521, 529, 530, 531, 532, 533, 534, 535, 536, 537,
    538, 539, 540, 541, 542, 543, 545, 546,
  ])],
  [50, new Set([547, 548])],
  [51, new Set([
    439, 442, 444, 478,
    549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
    559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
    569, 570, 571, 574, 575, 578, 581, 582, 583,
  ])],
  [54, new Set([563, 564, 567, 568, 574])],
  [55, new Set([790, 993])],
  [56, new Set([
    218, 234, 237, 239, 240, 241, 242, 283, 291, 556,
  ])],
  [57, new Set([287])],
  [58, new Set([
    218, 234, 237, 239, 241, 283, 291, 294, 556,
  ])],
  [61, new Set([302, 304, 305, 307, 308, 309, 310])],
  [65, new Set([507])],
  [66, new Set([524, 525, 526, 527, 528, 790])],
  [67, new Set([911, 913, 916, 917])],
  [69, new Set([918, 919])],
  [70, new Set([202, 204, 205])],
  [73, new Set([
    261, 266, 289, 290, 301, 473, 485, 486, 515, 519, 520, 521, 560,
    569, 570, 614, 677,
  ])],
]);
const ALTERNATING_PREFIX = "Alternating ";
const CONTINUOUS_ALTERNATION_NORMALIZATION_IDS = new Set();
export const APPROVED_EXERCISE_CORRECTIONS = new Map([
  [31, ["High-Knee Overhead-Reach March", "Alternating Knee Raises with Two-Arm Pull-Down"]],
  [21, ["Standing-Scale Balance", "Alternating Single-Leg Hinge with Forward Reach"]],
  [105, ["Plie Squat", "Wide Turned-Out Squat"]],
  [119, ["Squat to Calf Raise", "Tiptoe Walking Back and Forth"]],
  [139, ["Wide-Squat Heel Raise", "Wide-Squat Alternating Heel Raises"]],
  [188, ["Parallel Demi-Plie", "Narrow Turned-Out Shallow Squat"]],
  [197, ["First-Position Plie-Releve", "Squat to Calf Raise"]],
  [198, ["Second-Position Plie-Releve", "Wide Squat to Feet-Together Calf Raise"]],
  [199, ["Alternating Deep Side Lunge", "Horse-Stance Squat"]],
  [255, ["Standing Bent-Knee Calf Raise", "Deep-Squat Calf Raise"]],
  [145, ["Standing Knee Extension", "Wall-Supported Standing Knee-Extension Hold"]],
  [256, ["Self-Resisted Overhead Pull", "Self-Resisted Overhead Pull Hold"]],
  [257, ["Self-Resisted Chest-Level Pull", "Finger Spreading with Arms Held Forward"]],
  [258, ["Self-Resisted Low Pull", "Alternating Karate Downward Blocks"]],
  [262, ["Standing Hands-to-Thigh Abdominal Press", "Standing Hands-to-Thigh Abdominal Press Hold"]],
  [270, ["Bodyweight Svend Press", "Goalpost Arm Hold"]],
  [282, ["High-Knee Horizontal Punches", "Side-Step Knee Drive with Alternating Side Punches"]],
  [219, ["Single-Side High-Knee Cross-Body Pull", "High-Knee Cross-Body Pull"]],
  [684, ["Karate Step-Through Cross-Elbow Strike", "Knee Strike to Horizontal Elbow Strike"]],
  [290, ["Universe-in-Motion Qigong", "Thumb and Little-Finger Switches"]],
  [231, ["Karate Reverse Punch", "Step-In Karate Reverse Punch"]],
  [394, ["Standing Arms Open and Close", "Alternating Cross-Body Knee with Arm Sweep"]],
  [395, ["Standing Overhead Arm Sweep", "Alternating Knee Lift and Overhead Reach"]],
  [397, ["Inhale Open, Exhale Cross-Body Side Tap", "Alternating Side Tap with Diagonal Reach"]],
  [398, ["Standing Hug and Arm Expansion", "Inhale Arms Open, Exhale Arms Together"]],
  [399, ["Shallow Squat with Chest-Opening Arms", "Inhale Chest Open, Exhale Arms Close with Shallow Squat"]],
  [400, ["Shallow Squat with Overhead Arm Circle", "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down"]],
  [401, ["Alternating Weight Shift with Arm Swing", "Alternating Inhale-Twist, Exhale-Push"]],
  [402, ["Shibashi Rowing-a-Boat Breathing", "Shallow Squat with Rowing Arm Circle"]],
  [403, ["Shibashi Alternating Pushing-Palms Breathing", "Alternating Palm Press with Weight Shift"]],
  [404, ["Shibashi Alternating Punch Breathing", "Wide-Stance Alternating Slow Punch"]],
  [405, ["Shibashi Flying-Wild-Goose Breathing", "Shallow Squat with Wing Arm Raise"]],
  [406, ["Shibashi Spinning-Wheels Breathing", "Standing Wheel Arm Circles"]],
  [409, ["Neck Controlled Articular Rotation", "Full Neck Circles"]],
  [493, ["Track Finger Upper-Right to Lower-Left", "Diagonal Finger Tracking"]],
  [958, ["Standing Alternating Side Bend", "Standing Overhead Side-Stretch Hold"]],
  [425, ["Chin-Tuck Isometric", "Chin-Tuck Hold"]],
  [396, ["Unsupported Single-Leg Balance", "Standing Front-to-Side Knee Lifts"]],
  [510, ["Clasped-Hands Chest-Opening Forward Fold", "Clasped-Hands Chest-Opening Forward-Fold Hold"]],
  [507, ["Hamstring Curl with Elbow Pull", "Knee Raise with Elbow Pull"]],
  [508, ["Wide-Step Elbow Pull", "Alternating Side Taps with Forward Overhead Raises"]],
  [588, ["Belly-Dance Alternating Shoulder Roll", "Belly-Dance Alternating Shoulder Rolls"]],
  [577, ["High-Knee Goalpost Pull", "Standing Side Crunch and Side Kick"]],
  [915, ["Split-Stance Knee Drive with Overhead Reach", "Single-Side Split-Stance Knee Drive with Overhead Reach"]],
  [617, ["Standing Side-Leg Circles", "Standing Forward Side-Leg Circles"]],
  [626, ["Sumo Stance", "Sumo Squat Hold"]],
  [712, ["Standing Arms-Back Chest Opener", "Standing Arms-Back Chest-Opener Hold"]],
  [969, ["Chair-Pose Core Hold", "Chair-Pose Hold"]],
  [1000, ["Standing Forward Fold", "Standing Forward-Fold Hold"]],
  [136, ["Goddess Pose", "Wide Squat Hold with Hands on Thighs"]],
  [225, ["Clenched-Fist Wrist Extensor Stretch", "Opposite-Hand Fist-Down Wrist Stretch"]],
  [241, ["Hook-Fist Tendon Glide", "Open Hand to Hook Fist"]],
  [242, ["Full-Fist Tendon Glide", "Open Hand to Full Fist"]],
  [248, ["Side-Tap Palm Pushes", "Alternating Side-Tap Palm Pushes"]],
  [283, ["Straight-Fist Tendon Glide", "Rear-Hand Palm Strike"]],
  [291, ["Open-to-Claw Tendon Glide", "Open Hand to Claw Fist"]],
  [293, ["Finger-Web Space Stretch", "Opposite-Hand Thumb-Web Stretch"]],
  [683, ["Alternating Palm-Up T-Arm Flips", "Alternating Palm-Up Shoulder Rotations"]],
  [214, ["Forward Wrist Circles", "Single-Arm Wrist Circles"]],
  [223, ["Forward Controlled Wrist Circles", "Controlled Wrist Circles"]],
  [755, ["Reverse Wrist Circles", "Outward Wrist Circles"]],
  [756, ["Reverse Controlled Wrist Circles", "Outward Controlled Wrist Circles"]],
  [758, ["Reverse Knee-and-Ankle Circles", "Backward Knee-and-Ankle Circles"]],
  [94, ["Mirror-Guided Lateral Weight Shift", "Lateral Weight Shift"]],
  [95, ["Mirror-Guided Single-Leg Pelvic Control", "Single-Leg Knee-Raise Hold"]],
  [99, ["Mirror-Guided Bent-Knee Front-to-Back Leg Swing", "Bent-Knee Front-to-Back Leg Swing"]],
  [100, ["Mirror-Guided Bent-Knee Leg Swing with Pause", "Bent-Knee Leg Swing with Pause"]],
  [497, ["Mirror-Guided Eyebrow Raise", "Eyebrow Raise"]],
  [498, ["Mirror-Guided Firm Eye Closure", "Firm Eye Closure"]],
  [500, ["Mirror-Guided Straight Jaw Opening", "Straight Jaw Opening"]],
  [511, ["Mirror-Guided Lip Pucker", "Lip Pucker"]],
  [514, ["Mirror-Guided Symmetric Smile", "Symmetric Smile"]],
  [515, ["One-Eyebrow Isolation Practice", "Raise Upper Lip and Scrunch Nose"]],
  [522, ["Tutting Box Sequence", "Tutting Box Sequence"]],
  [523, ["Arm-Wave Isolation Practice", "Pass an Arm Wave from Hand to Hand"]],
  [524, ["Mirror Front Double-Biceps Pose Hold", "Front Double-Biceps Posing"]],
  [525, ["Mirror Front Lat-Spread Pose Hold", "Front Lat-Spread Posing"]],
  [526, ["Mirror Side-Chest Pose Hold", "Side-Chest Posing"]],
  [527, ["Mirror Side-Triceps Pose Hold", "Side-Triceps Posing"]],
  [528, ["Mirror Abdominals-and-Thighs Pose Hold", "Abdominals-and-Thighs Posing"]],
  [790, ["Mirror Most-Muscular Pose Hold", "Most-Muscular Posing, Hands on Thighs"]],
  [193, ["Wide-Squat Floor-to-Overhead Reach", "Hip Hinge with Overhead Reach"]],
  [417, ["Narrow Squat and Overhead Reach with Thumb Tracking", "Narrow-Stance Overhead-to-Toe Reach"]],
  [439, ["Feet-Together Fixed-Gaze Head Turns", "Pogo Bounces with Fixed-Gaze Head Turns"]],
  [442, ["Feet-Together Fixed-Gaze Head Nods", "Pogo Bounces with Fixed-Gaze Head Nods"]],
  [444, ["Feet-Together Fixed-Gaze Head Tilts", "Pogo Bounces with Fixed-Gaze Head Tilts"]],
  [556, ["Tiptoe Raises with Fist Clenches", "Single-Arm Backfist"]],
  [561, ["Tiptoe Bourree Steps with Head Spot", "Tiptoe Turn with Head Spot"]],
  [562, ["Ballet Rises with Arm Movement", "First-Position Calf Raises"]],
  [564, ["Calf Raise with Pelvic Floor Contraction", "Parallel Calf Raises with Hands on Hips"]],
  [565, ["Pelvic-Floor Mini Squat to Calf Raise", "Mini-Squat Calf Raises with Forward Reach"]],
  [566, ["Parallel Calf Raises for Pelvic-Floor Support", "Parallel Calf Raises"]],
  [581, ["Toes-In Calf Raises for Pelvic-Floor Support", "Toes-In Calf Raises"]],
  [582, ["Toes-Out Calf Raises for Pelvic-Floor Support", "Toes-Out Calf Raises"]],
  [615, ["Hamstring Curl with Prayer Hands", "Alternating Hamstring Curl with Prayer-to-Open Arms"]],
  [138, ["Narrow-Squat Heel Raise", "Narrow Squat to Calf Raise"]],
  [140, ["Sumo-Squat Calf Raise", "Tiptoe Sumo Squat"]],
  [152, ["Side-Leg Raise Pulse", "Alternating Side-Leg Raise Pulses"]],
  [390, ["Inhale Arms Up, Exhale Step-Touch", "Step-Touch with Goalpost Arm Openings"]],
  [391, ["Inhale Arms Open, Exhale High-Knee", "Alternating High-Knee Inner-Foot Taps"]],
  [408, ["Split-Squat Torso Rotation with Thumb Tracking", "Staggered-Stance Torso Rotation with Hand Tracking"]],
  [414, ["Fixed-Thumb Head Turns", "Tiptoe Fixed-Thumb Head Turns"]],
  [415, ["Fixed-Thumb Head Nods", "Tiptoe Fixed-Thumb Head Nods"]],
  [416, ["Fixed-Thumb Head Tilts", "Tiptoe Fixed-Thumb Head Tilts"]],
  [531, ["Alternating Standing Shoulder CARs", "Single-Arm Shoulder CAR"]],
  [533, ["Alternating Standing Donkey Kicks", "Standing Bent-Knee Hip Extension"]],
  [557, ["Tiptoe Forward Punches", "Calf Raise with Double Forward Punch"]],
  [618, ["Single-Side High-Knee Hold with Side Reach", "Single-Side Knee Raise with Torso Twist"]],
  [649, ["Standing Bent-Knee Hip Abduction", "Standing Side-Leg Raise with Knee Extension"]],
  [677, ["Bent-Elbow Reverse-Fly Hold", "Standing Goalpost Arm Hold"]],
  [911, ["Single-Side Wall Side-Plank Knee Drive", "Wall-Supported High-Knee Raise"]],
  [939, ["Hinge-to-Knee Drive", "Split-Stance Knee-to-Hand Crunch"]],
  [542, ["Alternating Standing Bird Dogs", "Standing Bird-Dog Reach"]],
  [269, ["Standing Leg-Resistance Biceps Curl", "Wall-Supported Leg-Resistance Biceps Curl"]],
  [619, ["Squat with Forward Scoop", "Mini Squat with Forward Scoop"]],
  [228, ["Bent-Elbow External Rotation", "Single-Arm Bent-Elbow External Rotation"]],
  [274, ["Alternating Boxing Uppercuts", "Alternating Standing Uppercut Punches"]],
  [285, ["Karate Inside Block", "Karate Outward Forearm Block"]],
  [286, ["Karate Outside Block", "Alternating Karate Inward Forearm Blocks"]],
  [541, ["Alternating Karate Inside Blocks", "Alternating Karate Cross-Body Forearm Blocks"]],
  [545, ["Alternating Karate Outside Blocks", "Alternating Karate Outward Forearm Blocks"]],
  [112, ["Wide Squat with Overhead Reach", "Wide Squat with Arms Held Overhead"]],
  [171, ["Alternating Hamstring Sweep", "Standing Hamstring Sweep"]],
  [748, ["Alternating Wide-Stance Groin-Hamstring Shift", "Wide-Stance Groin and Hamstring Stretch"]],
  [116, ["Alternating Cossack Squat", "Alternating Lateral Squat"]],
  [733, ["Alternating Side Lunge with Chest Push", "Alternating Side Lunge with Forward Arm Reach"]],
  [16, ["Split-Stance Toe Raises", "Split-Stance Calf Raises"]],
  [17, ["Standing Toe-Touch Windmill", "Alternating Toe Touch with Overhead Reach"]],
  [19, ["Wide Plie Squat Pulses", "Wide Squat with Hands on Hips"]],
  [20, ["Standing Rear-Leg Pulses", "Standing Rear-Leg Raises"]],
  [104, ["Sumo Squat", "Sumo Squat with Pause"]],
  [32, ["Tandem Walk", "Tandem Walk Forward and Back"]],
  [97, ["Standing Side-Kick Reach", "Knee Raise and Side-Kick Reach"]],
  [111, ["Rainbow Squat Thruster", "Squat with Overhead Reach and Arm Sweep"]],
  [115, ["Pistol Squat", "Pistol Squat with Bottom Pause"]],
  [125, ["Alternating Reverse Lunge with Torso Twist", "Split Squat with Torso Twist"]],
  [126, ["Squat to Alternating Side Kick", "Squat with Side Kick"]],
  [128, ["Alternating Curtsy Squat with Arm Sweep", "Curtsy Squat with Arm Sweep"]],
  [129, ["Squat to Alternating Front Kick", "Squat with Front Kick"]],
  [131, ["Squat to Alternating Back Leg Lift", "Squat with Rear Leg Lift"]],
  [133, ["Alternating Lateral Lunge with Overhead Reach", "Lateral Lunge with Overhead Reach"]],
  [135, ["Overhead Squat Hold", "Wide Overhead Squat Hold"]],
  [137, ["Wall Squat", "Shallow Wall Squat"]],
  [141, ["Squat with Alternating Heel Lift", "Squat Hold with Alternating Heel Raises"]],
  [144, ["Alternating Standing Knee Lift", "Standing Knee Lift"]],
  [147, ["Squat to Alternating Star Reach", "Shallow Squat with Overhead Reach"]],
  [148, ["Straight-Leg Hip-Extension Lift", "Bent-Over Standing Bird Dog"]],
  [150, ["Wide-Squat Side-to-Side Shifts", "Side-to-Side Lunge"]],
  [151, ["Alternating Straight-Leg Front Raise", "Straight-Leg Front Raise"]],
  [154, ["Diagonal Leg Raise", "Standing Fire Hydrant"]],
  [159, ["Standing Hamstring Curl to Diagonal Extension", "Standing Fire-Hydrant Kick"]],
  [161, ["Alternating Standing Gate Opener", "Standing Gate Opener"]],
  [170, ["Alternating Cross-Body Knee Drive", "Standing Cross-Body Knee Drive"]],
  [173, ["Alternating Side Knee Lift", "Standing Side Knee Lift"]],
  [178, ["Crescent Kick", "Outside Crescent Kick"]],
  [179, ["Hip-Hinge Rear-Leg Raises", "Standing Rear Leg Raise"]],
  [182, ["Capoeira Armada de Frente", "Capoeira Meia-Lua de Frente"]],
  [184, ["Split-Squat Hold", "Split-Squat Pulse"]],
  [187, ["Standing Heel-and-Toe Raises", "Standing Heel Raise"]],
  [201, ["Wide-Stance Tiptoe Hold", "Sumo Squat to Calf Raise"]],
  [211, ["Bent-Elbow Wrist-Flexion Stretch", "Assisted Standing Wrist-Flexion Stretch"]],
  [213, ["Bent-Elbow Wrist-Extension Stretch", "Assisted Standing Wrist-Extension Stretch"]],
  [227, ["Rotating Relaxed Arm Swings", "Arm Swings with Trunk Rotation"]],
  [236, ["Bilateral Wrist Figure Eights", "Bilateral Wrist Circles"]],
  [239, ["Standing Reverse Prayer Stretch", "Back-of-Hands Wrist Stretch"]],
  [245, ["Straight-Punch to Shovel-Hook Combo", "Jab-Jab-Cross-Shovel-Hook Combo"]],
  [261, ["Standing Bent-Elbow Reverse Fly", "Standing Bent-Elbow Chest Squeeze"]],
  [266, ["T-Arm Shoulder Hold", "Alternating Overhead Arm Raises"]],
  [268, ["Goalpost-to-T Rotations", "Goalpost-to-T Arm Extensions"]],
  [271, ["Standing Lumbar Extension", "Standing Lumbar Extension Hold"]],
  [273, ["Bent-Knee Calf-Raise Hold", "Wall-Supported Bent-Knee Calf Raise Hold"]],
  [280, ["Alternating Boxing Hook Punches", "Rear-Hand Boxing Hook"]],
  [288, ["Forward Knee-and-Ankle Circles", "Standing Knee-and-Ankle Circles"]],
  [289, ["Fingertip Spider Presses", "Fist Opening and Closing with Arms Held Forward"]],
  [294, ["Outward Knife-Hand Strikes", "Rear-Hand Outward Knife-Hand Strike"]],
  [295, ["Ankle Squat March", "Alternating Bent-Knee Heel Raises"]],
  [301, ["Overhead Arm Pumps", "Standing Side Arm Raises"]],
  [309, ["Self-Resisted Neck Rotation Isometric", "Self-Resisted Neck Rotation Hold with Head Turned"]],
  [321, ["Side-Tap Alternating Arm Raises", "Alternating Side Taps with Overhead Arm Raises"]],
  [379, ["Alternating Qigong Drawing the Bow", "Qigong Drawing the Bow"]],
  [389, ["First-Position Heel Raise with Elbow Pull-Down", "Toes-Out Heel Raise with Elbow Pull-Down"]],
  [418, ["Alternating-Thumb Head Turns", "Look Between Fingers with Head Turns"]],
  [419, ["Vertical Thumb Tracking with Head Nods", "Track Thumb While Nodding the Opposite Way"]],
  [427, ["Split Jacks", "Jumping Lunges with Alternating Overhead Reach"]],
  [437, ["Alternating High-Knee Under-Thigh Claps", "Hopping High-Knee Under-Thigh Claps"]],
  [467, ["Look Up and Down", "Standing Chin-to-Chest Nods"]],
  [470, ["Windmill Jacks", "Alternating Windmill Toe Touch"]],
  [473, ["Bouncing Uppercuts", "Boxing Lead-Hand Parry"]],
  [474, ["Head Glide Forward and Back", "Chin Tucks with Hands Pulling Forward at Base of Neck"]],
  [484, ["Goddess Squat with Lion's Breath", "Wide Squat with Tongue-Out Exhale"]],
  [485, ["Cupped-Palm Armpit Tapping", "Alternating Back Taps with Rowing Arms"]],
  [486, ["Bent-Over Back-of-Knee Tapping", "Alternating Knee Lift with Two-Arm Pull-Down"]],
  [490, ["Track One Thumb Side to Side", "Track Finger Side to Side, Head Still"]],
  [491, ["Keep Eyes on Thumb While Nodding", "Keep Eyes on Finger While Nodding"]],
  [501, ["Keep Eyes on Thumb While Turning Head", "Keep Eyes on Finger While Turning Head"]],
  [505, ["Jaw Side-to-Side Glides", "Jaw Side Glide and Relax"]],
  [519, ["Standing Yes Pullbacks", "Push Forearm Outward Against Other Hand"]],
  [520, ["Mirror Facial-Expression Practice", "Raise Arms, Exhale Choo as Hips Sink"]],
  [521, ["Smile at Yourself in the Mirror", "Stretch and Scrunch Face"]],
  [538, ["Alternating Reverse Lunge to Front Kicks", "Reverse Lunge with Front Kick"]],
  [547, ["Alternating Standing Rotation Claps", "Standing Rotational Reach and Clap"]],
  [554, ["Half Squat Arm Swing to Heel Raise", "Arm Raises in Shallow Squat"]],
  [560, ["Tiptoe Forward-and-Back Torso-and-Arm Sweep", "Try to Turn Palm Up Against Other Hand"]],
  [569, ["Tiptoe Hip Hinge", "Unsupported Single-Leg Straight-Knee Calf Raise"]],
  [570, ["Tiptoe Torso Twists", "Calf Raise with Arm Reach and Fist Close"]],
  [614, ["Alternating Reinforced Forearm Blocks", "Push Forearm Down Against Other Hand"]],
  [633, ["Wall Calf Stretch", "Straight-Knee Toes-on-Wall Stretch"]],
  [636, ["Curtsy-Lunge Hold", "Curtsy Lunge to Alternating Side Crunch"]],
  [647, ["Alternating Knee Lift with Overhead Reach", "Alternating Heel Curl with Overhead Reach"]],
  [654, ["Single-Side Leg Lift to Overhead Knee Drive", "Alternating Side Leg Lifts and Heel Curls"]],
  [678, ["Overhead Arm Figure Eight", "Overhead Arm Sways"]],
  [681, ["Muay Thai Downward Elbow Strike", "Downward Elbow Strike"]],
  [751, ["Neck Side Stretch", "Standing Neck Half Rolls"]],
  [816, ["Side-Step Overhead Reach", "Low-Impact Side-Step Jacks"]],
  [836, ["Squat with Back Squeeze", "Alternating Side-Step Squat with Back Squeeze"]],
  [845, ["Overhead Side Stretch", "Overhead Side-Bend Hold"]],
  [913, ["Wall-Supported Vertical Dead Bug", "Shallow Wall Sit with Overhead Arm Raises"]],
  [914, ["Alternating Diagonal Knee Pull-Down", "Single-Side Diagonal Knee Pull-Down"]],
  [919, ["March in Place with Fixed-Gaze Head Turns", "Marching in Place with Head Turns"]],
  [949, ["Standing Reverse Wood Chop", "Standing Low-to-High Wood Chop"]],
  [962, ["Alternating Standing Knee-to-Elbow Crunch", "Alternating Standing Knee Taps"]],
  [993, ["Mirror Standing Vacuum Repetitions", "Standing Stomach Vacuum, Then Release"]],
]);

// Exact historical actions that must never inherit a later replacement identity.
export const DISCARDED_EXERCISE_IDENTITY_NAMES = new Map([
  [202, new Set(["Finger Fan and Close — Four-Count Tempo", "Hook Fist"])],
  [204, new Set(["Finger Fan and Close — Half Range", "Full Fist"])],
  [205, new Set(["Finger Fan and Close — Full Range", "Tabletop Fist"])],
  [218, new Set(["Cumbia Two-Step", "Sequential Finger Waves"])],
  [234, new Set(["Merengue Six-Count Step", "Straight Fingers to Knuckle Bend"])],
  [237, new Set(["Salsa Front-and-Back Basic", "Sequential Finger Curl Waves"])],
  [239, new Set(["Reggaeton Single-Single-Double Step", "Tabletop Tendon Glide"])],
  [240, new Set(["Hook Fingers to Full Fist"])],
  [241, new Set(["Basic Mambo Step", "Hook-Fist Tendon Glide", "Ninja Water-Dragon 44 Hand-Seal Sequence", "Open Hand to Hook Fist"])],
  [242, new Set(["Full-Fist Tendon Glide", "Ninja Shadow-Clone Hand-Seal Sequence", "Open Hand to Full Fist"])],
  [256, new Set(["Self-Resisted Overhead Pull", "Self-Resisted Overhead Pull Hold"])],
  [257, new Set(["Self-Resisted Chest-Level Pull", "Self-Resisted Chest-Level Pull Hold"])],
  [258, new Set(["Self-Resisted Low Pull", "Self-Resisted Low Pull Hold"])],
  [262, new Set(["Standing Hands-to-Thigh Abdominal Press", "Standing Scapular Depression"])],
  [270, new Set(["Bodyweight Svend Press", "Palm-Squeeze Forward Press"])],
  [283, new Set(["Cha-Cha Basic Step", "Open Hand to Straight Fist", "Qigong Fist Rotation", "Straight-Fist Tendon Glide"])],
  [291, new Set(["Bachata Side-to-Side Basic", "Black Dragon Enters the Cave", "Open Hand to Claw Fist", "Open-to-Claw Tendon Glide"])],
  [294, new Set(["Five-Position Tendon Glide"])],
  [394, new Set(["Inhale Arms Open, Exhale Arms Close and Round", "Standing Arms Open and Close", "Standing Open-and-Close Breathing"])],
  [395, new Set(["Overhead Hold with Deep Ribcage Breaths", "Standing Overhead Arm Sweep", "Standing Overhead Rib-Expansion Breathing"])],
  [425, new Set(["Chin-Tuck Hold", "Chin-Tuck Isometric"])],
  [497, new Set(["Forehead Finger Sweep", "Odissi Sundari Griva", "Track Finger in Circles"])],
  [556, new Set(["Pony Step", "Standing Fist Clench and Release"])],
  [563, new Set(["Single-Leg Calf Raise with Head Turns"])],
  [564, new Set(["Parallel Calf Raises with Hands on Hips"])],
  [567, new Set(["Breathing Calf Raises with Arm Folds"])],
  [568, new Set(["Chest-Expansion Breathing Calf Raises"])],
  [574, new Set(["Tiptoe Overhead Side Bends"])],
]);

export const ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES = new Map([
  [31, new Set(["Knee Raise with Overhead Reach", "Single-Side Knee Raise with Two-Arm Pull-Down"])],
  [524, new Set(["Front Double-Biceps Pose Hold", "Mirror Front Double-Biceps Posing"])],
  [525, new Set(["Front Lat-Spread Pose Hold", "Mirror Front Lat-Spread Posing"])],
  [526, new Set(["Side-Chest Pose Hold", "Mirror Side-Chest Posing"])],
  [527, new Set(["Side-Triceps Pose Hold", "Mirror Side-Triceps Posing"])],
  [528, new Set(["Abdominals-and-Thighs Pose Hold", "Mirror Abdominals-and-Thighs Posing"])],
  [565, new Set(["Mini Squat with Forward Reach"])],
  [21, new Set(["Alternating Standing-Scale Balance", "Standing-Scale Balance Hold"])],
  [145, new Set(["Alternating Standing Knee Extension", "Standing Knee-Extension Hold"])],
  [231, new Set(["Alternating Karate Reverse Punch", "Step-Through Karate Reverse Punch"])],
  [394, new Set(["Standing Open-and-Close Breathing", "Inhale Open, Exhale Cross-Body Knee", "Inhale Arms Open, Exhale Arms Close and Round"])],
  [395, new Set(["Standing Overhead Rib-Expansion Breathing", "Single-Side Inhale Reach Up, Exhale Knee Lift", "Overhead Hold with Deep Ribcage Breaths", "Single-Side Knee Lift with Overhead Reach"])],
  [500, new Set(["Controlled Jaw Open and Close"])],
  [398, new Set(["Standing Arm-Expansion Breathing", "Inhale Arms Open, Exhale Self-Hug and Fold"])],
  [399, new Set(["Shibashi Opening-the-Chest Breathing"])],
  [400, new Set(["Shibashi Separating-the-Clouds Breathing"])],
  [401, new Set(["Shibashi Alternating Swinging-Arms Breathing"])],
  [617, new Set(["Alternating Standing Side-Leg Circles"])],
  [95, new Set(["Single-Leg Pelvic Control"])],
  [270, new Set(["Goalpost Chest-Opener Hold", "Palm-Squeeze Forward Press"])],
  [556, new Set(["Alternating Backfists", "Standing Fist Clench and Release"])],
  [615, new Set(["Alternating Hamstring Curls with Prayer Hands", "Heel Raise with Prayer-to-Open Arms"])],
  [119, new Set(["Tiptoe Walk"])],
  [136, new Set(["Wide Turned-Out Squat Hold"])],
  [193, new Set(["Wide-Stance Floor-to-Overhead Reach"])],
  [197, new Set(["Parallel Squat-to-Calf Raise"])],
  [199, new Set(["Wide-Stance Side-to-Side Squat"])],
  [214, new Set(["Inward Wrist Circles"])],
  [219, new Set(["Alternating High-Knee Cross-Body Pull"])],
  [223, new Set(["Inward Controlled Wrist Circles"])],
  [257, new Set(["Finger Spread to Interlace Stretch", "Self-Resisted Chest-Level Pull Hold"])],
  [258, new Set(["Karate Downward Block", "Self-Resisted Low Pull Hold"])],
  [283, new Set(["Alternating Palm Strikes", "Open Hand to Straight Fist"])],
  [290, new Set(["Low Palm Scoop to Side Opening"])],
  [293, new Set(["Opposite-Hand Finger-Web Stretches"])],
  [390, new Set(["Step-Touch with Overhead Arm Arcs"])],
  [391, new Set(["Alternating High Knee with Arm Opening"])],
  [396, new Set(["Single-Leg Knee-Lift Balance Hold", "Unsupported Single-Leg Balance Hold"])],
  [397, new Set(["Alternating Side Tap with Diagonal Arm Sweep"])],
  [403, new Set(["Alternating Weight Shift with Palm Push"])],
  [408, new Set(["Split-Squat Torso Rotation with Hand Tracking"])],
  [417, new Set(["Narrow-Stance Overhead-to-Floor Reach"])],
  [508, new Set(["Side-Step with Two-Arm Overhead Reach"])],
  [515, new Set(["Mirror One-Eyebrow Isolation Practice"])],
  [522, new Set(["Mirror Tutting Box Sequence"])],
  [523, new Set(["Mirror Arm-Wave Isolation Practice"])],
  [561, new Set(["Tiptoe Running Steps with Head Spot"])],
  [562, new Set(["Ballet Calf Raises with Arm Sweeps"])],
  [577, new Set(["Single-Side Standing Side-Leg Raise with Side Reach", "Standing Side-Leg Raise with Side Reach"])],
  [618, new Set(["Single-Side High-Knee Raise with Side Reach"])],
  [790, new Set(["Mirror Most-Muscular Posing"])],
  [939, new Set(["Split-Stance Knee-to-Hand Press"])],
  [958, new Set(["Standing Overhead Side Bend"])],
]);

const CANONICAL_GROUPS = Object.freeze([
  null,
  "MedialAndDeepKneeExtensors",
  "PosteriorThighAndKneeFlexors",
  "MajorHipAdductors",
  "LateralKneeExtensors",
  "GlutealExtensors",
  "SpinalExtensors",
  "CalfDeepPosteriorLegAndPlantarFoot",
  "Soleus",
  "ScapularGirdle",
  "ShoulderAdductorsAndExtensors",
  "AbdominalWall",
  "HipAbductors",
  "Chest",
  "ElbowExtensors",
  "HipFlexors",
  "AnteriorLateralLowerLegAndDorsalFoot",
  "DeepHipRotators",
  "ShoulderAbductors",
  "ForearmFlexorsAndPronators",
  "DeepAndIntersegmentalBack",
  "ElbowFlexors",
  "BreathingMuscles",
  "ForearmExtensorsAndSupinators",
  "RotatorCuff",
  "AccessoryHipAdductors",
  "PosteriorNeckAndSuboccipitalMuscles",
  "CranialMuscles",
  "AnteriorLateralNeckAndHyoidMuscles",
  "IntrinsicHand",
  "PelvicFloorAndPerineum",
]);

const CANONICAL_DISPLAY_NAMES = Object.freeze([
  null,
  "Medial and deep knee extensors",
  "Posterior thigh and knee flexors",
  "Major hip adductors",
  "Lateral knee extensors",
  "Gluteal extensors",
  "Spinal extensors",
  "Calf, deep posterior leg and plantar foot",
  "Soleus",
  "Scapular girdle",
  "Shoulder adductors and extensors",
  "Abdominal wall",
  "Hip abductors",
  "Chest",
  "Elbow extensors",
  "Hip flexors",
  "Anterior/lateral lower leg and dorsal foot",
  "Deep hip rotators",
  "Shoulder abductors",
  "Forearm flexors and pronators",
  "Deep and intersegmental back",
  "Elbow flexors",
  "Breathing muscles",
  "Forearm extensors and supinators",
  "Rotator cuff",
  "Accessory hip adductors",
  "Posterior neck and suboccipital muscles",
  "Cranial muscles",
  "Anterior/lateral neck and hyoid muscles",
  "Intrinsic hand",
  "Pelvic floor and perineum",
]);

const CANONICAL_KEYS = Object.freeze([
  null,
  "medial-deep-knee-extensors",
  "posterior-thigh-knee-flexors",
  "major-hip-adductors",
  "lateral-knee-extensors",
  "gluteal-extensors",
  "spinal-extensors",
  "calf-deep-posterior-leg-plantar-foot",
  "soleus",
  "scapular-girdle",
  "shoulder-adductors-extensors",
  "abdominal-wall",
  "hip-abductors",
  "chest",
  "elbow-extensors",
  "hip-flexors",
  "anterior-lateral-lower-leg-dorsal-foot",
  "deep-hip-rotators",
  "shoulder-abductors",
  "forearm-flexors-pronators",
  "deep-intersegmental-back",
  "elbow-flexors",
  "breathing-muscles",
  "forearm-extensors-supinators",
  "rotator-cuff",
  "accessory-hip-adductors",
  "posterior-neck-suboccipital",
  "cranial-muscles",
  "anterior-lateral-neck-hyoid",
  "intrinsic-hand",
  "pelvic-floor-perineum",
]);

function bucket(key, displayName, ...canonicalIds) {
  return {
    key,
    displayName,
    canonicalGroups: canonicalIds.map((id) => CANONICAL_GROUPS[id]),
  };
}

function canonicalBucket(canonicalId) {
  return bucket(
    CANONICAL_KEYS[canonicalId],
    CANONICAL_DISPLAY_NAMES[canonicalId],
    canonicalId,
  );
}

function resolution(minutes, declaredLargestToSmallest) {
  const groups = [...declaredLargestToSmallest]
    .reverse()
    .map((group, index) =>
      Object.freeze({
        id: `r${minutes}.${group.key}`,
        displayName: group.displayName,
        order: index + 1,
        canonicalGroups: Object.freeze([...group.canonicalGroups]),
      }),
    );
  return Object.freeze({ minutes, groups: Object.freeze(groups) });
}

export const RESOLUTIONS = new Map([
  [
    3,
    resolution(3, [
      bucket("lower-limbs", "Lower limbs", 1, 2, 3, 4, 5, 7, 8, 12, 15, 16, 17, 25),
      bucket("torso-pelvic-complex", "Torso and pelvic complex", 6, 11, 13, 20, 22, 30),
      bucket("head-neck-upper-limbs", "Head, neck and upper limbs", 9, 10, 14, 18, 19, 21, 23, 24, 26, 27, 28, 29),
    ]),
  ],
  [
    5,
    resolution(5, [
      bucket("hips-thighs", "Hips and thighs", 1, 2, 3, 4, 5, 12, 15, 17, 25),
      bucket("torso", "Torso", 6, 11, 13, 20, 22, 30),
      bucket("lower-legs-feet", "Lower legs and feet", 7, 8, 16),
      bucket("upper-limbs", "Upper limbs", 10, 14, 18, 19, 21, 23, 24, 29),
      bucket("head-neck-shoulder-girdle", "Head, neck and shoulder girdle", 9, 26, 27, 28),
    ]),
  ],
  [
    7,
    resolution(7, [
      bucket("torso", "Torso", 6, 11, 13, 20, 22, 30),
      bucket("knee-extensors", "Knee extensors", 1, 4),
      bucket("head-neck-upper-limbs", "Head, neck and upper limbs", 9, 10, 14, 18, 19, 21, 23, 24, 26, 27, 28, 29),
      bucket("lower-legs-feet", "Lower legs and feet", 7, 8, 16),
      bucket("hip-flexors-adductors", "Hip flexors and adductors", 3, 15, 25),
      bucket("gluteals-deep-hip", "Gluteals and deep hip stabilizers", 5, 12, 17),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
    ]),
  ],
  [
    10,
    resolution(10, [
      bucket("medial-deep-knee-extensors", "Medial and deep knee extensors", 1),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
      bucket("hip-flexors-adductors", "Hip flexors and adductors", 3, 15, 25),
      bucket("gluteals-deep-hip", "Gluteals and deep hip stabilizers", 5, 12, 17),
      bucket("back-abdominal-pelvic-floor", "Back, abdominal wall and pelvic floor", 6, 11, 20, 30),
      bucket("lateral-knee-extensors", "Lateral knee extensors", 4),
      bucket("head-neck-scapular-chest-breathing", "Head, neck, scapular girdle, chest and breathing", 9, 13, 22, 26, 27, 28),
      bucket("posterior-lower-leg-plantar-foot", "Posterior lower leg and plantar foot", 7, 8),
      bucket("upper-limbs", "Upper limbs", 10, 14, 18, 19, 21, 23, 24, 29),
      bucket("anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot", 16),
    ]),
  ],
  [
    15,
    resolution(15, [
      bucket("medial-deep-knee-extensors", "Medial and deep knee extensors", 1),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
      bucket("hip-adductors", "Hip adductors", 3, 25),
      bucket("lateral-knee-extensors", "Lateral knee extensors", 4),
      bucket("gluteal-extensors", "Gluteal extensors", 5),
      bucket("posterior-lower-leg-plantar-foot", "Posterior lower leg and plantar foot", 7, 8),
      bucket("back-spinal-stabilization", "Back and spinal stabilization", 6, 20),
      bucket("scapular-chest-breathing", "Scapular girdle, chest and breathing", 9, 13, 22),
      bucket("lateral-deep-hip-stabilizers", "Lateral and deep hip stabilizers", 12, 17),
      bucket("arm-forearm-hand", "Arm, forearm and hand", 14, 19, 21, 23, 29),
      bucket("abdominal-pelvic-floor", "Abdominal wall and pelvic floor", 11, 30),
      bucket("shoulder", "Shoulder", 10, 18, 24),
      bucket("hip-flexors", "Hip flexors", 15),
      bucket("anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot", 16),
      bucket("head-neck", "Head and neck", 26, 27, 28),
    ]),
  ],
  [
    20,
    resolution(20, [
      bucket("medial-deep-knee-extensors", "Medial and deep knee extensors", 1),
      bucket("posterior-thigh-knee-flexors", "Posterior thigh and knee flexors", 2),
      bucket("major-hip-adductors", "Major hip adductors", 3),
      bucket("lateral-knee-extensors", "Lateral knee extensors", 4),
      bucket("gluteal-extensors", "Gluteal extensors", 5),
      bucket("soleus", "Soleus", 8),
      bucket("back-spinal-stabilization", "Back and spinal stabilization", 6, 20),
      bucket("calf-flexors-plantar-foot", "Calf flexors and plantar foot", 7),
      bucket("scapular-girdle", "Scapular girdle", 9),
      bucket("chest-breathing", "Chest and breathing", 13, 22),
      bucket("shoulder-adduction-extension", "Shoulder adduction and extension", 10),
      bucket("abdominal-pelvic-floor", "Abdominal wall and pelvic floor", 11, 30),
      bucket("lateral-deep-hip-stabilizers", "Lateral and deep hip stabilizers", 12, 17),
      bucket("upper-arm", "Upper arm", 14, 21),
      bucket("hip-flexors", "Hip flexors", 15),
      bucket("anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot", 16),
      bucket("shoulder-abduction-rotation", "Shoulder abduction and rotation", 18, 24),
      bucket("forearm-hand", "Forearm and hand", 19, 23, 29),
      bucket("accessory-hip-adductors", "Accessory hip adductors", 25),
      bucket("head-neck", "Head and neck", 26, 27, 28),
    ]),
  ],
  [
    30,
    resolution(
      30,
      Array.from({ length: 30 }, (_, index) => canonicalBucket(index + 1)),
    ),
  ],
]);

const MUSCLE_BALANCE_GROUP_ID_BY_CANONICAL = new Map(
  [...RESOLUTIONS].map(([minutes, workoutResolution]) => [
    minutes,
    new Map(workoutResolution.groups.flatMap((group) =>
      group.canonicalGroups.map((canonicalGroup) => [canonicalGroup, group.id]))),
  ]),
);

const ALL_GROUPS = new Map(
  [...RESOLUTIONS.values()].flatMap((item) => item.groups.map((group) => [group.id, group])),
);

export function getResolution(minutes) {
  const item = RESOLUTIONS.get(minutes);
  if (!item) {
    throw new RangeError("Workout duration must be 3, 5, 7, 10, 15, 20, or 30 minutes.");
  }
  return item;
}

export function getSelectionKey(group) {
  return group.selectionGroupId ?? group.id;
}

export function getWorkoutDisplayProgress(activeGroups, currentGroup) {
  if (!Array.isArray(activeGroups) || !currentGroup) {
    throw new TypeError("Workout progress requires active and current groups.");
  }
  const selectionKeys = [];
  const seen = new Set();
  for (const group of [...activeGroups].sort((left, right) => left.order - right.order)) {
    const key = getSelectionKey(group);
    if (!seen.has(key)) {
      seen.add(key);
      selectionKeys.push(key);
    }
  }
  const position = selectionKeys.indexOf(getSelectionKey(currentGroup));
  if (position < 0 || selectionKeys.length === 0) {
    throw new Error("The current workout group is not in the active workout.");
  }
  return { position: position + 1, total: selectionKeys.length };
}

export function getWorkoutBlockAccent(group) {
  switch (group?.sequenceSideCue ?? "None") {
    case "ScreenRight":
    case "ShownLeadStance":
      return "blue";
    case "ScreenLeft":
    case "OppositeLeadStance":
      return "red";
    default:
      break;
  }

  switch (group?.sequenceDirectionCue ?? "None") {
    case "Forward":
    case "Clockwise":
    case "Inward":
      return "blue";
    case "Backward":
    case "Counterclockwise":
    case "Outward":
      return "red";
    default:
      return "neutral";
  }
}

function usesThreeDistinctExercisePalette(groups) {
  if (groups.length === 0 || !groups.every((group) =>
    group.sequenceBlockCount === 3 &&
    Number.isInteger(group.exerciseOverrideId) &&
    group.exerciseOverrideId > 0 &&
    getWorkoutBlockAccent(group) === "neutral")) {
    return false;
  }

  const exerciseIdsBySet = new Map();
  for (const group of groups) {
    const setNumber = group.setNumber ?? 1;
    const exerciseIds = exerciseIdsBySet.get(setNumber) ?? [];
    exerciseIds.push(group.exerciseOverrideId);
    exerciseIdsBySet.set(setNumber, exerciseIds);
  }
  return [...exerciseIdsBySet.values()].every((exerciseIds) =>
    exerciseIds.length === 3 && new Set(exerciseIds).size === 3);
}

function getThreeDistinctExerciseAccent(group) {
  switch (group.sequenceBlockIndex) {
    case 0:
      return "blue";
    case 1:
      return "neutral";
    case 2:
      return "red";
    default:
      throw new RangeError(
        "A three-exercise palette requires block indexes 0 through 2.",
      );
  }
}

export function getWorkoutExecutionTimeline(
  activeGroups,
  currentGroup,
  selectUpcomingBlock = false,
) {
  if (!Array.isArray(activeGroups) || !currentGroup) {
    throw new TypeError("An execution timeline requires active and current groups.");
  }
  const selectionKey = getSelectionKey(currentGroup);
  const timelineGroups = activeGroups
    .filter((group) => getSelectionKey(group) === selectionKey)
    .sort((left, right) => left.order - right.order);
  let currentBlockIndex = timelineGroups.findIndex((group) =>
    group.id === currentGroup.id);
  if (currentBlockIndex < 0 || timelineGroups.length === 0) {
    throw new Error("The current workout group has no active execution timeline.");
  }
  if (selectUpcomingBlock && currentBlockIndex + 1 < timelineGroups.length) {
    currentBlockIndex += 1;
  }
  const usesThreeExercisePalette =
    usesThreeDistinctExercisePalette(timelineGroups);
  return {
    blocks: timelineGroups.map((group) =>
      usesThreeExercisePalette
        ? getThreeDistinctExerciseAccent(group)
        : getWorkoutBlockAccent(group)),
    setStartBlockIndices: timelineGroups
      .map((group, index) => ({ setNumber: group.setNumber ?? 1, index }))
      .filter((entry, index, entries) => index === 0 ||
        entry.setNumber !== entries[index - 1].setNumber)
      .map((entry) => entry.index),
    currentBlockIndex,
  };
}

export function hasReviewedMuscularDemand(exercise) {
  return Number.isInteger(exercise?.muscularDemand) &&
    exercise.muscularDemand >= MINIMUM_MUSCULAR_DEMAND &&
    exercise.muscularDemand <= MAXIMUM_MUSCULAR_DEMAND;
}

export function getLastHardWorkUnixMilliseconds(
  lastHardWorkByPrimaryMuscle,
  primaryMuscle,
) {
  return getLastWorkUnixMilliseconds(lastHardWorkByPrimaryMuscle, primaryMuscle);
}

export function getLastMeaningfulWorkUnixMilliseconds(
  lastMeaningfulWorkByPrimaryMuscle,
  primaryMuscle,
) {
  return getLastWorkUnixMilliseconds(lastMeaningfulWorkByPrimaryMuscle, primaryMuscle);
}

function getLastWorkUnixMilliseconds(lastWorkByPrimaryMuscle, primaryMuscle) {
  const value = lastWorkByPrimaryMuscle?.[primaryMuscle];
  return Number.isSafeInteger(value) && value > 0 ? value : 0;
}

export function isPrimaryMuscleRecovering(
  lastHardWorkByPrimaryMuscle,
  primaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  return isPrimaryMuscleWithinRecoveryWindow(
    lastHardWorkByPrimaryMuscle,
    primaryMuscle,
    nowUnixMilliseconds,
    HARD_RECOVERY_WINDOW_MS,
  );
}

export function isPrimaryMuscleInModerateRecovery(
  lastMeaningfulWorkByPrimaryMuscle,
  primaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  return isPrimaryMuscleWithinRecoveryWindow(
    lastMeaningfulWorkByPrimaryMuscle,
    primaryMuscle,
    nowUnixMilliseconds,
    MODERATE_RECOVERY_WINDOW_MS,
  );
}

export function isModerateExerciseRecovering(
  exercise,
  lastMeaningfulWorkByPrimaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  return exercise?.muscularDemand === MODERATE_MUSCULAR_DEMAND &&
    isPrimaryMuscleInModerateRecovery(
      lastMeaningfulWorkByPrimaryMuscle,
      exercise.primaryCanonicalGroup,
      nowUnixMilliseconds,
    );
}

function isPrimaryMuscleWithinRecoveryWindow(
  lastWorkByPrimaryMuscle,
  primaryMuscle,
  nowUnixMilliseconds,
  recoveryWindowMilliseconds,
) {
  const lastWork = getLastWorkUnixMilliseconds(lastWorkByPrimaryMuscle, primaryMuscle);
  return lastWork > 0 &&
    nowUnixMilliseconds - lastWork < recoveryWindowMilliseconds;
}

export function getHardRotationStatus(
  exercise,
  group,
  lastHardWorkByPrimaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  if (exercise?.muscularDemand !== HARD_MUSCULAR_DEMAND) {
    return HARD_ROTATION_STATUS.Neutral;
  }
  if (isPrimaryMuscleRecovering(
    lastHardWorkByPrimaryMuscle,
    exercise.primaryCanonicalGroup,
    nowUnixMilliseconds,
  )) {
    return HARD_ROTATION_STATUS.RecoveringHard;
  }
  return group?.canonicalGroups?.includes(exercise.primaryCanonicalGroup)
    ? HARD_ROTATION_STATUS.FreshHard
    : HARD_ROTATION_STATUS.Neutral;
}

export function createWorkoutSchedule(
  minutes,
  sequenceRootsBySelectionGroupId = null,
  setCountsBySelectionGroupId = null,
  exercisesById = null,
  isRelaxedSingletonValid = null,
  frozenSelectionGroupIds = [],
  selectionGroups = null,
  simpleRoundSelectionGroupIds = null,
) {
  if (!SUPPORTED_MINUTES.includes(minutes)) {
    throw new RangeError("Unsupported workout duration.");
  }

  const sequenceRoots = sequenceRootsBySelectionGroupId instanceof Map
    ? sequenceRootsBySelectionGroupId
    : new Map();
  const exerciseMap = exercisesById instanceof Map
    ? exercisesById
    : new Map([...sequenceRoots.values()]
        .filter(Boolean)
        .map((exercise) => [exercise.id, exercise]));
  const placements = orderSelectedSequencePlacementsForSchedule(
    getSelectedSequencePlacements(
      minutes,
      sequenceRoots,
      exerciseMap,
      isRelaxedSingletonValid,
      selectionGroups,
    ),
    exerciseMap,
    frozenSelectionGroupIds,
  );
  const setCounts = setCountsBySelectionGroupId instanceof Map
    ? setCountsBySelectionGroupId
    : createDefaultWorkoutSetCounts(
      minutes,
      placements,
    );
  const rounds = [];
  for (const placement of placements) {
    const { root, anchor, coveredGroups } = placement;
    const setCount = Math.max(1, setCounts.get(anchor.id) ?? 1);
    const blocks = root.sequenceBlocks;
    for (let setNumber = 1; setNumber <= setCount; setNumber += 1) {
      for (let blockIndex = 0; blockIndex < blocks.length; blockIndex += 1) {
        const block = blocks[blockIndex];
        const blockExercise = exerciseMap.get(block.exerciseId);
        const blockGroup = coveredGroups.length === 1
          ? anchor
          : coveredGroups.find((group) => group.canonicalGroups.includes(
              blockExercise?.primaryCanonicalGroup,
            ));
        if (!blockExercise || !blockGroup) {
          throw new Error(`${anchor.displayName} has an invalid sequence block.`);
        }
        if ((simpleRoundSelectionGroupIds === null ? minutes <= 30 :
            simpleRoundSelectionGroupIds.includes(anchor.id)) &&
            blocks.length === 1 && setCount === 1) {
          rounds.push(Object.freeze({
            ...blockGroup,
            order: rounds.length + 1,
          }));
          continue;
        }
        rounds.push(Object.freeze({
          ...blockGroup,
          id: `${anchor.id}.set${setNumber}.block${blockIndex + 1}`,
          order: rounds.length + 1,
          selectionGroupId: anchor.id,
          exerciseOverrideId: block.exerciseId,
          sequenceBlockIndex: blockIndex,
          sequenceBlockCount: blocks.length,
          setNumber,
          setCount,
          sequenceSideCue: block.sideCue ?? "None",
          sequenceDirectionCue: block.directionCue ?? "None",
          mirrorSequenceMedia: block.mirrorMedia === true,
          sequenceMediaSegment: block.mediaSegment ?? "Full",
        }));
      }
    }
  }
  if (rounds.length !== minutes) {
    throw new Error(
      `The ${minutes}-minute workout scheduled ${rounds.length} exercise blocks.`,
    );
  }
  return Object.freeze(rounds);
}

function createDefaultWorkoutSetCounts(
  minutes,
  placements,
) {
  const setCounts = new Map(placements.map((placement) =>
    [placement.anchor.id, 1]));
  const blockCosts = new Map(placements.map((placement) =>
    [placement.anchor.id, placement.root.sequenceBlocks.length]));
  let remainingMinutes = minutes - [...blockCosts.values()]
    .reduce((total, cost) => total + cost, 0);
  if (remainingMinutes < 0) {
    throw new Error("The selected sequences do not fit this workout duration.");
  }
  const rankedPlacements = [...placements].reverse();
  const canFill = (remaining) => {
    const reachable = new Array(remaining + 1).fill(false);
    reachable[0] = true;
    for (let value = 1; value <= remaining; value += 1) {
      reachable[value] = [...blockCosts.values()].some((cost) =>
        cost <= value && reachable[value - cost]);
    }
    return reachable[remaining];
  };
  while (remainingMinutes > 0) {
    const placement = rankedPlacements.find((candidate) => {
      const cost = blockCosts.get(candidate.anchor.id);
      return cost <= remainingMinutes && canFill(remainingMinutes - cost);
    });
    if (!placement) {
      throw new Error("The selected sequence lengths cannot fill this workout duration.");
    }
    setCounts.set(
      placement.anchor.id,
      setCounts.get(placement.anchor.id) + 1,
    );
    remainingMinutes -= blockCosts.get(placement.anchor.id);
  }
  return setCounts;
}

export function normalizeMinutes(minutes) {
  if (!Number.isFinite(Number(minutes))) {
    return 10;
  }
  return [...SUPPORTED_MINUTES].sort((left, right) => {
    const distance = Math.abs(left - Number(minutes)) - Math.abs(right - Number(minutes));
    return distance || right - left;
  })[0];
}

export function getCanonicalCoverage(exercise, group) {
  const trained = new Set([
    exercise.primaryCanonicalGroup,
    ...(exercise.secondaryCanonicalGroups ?? []),
  ]);
  return group.canonicalGroups.filter((canonicalGroup) => trained.has(canonicalGroup)).length;
}

export function getRequiredCanonicalCoverage(group) {
  return Math.ceil(group.canonicalGroups.length / 2);
}

export function isSelectable(exercise, group) {
  return getCanonicalCoverage(exercise, group) >= getRequiredCanonicalCoverage(group) ||
    isRegionalCompound(exercise, group);
}

const REGIONAL_SHOULDER_TARGETS = new Set([
  "ShoulderAbductors", "ShoulderAdductorsAndExtensors", "RotatorCuff", "ScapularGirdle", "Chest",
]);
const REGIONAL_ELBOW_TARGETS = new Set(["ElbowFlexors", "ElbowExtensors"]);
const ANTERIOR_TRUNK_TARGETS = new Set(["AbdominalWall", "Chest"]);
const POSTERIOR_TRUNK_TARGETS = new Set(["SpinalExtensors", "DeepAndIntersegmentalBack"]);
const COMPOUND_UPPER_BUCKET_MEMBERS = [
  "ShoulderAbductors", "ShoulderAdductorsAndExtensors", "RotatorCuff", "ElbowFlexors", "ElbowExtensors",
];
const COMPOUND_TORSO_BUCKET_MEMBERS = [
  "SpinalExtensors", "DeepAndIntersegmentalBack", "AbdominalWall", "Chest", "BreathingMuscles", "PelvicFloorAndPerineum",
];

export function isRegionalCompound(exercise, group) {
  if (exercise.mode !== "Repetition" || exercise.presentation !== "Motion") return false;
  const trained = [exercise.primaryCanonicalGroup, ...(exercise.secondaryCanonicalGroups ?? [])];
  // Broad rounds must recognize reviewed work across the shoulder and elbow,
  // without treating fine hand, face and neck subdivisions as equal-sized regions.
  if (COMPOUND_UPPER_BUCKET_MEMBERS.every((muscle) => group.canonicalGroups.includes(muscle))) {
    return (REGIONAL_SHOULDER_TARGETS.has(exercise.primaryCanonicalGroup) ||
      REGIONAL_ELBOW_TARGETS.has(exercise.primaryCanonicalGroup)) &&
      trained.some((muscle) => REGIONAL_SHOULDER_TARGETS.has(muscle)) &&
      trained.some((muscle) => REGIONAL_ELBOW_TARGETS.has(muscle));
  }
  // Direct front-and-back trunk work does not need an invented breathing,
  // pelvic-floor or incidental stabilization claim to be a broad torso movement.
  if (COMPOUND_TORSO_BUCKET_MEMBERS.every((muscle) => group.canonicalGroups.includes(muscle))) {
    return (ANTERIOR_TRUNK_TARGETS.has(exercise.primaryCanonicalGroup) ||
      POSTERIOR_TRUNK_TARGETS.has(exercise.primaryCanonicalGroup)) &&
      trained.some((muscle) => ANTERIOR_TRUNK_TARGETS.has(muscle)) &&
      trained.some((muscle) => POSTERIOR_TRUNK_TARGETS.has(muscle));
  }
  return false;
}

function getSequenceMembers(root, exercisesById) {
  if (!Array.isArray(root?.sequenceBlocks) || root.sequenceBlocks.length === 0) {
    return [];
  }
  const members = new Map();
  for (const block of root.sequenceBlocks) {
    const member = exercisesById.get(block.exerciseId);
    if (!member) {
      return [];
    }
    members.set(member.id, member);
  }
  return [...members.values()];
}

function getSequenceCanonicalCoverage(root, exercisesById, group) {
  const trained = new Set(getSequenceMembers(root, exercisesById)
    .flatMap((member) => [
      member.primaryCanonicalGroup,
      ...(member.secondaryCanonicalGroups ?? []),
    ]));
  return group.canonicalGroups.filter((canonicalGroup) =>
    trained.has(canonicalGroup)).length;
}

function isSequenceSelectable(root, exercisesById, group) {
  const members = getSequenceMembers(root, exercisesById);
  if (members.length === 0) return false;
  const trained = new Set(members.flatMap((member) => [
    member.primaryCanonicalGroup, ...(member.secondaryCanonicalGroups ?? []),
  ]));
  return group.canonicalGroups.filter((muscle) => trained.has(muscle)).length >=
    getRequiredCanonicalCoverage(group) ||
    members.every((member) => isRegionalCompound(member, group));
}

function getSequencePrimaryGroups(root, exercisesById, groups) {
  const coveredGroupIds = new Set();
  for (const block of root?.sequenceBlocks ?? []) {
    const member = exercisesById.get(block.exerciseId);
    const primaryGroups = groups.filter((group) =>
      group.canonicalGroups.includes(member?.primaryCanonicalGroup));
    if (!member || primaryGroups.length !== 1) {
      return [];
    }
    coveredGroupIds.add(primaryGroups[0].id);
  }
  return groups.filter((group) => coveredGroupIds.has(group.id));
}

function getSequencePlacementOptions(root, exercisesById, groups) {
  if (!Array.isArray(root?.sequenceBlocks) || root.sequenceBlocks.length === 0) {
    return [];
  }
  const eligibleAnchors = groups.filter((group) =>
    isSequenceSelectable(root, exercisesById, group));
  const primaryGroups = getSequencePrimaryGroups(root, exercisesById, groups);
  const canClaimMultiplePrimarySlots = primaryGroups.length > 1 &&
    primaryGroups.every((primaryGroup) => eligibleAnchors.some((anchor) =>
      anchor.id === primaryGroup.id));
  const options = eligibleAnchors.map((anchor) =>
    canClaimMultiplePrimarySlots && primaryGroups.some((primaryGroup) =>
      primaryGroup.id === anchor.id)
      ? primaryGroups
      : [anchor]);
  return [...new Map(options.map((option) => [
    option.map((group) => group.id).sort().join("|"),
    [...option].sort((left, right) => left.order - right.order),
  ])).values()];
}

function getSelectedSequencePlacements(
  minutes,
  sequenceRoots,
  exercisesById,
  isRelaxedSingletonValid = null,
  selectionGroups = null,
) {
  const groups = Array.isArray(selectionGroups)
    ? selectionGroups
    : getResolution(minutes > 30 ? 30 : minutes).groups;
  const selectedGroupsByRootId = new Map();
  for (const group of groups) {
    const root = sequenceRoots.get(group.id);
    if (!root || !Array.isArray(root.sequenceBlocks) || root.sequenceBlocks.length === 0) {
      throw new Error(`${group.displayName} has no selected sequence.`);
    }
    const selected = selectedGroupsByRootId.get(root.id) ?? { root, groups: [] };
    selected.groups.push(group);
    selectedGroupsByRootId.set(root.id, selected);
  }

  const placements = [];
  for (const { root, groups: selectedGroups } of selectedGroupsByRootId.values()) {
    const remainingGroupIds = new Set(selectedGroups.map((group) => group.id));
    // Keep complete cross-primary placements together; repeated roots in other
    // eligible slots have separate round IDs, feedback and Keep.
    for (const option of getSequencePlacementOptions(root, exercisesById, groups)) {
      if (!option.every((group) => remainingGroupIds.has(group.id))) continue;
      const coveredGroups = [...option].sort((left, right) => left.order - right.order);
      placements.push({ root, anchor: coveredGroups[0], coveredGroups });
      for (const group of option) remainingGroupIds.delete(group.id);
    }
    for (const group of selectedGroups.filter((item) => remainingGroupIds.has(item.id))) {
      if (typeof isRelaxedSingletonValid !== "function" ||
          !isRelaxedSingletonValid(root, group)) {
        throw new Error(
          "The selected atomic sequence placements do not match their " +
          "primary-muscle workout slots.",
        );
      }
      placements.push({ root, anchor: group, coveredGroups: [group] });
    }
  }
  return placements.sort((left, right) => left.anchor.order - right.anchor.order);
}

export function getSequenceMuscularDemand(root, exercisesById) {
  if (!root || !(exercisesById instanceof Map) ||
      !Array.isArray(root.sequenceBlocks) || root.sequenceBlocks.length === 0) {
    throw new TypeError("A scheduled sequence and exercise map are required.");
  }
  return Math.max(...root.sequenceBlocks.map((block) => {
    const exercise = exercisesById.get(block.exerciseId);
    if (!exercise) {
      throw new Error(`Sequence block exercise ${block.exerciseId} is missing.`);
    }
    return exercise.muscularDemand;
  }));
}

export function orderSelectedSequencePlacementsForSchedule(
  placements,
  exercisesById,
  frozenSelectionGroupIds = [],
) {
  const placementArray = [...placements];
  const frozenIds = Array.isArray(frozenSelectionGroupIds)
    ? frozenSelectionGroupIds
    : [];
  if (frozenIds.length === placementArray.length &&
      new Set(frozenIds).size === placementArray.length) {
    const placementsByAnchor = new Map(placementArray.map((placement) =>
      [placement.anchor.id, placement]));
    if (frozenIds.every((selectionGroupId) =>
      placementsByAnchor.has(selectionGroupId))) {
      return frozenIds.map((selectionGroupId) =>
        placementsByAnchor.get(selectionGroupId));
    }
  }

  return placementArray.sort((left, right) =>
    getMuscularDemandSchedulePriority(
      getSequenceMuscularDemand(left.root, exercisesById),
    ) - getMuscularDemandSchedulePriority(
      getSequenceMuscularDemand(right.root, exercisesById),
    ) || left.anchor.order - right.anchor.order);
}

export function isPrimaryForGroup(exercise, group) {
  return group.canonicalGroups.includes(exercise.primaryCanonicalGroup);
}

export function calculateCanonicalMuscleLoadEighthUnits(scheduledExercises) {
  const result = new Map();
  for (const exercise of scheduledExercises) {
    addExerciseMuscleLoadEighthUnits(result, exercise, 1);
  }
  return result;
}

export function addExerciseMuscleLoadEighthUnits(
  loadEighthUnits,
  exercise,
  setCount = 1,
) {
  if (!(loadEighthUnits instanceof Map)) {
    throw new TypeError("Muscle load must be a Map.");
  }
  if (!Number.isInteger(setCount) || setCount < 1) {
    throw new RangeError("Set count must be a positive integer.");
  }

  const primaryLoad = getPrimaryMuscleLoadEighthUnits(exercise) * setCount;
  loadEighthUnits.set(
    exercise.primaryCanonicalGroup,
    (loadEighthUnits.get(exercise.primaryCanonicalGroup) ?? 0) + primaryLoad,
  );
  const secondaryLoad = getSecondaryMuscleLoadEighthUnits(exercise) * setCount;
  for (const secondary of new Set(exercise.secondaryCanonicalGroups ?? [])) {
    loadEighthUnits.set(
      secondary,
      (loadEighthUnits.get(secondary) ?? 0) + secondaryLoad,
    );
  }
}

export function getPrimaryMuscleLoadEighthUnits(exercise) {
  switch (exercise?.muscularDemand) {
    case MINIMUM_MUSCULAR_DEMAND:
      return MINIMUM_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS;
    case MODERATE_MUSCULAR_DEMAND:
      return MODERATE_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS;
    case HARD_MUSCULAR_DEMAND:
      return HARD_PRIMARY_MUSCLE_LOAD_EIGHTH_UNITS;
    default:
      throw new RangeError("Muscular demand must be between 0 and 2.");
  }
}

export function getSecondaryMuscleLoadEighthUnits(exercise) {
  switch (exercise?.muscularDemand) {
    case MINIMUM_MUSCULAR_DEMAND:
      return MINIMUM_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS;
    case MODERATE_MUSCULAR_DEMAND:
      return MODERATE_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS;
    case HARD_MUSCULAR_DEMAND:
      return HARD_SECONDARY_MUSCLE_LOAD_EIGHTH_UNITS;
    default:
      throw new RangeError("Muscular demand must be between 0 and 2.");
  }
}

function muscleBalanceShare(balance) {
  return balance.strongestLoadEighthUnits === 0
    ? [1, 1]
    : [balance.weakestLoadEighthUnits, balance.strongestLoadEighthUnits];
}

function compareMuscleResolutionShare(left, right) {
  const [leftNumerator, leftDenominator] = muscleBalanceShare(left);
  const [rightNumerator, rightDenominator] = muscleBalanceShare(right);
  return leftNumerator * rightDenominator -
    rightNumerator * leftDenominator;
}

export function calculateMuscleBalanceEvaluation(canonicalLoadEighthUnits) {
  if (!(canonicalLoadEighthUnits instanceof Map)) {
    throw new TypeError("Canonical muscle load must be a Map.");
  }

  const resolutions = [...RESOLUTIONS].map(([minutes, workoutResolution]) => {
    const loadEighthUnitsByGroupId = new Map(workoutResolution.groups.map((group) => [
      group.id,
      group.canonicalGroups.reduce(
        (total, canonicalGroup) =>
          total + (canonicalLoadEighthUnits.get(canonicalGroup) ?? 0),
        0,
      ),
    ]));
    const loads = [...loadEighthUnitsByGroupId.values()];
    const weakestLoadEighthUnits = Math.min(...loads);
    const strongestLoadEighthUnits = Math.max(...loads);
    const isBalanced = strongestLoadEighthUnits === 0 ||
      weakestLoadEighthUnits * MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR >=
        strongestLoadEighthUnits * MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR;
    return {
      minutes,
      loadEighthUnitsByGroupId,
      weakestLoadEighthUnits,
      strongestLoadEighthUnits,
      isBalanced,
    };
  });
  return {
    resolutions,
    isBalanced: resolutions.every((resolutionBalance) =>
      resolutionBalance.isBalanced),
  };
}

export function compareMuscleBalanceEvaluations(left, right) {
  if (left.resolutions.length !== right.resolutions.length) {
    throw new RangeError(
      "Muscle-balance evaluations must cover the same resolutions.",
    );
  }
  const order = (evaluation) => [...evaluation.resolutions].sort(
    (first, second) =>
      compareMuscleResolutionShare(first, second) || first.minutes - second.minutes,
  );
  const leftOrdered = order(left);
  const rightOrdered = order(right);
  for (let index = 0; index < leftOrdered.length; index += 1) {
    const comparison = compareMuscleResolutionShare(
      leftOrdered[index],
      rightOrdered[index],
    );
    if (comparison !== 0) {
      return Math.sign(comparison);
    }
  }
  return 0;
}

function calculateMuscleBalanceAfterCanonicalDelta(
  currentEvaluation,
  canonicalLoadDelta,
) {
  const resolutions = currentEvaluation.resolutions.map((currentBalance) => {
    const groupIdByCanonical = MUSCLE_BALANCE_GROUP_ID_BY_CANONICAL.get(
      currentBalance.minutes,
    );
    const groupLoadDelta = new Map();
    for (const [canonicalGroup, loadDelta] of canonicalLoadDelta) {
      if (loadDelta === 0) {
        continue;
      }
      const groupId = groupIdByCanonical.get(canonicalGroup);
      groupLoadDelta.set(
        groupId,
        (groupLoadDelta.get(groupId) ?? 0) + loadDelta,
      );
    }
    let weakestLoadEighthUnits = Number.POSITIVE_INFINITY;
    let strongestLoadEighthUnits = Number.NEGATIVE_INFINITY;
    for (const [groupId, currentLoad] of
      currentBalance.loadEighthUnitsByGroupId) {
      const nextLoad = currentLoad + (groupLoadDelta.get(groupId) ?? 0);
      weakestLoadEighthUnits = Math.min(weakestLoadEighthUnits, nextLoad);
      strongestLoadEighthUnits = Math.max(strongestLoadEighthUnits, nextLoad);
    }
    return {
      minutes: currentBalance.minutes,
      loadEighthUnitsByGroupId: null,
      weakestLoadEighthUnits,
      strongestLoadEighthUnits,
      isBalanced: strongestLoadEighthUnits === 0 ||
        weakestLoadEighthUnits * MINIMUM_BALANCED_MUSCLE_SHARE_DENOMINATOR >=
          strongestLoadEighthUnits * MINIMUM_BALANCED_MUSCLE_SHARE_NUMERATOR,
    };
  });
  return {
    resolutions,
    isBalanced: resolutions.every((resolutionBalance) =>
      resolutionBalance.isBalanced),
  };
}

export function isSequenceContinuationRound(group) {
  return (group?.setNumber ?? 1) > 1 ||
    (group?.sequenceBlockIndex ?? 0) > 0;
}

export function isSequenceRound(group) {
  return (group?.sequenceBlockCount ?? 1) > 1;
}

export function hasRepeatedSets(group) {
  return (group?.setCount ?? 1) > 1;
}

export function isFinalSequenceRound(group) {
  return (group?.sequenceBlockIndex ?? 0) ===
      (group?.sequenceBlockCount ?? 1) - 1 &&
    (group?.setNumber ?? 1) === (group?.setCount ?? 1);
}

export function getMovementDurationMs() {
  return MOVEMENT_DURATION_MS;
}

export function isModifierMetadataComplete(exercises) {
  return exercises.every((exercise) =>
    typeof exercise.wallRequired === "boolean" &&
    typeof exercise.soleWallContactRequired === "boolean" &&
    (!exercise.soleWallContactRequired || exercise.wallRequired) &&
    MODIFIER_RULES.every((rule) => rule.isReviewed(exercise)));
}

export function isSessionMovementMetadataValid(exercises) {
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  if (exercisesById.size !== exercises.length) {
    return false;
  }

  const validSideCues = new Set([
    "None", "ScreenLeft", "ScreenRight", "ShownLeadStance", "OppositeLeadStance",
  ]);
  const validDirectionCues = new Set([
    "None", "Forward", "Backward", "Clockwise", "Counterclockwise", "Inward", "Outward",
  ]);
  const validMediaSegments = new Set([
    "Full", "FirstDirection", "SecondDirection",
  ]);
  const sequenceOwnerByExerciseId = new Map();
  for (const root of exercises) {
    if (!Array.isArray(root.sequenceBlocks)) {
      return false;
    }
    if (root.sequenceBlocks.length === 0) {
      continue;
    }
    const uniqueMemberIds = new Set();
    for (const block of root.sequenceBlocks) {
      const member = exercisesById.get(block?.exerciseId);
      if (!member ||
          member.wallRequired !== root.wallRequired ||
          member.soleWallContactRequired !==
            root.soleWallContactRequired ||
          member.upperBodyClothingRequirement !==
            root.upperBodyClothingRequirement ||
          member.shyCompatibility !== root.shyCompatibility ||
          !validSideCues.has(block.sideCue ?? "None") ||
          !validDirectionCues.has(block.directionCue ?? "None") ||
          typeof block.mirrorMedia !== "boolean" ||
          !validMediaSegments.has(block.mediaSegment ?? "Full")) {
        return false;
      }
      uniqueMemberIds.add(member.id);
    }
    if (!uniqueMemberIds.has(root.id)) {
      return false;
    }
    for (const memberId of uniqueMemberIds) {
      if (sequenceOwnerByExerciseId.has(memberId)) {
        return false;
      }
      sequenceOwnerByExerciseId.set(memberId, root.id);
    }
  }
  if (sequenceOwnerByExerciseId.size !== exercises.length) {
    return false;
  }

  const membersByMovementId = new Map();
  for (const exercise of exercises) {
    if (exercise.sessionMovementId === undefined ||
        exercise.sessionMovementId === 0) {
      continue;
    }
    if (!Number.isInteger(exercise.sessionMovementId) ||
        exercise.sessionMovementId < 0) {
      return false;
    }
    const members = membersByMovementId.get(exercise.sessionMovementId) ?? [];
    members.push(exercise);
    membersByMovementId.set(exercise.sessionMovementId, members);
  }

  for (const [movementId, members] of membersByMovementId) {
    const root = exercisesById.get(movementId);
    const rootCanonicalGroups = new Set([
      root?.primaryCanonicalGroup,
      ...(root?.secondaryCanonicalGroups ?? []),
    ]);
    if (members.length < 2 ||
        !root ||
        root.sessionMovementId !== root.id ||
        members.some((exercise) =>
          ![
            exercise.primaryCanonicalGroup,
            ...exercise.secondaryCanonicalGroups,
          ].some((group) => rootCanonicalGroups.has(group)))) {
      return false;
    }
  }
  return true;
}

export function isCompatibleWithWorkoutModifiers(exercise, modifiers) {
  const normalized = normalizeWorkoutModifiers(modifiers);
  const wallEquipment = getWallEquipment(normalized);
  return (exercise.wallRequired !== true ||
      wallEquipment !== WALL_EQUIPMENT.None) &&
    (exercise.soleWallContactRequired !== true ||
      wallEquipment === WALL_EQUIPMENT.SolesMayTouch) &&
    MODIFIER_RULES.every((rule) =>
    rule.isCompatibleForProfile(exercise, normalized));
}

export function isWallPreferred(exercise, modifiers) {
  return exercise.wallRequired === true &&
    getWallEquipment(modifiers) !== WALL_EQUIPMENT.None;
}

export function getEquipmentPreferenceCount(exercise, modifiers) {
  return Number(isWallPreferred(exercise, modifiers)) +
    Number(isMirrorPreferred(exercise, modifiers));
}

export function findWallRequiredCatalogDeficiencies(exercises) {
  const movementCount = new Set(exercises
    .filter((exercise) =>
      exercise.wallRequired === true &&
      exercise.soleWallContactRequired !== true)
    .map(getSessionMovementId)).size;
  return movementCount >= MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS
    ? []
    : [{
        matchingSessionMovementCount: movementCount,
        requiredSessionMovementCount:
          MINIMUM_WALL_REQUIRED_SESSION_MOVEMENTS,
      }];
}

export function findSoleWallContactRequiredCatalogDeficiencies(exercises) {
  const movementCount = new Set(exercises
    .filter((exercise) => exercise.soleWallContactRequired === true)
    .map(getSessionMovementId)).size;
  return movementCount >=
      MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS
    ? []
    : [{
        matchingSessionMovementCount: movementCount,
        requiredSessionMovementCount:
          MINIMUM_SOLE_WALL_CONTACT_REQUIRED_SESSION_MOVEMENTS,
      }];
}

export function isMirrorRelevant(exercise) {
  return exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly ||
    exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly;
}

export function isMirrorPreferred(exercise, modifiers) {
  const equipment = getMirrorEquipment(modifiers);
  if (equipment === MIRROR_EQUIPMENT.None) {
    return false;
  }

  if (exercise.mirrorRelationship === EXERCISE_MIRROR_RELATIONSHIP.MirrorOnly) {
    return isMirrorCompatible(exercise, normalizeWorkoutModifiers(modifiers));
  }
  return exercise.mirrorRelationship ===
      EXERCISE_MIRROR_RELATIONSHIP.BenefitsGreatly &&
    (exercise.minimumMirrorCoverage === EXERCISE_MIRROR_COVERAGE.UpperBody ||
      equipment === MIRROR_EQUIPMENT.Tall);
}

export function isSelectableForWorkoutProfile(exercise, group, modifiers) {
  return isSelectable(exercise, group) &&
    isCompatibleWithWorkoutModifiers(exercise, modifiers);
}

function isSequenceUnitEligible(
  exercise,
  exercisesById,
  group,
  modifiers,
) {
  if (!isSequenceSelectable(exercise, exercisesById, group)) {
    return false;
  }

  return isSequenceCompatible(exercise, exercisesById, modifiers);
}

function isSequenceCompatible(exercise, exercisesById, modifiers) {
  if (!Array.isArray(exercise?.sequenceBlocks) ||
      exercise.sequenceBlocks.length === 0) {
    return false;
  }
  const memberIds = [...new Set(exercise.sequenceBlocks.map((block) =>
    block.exerciseId))];
  return memberIds.length > 0 && memberIds.every((exerciseId) => {
    const member = exercisesById.get(exerciseId);
    return member &&
      MODIFIER_RULES.every((rule) => rule.isReviewed(member)) &&
      isCompatibleWithWorkoutModifiers(member, modifiers);
  });
}

export function findWorkoutModifierPairCoverageDeficiencies(exercises) {
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const rulePairs = MODIFIER_RULES.flatMap((firstRule, firstIndex) =>
    MODIFIER_RULES.slice(firstIndex + 1).map((secondRule) =>
      ({ firstRule, secondRule })));
  return [...RESOLUTIONS.entries()].flatMap(([minutes, resolution]) =>
    resolution.groups.flatMap((group) =>
      rulePairs.flatMap(({ firstRule, secondRule }) =>
        getModifierRuleStateProfiles(firstRule).flatMap((firstState) =>
          getModifierRuleStateProfiles(secondRule).map((secondState) => {
            const profile = normalizeWorkoutModifiers(firstState | secondState);
            const mirrorEquipment = getMirrorEquipment(profile);
            // Every selectable relationship counts; materiality separately
            // verifies that mirror-relevant movements provide a real benefit.
            return {
              minutes,
              groupId: group.id,
              groupName: group.displayName,
              firstModifier: firstRule.flag,
              firstModifierEnabled: firstState !== WORKOUT_MODIFIERS.None,
              secondModifier: secondRule.flag,
              secondModifierEnabled: secondState !== WORKOUT_MODIFIERS.None,
              mirrorEquipment,
              requiredExerciseCount:
                isCoverageException(group, profile)
                  ? 0
                  : getMinimumExercisesPerModifierPairStatePerGroup(minutes),
              matchingExerciseCount: new Set(exercises
                .filter((exercise) =>
                  MODIFIER_RULES.every((rule) => rule.isReviewed(exercise)) &&
                  isSequenceUnitEligible(
                    exercise,
                    exercisesById,
                    group,
                    profile,
                  ))
                .map(getSessionMovementId)).size,
            };
          })))
        .filter((result) =>
          result.matchingExerciseCount < result.requiredExerciseCount)));
}

export function findHardFloorCategoryCoverageDeficiencies(exercises) {
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const requiredCategories = [
    EXERCISE_HARD_FLOOR_COMPATIBILITY.Compatible,
  ];
  const partnerStates = [
    [WORKOUT_MODIFIERS.Insect, false],
    [WORKOUT_MODIFIERS.Insect, true],
    [WORKOUT_MODIFIERS.Silence, false],
    [WORKOUT_MODIFIERS.Silence, true],
    [WORKOUT_MODIFIERS.Mirror, false],
  ];

  return [...RESOLUTIONS.entries()].flatMap(([minutes, resolution]) =>
    resolution.groups.flatMap((group) =>
      requiredCategories.flatMap((hardFloorCompatibility) =>
        partnerStates.map(([partnerModifier, partnerModifierEnabled]) => {
          let profile = WORKOUT_MODIFIERS.HardFloor;
          if (partnerModifierEnabled) {
            profile |= partnerModifier;
          }
          profile = normalizeWorkoutModifiers(profile);

          const matchingExerciseCount = new Set(exercises
            .filter((exercise) =>
              exercise.hardFloorCompatibility === hardFloorCompatibility &&
              isSequenceHardFloorCategory(
                exercise,
                exercisesById,
                hardFloorCompatibility,
              ) &&
              isSequenceUnitEligible(
                exercise,
                exercisesById,
                group,
                profile,
              ))
            .map(getSessionMovementId)).size;
          return {
            minutes,
            groupId: group.id,
            groupName: group.displayName,
            hardFloorCompatibility,
            partnerModifier,
            partnerModifierEnabled,
            matchingExerciseCount,
            requiredExerciseCount:
              isCoverageException(group, profile)
                ? 0
                : getMinimumExercisesPerModifierPairStatePerGroup(minutes),
          };
        }))
      .filter((result) =>
        result.matchingExerciseCount < result.requiredExerciseCount)));
}

export function findMuscularDemandCoverageDeficiencies(exercises) {
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const requiredCategories = [
    MINIMUM_MUSCULAR_DEMAND,
    MAXIMUM_MUSCULAR_DEMAND,
  ];

  const minutes = BROAD_COVERAGE_RESOLUTION_MINUTES;
  const resolution = RESOLUTIONS.get(minutes);
  return resolution.groups.flatMap((group) =>
      requiredCategories.flatMap((muscularDemand) =>
        WORKOUT_MODIFIER_VALIDATION_PROFILES.map((profile) => {
          const matchingExerciseCount = new Set(exercises
            .filter((exercise) =>
              isSequenceCompatible(
                exercise,
                exercisesById,
                profile,
              ) && isSequenceMuscularDemandCategoryForGroup(
                exercise,
                exercisesById,
                group,
                muscularDemand,
              ))
            .map(getSessionMovementId)).size;
          return {
            minutes,
            groupId: group.id,
            groupName: group.displayName,
            muscularDemand,
            profile,
            matchingExerciseCount,
            requiredExerciseCount:
              MINIMUM_EXERCISES_PER_MUSCULAR_DEMAND_CATEGORY_PER_GROUP,
          };
        })))
    .filter((result) =>
      result.matchingExerciseCount < result.requiredExerciseCount);
}

function isSequenceHardFloorCategory(
  exercise,
  exercisesById,
  hardFloorCompatibility,
) {
  return Array.isArray(exercise?.sequenceBlocks) &&
    exercise.sequenceBlocks.length > 0 &&
    [...new Set(exercise.sequenceBlocks.map((block) => block.exerciseId))]
      .every((exerciseId) =>
        exercisesById.get(exerciseId)?.hardFloorCompatibility ===
          hardFloorCompatibility);
}

function isSequenceMuscularDemandCategoryForGroup(
  exercise,
  exercisesById,
  group,
  muscularDemand,
) {
  const members = [...new Set(exercise?.sequenceBlocks?.map((block) =>
    block.exerciseId) ?? [])]
    .map((exerciseId) => exercisesById.get(exerciseId))
    .filter(Boolean);
  if (members.length === 0) {
    return false;
  }

  if (muscularDemand === MINIMUM_MUSCULAR_DEMAND) {
    return members.every((member) =>
      member.muscularDemand === MINIMUM_MUSCULAR_DEMAND) &&
      members.some((member) =>
        group.canonicalGroups.includes(member.primaryCanonicalGroup));
  }
  if (muscularDemand === MAXIMUM_MUSCULAR_DEMAND) {
    return members.some((member) =>
      member.muscularDemand === MAXIMUM_MUSCULAR_DEMAND &&
      group.canonicalGroups.includes(member.primaryCanonicalGroup));
  }
  return false;
}

export function findWorkoutModifierMaterialityDeficiencies(exercises) {
  const canonicalGroups = RESOLUTIONS.get(30).groups;
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const reviewedExercises = exercises.filter((exercise) =>
    MODIFIER_RULES.every((rule) => rule.isReviewed(exercise)));
  const materialityRules = MODIFIER_RULES.filter((rule) =>
    rule.flag !== WORKOUT_MODIFIERS.UpperBodyClothing);
  const rulePairs = materialityRules.flatMap((firstRule, firstIndex) =>
    materialityRules.slice(firstIndex + 1).map((secondRule) =>
      ({ firstRule, secondRule })));
  const enabledStates = (rule) =>
    getModifierRuleStateProfiles(rule).filter((state) =>
      state !== WORKOUT_MODIFIERS.None);
  const edges = materialityRules.flatMap((rule) =>
    enabledStates(rule).map((enabledStateProfile) => ({
      rule,
      baseProfile: WORKOUT_MODIFIERS.None,
      enabledStateProfile,
    })));
  for (const { firstRule, secondRule } of rulePairs) {
    for (const firstState of enabledStates(firstRule)) {
      for (const secondState of enabledStates(secondRule)) {
        edges.push({
          rule: firstRule,
          baseProfile: secondState,
          enabledStateProfile: firstState,
        });
        edges.push({
          rule: secondRule,
          baseProfile: firstState,
          enabledStateProfile: secondState,
        });
      }
    }
  }

  return edges.map(({ rule, baseProfile, enabledStateProfile }) => {
    const enabledModifier = rule.flag;
    const enabledProfile = normalizeWorkoutModifiers(
      baseProfile | enabledStateProfile,
    );
    const beforeExerciseIds = new Set(reviewedExercises
      .filter((exercise) => canonicalGroups.some((group) =>
        isSequenceUnitEligible(
          exercise,
          exercisesById,
          group,
          baseProfile,
        )))
      .map(getSessionMovementId));
    const afterExerciseIds = new Set(reviewedExercises
      .filter((exercise) => canonicalGroups.some((group) =>
        isSequenceUnitEligible(
          exercise,
          exercisesById,
          group,
          enabledProfile,
        )))
      .map(getSessionMovementId));
    const isMirror = enabledModifier === WORKOUT_MODIFIERS.Mirror;
    const materialExerciseIds = isMirror
      ? new Set(reviewedExercises
          .filter((exercise) =>
            isMirrorPreferred(exercise, enabledProfile) &&
              canonicalGroups.some((group) =>
              isSequenceUnitEligible(
                exercise,
                exercisesById,
                group,
                enabledProfile,
              )))
          .map(getSessionMovementId))
      : new Set([...beforeExerciseIds].filter((exerciseId) =>
          !afterExerciseIds.has(exerciseId)));
    const requiredMaterialExerciseCount = Math.max(
      MINIMUM_MODIFIER_MATERIALITY_EXERCISES,
      Math.ceil(
        (isMirror ? afterExerciseIds.size : beforeExerciseIds.size) *
          MINIMUM_MODIFIER_MATERIALITY_PERCENT / 100,
      ),
    );
    const affectedGroupCount = canonicalGroups.filter((group) => {
      if (isMirror) {
        return reviewedExercises.some((exercise) =>
          isMirrorPreferred(exercise, enabledProfile) &&
          isSequenceUnitEligible(
            exercise,
            exercisesById,
            group,
            enabledProfile,
          ));
      }
      const baselineMovementIds = new Set(reviewedExercises
        .filter((exercise) =>
          isSequenceUnitEligible(
            exercise,
            exercisesById,
            group,
            baseProfile,
          ))
        .map(getSessionMovementId));
      const modifiedMovementIds = new Set(reviewedExercises
        .filter((exercise) =>
          isSequenceUnitEligible(
            exercise,
            exercisesById,
            group,
            enabledProfile,
          ))
        .map(getSessionMovementId));
      return [...baselineMovementIds].some((movementId) =>
        !modifiedMovementIds.has(movementId));
    }).length;
    const requiredAffectedGroupCount = Math.ceil(
      canonicalGroups.length * MINIMUM_MODIFIER_MATERIALITY_GROUP_PERCENT / 100,
    );

    return {
      baseProfile,
      enabledModifier,
      modifiedProfile: enabledProfile,
      baselineExerciseCount: beforeExerciseIds.size,
      modifiedExerciseCount: afterExerciseIds.size,
      materialExerciseCount: materialExerciseIds.size,
      requiredMaterialExerciseCount,
      affectedGroupCount,
      requiredAffectedGroupCount,
    };
  }).filter((result) =>
    result.materialExerciseCount < result.requiredMaterialExerciseCount ||
    result.affectedGroupCount < result.requiredAffectedGroupCount);
}

export function getMaximumDistinctLineupSize(exercises, groups, modifiers, workoutMinutes = groups.length) {
  return getMaximumLineupSize(exercises, groups, modifiers, workoutMinutes, false);
}

export function getMaximumCompleteLineupSize(exercises, groups, modifiers, workoutMinutes = groups.length) {
  return getMaximumLineupSize(exercises, groups, modifiers, workoutMinutes, true);
}

function getMaximumLineupSize(exercises, groups, modifiers, workoutMinutes, allowRepeatedMovements) {
  if (!Number.isInteger(workoutMinutes) || workoutMinutes < groups.length) {
    throw new RangeError("Workout minutes must fit every workout group.");
  }
  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const candidateOneBlockMovementsByGroupId = new Map(groups.map((group) =>
    [group.id, new Set()]));
  for (const exercise of exercises) {
    if (exercise.sequenceBlocks?.length !== 1 ||
        !isSequenceCompatible(exercise, exercisesById, modifiers)) {
      continue;
    }
    const movementId = getSessionMovementId(exercise);
    for (const option of getSequencePlacementOptions(
      exercise,
      exercisesById,
      groups,
    )) {
      if (option.length === 1) {
        candidateOneBlockMovementsByGroupId.get(option[0].id)?.add(movementId);
      }
    }
  }
  const candidateOneBlockMovementsByGroup = groups
    .map((group) => [...candidateOneBlockMovementsByGroupId.get(group.id)])
    .sort((left, right) => left.length - right.length);
  const assignedOneBlockGroupByMovement = new Map();
  const tryAssignOneBlockMovement = (groupIndex, visitedMovementIds) => {
    for (const movementId of candidateOneBlockMovementsByGroup[groupIndex]) {
      if (visitedMovementIds.has(movementId)) {
        continue;
      }
      visitedMovementIds.add(movementId);
      const assignedGroupIndex = assignedOneBlockGroupByMovement.get(movementId);
      if (assignedGroupIndex === undefined ||
          tryAssignOneBlockMovement(assignedGroupIndex, visitedMovementIds)) {
        assignedOneBlockGroupByMovement.set(movementId, groupIndex);
        return true;
      }
    }
    return false;
  };
  let oneBlockLineupSize = 0;
  for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
    if (tryAssignOneBlockMovement(groupIndex, new Set())) {
      oneBlockLineupSize += 1;
    }
  }
  if (allowRepeatedMovements) {
    oneBlockLineupSize = candidateOneBlockMovementsByGroup.filter((ids) => ids.length > 0).length;
  }
  if (oneBlockLineupSize === groups.length) {
    return groups.length;
  }

  const groupIndexes = new Map(groups.map((group, index) => [group.id, index]));
  const candidates = [];
  for (const exercise of exercises.filter((candidate) =>
    isSequenceCompatible(candidate, exercisesById, modifiers))) {
    for (const placement of getSequencePlacementOptions(
      exercise,
      exercisesById,
      groups,
    )) {
      if (exercise.sequenceBlocks.length + groups.length - placement.length >
          workoutMinutes) {
        continue;
      }
      let coverageMask = 0n;
      const utilitiesByGroup = Array(groups.length).fill(0n);
      for (const group of placement) {
        const groupIndex = groupIndexes.get(group.id);
        coverageMask |= 1n << BigInt(groupIndex);
        utilitiesByGroup[groupIndex] = 1n;
      }
      candidates.push({
        exerciseId: exercise.id,
        movementId: getSessionMovementId(exercise),
        coverageMask,
        blockCount: exercise.sequenceBlocks.length,
        utilitiesByGroup,
        tieOrder: exercise.id,
      });
    }
  }

  // One empty one-block placement per group makes this an exact maximum-
  // coverage audit while still using the production atomic-capacity solver.
  for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
    candidates.push({
      exerciseId: -groupIndex - 1,
      movementId: -groupIndex - 1,
      coverageMask: 1n << BigInt(groupIndex),
      blockCount: 1,
      utilitiesByGroup: Array(groups.length).fill(0n),
      tieOrder: Number.MAX_SAFE_INTEGER - groupIndex,
    });
  }

  const solve = allowRepeatedMovements
    ? solveAtomicSequenceLineupAllowingRepeatedMovements : solveAtomicSequenceLineup;
  const solution = solve(
    groups.length,
    workoutMinutes,
    candidates,
  );
  if (!solution) {
    throw new Error("Atomic lineup validation could not place empty muscle slots.");
  }
  return [...solution.exerciseIdByGroupIndex.values()]
    .filter((exerciseId) => exerciseId > 0).length;
}

export function findWorkoutProfileLineupDeficiencies(exercises) {
  return SUPPORTED_MINUTES.flatMap((minutes) => {
    return WORKOUT_MODIFIER_VALIDATION_PROFILES
      .map((profile) => {
        const groups = getResolution(minutes > 30 ? 30 : minutes).groups
          .filter((group) => isSelectionGroupAvailable(group, profile));
        return {
          minutes,
          profile,
          maximumDistinctExerciseCount: getMaximumDistinctLineupSize(
            exercises,
            groups,
            profile,
            minutes,
          ),
          requiredDistinctExerciseCount: groups.length,
        };
      })
      .filter((result) =>
        result.maximumDistinctExerciseCount < result.requiredDistinctExerciseCount);
  });
}

export function findCompleteWorkoutProfileLineupDeficiencies(exercises) {
  return SUPPORTED_MINUTES.flatMap((minutes) =>
    WORKOUT_MODIFIER_VALIDATION_PROFILES.map((profile) => {
      const groups = getResolution(Math.min(minutes, 30)).groups
        .filter((group) => isSelectionGroupAvailable(group, profile));
      return {
        minutes, profile,
        maximumCoveredGroupCount: getMaximumCompleteLineupSize(exercises, groups, profile, minutes),
        requiredGroupCount: groups.length,
      };
    }).filter((result) => result.maximumCoveredGroupCount < result.requiredGroupCount));
}

export function evaluateRecoveryLightMode(
  exercises,
  modifiers,
  lastMeaningfulWorkByPrimaryMuscle,
  lastHardWorkByPrimaryMuscle,
  nowUnixMilliseconds = Date.now(),
) {
  if (!Array.isArray(exercises)) {
    throw new TypeError("Recovery-light evaluation requires an exercise catalog.");
  }

  const exercisesById = new Map(exercises.map((exercise) =>
    [exercise.id, exercise]));
  const physicalProfile = normalizeWorkoutModifiers(
    modifiers & ~WORKOUT_MODIFIERS.Light,
  );
  const selectableMembers = exercises
    .filter((root) => Array.isArray(root.sequenceBlocks) &&
      root.sequenceBlocks.length > 0)
    .map((root) => getSequenceMembers(root, exercisesById))
    .filter((members) => members.every((member) =>
      isCompatibleWithWorkoutModifiers(member, physicalProfile)))
    .flat();

  const availableDemandByMuscle = new Map();
  for (const exercise of selectableMembers) {
    if (exercise.muscularDemand !== MODERATE_MUSCULAR_DEMAND &&
        exercise.muscularDemand !== HARD_MUSCULAR_DEMAND) {
      continue;
    }
    if (!availableDemandByMuscle.has(exercise.primaryCanonicalGroup)) {
      availableDemandByMuscle.set(exercise.primaryCanonicalGroup, new Set());
    }
    availableDemandByMuscle.get(exercise.primaryCanonicalGroup)
      .add(exercise.muscularDemand);
  }

  let recoveringMuscleCount = 0;
  for (const [muscle, demands] of availableDemandByMuscle) {
    const everyDemandIsRecovering = [...demands].every((demand) =>
      demand === MODERATE_MUSCULAR_DEMAND
        ? isPrimaryMuscleWithinModerateRecovery(
          lastMeaningfulWorkByPrimaryMuscle,
          muscle,
          nowUnixMilliseconds,
        )
        : isPrimaryMuscleRecovering(
          lastHardWorkByPrimaryMuscle,
          muscle,
          nowUnixMilliseconds,
        ));
    if (everyDemandIsRecovering) {
      recoveringMuscleCount += 1;
    }
  }

  const eligibleMuscleCount = availableDemandByMuscle.size;
  return {
    recoveringMuscleCount,
    eligibleMuscleCount,
    isActive: eligibleMuscleCount > 0 &&
      recoveringMuscleCount * RECOVERY_LIGHT_MINIMUM_SHARE_DENOMINATOR >=
        eligibleMuscleCount * RECOVERY_LIGHT_MINIMUM_SHARE_NUMERATOR,
  };
}

function isPrimaryMuscleWithinModerateRecovery(
  lastMeaningfulWorkByPrimaryMuscle,
  primaryMuscle,
  nowUnixMilliseconds,
) {
  return isPrimaryMuscleWithinRecoveryWindow(
    lastMeaningfulWorkByPrimaryMuscle,
    primaryMuscle,
    nowUnixMilliseconds,
    MODERATE_RECOVERY_WINDOW_MS,
  );
}

export function getRequiredDistinctLineupSize(groups, modifiers) {
  return groups.filter((group) =>
    isSelectionGroupAvailable(group, modifiers)).length;
}

function solveMaximumWeightAssignment(utilities, allowed, maximumUtility) {
  const groupCount = utilities.length;
  const candidateCount = utilities[0]?.length ?? 0;
  if (candidateCount < groupCount) {
    return Array(groupCount).fill(-1);
  }

  const invalidCost = (maximumUtility + 1n) * BigInt(groupCount + 1);
  const costs = utilities.map((row, groupIndex) =>
    row.map((utility, candidateIndex) =>
      allowed[groupIndex][candidateIndex]
        ? maximumUtility - utility
        : invalidCost));
  const rowPotential = Array(groupCount + 1).fill(0n);
  const columnPotential = Array(candidateCount + 1).fill(0n);
  const matchedRowByColumn = Array(candidateCount + 1).fill(0);
  const previousColumn = Array(candidateCount + 1).fill(0);

  for (let row = 1; row <= groupCount; row += 1) {
    matchedRowByColumn[0] = row;
    let column = 0;
    const minimumReducedCost = Array(candidateCount + 1).fill(null);
    const visitedColumns = Array(candidateCount + 1).fill(false);
    do {
      visitedColumns[column] = true;
      const currentRow = matchedRowByColumn[column];
      let delta = null;
      let nextColumn = 0;
      for (let candidateColumn = 1;
        candidateColumn <= candidateCount;
        candidateColumn += 1) {
        if (visitedColumns[candidateColumn]) {
          continue;
        }
        const reducedCost = costs[currentRow - 1][candidateColumn - 1] -
          rowPotential[currentRow] -
          columnPotential[candidateColumn];
        if (minimumReducedCost[candidateColumn] === null ||
            reducedCost < minimumReducedCost[candidateColumn]) {
          minimumReducedCost[candidateColumn] = reducedCost;
          previousColumn[candidateColumn] = column;
        }
        if (delta === null || minimumReducedCost[candidateColumn] < delta) {
          delta = minimumReducedCost[candidateColumn];
          nextColumn = candidateColumn;
        }
      }
      if (delta === null) {
        return Array(groupCount).fill(-1);
      }
      for (let candidateColumn = 0;
        candidateColumn <= candidateCount;
        candidateColumn += 1) {
        if (visitedColumns[candidateColumn]) {
          rowPotential[matchedRowByColumn[candidateColumn]] += delta;
          columnPotential[candidateColumn] -= delta;
        } else if (minimumReducedCost[candidateColumn] !== null) {
          minimumReducedCost[candidateColumn] -= delta;
        }
      }
      column = nextColumn;
    } while (matchedRowByColumn[column] !== 0);

    do {
      const priorColumn = previousColumn[column];
      matchedRowByColumn[column] = matchedRowByColumn[priorColumn];
      column = priorColumn;
    } while (column !== 0);
  }

  const assignment = Array(groupCount).fill(-1);
  for (let column = 1; column <= candidateCount; column += 1) {
    const row = matchedRowByColumn[column];
    if (row !== 0) {
      assignment[row - 1] = column - 1;
    }
  }
  return assignment;
}

function countMaskBits(mask) {
  let value = mask;
  let count = 0;
  while (value !== 0n) {
    value &= value - 1n;
    count += 1;
  }
  return count;
}

function solveAtomicSequenceLineupAllowingRepeatedMovements(
  groupCount, workoutMinutes, sourceCandidates,
) {
  // Occupancy keys belong only to the solver. Real identities, utilities and
  // complete blocks remain unchanged; aliases still share each placement key.
  const occupancyKeys = new Map();
  const repeatableCandidates = sourceCandidates.map((candidate) => {
    const key = `${candidate.movementId}:${candidate.coverageMask}`;
    if (!occupancyKeys.has(key)) occupancyKeys.set(key, occupancyKeys.size);
    return { ...candidate, movementId: occupancyKeys.get(key) };
  });
  return solveAtomicSequenceLineup(groupCount, workoutMinutes, repeatableCandidates);
}

function solveAtomicSequenceLineup(
  groupCount,
  workoutMinutes,
  sourceCandidates,
) {
  const allGroupsMask = (1n << BigInt(groupCount)) - 1n;
  const candidates = sourceCandidates.filter((candidate) =>
    candidate.coverageMask !== 0n &&
    (candidate.coverageMask & ~allGroupsMask) === 0n &&
    candidate.blockCount > 0 &&
    candidate.utilitiesByGroup.length === groupCount);
  const candidateUtility = (candidate) => candidate.utilitiesByGroup
    .reduce((sum, utility, groupIndex) =>
      (candidate.coverageMask & (1n << BigInt(groupIndex))) !== 0n
        ? sum + utility
        : sum, 0n);
  const singletonCandidatesByGroup = Array.from({ length: groupCount }, (_, groupIndex) =>
    candidates.filter((candidate) =>
      candidate.coverageMask === (1n << BigInt(groupIndex))));
  const multiGroupCandidates = candidates
    .filter((candidate) => countMaskBits(candidate.coverageMask) > 1)
    .sort((left, right) => {
      const utilityDifference = candidateUtility(right) - candidateUtility(left);
      return utilityDifference > 0n ? 1 : utilityDifference < 0n ? -1 :
        left.blockCount - right.blockCount;
    });
  const maximumUtilityByGroup = Array.from({ length: groupCount }, (_, groupIndex) =>
    candidates
      .filter((candidate) =>
        (candidate.coverageMask & (1n << BigInt(groupIndex))) !== 0n)
      .reduce((maximum, candidate) =>
        candidate.utilitiesByGroup[groupIndex] > maximum
          ? candidate.utilitiesByGroup[groupIndex]
          : maximum, 0n));

  let best = null;
  const selected = [];
  const selectedMovementIds = new Set();

  const canAllocate = (placements) => {
    const baseBlockCount = placements.reduce((sum, candidate) =>
      sum + candidate.blockCount, 0);
    if (baseBlockCount > workoutMinutes) {
      return false;
    }
    const remainingBlocks = workoutMinutes - baseBlockCount;
    if (remainingBlocks === 0) {
      return true;
    }
    const costs = [...new Set(placements.map((candidate) => candidate.blockCount))];
    const fillable = new Array(remainingBlocks + 1).fill(false);
    fillable[0] = true;
    for (let value = 1; value <= remainingBlocks; value += 1) {
      fillable[value] = costs.some((cost) =>
        cost <= value && fillable[value - cost]);
    }
    return fillable[remainingBlocks];
  };

  const completeWithSingletons = (singletonGroupsMask) => {
    const singletonGroupIndexes = Array.from({ length: groupCount }, (_, index) => index)
      .filter((groupIndex) =>
        (singletonGroupsMask & (1n << BigInt(groupIndex))) !== 0n);
    const selectedByGroupIndex = new Map();
    const fixedBlockCount = selected.reduce((sum, candidate) =>
      sum + candidate.blockCount, 0);
    const availableSingletonBlocks = workoutMinutes - fixedBlockCount;
    if (availableSingletonBlocks < singletonGroupIndexes.length) {
      return null;
    }
    const everySingletonMustUseOneBlock =
      availableSingletonBlocks === singletonGroupIndexes.length;
    if (singletonGroupIndexes.length > 0) {
      const movementIds = [...new Map(singletonGroupIndexes
        .flatMap((groupIndex) => singletonCandidatesByGroup[groupIndex])
        .filter((candidate) => !everySingletonMustUseOneBlock ||
          candidate.blockCount === 1)
        .filter((candidate) => !selectedMovementIds.has(candidate.movementId))
        .sort((left, right) => left.tieOrder - right.tieOrder)
        .map((candidate) => [candidate.movementId, candidate.movementId])).values()];
      if (movementIds.length < singletonGroupIndexes.length) {
        return null;
      }
      const movementIndexes = new Map(movementIds.map((movementId, index) =>
        [movementId, index]));
      const allowed = singletonGroupIndexes.map(() => movementIds.map(() => false));
      const utilities = singletonGroupIndexes.map(() => movementIds.map(() => 0n));
      const chosen = singletonGroupIndexes.map(() => movementIds.map(() => null));
      let maximumUtility = 0n;
      for (let row = 0; row < singletonGroupIndexes.length; row += 1) {
        const groupIndex = singletonGroupIndexes[row];
        for (const candidate of singletonCandidatesByGroup[groupIndex]) {
          if (selectedMovementIds.has(candidate.movementId) ||
              (everySingletonMustUseOneBlock && candidate.blockCount !== 1)) {
            continue;
          }
          const column = movementIndexes.get(candidate.movementId);
          const utility = candidate.utilitiesByGroup[groupIndex];
          if (!allowed[row][column] || utility > utilities[row][column]) {
            allowed[row][column] = true;
            utilities[row][column] = utility;
            chosen[row][column] = candidate;
            if (utility > maximumUtility) {
              maximumUtility = utility;
            }
          }
        }
      }
      const assignment = solveMaximumWeightAssignment(
        utilities,
        allowed,
        maximumUtility,
      );
      for (let row = 0; row < singletonGroupIndexes.length; row += 1) {
        const column = assignment[row];
        const candidate = column >= 0 ? chosen[row][column] : null;
        if (!candidate) {
          return null;
        }
        selectedByGroupIndex.set(singletonGroupIndexes[row], candidate);
      }
    }

    while (!canAllocate([...selected, ...selectedByGroupIndex.values()])) {
      const usedMovementIds = new Set([
        ...selected,
        ...selectedByGroupIndex.values(),
      ].map((candidate) => candidate.movementId));
      let replacement = null;
      for (const [groupIndex, current] of selectedByGroupIndex) {
        for (const alternative of singletonCandidatesByGroup[groupIndex]) {
          const savedBlocks = current.blockCount - alternative.blockCount;
          if (savedBlocks <= 0 ||
              (alternative.movementId !== current.movementId &&
                usedMovementIds.has(alternative.movementId))) {
            continue;
          }
          const utilityLoss = current.utilitiesByGroup[groupIndex] -
            alternative.utilitiesByGroup[groupIndex];
          if (!replacement || utilityLoss < replacement.utilityLoss ||
              (utilityLoss === replacement.utilityLoss &&
                savedBlocks < replacement.savedBlocks)) {
            replacement = { groupIndex, alternative, utilityLoss, savedBlocks };
          }
        }
      }
      if (!replacement) {
        return null;
      }
      selectedByGroupIndex.set(replacement.groupIndex, replacement.alternative);
    }

    const exerciseIdByGroupIndex = new Map();
    let utility = 0n;
    for (const candidate of selected) {
      utility += candidateUtility(candidate);
      for (let groupIndex = 0; groupIndex < groupCount; groupIndex += 1) {
        if ((candidate.coverageMask & (1n << BigInt(groupIndex))) !== 0n) {
          exerciseIdByGroupIndex.set(groupIndex, candidate.exerciseId);
        }
      }
    }
    for (const [groupIndex, candidate] of selectedByGroupIndex) {
      exerciseIdByGroupIndex.set(groupIndex, candidate.exerciseId);
      utility += candidate.utilitiesByGroup[groupIndex];
    }
    return exerciseIdByGroupIndex.size === groupCount
      ? { exerciseIdByGroupIndex, utility }
      : null;
  };

  const search = (decidedMask, coveredMask, utility) => {
    let upperBound = utility;
    for (let groupIndex = 0; groupIndex < groupCount; groupIndex += 1) {
      if ((coveredMask & (1n << BigInt(groupIndex))) === 0n) {
        upperBound += maximumUtilityByGroup[groupIndex];
      }
    }
    if (best && upperBound <= best.utility) {
      return;
    }
    if (decidedMask === allGroupsMask) {
      const completed = completeWithSingletons(allGroupsMask & ~coveredMask);
      if (completed && (!best || completed.utility > best.utility)) {
        best = completed;
      }
      return;
    }

    const undecidedGroupIndexes = Array.from({ length: groupCount }, (_, index) => index)
      .filter((groupIndex) =>
        (decidedMask & (1n << BigInt(groupIndex))) === 0n)
      .sort((left, right) => {
        const count = (groupIndex) => multiGroupCandidates.filter((candidate) =>
          (candidate.coverageMask & (1n << BigInt(groupIndex))) !== 0n &&
          (candidate.coverageMask & decidedMask) === 0n &&
          !selectedMovementIds.has(candidate.movementId)).length;
        return count(left) - count(right);
      });
    const nextGroupIndex = undecidedGroupIndexes[0];
    const nextGroupMask = 1n << BigInt(nextGroupIndex);
    for (const candidate of multiGroupCandidates) {
      if ((candidate.coverageMask & nextGroupMask) === 0n ||
          (candidate.coverageMask & decidedMask) !== 0n ||
          selectedMovementIds.has(candidate.movementId)) {
        continue;
      }
      selectedMovementIds.add(candidate.movementId);
      selected.push(candidate);
      search(
        decidedMask | candidate.coverageMask,
        coveredMask | candidate.coverageMask,
        utility + candidateUtility(candidate),
      );
      selected.pop();
      selectedMovementIds.delete(candidate.movementId);
    }
    // Establish a high-utility exact incumbent before exploring the
    // singleton-only branch, allowing the upper bound to prune equivalent
    // catalog choices instead of enumerating them combinatorially.
    search(decidedMask | nextGroupMask, coveredMask, utility);
  };

  if (multiGroupCandidates.length === 0) {
    return completeWithSingletons(allGroupsMask);
  }
  // Seed exact branch-and-bound with the polynomial singleton solution. In
  // ordinary catalogs this already reaches the per-group utility ceiling and
  // prevents exponential enumeration of equivalent multi-group placements.
  best = completeWithSingletons(allGroupsMask);
  search(0n, 0n, 0n);
  return best;
}

export function normalizeWorkoutModifiers(modifiers) {
  if (!Number.isInteger(modifiers)) {
    return WORKOUT_MODIFIERS.None;
  }
  let normalized = modifiers & SUPPORTED_WORKOUT_MODIFIER_MASK;
  if ((normalized & WORKOUT_MODIFIERS.Mirror) === 0) {
    normalized &= ~WORKOUT_MODIFIERS.TallMirror;
  }
  if ((normalized & WORKOUT_MODIFIERS.Wall) === 0) {
    normalized &= ~WORKOUT_MODIFIERS.SoleWallContact;
  }
  return normalized;
}

export function getPersistentSetupModifiers(modifiers) {
  return normalizeWorkoutModifiers(modifiers) & ~WORKOUT_MODIFIERS.Light;
}

export function getMirrorEquipment(modifiers) {
  const normalized = normalizeWorkoutModifiers(modifiers);
  if ((normalized & WORKOUT_MODIFIERS.Mirror) === 0) {
    return MIRROR_EQUIPMENT.None;
  }
  return (normalized & WORKOUT_MODIFIERS.TallMirror) !== 0
    ? MIRROR_EQUIPMENT.Tall
    : MIRROR_EQUIPMENT.Compact;
}

export function withMirrorEquipment(modifiers, equipment) {
  const withoutMirror = normalizeWorkoutModifiers(modifiers) &
    ~(WORKOUT_MODIFIERS.Mirror | WORKOUT_MODIFIERS.TallMirror);
  if (equipment === MIRROR_EQUIPMENT.None) {
    return withoutMirror;
  }
  if (equipment === MIRROR_EQUIPMENT.Compact) {
    return withoutMirror | WORKOUT_MODIFIERS.Mirror;
  }
  if (equipment === MIRROR_EQUIPMENT.Tall) {
    return withoutMirror | WORKOUT_MODIFIERS.Mirror |
      WORKOUT_MODIFIERS.TallMirror;
  }
  throw new RangeError(`Unknown mirror equipment: ${equipment}`);
}

export function getWallEquipment(modifiers) {
  const normalized = normalizeWorkoutModifiers(modifiers);
  if ((normalized & WORKOUT_MODIFIERS.Wall) === 0) {
    return WALL_EQUIPMENT.None;
  }
  return (normalized & WORKOUT_MODIFIERS.SoleWallContact) !== 0
    ? WALL_EQUIPMENT.SolesMayTouch
    : WALL_EQUIPMENT.SolesStayOff;
}

export function withWallEquipment(modifiers, equipment) {
  const withoutWall = normalizeWorkoutModifiers(modifiers) &
    ~(WORKOUT_MODIFIERS.Wall | WORKOUT_MODIFIERS.SoleWallContact);
  if (equipment === WALL_EQUIPMENT.None) {
    return withoutWall;
  }
  if (equipment === WALL_EQUIPMENT.SolesStayOff) {
    return withoutWall | WORKOUT_MODIFIERS.Wall;
  }
  if (equipment === WALL_EQUIPMENT.SolesMayTouch) {
    return withoutWall | WORKOUT_MODIFIERS.Wall |
      WORKOUT_MODIFIERS.SoleWallContact;
  }
  throw new RangeError(`Unknown wall equipment: ${equipment}`);
}

export function getMovementCountdownDurationMs(group) {
  return MOVEMENT_DURATION_MS +
    (isSequenceContinuationRound(group) ? 0 : PREPARATION_DURATION_MS);
}

export function getMovementPhaseState(
  remainingMilliseconds,
  includePreparation = true,
) {
  if (remainingMilliseconds <= 0) {
    return { phase: "Complete", secondsRemaining: 0, segmentDurationSeconds: 0, isExercise: false };
  }

  const totalDuration = MOVEMENT_DURATION_MS +
    (includePreparation ? PREPARATION_DURATION_MS : 0);
  const bounded = Math.min(remainingMilliseconds, totalDuration);
  if (includePreparation && bounded > MOVEMENT_DURATION_MS) {
    return {
      phase: "Preparation",
      secondsRemaining: Math.ceil((bounded - MOVEMENT_DURATION_MS) / 1000),
      segmentDurationSeconds: PREPARATION_DURATION_MS / 1000,
      isExercise: false,
    };
  }

  return {
    phase: "Continuous",
    secondsRemaining: Math.ceil(Math.min(bounded, MOVEMENT_DURATION_MS) / 1000),
    segmentDurationSeconds: MOVEMENT_DURATION_MS / 1000,
    isExercise: true,
  };
}

export function getMovementPresentation(group, phase) {
  if (phase === "Complete") {
    return {
      sideCue: "None",
      directionCue: "None",
      mirrorMedia: false,
    };
  }
  return {
    sideCue: group?.sequenceSideCue ?? "None",
    directionCue: group?.sequenceDirectionCue ?? "None",
    mirrorMedia: group?.mirrorSequenceMedia === true,
  };
}

export function getExerciseVideoPath(exercise, mediaSegment = "Full") {
  return mediaSegment === "SecondDirection"
    ? `exercise_direction_videos/exercise_${formatExerciseId(exercise.id)}.mp4`
    : exercise.video;
}

export function getHoldFramePath(exercise) {
  return `exercise_hold_frames/exercise_${formatExerciseId(exercise.id)}.png`;
}

export function formatExerciseId(exerciseId) {
  return String(exerciseId).padStart(4, "0");
}

export function createDefaultState() {
  return {
    version: CURRENT_WORKOUT_STATE_VERSION,
    catalogRevision: CURRENT_CATALOG_REVISION,
    catalogIdentities: {},
    selectedExerciseIds: {},
    scores: {},
    outcomes: {},
    lastKeptExerciseIds: [],
    keptExerciseRootIdsBySelectionGroupId: {},
    exerciseScoreAdjustmentsBySelectionGroupId: {},
    exerciseScoreAdjustmentsByPhase: {},
    lastHardWorkUnixMillisecondsByPrimaryMuscle: {},
    lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle: {},
    legacyCompletedTrainingDayUnixMilliseconds: [],
    nextWorkoutSessionId: 1,
    activeWorkoutSession: null,
    workoutHistory: [],
    nextWorkoutExcludedExerciseIds: [],
    activeExtraSetSelectionGroupIds: [],
    activeSetCountsBySelectionGroupId: {},
    activeSelectionGroupOrder: [],
    activeDurationSelectionGroupIds: null,
    activeSimpleRoundSelectionGroupIds: [],
    activeModifierRetainedSelectionGroupIds: [],
    activeModifierProtectedSelectionGroupId: null,
    activeDirectionPartnerExerciseIds: {},
    activeFullSideRoundIds: [],
    pendingMovementGroupId: null,
    pendingMovementMillisecondsRemaining: 0,
    pendingMovementEndsAtUnixMilliseconds: 0,
    pendingMovementPausedByUser: false,
    pendingRestGroupId: null,
    pendingRestEndsAtUnixMilliseconds: 0,
    pendingRestMillisecondsRemaining: 0,
    pendingRestPausedByUser: false,
    pendingRestKept: false,
    lastWorkoutMinutes: 10,
    lastWorkoutModifiers: DEFAULT_WORKOUT_MODIFIERS,
    activeWorkoutMinutes: 0,
    activeWorkoutModifiers: WORKOUT_MODIFIERS.None,
    activeWorkoutIsLightDay: false,
    workoutCompleted: false,
    completionAcknowledged: false,
  };
}

export function parseStoredState(serialized) {
  if (!serialized) {
    return createDefaultState();
  }
  try {
    return normalizeStateShape(JSON.parse(serialized));
  } catch {
    return createDefaultState();
  }
}

function normalizeStateShape(raw) {
  const state = createDefaultState();
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) {
    return state;
  }

  // A stored object without a version predates the modifier-profile schema.
  // Treat it as legacy instead of mistaking it for a brand-new state.
  const sourceVersion = Number.isInteger(raw[SOURCE_STATE_VERSION])
    ? raw[SOURCE_STATE_VERSION]
    : Number.isInteger(raw.version) ? raw.version : 0;
  state.version = sourceVersion;
  state.catalogRevision = Number.isInteger(raw.catalogRevision)
    ? raw.catalogRevision
    : 0;
  state.lastWorkoutMinutes = normalizeMinutes(raw.lastWorkoutMinutes);
  state.lastWorkoutModifiers = raw.lastWorkoutModifiers === undefined
    ? state.lastWorkoutModifiers
    : normalizeWorkoutModifiers(raw.lastWorkoutModifiers);
  state.activeWorkoutMinutes = Number.isInteger(raw.activeWorkoutMinutes)
    ? raw.activeWorkoutMinutes
    : 0;
  state.activeWorkoutModifiers = raw.activeWorkoutModifiers === undefined
    ? state.activeWorkoutModifiers
    : normalizeWorkoutModifiers(raw.activeWorkoutModifiers);
  state.activeWorkoutIsLightDay = raw.activeWorkoutIsLightDay === true;
  state.workoutCompleted = raw.workoutCompleted === true;
  state.completionAcknowledged = raw.completionAcknowledged === true;
  state.pendingRestGroupId =
    typeof raw.pendingRestGroupId === "string" ? raw.pendingRestGroupId : null;
  state.pendingRestEndsAtUnixMilliseconds = Number.isFinite(raw.pendingRestEndsAtUnixMilliseconds)
    ? Math.trunc(raw.pendingRestEndsAtUnixMilliseconds)
    : 0;
  state.pendingRestMillisecondsRemaining = Number.isFinite(
    raw.pendingRestMillisecondsRemaining,
  )
    ? Math.trunc(raw.pendingRestMillisecondsRemaining)
    : 0;
  state.pendingRestPausedByUser = raw.pendingRestPausedByUser === true;
  state.pendingRestKept = raw.pendingRestKept === true;
  state.pendingMovementGroupId = typeof raw.pendingMovementGroupId === "string"
    ? raw.pendingMovementGroupId
    : null;
  state.pendingMovementMillisecondsRemaining = Number.isFinite(
    raw.pendingMovementMillisecondsRemaining,
  )
    ? Math.trunc(raw.pendingMovementMillisecondsRemaining)
    : 0;
  state.pendingMovementEndsAtUnixMilliseconds = Number.isFinite(
    raw.pendingMovementEndsAtUnixMilliseconds,
  )
    ? Math.trunc(raw.pendingMovementEndsAtUnixMilliseconds)
    : 0;
  state.pendingMovementPausedByUser = raw.pendingMovementPausedByUser === true;

  for (const [groupId, exerciseId] of Object.entries(objectOrEmpty(raw.selectedExerciseIds))) {
    if (typeof groupId === "string" && Number.isInteger(exerciseId) && exerciseId > 0) {
      state.selectedExerciseIds[groupId] = exerciseId;
    }
  }
  for (const [exerciseId, score] of Object.entries(objectOrEmpty(raw.scores))) {
    if (/^\d+$/.test(exerciseId) && Number.isInteger(score)) {
      state.scores[exerciseId] = score;
    }
  }
  for (const [exerciseId, identity] of Object.entries(objectOrEmpty(raw.catalogIdentities))) {
    if (/^\d+$/.test(exerciseId) && typeof identity === "string") {
      state.catalogIdentities[exerciseId] = identity;
    }
  }
  for (const [groupId, outcome] of Object.entries(objectOrEmpty(raw.outcomes))) {
    if (outcome === "x" || outcome === "neutral" || outcome === "tick") {
      state.outcomes[groupId] = outcome;
    }
  }
  state.lastKeptExerciseIds = uniquePositiveIntegers(raw.lastKeptExerciseIds);
  for (const [selectionGroupId, rootIds] of Object.entries(objectOrEmpty(
    raw.keptExerciseRootIdsBySelectionGroupId,
  ))) {
    if (typeof selectionGroupId === "string" && selectionGroupId.length > 0) {
      state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
        uniquePositiveIntegers(rootIds);
    }
  }
  for (const [selectionGroupId, adjustments] of Object.entries(objectOrEmpty(
    raw.exerciseScoreAdjustmentsBySelectionGroupId,
  ))) {
    if (typeof selectionGroupId !== "string" || selectionGroupId.length === 0) {
      continue;
    }
    const normalized = {};
    for (const [rootId, adjustment] of Object.entries(objectOrEmpty(adjustments))) {
      if (/^\d+$/.test(rootId) && Number.isInteger(adjustment) && adjustment !== 0) {
        normalized[rootId] = adjustment;
      }
    }
    if (Object.keys(normalized).length > 0) {
      state.exerciseScoreAdjustmentsBySelectionGroupId[selectionGroupId] = normalized;
    }
  }
  for (const [phase, adjustments] of Object.entries(objectOrEmpty(
    raw.exerciseScoreAdjustmentsByPhase,
  ))) {
    if (!isPersistableExercisePhase(phase)) {
      continue;
    }
    const normalized = {};
    for (const [rootId, adjustment] of Object.entries(objectOrEmpty(adjustments))) {
      if (/^\d+$/.test(rootId) && Number.isInteger(adjustment) && adjustment < 0) {
        normalized[rootId] = adjustment;
      }
    }
    if (Object.keys(normalized).length > 0) {
      state.exerciseScoreAdjustmentsByPhase[phase] = normalized;
    }
  }
  state.lastHardWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
    raw.lastHardWorkUnixMillisecondsByPrimaryMuscle,
  );
  state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
    raw.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
  );
  state.legacyCompletedTrainingDayUnixMilliseconds = uniquePositiveIntegers(
    raw.legacyCompletedTrainingDayUnixMilliseconds,
  );
  const normalizedWorkoutHistory = normalizeWorkoutHistoryShape(raw);
  state.nextWorkoutSessionId = normalizedWorkoutHistory.nextWorkoutSessionId;
  state.activeWorkoutSession = normalizedWorkoutHistory.activeWorkoutSession;
  state.workoutHistory = normalizedWorkoutHistory.workoutHistory;
  if (state.activeWorkoutSession) {
    state.activeWorkoutIsLightDay = state.activeWorkoutSession.isLightDay;
  }
  state.nextWorkoutExcludedExerciseIds = uniquePositiveIntegers(
    raw.nextWorkoutExcludedExerciseIds,
  );
  state.activeExtraSetSelectionGroupIds = Array.isArray(raw.activeExtraSetSelectionGroupIds)
    ? [...new Set(raw.activeExtraSetSelectionGroupIds.filter((groupId) =>
        typeof groupId === "string"))]
    : [];
  for (const [groupId, setCount] of Object.entries(
    objectOrEmpty(raw.activeSetCountsBySelectionGroupId),
  )) {
    if (typeof groupId === "string" && Number.isInteger(setCount) && setCount > 0) {
      state.activeSetCountsBySelectionGroupId[groupId] = setCount;
    }
  }
  state.activeSelectionGroupOrder = uniqueStrings(
    raw.activeSelectionGroupOrder,
  );
  state.activeDurationSelectionGroupIds = Array.isArray(raw.activeDurationSelectionGroupIds)
    ? uniqueStrings(raw.activeDurationSelectionGroupIds).filter(id => ALL_GROUPS.has(id)) : null;
  state.activeSimpleRoundSelectionGroupIds = uniqueStrings(raw.activeSimpleRoundSelectionGroupIds);
  state.activeModifierRetainedSelectionGroupIds = uniqueStrings(
    raw.activeModifierRetainedSelectionGroupIds,
  );
  state.activeModifierProtectedSelectionGroupId =
    typeof raw.activeModifierProtectedSelectionGroupId === "string" &&
      raw.activeModifierProtectedSelectionGroupId.length > 0
      ? raw.activeModifierProtectedSelectionGroupId
      : null;
  for (const [groupId, exerciseId] of Object.entries(
    objectOrEmpty(raw.activeDirectionPartnerExerciseIds),
  )) {
    if (typeof groupId === "string" && Number.isInteger(exerciseId) && exerciseId > 0) {
      state.activeDirectionPartnerExerciseIds[groupId] = exerciseId;
    }
  }
  state.activeFullSideRoundIds = Array.isArray(raw.activeFullSideRoundIds)
    ? [...new Set(raw.activeFullSideRoundIds.filter((groupId) =>
        typeof groupId === "string"))]
    : [];
  if (state.version < IMPLICIT_SILENCE_STATE_VERSION) {
    migrateImplicitSilenceModifier(state);
  }
  if (state.version < EXPLICIT_MIRROR_EQUIPMENT_STATE_VERSION) {
    migrateExplicitMirrorEquipment(state);
  }
  if (state.version < IMPLICIT_HARD_FLOOR_STATE_VERSION) {
    migrateImplicitHardFloorModifier(state);
  }
  if (state.version < IMPLICIT_UPPER_BODY_CLOTHING_STATE_VERSION) {
    migrateImplicitUpperBodyClothingModifier(state);
  }
  if (state.version < EXPLICIT_LIGHT_MODE_STATE_VERSION) {
    migrateExplicitLightMode(state);
  }
  state.version = CURRENT_WORKOUT_STATE_VERSION;
  Object.defineProperty(state, SOURCE_STATE_VERSION, {
    value: sourceVersion,
    enumerable: false,
  });
  return state;
}

function migrateImplicitSilenceModifier(state) {
  for (const [selectionStorageKey, exerciseId] of
    Object.entries(state.selectedExerciseIds)) {
    const match = /^p(\d+)\|(.+)$/.exec(selectionStorageKey);
    const modifierValue = match ? Number(match[1]) : WORKOUT_MODIFIERS.None;
    const selectionGroupId = match ? match[2] : selectionStorageKey;
    if (!selectionGroupId || normalizeWorkoutModifiers(modifierValue) !== modifierValue) {
      continue;
    }
    const quietProfile = normalizeWorkoutModifiers(
      modifierValue | WORKOUT_MODIFIERS.Silence,
    );
    const quietKey = quietProfile === WORKOUT_MODIFIERS.None
      ? selectionGroupId
      : `${SELECTION_PROFILE_PREFIX}${quietProfile}` +
        `${SELECTION_PROFILE_SEPARATOR}${selectionGroupId}`;
    if (state.selectedExerciseIds[quietKey] === undefined) {
      state.selectedExerciseIds[quietKey] = exerciseId;
    }
  }
  state.lastWorkoutModifiers = normalizeWorkoutModifiers(
    state.lastWorkoutModifiers | WORKOUT_MODIFIERS.Silence,
  );
  if (state.activeWorkoutMinutes > 0) {
    state.activeWorkoutModifiers = normalizeWorkoutModifiers(
      state.activeWorkoutModifiers | WORKOUT_MODIFIERS.Silence,
    );
  }
}

function migrateExplicitMirrorEquipment(state) {
  for (const selectionStorageKey of Object.keys(state.selectedExerciseIds)) {
    const match = /^p(\d+)\|(.+)$/.exec(selectionStorageKey);
    if (match &&
        (normalizeWorkoutModifiers(Number(match[1])) &
          WORKOUT_MODIFIERS.Mirror) !== 0) {
      delete state.selectedExerciseIds[selectionStorageKey];
    }
  }
  // The old binary value did not say whether the available mirror was compact
  // or tall, so do not silently claim either piece of equipment after upgrade.
  state.lastWorkoutModifiers = withMirrorEquipment(
    state.lastWorkoutModifiers,
    MIRROR_EQUIPMENT.None,
  );
  state.activeWorkoutModifiers = withMirrorEquipment(
    state.activeWorkoutModifiers,
    MIRROR_EQUIPMENT.None,
  );
}

function migrateImplicitHardFloorModifier(state) {
  for (const [selectionStorageKey, exerciseId] of
    Object.entries(state.selectedExerciseIds)) {
    const match = /^p(\d+)\|(.+)$/.exec(selectionStorageKey);
    const modifierValue = match ? Number(match[1]) : WORKOUT_MODIFIERS.None;
    const selectionGroupId = match ? match[2] : selectionStorageKey;
    if (!selectionGroupId || normalizeWorkoutModifiers(modifierValue) !== modifierValue) {
      continue;
    }
    const hardFloorProfile = normalizeWorkoutModifiers(
      modifierValue | WORKOUT_MODIFIERS.HardFloor,
    );
    const hardFloorKey = `${SELECTION_PROFILE_PREFIX}${hardFloorProfile}` +
      `${SELECTION_PROFILE_SEPARATOR}${selectionGroupId}`;
    if (state.selectedExerciseIds[hardFloorKey] === undefined) {
      state.selectedExerciseIds[hardFloorKey] = exerciseId;
    }
  }
  state.lastWorkoutModifiers = normalizeWorkoutModifiers(
    state.lastWorkoutModifiers | WORKOUT_MODIFIERS.HardFloor,
  );

  // Do not alter an in-progress workout's modifier profile during upgrade.
}

function migrateImplicitUpperBodyClothingModifier(state) {
  for (const [selectionStorageKey, exerciseId] of
    Object.entries(state.selectedExerciseIds)) {
    const match = /^p(\d+)\|(.+)$/.exec(selectionStorageKey);
    const modifierValue = match ? Number(match[1]) : WORKOUT_MODIFIERS.None;
    const selectionGroupId = match ? match[2] : selectionStorageKey;
    if (!selectionGroupId || normalizeWorkoutModifiers(modifierValue) !== modifierValue) {
      continue;
    }
    const clothingProfile = normalizeWorkoutModifiers(
      modifierValue | WORKOUT_MODIFIERS.UpperBodyClothing,
    );
    const clothingKey = `${SELECTION_PROFILE_PREFIX}${clothingProfile}` +
      `${SELECTION_PROFILE_SEPARATOR}${selectionGroupId}`;
    if (state.selectedExerciseIds[clothingKey] === undefined) {
      state.selectedExerciseIds[clothingKey] = exerciseId;
    }
  }
  state.lastWorkoutModifiers = normalizeWorkoutModifiers(
    state.lastWorkoutModifiers | WORKOUT_MODIFIERS.UpperBodyClothing,
  );

  // Do not rewrite an in-progress workout's modifier profile or checkpoints.
}

function migrateExplicitLightMode(state) {
  state.lastWorkoutModifiers = getPersistentSetupModifiers(
    state.lastWorkoutModifiers,
  );
  for (const session of state.workoutHistory) {
    addLightModifierToLegacyLightSession(session);
  }
  if (state.activeWorkoutSession?.isLightDay === true) {
    addLightModifierToLegacyLightSession(state.activeWorkoutSession);
  }
  if (state.activeWorkoutMinutes > 0 && state.activeWorkoutIsLightDay) {
    enableLightModeForExistingActiveWorkout(state);
  }
}

function enableLightModeForExistingActiveWorkout(state) {
  const previousProfile = normalizeWorkoutModifiers(
    state.activeWorkoutModifiers & ~WORKOUT_MODIFIERS.Light,
  );
  const lightProfile = normalizeWorkoutModifiers(
    previousProfile | WORKOUT_MODIFIERS.Light,
  );
  for (const [selectionStorageKey, exerciseId] of
    Object.entries(state.selectedExerciseIds)) {
    const match = /^p(\d+)\|(.+)$/.exec(selectionStorageKey);
    const storedProfile = match
      ? normalizeWorkoutModifiers(Number(match[1]))
      : WORKOUT_MODIFIERS.None;
    const selectionGroupId = match ? match[2] : selectionStorageKey;
    if (!selectionGroupId || storedProfile !== previousProfile) {
      continue;
    }
    const lightKey = lightProfile === WORKOUT_MODIFIERS.None
      ? selectionGroupId
      : `${SELECTION_PROFILE_PREFIX}${lightProfile}` +
        `${SELECTION_PROFILE_SEPARATOR}${selectionGroupId}`;
    if (state.selectedExerciseIds[lightKey] === undefined) {
      state.selectedExerciseIds[lightKey] = exerciseId;
    }
  }
  state.activeWorkoutModifiers = lightProfile;
  state.activeWorkoutIsLightDay = true;
  if (state.activeWorkoutSession) {
    state.activeWorkoutSession.isLightDay = true;
    addLightModifierToLegacyLightSession(state.activeWorkoutSession);
  }
}

function addLightModifierToLegacyLightSession(session) {
  if (!session || session.isLightDay !== true) {
    return;
  }
  session.modifiers = normalizeWorkoutModifiers(
    session.modifiers | WORKOUT_MODIFIERS.Light,
  );
  for (const change of session.modifierChanges ?? []) {
    change.previousModifiers = normalizeWorkoutModifiers(
      change.previousModifiers | WORKOUT_MODIFIERS.Light,
    );
    change.newModifiers = normalizeWorkoutModifiers(
      change.newModifiers | WORKOUT_MODIFIERS.Light,
    );
  }
}

function uniquePositiveIntegers(value) {
  return Array.isArray(value)
    ? [...new Set(value.filter((item) => Number.isInteger(item) && item > 0))]
    : [];
}

function objectOrEmpty(value) {
  return value && typeof value === "object" && !Array.isArray(value) ? value : {};
}

function normalizeWorkHistory(value) {
  const canonicalMuscleGroups = new Set(CANONICAL_GROUPS.slice(1));
  return Object.fromEntries(Object.entries(objectOrEmpty(value))
    .filter(([primaryMuscle, completedAtUnixMilliseconds]) =>
      canonicalMuscleGroups.has(primaryMuscle) &&
      Number.isSafeInteger(completedAtUnixMilliseconds) &&
      completedAtUnixMilliseconds > 0));
}

function normalizeWorkoutHistoryShape(rawState) {
  const usedSessionIds = new Set();
  let nextWorkoutSessionId = Number.isSafeInteger(rawState.nextWorkoutSessionId) &&
      rawState.nextWorkoutSessionId > 0
    ? rawState.nextWorkoutSessionId
    : 1;

  const claimSessionId = (requestedId) => {
    if (Number.isSafeInteger(requestedId) && requestedId > 0 &&
        !usedSessionIds.has(requestedId)) {
      usedSessionIds.add(requestedId);
      nextWorkoutSessionId = Math.max(nextWorkoutSessionId, requestedId + 1);
      return requestedId;
    }
    while (usedSessionIds.has(nextWorkoutSessionId)) {
      nextWorkoutSessionId += 1;
    }
    if (!Number.isSafeInteger(nextWorkoutSessionId) || nextWorkoutSessionId <= 0) {
      throw new Error("Workout session IDs are exhausted.");
    }
    const assignedId = nextWorkoutSessionId;
    usedSessionIds.add(assignedId);
    nextWorkoutSessionId += 1;
    return assignedId;
  };

  const workoutHistory = Array.isArray(rawState.workoutHistory)
    ? rawState.workoutHistory
        .filter((session) => session && typeof session === "object" &&
          !Array.isArray(session))
        .map((session) => {
          const normalized = normalizeWorkoutSessionLog(session);
          normalized.sessionId = claimSessionId(normalized.sessionId);
          if (normalized.status === "InProgress") {
            normalized.status = "Interrupted";
          }
          return normalized;
        })
    : [];

  let activeWorkoutSession = null;
  if (rawState.activeWorkoutSession &&
      typeof rawState.activeWorkoutSession === "object" &&
      !Array.isArray(rawState.activeWorkoutSession)) {
    activeWorkoutSession = normalizeWorkoutSessionLog(rawState.activeWorkoutSession);
    activeWorkoutSession.sessionId = claimSessionId(activeWorkoutSession.sessionId);
    activeWorkoutSession.status = "InProgress";
    activeWorkoutSession.endedAtUnixMilliseconds = 0;
  }

  return {
    nextWorkoutSessionId: Math.max(1, nextWorkoutSessionId),
    activeWorkoutSession,
    workoutHistory,
  };
}

function normalizeWorkoutSessionLog(raw) {
  const session = objectOrEmpty(raw);
  return {
    sessionId: positiveSafeIntegerOrZero(session.sessionId),
    startedAtUnixMilliseconds: positiveSafeIntegerOrZero(
      session.startedAtUnixMilliseconds,
    ),
    endedAtUnixMilliseconds: positiveSafeIntegerOrZero(
      session.endedAtUnixMilliseconds,
    ),
    workoutMinutes: Number.isInteger(session.workoutMinutes)
      ? session.workoutMinutes
      : 0,
    modifiers: normalizeWorkoutModifiers(session.modifiers),
    isLightDay: session.isLightDay === true,
    status: session.status === "Completed" || session.status === "Interrupted"
      ? session.status
      : "InProgress",
    startedBeforeLogging: session.startedBeforeLogging === true,
    keptExerciseIdsAtStart: uniquePositiveIntegers(session.keptExerciseIdsAtStart)
      .sort((left, right) => left - right),
    keptExerciseRootIdsBySelectionGroupIdAtStart: Object.fromEntries(
      Object.entries(objectOrEmpty(
        session.keptExerciseRootIdsBySelectionGroupIdAtStart,
      ))
        .filter(([selectionGroupId]) => selectionGroupId.length > 0)
        .map(([selectionGroupId, rootIds]) => [
          selectionGroupId,
          uniquePositiveIntegers(rootIds).sort((left, right) => left - right),
        ]),
    ),
    initialSelections: normalizeObjectArray(session.initialSelections).map((selection) => ({
      selectionGroupId: stringOrEmpty(selection.selectionGroupId),
      coveredWorkoutGroupIds: uniqueStrings(selection.coveredWorkoutGroupIds),
      rootExerciseId: positiveIntegerOrZero(selection.rootExerciseId),
      rootExerciseName: stringOrEmpty(selection.rootExerciseName),
      selectionScoreAtStart: integerOrZero(selection.selectionScoreAtStart),
      sequenceBlockCount: positiveIntegerOrZero(selection.sequenceBlockCount),
      setCount: positiveIntegerOrZero(selection.setCount),
      wasKeptAtWorkoutStart: selection.wasKeptAtWorkoutStart === true,
    })),
    selectionChanges: normalizeObjectArray(session.selectionChanges).map((change) => ({
      kind: "Shuffle",
      changedAtUnixMilliseconds: positiveSafeIntegerOrZero(
        change.changedAtUnixMilliseconds,
      ),
      selectionGroupId: stringOrEmpty(change.selectionGroupId),
      exercisePhase: isPersistableExercisePhase(change.exercisePhase)
        ? change.exercisePhase
        : WORKOUT_EXERCISE_PHASE.Unknown,
      rejectedRootExerciseId: positiveIntegerOrZero(change.rejectedRootExerciseId),
      rejectedRootExerciseName: stringOrEmpty(change.rejectedRootExerciseName),
      rejectedSelectionScoreBeforeChange: integerOrZero(
        change.rejectedSelectionScoreBeforeChange,
      ),
      rejectedSelectionWasKeptAtWorkoutStart:
        change.rejectedSelectionWasKeptAtWorkoutStart === true,
      replacementRootExerciseId: positiveIntegerOrZero(
        change.replacementRootExerciseId,
      ),
      replacementRootExerciseName: stringOrEmpty(change.replacementRootExerciseName),
      replacementSelectionScore: integerOrZero(change.replacementSelectionScore),
    })),
    modifierChanges: normalizeObjectArray(session.modifierChanges).map((change) => ({
      changedAtUnixMilliseconds: positiveSafeIntegerOrZero(
        change.changedAtUnixMilliseconds,
      ),
      previousModifiers: normalizeWorkoutModifiers(change.previousModifiers),
      newModifiers: normalizeWorkoutModifiers(change.newModifiers),
      protectedSelectionGroupId: stringOrEmpty(
        change.protectedSelectionGroupId,
      ),
      plannedSelections: normalizeObjectArray(change.plannedSelections)
        .map((selection) => ({
          selectionGroupId: stringOrEmpty(selection.selectionGroupId),
          coveredWorkoutGroupIds: uniqueStrings(
            selection.coveredWorkoutGroupIds,
          ),
          rootExerciseId: positiveIntegerOrZero(selection.rootExerciseId),
          rootExerciseName: stringOrEmpty(selection.rootExerciseName),
          selectionScoreAtStart: integerOrZero(
            selection.selectionScoreAtStart,
          ),
          sequenceBlockCount: positiveIntegerOrZero(
            selection.sequenceBlockCount,
          ),
          setCount: positiveIntegerOrZero(selection.setCount),
          wasKeptAtWorkoutStart: selection.wasKeptAtWorkoutStart === true,
        })),
    })),
    durationChanges: normalizeObjectArray(session.durationChanges).map(change => ({
      changedAtUnixMilliseconds: positiveSafeIntegerOrZero(change.changedAtUnixMilliseconds),
      previousMinutes: positiveIntegerOrZero(change.previousMinutes),
      newMinutes: positiveIntegerOrZero(change.newMinutes),
      plannedSelections: normalizeObjectArray(change.plannedSelections),
    })),
    blocks: normalizeObjectArray(session.blocks).map((block) => ({
      completedAtUnixMilliseconds: positiveSafeIntegerOrZero(
        block.completedAtUnixMilliseconds,
      ),
      workoutGroupId: stringOrEmpty(block.workoutGroupId),
      selectionGroupId: stringOrEmpty(block.selectionGroupId),
      order: integerOrZero(block.order),
      rootExerciseId: positiveIntegerOrZero(block.rootExerciseId),
      rootExerciseName: stringOrEmpty(block.rootExerciseName),
      exerciseId: positiveIntegerOrZero(block.exerciseId),
      exerciseName: stringOrEmpty(block.exerciseName),
      sequenceBlockNumber: positiveIntegerOrZero(block.sequenceBlockNumber),
      sequenceBlockCount: positiveIntegerOrZero(block.sequenceBlockCount),
      setNumber: positiveIntegerOrZero(block.setNumber),
      setCount: positiveIntegerOrZero(block.setCount),
      sideCue: stringOrDefault(block.sideCue, "None"),
      directionCue: stringOrDefault(block.directionCue, "None"),
      mirrorMedia: block.mirrorMedia === true,
      mediaSegment: stringOrDefault(block.mediaSegment, "Full"),
      muscularDemand: integerOrZero(block.muscularDemand),
      primaryCanonicalGroup: stringOrEmpty(block.primaryCanonicalGroup),
      secondaryCanonicalGroups: uniqueStrings(block.secondaryCanonicalGroups),
      wasSequenceKeptAtWorkoutStart: block.wasSequenceKeptAtWorkoutStart === true,
    })),
    decisions: normalizeObjectArray(session.decisions).map((decision) => ({
      decidedAtUnixMilliseconds: positiveSafeIntegerOrZero(
        decision.decidedAtUnixMilliseconds,
      ),
      selectionGroupId: stringOrEmpty(decision.selectionGroupId),
      exercisePhase: isPersistableExercisePhase(decision.exercisePhase)
        ? decision.exercisePhase
        : WORKOUT_EXERCISE_PHASE.Unknown,
      rootExerciseId: positiveIntegerOrZero(decision.rootExerciseId),
      rootExerciseName: stringOrEmpty(decision.rootExerciseName),
      sequenceExerciseIds: uniquePositiveIntegers(decision.sequenceExerciseIds)
        .sort((left, right) => left - right),
      outcome: decision.outcome === "tick" || decision.outcome === "x"
        ? decision.outcome
        : "neutral",
      selectionScoreBeforeDecision: integerOrZero(
        decision.selectionScoreBeforeDecision,
      ),
      completedBlockCount: positiveIntegerOrZero(decision.completedBlockCount),
      plannedBlockCount: positiveIntegerOrZero(decision.plannedBlockCount),
      wasKeptAtWorkoutStart: decision.wasKeptAtWorkoutStart === true,
    })),
  };
}

function normalizeObjectArray(value) {
  return Array.isArray(value)
    ? value.filter((item) => item && typeof item === "object" && !Array.isArray(item))
    : [];
}

function uniqueStrings(value) {
  return Array.isArray(value)
    ? [...new Set(value.filter((item) => typeof item === "string" && item.length > 0))]
    : [];
}

function stringOrEmpty(value) {
  return typeof value === "string" ? value : "";
}

function stringOrDefault(value, fallback) {
  return typeof value === "string" && value.length > 0 ? value : fallback;
}

function integerOrZero(value) {
  return Number.isInteger(value) ? value : 0;
}

function positiveIntegerOrZero(value) {
  return Number.isInteger(value) && value > 0 ? value : 0;
}

function positiveSafeIntegerOrZero(value) {
  return Number.isSafeInteger(value) && value > 0 ? value : 0;
}

function sameStringSet(left, right) {
  const leftSet = new Set(left);
  const rightSet = new Set(right);
  return leftSet.size === rightSet.size &&
    [...leftSet].every((value) => rightSet.has(value));
}

function getLocalCalendarDayNumber(unixMilliseconds) {
  if (!Number.isSafeInteger(unixMilliseconds) || unixMilliseconds <= 0) {
    return null;
  }
  const localTime = new Date(unixMilliseconds);
  if (Number.isNaN(localTime.getTime())) {
    return null;
  }
  return Math.trunc(Date.UTC(
    localTime.getFullYear(),
    localTime.getMonth(),
    localTime.getDate(),
  ) / 86_400_000);
}

export function isLightWorkoutDayDue(
  workoutHistory,
  nowUnixMilliseconds,
  legacyCompletedTrainingDayUnixMilliseconds = [],
) {
  return globalThis.nomadicMethodLightCadence.isDue(
    workoutHistory, nowUnixMilliseconds, legacyCompletedTrainingDayUnixMilliseconds,
  );
}

export function getWorkoutsUntilLightWorkout(
  workoutHistory,
  prospectiveWorkoutMinutes,
  nowUnixMilliseconds,
  legacyCompletedTrainingDayUnixMilliseconds = [],
) {
  return globalThis.nomadicMethodLightCadence.workoutsRemaining(
    workoutHistory, prospectiveWorkoutMinutes, nowUnixMilliseconds,
    legacyCompletedTrainingDayUnixMilliseconds,
  );
}

export function getDefaultWorkoutModifiers(
  persistentSetupModifiers,
  workoutHistory,
  nowUnixMilliseconds,
  legacyCompletedTrainingDayUnixMilliseconds = [],
) {
  const modifiers = getPersistentSetupModifiers(persistentSetupModifiers);
  return isLightWorkoutDayDue(
    workoutHistory,
    nowUnixMilliseconds,
    legacyCompletedTrainingDayUnixMilliseconds,
  )
    ? modifiers | WORKOUT_MODIFIERS.Light
    : modifiers;
}

export function inferLegacyCompletedTrainingDays(
  workoutHistory,
  lastHardWorkByPrimaryMuscle,
  existingLegacyTrainingDayTimestamps,
  nowUnixMilliseconds,
) {
  const today = getLocalCalendarDayNumber(nowUnixMilliseconds);
  if (today === null) {
    throw new RangeError("Current workout time must be positive Unix milliseconds.");
  }

  const loggedCompletedDays = new Set((Array.isArray(workoutHistory)
    ? workoutHistory
    : [])
    .filter((session) => session?.status === "Completed")
    .map((session) => getLocalCalendarDayNumber(
      positiveSafeIntegerOrZero(session.startedAtUnixMilliseconds) ||
        positiveSafeIntegerOrZero(session.endedAtUnixMilliseconds),
    ))
    .filter((dayNumber) => dayNumber !== null));
  const existingLegacyDays = new Set(uniquePositiveIntegers(
    existingLegacyTrainingDayTimestamps,
  ).map(getLocalCalendarDayNumber).filter((day) => day !== null));
  const hardEvidenceByDay = new Map();
  for (const timestamp of Object.values(normalizeWorkHistory(
    lastHardWorkByPrimaryMuscle,
  ))) {
    const day = getLocalCalendarDayNumber(timestamp);
    if (day === null) {
      continue;
    }
    const timestamps = hardEvidenceByDay.get(day) ?? [];
    timestamps.push(timestamp);
    hardEvidenceByDay.set(day, timestamps);
  }

  // Bridge only the uninterrupted days immediately before the current logged
  // streak. Sparse old recovery timestamps are not full session logs.
  let cursor = loggedCompletedDays.has(today) ? today : today - 1;
  while (loggedCompletedDays.has(cursor) || existingLegacyDays.has(cursor)) {
    cursor -= 1;
  }

  const inferred = [];
  while ((hardEvidenceByDay.get(cursor)?.length ?? 0) >=
      MINIMUM_LEGACY_HARD_PRIMARY_MUSCLES) {
    inferred.push(Math.max(...hardEvidenceByDay.get(cursor)));
    cursor -= 1;
  }
  return inferred;
}

export class WorkoutSession {
  constructor(
    exercises,
    storedState = createDefaultState(),
    random = Math.random,
    nowProvider = () => Date.now(),
  ) {
    if (!Array.isArray(exercises)) {
      throw new TypeError("Exercise catalog must be an array.");
    }
    this.exercises = exercises;
    this.exercisesById = new Map(exercises.map((exercise) => [exercise.id, exercise]));
    if (this.exercisesById.size !== exercises.length) {
      throw new Error("Exercise catalog contains duplicate IDs.");
    }
    this.sequenceRootByExerciseId = new Map();
    this.sequenceExercisesByRootId = new Map();
    for (const root of exercises.filter((exercise) =>
      Array.isArray(exercise.sequenceBlocks) && exercise.sequenceBlocks.length > 0)) {
      const memberIds = [...new Set(root.sequenceBlocks.map((block) =>
        block.exerciseId))];
      const members = memberIds.map((memberId) => this.exercisesById.get(memberId));
      this.sequenceExercisesByRootId.set(root.id, members);
      for (const memberId of memberIds) {
        if (!this.exercisesById.has(memberId) ||
            this.sequenceRootByExerciseId.has(memberId)) {
          throw new Error(`Exercise ${memberId} has an invalid sequence owner.`);
        }
        this.sequenceRootByExerciseId.set(memberId, root);
      }
    }
    if (this.sequenceRootByExerciseId.size !== exercises.length) {
      throw new Error("Every exercise must belong to exactly one sequence.");
    }
    this.sequencePlacementOptionsCache = new Map();
    this.state = normalizeStateShape(storedState);
    this.loadedStateVersion = this.state[SOURCE_STATE_VERSION] ?? this.state.version;
    this.random = random;
    this.nowProvider = nowProvider;
    if (this.loadedStateVersion < SLOT_SCOPED_PREFERENCE_STATE_VERSION) {
      this.migrateSlotScopedPreferences();
    }
    if (this.loadedStateVersion < PHASE_SCOPED_DOWNVOTE_STATE_VERSION) {
      if (this.loadedStateVersion < SLOT_SCOPED_PREFERENCE_STATE_VERSION) {
        // Older releases have only the global score baseline, so their
        // historical rejections cannot be assigned a truthful phase.
        this.state.exerciseScoreAdjustmentsBySelectionGroupId = {};
      }
      // This also restores historical Keeps that an older rejection removed.
      this.migratePhaseScopedDownvotes();
    }
    this.normalizeSlotPreferences();
  }

  getCurrentUnixTimeMilliseconds() {
    const value = this.nowProvider();
    if (!Number.isSafeInteger(value) || value <= 0) {
      throw new TypeError("Time provider must return positive Unix milliseconds.");
    }
    return value;
  }

  getDefaultWorkoutModifiers() {
    return getDefaultWorkoutModifiers(
      this.state.lastWorkoutModifiers,
      this.state.workoutHistory,
      this.getCurrentUnixTimeMilliseconds(),
      this.state.legacyCompletedTrainingDayUnixMilliseconds,
    );
  }

  getWorkoutsUntilLightWorkout(prospectiveWorkoutMinutes) {
    return getWorkoutsUntilLightWorkout(
      this.state.workoutHistory,
      prospectiveWorkoutMinutes,
      this.getCurrentUnixTimeMilliseconds(),
      this.state.legacyCompletedTrainingDayUnixMilliseconds,
    );
  }

  isAutomaticLightDayDue() {
    return isLightWorkoutDayDue(
      this.state.workoutHistory,
      this.getCurrentUnixTimeMilliseconds(),
      this.state.legacyCompletedTrainingDayUnixMilliseconds,
    );
  }

  initialize() {
    const currentUnixTimeMilliseconds = this.getCurrentUnixTimeMilliseconds();
    if (this.loadedStateVersion < LEGACY_TRAINING_DAY_INFERENCE_STATE_VERSION) {
      this.migrateLegacyCompletedTrainingDays(currentUnixTimeMilliseconds);
    }
    const atomicSequenceMigration =
      [13, 14].includes(this.loadedStateVersion) &&
        this.state.activeWorkoutMinutes > 0
        ? this.captureLegacyActiveProgress()
        : null;
    this.reconcileCatalog();
    this.normalizeScores();
    this.normalizeSavedLineups();
    this.normalizeSlotPreferences();

    const shouldMigratePreparedLightDay =
      this.loadedStateVersion < PERSISTED_LIGHT_DAY_STATE_VERSION &&
      !this.state.activeWorkoutSession &&
      SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes) &&
      Object.keys(this.state.outcomes).length === 0 &&
      !this.state.workoutCompleted &&
      !this.state.completionAcknowledged &&
      !this.state.pendingMovementGroupId &&
      !this.state.pendingRestGroupId &&
      isLightWorkoutDayDue(
        this.state.workoutHistory,
        currentUnixTimeMilliseconds,
        this.state.legacyCompletedTrainingDayUnixMilliseconds,
      );
    const shouldMigrateDominantLightLineup =
      this.loadedStateVersion < DOMINANT_LIGHT_MODE_STATE_VERSION &&
      this.state.activeWorkoutMinutes > 0 &&
      !this.state.workoutCompleted &&
      !this.state.completionAcknowledged &&
      (this.state.activeWorkoutModifiers & WORKOUT_MODIFIERS.Light) !== 0;
    const shouldMigrateActiveLightLineup =
      shouldMigrateDominantLightLineup &&
      (this.state.activeWorkoutSession !== null ||
       Object.keys(this.state.outcomes).length > 0 ||
       this.state.pendingMovementGroupId !== null ||
       this.state.pendingRestGroupId !== null);
    const shouldMigratePreparedDominantLightLineup =
      shouldMigrateDominantLightLineup &&
      !shouldMigrateActiveLightLineup;
    if (shouldMigratePreparedLightDay) {
      enableLightModeForExistingActiveWorkout(this.state);
      this.carrySlotPreferencesForward();
    }
    this.state.activeWorkoutIsLightDay =
      (this.state.activeWorkoutModifiers & WORKOUT_MODIFIERS.Light) !== 0;

    if (this.state.activeWorkoutMinutes === 0) {
      this.finalizeActiveWorkoutSession("Interrupted");
      this.resetTransientState();
      return;
    }

    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      this.finalizeActiveWorkoutSession("Interrupted");
      this.resetTransientState();
      return;
    }

    this.normalizePendingRest();
    this.normalizeActiveModifierRetainedSelectionGroups();
    this.normalizeActiveModifierTransitionProtection();
    if (shouldMigrateActiveLightLineup) {
      this.migrateActiveLightLineup();
    } else {
      this.repairActiveLineup(
        !shouldMigratePreparedLightDay &&
          !shouldMigratePreparedDominantLightLineup,
      );
      this.normalizeActiveLongWorkoutAllocation();
    }
    if ((shouldMigratePreparedLightDay ||
         shouldMigratePreparedDominantLightLineup) &&
        !shouldMigrateActiveLightLineup) {
      this.rebalanceNewExercisesByMuscleBalance();
      this.setActiveLongWorkoutAllocation();
    }
    if (atomicSequenceMigration) {
      this.migrateLegacyActiveProgress(atomicSequenceMigration);
    }
    this.normalizeOutcomes();
    this.normalizeCompletionState();
    this.normalizePendingRest();
    this.normalizePendingMovement();
    this.normalizeCompletionState();
    if (this.state.workoutCompleted) {
      this.finalizeActiveWorkoutSession("Completed");
    } else {
      this.ensureActiveWorkoutSession(true);
    }
    if (this.state.workoutCompleted && this.state.completionAcknowledged) {
      this.finalizeCurrentWorkout();
    }
  }

  startWorkout(minutes, modifiers = DEFAULT_WORKOUT_MODIFIERS) {
    this.prepareWorkout(minutes, modifiers);
    this.activatePreparedWorkout();
  }

  prepareWorkout(minutes, modifiers = DEFAULT_WORKOUT_MODIFIERS) {
    if (!SUPPORTED_MINUTES.includes(minutes)) {
      throw new RangeError("Unsupported workout duration.");
    }
    if (this.state.activeWorkoutMinutes !== 0) {
      throw new Error("A workout is already active.");
    }

    this.normalizeSlotPreferences();
    const workoutStartedAtUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    if (this.loadedStateVersion < LEGACY_TRAINING_DAY_INFERENCE_STATE_VERSION) {
      this.migrateLegacyCompletedTrainingDays(workoutStartedAtUnixMilliseconds);
    }
    this.finalizeActiveWorkoutSession(
      "Interrupted",
      workoutStartedAtUnixMilliseconds,
    );
    this.state.version = CURRENT_WORKOUT_STATE_VERSION;
    modifiers = normalizeWorkoutModifiers(modifiers);
    if (isLightWorkoutDayDue(
      this.state.workoutHistory,
      workoutStartedAtUnixMilliseconds,
      this.state.legacyCompletedTrainingDayUnixMilliseconds,
    )) {
      modifiers |= WORKOUT_MODIFIERS.Light;
    }
    this.state.lastWorkoutMinutes = minutes;
    this.state.lastWorkoutModifiers = getPersistentSetupModifiers(modifiers);
    this.state.activeWorkoutMinutes = minutes;
    this.state.activeWorkoutModifiers = modifiers;
    this.state.activeWorkoutIsLightDay =
      (modifiers & WORKOUT_MODIFIERS.Light) !== 0;
    this.state.activeSelectionGroupOrder = [];
    this.state.activeDurationSelectionGroupIds = null;
    this.state.activeSimpleRoundSelectionGroupIds = [];
    this.state.activeModifierRetainedSelectionGroupIds = [];
    this.state.activeModifierProtectedSelectionGroupId = null;
    this.state.outcomes = {};
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
    this.clearPendingMovement();
    this.clearPendingRest();
    // Shuffle exclusions belong only to the workout being shuffled. Durable
    // rejection feedback is stored by workout phase.
    this.state.nextWorkoutExcludedExerciseIds = [];
    this.carrySlotPreferencesForward();
    this.repairActiveLineup(
      (modifiers & WORKOUT_MODIFIERS.Light) === 0,
    );
    this.rebalanceNewExercisesByMuscleBalance();
    this.setActiveLongWorkoutAllocation();
    this.reconcileLineupWithScheduledPhases();
  }

  activatePreparedWorkout() {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes) ||
        this.state.activeWorkoutSession ||
        Object.keys(this.state.outcomes).length !== 0 ||
        this.state.workoutCompleted ||
        this.state.completionAcknowledged ||
        this.state.pendingMovementGroupId ||
        this.state.pendingRestGroupId) {
      throw new Error(
        "The workout state does not contain an activatable prepared workout.",
      );
    }
    const workoutStartedAtUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    if ((this.state.activeWorkoutModifiers & WORKOUT_MODIFIERS.Light) === 0 &&
        this.isAutomaticLightDayDue()) {
      enableLightModeForExistingActiveWorkout(this.state);
      this.repairActiveLineup(false);
      this.rebalanceNewExercisesByMuscleBalance();
      this.setActiveLongWorkoutAllocation();
      this.reconcileLineupWithScheduledPhases();
    }
    const keptExerciseIdsAtStart = [...this.state.lastKeptExerciseIds]
      .sort((left, right) => left - right);
    this.createActiveWorkoutSession(
      workoutStartedAtUnixMilliseconds,
      keptExerciseIdsAtStart,
      false,
    );
  }

  migrateActiveLightLineup() {
    const priorPlacements = this.getSelectedSequencePlacements();
    const priorOrderedPlacements = this.getScheduleOrderedPlacements(
      priorPlacements,
    );
    const priorRounds = this.getActiveGroups();
    const currentRound = this.getNextGroup();
    if (!currentRound) {
      this.repairActiveLineup();
      this.normalizeActiveLongWorkoutAllocation();
      return;
    }

    const session = this.ensureActiveWorkoutSession(true);
    const preserveCompletedCurrentSelection =
      this.getPendingRestGroup()?.id === currentRound.id;
    const lockedSelectionGroupIds = new Set(session.decisions
      .map((decision) => decision.selectionGroupId)
      .filter((selectionGroupId) =>
        typeof selectionGroupId === "string" && selectionGroupId.length > 0));
    for (const decidedRound of priorRounds.filter((round) =>
      ["tick", "x"].includes(this.state.outcomes[round.id]))) {
      lockedSelectionGroupIds.add(getSelectionKey(decidedRound));
    }
    if (preserveCompletedCurrentSelection) {
      lockedSelectionGroupIds.add(getSelectionKey(currentRound));
    } else {
      lockedSelectionGroupIds.delete(getSelectionKey(currentRound));
    }

    const lockedPlacements = priorPlacements.filter((placement) =>
      lockedSelectionGroupIds.has(placement.anchor.id));
    const lockedExerciseIdsByGroup = new Map(lockedPlacements.flatMap(
      (placement) => placement.coveredGroups.map((group) =>
        [group.id, placement.root.id]),
    ));
    const lockedSetCountsBySelectionGroupId = new Map(lockedPlacements.map(
      (placement) => [
        placement.anchor.id,
        this.state.activeSetCountsBySelectionGroupId[placement.anchor.id] ?? 1,
      ],
    ));
    const protectedBaseGroupIds = new Set(lockedExerciseIdsByGroup.keys());
    const selectionGroups = this.getSelectionGroups();
    const replannedLineup = this.chooseBestDistinctLineup(
      selectionGroups,
      this.state.activeWorkoutModifiers,
      {
        currentExerciseIds: lockedExerciseIdsByGroup,
        allowSavedSelectionException: true,
        modifierTransitionProtectedGroupIds: protectedBaseGroupIds,
      },
    );

    if (!preserveCompletedCurrentSelection) {
      for (const currentSelectionRound of priorRounds.filter((round) =>
        getSelectionKey(round) === getSelectionKey(currentRound))) {
        delete this.state.outcomes[currentSelectionRound.id];
      }
      this.clearPendingMovement();
      this.clearPendingRest();
      this.state.activeModifierProtectedSelectionGroupId = null;
    }

    this.applyDistinctLineup(selectionGroups, replannedLineup, false);
    this.updateSelectionOrderAfterReconfiguration(priorOrderedPlacements);
    this.rebalanceNewExercisesByMuscleBalance(lockedSelectionGroupIds);
    this.updateSelectionOrderAfterReconfiguration(priorOrderedPlacements);
    this.applyLongWorkoutAllocation(this.chooseLongWorkoutAllocation(
      lockedSelectionGroupIds,
    ));

    const replannedRounds = this.getActiveGroups();
    const changedLockedSelection = [...lockedExerciseIdsByGroup].some(
      ([groupId, exerciseId]) => this.state.selectedExerciseIds[
        this.getSelectionStorageKey(
          groupId,
          this.state.activeWorkoutModifiers,
        )
      ] !== exerciseId,
    );
    const changedLockedSetCount = [...lockedSetCountsBySelectionGroupId].some(
      ([selectionGroupId, setCount]) =>
        (this.state.activeSetCountsBySelectionGroupId[
          selectionGroupId
        ] ?? 1) !== setCount,
    );
    const replannedNextRound = this.getNextGroup();
    if (changedLockedSelection || changedLockedSetCount ||
        Object.keys(this.state.outcomes).some((outcomeGroupId) =>
          !replannedRounds.some((round) => round.id === outcomeGroupId)) ||
        (preserveCompletedCurrentSelection &&
          replannedNextRound?.id !== currentRound.id) ||
        (!preserveCompletedCurrentSelection &&
          (!replannedNextRound ||
            getSelectionKey(replannedNextRound) !== getSelectionKey(currentRound)))) {
      throw new Error(
        "The Light-mode upgrade could not preserve completed work.",
      );
    }

    if (!preserveCompletedCurrentSelection) {
      if (!replannedNextRound) {
        throw new Error(
          "The Light-mode upgrade did not retain the current workout slot.",
        );
      }
      this.pauseMovement(
        replannedNextRound,
        getMovementCountdownDurationMs(replannedNextRound),
        true,
      );
    }

    session.modifierChanges.push({
      changedAtUnixMilliseconds: this.getCurrentUnixTimeMilliseconds(),
      previousModifiers: this.state.activeWorkoutModifiers,
      newModifiers: this.state.activeWorkoutModifiers,
      protectedSelectionGroupId: preserveCompletedCurrentSelection
        ? getSelectionKey(currentRound)
        : "",
      plannedSelections: this.createCurrentSelectionSnapshots(session),
    });
  }

  restoreAfterReopen() {
    if (this.state.pendingMovementMillisecondsRemaining > 0) {
      this.state.pendingMovementEndsAtUnixMilliseconds = 0;
      this.state.pendingMovementPausedByUser = true;
    }
    if (this.state.pendingRestGroupId) {
      this.state.pendingRestMillisecondsRemaining =
        this.state.pendingRestMillisecondsRemaining || 15_000;
      this.state.pendingRestEndsAtUnixMilliseconds = 0;
      this.state.pendingRestPausedByUser = true;
    }
    this.initialize();
  }

  getMinimumActiveWorkoutMinutes() {
    const current = this.getNextGroup();
    if (!current) return SUPPORTED_MINUTES[0];
    const end = Math.max(...this.getActiveGroups()
      .filter(r => getSelectionKey(r) === getSelectionKey(current)).map(r => r.order));
    return SUPPORTED_MINUTES.find(minutes => minutes >= end);
  }

  endActiveWorkout() {
    if (!this.state.activeWorkoutMinutes || this.state.workoutCompleted) return;
    this.finalizeCurrentWorkout();
  }

  resizeActiveWorkout(minutes) {
    if (this.state.activeWorkoutMinutes === minutes) return;
    if (!SUPPORTED_MINUTES.includes(minutes) || this.state.workoutCompleted ||
        !this.state.activeWorkoutMinutes || minutes < this.getMinimumActiveWorkoutMinutes()) {
      throw new Error("The duration must include the current exercise sequence.");
    }
    const current = this.getNextGroup();
    const priorRounds = this.getActiveGroups();
    const prior = this.getScheduleOrderedPlacements();
    const prefix = prior.slice(0, prior.findIndex(p => p.anchor.id === getSelectionKey(current)) + 1);
    const lockedIds = prefix.map(p => p.anchor.id);
    const locked = new Set(lockedIds);
    const lockedExercises = new Map(prefix.flatMap(p => p.coveredGroups.map(g => [g.id, p.root.id])));
    const protectedGroups = new Set(lockedExercises.keys());
    const protectedCount = priorRounds.filter(r => locked.has(getSelectionKey(r))).length;
    const freeMinutes = minutes - protectedCount;
    const covered = new Set(prefix.flatMap(p => this.getSequenceExercises(p.root)
      .flatMap(e => [e.primaryCanonicalGroup, ...e.secondaryCanonicalGroups])));
    let candidates = getResolution(Math.min(minutes, 30)).groups
      .filter(g => !protectedGroups.has(g.id) && isSelectionGroupAvailable(g, this.state.activeWorkoutModifiers))
      .sort((a, b) => a.canonicalGroups.filter(m => covered.has(m)).length / a.canonicalGroups.length -
        b.canonicalGroups.filter(m => covered.has(m)).length / b.canonicalGroups.length || a.order - b.order);
    if (freeMinutes > 0 && candidates.length === 0) {
      candidates = [...ALL_GROUPS.values()]
        .filter(g => !protectedGroups.has(g.id) && isSelectionGroupAvailable(g, this.state.activeWorkoutModifiers))
        .sort((a, b) => a.canonicalGroups.length - b.canonicalGroups.length || a.id.localeCompare(b.id));
    }
    const before = {
      activeWorkoutMinutes: this.state.activeWorkoutMinutes,
      lastWorkoutMinutes: this.state.lastWorkoutMinutes,
      activeDurationSelectionGroupIds: this.state.activeDurationSelectionGroupIds,
      activeSimpleRoundSelectionGroupIds: this.state.activeSimpleRoundSelectionGroupIds,
      selectedExerciseIds: this.state.selectedExerciseIds,
      activeSetCountsBySelectionGroupId: this.state.activeSetCountsBySelectionGroupId,
      activeExtraSetSelectionGroupIds: this.state.activeExtraSetSelectionGroupIds,
      activeSelectionGroupOrder: this.state.activeSelectionGroupOrder,
      activeModifierRetainedSelectionGroupIds: this.state.activeModifierRetainedSelectionGroupIds,
    };
    try {
      this.state.activeWorkoutMinutes = minutes;
      this.state.activeSimpleRoundSelectionGroupIds = priorRounds
        .filter(r => r.id === getSelectionKey(r) && locked.has(getSelectionKey(r)))
        .map(getSelectionKey);
      this.state.activeModifierRetainedSelectionGroupIds = [...protectedGroups];
      let applied = false;
      let lastError;
      const maximumCount = Math.min(freeMinutes, candidates.length, 63 - protectedGroups.size);
      for (let count = maximumCount; count >= (freeMinutes > 0 ? 1 : 0); count--) {
        this.state.selectedExerciseIds = { ...before.selectedExerciseIds };
        this.state.activeSetCountsBySelectionGroupId = { ...before.activeSetCountsBySelectionGroupId };
        this.state.activeExtraSetSelectionGroupIds = [...before.activeExtraSetSelectionGroupIds];
        this.state.activeSelectionGroupOrder = [];
        const groups = [...prefix.flatMap(p => p.coveredGroups), ...candidates.slice(0, count)];
        this.state.activeDurationSelectionGroupIds = [...new Set(groups.map(g => g.id))];
        try {
          const lineup = this.chooseBestDistinctLineup(groups, this.state.activeWorkoutModifiers, {
            currentExerciseIds: lockedExercises,
            allowSavedSelectionException: true,
            modifierTransitionProtectedGroupIds: protectedGroups,
            reservedRepeatMinutes: protectedCount - prefix.reduce((sum, p) => sum + p.root.sequenceBlocks.length, 0),
          });
          this.applyDistinctLineup(groups, lineup, false);
          this.rebalanceNewExercisesByMuscleBalance(locked);
          this.setEditedSelectionOrder(lockedIds);
          this.applyLongWorkoutAllocation(this.chooseLongWorkoutAllocation(locked));
          this.reconcileLineupWithScheduledPhases(locked);
          if (JSON.stringify(priorRounds.slice(0, protectedCount)) !==
              JSON.stringify(this.getActiveGroups().slice(0, protectedCount)) ||
              this.getNextGroup()?.id !== current.id) {
            throw new Error("A duration edit changed protected workout blocks.");
          }
          applied = true;
          break;
        } catch (error) { lastError ??= error; }
      }
      if (!applied) throw new Error("The new duration could not preserve this workout.", { cause: lastError });
      this.state.lastWorkoutMinutes = minutes;
      const session = this.ensureActiveWorkoutSession(true);
      session.workoutMinutes = minutes;
      session.durationChanges.push({
        changedAtUnixMilliseconds: this.getCurrentUnixTimeMilliseconds(), previousMinutes: before.activeWorkoutMinutes,
        newMinutes: minutes, plannedSelections: this.createCurrentSelectionSnapshots(session),
      });
    } catch (error) { Object.assign(this.state, before); throw error; }
  }

  setEditedSelectionOrder(prefixOrder) {
    this.state.activeSelectionGroupOrder = [...prefixOrder,
      ...this.getSelectedSequencePlacements()
        .filter(p => !prefixOrder.includes(p.anchor.id))
        .sort((a, b) => getMuscularDemandSchedulePriority(getSequenceMuscularDemand(a.root, this.exercisesById)) -
          getMuscularDemandSchedulePriority(getSequenceMuscularDemand(b.root, this.exercisesById)) || a.anchor.order - b.anchor.order)
        .map(p => p.anchor.id)];
  }

  reconfigureActiveWorkout(modifiers, currentWorkoutGroupId) {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes) ||
        this.state.workoutCompleted || this.state.completionAcknowledged) {
      throw new Error("Only an in-progress workout can be reconfigured.");
    }
    if (typeof currentWorkoutGroupId !== "string" ||
        currentWorkoutGroupId.length === 0) {
      throw new TypeError("A current workout block is required.");
    }

    const priorActiveRounds = this.getActiveGroups();
    const currentRound = priorActiveRounds.find((round) =>
      round.id === currentWorkoutGroupId);
    if (!currentRound || this.getNextGroup()?.id !== currentRound.id) {
      throw new Error("Only the currently displayed workout block can be replanned.");
    }

    const previousModifiers = this.state.activeWorkoutModifiers;
    const previousLastModifiers = this.state.lastWorkoutModifiers;
    const previousIsLightDay = this.state.activeWorkoutIsLightDay;
    modifiers = normalizeWorkoutModifiers(modifiers);
    if (this.isAutomaticLightDayDue()) {
      modifiers |= WORKOUT_MODIFIERS.Light;
    }
    const targetIsLightDay =
      (modifiers & WORKOUT_MODIFIERS.Light) !== 0;
    if (modifiers === previousModifiers) {
      this.state.lastWorkoutModifiers = getPersistentSetupModifiers(modifiers);
      this.state.activeWorkoutIsLightDay = targetIsLightDay;
      return;
    }

    const priorPlacements = this.getSelectedSequencePlacements();
    const priorOrderedPlacements = this.getScheduleOrderedPlacements(
      priorPlacements,
    );
    const currentPlacement = priorPlacements.find((placement) =>
      placement.anchor.id === getSelectionKey(currentRound));
    if (!currentPlacement) {
      throw new Error("The current atomic selection could not be resolved.");
    }
    const preserveCompletedCurrentSelection =
      this.getPendingRestGroup()?.id === currentRound.id;
    const lockedSelectionGroupIds = new Set(priorActiveRounds
      .filter((round) => this.state.outcomes[round.id] !== undefined)
      .map((round) => getSelectionKey(round)));
    if (preserveCompletedCurrentSelection) {
      lockedSelectionGroupIds.add(getSelectionKey(currentRound));
    } else {
      // Choose the unfinished current slot normally for the target profile.
      // Newly enabled equipment preferences can therefore restore their own
      // best selection just as restrictive changes can replace invalid work.
      lockedSelectionGroupIds.delete(getSelectionKey(currentRound));
    }
    const lockedPlacements = priorPlacements.filter((placement) =>
      lockedSelectionGroupIds.has(placement.anchor.id));
    const lockedExerciseIdsByGroup = new Map(lockedPlacements.flatMap(
      (placement) => placement.coveredGroups.map((group) =>
        [group.id, placement.root.id]),
    ));
    const lockedSetCountsBySelectionGroupId = new Map(lockedPlacements.map(
      (placement) => [
        placement.anchor.id,
        this.state.activeSetCountsBySelectionGroupId[placement.anchor.id] ?? 1,
      ],
    ));
    const protectedBaseGroupIds = new Set(lockedExerciseIdsByGroup.keys());
    const selectionGroups = this.getSelectionGroups(
      modifiers,
      protectedBaseGroupIds,
    );
    const retainedUnavailableSelectionGroupIds = selectionGroups
      .filter((group) => !isSelectionGroupAvailable(group, modifiers))
      .map((group) => group.id);
    const currentSelectionGroupAvailable = selectionGroups.some((group) =>
      group.id === getSelectionKey(currentRound));
    const replannedLineup = this.chooseBestDistinctLineup(
      selectionGroups,
      modifiers,
      {
        currentExerciseIds: lockedExerciseIdsByGroup,
        allowSavedSelectionException: true,
        modifierTransitionProtectedGroupIds: protectedBaseGroupIds,
        reservedRepeatMinutes: lockedPlacements.reduce((sum, p) => sum +
          (lockedSetCountsBySelectionGroupId.get(p.anchor.id) - 1) * p.root.sequenceBlocks.length, 0),
      },
    );

    const selectedExerciseIdsBefore = { ...this.state.selectedExerciseIds };
    const setCountsBefore = { ...this.state.activeSetCountsBySelectionGroupId };
    const extraSetGroupsBefore = [
      ...this.state.activeExtraSetSelectionGroupIds,
    ];
    const selectionOrderBefore = [...this.state.activeSelectionGroupOrder];
    const retainedSelectionGroupsBefore = [
      ...this.state.activeModifierRetainedSelectionGroupIds,
    ];
    const outcomesBefore = { ...this.state.outcomes };
    const protectedSelectionBefore =
      this.state.activeModifierProtectedSelectionGroupId;
    const pendingMovementGroupBefore = this.state.pendingMovementGroupId;
    const pendingMovementRemainingBefore =
      this.state.pendingMovementMillisecondsRemaining;
    const pendingMovementEndsAtBefore =
      this.state.pendingMovementEndsAtUnixMilliseconds;
    const pendingMovementPausedBefore =
      this.state.pendingMovementPausedByUser;
    try {
      this.state.lastWorkoutModifiers = getPersistentSetupModifiers(modifiers);
      this.state.activeWorkoutModifiers = modifiers;
      this.state.activeWorkoutIsLightDay = targetIsLightDay;
      this.state.activeModifierRetainedSelectionGroupIds =
        retainedUnavailableSelectionGroupIds;
      this.state.activeModifierProtectedSelectionGroupId =
        preserveCompletedCurrentSelection
          ? getSelectionKey(currentRound)
          : null;
      this.applyDistinctLineup(selectionGroups, replannedLineup, false);
      this.updateSelectionOrderAfterReconfiguration(priorOrderedPlacements);
      this.rebalanceNewExercisesByMuscleBalance(lockedSelectionGroupIds);
      this.updateSelectionOrderAfterReconfiguration(priorOrderedPlacements);
      this.applyLongWorkoutAllocation(this.chooseLongWorkoutAllocation(
        lockedSelectionGroupIds,
      ));

      const replannedRounds = this.getActiveGroups();
      const replannedCurrentPlacement = this.getSelectedSequencePlacements()
        .find((placement) => placement.coveredGroups.some((group) =>
          group.id === getSelectionKey(currentRound)));
      const currentSelectionChanged =
        !replannedCurrentPlacement ||
        replannedCurrentPlacement.root.id !== currentPlacement.root.id;
      if (currentSelectionChanged) {
        // Position IDs cannot transfer completed work to a new exercise.
        // The original movement's actual work remains in the session log.
        for (const priorRound of priorActiveRounds.filter((round) =>
          getSelectionKey(round) === getSelectionKey(currentRound))) {
          delete this.state.outcomes[priorRound.id];
        }

        // Partial time belonged to the exercise that was removed. Its
        // replacement returns in Ready state with a full timer and no
        // score/Keep mutation.
        this.clearPendingMovement();
        this.state.activeModifierProtectedSelectionGroupId = null;
      }

      const replannedNextRound = this.getNextGroup();
      const changedLockedSelection = [...lockedExerciseIdsByGroup].some(
        ([groupId, exerciseId]) => this.state.selectedExerciseIds[
          this.getSelectionStorageKey(
            groupId,
            this.state.activeWorkoutModifiers,
          )
        ] !== exerciseId,
      );
      const changedLockedSetCount =
        [...lockedSetCountsBySelectionGroupId].some(
          ([selectionGroupId, setCount]) =>
            (this.state.activeSetCountsBySelectionGroupId[
              selectionGroupId
            ] ?? 1) !== setCount,
        );
      if (changedLockedSelection || changedLockedSetCount ||
          Object.keys(this.state.outcomes).some((outcomeGroupId) =>
          !replannedRounds.some((round) => round.id === outcomeGroupId)) ||
          (preserveCompletedCurrentSelection &&
            replannedNextRound?.id !== currentRound.id) ||
          (!preserveCompletedCurrentSelection &&
            currentSelectionGroupAvailable &&
            (!replannedNextRound ||
              getSelectionKey(replannedNextRound) !== getSelectionKey(currentRound))) ||
          (!currentSelectionChanged &&
            replannedNextRound?.id !== currentRound.id) ||
          (!currentSelectionChanged && pendingMovementGroupBefore &&
            this.state.pendingMovementGroupId !== pendingMovementGroupBefore) ||
          (preserveCompletedCurrentSelection &&
            this.state.pendingMovementGroupId &&
            this.state.pendingMovementGroupId !== currentRound.id) ||
          (preserveCompletedCurrentSelection &&
            this.state.pendingRestGroupId &&
            this.state.pendingRestGroupId !== currentRound.id) ||
          (currentSelectionChanged && this.state.pendingMovementGroupId)) {
        throw new Error(
          "The modifier change could not preserve completed work or " +
            "replan the current exercise safely.",
        );
      }

      const session = this.ensureActiveWorkoutSession(true);
      session.modifierChanges.push({
        changedAtUnixMilliseconds: this.getCurrentUnixTimeMilliseconds(),
        previousModifiers,
        newModifiers: modifiers,
        protectedSelectionGroupId: preserveCompletedCurrentSelection
          ? getSelectionKey(currentRound)
          : "",
        plannedSelections: this.createCurrentSelectionSnapshots(session),
      });
    } catch (error) {
      this.state.selectedExerciseIds = selectedExerciseIdsBefore;
      this.state.activeSetCountsBySelectionGroupId = setCountsBefore;
      this.state.activeExtraSetSelectionGroupIds = extraSetGroupsBefore;
      this.state.activeSelectionGroupOrder = selectionOrderBefore;
      this.state.activeModifierRetainedSelectionGroupIds =
        retainedSelectionGroupsBefore;
      this.state.outcomes = outcomesBefore;
      this.state.activeModifierProtectedSelectionGroupId =
        protectedSelectionBefore;
      this.state.pendingMovementGroupId = pendingMovementGroupBefore;
      this.state.pendingMovementMillisecondsRemaining =
        pendingMovementRemainingBefore;
      this.state.pendingMovementEndsAtUnixMilliseconds =
        pendingMovementEndsAtBefore;
      this.state.pendingMovementPausedByUser = pendingMovementPausedBefore;
      this.state.activeWorkoutModifiers = previousModifiers;
      this.state.lastWorkoutModifiers = previousLastModifiers;
      this.state.activeWorkoutIsLightDay = previousIsLightDay;
      throw error;
    }
  }

  getActiveGroups() {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      return [];
    }
    return this.createActiveWorkoutSchedule(this.getEffectiveSetCounts());
  }

  getSelectedSequencePlacements() {
    const roots = new Map(this.getSelectionGroups().map((group) => [
      group.id,
      this.exercisesById.get(this.state.selectedExerciseIds[
        this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
      ]),
    ]));
    return getSelectedSequencePlacements(
      this.state.activeWorkoutMinutes,
      roots,
      this.exercisesById,
      (root, group) =>
        (this.pendingRestMatchesSelectionGroup(group.id) ||
          Object.prototype.hasOwnProperty.call(this.state.outcomes, group.id)) &&
        this.getSequenceExercises(root).every((member) =>
          this.isCompatibleWithModifiers(member, this.state.activeWorkoutModifiers) &&
          this.isAssignedToGroup(member, group)),
      this.getSelectionGroups(),
    );
  }

  getScheduleOrderedPlacements(
    placements = this.getSelectedSequencePlacements(),
  ) {
    const frozenSelectionGroupIds =
      this.state.activeSelectionGroupOrder.length > 0
        ? this.state.activeSelectionGroupOrder
        : this.state.activeWorkoutSession
          ?.initialSelections
          ?.map((selection) => selection.selectionGroupId) ?? [];
    return orderSelectedSequencePlacementsForSchedule(
      placements,
      this.exercisesById,
      frozenSelectionGroupIds,
    );
  }

  updateSelectionOrderAfterReconfiguration(priorOrderedPlacements) {
    const priorRankByWorkoutGroupId = new Map(priorOrderedPlacements.flatMap(
      (placement, rank) => placement.coveredGroups.map((group) =>
        [group.id, rank]),
    ));
    this.state.activeSelectionGroupOrder = this.getSelectedSequencePlacements()
      .sort((left, right) => {
        const leftRank = Math.min(...left.coveredGroups.map((group) =>
          priorRankByWorkoutGroupId.get(group.id) ?? Number.MAX_SAFE_INTEGER));
        const rightRank = Math.min(...right.coveredGroups.map((group) =>
          priorRankByWorkoutGroupId.get(group.id) ?? Number.MAX_SAFE_INTEGER));
        return leftRank - rightRank || left.anchor.order - right.anchor.order;
      })
      .map((placement) => placement.anchor.id);
  }

  createActiveWorkoutSchedule(setCountsBySelectionGroupId) {
    const roots = new Map(this.getSelectionGroups().map((group) => [
      group.id,
      this.exercisesById.get(this.state.selectedExerciseIds[
        this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
      ]),
    ]));
    return createWorkoutSchedule(
      this.state.activeWorkoutMinutes,
      roots,
      setCountsBySelectionGroupId,
      this.exercisesById,
      (root, group) =>
        (this.pendingRestMatchesSelectionGroup(group.id) ||
          Object.prototype.hasOwnProperty.call(this.state.outcomes, group.id)) &&
        this.getSequenceExercises(root).every((member) =>
          this.isCompatibleWithModifiers(member, this.state.activeWorkoutModifiers) &&
          this.isAssignedToGroup(member, group)),
      this.state.activeSelectionGroupOrder.length > 0
        ? this.state.activeSelectionGroupOrder
        : this.state.activeWorkoutSession?.initialSelections?.map((selection) =>
          selection.selectionGroupId) ?? [],
      this.getSelectionGroups(),
      this.state.activeDurationSelectionGroupIds === null
        ? null : this.state.activeSimpleRoundSelectionGroupIds,
    );
  }

  getSelectionGroups(
    modifiers = this.state.activeWorkoutModifiers,
    retainedSelectionGroupIds = this.state.activeModifierRetainedSelectionGroupIds,
  ) {
    const retained = new Set(retainedSelectionGroupIds ?? []);
    return SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)
      ? (this.state.activeDurationSelectionGroupIds?.map(id => ALL_GROUPS.get(id)) ?? getResolution(
          this.state.activeWorkoutMinutes > 30 ? 30 : this.state.activeWorkoutMinutes,
        ).groups).filter((group) => isSelectionGroupAvailable(
          group,
          modifiers,
        ) || retained.has(group.id))
      : [];
  }

  getRecoveryLightStatus(modifiers = this.state.activeWorkoutModifiers) {
    return evaluateRecoveryLightMode(
      this.exercises,
      modifiers,
      this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
      this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
      this.getCurrentUnixTimeMilliseconds(),
    );
  }

  getNextGroup() {
    return this.getActiveGroups().find((group) => this.state.outcomes[group.id] === undefined) ?? null;
  }

  isIntermediateSequenceBlock(group) {
    return this.getNextSequenceBlock(group) !== null;
  }

  getNextSequenceBlock(group) {
    const activeGroups = this.getActiveGroups();
    const groupIndex = activeGroups.findIndex((activeGroup) =>
      activeGroup.id === group?.id);
    if (groupIndex < 0 || groupIndex + 1 >= activeGroups.length) {
      return null;
    }
    const nextGroup = activeGroups[groupIndex + 1];
    return getSelectionKey(nextGroup) === getSelectionKey(group)
      ? nextGroup
      : null;
  }

  isSequenceContinuationBlock(group) {
    if (!group) {
      return false;
    }
    const activeGroups = this.getActiveGroups();
    const groupIndex = activeGroups.findIndex((activeGroup) =>
      activeGroup.id === group.id);
    return groupIndex > 0 &&
      getSelectionKey(activeGroups[groupIndex - 1]) === getSelectionKey(group);
  }

  canShuffleNextExercise(group) {
    return this.getNextGroup()?.id === group.id &&
      !this.isSequenceContinuationBlock(group) &&
      this.getCompatibleShuffleCandidates(group).length > 0;
  }

  shuffleNextExercise(group) {
    if (this.getNextGroup()?.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (this.isSequenceContinuationBlock(group)) {
      return null;
    }

    const candidates = this.getCompatibleShuffleCandidates(group);
    if (candidates.length === 0) {
      return null;
    }

    let replacementCandidates = candidates;
    if ((this.state.activeWorkoutModifiers & WORKOUT_MODIFIERS.Light) !== 0) {
      const lightCandidates = candidates.filter((candidate) =>
        this.isDemandZeroSequence(candidate.exercise));
      if (lightCandidates.length > 0) {
        const phase = this.getExercisePhase(group);
        const highestLightScore = Math.max(...lightCandidates.map((candidate) =>
          this.getSelectionScore(candidate.exercise, phase)));
        replacementCandidates = lightCandidates.filter((candidate) =>
          this.getSelectionScore(candidate.exercise, phase) ===
            highestLightScore);
      }
    }

    const rejectedExercise = this.getSelectedExercise(group);
    const rejectedRoot = this.getSequenceRoot(rejectedExercise);
    const scoreUpdates = this.getSequenceExercises(rejectedRoot);
    const selectionGroupId = getSelectionKey(group);
    const rejectedSelectionScore = this.getSelectionScore(
      rejectedRoot,
      this.getExercisePhase(group),
    );

    this.shuffle(replacementCandidates);
    const selected = replacementCandidates[0];

    for (const coveredGroup of selected.coveredGroups) {
      this.state.selectedExerciseIds[this.getSelectionStorageKey(
        coveredGroup.id,
        this.state.activeWorkoutModifiers,
      )] = selected.exercise.id;
    }
    this.recordWorkoutSelectionChange(
      selectionGroupId,
      this.getExercisePhase(group),
      rejectedRoot,
      rejectedSelectionScore,
      selected.exercise,
    );
    this.applyShuffleRejection(
      selectionGroupId,
      this.getExercisePhase(group),
      rejectedRoot,
      scoreUpdates,
    );
    this.applyLongWorkoutAllocation(selected.allocation);
    if (this.state.activeModifierProtectedSelectionGroupId ===
        selectionGroupId) {
      this.state.activeModifierProtectedSelectionGroupId = null;
    }
    return {
      rejectedExercise,
      replacementExercise: selected.exercise,
      scoreUpdates,
    };
  }

  applyShuffleRejection(selectionGroupId, phase, rejectedRoot, exercises) {
    const rejectedExerciseIds = new Set(exercises.map((exercise) => exercise.id));
    this.downvoteSequenceInPhase(phase, rejectedRoot);
    this.state.nextWorkoutExcludedExerciseIds = [...new Set([
      ...this.state.nextWorkoutExcludedExerciseIds,
      ...rejectedExerciseIds,
    ])];
    this.removeSavedSequenceCopiesForSlot(selectionGroupId, rejectedRoot.id);
  }

  getSelectedExercise(group) {
    if (Number.isInteger(group.exerciseOverrideId) && group.exerciseOverrideId > 0) {
      const overrideExercise = this.exercisesById.get(group.exerciseOverrideId);
      if (!overrideExercise || !this.isSequenceOverrideValid(
        overrideExercise,
        group,
        this.state.activeWorkoutModifiers,
      )) {
        throw new Error(`The linked sequence block for ${group.displayName} is unavailable.`);
      }
      return overrideExercise;
    }
    const exercise = this.exercisesById.get(
      this.state.selectedExerciseIds[this.getSelectionStorageKey(
        getSelectionKey(group),
        this.state.activeWorkoutModifiers,
      )],
    );
    if (!exercise || !this.isSavedSelectionValid(
      exercise,
      group,
      this.state.activeWorkoutModifiers,
    )) {
      throw new Error(`No eligible exercise selected for ${group.displayName}.`);
    }
    return exercise;
  }

  tryGetSelectedExercise(group) {
    try {
      return this.getSelectedExercise(group);
    } catch {
      return null;
    }
  }

  getCompatibleShuffleCandidates(currentRound) {
    const activeRounds = this.getActiveGroups();
    const selectionGroupId = getSelectionKey(currentRound);
    if (
      activeRounds.some((round) =>
        getSelectionKey(round) === selectionGroupId &&
        this.state.outcomes[round.id] !== undefined) ||
      !this.isLongWorkoutAllocationValid()
    ) {
      return [];
    }

    let currentPlacement;
    try {
      currentPlacement = this.getSelectedSequencePlacements().find((placement) =>
        placement.anchor.id === selectionGroupId);
    } catch {
      return [];
    }
    if (!currentPlacement) {
      return [];
    }
    const currentExercise = currentPlacement.root;
    const currentExerciseId = currentExercise.id;
    const coveredGroupIds = new Set(currentPlacement.coveredGroups.map((group) =>
      group.id));

    const rejectedExerciseIds = new Set(this.getSequenceExercises(currentExercise)
      .map((exercise) => exercise.id));
    const startedSelectionGroupIds = new Set(activeRounds
      .filter((round) => this.state.outcomes[round.id] !== undefined)
      .map(getSelectionKey));

    const unavailableExerciseIds = new Set(this.getSelectionGroups()
      .filter((group) => !coveredGroupIds.has(group.id))
      .map((group) => this.state.selectedExerciseIds[this.getSelectionStorageKey(
        group.id,
        this.state.activeWorkoutModifiers,
      )])
      .filter((exerciseId) => Number.isInteger(exerciseId) && exerciseId > 0));
    const unavailableMovementIds = new Set([
      ...[...unavailableExerciseIds]
        .map((exerciseId) => this.exercisesById.get(exerciseId))
        .filter(Boolean)
        .map(getSessionMovementId),
      getSessionMovementId(currentExercise),
    ]);
    const candidates = [];
    for (const exercise of this.exercises) {
      if (
        exercise.id === currentExerciseId ||
        this.state.nextWorkoutExcludedExerciseIds.includes(exercise.id) ||
        unavailableExerciseIds.has(exercise.id) ||
        unavailableMovementIds.has(getSessionMovementId(exercise)) ||
        !this.getSequencePlacementOptions(exercise, this.getSelectionGroups())
          .some((option) => sameStringSet(
            option.map((group) => group.id),
            coveredGroupIds,
          )) ||
        !this.isWorkoutSelectionCandidate(
          exercise,
          currentPlacement.anchor,
          this.state.activeWorkoutModifiers,
        )
      ) {
        continue;
      }
      const allocation = this.tryGetCompatibleShuffleAllocation(
        currentPlacement.coveredGroups,
        exercise,
        startedSelectionGroupIds,
        rejectedExerciseIds,
      );
      if (allocation) {
        candidates.push({
          exercise,
          coveredGroups: currentPlacement.coveredGroups,
          allocation,
        });
      }
    }
    return candidates;
  }

  tryGetCompatibleShuffleAllocation(
    coveredGroups,
    candidate,
    startedSelectionGroupIds,
    rejectedExerciseIds,
  ) {
    const previousExerciseIds = new Map(coveredGroups.map((group) => {
      const selectionStorageKey = this.getSelectionStorageKey(
        group.id,
        this.state.activeWorkoutModifiers,
      );
      return [selectionStorageKey, this.state.selectedExerciseIds[selectionStorageKey]];
    }));
    const selectionGroupId = [...coveredGroups]
      .sort((left, right) => left.order - right.order)[0].id;
    const previousSlotKeeps = this.getKeptRootIdsForSlot(selectionGroupId);
    for (const selectionStorageKey of previousExerciseIds.keys()) {
      this.state.selectedExerciseIds[selectionStorageKey] = candidate.id;
    }
    this.removeSequenceKeep(
      selectionGroupId,
      this.exercisesById.get([...rejectedExerciseIds][0]),
    );
    try {
      return this.chooseLongWorkoutAllocation(startedSelectionGroupIds);
    } catch {
      return null;
    } finally {
      for (const [selectionStorageKey, previousExerciseId] of previousExerciseIds) {
        this.state.selectedExerciseIds[selectionStorageKey] = previousExerciseId;
      }
      if (previousSlotKeeps.size === 0) {
        delete this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId];
      } else {
        this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
          [...previousSlotKeeps];
      }
    }
  }

  beginMovement(group, millisecondsRemaining, endsAtUnixMilliseconds) {
    const normalizedRemaining = Math.trunc(millisecondsRemaining);
    const normalizedDeadline = Math.trunc(endsAtUnixMilliseconds);
    this.validatePendingMovement(
      group,
      normalizedRemaining,
      normalizedDeadline,
      false,
    );
    this.clearPendingRest();
    this.state.pendingMovementGroupId = group.id;
    this.state.pendingMovementMillisecondsRemaining = normalizedRemaining;
    this.state.pendingMovementEndsAtUnixMilliseconds = normalizedDeadline;
    this.state.pendingMovementPausedByUser = false;
  }

  pauseMovement(group, millisecondsRemaining, pausedByUser) {
    const normalizedRemaining = Math.trunc(millisecondsRemaining);
    this.validatePendingMovement(group, normalizedRemaining, 0, true);
    this.clearPendingRest();
    this.state.pendingMovementGroupId = group.id;
    this.state.pendingMovementMillisecondsRemaining = normalizedRemaining;
    this.state.pendingMovementEndsAtUnixMilliseconds = 0;
    this.state.pendingMovementPausedByUser = pausedByUser === true;
  }

  getPendingMovementGroup() {
    const pendingGroup = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingMovementGroupId,
    );
    if (!pendingGroup ||
        this.getNextGroup()?.id !== pendingGroup.id ||
        this.state.outcomes[pendingGroup.id] !== undefined ||
        !Number.isSafeInteger(this.state.pendingMovementMillisecondsRemaining) ||
        this.state.pendingMovementMillisecondsRemaining <= 0 ||
        this.state.pendingMovementMillisecondsRemaining >
          getMovementCountdownDurationMs(pendingGroup) ||
        !Number.isSafeInteger(this.state.pendingMovementEndsAtUnixMilliseconds) ||
        this.state.pendingMovementEndsAtUnixMilliseconds < 0) {
      return null;
    }

    try {
      this.getSelectedExercise(pendingGroup);
    } catch {
      return null;
    }
    return pendingGroup;
  }

  getPendingRestGroup() {
    const pendingGroup = this.getValidPendingRestGroup();
    return pendingGroup && this.getNextGroup()?.id === pendingGroup.id
      ? pendingGroup
      : null;
  }

  getValidPendingRestGroup() {
    const pendingGroup = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingRestGroupId,
    );
    if (!pendingGroup ||
        !this.hasValidPendingRestTiming() ||
        this.state.outcomes[pendingGroup.id] !== undefined) {
      return null;
    }

    try {
      // The selected root and exact sequence block are validated together.
      // An integration member need not independently target this fine slot.
      this.getSelectedExercise(pendingGroup);
      return pendingGroup;
    } catch {
      return null;
    }
  }

  hasValidPendingRestTiming() {
    return this.state.pendingRestPausedByUser
      ? this.state.pendingRestEndsAtUnixMilliseconds === 0 &&
          Number.isSafeInteger(this.state.pendingRestMillisecondsRemaining) &&
          this.state.pendingRestMillisecondsRemaining > 0 &&
          this.state.pendingRestMillisecondsRemaining <= REST_DURATION_MS
      : Number.isSafeInteger(this.state.pendingRestEndsAtUnixMilliseconds) &&
          this.state.pendingRestEndsAtUnixMilliseconds > 0 &&
          this.state.pendingRestMillisecondsRemaining === 0;
  }

  getPendingMovementMillisecondsRemaining(nowUnixMilliseconds) {
    if (!Number.isSafeInteger(nowUnixMilliseconds) || nowUnixMilliseconds <= 0) {
      throw new RangeError("Current time must be positive Unix milliseconds.");
    }
    const pendingGroup = this.getPendingMovementGroup();
    if (!pendingGroup) {
      return 0;
    }
    const storedRemaining = this.state.pendingMovementMillisecondsRemaining;
    const remaining = this.state.pendingMovementEndsAtUnixMilliseconds >
        nowUnixMilliseconds
      ? Math.min(
          storedRemaining,
          this.state.pendingMovementEndsAtUnixMilliseconds - nowUnixMilliseconds,
        )
      : storedRemaining;
    return Math.max(
      1,
      Math.min(remaining, getMovementCountdownDurationMs(pendingGroup)),
    );
  }

  clearPendingMovement() {
    this.state.pendingMovementGroupId = null;
    this.state.pendingMovementMillisecondsRemaining = 0;
    this.state.pendingMovementEndsAtUnixMilliseconds = 0;
    this.state.pendingMovementPausedByUser = false;
  }

  validatePendingMovement(
    group,
    millisecondsRemaining,
    endsAtUnixMilliseconds,
    allowPausedDeadline,
  ) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (!Number.isSafeInteger(millisecondsRemaining) ||
        millisecondsRemaining <= 0 ||
        millisecondsRemaining > getMovementCountdownDurationMs(group)) {
      throw new RangeError("Movement time remaining is invalid.");
    }
    if ((!allowPausedDeadline &&
          (!Number.isSafeInteger(endsAtUnixMilliseconds) ||
            endsAtUnixMilliseconds <= 0)) ||
        (allowPausedDeadline && endsAtUnixMilliseconds !== 0)) {
      throw new RangeError("Movement deadline is invalid.");
    }
    this.getSelectedExercise(group);
  }

  beginRest(group, endsAtUnixMilliseconds) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (!Number.isSafeInteger(endsAtUnixMilliseconds) || endsAtUnixMilliseconds <= 0) {
      throw new RangeError("Rest deadline must be positive Unix milliseconds.");
    }
    this.clearPendingMovement();
    this.state.pendingRestGroupId = group.id;
    this.state.pendingRestEndsAtUnixMilliseconds = Math.trunc(endsAtUnixMilliseconds);
    this.state.pendingRestMillisecondsRemaining = 0;
    this.state.pendingRestPausedByUser = false;
    this.state.pendingRestKept = false;
    const exercise = this.getSelectedExercise(group);
    const completedAtUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    this.recordCompletedWorkoutBlock(group, completedAtUnixMilliseconds);
    if (exercise.muscularDemand === MODERATE_MUSCULAR_DEMAND ||
        exercise.muscularDemand === HARD_MUSCULAR_DEMAND) {
      const primaryMuscle = exercise.primaryCanonicalGroup;
      this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[primaryMuscle] =
        Math.max(
          completedAtUnixMilliseconds,
          getLastMeaningfulWorkUnixMilliseconds(
            this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
            primaryMuscle,
          ),
        );
      if (exercise.muscularDemand === HARD_MUSCULAR_DEMAND) {
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle[primaryMuscle] =
          Math.max(
            completedAtUnixMilliseconds,
            getLastHardWorkUnixMilliseconds(
              this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
              primaryMuscle,
            ),
          );
      }
    }
  }

  keepPendingRest() {
    const pendingGroup = this.getPendingRestGroup();
    if (!pendingGroup ||
        this.isIntermediateSequenceBlock(pendingGroup)) {
      return false;
    }
    this.state.pendingRestKept = true;
    return true;
  }

  pauseRest(group, millisecondsRemaining) {
    const pendingGroup = this.getPendingRestGroup();
    if (!pendingGroup || pendingGroup.id !== group.id ||
        this.state.pendingRestPausedByUser) {
      throw new Error(`${group.displayName} does not have a running rest.`);
    }
    if (!Number.isSafeInteger(millisecondsRemaining) ||
        millisecondsRemaining <= 0 ||
        millisecondsRemaining > REST_DURATION_MS) {
      throw new RangeError("Rest time remaining is invalid.");
    }

    this.state.pendingRestEndsAtUnixMilliseconds = 0;
    this.state.pendingRestMillisecondsRemaining = millisecondsRemaining;
    this.state.pendingRestPausedByUser = true;
  }

  resumeRest(group, endsAtUnixMilliseconds) {
    const pendingGroup = this.getPendingRestGroup();
    if (!pendingGroup || pendingGroup.id !== group.id ||
        !this.state.pendingRestPausedByUser) {
      throw new Error(`${group.displayName} does not have a paused rest.`);
    }
    if (!Number.isSafeInteger(endsAtUnixMilliseconds) ||
        endsAtUnixMilliseconds <= 0) {
      throw new RangeError("Rest deadline must be positive Unix milliseconds.");
    }

    this.state.pendingRestEndsAtUnixMilliseconds = endsAtUnixMilliseconds;
    this.state.pendingRestMillisecondsRemaining = 0;
    this.state.pendingRestPausedByUser = false;
  }

  getPendingRestMillisecondsRemaining(nowUnixMilliseconds) {
    if (!Number.isSafeInteger(nowUnixMilliseconds) || nowUnixMilliseconds <= 0) {
      throw new RangeError("Current time must be positive Unix milliseconds.");
    }
    if (!this.getPendingRestGroup()) {
      return 0;
    }

    const remaining = this.state.pendingRestPausedByUser
      ? this.state.pendingRestMillisecondsRemaining
      : this.state.pendingRestEndsAtUnixMilliseconds - nowUnixMilliseconds;
    return Math.max(0, Math.min(remaining, REST_DURATION_MS));
  }

  clearPendingRest() {
    this.state.pendingRestGroupId = null;
    this.state.pendingRestEndsAtUnixMilliseconds = 0;
    this.state.pendingRestMillisecondsRemaining = 0;
    this.state.pendingRestPausedByUser = false;
    this.state.pendingRestKept = false;
  }

  recordOutcome(group, keep) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (!isFinalSequenceRound(group)) {
      throw new Error("A sequence can only be rated after its final block.");
    }

    this.clearPendingMovement();
    return this.applySequenceOutcome(group, keep);
  }

  rejectCurrentSequence(group) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }

    const sequenceRounds = this.getActiveGroups()
      .filter((round) => getSelectionKey(round) === getSelectionKey(group))
      .sort((left, right) => left.order - right.order);
    const decisionRound = sequenceRounds.at(-1);
    for (const round of sequenceRounds) {
      if (round.id !== decisionRound.id) {
        this.state.outcomes[round.id] ??= "neutral";
      }
    }
    this.clearPendingMovement();
    this.clearPendingRest();
    return this.applySequenceOutcome(
      decisionRound,
      false,
      this.getExercisePhase(group),
    );
  }

  advanceSequence(group) {
    const nextGroup = this.getNextGroup();
    if (!nextGroup || nextGroup.id !== group.id) {
      throw new Error(`${group.displayName} is not the next workout group.`);
    }
    if (!this.isIntermediateSequenceBlock(group)) {
      throw new Error(`${group.displayName} is not an intermediate sequence block.`);
    }
    this.state.outcomes[group.id] = "neutral";
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
  }

  applySequenceOutcome(group, keep, feedbackPhase = null) {
    const exercise = this.getSelectedExercise(group);
    const root = this.getSequenceRoot(exercise);
    const selectionGroupId = getSelectionKey(group);
    const exercisePhase = feedbackPhase ?? this.getExercisePhase(group);
    const selectionScoreBeforeDecision = this.getSelectionScore(
      root,
      exercisePhase,
    );
    if (!keep) {
      this.downvoteSequenceInPhase(exercisePhase, root);
    }
    const decidedAtUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    this.recordWorkoutDecision(
      group,
      root,
      keep ? "tick" : "x",
      selectionScoreBeforeDecision,
      exercisePhase,
      decidedAtUnixMilliseconds,
    );
    this.state.outcomes[group.id] = keep ? "tick" : "x";
    if (this.state.activeModifierProtectedSelectionGroupId ===
          selectionGroupId &&
        this.getActiveGroups()
          .filter((activeGroup) => getSelectionKey(activeGroup) === selectionGroupId)
          .every((activeGroup) =>
            this.state.outcomes[activeGroup.id] !== undefined)) {
      this.state.activeModifierProtectedSelectionGroupId = null;
    }
    this.state.workoutCompleted = this.getActiveGroups().every(
      (activeGroup) => this.state.outcomes[activeGroup.id] !== undefined,
    );
    this.state.completionAcknowledged = false;
    if (this.state.workoutCompleted) {
      this.finalizeActiveWorkoutSession("Completed", decidedAtUnixMilliseconds);
    }
    return exercise;
  }

  acknowledgeCompletion() {
    if (!this.state.workoutCompleted) {
      throw new Error("Workout is not complete.");
    }
    this.state.completionAcknowledged = true;
    this.finalizeCurrentWorkout();
  }

  finishInterruptedWorkout() {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      this.finalizeActiveWorkoutSession("Interrupted");
      this.resetTransientState();
      return;
    }

    if (this.state.pendingRestGroupId) {
      const group = this.getActiveGroups().find(
        (candidate) => candidate.id === this.state.pendingRestGroupId,
      );
      if (group && this.state.outcomes[group.id] === undefined) {
        if (this.isIntermediateSequenceBlock(group)) {
          this.advanceSequence(group);
        } else {
          this.applySequenceOutcome(group, this.state.pendingRestKept);
        }
      }
      this.clearPendingRest();
    }
    this.finalizeCurrentWorkout();
  }

  finalizeCurrentWorkout() {
    // Closing a workout must not depend on finding a future lineup. The next
    // preparation uses its own duration, modifiers, phase scores and recovery.
    const activeGroups = this.getActiveGroups();
    const selectionGroups = this.getSelectionGroups();
    const rejectedSelections = new Map();
    for (const selectionGroup of selectionGroups) {
      const decisionRound = activeGroups
        .filter((round) => getSelectionKey(round) === selectionGroup.id)
        .sort((left, right) => left.order - right.order)
        .at(-1);
      const outcome = decisionRound
        ? this.state.outcomes[decisionRound.id]
        : undefined;
      if (outcome !== "tick" && outcome !== "x") {
        continue;
      }
      const root = this.exercisesById.get(this.state.selectedExerciseIds[
        this.getSelectionStorageKey(
          selectionGroup.id,
          this.state.activeWorkoutModifiers,
        )
      ]);
      if (!root) {
        continue;
      }
      if (outcome === "tick") {
        this.keepSequenceInSlot(selectionGroup.id, root);
      } else {
        rejectedSelections.set(selectionGroup.id, root.id);
      }
    }
    this.state.nextWorkoutExcludedExerciseIds = [];
    this.syncLegacyKeptExerciseIds();
    for (const [groupId, rootId] of rejectedSelections) {
      // Rejection is a phase-local downvote, not a compatibility ban. Clear
      // cached slot selections, but preserve Keeps and already-recorded scores.
      this.removeSavedSequenceCopiesForSlot(groupId, rootId);
    }

    this.finalizeActiveWorkoutSession(
      this.state.workoutCompleted ? "Completed" : "Interrupted",
    );
    this.resetTransientState();
  }

  repairActiveLineup(preserveCurrentSelections = true) {
    const selectionGroups = this.getSelectionGroups();
    let activeGroups = [];
    try {
      activeGroups = this.getActiveGroups();
    } catch {
      activeGroups = [];
    }
    const currentExerciseIds = new Map(
      selectionGroups
        .map((group) => [
          group.id,
          this.state.selectedExerciseIds[this.getSelectionStorageKey(
            group.id,
            this.state.activeWorkoutModifiers,
          )],
        ])
        .filter(([, exerciseId]) => exerciseId),
    );
    const repairedLineup = this.chooseBestDistinctLineup(
      selectionGroups,
      this.state.activeWorkoutModifiers,
      {
        currentExerciseIds,
        allowSavedSelectionException: preserveCurrentSelections,
      },
    );
    this.applyDistinctLineup(selectionGroups, repairedLineup, true, activeGroups);
  }

  normalizeActiveModifierTransitionProtection() {
    const protectedSelectionGroupId =
      this.state.activeModifierProtectedSelectionGroupId;
    if (!protectedSelectionGroupId ||
        this.pendingRestMatchesSelectionGroup(protectedSelectionGroupId)) {
      return;
    }

    const root = this.exercisesById.get(this.state.selectedExerciseIds[
      this.getSelectionStorageKey(
        protectedSelectionGroupId,
        this.state.activeWorkoutModifiers,
      )
    ]);
    const remainsCompatible = root &&
      this.getSequenceRoot(root).id === root.id &&
      this.getSequenceExercises(root).every((member) =>
        this.isCompatibleWithModifiers(
          member,
          this.state.activeWorkoutModifiers,
        ));
    // Current work is no longer privileged merely because it is compatible.
    // Remove the one-way exception persisted by older builds.
    this.state.activeModifierProtectedSelectionGroupId = null;
    if (!remainsCompatible) {
      this.clearPendingMovement();
    }
  }

  normalizeActiveModifierRetainedSelectionGroups() {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      this.state.activeModifierRetainedSelectionGroupIds = [];
      return;
    }

    const completedRootIds = new Set(
      this.state.activeWorkoutSession?.decisions
        ?.map((decision) => decision.rootExerciseId)
        .filter((rootExerciseId) =>
          Number.isInteger(rootExerciseId) && rootExerciseId > 0) ?? [],
    );
    const protectedSelectionGroupId =
      this.state.activeModifierProtectedSelectionGroupId;
    const validGroupIds = new Set(this.state.activeDurationSelectionGroupIds ?? getResolution(
      this.state.activeWorkoutMinutes > 30
        ? 30
        : this.state.activeWorkoutMinutes,
    ).groups.map((group) => group.id));
    this.state.activeModifierRetainedSelectionGroupIds =
      this.state.activeModifierRetainedSelectionGroupIds.filter((groupId) => {
        const selectedRootId = this.state.selectedExerciseIds[
          this.getSelectionStorageKey(
            groupId,
            this.state.activeWorkoutModifiers,
          )
        ];
        return validGroupIds.has(groupId) &&
          (completedRootIds.has(selectedRootId) ||
           (groupId === protectedSelectionGroupId &&
            this.pendingRestMatchesSelectionGroup(protectedSelectionGroupId)));
      });
  }

  carrySlotPreferencesForward() {
    if (Object.values(this.state.keptExerciseRootIdsBySelectionGroupId)
      .every((rootIds) => rootIds.length === 0)) {
      return;
    }

    const targetGroups = this.getSelectionGroups();
    const carriedKeepRootIdsBySelectionGroupId =
      this.buildCrossResolutionKeepPreferences(targetGroups);
    const currentExerciseIds = new Map(
      targetGroups
        .map((group) => [
          group.id,
          this.state.selectedExerciseIds[this.getSelectionStorageKey(
            group.id,
            this.state.activeWorkoutModifiers,
          )],
        ])
        .filter(([, exerciseId]) => exerciseId),
    );
    const carriedLineup = this.chooseBestDistinctLineup(
      targetGroups,
      this.state.activeWorkoutModifiers,
      {
        currentExerciseIds,
        carriedKeepRootIdsBySelectionGroupId,
      },
    );
    this.applyDistinctLineup(targetGroups, carriedLineup, false);

    for (const placement of this.getSelectedSequencePlacements()) {
      if (!carriedKeepRootIdsBySelectionGroupId.get(placement.anchor.id)
        ?.has(placement.root.id)) {
        continue;
      }
      this.keepSequenceInSlot(placement.anchor.id, placement.root);
    }
    this.syncLegacyKeptExerciseIds();
  }

  buildCrossResolutionKeepPreferences(targetGroups) {
    const targetGroupIds = new Set(targetGroups.map((group) => group.id));
    const rootsWithTargetResolutionKeeps = new Set(Object.entries(
      this.state.keptExerciseRootIdsBySelectionGroupId,
    ).filter(([selectionGroupId]) => targetGroupIds.has(selectionGroupId))
      .flatMap(([, rootIds]) => rootIds));
    const carried = new Map();
    for (const [sourceGroupId, keptRootIds] of Object.entries(
      this.state.keptExerciseRootIdsBySelectionGroupId,
    )) {
      const sourceGroup = ALL_GROUPS.get(sourceGroupId);
      if (targetGroupIds.has(sourceGroupId) || !sourceGroup) {
        continue;
      }
      for (const rootId of keptRootIds) {
        const root = this.exercisesById.get(rootId);
        if (rootsWithTargetResolutionKeeps.has(rootId) ||
            !root || this.getSequenceRoot(root).id !== rootId) {
          continue;
        }
        const sourceSelectionExercise = this.getSequenceSelectionExerciseForGroup(
          root,
          sourceGroup,
        );
        const targetPrimaryGroup = targetGroups.find((group) =>
          group.canonicalGroups.includes(
            sourceSelectionExercise.primaryCanonicalGroup,
          ));
        if (!targetPrimaryGroup) {
          continue;
        }
        const mappedPlacement = this.getSequencePlacementOptions(root, targetGroups)
          .filter((option) => option.some((group) =>
            group.id === targetPrimaryGroup.id))
          .sort((left, right) => right.length - left.length ||
            Math.min(...left.map((group) => group.order)) -
              Math.min(...right.map((group) => group.order)))[0];
        if (!mappedPlacement) {
          continue;
        }
        const targetAnchor = [...mappedPlacement]
          .sort((left, right) => left.order - right.order)[0];
        const targetKeeps = carried.get(targetAnchor.id) ?? new Set();
        targetKeeps.add(rootId);
        carried.set(targetAnchor.id, targetKeeps);
      }
    }
    return carried;
  }

  chooseBestDistinctLineup(
    groups,
    modifiers = this.state.activeWorkoutModifiers,
    {
      currentExerciseIds = new Map(),
      allowSavedSelectionException = false,
      carriedKeepRootIdsBySelectionGroupId = new Map(),
      modifierTransitionProtectedGroupIds = new Set(),
      scheduledPhaseByGroupId = new Map(),
      reservedRepeatMinutes = 0,
    } = {},
  ) {
    if (groups.length === 0) {
      return new Map();
    }

    const calculateIsAllowed = (exercise, group) => {
      if (this.isWorkoutSelectionCandidate(
        exercise,
        group,
        modifiers,
        groups,
      )) {
        return true;
      }
      if (modifierTransitionProtectedGroupIds.has(group.id) &&
          currentExerciseIds.get(group.id) === exercise.id) {
        return true;
      }
      return allowSavedSelectionException &&
        currentExerciseIds.get(group.id) === exercise.id &&
        this.isSavedSelectionValid(exercise, group, modifiers, groups);
    };
    const allowedGroupIdsByExerciseId = new Map();
    let candidates = [];
    for (const exercise of this.exercises) {
      if (exercise.sequenceBlocks.length === 0 ||
          this.getSequenceRoot(exercise).id !== exercise.id) {
        continue;
      }
      const allowedGroupIds = new Set(groups
        .filter((group) => calculateIsAllowed(exercise, group))
        .map((group) => group.id));
      if (allowedGroupIds.size === 0) {
        continue;
      }
      candidates.push(exercise);
      allowedGroupIdsByExerciseId.set(exercise.id, allowedGroupIds);
    }
    const isAllowed = (exercise, group) =>
      allowedGroupIdsByExerciseId.get(exercise.id)?.has(group.id) ?? false;
    const getSelectionPhase = (group) =>
      scheduledPhaseByGroupId.get(group.id) ??
        this.getProjectedSelectionPhase(group, groups.length);
    this.shuffle(candidates);
    const orderedScores = [...new Set(candidates.flatMap((exercise) => groups
      .filter((group) => isAllowed(exercise, group))
      .map((group) => this.getSelectionScore(
        exercise,
        getSelectionPhase(group),
      ))))]
      .sort((left, right) => left - right);
    const scoreRanks = new Map(orderedScores.map((score, rank) => [score, rank]));
    const highestScoreByGroup = new Map(groups.map((group) => {
      const allowedScores = candidates
        .filter((exercise) => isAllowed(exercise, group))
        .map((exercise) => this.getSelectionScore(
          exercise,
          getSelectionPhase(group),
        ));
      return [
        group.id,
        allowedScores.length > 0
          ? Math.max(...allowedScores)
          : Number.MIN_SAFE_INTEGER,
      ];
    }));
    const selectionTimeUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    const freshHardMuscleTimestamps = [...new Set(candidates
      .flatMap((exercise) => groups
        .filter((group) => isAllowed(exercise, group))
        .map((group) => this.getSequenceSelectionExerciseForGroup(
          exercise,
          group,
        )))
      .filter((exercise) =>
        exercise.muscularDemand === HARD_MUSCULAR_DEMAND &&
        !isPrimaryMuscleRecovering(
          this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
          exercise.primaryCanonicalGroup,
          selectionTimeUnixMilliseconds,
        ))
      .map((exercise) => getLastHardWorkUnixMilliseconds(
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
        exercise.primaryCanonicalGroup,
      )))].sort((left, right) => right - left);
    const freshHardMuscleRanks = new Map(
      freshHardMuscleTimestamps.map((timestamp, rank) => [timestamp, rank]),
    );
    const maximumCoverage = Math.max(...groups.map((group) => group.canonicalGroups.length));
    // These are exact lexicographic assignment dimensions, not hardness
    // points. BigInt keeps arbitrary saved-score histories lossless without
    // persisting any derived value.
    let totalLowerPriorityRange = BigInt(groups.length * maximumCoverage);
    const addPriorityDimension = (maximumValue) => {
      const weight = totalLowerPriorityRange + 1n;
      totalLowerPriorityRange +=
        BigInt(groups.length) * BigInt(maximumValue) * weight;
      return weight;
    };
    const primaryWeight = addPriorityDimension(1);
    const equipmentPreferenceWeight = addPriorityDimension(2);
    const currentSelectionWeight = addPriorityDimension(1);
    const hardMuscleAgeWeight = addPriorityDimension(
      Math.max(0, freshHardMuscleRanks.size - 1),
    );
    const moderateRecoveryAvoidanceWeight = addPriorityDimension(1);
    const hardRecoveryAvoidanceWeight = addPriorityDimension(1);
    const freshHardWeight = addPriorityDimension(1);
    const scoreWeight = addPriorityDimension(Math.max(0, orderedScores.length - 1));
    const keptExerciseWeight = addPriorityDimension(1);
    const hardOpportunityWeight = addPriorityDimension(1);
    const lightDayOpportunityWeight = addPriorityDimension(1);
    const preservedActiveSelectionWeight = allowSavedSelectionException
      ? totalLowerPriorityRange + 1n
      : 0n;

    const hasLightDayOpportunity = (exercise, preferenceSlot) => {
      return (modifiers & WORKOUT_MODIFIERS.Light) !== 0 &&
        this.isDemandZeroSequence(exercise);
    };

    const calculateUtility = (exercise, evaluationGroup, includeSlotPreference) => {
      const selectionExercise = this.getSequenceSelectionExerciseForGroup(
        exercise,
        evaluationGroup,
      );
      const hardRotationStatus = getHardRotationStatus(
        selectionExercise,
        evaluationGroup,
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      );
      const isRecoveringModerate = isModerateExerciseRecovering(
        selectionExercise,
        this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      );
      const hardMuscleAgeRank = hardRotationStatus === HARD_ROTATION_STATUS.FreshHard
        ? freshHardMuscleRanks.get(getLastHardWorkUnixMilliseconds(
            this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
            selectionExercise.primaryCanonicalGroup,
          )) ?? 0
        : 0;
      const isKept = includeSlotPreference &&
        (this.isSequenceKept(evaluationGroup.id, exercise) ||
         carriedKeepRootIdsBySelectionGroupId.get(evaluationGroup.id)
           ?.has(this.getSequenceRoot(exercise).id) === true);
      const phase = getSelectionPhase(evaluationGroup);
      const selectionScore = includeSlotPreference
        ? this.getSelectionScore(exercise, phase)
        : 0;
      const isDownvotedInPhase = includeSlotPreference &&
        this.getPhaseScoreAdjustment(exercise, phase) < 0;
      const hasHardOpportunity = includeSlotPreference &&
        hardRotationStatus === HARD_ROTATION_STATUS.FreshHard &&
        !isDownvotedInPhase &&
        (isKept || selectionScore === highestScoreByGroup.get(evaluationGroup.id));
      const hasContextualKeepPreference = includeSlotPreference && isKept &&
        !isDownvotedInPhase &&
        hardRotationStatus !== HARD_ROTATION_STATUS.RecoveringHard &&
        !isRecoveringModerate;
      const isCurrentSelection = includeSlotPreference &&
        currentExerciseIds.get(evaluationGroup.id) === exercise.id;
      return (allowSavedSelectionException && isCurrentSelection
        ? preservedActiveSelectionWeight
        : 0n) +
        (includeSlotPreference && hasLightDayOpportunity(exercise, evaluationGroup)
          ? lightDayOpportunityWeight
          : 0n) +
        (hasHardOpportunity ? hardOpportunityWeight : 0n) +
        (hasContextualKeepPreference ? keptExerciseWeight : 0n) +
        (includeSlotPreference
          ? BigInt(scoreRanks.get(selectionScore)) * scoreWeight
          : 0n) +
        (hardRotationStatus !== HARD_ROTATION_STATUS.RecoveringHard
          ? hardRecoveryAvoidanceWeight
          : 0n) +
        (!isRecoveringModerate ? moderateRecoveryAvoidanceWeight : 0n) +
        (hardRotationStatus === HARD_ROTATION_STATUS.FreshHard
          ? freshHardWeight
          : 0n) +
        BigInt(hardMuscleAgeRank) * hardMuscleAgeWeight +
        (isCurrentSelection ? currentSelectionWeight : 0n) +
        BigInt(getEquipmentPreferenceCount(selectionExercise, modifiers)) *
          equipmentPreferenceWeight +
        (isPrimaryForGroup(selectionExercise, evaluationGroup) ? primaryWeight : 0n) +
        BigInt(getSequenceCanonicalCoverage(
          exercise,
          this.exercisesById,
          evaluationGroup,
        ));
    };

    const allowed = groups.map(() => candidates.map(() => false));
    const baseUtilities = groups.map(() => candidates.map(() => 0n));
    const anchorUtilities = groups.map(() => candidates.map(() => 0n));
    for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
      const group = groups[groupIndex];
      for (let exerciseIndex = 0; exerciseIndex < candidates.length; exerciseIndex += 1) {
        const exercise = candidates[exerciseIndex];
        if (!isAllowed(exercise, group)) {
          continue;
        }
        allowed[groupIndex][exerciseIndex] = true;
        baseUtilities[groupIndex][exerciseIndex] = calculateUtility(
          exercise,
          group,
          false,
        );
        anchorUtilities[groupIndex][exerciseIndex] = calculateUtility(
          exercise,
          group,
          true,
        );
      }
    }

    const groupIndexById = new Map(groups.map((group, groupIndex) =>
      [group.id, groupIndex]));
    const atomicCandidates = [];
    for (let candidateIndex = 0;
      candidateIndex < candidates.length;
      candidateIndex += 1) {
      const candidate = candidates[candidateIndex];
      const placementOptions = this.getSequencePlacementOptions(candidate, groups);
      if (allowSavedSelectionException) {
        for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
          if (currentExerciseIds.get(groups[groupIndex].id) === candidate.id &&
              allowed[groupIndex][candidateIndex] &&
              placementOptions.every((option) =>
                option.every((placementGroup) =>
                  placementGroup.id !== groups[groupIndex].id))) {
            placementOptions.push([groups[groupIndex]]);
          }
        }
      }

      for (const placementGroups of placementOptions) {
        if (candidate.sequenceBlocks.length +
              (groups.length - placementGroups.length) >
            this.state.activeWorkoutMinutes) {
          // Even with one block in every remaining slot, this placement
          // cannot fit the requested duration.
          continue;
        }
        let coverageMask = 0n;
        let placementAllowed = true;
        for (const placementGroup of placementGroups) {
          const groupIndex = groupIndexById.get(placementGroup.id);
          if (groupIndex === undefined || !allowed[groupIndex][candidateIndex]) {
            placementAllowed = false;
            break;
          }
          coverageMask |= 1n << BigInt(groupIndex);
        }
        if (!placementAllowed) {
          continue;
        }
        const preferenceSlot = [...placementGroups]
          .sort((left, right) => left.order - right.order)[0];
        const preferenceSlotIndex = groupIndexById.get(preferenceSlot.id);
        const utilitiesByGroup = groups.map((evaluationGroup, groupIndex) =>
          (coverageMask & (1n << BigInt(groupIndex))) !== 0n
            ? groupIndex === preferenceSlotIndex
              ? anchorUtilities[groupIndex][candidateIndex]
              : baseUtilities[groupIndex][candidateIndex]
            : 0n);
        if (hasLightDayOpportunity(candidate, preferenceSlot)) {
          for (let groupIndex = 0; groupIndex < groups.length; groupIndex += 1) {
            if (groupIndex !== preferenceSlotIndex &&
                (coverageMask & (1n << BigInt(groupIndex))) !== 0n) {
              utilitiesByGroup[groupIndex] += lightDayOpportunityWeight;
            }
          }
        }
        atomicCandidates.push({
          exerciseId: candidate.id,
          movementId: getSessionMovementId(candidate),
          coverageMask,
          blockCount: candidate.sequenceBlocks.length,
          utilitiesByGroup,
          tieOrder: candidateIndex,
        });
      }
    }

    const solution = solveAtomicSequenceLineup(
      groups.length,
      this.state.activeWorkoutMinutes - reservedRepeatMinutes,
      atomicCandidates,
    ) ?? solveAtomicSequenceLineupAllowingRepeatedMovements(
      groups.length, this.state.activeWorkoutMinutes - reservedRepeatMinutes, atomicCandidates,
    );
    if (!solution) {
      const movementCount = new Set(atomicCandidates.map((candidate) =>
        candidate.movementId)).size;
      throw new Error(this.createDistinctLineupError(groups, movementCount).message +
        ` Budget: ${this.state.activeWorkoutMinutes - reservedRepeatMinutes}; unavailable slots: ` +
        groups.filter((g, i) => !atomicCandidates.some(c => (c.coverageMask & (1n << BigInt(i))) !== 0n))
          .map(g => g.id).join(","));
    }
    return new Map([...solution.exerciseIdByGroupIndex].map(
      ([groupIndex, exerciseId]) => [groups[groupIndex].id, exerciseId],
    ));
  }

  chooseBestCandidate(
    group,
    excludedExerciseIds = new Set(),
    modifiers = this.state.activeWorkoutModifiers,
  ) {
    const candidates = this.exercises.filter((exercise) =>
      this.isSelectable(exercise, group, modifiers) &&
      !excludedExerciseIds.has(exercise.id));
    if (candidates.length === 0) {
      throw new Error(`No eligible exercise exists for ${group.displayName}.`);
    }

    const phase = this.getLegacySelectionGroupPhase(
      group.id,
      this.state.activeWorkoutMinutes,
    );
    const highestScore = Math.max(...candidates.map((exercise) =>
      this.getSelectionScore(exercise, phase)));
    const highestScored = candidates.filter((exercise) =>
      this.getSelectionScore(exercise, phase) === highestScore);
    const selectionTimeUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    const rotationStatusByExercise = new Map(highestScored.map((exercise) => [
      exercise.id,
      getHardRotationStatus(
        exercise,
        group,
        this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      ),
    ]));
    const isRecoveringModerateByExercise = new Map(highestScored.map((exercise) => [
      exercise.id,
      isModerateExerciseRecovering(
        exercise,
        this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
        selectionTimeUnixMilliseconds,
      ),
    ]));
    const rotationTiers = [
      highestScored.filter((exercise) =>
        rotationStatusByExercise.get(exercise.id) === HARD_ROTATION_STATUS.FreshHard),
      highestScored.filter((exercise) =>
        rotationStatusByExercise.get(exercise.id) !== HARD_ROTATION_STATUS.RecoveringHard &&
        !isRecoveringModerateByExercise.get(exercise.id)),
      highestScored.filter((exercise) => isRecoveringModerateByExercise.get(exercise.id)),
      highestScored.filter((exercise) =>
        rotationStatusByExercise.get(exercise.id) === HARD_ROTATION_STATUS.RecoveringHard),
    ];
    const rotationPreferred = rotationTiers.find((tier) => tier.length > 0);
    const kept = rotationPreferred.filter((exercise) =>
      this.isSequenceKept(group.id, exercise));
    let keepPreferred = kept.length > 0 ? kept : rotationPreferred;
    if (rotationPreferred.every((exercise) =>
      rotationStatusByExercise.get(exercise.id) === HARD_ROTATION_STATUS.FreshHard)) {
      const oldestHardWork = Math.min(...keepPreferred.map((exercise) =>
        getLastHardWorkUnixMilliseconds(
          this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
          exercise.primaryCanonicalGroup,
        )));
      keepPreferred = keepPreferred.filter((exercise) =>
        getLastHardWorkUnixMilliseconds(
          this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
          exercise.primaryCanonicalGroup,
        ) === oldestHardWork);
    }
    const highestEquipmentPreference = Math.max(...keepPreferred.map((exercise) =>
      getEquipmentPreferenceCount(exercise, modifiers)));
    const equipmentPreferred = keepPreferred.filter((exercise) =>
      getEquipmentPreferenceCount(exercise, modifiers) ===
        highestEquipmentPreference);
    const primaryOwned = equipmentPreferred.filter((exercise) =>
      isPrimaryForGroup(exercise, group));
    const ownershipPreferred = primaryOwned.length > 0
      ? primaryOwned
      : equipmentPreferred;
    const widestCoverage = Math.max(
      ...ownershipPreferred.map((exercise) => getCanonicalCoverage(exercise, group)),
    );
    const finalists = ownershipPreferred.filter((exercise) =>
      getCanonicalCoverage(exercise, group) === widestCoverage);
    const index = Math.min(finalists.length - 1, Math.floor(this.random() * finalists.length));
    return finalists[Math.max(0, index)];
  }

  applyDistinctLineup(groups, lineup, clearChangedProgress, activeGroups = null) {
    activeGroups ??= clearChangedProgress ? this.getActiveGroups() : [];
    for (const group of groups) {
      const selectionStorageKey = this.getSelectionStorageKey(
        group.id,
        this.state.activeWorkoutModifiers,
      );
      const previousExerciseId = this.state.selectedExerciseIds[selectionStorageKey];
      const nextExerciseId = lineup.get(group.id);
      this.state.selectedExerciseIds[selectionStorageKey] = nextExerciseId;
      if (!clearChangedProgress || previousExerciseId === nextExerciseId) {
        continue;
      }
      for (const round of activeGroups.filter((candidate) =>
        getSelectionKey(candidate) === group.id)) {
        delete this.state.outcomes[round.id];
      }
      if (this.pendingRestMatchesSelectionGroup(group.id)) {
        this.clearPendingRest();
      }
    }
  }

  createDistinctLineupError(groups, movementCount) {
    return new Error(
      `No complete exercise lineup exists for the active workout profile across ` +
      `${groups.length} groups and ${movementCount} eligible session movements with at least ` +
      `${MINIMUM_CANONICAL_COVERAGE_PERCENT}% coverage.`,
    );
  }

  shuffle(items) {
    for (let index = items.length - 1; index > 0; index -= 1) {
      const randomIndex = Math.min(
        index,
        Math.max(0, Math.floor(this.random() * (index + 1))),
      );
      [items[index], items[randomIndex]] = [items[randomIndex], items[index]];
    }
  }

  isCompatibleWithModifiers(exercise, modifiers) {
    return isCompatibleWithWorkoutModifiers(exercise, modifiers);
  }

  getSequenceRoot(exercise) {
    const root = exercise
      ? this.sequenceRootByExerciseId.get(exercise.id)
      : null;
    if (!root) {
      throw new Error(`Exercise ${exercise?.id ?? "unknown"} has no sequence root.`);
    }
    return root;
  }

  getSequenceExercises(exercise) {
    const root = this.getSequenceRoot(exercise);
    return this.sequenceExercisesByRootId.get(root.id);
  }

  isDemandZeroSequence(exercise) {
    return this.getSequenceExercises(exercise).every((member) =>
      member.muscularDemand === MINIMUM_MUSCULAR_DEMAND);
  }

  getSequenceSelectionExerciseForGroup(exercise, group) {
    const root = this.getSequenceRoot(exercise);
    const primaryMembers = root.sequenceBlocks
      .map((block) => this.exercisesById.get(block.exerciseId))
      .filter((member) => group.canonicalGroups.includes(
        member.primaryCanonicalGroup,
      ));
    return primaryMembers.sort((left, right) =>
      right.muscularDemand - left.muscularDemand ||
      Number(right.id === root.id) - Number(left.id === root.id))[0] ??
      root;
  }

  getSequencePlacementOptions(exercise, groups) {
    const root = this.getSequenceRoot(exercise);
    const cacheKey = `${root.id}:${groups.map((group) => group.id).join("|")}`;
    if (this.sequencePlacementOptionsCache.has(cacheKey)) {
      return this.sequencePlacementOptionsCache.get(cacheKey);
    }
    const options = getSequencePlacementOptions(
      root,
      this.exercisesById,
      groups,
    );
    this.sequencePlacementOptionsCache.set(cacheKey, options);
    return options;
  }

  getResolutionGroupsForGroup(group) {
    const resolution = [...RESOLUTIONS.values()].find((candidate) =>
      candidate.groups.some((knownGroup) => knownGroup.id === group.id));
    if (!resolution) {
      throw new Error(`${group.displayName} has no workout resolution.`);
    }
    return resolution.groups;
  }

  isWorkoutSelectionCandidate(
    exercise,
    group,
    modifiers,
    selectionGroups = null,
  ) {
    if (!Array.isArray(exercise?.sequenceBlocks) ||
        exercise.sequenceBlocks.length === 0 ||
        this.getSequenceRoot(exercise).id !== exercise.id) {
      return false;
    }
    const activeGroups = selectionGroups ?? this.getSelectionGroups();
    const groups = activeGroups.length > 0
      ? activeGroups
      : this.getResolutionGroupsForGroup(group);
    return this.getSequencePlacementOptions(exercise, groups).some((option) =>
      option.some((candidate) => sameStringSet(
        candidate.canonicalGroups,
        group.canonicalGroups,
      ))) && this.getSequenceExercises(exercise).every((member) =>
      this.isCompatibleWithModifiers(member, modifiers));
  }

  isSelectable(exercise, group, modifiers) {
    return isSelectableForWorkoutProfile(exercise, group, modifiers);
  }

  getSelectionStorageKey(selectionGroupId, modifiers) {
    const normalized = normalizeWorkoutModifiers(modifiers);
    return normalized === WORKOUT_MODIFIERS.None
      ? selectionGroupId
      : `${SELECTION_PROFILE_PREFIX}${normalized}` +
          `${SELECTION_PROFILE_SEPARATOR}${selectionGroupId}`;
  }

  parseSelectionStorageKey(selectionStorageKey) {
    const separatorIndex = selectionStorageKey.indexOf(SELECTION_PROFILE_SEPARATOR);
    if (selectionStorageKey.startsWith(SELECTION_PROFILE_PREFIX) &&
        separatorIndex > SELECTION_PROFILE_PREFIX.length) {
      const modifierValue = Number(selectionStorageKey.slice(
        SELECTION_PROFILE_PREFIX.length,
        separatorIndex,
      ));
      const modifiers = normalizeWorkoutModifiers(modifierValue);
      if (Number.isInteger(modifierValue) && modifierValue > 0 &&
          modifiers === modifierValue) {
        return {
          selectionGroupId: selectionStorageKey.slice(separatorIndex + 1),
          modifiers,
        };
      }
    }
    return {
      selectionGroupId: selectionStorageKey.includes(SELECTION_PROFILE_SEPARATOR)
        ? ""
        : selectionStorageKey,
      modifiers: WORKOUT_MODIFIERS.None,
    };
  }

  getScore(exercise) {
    const saved = this.state.scores[String(exercise.id)];
    return Number.isInteger(saved) ? saved : Number.isInteger(exercise.score) ? exercise.score : 0;
  }

  setScore(exercise, score) {
    this.state.scores[String(exercise.id)] = Math.trunc(score);
  }

  normalizeSavedLineups() {
    for (const [selectionStorageKey, exerciseId] of
      Object.entries(this.state.selectedExerciseIds)) {
      const { selectionGroupId, modifiers } = this.parseSelectionStorageKey(
        selectionStorageKey,
      );
      const group = ALL_GROUPS.get(selectionGroupId);
      const exercise = this.exercisesById.get(exerciseId);
      if (!group || !exercise || !this.isStoredLineupSelectionValid(
        exercise,
        group,
        modifiers,
      )) {
        delete this.state.selectedExerciseIds[selectionStorageKey];
      }
    }
  }

  normalizeScores() {
    for (const exerciseId of Object.keys(this.state.scores)) {
      if (!this.exercisesById.has(Number(exerciseId))) {
        delete this.state.scores[exerciseId];
      }
    }
  }

  getSelectionScore(exercise, phase) {
    const root = this.getSequenceRoot(exercise);
    const legacyScore = Math.min(...this.getSequenceExercises(root).map((member) =>
      this.getScore(member)));
    const adjustment = this.state.exerciseScoreAdjustmentsByPhase[phase]?.[
      String(root.id)
    ];
    return legacyScore + (Number.isInteger(adjustment) ? adjustment : 0);
  }

  getPhaseScoreAdjustment(exercise, phase) {
    if (!isPersistableExercisePhase(phase)) {
      return 0;
    }
    const root = this.getSequenceRoot(exercise);
    const adjustment = this.state.exerciseScoreAdjustmentsByPhase[phase]?.[
      String(root.id)
    ];
    return Number.isInteger(adjustment) ? adjustment : 0;
  }

  downvoteSequenceInPhase(phase, exercise) {
    if (!isPersistableExercisePhase(phase)) {
      throw new RangeError("A downvote requires a workout exercise phase.");
    }
    const root = this.getSequenceRoot(exercise);
    const adjustments = this.state.exerciseScoreAdjustmentsByPhase[phase] ?? {};
    adjustments[String(root.id)] = (adjustments[String(root.id)] ?? 0) - 1;
    this.state.exerciseScoreAdjustmentsByPhase[phase] = adjustments;
  }

  getExercisePhase(group) {
    return getWorkoutExercisePhase(group.order);
  }

  getProjectedSelectionPhase(group, selectionGroupCount) {
    if (!Number.isInteger(selectionGroupCount) || selectionGroupCount <= 0) {
      throw new RangeError("Selection group count must be positive.");
    }
    const workoutMinutes = Math.max(
      selectionGroupCount,
      this.state.activeWorkoutMinutes,
    );
    const projectedFinalBlockOrder = Math.ceil(
      group.order * workoutMinutes / selectionGroupCount,
    );
    return getWorkoutExercisePhase(projectedFinalBlockOrder);
  }

  migrateLegacyCompletedTrainingDays(nowUnixMilliseconds) {
    const inferred = inferLegacyCompletedTrainingDays(
      this.state.workoutHistory,
      this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
      this.state.legacyCompletedTrainingDayUnixMilliseconds,
      nowUnixMilliseconds,
    );
    this.state.legacyCompletedTrainingDayUnixMilliseconds = [
      ...new Set([
        ...this.state.legacyCompletedTrainingDayUnixMilliseconds,
        ...inferred,
      ]),
    ];
  }

  migrateSlotScopedPreferences() {
    const sessions = [
      ...this.state.workoutHistory,
      ...(this.state.activeWorkoutSession ? [this.state.activeWorkoutSession] : []),
    ].sort((left, right) =>
      left.startedAtUnixMilliseconds - right.startedAtUnixMilliseconds ||
      left.sessionId - right.sessionId);
    for (const session of sessions) {
      for (const selection of session.initialSelections.filter((item) =>
        item.wasKeptAtWorkoutStart)) {
        const root = this.exercisesById.get(selection.rootExerciseId);
        if (root) {
          this.keepSequenceInSlot(selection.selectionGroupId, root);
        }
      }
      for (const change of [...session.selectionChanges].sort((left, right) =>
        left.changedAtUnixMilliseconds - right.changedAtUnixMilliseconds)) {
        const root = this.exercisesById.get(change.rejectedRootExerciseId);
        if (root) {
          this.removeSequenceKeep(change.selectionGroupId, root);
        }
      }
      for (const decision of [...session.decisions].sort((left, right) =>
        left.decidedAtUnixMilliseconds - right.decidedAtUnixMilliseconds)) {
        const root = this.exercisesById.get(decision.rootExerciseId);
        if (!root) {
          continue;
        }
        if (decision.outcome === "tick") {
          this.keepSequenceInSlot(decision.selectionGroupId, root);
        } else if (decision.outcome === "x") {
          this.removeSequenceKeep(decision.selectionGroupId, root);
        }
      }
    }

    const legacyKeeps = new Set(this.state.lastKeptExerciseIds);
    const savedPlacements = new Map();
    for (const [storageKey, exerciseId] of Object.entries(
      this.state.selectedExerciseIds,
    )) {
      const { selectionGroupId, modifiers } = this.parseSelectionStorageKey(storageKey);
      const selected = this.exercisesById.get(exerciseId);
      const group = ALL_GROUPS.get(selectionGroupId);
      if (!selected || !group) {
        continue;
      }
      const root = this.getSequenceRoot(selected);
      const resolution = selectionGroupId.split(".")[0];
      const key = `${modifiers}|${resolution}|${root.id}`;
      const placement = savedPlacements.get(key) ?? { root, groupIds: [] };
      placement.groupIds.push(selectionGroupId);
      savedPlacements.set(key, placement);
    }
    for (const { root, groupIds } of savedPlacements.values()) {
      if (!this.getSequenceExercises(root).every((member) =>
        legacyKeeps.has(member.id))) {
        continue;
      }
      const anchorId = [...new Set(groupIds)].sort((left, right) =>
        ALL_GROUPS.get(left).order - ALL_GROUPS.get(right).order)[0];
      this.keepSequenceInSlot(anchorId, root);
    }

    // Historical global scores are retained as a read-only baseline: older
    // state did not record enough information to invent exact original slots.
    // Every vote after this migration is represented only by a slot delta.
    this.state.exerciseScoreAdjustmentsBySelectionGroupId = {};
    this.syncLegacyKeptExerciseIds();
  }

  migratePhaseScopedDownvotes() {
    const sessions = [
      ...this.state.workoutHistory,
      ...(this.state.activeWorkoutSession ? [this.state.activeWorkoutSession] : []),
    ];
    // Earlier preference models removed a slot Keep when that same slot was
    // rejected. Phase-local rejection no longer means "unkeep everywhere",
    // so restore historical Keeps before replaying phase-provenance feedback.
    for (const session of sessions) {
      for (const decision of session.decisions.filter((item) =>
        item.outcome === "tick")) {
        const root = this.exercisesById.get(decision.rootExerciseId);
        if (root && ALL_GROUPS.has(decision.selectionGroupId)) {
          this.keepSequenceInSlot(decision.selectionGroupId, root);
        }
      }
    }
    const loggedDownvotes = sessions.flatMap((session) => [
      ...session.selectionChanges.map((change) => ({
        sessionId: session.sessionId,
        timestamp: change.changedAtUnixMilliseconds,
        selectionGroupId: change.selectionGroupId,
        rootExerciseId: change.rejectedRootExerciseId,
        phase: this.resolveLoggedSelectionChangePhase(session, change),
      })),
      ...session.decisions.filter((decision) => decision.outcome === "x")
        .map((decision) => ({
          sessionId: session.sessionId,
          timestamp: decision.decidedAtUnixMilliseconds,
          selectionGroupId: decision.selectionGroupId,
          rootExerciseId: decision.rootExerciseId,
          phase: this.resolveLoggedDecisionPhase(session, decision),
        })),
    ]).sort((left, right) => left.timestamp - right.timestamp ||
      left.sessionId - right.sessionId);

    for (const [selectionGroupId, adjustments] of Object.entries(
      this.state.exerciseScoreAdjustmentsBySelectionGroupId,
    )) {
      for (const [rootIdText, adjustment] of Object.entries(
        objectOrEmpty(adjustments),
      )) {
        const rootId = Number(rootIdText);
        const root = this.exercisesById.get(rootId);
        const downvoteCount = Number.isInteger(adjustment)
          ? Math.max(0, -adjustment)
          : 0;
        if (!root || downvoteCount === 0) {
          continue;
        }
        const matchingEvents = loggedDownvotes.filter((entry) =>
          entry.selectionGroupId === selectionGroupId &&
          entry.rootExerciseId === rootId).slice(-downvoteCount);
        for (const entry of matchingEvents) {
          this.downvoteSequenceInPhase(entry.phase, root);
        }
        const fallbackPhase = this.getLegacySelectionGroupPhase(selectionGroupId);
        for (let index = matchingEvents.length; index < downvoteCount; index += 1) {
          this.downvoteSequenceInPhase(fallbackPhase, root);
        }
      }
    }
    this.state.exerciseScoreAdjustmentsBySelectionGroupId = {};
  }

  resolveLoggedSelectionChangePhase(session, change) {
    if (isPersistableExercisePhase(change.exercisePhase)) {
      return change.exercisePhase;
    }
    const lastCompletedOrder = Math.max(0, ...session.blocks
      .filter((block) => change.changedAtUnixMilliseconds <= 0 ||
        block.completedAtUnixMilliseconds <= change.changedAtUnixMilliseconds)
      .map((block) => block.order));
    return lastCompletedOrder > 0
      ? getWorkoutExercisePhase(lastCompletedOrder + 1)
      : this.getLegacySelectionGroupPhase(
        change.selectionGroupId,
        session.workoutMinutes,
      );
  }

  resolveLoggedDecisionPhase(session, decision) {
    if (isPersistableExercisePhase(decision.exercisePhase)) {
      return decision.exercisePhase;
    }
    const matchingOrders = session.blocks.filter((block) =>
      block.selectionGroupId === decision.selectionGroupId &&
      block.rootExerciseId === decision.rootExerciseId &&
      (decision.decidedAtUnixMilliseconds <= 0 ||
       block.completedAtUnixMilliseconds <= decision.decidedAtUnixMilliseconds))
      .map((block) => block.order);
    const fallbackOrders = session.blocks.filter((block) =>
      decision.decidedAtUnixMilliseconds <= 0 ||
      block.completedAtUnixMilliseconds <= decision.decidedAtUnixMilliseconds)
      .map((block) => block.order);
    const decisionOrder = Math.max(
      0,
      ...(matchingOrders.length > 0 ? matchingOrders : fallbackOrders),
    );
    return decisionOrder > 0
      ? getWorkoutExercisePhase(decisionOrder)
      : this.getLegacySelectionGroupPhase(
        decision.selectionGroupId,
        session.workoutMinutes,
      );
  }

  getLegacySelectionGroupPhase(selectionGroupId, workoutMinutes = 0) {
    const group = ALL_GROUPS.get(selectionGroupId);
    const resolutionMatch = /^r(\d+)\./.exec(selectionGroupId);
    const resolutionMinutes = Number(resolutionMatch?.[1]);
    if (!group || !RESOLUTIONS.has(resolutionMinutes)) {
      return WORKOUT_EXERCISE_PHASE.Warmup;
    }
    const resolutionGroupCount = getResolution(resolutionMinutes).groups.length;
    const effectiveMinutes = SUPPORTED_MINUTES.includes(workoutMinutes)
      ? workoutMinutes
      : resolutionGroupCount;
    return getWorkoutExercisePhase(Math.ceil(
      group.order * effectiveMinutes / resolutionGroupCount,
    ));
  }

  normalizeSlotPreferences() {
    for (const [selectionGroupId, rootIds] of Object.entries(
      this.state.keptExerciseRootIdsBySelectionGroupId,
    )) {
      const normalized = uniquePositiveIntegers(rootIds).filter((rootId) =>
        this.isValidPreferenceRoot(selectionGroupId, rootId));
      if (normalized.length === 0) {
        delete this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId];
      } else {
        this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId] = normalized;
      }
    }
    for (const [selectionGroupId, adjustments] of Object.entries(
      this.state.exerciseScoreAdjustmentsBySelectionGroupId,
    )) {
      const normalized = Object.fromEntries(Object.entries(objectOrEmpty(adjustments))
        .filter(([rootId, adjustment]) => /^\d+$/.test(rootId) &&
          Number.isInteger(adjustment) && adjustment !== 0 &&
          this.isValidPreferenceRoot(selectionGroupId, Number(rootId))));
      if (Object.keys(normalized).length === 0) {
        delete this.state.exerciseScoreAdjustmentsBySelectionGroupId[selectionGroupId];
      } else {
        this.state.exerciseScoreAdjustmentsBySelectionGroupId[selectionGroupId] = normalized;
      }
    }
    for (const [phase, adjustments] of Object.entries(
      this.state.exerciseScoreAdjustmentsByPhase,
    )) {
      const normalized = Object.fromEntries(Object.entries(objectOrEmpty(adjustments))
        .filter(([rootId, adjustment]) => /^\d+$/.test(rootId) &&
          isPersistableExercisePhase(phase) &&
          Number.isInteger(adjustment) && adjustment < 0 &&
          this.isValidPreferenceRootId(Number(rootId))));
      if (Object.keys(normalized).length === 0) {
        delete this.state.exerciseScoreAdjustmentsByPhase[phase];
      } else {
        this.state.exerciseScoreAdjustmentsByPhase[phase] = normalized;
      }
    }
    this.syncLegacyKeptExerciseIds();
    this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
      this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
    );
    this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = normalizeWorkHistory(
      this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
    );
    const nextExcludedExerciseIds = new Set(
      this.state.nextWorkoutExcludedExerciseIds.filter((exerciseId) =>
        this.exercisesById.has(exerciseId)),
    );
    this.expandSequenceIds(nextExcludedExerciseIds);
    this.state.nextWorkoutExcludedExerciseIds = [...nextExcludedExerciseIds];
  }

  isValidPreferenceRoot(selectionGroupId, rootId) {
    const group = ALL_GROUPS.get(selectionGroupId);
    const root = this.exercisesById.get(rootId);
    if (!group || !root || this.getSequenceRoot(root).id !== rootId) {
      return false;
    }
    return this.getSequencePlacementOptions(
      root,
      this.getResolutionGroupsForGroup(group),
    ).some((option) => [...option].sort((left, right) => left.order - right.order)[0]
      ?.id === selectionGroupId);
  }

  isValidPreferenceRootId(rootId) {
    const root = this.exercisesById.get(rootId);
    return Boolean(root && this.getSequenceRoot(root).id === rootId);
  }

  getKeptRootIdsForSlot(selectionGroupId) {
    return new Set(this.state.keptExerciseRootIdsBySelectionGroupId[
      selectionGroupId
    ] ?? []);
  }

  isSequenceKept(selectionGroupId, exercise) {
    return this.getKeptRootIdsForSlot(selectionGroupId)
      .has(this.getSequenceRoot(exercise).id);
  }

  keepSequenceInSlot(selectionGroupId, exercise) {
    const keptRootIds = this.getKeptRootIdsForSlot(selectionGroupId);
    keptRootIds.add(this.getSequenceRoot(exercise).id);
    this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
      [...keptRootIds];
  }

  removeSequenceKeep(selectionGroupId, exercise) {
    const keptRootIds = this.getKeptRootIdsForSlot(selectionGroupId);
    keptRootIds.delete(this.getSequenceRoot(exercise).id);
    if (keptRootIds.size === 0) {
      delete this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId];
    } else {
      this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
        [...keptRootIds];
    }
  }

  syncLegacyKeptExerciseIds() {
    const keptExerciseIds = new Set();
    for (const rootId of new Set(Object.values(
      this.state.keptExerciseRootIdsBySelectionGroupId,
    ).flat())) {
      const root = this.exercisesById.get(rootId);
      if (root) {
        for (const member of this.getSequenceExercises(root)) {
          keptExerciseIds.add(member.id);
        }
      }
    }
    this.state.lastKeptExerciseIds = [...keptExerciseIds];
  }

  removeSavedSequenceCopiesForSlot(selectionGroupId, rootId) {
    const matchingByProfileAndResolution = new Map();
    for (const [storageKey, exerciseId] of Object.entries(
      this.state.selectedExerciseIds,
    )) {
      const parsed = this.parseSelectionStorageKey(storageKey);
      if (exerciseId !== rootId || !ALL_GROUPS.has(parsed.selectionGroupId)) {
        continue;
      }
      const resolution = parsed.selectionGroupId.split(".")[0];
      const key = `${parsed.modifiers}|${resolution}`;
      const entries = matchingByProfileAndResolution.get(key) ?? [];
      entries.push({ storageKey, selectionGroupId: parsed.selectionGroupId });
      matchingByProfileAndResolution.set(key, entries);
    }
    for (const entries of matchingByProfileAndResolution.values()) {
      const anchorId = [...new Set(entries.map((entry) => entry.selectionGroupId))]
        .sort((left, right) => ALL_GROUPS.get(left).order - ALL_GROUPS.get(right).order)[0];
      if (anchorId !== selectionGroupId) {
        continue;
      }
      for (const { storageKey } of entries) {
        delete this.state.selectedExerciseIds[storageKey];
      }
    }
  }

  removePartialSequenceIds(exerciseIds) {
    const completeSequenceExerciseIds = new Set();
    const roots = new Map([...exerciseIds].map((exerciseId) => {
      const root = this.getSequenceRoot(this.exercisesById.get(exerciseId));
      return [root.id, root];
    }));
    for (const root of roots.values()) {
      const members = this.getSequenceExercises(root);
      if (members.every((member) => exerciseIds.has(member.id))) {
        for (const member of members) {
          completeSequenceExerciseIds.add(member.id);
        }
      }
    }
    for (const exerciseId of [...exerciseIds]) {
      if (!completeSequenceExerciseIds.has(exerciseId)) {
        exerciseIds.delete(exerciseId);
      }
    }
  }

  expandSequenceIds(exerciseIds) {
    for (const exerciseId of [...exerciseIds]) {
      const exercise = this.exercisesById.get(exerciseId);
      if (exercise) {
        for (const member of this.getSequenceExercises(exercise)) {
          exerciseIds.add(member.id);
        }
      }
    }
  }

  isStoredLineupSelectionValid(exercise, group, modifiers) {
    if (this.isSavedSelectionValid(exercise, group, modifiers)) {
      return true;
    }
    return this.state.activeWorkoutMinutes === 0 &&
      Array.isArray(exercise.sequenceBlocks) &&
      exercise.sequenceBlocks.length > 0 &&
      this.getSequenceRoot(exercise).id === exercise.id &&
      this.getSequencePlacementOptions(
        exercise,
        this.getResolutionGroupsForGroup(group),
      ).some((option) => option.some((candidate) => sameStringSet(
        candidate.canonicalGroups,
        group.canonicalGroups,
      ))) &&
      this.getSequenceExercises(exercise).every((member) =>
        this.isCompatibleWithModifiers(member, modifiers));
  }

  normalizeActiveLongWorkoutAllocation() {
    if (!this.isLongWorkoutAllocationValid()) {
      this.setActiveLongWorkoutAllocation();
    }
  }

  isLongWorkoutAllocationValid() {
    if (Object.keys(this.state.activeDirectionPartnerExerciseIds).length !== 0 ||
        this.state.activeFullSideRoundIds.length !== 0) {
      return false;
    }

    let placements;
    try {
      placements = this.getSelectedSequencePlacements();
    } catch {
      return false;
    }
    const actualSetCounts = new Map(Object.entries(
      this.state.activeSetCountsBySelectionGroupId,
    ));
    if (actualSetCounts.size !== placements.length ||
        placements.some((placement) =>
          !Number.isInteger(actualSetCounts.get(placement.anchor.id)) ||
          actualSetCounts.get(placement.anchor.id) < 1)) {
      return false;
    }

    const expectedExtraSetGroups = [...actualSetCounts]
      .filter(([, setCount]) => setCount > 1)
      .map(([groupId]) => groupId);
    if (!sameStringSet(
      this.state.activeExtraSetSelectionGroupIds,
      expectedExtraSetGroups,
    )) {
      return false;
    }

    try {
      const rounds = this.createActiveWorkoutSchedule(actualSetCounts);
      // This is an active-workout snapshot. Re-ranking it after a Keep or
      // downvote would move already scheduled blocks and orphan their results.
      // New phase feedback is applied when the next allocation is created.
      return rounds.length === this.state.activeWorkoutMinutes;
    } catch {
      return false;
    }
  }

  chooseLongWorkoutAllocation(
    lockedSelectionGroupIds = new Set(),
    selectedPlacements = null,
  ) {
    const rankedPlacements = [...(selectedPlacements ??
      this.getSelectedSequencePlacements())]
      .sort((left, right) => {
        const leftMembers = this.getSequenceExercises(left.root);
        const rightMembers = this.getSequenceExercises(right.root);
        return Number(rightMembers.some((member) =>
          member.muscularDemand === HARD_MUSCULAR_DEMAND)) -
            Number(leftMembers.some((member) =>
              member.muscularDemand === HARD_MUSCULAR_DEMAND)) ||
          Number(this.isSequenceKept(right.anchor.id, right.root)) -
            Number(this.isSequenceKept(left.anchor.id, left.root)) ||
          right.anchor.order - left.anchor.order;
      });
    const blockCostByGroup = new Map(rankedPlacements.map((placement) => [
      placement.anchor.id,
      placement.root.sequenceBlocks.length,
    ]));
    const setCountsBySelectionGroupId = new Map(rankedPlacements.map((placement) => [
      placement.anchor.id,
      1,
    ]));
    let remainingMinutes = this.state.activeWorkoutMinutes -
      [...blockCostByGroup.values()].reduce((total, cost) => total + cost, 0);

    for (const placement of rankedPlacements.filter(({ anchor }) =>
      lockedSelectionGroupIds.has(anchor.id))) {
      const lockedSetCount =
        this.state.activeSetCountsBySelectionGroupId[placement.anchor.id] ?? 1;
      if (!Number.isInteger(lockedSetCount) || lockedSetCount < 1) {
        throw new Error(
          `The completed set allocation for ${placement.anchor.displayName} is invalid.`,
        );
      }
      setCountsBySelectionGroupId.set(placement.anchor.id, lockedSetCount);
      remainingMinutes -=
        (lockedSetCount - 1) * blockCostByGroup.get(placement.anchor.id);
    }
    if (remainingMinutes < 0) {
      throw new Error("The selected mandatory sequences exceed the workout duration.");
    }

    const repeatablePlacements = rankedPlacements.filter(({ anchor }) =>
      !lockedSelectionGroupIds.has(anchor.id));
    const repeatableMetadata = repeatablePlacements.map((placement) => ({
      placement,
      groupId: placement.anchor.id,
      cost: blockCostByGroup.get(placement.anchor.id),
      setCount: setCountsBySelectionGroupId.get(placement.anchor.id),
      kept: this.isSequenceKept(placement.anchor.id, placement.root),
    }));
    const repeatableCosts = [...new Set(repeatableMetadata.map(({ cost }) => cost))];
    const scheduleOrderedPlacements = this.getScheduleOrderedPlacements(
      rankedPlacements,
    );
    // Fillability depends only on the available sequence lengths. Build the
    // unbounded-knapsack table once instead of rebuilding it for every
    // candidate in every added set.
    const fillable = new Array(remainingMinutes + 1).fill(false);
    fillable[0] = true;
    for (let value = 1; value <= remainingMinutes; value += 1) {
      fillable[value] = repeatableCosts.some((cost) =>
        cost <= value && fillable[value - cost]);
    }
    const hasPhaseScoreAdjustments = Object.values(
      this.state.exerciseScoreAdjustmentsByPhase,
    ).some((adjustments) => Object.keys(adjustments).length > 0);

    while (remainingMinutes > 0) {
      // Compute every candidate's next-set phase in one prefix scan. The old
      // comparator rescanned the entire schedule for both sides of every sort
      // comparison, which made long-workout rebalancing needlessly cubic.
      let phaseAfterAddingSetByGroupId = null;
      if (hasPhaseScoreAdjustments) {
        phaseAfterAddingSetByGroupId = new Map();
        let finalBlockOrder = 0;
        for (const placement of scheduleOrderedPlacements) {
          const groupId = placement.anchor.id;
          const cost = blockCostByGroup.get(groupId);
          finalBlockOrder += cost * setCountsBySelectionGroupId.get(groupId);
          phaseAfterAddingSetByGroupId.set(
            groupId,
            getWorkoutExercisePhase(finalBlockOrder + cost),
          );
        }
      }

      let selectedMetadata = null;
      let selectedScore = Number.MIN_SAFE_INTEGER;
      for (const metadata of repeatableMetadata) {
        const { placement, groupId, cost, setCount, kept } = metadata;
        if (cost > remainingMinutes || !fillable[remainingMinutes - cost]) {
          continue;
        }
        const score = hasPhaseScoreAdjustments
          ? this.getPhaseScoreAdjustment(
              placement.root,
              phaseAfterAddingSetByGroupId.get(groupId),
            )
          : 0;
        if (!selectedMetadata ||
            score > selectedScore ||
            (score === selectedScore &&
              (setCount < selectedMetadata.setCount ||
                (setCount === selectedMetadata.setCount &&
                  (Number(kept) > Number(selectedMetadata.kept) ||
                    (kept === selectedMetadata.kept &&
                      Number(cost === 1) >
                        Number(selectedMetadata.cost === 1))))))) {
          selectedMetadata = metadata;
          selectedScore = score;
        }
      }
      if (!selectedMetadata) {
        throw new Error(
          "The selected sequence lengths cannot fill the workout duration.",
        );
      }
      const selectedPlacement = selectedMetadata.placement;
      selectedMetadata.setCount += 1;
      setCountsBySelectionGroupId.set(
        selectedPlacement.anchor.id,
        selectedMetadata.setCount,
      );
      remainingMinutes -= selectedMetadata.cost;
    }

    return {
      extraSetSelectionGroupIds: [...setCountsBySelectionGroupId]
        .filter(([, setCount]) => setCount > 1)
        .map(([groupId]) => groupId),
      setCountsBySelectionGroupId,
    };
  }
  rebalanceNewExercisesByMuscleBalance(
    lockedSelectionGroupIds = new Set(),
  ) {
    const groups = this.getSelectionGroups();
    const keptExerciseIds = new Set(Object.values(
      this.state.keptExerciseRootIdsBySelectionGroupId,
    ).flat());
    if (groups.length === 0) {
      return;
    }
    const selectionTimeUnixMilliseconds = this.getCurrentUnixTimeMilliseconds();
    const allocationCache = new Map();
    const rebalanceRoots = this.exercises.filter((exercise) =>
      exercise.sequenceBlocks.length > 0 &&
      this.getSequenceRoot(exercise).id === exercise.id &&
      !keptExerciseIds.has(exercise.id) &&
      !this.state.nextWorkoutExcludedExerciseIds.includes(exercise.id) &&
      this.getSequenceExercises(exercise).every((member) =>
        this.isCompatibleWithModifiers(
          member,
          this.state.activeWorkoutModifiers,
        )));
    const placementOptionsByRootId = new Map(rebalanceRoots.map((exercise) => [
      exercise.id,
      this.getSequencePlacementOptions(exercise, groups),
    ]));
    const sequenceLoadByRootId = new Map();
    const getSequenceLoad = (root) => {
      if (!sequenceLoadByRootId.has(root.id)) {
        sequenceLoadByRootId.set(
          root.id,
          calculateCanonicalMuscleLoadEighthUnits(
            this.getSequenceExercises(root),
          ),
        );
      }
      return sequenceLoadByRootId.get(root.id);
    };
    const selectionScoreByRootAndGroup = new Map();
    const getCachedSelectionScore = (root, groupId) => {
      const key = `${root.id}:${groupId}`;
      if (!selectionScoreByRootAndGroup.has(key)) {
        selectionScoreByRootAndGroup.set(
          key,
          this.getSelectionScore(
            root,
            this.getProjectedSelectionPhase(
              ALL_GROUPS.get(groupId),
              groups.length,
            ),
          ),
        );
      }
      return selectionScoreByRootAndGroup.get(key);
    };
    const rebalanceCandidates = rebalanceRoots.map((candidate) => ({
      candidate,
      movementId: getSessionMovementId(candidate),
      options: placementOptionsByRootId.get(candidate.id)
        .filter((option) => option.every((group) => isSelectableForWorkoutProfile(
          this.getSequenceSelectionExerciseForGroup(candidate, group),
          group,
          this.state.activeWorkoutModifiers,
        )))
        .map((option) => ({
          groups: option,
          anchor: [...option].sort((left, right) => left.order - right.order)[0],
        }))
        .map((option) => ({
          ...option,
          allocationBehaviorKey: this.getLongWorkoutAllocationPlacementKey(
            candidate,
            option.anchor,
          ),
        })),
    }));

    const seenLineups = new Set();
    for (let pass = 0; pass < MUSCLE_BALANCE_MAX_REBALANCE_PASSES; pass += 1) {
      const signature = groups.map((group) => this.state.selectedExerciseIds[
        this.getSelectionStorageKey(group.id, this.state.activeWorkoutModifiers)
      ] ?? 0).join(",");
      if (seenLineups.has(signature)) {
        break;
      }
      seenLineups.add(signature);

      let currentPlacements;
      let currentAllocation;
      try {
        currentPlacements = this.getSelectedSequencePlacements();
        currentAllocation = this.getCachedLongWorkoutAllocation(
          currentPlacements,
          allocationCache,
          lockedSelectionGroupIds,
        );
      } catch {
        break;
      }

      const currentCanonicalLoad =
        this.calculateScheduledCanonicalLoadEighthUnits(
          currentPlacements,
          currentAllocation,
        );
      const currentBalance = calculateMuscleBalanceEvaluation(
        currentCanonicalLoad,
      );
      if (currentBalance.isBalanced) {
        break;
      }

      const currentRootIds = new Set(currentPlacements.map(({ root }) => root.id));
      const currentMovementIds = new Set(currentPlacements.map(({ root }) =>
        getSessionMovementId(root)));
      const placementByGroupId = new Map(currentPlacements.flatMap((placement) =>
        placement.coveredGroups.map((group) => [group.id, placement])));
      let bestAlternative = null;
      const currentAllocationHasOneSetPerPlacement = [
        ...currentAllocation.setCountsBySelectionGroupId.values(),
      ].every((setCount) => setCount === 1);
      const replacementAllocationByKey = new Map();

      for (const candidateMetadata of rebalanceCandidates) {
        const { candidate, movementId: candidateMovementId } = candidateMetadata;
        if (currentRootIds.has(candidate.id) ||
            currentMovementIds.has(candidateMovementId)) {
          continue;
        }
        for (const {
          groups: option,
          anchor,
          allocationBehaviorKey,
        } of candidateMetadata.options) {
          const removedPlacements = [...new Set(option.map((group) =>
            placementByGroupId.get(group.id)))];
          if (removedPlacements.reduce((total, placement) =>
            total + placement.coveredGroups.length, 0) !== option.length ||
              removedPlacements.some((placement) =>
                lockedSelectionGroupIds.has(placement.anchor.id)) ||
              removedPlacements.some((placement) =>
                this.isSequenceKept(placement.anchor.id, placement.root)) ||
              removedPlacements.some((placement) =>
                getSessionMovementId(placement.root) === candidateMovementId)) {
            continue;
          }

          if ((this.state.activeWorkoutModifiers &
                WORKOUT_MODIFIERS.Light) !== 0 &&
              removedPlacements.some((placement) =>
                this.isDemandZeroSequence(placement.root)) &&
              !this.isDemandZeroSequence(candidate)) {
            // The balance pass is lower priority than the calendar day mode.
            continue;
          }

          const preservesScores = option.every((group) => {
            const displacedRootId = this.state.selectedExerciseIds[
              this.getSelectionStorageKey(
                group.id,
                this.state.activeWorkoutModifiers,
              )
            ];
            const displacedRoot = this.exercisesById.get(displacedRootId);
            return displacedRoot &&
              getCachedSelectionScore(candidate, group.id) >=
                getCachedSelectionScore(displacedRoot, group.id);
          });
          if (!preservesScores) {
            continue;
          }
          let candidateBalance;
          const removedBlockCount = removedPlacements.reduce(
            (total, placement) =>
              total + placement.root.sequenceBlocks.length,
            0,
          );
          if (currentAllocationHasOneSetPerPlacement &&
              candidate.sequenceBlocks.length > removedBlockCount) {
            continue;
          }
          const reusesCurrentAllocation =
            currentAllocationHasOneSetPerPlacement &&
              candidate.sequenceBlocks.length === removedBlockCount ||
            removedPlacements.length === 1 &&
              candidate.sequenceBlocks.length ===
                removedPlacements[0].root.sequenceBlocks.length &&
              this.getSequenceExercises(candidate).some((member) =>
                member.muscularDemand === HARD_MUSCULAR_DEMAND) ===
              this.getSequenceExercises(removedPlacements[0].root).some((member) =>
                member.muscularDemand === HARD_MUSCULAR_DEMAND);
          if (reusesCurrentAllocation) {
            const canonicalLoadDelta = new Map();
            for (const removed of removedPlacements) {
              const removedSetCount =
                currentAllocation.setCountsBySelectionGroupId.get(
                  removed.anchor.id,
                ) ?? 1;
              for (const [muscle, load] of getSequenceLoad(removed.root)) {
                canonicalLoadDelta.set(
                  muscle,
                  (canonicalLoadDelta.get(muscle) ?? 0) -
                    load * removedSetCount,
                );
              }
            }
            const candidateSetCount = removedPlacements.length === 1
              ? currentAllocation.setCountsBySelectionGroupId.get(
                  removedPlacements[0].anchor.id,
                ) ?? 1
              : 1;
            for (const [muscle, load] of getSequenceLoad(candidate)) {
              canonicalLoadDelta.set(
                muscle,
                (canonicalLoadDelta.get(muscle) ?? 0) +
                  load * candidateSetCount,
              );
            }
            candidateBalance = calculateMuscleBalanceAfterCanonicalDelta(
              currentBalance,
              canonicalLoadDelta,
            );
          } else {
            const removedPlacementSet = new Set(removedPlacements);
            let candidateAllocation;
            const replacementAllocationKey = `${removedPlacements
              .map((placement) => placement.anchor.id)
              .sort()
              .join(",")}>${allocationBehaviorKey}`;
            if (replacementAllocationByKey.has(replacementAllocationKey)) {
              candidateAllocation = replacementAllocationByKey.get(
                replacementAllocationKey,
              );
            } else {
              const candidatePlacements = [
                ...currentPlacements.filter((placement) =>
                  !removedPlacementSet.has(placement)),
                { root: candidate, anchor, coveredGroups: option },
              ].sort((left, right) => left.anchor.order - right.anchor.order);
              try {
                candidateAllocation = this.getCachedLongWorkoutAllocation(
                  candidatePlacements,
                  allocationCache,
                  lockedSelectionGroupIds,
                );
              } catch {
                candidateAllocation = null;
              }
              replacementAllocationByKey.set(
                replacementAllocationKey,
                candidateAllocation,
              );
            }
            if (!candidateAllocation) {
              continue;
            }
            const canonicalLoadDelta = new Map();
            for (const placement of currentPlacements) {
              const currentSetCount =
                currentAllocation.setCountsBySelectionGroupId.get(
                  placement.anchor.id,
                ) ?? 1;
              const candidateSetCount = removedPlacementSet.has(placement)
                ? 0
                : candidateAllocation.setCountsBySelectionGroupId.get(
                  placement.anchor.id,
                ) ?? 1;
              const setCountDelta = candidateSetCount - currentSetCount;
              if (setCountDelta === 0) {
                continue;
              }
              for (const [muscle, load] of getSequenceLoad(placement.root)) {
                canonicalLoadDelta.set(
                  muscle,
                  (canonicalLoadDelta.get(muscle) ?? 0) + load * setCountDelta,
                );
              }
            }
            const candidateSetCount =
              candidateAllocation.setCountsBySelectionGroupId.get(anchor.id) ?? 1;
            for (const [muscle, load] of getSequenceLoad(candidate)) {
              canonicalLoadDelta.set(
                muscle,
                (canonicalLoadDelta.get(muscle) ?? 0) + load * candidateSetCount,
              );
            }
            candidateBalance = calculateMuscleBalanceAfterCanonicalDelta(
              currentBalance,
              canonicalLoadDelta,
            );
          }
          if (compareMuscleBalanceEvaluations(
            candidateBalance,
            currentBalance,
          ) <= 0) {
            continue;
          }

          const alternative = this.createMuscleBalanceCandidate(
            candidate,
            anchor,
            option,
            candidateBalance,
            selectionTimeUnixMilliseconds,
          );
          if (!bestAlternative || this.isPreferredMuscleBalanceCandidate(
            alternative,
            bestAlternative,
          )) {
            bestAlternative = alternative;
          }
        }
      }

      if (!bestAlternative) {
        break;
      }
      for (const coveredGroup of bestAlternative.coveredGroups) {
        this.state.selectedExerciseIds[this.getSelectionStorageKey(
          coveredGroup.id,
          this.state.activeWorkoutModifiers,
        )] = bestAlternative.exerciseId;
      }
    }
  }

  createMuscleBalanceCandidate(
    candidate,
    anchor,
    coveredGroups,
    balance,
    selectionTimeUnixMilliseconds,
  ) {
    const selectionExercise = this.getSequenceSelectionExerciseForGroup(
      candidate,
      anchor,
    );
    const rotationStatus = getHardRotationStatus(
      selectionExercise,
      anchor,
      this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
      selectionTimeUnixMilliseconds,
    );
    const isRecoveringModerate = isModerateExerciseRecovering(
      selectionExercise,
      this.state.lastMeaningfulWorkUnixMillisecondsByPrimaryMuscle,
      selectionTimeUnixMilliseconds,
    );
    return {
      exerciseId: candidate.id,
      coveredGroups,
      balance,
      realScore: this.getSelectionScore(
        candidate,
        this.getProjectedSelectionPhase(
          anchor,
          this.getSelectionGroups().length,
        ),
      ),
      isFreshHard: rotationStatus === HARD_ROTATION_STATUS.FreshHard,
      isRecoveringHard: rotationStatus === HARD_ROTATION_STATUS.RecoveringHard,
      isRecoveringModerate,
      lastHardWorkUnixMilliseconds:
        rotationStatus === HARD_ROTATION_STATUS.FreshHard
          ? getLastHardWorkUnixMilliseconds(
              this.state.lastHardWorkUnixMillisecondsByPrimaryMuscle,
              selectionExercise.primaryCanonicalGroup,
            )
          : 0,
      equipmentPreferenceCount: getEquipmentPreferenceCount(
        selectionExercise,
        this.state.activeWorkoutModifiers,
      ),
      isPrimary: isPrimaryForGroup(selectionExercise, anchor),
      canonicalCoverage: getSequenceCanonicalCoverage(
        candidate,
        this.exercisesById,
        anchor,
      ),
    };
  }

  isPreferredMuscleBalanceCandidate(candidate, currentBest) {
    const balanceComparison = compareMuscleBalanceEvaluations(
      candidate.balance,
      currentBest.balance,
    );
    if (balanceComparison !== 0) {
      return balanceComparison > 0;
    }
    return candidate.realScore !== currentBest.realScore
      ? candidate.realScore > currentBest.realScore
      : candidate.isFreshHard !== currentBest.isFreshHard
        ? candidate.isFreshHard
        : candidate.isRecoveringHard !== currentBest.isRecoveringHard
          ? !candidate.isRecoveringHard
          : candidate.isRecoveringModerate !== currentBest.isRecoveringModerate
            ? !candidate.isRecoveringModerate
            : candidate.lastHardWorkUnixMilliseconds !==
                currentBest.lastHardWorkUnixMilliseconds
              ? candidate.lastHardWorkUnixMilliseconds <
                  currentBest.lastHardWorkUnixMilliseconds
              : candidate.equipmentPreferenceCount !==
                  currentBest.equipmentPreferenceCount
                ? candidate.equipmentPreferenceCount >
                    currentBest.equipmentPreferenceCount
                : candidate.isPrimary !== currentBest.isPrimary
                  ? candidate.isPrimary
                  : candidate.canonicalCoverage !== currentBest.canonicalCoverage
                    ? candidate.canonicalCoverage > currentBest.canonicalCoverage
                    : candidate.exerciseId < currentBest.exerciseId;
  }

  getCachedLongWorkoutAllocation(
    placements,
    allocationCache,
    lockedSelectionGroupIds = new Set(),
  ) {
    const signature = placements
      .sort((left, right) => left.anchor.order - right.anchor.order)
      .map((placement) => this.getLongWorkoutAllocationPlacementKey(
        placement.root,
        placement.anchor,
      ))
      .join("|");
    if (allocationCache.has(signature)) {
      const cached = allocationCache.get(signature);
      if (!cached) {
        throw new Error("The selected sequence lengths cannot fill the workout duration.");
      }
      return cached;
    }
    try {
      const allocation = this.chooseLongWorkoutAllocation(
        lockedSelectionGroupIds,
        placements,
      );
      allocationCache.set(signature, allocation);
      return allocation;
    } catch (error) {
      allocationCache.set(signature, null);
      throw error;
    }
  }

  getLongWorkoutAllocationPlacementKey(root, anchor) {
    const sequenceExercises = this.getSequenceExercises(root);
    const keyParts = [
      anchor.id,
      root.sequenceBlocks.length,
      Number(sequenceExercises.some((member) =>
        member.muscularDemand === HARD_MUSCULAR_DEMAND)),
      Number(this.isSequenceKept(anchor.id, root)),
    ];
    const hasPhaseScoreAdjustments = Object.values(
      this.state.exerciseScoreAdjustmentsByPhase,
    ).some((adjustments) => Object.keys(adjustments).length > 0);
    if (hasPhaseScoreAdjustments) {
      // Schedule order influences allocation only when a phase can change a
      // score. Include every phase-sensitive dependency in that case; omit
      // them when all phase adjustments are empty so behaviorally identical
      // catalog roots can share an allocation safely.
      keyParts.push(
        getMuscularDemandSchedulePriority(
          getSequenceMuscularDemand(root, this.exercisesById),
        ),
        this.getPhaseScoreAdjustment(root, WORKOUT_EXERCISE_PHASE.Warmup),
        this.getPhaseScoreAdjustment(root, WORKOUT_EXERCISE_PHASE.PeakPerformance),
        this.getPhaseScoreAdjustment(root, WORKOUT_EXERCISE_PHASE.Fatigued),
      );
    }
    return keyParts.join(":");
  }

  calculateScheduledCanonicalLoadEighthUnits(placements, allocation) {
    const loadEighthUnits = new Map();
    for (const placement of placements) {
      const setCount = allocation.setCountsBySelectionGroupId.get(
        placement.anchor.id,
      ) ?? 1;
      for (const exercise of this.getSequenceExercises(placement.root)) {
        addExerciseMuscleLoadEighthUnits(
          loadEighthUnits,
          exercise,
          setCount,
        );
      }
    }
    return loadEighthUnits;
  }

  setActiveLongWorkoutAllocation() {
    this.applyLongWorkoutAllocation(this.chooseLongWorkoutAllocation());
  }

  reconcileLineupWithScheduledPhases(lockedSelectionGroupIds = null) {
    const hasPhaseScoreAdjustments = Object.values(
      this.state.exerciseScoreAdjustmentsByPhase,
    ).some((adjustments) => Object.keys(adjustments).length > 0);
    if (!hasPhaseScoreAdjustments) {
      return;
    }

    const selectionGroups = this.getSelectionGroups();
    if (selectionGroups.length === 0) {
      return;
    }

    // Demand ordering and repeated sets determine the phase in which a
    // sequence actually ends. Iterate to a stable lineup so a phase-local
    // downvote is never evaluated from the unrelated anatomical bucket order.
    const seenLineups = new Set();
    const prefixOrder = this.state.activeSelectionGroupOrder.filter(id => lockedSelectionGroupIds?.has(id));
    const protectedGroups = new Set(this.getSelectedSequencePlacements()
      .filter(p => lockedSelectionGroupIds?.has(p.anchor.id)).flatMap(p => p.coveredGroups.map(g => g.id)));
    for (let pass = 0; pass < selectionGroups.length; pass += 1) {
      const currentLineup = new Map(selectionGroups.map((group) => [
        group.id,
        this.state.selectedExerciseIds[this.getSelectionStorageKey(
          group.id,
          this.state.activeWorkoutModifiers,
        )],
      ]));
      const signature = selectionGroups
        .map((group) => currentLineup.get(group.id))
        .join(",");
      if (seenLineups.has(signature)) {
        break;
      }
      seenLineups.add(signature);

      const allocation = this.chooseLongWorkoutAllocation(lockedSelectionGroupIds ?? undefined);
      this.applyLongWorkoutAllocation(allocation);
      const scheduledPhaseByGroupId = this.getScheduledPhaseByGroupId(allocation);
      const nextLineup = this.chooseBestDistinctLineup(
        selectionGroups,
        this.state.activeWorkoutModifiers,
        {
          currentExerciseIds: lockedSelectionGroupIds === null ? currentLineup :
            new Map([...currentLineup].filter(([id]) => protectedGroups.has(id))),
          allowSavedSelectionException: lockedSelectionGroupIds !== null,
          modifierTransitionProtectedGroupIds: protectedGroups,
          reservedRepeatMinutes: this.getSelectedSequencePlacements()
            .filter(p => lockedSelectionGroupIds?.has(p.anchor.id))
            .reduce((sum, p) => sum + ((this.state.activeSetCountsBySelectionGroupId[p.anchor.id] ?? 1) - 1) *
              p.root.sequenceBlocks.length, 0),
          scheduledPhaseByGroupId,
        },
      );
      if (selectionGroups.every((group) =>
        nextLineup.get(group.id) === currentLineup.get(group.id))) {
        return;
      }
      this.applyDistinctLineup(selectionGroups, nextLineup, false);
      if (lockedSelectionGroupIds !== null) this.setEditedSelectionOrder(prefixOrder);
    }

    this.applyLongWorkoutAllocation(this.chooseLongWorkoutAllocation(lockedSelectionGroupIds ?? undefined));
  }

  getScheduledPhaseByGroupId(allocation) {
    const phaseBySelectionGroupId = new Map();
    for (const group of this.createActiveWorkoutSchedule(
      allocation.setCountsBySelectionGroupId,
    )) {
      phaseBySelectionGroupId.set(
        getSelectionKey(group),
        getWorkoutExercisePhase(group.order),
      );
    }

    const result = new Map();
    for (const placement of this.getSelectedSequencePlacements()) {
      const phase = phaseBySelectionGroupId.get(placement.anchor.id);
      for (const coveredGroup of placement.coveredGroups) {
        result.set(coveredGroup.id, phase);
      }
    }
    return result;
  }

  applyLongWorkoutAllocation(allocation) {
    this.state.activeDirectionPartnerExerciseIds = {};
    this.state.activeFullSideRoundIds = [];
    this.state.activeExtraSetSelectionGroupIds = [...allocation.extraSetSelectionGroupIds];
    this.state.activeSetCountsBySelectionGroupId = Object.fromEntries(
      allocation.setCountsBySelectionGroupId,
    );
  }

  getEffectiveSetCounts() {
    return this.isLongWorkoutAllocationValid()
      ? new Map(Object.entries(this.state.activeSetCountsBySelectionGroupId))
      : this.chooseLongWorkoutAllocation().setCountsBySelectionGroupId;
  }

  normalizeOutcomes() {
    const activeGroups = new Map(this.getActiveGroups().map((group) =>
      [group.id, group]));
    for (const groupId of Object.keys(this.state.outcomes)) {
      if (!activeGroups.has(groupId)) {
        delete this.state.outcomes[groupId];
      }
    }
    for (const [groupId, outcome] of Object.entries(this.state.outcomes)) {
      if (outcome === "neutral" &&
          !this.isIntermediateSequenceBlock(activeGroups.get(groupId))) {
        this.state.outcomes[groupId] = "tick";
      }
    }
  }

  normalizeCompletionState() {
    const activeGroups = this.getActiveGroups();
    this.state.workoutCompleted = activeGroups.length > 0 &&
      activeGroups.every((group) => this.state.outcomes[group.id] !== undefined);
    if (!this.state.workoutCompleted) {
      this.state.completionAcknowledged = false;
    }
  }

  captureLegacyActiveProgress() {
    return {
      outcomes: { ...this.state.outcomes },
      selectedExerciseIds: { ...this.state.selectedExerciseIds },
      directionPartnerExerciseIds: {
        ...this.state.activeDirectionPartnerExerciseIds,
      },
      fullSideRoundIds: new Set(this.state.activeFullSideRoundIds),
      pendingMovementGroupId: this.state.pendingMovementGroupId,
      pendingMovementMillisecondsRemaining:
        this.state.pendingMovementMillisecondsRemaining,
      pendingMovementEndsAtUnixMilliseconds:
        this.state.pendingMovementEndsAtUnixMilliseconds,
      pendingMovementPausedByUser: this.state.pendingMovementPausedByUser,
      pendingRestGroupId: this.state.pendingRestGroupId,
      pendingRestEndsAtUnixMilliseconds: this.state.pendingRestEndsAtUnixMilliseconds,
      pendingRestKept: this.state.pendingRestKept,
    };
  }

  migrateLegacyActiveProgress(snapshot) {
    const rounds = [...this.getActiveGroups()].sort((left, right) =>
      left.order - right.order);
    this.state.outcomes = {};
    this.clearPendingMovement();
    this.clearPendingRest();

    for (const selectionGroup of this.getSelectionGroups()) {
      const sequenceRounds = rounds.filter((round) =>
        getSelectionKey(round) === selectionGroup.id);
      if (!this.legacySelectionMatchesCurrentSequence(
        snapshot,
        selectionGroup.id,
      )) {
        continue;
      }
      const oldOutcomes = Object.entries(snapshot.outcomes)
        .filter(([roundId]) =>
          this.resolveLegacySelectionKey(roundId) === selectionGroup.id);
      const decision = oldOutcomes
        .filter(([, outcome]) => outcome === "tick" || outcome === "x")
        .at(-1)?.[1];
      if (decision) {
        for (const round of sequenceRounds.slice(0, -1)) {
          this.state.outcomes[round.id] = "neutral";
        }
        this.state.outcomes[sequenceRounds.at(-1).id] = decision;
        continue;
      }
      for (const [legacyRoundId, outcome] of oldOutcomes
        .filter(([, value]) => value === "neutral")
        .sort((left, right) =>
          this.getLegacyRoundOrdinal(left[0]) - this.getLegacyRoundOrdinal(right[0]))) {
        if (outcome !== "neutral") {
          continue;
        }
        for (const round of this.resolveLegacyRepresentedRounds(
          snapshot,
          sequenceRounds,
          legacyRoundId,
        )) {
          if (!isFinalSequenceRound(round)) {
            this.state.outcomes[round.id] = "neutral";
          }
        }
      }
    }

    const legacyPendingRoundId =
      snapshot.pendingRestGroupId ?? snapshot.pendingMovementGroupId;
    const pendingSelectionKey = this.resolveLegacySelectionKey(
      legacyPendingRoundId,
    );
    if (!legacyPendingRoundId || !pendingSelectionKey) {
      return;
    }
    if (!this.legacySelectionMatchesCurrentSequence(snapshot, pendingSelectionKey)) {
      return;
    }
    const pendingSequenceRounds = rounds.filter((round) =>
      getSelectionKey(round) === pendingSelectionKey);
    const representedRounds = this.resolveLegacyRepresentedRounds(
      snapshot,
      pendingSequenceRounds,
      legacyPendingRoundId,
    );
    if (representedRounds.length === 0) {
      return;
    }

    if (snapshot.pendingRestGroupId &&
        snapshot.pendingRestEndsAtUnixMilliseconds > 0) {
      const pendingRound = representedRounds.at(-1);
      this.markSequenceRoundsBeforePending(pendingSequenceRounds, pendingRound);
      this.state.pendingRestGroupId = pendingRound.id;
      this.state.pendingRestEndsAtUnixMilliseconds =
        snapshot.pendingRestEndsAtUnixMilliseconds;
      this.state.pendingRestKept = snapshot.pendingRestKept &&
        isFinalSequenceRound(pendingRound);
      return;
    }
    if (!snapshot.pendingMovementGroupId ||
        snapshot.pendingMovementMillisecondsRemaining <= 0) {
      return;
    }

    const mapped = this.mapLegacyMovementProgress(
      snapshot,
      legacyPendingRoundId,
      representedRounds.length,
    );
    const pendingRound = representedRounds[Math.max(
      0,
      Math.min(representedRounds.length - 1, mapped.representedRoundIndex),
    )];
    this.markSequenceRoundsBeforePending(pendingSequenceRounds, pendingRound);
    const maximum = getMovementCountdownDurationMs(pendingRound);
    this.state.pendingMovementGroupId = pendingRound.id;
    this.state.pendingMovementMillisecondsRemaining = Math.max(
      1,
      Math.min(maximum, mapped.remainingMilliseconds),
    );
    this.state.pendingMovementEndsAtUnixMilliseconds =
      representedRounds.length === 1 &&
        mapped.remainingMilliseconds ===
          snapshot.pendingMovementMillisecondsRemaining
        ? snapshot.pendingMovementEndsAtUnixMilliseconds
        : 0;
    this.state.pendingMovementPausedByUser = snapshot.pendingMovementPausedByUser;
  }

  legacySelectionMatchesCurrentSequence(snapshot, selectionKey) {
    const legacyExerciseId = this.resolveLegacyMemberExerciseId(
      snapshot,
      selectionKey,
      selectionKey,
    );
    const currentExerciseId = this.state.selectedExerciseIds[
      this.getSelectionStorageKey(
        selectionKey,
        this.state.activeWorkoutModifiers,
      )
    ];
    const legacyExercise = this.exercisesById.get(legacyExerciseId);
    const currentExercise = this.exercisesById.get(currentExerciseId);
    return Boolean(legacyExercise && currentExercise &&
      this.getSequenceRoot(legacyExercise).id ===
        this.getSequenceRoot(currentExercise).id);
  }

  resolveLegacyRepresentedRounds(snapshot, sequenceRounds, legacyRoundId) {
    const selectionKey = this.resolveLegacySelectionKey(legacyRoundId);
    if (!selectionKey) {
      return [];
    }
    const setNumber = this.getLegacySetNumber(legacyRoundId);
    const setRounds = sequenceRounds
      .filter((round) => (round.setNumber ?? 1) === setNumber)
      .sort((left, right) =>
        (left.sequenceBlockIndex ?? 0) - (right.sequenceBlockIndex ?? 0));
    if (setRounds.length === 0) {
      return [];
    }
    const memberExerciseId = this.resolveLegacyMemberExerciseId(
      snapshot,
      selectionKey,
      legacyRoundId,
    );
    const represented = memberExerciseId > 0
      ? setRounds.filter((round) => this.getSelectedExercise(round).id ===
        memberExerciseId)
      : [];
    return represented.length > 0 ? represented : setRounds;
  }

  resolveLegacyMemberExerciseId(snapshot, selectionKey, legacyRoundId) {
    if (legacyRoundId.startsWith(`${selectionKey}.direction`) &&
        Number.isInteger(snapshot.directionPartnerExerciseIds[selectionKey])) {
      return snapshot.directionPartnerExerciseIds[selectionKey];
    }
    const activeStorageKey = this.getSelectionStorageKey(
      selectionKey,
      this.state.activeWorkoutModifiers,
    );
    if (Number.isInteger(snapshot.selectedExerciseIds[activeStorageKey])) {
      return snapshot.selectedExerciseIds[activeStorageKey];
    }
    return Object.entries(snapshot.selectedExerciseIds)
      .find(([storageKey]) => {
        const parsed = this.parseSelectionStorageKey(storageKey);
        return parsed.selectionGroupId === selectionKey &&
          normalizeWorkoutModifiers(parsed.modifiers) ===
            this.state.activeWorkoutModifiers;
      })?.[1] ?? 0;
  }

  mapLegacyMovementProgress(snapshot, legacyRoundId, representedRoundCount) {
    const remaining = snapshot.pendingMovementMillisecondsRemaining;
    if (representedRoundCount < 2) {
      return { representedRoundIndex: 0, remainingMilliseconds: remaining };
    }
    const selectionKey = this.resolveLegacySelectionKey(legacyRoundId);
    const memberExerciseId = selectionKey
      ? this.resolveLegacyMemberExerciseId(snapshot, selectionKey, legacyRoundId)
      : 0;
    const member = this.exercisesById.get(memberExerciseId);
    const usedTimedPair = member &&
      ([
        "ScreenLeftThenRight",
        "ScreenRightThenLeft",
        "ScreenLeftLeadThenRightLead",
        "ScreenRightLeadThenLeftLead",
      ].includes(member.sideSequence) || member.directionSequence !== "None");
    if (!usedTimedPair) {
      return { representedRoundIndex: 0, remainingMilliseconds: remaining };
    }
    if (snapshot.fullSideRoundIds.has(legacyRoundId)) {
      if (remaining > 60_000) {
        return {
          representedRoundIndex: 0,
          remainingMilliseconds: remaining - 60_000,
        };
      }
      return {
        representedRoundIndex: 1,
        remainingMilliseconds: remaining > 45_000 ? 45_000 : remaining,
      };
    }
    if (remaining > 25_000) {
      return { representedRoundIndex: 0, remainingMilliseconds: remaining };
    }
    return {
      representedRoundIndex: 1,
      remainingMilliseconds: remaining > 20_000 ? 45_000 : remaining + 25_000,
    };
  }

  markSequenceRoundsBeforePending(sequenceRounds, pendingRound) {
    for (const round of sequenceRounds) {
      if (round.id === pendingRound.id) {
        break;
      }
      this.state.outcomes[round.id] ??= "neutral";
    }
  }

  getLegacySetNumber(roundId) {
    for (const marker of [".set", ".direction"]) {
      const index = roundId.lastIndexOf(marker);
      const value = index >= 0 ? Number(roundId.slice(index + marker.length)) : 0;
      if (Number.isInteger(value) && value > 0) {
        return value;
      }
    }
    return 1;
  }

  getLegacyRoundOrdinal(roundId) {
    return (this.getLegacySetNumber(roundId) - 1) * 2 +
      (roundId.includes(".direction") ? 1 : 0);
  }

  resolveLegacySelectionKey(roundId) {
    if (typeof roundId !== "string" || !roundId) {
      return null;
    }
    return [...ALL_GROUPS.keys()]
      .filter((groupId) => roundId === groupId ||
        roundId.startsWith(`${groupId}.`))
      .sort((left, right) => right.length - left.length)[0] ?? null;
  }

  normalizePendingRest() {
    const pendingBaseGroup = ALL_GROUPS.get(this.state.pendingRestGroupId);
    const pendingRoot = pendingBaseGroup
      ? this.exercisesById.get(this.state.selectedExerciseIds[
          this.getSelectionStorageKey(
            pendingBaseGroup.id,
            this.state.activeWorkoutModifiers,
          )
        ])
      : null;
    if (pendingBaseGroup && pendingRoot &&
        this.hasValidPendingRestTiming() &&
        this.isSavedSelectionValid(
          pendingRoot,
          pendingBaseGroup,
          this.state.activeWorkoutModifiers,
        )) {
      return;
    }
    try {
      if (this.getValidPendingRestGroup()) {
        return;
      }
    } catch {
      // An obsolete schedule cannot identify a valid current checkpoint.
    }
    this.clearPendingRest();
  }

  normalizePendingMovement() {
    if (!this.getPendingMovementGroup()) {
      this.clearPendingMovement();
      return;
    }

    // A valid rest means this movement already completed. Persisted phases
    // are mutually exclusive, and rest therefore wins over movement.
    const pendingRest = this.getActiveGroups().find(
      (group) => group.id === this.state.pendingRestGroupId,
    );
    if (pendingRest &&
        this.hasValidPendingRestTiming() &&
        this.state.outcomes[pendingRest.id] === undefined) {
      this.clearPendingMovement();
    }
  }

  isSavedSelectionValid(exercise, group, modifiers, selectionGroups = null) {
    if (this.isWorkoutSelectionCandidate(
      exercise,
      group,
      modifiers,
      selectionGroups,
    )) {
      return true;
    }
    if (this.isModifierTransitionProtectedSelection(
      exercise,
      group,
      modifiers,
    )) {
      return true;
    }
    if (this.isModifierTransitionRetainedCompletedSelection(
      exercise,
      group,
      modifiers,
    )) {
      return true;
    }
    if (
      !this.pendingRestMatchesSelectionGroup(getSelectionKey(group)) ||
      this.getSequenceRoot(exercise).id !== exercise.id
    ) {
      return false;
    }
    return this.getSequenceExercises(exercise).every((member) =>
        this.isCompatibleWithModifiers(member, modifiers) &&
        this.isAssignedToGroup(member, group));
  }

  isSequenceOverrideValid(exercise, group, modifiers) {
    const blockIndex = group.sequenceBlockIndex ?? -1;
    const blockCount = group.sequenceBlockCount ?? 0;
    if (blockIndex < 0 || blockIndex >= blockCount) {
      return false;
    }
    const root = this.exercisesById.get(
      this.state.selectedExerciseIds[this.getSelectionStorageKey(
        getSelectionKey(group),
        modifiers,
      )],
    );
    const block = root?.sequenceBlocks?.[blockIndex];
    return Boolean(root &&
      root.sequenceBlocks.length === blockCount &&
      block?.exerciseId === exercise.id &&
      (block.sideCue ?? "None") === (group.sequenceSideCue ?? "None") &&
      (block.directionCue ?? "None") ===
        (group.sequenceDirectionCue ?? "None") &&
      (block.mirrorMedia === true) === (group.mirrorSequenceMedia === true) &&
      (block.mediaSegment ?? "Full") ===
        (group.sequenceMediaSegment ?? "Full") &&
      (this.isWorkoutSelectionCandidate(root, group, modifiers) ||
        this.isModifierTransitionProtectedSelection(
          root,
          group,
          modifiers,
        ) ||
        this.isModifierTransitionRetainedCompletedSelection(
          root,
          group,
          modifiers,
        )));
  }

  isModifierTransitionProtectedSelection(exercise, group, modifiers) {
    const protectedSelectionGroupId =
      this.state.activeModifierProtectedSelectionGroupId;
    if (!protectedSelectionGroupId ||
        normalizeWorkoutModifiers(modifiers) !==
          this.state.activeWorkoutModifiers) {
      return false;
    }
    const root = this.getSequenceRoot(exercise);
    if (this.state.selectedExerciseIds[this.getSelectionStorageKey(
      protectedSelectionGroupId,
      modifiers,
    )] !== root.id) {
      return false;
    }
    if (getSelectionKey(group) === protectedSelectionGroupId) {
      return true;
    }
    return this.getSequencePlacementOptions(root, this.getSelectionGroups())
      .some((option) =>
        [...option].sort((left, right) => left.order - right.order)[0]?.id ===
          protectedSelectionGroupId &&
        option.some((candidate) => candidate.id === getSelectionKey(group)));
  }

  isModifierTransitionRetainedCompletedSelection(exercise, group, modifiers) {
    if (this.state.activeModifierRetainedSelectionGroupIds.length === 0 ||
        normalizeWorkoutModifiers(modifiers) !==
          this.state.activeWorkoutModifiers) {
      return false;
    }
    const root = this.getSequenceRoot(exercise);
    if (this.state.selectedExerciseIds[this.getSelectionStorageKey(
      getSelectionKey(group),
      modifiers,
    )] !== root.id) {
      return false;
    }
    const retainedSelectionGroupIds = new Set(
      this.state.activeModifierRetainedSelectionGroupIds,
    );
    const coversRetainedGroup = [...retainedSelectionGroupIds].some(
      (selectionGroupId) => this.state.selectedExerciseIds[
        this.getSelectionStorageKey(selectionGroupId, modifiers)
      ] === root.id,
    );
    return coversRetainedGroup &&
      (this.state.activeWorkoutSession?.decisions ?? []).some((decision) =>
        decision.rootExerciseId === root.id);
  }

  pendingRestMatchesSelectionGroup(selectionGroupId) {
    if (!this.state.pendingRestGroupId) {
      return false;
    }
    const roundMatch = /^(.*)\.set[1-9]\d*\.block[1-9]\d*$/.exec(
      this.state.pendingRestGroupId,
    );
    return (roundMatch?.[1] ?? this.state.pendingRestGroupId) === selectionGroupId;
  }

  isAssignedToGroup(exercise, group) {
    return (
      group.canonicalGroups.includes(exercise.primaryCanonicalGroup) ||
      (exercise.secondaryCanonicalGroups ?? []).some((canonicalGroup) =>
        group.canonicalGroups.includes(canonicalGroup),
      )
    );
  }

  reconcileCatalog() {
    const previousIdentities = this.state.catalogIdentities;
    const currentIdentities = Object.fromEntries(
      this.exercises.map((exercise) => [String(exercise.id), catalogIdentity(exercise)]),
    );
    const hardFloorInvalidatedExerciseIds =
      this.state.catalogRevision < HARD_FLOOR_SLIPPERINESS_CATALOG_REVISION
        ? SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(
          HARD_FLOOR_SLIPPERINESS_CATALOG_REVISION,
        ) ?? new Set()
        : new Set();
    const changedExerciseIds = catalogInvalidationIdsSince(
      this.state.catalogRevision,
      this.exercises,
      SCOPED_CATALOG_INVALIDATIONS_BY_REVISION,
      new Set([
        HARD_FLOOR_SLIPPERINESS_CATALOG_REVISION,
        REUSED_SHY_AUDIT_CATALOG_REVISION,
      ]),
    );
    const shyAuditInvalidatedExerciseIds =
      this.state.catalogRevision < REUSED_SHY_AUDIT_CATALOG_REVISION
        ? SCOPED_CATALOG_INVALIDATIONS_BY_REVISION.get(
          REUSED_SHY_AUDIT_CATALOG_REVISION,
        ) ?? new Set()
        : new Set();
    const scoreResetExerciseIds = catalogInvalidationIdsSince(
      this.state.catalogRevision,
      this.exercises,
      SCOPED_SCORE_INVALIDATIONS_BY_REVISION,
    );
    const trainingClaimChangedExerciseIds = new Set();
    for (const [revision, exerciseIds] of
      TRAINING_CLAIM_ASSOCIATION_CHANGES_BY_REVISION) {
      if (revision > this.state.catalogRevision) {
        for (const exerciseId of exerciseIds) {
          trainingClaimChangedExerciseIds.add(exerciseId);
        }
      }
    }
    const reusedExerciseIds = new Set([
      ...(this.state.catalogRevision < REUSED_SHY_AUDIT_CATALOG_REVISION
        ? REUSED_SHY_AUDIT_EXERCISE_IDS : []),
      ...(this.state.catalogRevision < 73
        ? SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(73) : []),
    ]);
    const shouldDiscardReusedExerciseKeeps = reusedExerciseIds.size > 0;
    const semanticallyInvalidSelectionStorageKeys =
      trainingClaimChangedExerciseIds.size > 0
        ? new Set(Object.entries(this.state.selectedExerciseIds)
          .filter(([selectionStorageKey, rootExerciseId]) => {
            const { selectionGroupId } =
              this.parseSelectionStorageKey(selectionStorageKey);
            return ALL_GROUPS.has(selectionGroupId) &&
              this.isTrainingClaimAffectedRoot(
              rootExerciseId,
              trainingClaimChangedExerciseIds,
            ) && !this.isValidPreferenceRoot(
              selectionGroupId,
              rootExerciseId,
            );
          })
          .map(([selectionStorageKey]) => selectionStorageKey))
        : new Set();

    for (const [exerciseId, previousIdentity] of Object.entries(previousIdentities)) {
      const currentIdentity = currentIdentities[exerciseId];
      const currentExercise = this.exercisesById.get(Number(exerciseId));
      if (
        currentIdentity === undefined ||
        (currentIdentity !== previousIdentity &&
          !isApprovedIdentityPreservingNameChange(
            Number(exerciseId),
            previousIdentity,
            currentExercise,
          ))
      ) {
        changedExerciseIds.add(Number(exerciseId));
        scoreResetExerciseIds.add(Number(exerciseId));
      }
    }

    if (changedExerciseIds.size > 0 ||
        shyAuditInvalidatedExerciseIds.size > 0 ||
        shouldDiscardReusedExerciseKeeps ||
        hardFloorInvalidatedExerciseIds.size > 0 ||
        semanticallyInvalidSelectionStorageKeys.size > 0) {
      const affectedSelectionStorageKeys = Object.entries(this.state.selectedExerciseIds)
        .filter(([selectionStorageKey, exerciseId]) =>
          semanticallyInvalidSelectionStorageKeys.has(selectionStorageKey) ||
          changedExerciseIds.has(exerciseId) ||
          (shouldDiscardReusedExerciseKeeps &&
            reusedExerciseIds.has(exerciseId)) ||
          (shyAuditInvalidatedExerciseIds.has(exerciseId) &&
            (this.parseSelectionStorageKey(selectionStorageKey).modifiers &
              WORKOUT_MODIFIERS.Shy) !== 0) ||
          (hardFloorInvalidatedExerciseIds.has(exerciseId) &&
            (this.parseSelectionStorageKey(selectionStorageKey).modifiers &
              WORKOUT_MODIFIERS.HardFloor) !== 0))
        .map(([selectionStorageKey]) => selectionStorageKey);
      for (const selectionStorageKey of affectedSelectionStorageKeys) {
        delete this.state.selectedExerciseIds[selectionStorageKey];
        const parsed = this.parseSelectionStorageKey(selectionStorageKey);
        if (parsed.modifiers !== this.state.activeWorkoutModifiers) {
          continue;
        }
        for (const roundId of Object.keys(this.state.outcomes)) {
          if (this.resolveLegacySelectionKey(roundId) === parsed.selectionGroupId) {
            delete this.state.outcomes[roundId];
          }
        }
        if (this.resolveLegacySelectionKey(this.state.pendingRestGroupId) ===
            parsed.selectionGroupId) {
          this.clearPendingRest();
        }
        if (this.resolveLegacySelectionKey(this.state.pendingMovementGroupId) ===
            parsed.selectionGroupId) {
          this.clearPendingMovement();
        }
      }
    }

    for (const exerciseId of scoreResetExerciseIds) {
      delete this.state.scores[String(exerciseId)];
    }

    const scoreResetPreferenceRoots = new Set(this.exercises
      .filter((exercise) => this.getSequenceRoot(exercise).id === exercise.id)
      .filter((root) => this.getSequenceExercises(root).some((member) =>
        scoreResetExerciseIds.has(member.id)))
      .map((root) => root.id));
    for (const adjustments of Object.values(
      this.state.exerciseScoreAdjustmentsBySelectionGroupId,
    )) {
      for (const rootId of scoreResetPreferenceRoots) {
        delete adjustments[String(rootId)];
      }
    }
    for (const adjustments of Object.values(
      this.state.exerciseScoreAdjustmentsByPhase,
    )) {
      for (const rootId of scoreResetPreferenceRoots) {
        delete adjustments[String(rootId)];
      }
    }

    if (shouldDiscardReusedExerciseKeeps) {
      for (const [selectionGroupId, rootIds] of Object.entries(
        this.state.keptExerciseRootIdsBySelectionGroupId,
      )) {
        const retainedRootIds = uniquePositiveIntegers(rootIds).filter((rootId) => {
          const root = this.exercisesById.get(rootId);
          return !reusedExerciseIds.has(rootId) &&
            !root?.sequenceBlocks.some((block) =>
              reusedExerciseIds.has(block.exerciseId));
        });
        if (retainedRootIds.length === 0) {
          delete this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId];
        } else {
          this.state.keptExerciseRootIdsBySelectionGroupId[selectionGroupId] =
            retainedRootIds;
        }
      }
      this.state.lastKeptExerciseIds = this.state.lastKeptExerciseIds.filter(
        (exerciseId) => !reusedExerciseIds.has(exerciseId),
      );
    }

    this.normalizeSlotPreferences();

    this.state.catalogIdentities = currentIdentities;
    this.state.catalogRevision = Math.max(
      this.state.catalogRevision,
      CURRENT_CATALOG_REVISION,
    );
    this.state.version = CURRENT_WORKOUT_STATE_VERSION;
  }

  isTrainingClaimAffectedRoot(rootId, trainingClaimChangedExerciseIds) {
    if (trainingClaimChangedExerciseIds.has(rootId)) {
      return true;
    }
    const root = this.exercisesById.get(rootId);
    return root?.sequenceBlocks.some((block) =>
      trainingClaimChangedExerciseIds.has(block.exerciseId)) === true;
  }

  ensureActiveWorkoutSession(startedBeforeLogging) {
    return this.state.activeWorkoutSession ?? this.createActiveWorkoutSession(
      this.getCurrentUnixTimeMilliseconds(),
      [...this.state.lastKeptExerciseIds].sort((left, right) => left - right),
      startedBeforeLogging,
    );
  }

  createActiveWorkoutSession(
    startedAtUnixMilliseconds,
    keptExerciseIdsAtStart,
    startedBeforeLogging,
  ) {
    if (!SUPPORTED_MINUTES.includes(this.state.activeWorkoutMinutes)) {
      throw new Error("Cannot log a workout without a valid active duration.");
    }
    if (!Number.isSafeInteger(startedAtUnixMilliseconds) ||
        startedAtUnixMilliseconds <= 0) {
      throw new RangeError("Workout start time must be positive Unix milliseconds.");
    }
    if (this.state.activeWorkoutSession) {
      return this.state.activeWorkoutSession;
    }

    const sessionId = Math.max(1, this.state.nextWorkoutSessionId);
    if (!Number.isSafeInteger(sessionId) || sessionId >= Number.MAX_SAFE_INTEGER) {
      throw new Error("Workout session IDs are exhausted.");
    }
    this.state.nextWorkoutSessionId = sessionId + 1;
    const normalizedKeptExerciseIdsAtStart = uniquePositiveIntegers(
      keptExerciseIdsAtStart,
    ).sort((left, right) => left - right);
    const setCounts = this.getEffectiveSetCounts();
    const scheduleOrderedPlacements = this.getScheduleOrderedPlacements();
    const finalBlockOrderBySelectionGroupId = new Map();
    for (const group of this.createActiveWorkoutSchedule(setCounts)) {
      finalBlockOrderBySelectionGroupId.set(
        getSelectionKey(group),
        group.order,
      );
    }
    const session = {
      sessionId,
      startedAtUnixMilliseconds,
      endedAtUnixMilliseconds: 0,
      workoutMinutes: this.state.activeWorkoutMinutes,
      modifiers: this.state.activeWorkoutModifiers,
      isLightDay:
        (this.state.activeWorkoutModifiers & WORKOUT_MODIFIERS.Light) !== 0,
      status: "InProgress",
      startedBeforeLogging: startedBeforeLogging === true,
      keptExerciseIdsAtStart: normalizedKeptExerciseIdsAtStart,
      keptExerciseRootIdsBySelectionGroupIdAtStart: Object.fromEntries(
        Object.entries(this.state.keptExerciseRootIdsBySelectionGroupId)
          .filter(([, rootIds]) => rootIds.length > 0)
          .map(([selectionGroupId, rootIds]) => [
            selectionGroupId,
            [...rootIds].sort((left, right) => left - right),
          ]),
      ),
      initialSelections: scheduleOrderedPlacements
        .map((placement) => ({
          selectionGroupId: placement.anchor.id,
          coveredWorkoutGroupIds: [...placement.coveredGroups]
            .sort((left, right) => left.order - right.order)
            .map((group) => group.id),
          rootExerciseId: placement.root.id,
          rootExerciseName: placement.root.name,
          selectionScoreAtStart: this.getSelectionScore(
            placement.root,
            getWorkoutExercisePhase(
              finalBlockOrderBySelectionGroupId.get(placement.anchor.id),
            ),
          ),
          sequenceBlockCount: placement.root.sequenceBlocks.length,
          setCount: Math.max(1, setCounts.get(placement.anchor.id) ?? 1),
          wasKeptAtWorkoutStart: this.isSequenceKept(
            placement.anchor.id,
            placement.root,
          ),
        })),
      selectionChanges: [],
      modifierChanges: [],
      durationChanges: [],
      blocks: [],
      decisions: [],
    };
    this.state.activeWorkoutSession = session;
    return session;
  }

  recordWorkoutSelectionChange(
    selectionGroupId,
    phase,
    rejectedRoot,
    rejectedSelectionScore,
    replacementRoot,
  ) {
    const session = this.ensureActiveWorkoutSession(true);
    session.selectionChanges.push({
      kind: "Shuffle",
      changedAtUnixMilliseconds: this.getCurrentUnixTimeMilliseconds(),
      selectionGroupId,
      exercisePhase: phase,
      rejectedRootExerciseId: rejectedRoot.id,
      rejectedRootExerciseName: rejectedRoot.name,
      rejectedSelectionScoreBeforeChange: rejectedSelectionScore,
      rejectedSelectionWasKeptAtWorkoutStart:
        this.wasSequenceKeptAtWorkoutStart(
          session,
          selectionGroupId,
          rejectedRoot,
        ),
      replacementRootExerciseId: replacementRoot.id,
      replacementRootExerciseName: replacementRoot.name,
      replacementSelectionScore: this.getSelectionScore(
        replacementRoot,
        phase,
      ),
    });
  }

  recordCompletedWorkoutBlock(group, completedAtUnixMilliseconds) {
    const session = this.ensureActiveWorkoutSession(true);
    if (session.blocks.some((block) => block.workoutGroupId === group.id)) {
      return;
    }

    const exercise = this.getSelectedExercise(group);
    const root = this.getSequenceRoot(exercise);
    session.blocks.push({
      completedAtUnixMilliseconds,
      workoutGroupId: group.id,
      selectionGroupId: getSelectionKey(group),
      order: group.order,
      rootExerciseId: root.id,
      rootExerciseName: root.name,
      exerciseId: exercise.id,
      exerciseName: exercise.name,
      sequenceBlockNumber: (group.sequenceBlockIndex ?? 0) + 1,
      sequenceBlockCount: group.sequenceBlockCount ?? 1,
      setNumber: group.setNumber ?? 1,
      setCount: group.setCount ?? 1,
      sideCue: group.sequenceSideCue ?? "None",
      directionCue: group.sequenceDirectionCue ?? "None",
      mirrorMedia: group.mirrorSequenceMedia === true,
      mediaSegment: group.sequenceMediaSegment ?? "Full",
      muscularDemand: exercise.muscularDemand,
      primaryCanonicalGroup: exercise.primaryCanonicalGroup,
      secondaryCanonicalGroups: [...exercise.secondaryCanonicalGroups],
      wasSequenceKeptAtWorkoutStart:
        this.wasSequenceKeptAtWorkoutStart(
          session,
          getSelectionKey(group),
          root,
        ),
    });
  }

  recordWorkoutDecision(
    group,
    root,
    outcome,
    selectionScoreBeforeDecision,
    exercisePhase,
    decidedAtUnixMilliseconds,
  ) {
    const session = this.ensureActiveWorkoutSession(true);
    const selectionGroupId = getSelectionKey(group);
    const existing = session.decisions.find((decision) =>
      decision.selectionGroupId === selectionGroupId);
    if (existing) {
      if (existing.rootExerciseId !== root.id || existing.outcome !== outcome) {
        throw new Error(`Workout selection ${selectionGroupId} was decided twice.`);
      }
      return;
    }

    session.decisions.push({
      decidedAtUnixMilliseconds,
      selectionGroupId,
      exercisePhase,
      rootExerciseId: root.id,
      rootExerciseName: root.name,
      sequenceExerciseIds: this.getSequenceExercises(root)
        .map((exercise) => exercise.id)
        .sort((left, right) => left - right),
      outcome,
      selectionScoreBeforeDecision,
      completedBlockCount: session.blocks.filter((block) =>
        block.selectionGroupId === selectionGroupId).length,
      plannedBlockCount:
        (group.sequenceBlockCount ?? 1) * (group.setCount ?? 1),
      wasKeptAtWorkoutStart: this.wasSequenceKeptAtWorkoutStart(
        session,
        selectionGroupId,
        root,
      ),
    });
  }

  createCurrentSelectionSnapshots(session) {
    const setCounts = this.getEffectiveSetCounts();
    const finalBlockOrderBySelectionGroupId = new Map();
    for (const group of this.createActiveWorkoutSchedule(setCounts)) {
      finalBlockOrderBySelectionGroupId.set(getSelectionKey(group), group.order);
    }
    return this.getScheduleOrderedPlacements().map((placement) => ({
      selectionGroupId: placement.anchor.id,
      coveredWorkoutGroupIds: [...placement.coveredGroups]
        .sort((left, right) => left.order - right.order)
        .map((group) => group.id),
      rootExerciseId: placement.root.id,
      rootExerciseName: placement.root.name,
      selectionScoreAtStart: this.getSelectionScore(
        placement.root,
        getWorkoutExercisePhase(
          finalBlockOrderBySelectionGroupId.get(placement.anchor.id),
        ),
      ),
      sequenceBlockCount: placement.root.sequenceBlocks.length,
      setCount: Math.max(1, setCounts.get(placement.anchor.id) ?? 1),
      wasKeptAtWorkoutStart: this.wasSequenceKeptAtWorkoutStart(
        session,
        placement.anchor.id,
        placement.root,
      ),
    }));
  }

  wasSequenceKeptAtWorkoutStart(session, selectionGroupId, root) {
    if ((session.keptExerciseRootIdsBySelectionGroupIdAtStart[
      selectionGroupId
    ] ?? []).includes(root.id)) {
      return true;
    }
    return session.initialSelections.some((selection) =>
      selection.selectionGroupId === selectionGroupId &&
      selection.rootExerciseId === root.id &&
      selection.wasKeptAtWorkoutStart);
  }

  finalizeActiveWorkoutSession(status, endedAtUnixMilliseconds = null) {
    const session = this.state.activeWorkoutSession;
    if (!session) {
      return;
    }
    if (status === "InProgress") {
      throw new RangeError("An active workout cannot be finalized as in progress.");
    }

    const endedAt = endedAtUnixMilliseconds ?? this.getCurrentUnixTimeMilliseconds();
    session.endedAtUnixMilliseconds = Math.max(
      session.startedAtUnixMilliseconds,
      endedAt,
    );
    session.status = status;
    const existingIndex = this.state.workoutHistory.findIndex((candidate) =>
      candidate.sessionId === session.sessionId);
    if (existingIndex >= 0) {
      this.state.workoutHistory[existingIndex] = session;
    } else {
      this.state.workoutHistory.push(session);
    }
    this.state.activeWorkoutSession = null;
  }

  resetTransientState() {
    this.state.activeDurationSelectionGroupIds = null;
    this.state.activeSimpleRoundSelectionGroupIds = [];
    this.state.activeWorkoutSession = null;
    this.state.activeWorkoutMinutes = 0;
    this.state.activeWorkoutModifiers = WORKOUT_MODIFIERS.None;
    this.state.activeWorkoutIsLightDay = false;
    this.state.outcomes = {};
    this.state.activeExtraSetSelectionGroupIds = [];
    this.state.activeSetCountsBySelectionGroupId = {};
    this.state.activeSelectionGroupOrder = [];
    this.state.activeModifierRetainedSelectionGroupIds = [];
    this.state.activeModifierProtectedSelectionGroupId = null;
    this.state.activeDirectionPartnerExerciseIds = {};
    this.state.activeFullSideRoundIds = [];
    this.state.workoutCompleted = false;
    this.state.completionAcknowledged = false;
    this.clearPendingMovement();
    this.clearPendingRest();
  }
}

function catalogIdentity(exercise) {
  return `${exercise.name}\u001f${exercise.video}`;
}

function catalogInvalidationIdsSince(
  priorRevision,
  exercises,
  scopedInvalidations,
  excludedRevisions = new Set(),
) {
  const invalidatedExerciseIds = new Set();
  if (priorRevision < LAST_CUMULATIVE_CATALOG_REVISION) {
    for (const exercise of exercises) {
      if (typeof exercise.retiredName === "string" && exercise.retiredName) {
        invalidatedExerciseIds.add(exercise.id);
      }
    }
  }

  for (const [revision, exerciseIds] of scopedInvalidations) {
    if (revision > priorRevision && !excludedRevisions.has(revision)) {
      for (const exerciseId of exerciseIds) {
        invalidatedExerciseIds.add(exerciseId);
      }
    }
  }

  return invalidatedExerciseIds;
}

function isApprovedIdentityPreservingNameChange(exerciseId, previousIdentity, currentExercise) {
  if (!currentExercise) {
    return false;
  }
  // These reviewed replacements introduce a different action at the same ID.
  // Ordinary anatomy and clarity corrections continue to preserve feedback.
  if (SCOPED_SCORE_INVALIDATIONS_BY_REVISION.get(73).has(exerciseId)) {
    return false;
  }
  const separatorIndex = previousIdentity.indexOf("\u001f");
  if (separatorIndex < 0) {
    return false;
  }
  const previousName = previousIdentity.slice(0, separatorIndex);
  if (DISCARDED_EXERCISE_IDENTITY_NAMES.get(exerciseId)?.has(previousName)) {
    return false;
  }
  const previousVideo = previousIdentity.slice(separatorIndex + 1);
  if (previousVideo !== currentExercise.video) {
    return false;
  }

  const usesLegacyTimedSides = [
    "ScreenLeftThenRight",
    "ScreenRightThenLeft",
    "ScreenLeftLeadThenRightLead",
    "ScreenRightLeadThenLeftLead",
  ].includes(currentExercise.sideSequence);
  const timedSideNormalization =
    usesLegacyTimedSides &&
    previousName.startsWith(ALTERNATING_PREFIX) &&
    previousName.slice(ALTERNATING_PREFIX.length) === currentExercise.name;
  const continuousAlternationNormalization =
    CONTINUOUS_ALTERNATION_NORMALIZATION_IDS.has(exerciseId) &&
    !usesLegacyTimedSides &&
    currentExercise.name.startsWith(ALTERNATING_PREFIX) &&
    previousName === currentExercise.name.slice(ALTERNATING_PREFIX.length);
  const correction = APPROVED_EXERCISE_CORRECTIONS.get(exerciseId);
  const additionalCorrectionNames =
    ADDITIONAL_APPROVED_EXERCISE_CORRECTION_NAMES.get(exerciseId);
  const approvedExerciseCorrection =
    correction !== undefined &&
    (previousName === correction[0] ||
      additionalCorrectionNames?.has(previousName) === true) &&
    currentExercise.name === correction[1];

  return (
    timedSideNormalization ||
    continuousAlternationNormalization ||
    approvedExerciseCorrection
  );
}
