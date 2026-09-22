using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed record StoredExerciseSnapshot(string Name, string Video, int Score);

public static class CatalogMigrationRules
{
    private const string AlternatingPrefix = "Alternating ";
    // Revision 73 corrects catalog training claims and complete movement sequences.
    // Revalidate impossible slot choices while preserving exercise feedback.
    public const int CurrentCatalogRevision = 79;
    private const int HardFloorSlipperinessCatalogRevision = 53;
    private const int ReusedShyAuditCatalogRevision = 70;
    private const int LastCumulativeWorkoutStateRevision = 3;

    private static readonly HashSet<int> ReusedShyAuditExerciseIdSet =
    [
        202, 204, 205,
    ];

    // Anatomy migrations revalidate only saved slot preferences containing an
    // exercise whose training claims changed. They never erase the exercise's
    // global or phase score merely because a secondary association was fixed.
    private static readonly IReadOnlyDictionary<int, IReadOnlySet<int>>
        TrainingClaimAssociationChangesByRevision =
            new Dictionary<int, IReadOnlySet<int>>
            {
                [67] = new HashSet<int>
                {
                    31, 41, 56, 94, 95, 98, 99, 100, 113, 114, 115, 118, 119,
                    120, 126, 129, 133, 134, 135, 137, 143, 144, 146, 149, 151,
                    153, 154, 159, 166, 167, 168, 169, 172, 173, 175, 184, 192,
                    195, 196, 201, 212, 217, 218, 230, 231, 232, 237, 242, 245,
                    251, 256, 260, 263, 265, 266, 269, 271, 272, 274, 275, 276,
                    280, 282, 283, 287, 288, 291, 294, 296, 326, 327, 338, 367,
                    393, 396, 403, 406, 428, 429, 430, 431, 432, 433, 434, 435,
                    437, 440, 443, 448, 452, 457, 461, 472, 473, 484, 487, 508,
                    509, 529, 532, 537, 538, 546, 556, 560, 584, 591, 603, 608,
                    611, 613, 616, 617, 620, 632, 636, 647, 654, 681, 685, 701,
                    702, 703, 758, 816, 818, 831, 845, 886, 887, 915, 939, 943,
                    958, 969, 971, 986, 996, 997,
                },
                [69] = new HashSet<int>
                {
                    15, 21, 32, 47, 58, 92, 119, 167, 168, 169, 194, 211, 213,
                    216, 220, 225, 231, 233, 236, 239, 241, 245, 248, 258, 269,
                    274, 277, 282, 283, 285, 286, 290, 291, 294, 295, 296, 326, 327, 390,
                    393, 403, 404, 406, 413, 417, 420, 428, 431, 432, 433, 434,
                    435, 439, 440, 441, 442, 443, 444, 445, 448, 450, 452, 458,
                    462, 463, 464, 465, 469, 471, 472, 475, 476, 488, 523, 524,
                    537, 541, 543, 545, 546, 548, 556, 561, 573, 575, 578, 583, 613,
                    681, 685, 712, 745, 790,
                },
                [73] = new HashSet<int>
                {
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
                },
            };

    private sealed record PriorReviewedReplacementIdentity(
        string Name,
        string BaselineRetiredName);

    private sealed record ApprovedExerciseCorrection(
        string PreviousName,
        string CurrentName);

    private sealed record RestoredReviewedExerciseIdentity(
        string PreviousReplacementName,
        string RestoredName);

    private sealed record DiscardedStoredExerciseIdentity(
        string Name,
        string Video);

    private static readonly IReadOnlyDictionary<int, ApprovedExerciseCorrection>
        ApprovedExerciseCorrections =
            new Dictionary<int, ApprovedExerciseCorrection>
            {
                [31] = new(
                    "High-Knee Overhead-Reach March",
                    "Alternating Knee Raises with Two-Arm Pull-Down"),
                [21] = new(
                    "Standing-Scale Balance",
                    "Alternating Single-Leg Hinge with Forward Reach"),
                [105] = new(
                    "Plie Squat",
                    "Wide Turned-Out Squat"),
                [119] = new(
                    "Squat to Calf Raise",
                    "Tiptoe Walking Back and Forth"),
                [139] = new(
                    "Wide-Squat Heel Raise",
                    "Wide-Squat Alternating Heel Raises"),
                [188] = new(
                    "Parallel Demi-Plie",
                    "Narrow Turned-Out Shallow Squat"),
                [197] = new(
                    "First-Position Plie-Releve",
                    "Squat to Calf Raise"),
                [198] = new(
                    "Second-Position Plie-Releve",
                    "Wide Squat to Feet-Together Calf Raise"),
                [199] = new(
                    "Alternating Deep Side Lunge",
                    "Horse-Stance Squat"),
                [255] = new(
                    "Standing Bent-Knee Calf Raise",
                    "Deep-Squat Calf Raise"),
                [145] = new(
                    "Standing Knee Extension",
                    "Wall-Supported Standing Knee-Extension Hold"),
                [256] = new(
                    "Self-Resisted Overhead Pull",
                    "Self-Resisted Overhead Pull Hold"),
                [257] = new(
                    "Self-Resisted Chest-Level Pull",
                    "Finger Spreading with Arms Held Forward"),
                [258] = new(
                    "Self-Resisted Low Pull",
                    "Alternating Karate Downward Blocks"),
                [262] = new(
                    "Standing Hands-to-Thigh Abdominal Press",
                    "Standing Hands-to-Thigh Abdominal Press Hold"),
                [270] = new(
                    "Bodyweight Svend Press",
                    "Goalpost Arm Hold"),
                [282] = new(
                    "High-Knee Horizontal Punches",
                    "Side-Step Knee Drive with Alternating Side Punches"),
                [219] = new(
                    "Single-Side High-Knee Cross-Body Pull",
                    "High-Knee Cross-Body Pull"),
                [684] = new(
                    "Karate Step-Through Cross-Elbow Strike",
                    "Knee Strike to Horizontal Elbow Strike"),
                [290] = new(
                    "Universe-in-Motion Qigong",
                    "Thumb and Little-Finger Switches"),
                [231] = new(
                    "Karate Reverse Punch",
                    "Step-In Karate Reverse Punch"),
                [394] = new(
                    "Standing Arms Open and Close",
                    "Alternating Cross-Body Knee with Arm Sweep"),
                [395] = new(
                    "Standing Overhead Arm Sweep",
                    "Alternating Knee Lift and Overhead Reach"),
                [397] = new(
                    "Inhale Open, Exhale Cross-Body Side Tap",
                    "Alternating Side Tap with Diagonal Reach"),
                [398] = new(
                    "Standing Hug and Arm Expansion",
                    "Inhale Arms Open, Exhale Arms Together"),
                [399] = new(
                    "Shallow Squat with Chest-Opening Arms",
                    "Inhale Chest Open, Exhale Arms Close with Shallow Squat"),
                [400] = new(
                    "Shallow Squat with Overhead Arm Circle",
                    "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down"),
                [401] = new(
                    "Alternating Weight Shift with Arm Swing",
                    "Alternating Inhale-Twist, Exhale-Push"),
                [402] = new(
                    "Shibashi Rowing-a-Boat Breathing",
                    "Shallow Squat with Rowing Arm Circle"),
                [403] = new(
                    "Shibashi Alternating Pushing-Palms Breathing",
                    "Alternating Palm Press with Weight Shift"),
                [404] = new(
                    "Shibashi Alternating Punch Breathing",
                    "Wide-Stance Alternating Slow Punch"),
                [405] = new(
                    "Shibashi Flying-Wild-Goose Breathing",
                    "Shallow Squat with Wing Arm Raise"),
                [406] = new(
                    "Shibashi Spinning-Wheels Breathing",
                    "Standing Wheel Arm Circles"),
                [409] = new(
                    "Neck Controlled Articular Rotation",
                    "Full Neck Circles"),
                [493] = new(
                    "Track Finger Upper-Right to Lower-Left",
                    "Diagonal Finger Tracking"),
                [958] = new(
                    "Standing Alternating Side Bend",
                    "Standing Overhead Side-Stretch Hold"),
                [425] = new(
                    "Chin-Tuck Isometric",
                    "Chin-Tuck Hold"),
                [396] = new(
                    "Unsupported Single-Leg Balance",
                    "Standing Front-to-Side Knee Lifts"),
                [510] = new(
                    "Clasped-Hands Chest-Opening Forward Fold",
                    "Clasped-Hands Chest-Opening Forward-Fold Hold"),
                [507] = new(
                    "Hamstring Curl with Elbow Pull",
                    "Knee Raise with Elbow Pull"),
                [508] = new(
                    "Wide-Step Elbow Pull",
                    "Alternating Side Taps with Forward Overhead Raises"),
                [588] = new(
                    "Belly-Dance Alternating Shoulder Roll",
                    "Belly-Dance Alternating Shoulder Rolls"),
                [577] = new(
                    "High-Knee Goalpost Pull",
                    "Standing Side Crunch and Side Kick"),
                [915] = new(
                    "Split-Stance Knee Drive with Overhead Reach",
                    "Single-Side Split-Stance Knee Drive with Overhead Reach"),
                [617] = new(
                    "Standing Side-Leg Circles",
                    "Standing Forward Side-Leg Circles"),
                [626] = new(
                    "Sumo Stance",
                    "Sumo Squat Hold"),
                [712] = new(
                    "Standing Arms-Back Chest Opener",
                    "Standing Arms-Back Chest-Opener Hold"),
                [969] = new(
                    "Chair-Pose Core Hold",
                    "Chair-Pose Hold"),
                [1000] = new(
                    "Standing Forward Fold",
                    "Standing Forward-Fold Hold"),
                [136] = new(
                    "Goddess Pose",
                    "Wide Squat Hold with Hands on Thighs"),
                [225] = new(
                    "Clenched-Fist Wrist Extensor Stretch",
                    "Opposite-Hand Fist-Down Wrist Stretch"),
                [241] = new(
                    "Hook-Fist Tendon Glide",
                    "Open Hand to Hook Fist"),
                [242] = new(
                    "Full-Fist Tendon Glide",
                    "Open Hand to Full Fist"),
                [248] = new(
                    "Side-Tap Palm Pushes",
                    "Alternating Side-Tap Palm Pushes"),
                [283] = new(
                    "Straight-Fist Tendon Glide",
                    "Rear-Hand Palm Strike"),
                [291] = new(
                    "Open-to-Claw Tendon Glide",
                    "Open Hand to Claw Fist"),
                [293] = new(
                    "Finger-Web Space Stretch",
                    "Opposite-Hand Thumb-Web Stretch"),
                [683] = new(
                    "Alternating Palm-Up T-Arm Flips",
                    "Alternating Palm-Up Shoulder Rotations"),
                [214] = new(
                    "Forward Wrist Circles",
                    "Single-Arm Wrist Circles"),
                [223] = new(
                    "Forward Controlled Wrist Circles",
                    "Controlled Wrist Circles"),
                [755] = new(
                    "Reverse Wrist Circles",
                    "Outward Wrist Circles"),
                [756] = new(
                    "Reverse Controlled Wrist Circles",
                    "Outward Controlled Wrist Circles"),
                [758] = new(
                    "Reverse Knee-and-Ankle Circles",
                    "Backward Knee-and-Ankle Circles"),
                [94] = new(
                    "Mirror-Guided Lateral Weight Shift",
                    "Lateral Weight Shift"),
                [95] = new(
                    "Mirror-Guided Single-Leg Pelvic Control",
                    "Single-Leg Knee-Raise Hold"),
                [99] = new(
                    "Mirror-Guided Bent-Knee Front-to-Back Leg Swing",
                    "Bent-Knee Front-to-Back Leg Swing"),
                [100] = new(
                    "Mirror-Guided Bent-Knee Leg Swing with Pause",
                    "Bent-Knee Leg Swing with Pause"),
                [497] = new(
                    "Mirror-Guided Eyebrow Raise",
                    "Eyebrow Raise"),
                [498] = new(
                    "Mirror-Guided Firm Eye Closure",
                    "Firm Eye Closure"),
                [500] = new(
                    "Mirror-Guided Straight Jaw Opening",
                    "Straight Jaw Opening"),
                [511] = new(
                    "Mirror-Guided Lip Pucker",
                    "Lip Pucker"),
                [514] = new(
                    "Mirror-Guided Symmetric Smile",
                    "Symmetric Smile"),
                [515] = new(
                    "One-Eyebrow Isolation Practice",
                    "Raise Upper Lip and Scrunch Nose"),
                [522] = new(
                    "Tutting Box Sequence",
                    "Tutting Box Sequence"),
                [523] = new(
                    "Arm-Wave Isolation Practice",
                    "Pass an Arm Wave from Hand to Hand"),
                [524] = new(
                    "Mirror Front Double-Biceps Pose Hold",
                    "Front Double-Biceps Posing"),
                [525] = new(
                    "Mirror Front Lat-Spread Pose Hold",
                    "Front Lat-Spread Posing"),
                [526] = new(
                    "Mirror Side-Chest Pose Hold",
                    "Side-Chest Posing"),
                [527] = new(
                    "Mirror Side-Triceps Pose Hold",
                    "Side-Triceps Posing"),
                [528] = new(
                    "Mirror Abdominals-and-Thighs Pose Hold",
                    "Abdominals-and-Thighs Posing"),
                [790] = new(
                    "Mirror Most-Muscular Pose Hold",
                    "Most-Muscular Posing, Hands on Thighs"),
                [193] = new(
                    "Wide-Squat Floor-to-Overhead Reach",
                    "Hip Hinge with Overhead Reach"),
                [417] = new(
                    "Narrow Squat and Overhead Reach with Thumb Tracking",
                    "Narrow-Stance Overhead-to-Toe Reach"),
                [439] = new(
                    "Feet-Together Fixed-Gaze Head Turns",
                    "Pogo Bounces with Fixed-Gaze Head Turns"),
                [442] = new(
                    "Feet-Together Fixed-Gaze Head Nods",
                    "Pogo Bounces with Fixed-Gaze Head Nods"),
                [444] = new(
                    "Feet-Together Fixed-Gaze Head Tilts",
                    "Pogo Bounces with Fixed-Gaze Head Tilts"),
                [556] = new(
                    "Tiptoe Raises with Fist Clenches",
                    "Single-Arm Backfist"),
                [561] = new(
                    "Tiptoe Bourree Steps with Head Spot",
                    "Tiptoe Turn with Head Spot"),
                [562] = new(
                    "Ballet Rises with Arm Movement",
                    "First-Position Calf Raises"),
                [564] = new(
                    "Calf Raise with Pelvic Floor Contraction",
                    "Parallel Calf Raises with Hands on Hips"),
                [565] = new(
                    "Pelvic-Floor Mini Squat to Calf Raise",
                    "Mini-Squat Calf Raises with Forward Reach"),
                [566] = new(
                    "Parallel Calf Raises for Pelvic-Floor Support",
                    "Parallel Calf Raises"),
                [581] = new(
                    "Toes-In Calf Raises for Pelvic-Floor Support",
                    "Toes-In Calf Raises"),
                [582] = new(
                    "Toes-Out Calf Raises for Pelvic-Floor Support",
                    "Toes-Out Calf Raises"),
                [615] = new(
                    "Hamstring Curl with Prayer Hands",
                    "Alternating Hamstring Curl with Prayer-to-Open Arms"),
                [138] = new(
                    "Narrow-Squat Heel Raise",
                    "Narrow Squat to Calf Raise"),
                [140] = new(
                    "Sumo-Squat Calf Raise",
                    "Tiptoe Sumo Squat"),
                [152] = new(
                    "Side-Leg Raise Pulse",
                    "Alternating Side-Leg Raise Pulses"),
                [390] = new(
                    "Inhale Arms Up, Exhale Step-Touch",
                    "Step-Touch with Goalpost Arm Openings"),
                [391] = new(
                    "Inhale Arms Open, Exhale High-Knee",
                    "Alternating High-Knee Inner-Foot Taps"),
                [408] = new(
                    "Split-Squat Torso Rotation with Thumb Tracking",
                    "Staggered-Stance Torso Rotation with Hand Tracking"),
                [414] = new(
                    "Fixed-Thumb Head Turns",
                    "Tiptoe Fixed-Thumb Head Turns"),
                [415] = new(
                    "Fixed-Thumb Head Nods",
                    "Tiptoe Fixed-Thumb Head Nods"),
                [416] = new(
                    "Fixed-Thumb Head Tilts",
                    "Tiptoe Fixed-Thumb Head Tilts"),
                [531] = new(
                    "Alternating Standing Shoulder CARs",
                    "Single-Arm Shoulder CAR"),
                [533] = new(
                    "Alternating Standing Donkey Kicks",
                    "Standing Bent-Knee Hip Extension"),
                [557] = new(
                    "Tiptoe Forward Punches",
                    "Calf Raise with Double Forward Punch"),
                [618] = new(
                    "Single-Side High-Knee Hold with Side Reach",
                    "Single-Side Knee Raise with Torso Twist"),
                [649] = new(
                    "Standing Bent-Knee Hip Abduction",
                    "Standing Side-Leg Raise with Knee Extension"),
                [677] = new(
                    "Bent-Elbow Reverse-Fly Hold",
                    "Standing Goalpost Arm Hold"),
                [911] = new(
                    "Single-Side Wall Side-Plank Knee Drive",
                    "Wall-Supported High-Knee Raise"),
                [939] = new(
                    "Hinge-to-Knee Drive",
                    "Split-Stance Knee-to-Hand Crunch"),
                [542] = new(
                    "Alternating Standing Bird Dogs",
                    "Standing Bird-Dog Reach"),
                [269] = new(
                    "Standing Leg-Resistance Biceps Curl",
                    "Wall-Supported Leg-Resistance Biceps Curl"),
                [619] = new(
                    "Squat with Forward Scoop",
                    "Mini Squat with Forward Scoop"),
                [228] = new(
                    "Bent-Elbow External Rotation",
                    "Single-Arm Bent-Elbow External Rotation"),
                [274] = new(
                    "Alternating Boxing Uppercuts",
                    "Alternating Standing Uppercut Punches"),
                [285] = new(
                    "Karate Inside Block",
                    "Karate Outward Forearm Block"),
                [286] = new(
                    "Karate Outside Block",
                    "Alternating Karate Inward Forearm Blocks"),
                [541] = new(
                    "Alternating Karate Inside Blocks",
                    "Alternating Karate Cross-Body Forearm Blocks"),
                [545] = new(
                    "Alternating Karate Outside Blocks",
                    "Alternating Karate Outward Forearm Blocks"),
                [112] = new(
                    "Wide Squat with Overhead Reach",
                    "Wide Squat with Arms Held Overhead"),
                [171] = new(
                    "Alternating Hamstring Sweep",
                    "Standing Hamstring Sweep"),
                [748] = new(
                    "Alternating Wide-Stance Groin-Hamstring Shift",
                    "Wide-Stance Groin and Hamstring Stretch"),
                [116] = new(
                    "Alternating Cossack Squat",
                    "Alternating Lateral Squat"),
                [733] = new(
                    "Alternating Side Lunge with Chest Push",
                    "Alternating Side Lunge with Forward Arm Reach"),
                [16] = new(
                    "Split-Stance Toe Raises",
                    "Split-Stance Calf Raises"),
                [17] = new(
                    "Standing Toe-Touch Windmill",
                    "Alternating Toe Touch with Overhead Reach"),
                [19] = new(
                    "Wide Plie Squat Pulses",
                    "Wide Squat with Hands on Hips"),
                [20] = new(
                    "Standing Rear-Leg Pulses",
                    "Standing Rear-Leg Raises"),
                [104] = new(
                    "Sumo Squat",
                    "Sumo Squat with Pause"),
                [32] = new(
                    "Tandem Walk",
                    "Tandem Walk Forward and Back"),
                [97] = new(
                    "Standing Side-Kick Reach",
                    "Knee Raise and Side-Kick Reach"),
                [111] = new(
                    "Rainbow Squat Thruster",
                    "Squat with Overhead Reach and Arm Sweep"),
                [115] = new(
                    "Pistol Squat",
                    "Pistol Squat with Bottom Pause"),
                [125] = new(
                    "Alternating Reverse Lunge with Torso Twist",
                    "Split Squat with Torso Twist"),
                [126] = new(
                    "Squat to Alternating Side Kick",
                    "Squat with Side Kick"),
                [128] = new(
                    "Alternating Curtsy Squat with Arm Sweep",
                    "Curtsy Squat with Arm Sweep"),
                [129] = new(
                    "Squat to Alternating Front Kick",
                    "Squat with Front Kick"),
                [131] = new(
                    "Squat to Alternating Back Leg Lift",
                    "Squat with Rear Leg Lift"),
                [133] = new(
                    "Alternating Lateral Lunge with Overhead Reach",
                    "Lateral Lunge with Overhead Reach"),
                [135] = new(
                    "Overhead Squat Hold",
                    "Wide Overhead Squat Hold"),
                [137] = new(
                    "Wall Squat",
                    "Shallow Wall Squat"),
                [141] = new(
                    "Squat with Alternating Heel Lift",
                    "Squat Hold with Alternating Heel Raises"),
                [144] = new(
                    "Alternating Standing Knee Lift",
                    "Standing Knee Lift"),
                [147] = new(
                    "Squat to Alternating Star Reach",
                    "Shallow Squat with Overhead Reach"),
                [148] = new(
                    "Straight-Leg Hip-Extension Lift",
                    "Bent-Over Standing Bird Dog"),
                [150] = new(
                    "Wide-Squat Side-to-Side Shifts",
                    "Side-to-Side Lunge"),
                [151] = new(
                    "Alternating Straight-Leg Front Raise",
                    "Straight-Leg Front Raise"),
                [154] = new(
                    "Diagonal Leg Raise",
                    "Standing Fire Hydrant"),
                [159] = new(
                    "Standing Hamstring Curl to Diagonal Extension",
                    "Standing Fire-Hydrant Kick"),
                [161] = new(
                    "Alternating Standing Gate Opener",
                    "Standing Gate Opener"),
                [170] = new(
                    "Alternating Cross-Body Knee Drive",
                    "Standing Cross-Body Knee Drive"),
                [173] = new(
                    "Alternating Side Knee Lift",
                    "Standing Side Knee Lift"),
                [178] = new(
                    "Crescent Kick",
                    "Outside Crescent Kick"),
                [179] = new(
                    "Hip-Hinge Rear-Leg Raises",
                    "Standing Rear Leg Raise"),
                [182] = new(
                    "Capoeira Armada de Frente",
                    "Capoeira Meia-Lua de Frente"),
                [184] = new(
                    "Split-Squat Hold",
                    "Split-Squat Pulse"),
                [187] = new(
                    "Standing Heel-and-Toe Raises",
                    "Standing Heel Raise"),
                [201] = new(
                    "Wide-Stance Tiptoe Hold",
                    "Sumo Squat to Calf Raise"),
                [211] = new(
                    "Bent-Elbow Wrist-Flexion Stretch",
                    "Assisted Standing Wrist-Flexion Stretch"),
                [213] = new(
                    "Bent-Elbow Wrist-Extension Stretch",
                    "Assisted Standing Wrist-Extension Stretch"),
                [227] = new(
                    "Rotating Relaxed Arm Swings",
                    "Arm Swings with Trunk Rotation"),
                [236] = new(
                    "Bilateral Wrist Figure Eights",
                    "Bilateral Wrist Circles"),
                [239] = new(
                    "Standing Reverse Prayer Stretch",
                    "Back-of-Hands Wrist Stretch"),
                [245] = new(
                    "Straight-Punch to Shovel-Hook Combo",
                    "Jab-Jab-Cross-Shovel-Hook Combo"),
                [261] = new(
                    "Standing Bent-Elbow Reverse Fly",
                    "Standing Bent-Elbow Chest Squeeze"),
                [266] = new(
                    "T-Arm Shoulder Hold",
                    "Alternating Overhead Arm Raises"),
                [268] = new(
                    "Goalpost-to-T Rotations",
                    "Goalpost-to-T Arm Extensions"),
                [271] = new(
                    "Standing Lumbar Extension",
                    "Standing Lumbar Extension Hold"),
                [273] = new(
                    "Bent-Knee Calf-Raise Hold",
                    "Wall-Supported Bent-Knee Calf Raise Hold"),
                [280] = new(
                    "Alternating Boxing Hook Punches",
                    "Rear-Hand Boxing Hook"),
                [288] = new(
                    "Forward Knee-and-Ankle Circles",
                    "Standing Knee-and-Ankle Circles"),
                [289] = new(
                    "Fingertip Spider Presses",
                    "Fist Opening and Closing with Arms Held Forward"),
                [294] = new(
                    "Outward Knife-Hand Strikes",
                    "Rear-Hand Outward Knife-Hand Strike"),
                [295] = new(
                    "Ankle Squat March",
                    "Alternating Bent-Knee Heel Raises"),
                [301] = new(
                    "Overhead Arm Pumps",
                    "Standing Side Arm Raises"),
                [309] = new(
                    "Self-Resisted Neck Rotation Isometric",
                    "Self-Resisted Neck Rotation Hold with Head Turned"),
                [321] = new(
                    "Side-Tap Alternating Arm Raises",
                    "Alternating Side Taps with Overhead Arm Raises"),
                [379] = new(
                    "Alternating Qigong Drawing the Bow",
                    "Qigong Drawing the Bow"),
                [389] = new(
                    "First-Position Heel Raise with Elbow Pull-Down",
                    "Toes-Out Heel Raise with Elbow Pull-Down"),
                [418] = new(
                    "Alternating-Thumb Head Turns",
                    "Look Between Fingers with Head Turns"),
                [419] = new(
                    "Vertical Thumb Tracking with Head Nods",
                    "Track Thumb While Nodding the Opposite Way"),
                [427] = new(
                    "Split Jacks",
                    "Jumping Lunges with Alternating Overhead Reach"),
                [437] = new(
                    "Alternating High-Knee Under-Thigh Claps",
                    "Hopping High-Knee Under-Thigh Claps"),
                [467] = new(
                    "Look Up and Down",
                    "Standing Chin-to-Chest Nods"),
                [470] = new(
                    "Windmill Jacks",
                    "Alternating Windmill Toe Touch"),
                [473] = new(
                    "Bouncing Uppercuts",
                    "Boxing Lead-Hand Parry"),
                [474] = new(
                    "Head Glide Forward and Back",
                    "Chin Tucks with Hands Pulling Forward at Base of Neck"),
                [484] = new(
                    "Goddess Squat with Lion's Breath",
                    "Wide Squat with Tongue-Out Exhale"),
                [485] = new(
                    "Cupped-Palm Armpit Tapping",
                    "Alternating Back Taps with Rowing Arms"),
                [486] = new(
                    "Bent-Over Back-of-Knee Tapping",
                    "Alternating Knee Lift with Two-Arm Pull-Down"),
                [490] = new(
                    "Track One Thumb Side to Side",
                    "Track Finger Side to Side, Head Still"),
                [491] = new(
                    "Keep Eyes on Thumb While Nodding",
                    "Keep Eyes on Finger While Nodding"),
                [501] = new(
                    "Keep Eyes on Thumb While Turning Head",
                    "Keep Eyes on Finger While Turning Head"),
                [505] = new(
                    "Jaw Side-to-Side Glides",
                    "Jaw Side Glide and Relax"),
                [519] = new(
                    "Standing Yes Pullbacks",
                    "Push Forearm Outward Against Other Hand"),
                [520] = new(
                    "Mirror Facial-Expression Practice",
                    "Raise Arms, Exhale Choo as Hips Sink"),
                [521] = new(
                    "Smile at Yourself in the Mirror",
                    "Stretch and Scrunch Face"),
                [538] = new(
                    "Alternating Reverse Lunge to Front Kicks",
                    "Reverse Lunge with Front Kick"),
                [547] = new(
                    "Alternating Standing Rotation Claps",
                    "Standing Rotational Reach and Clap"),
                [554] = new(
                    "Half Squat Arm Swing to Heel Raise",
                    "Arm Raises in Shallow Squat"),
                [560] = new(
                    "Tiptoe Forward-and-Back Torso-and-Arm Sweep",
                    "Try to Turn Palm Up Against Other Hand"),
                [569] = new(
                    "Tiptoe Hip Hinge",
                    "Unsupported Single-Leg Straight-Knee Calf Raise"),
                [570] = new(
                    "Tiptoe Torso Twists",
                    "Calf Raise with Arm Reach and Fist Close"),
                [614] = new(
                    "Alternating Reinforced Forearm Blocks",
                    "Push Forearm Down Against Other Hand"),
                [633] = new(
                    "Wall Calf Stretch",
                    "Straight-Knee Toes-on-Wall Stretch"),
                [636] = new(
                    "Curtsy-Lunge Hold",
                    "Curtsy Lunge to Alternating Side Crunch"),
                [647] = new(
                    "Alternating Knee Lift with Overhead Reach",
                    "Alternating Heel Curl with Overhead Reach"),
                [654] = new(
                    "Single-Side Leg Lift to Overhead Knee Drive",
                    "Alternating Side Leg Lifts and Heel Curls"),
                [678] = new(
                    "Overhead Arm Figure Eight",
                    "Overhead Arm Sways"),
                [681] = new(
                    "Muay Thai Downward Elbow Strike",
                    "Downward Elbow Strike"),
                [751] = new(
                    "Neck Side Stretch",
                    "Standing Neck Half Rolls"),
                [816] = new(
                    "Side-Step Overhead Reach",
                    "Low-Impact Side-Step Jacks"),
                [836] = new(
                    "Squat with Back Squeeze",
                    "Alternating Side-Step Squat with Back Squeeze"),
                [845] = new(
                    "Overhead Side Stretch",
                    "Overhead Side-Bend Hold"),
                [913] = new(
                    "Wall-Supported Vertical Dead Bug",
                    "Shallow Wall Sit with Overhead Arm Raises"),
                [914] = new(
                    "Alternating Diagonal Knee Pull-Down",
                    "Single-Side Diagonal Knee Pull-Down"),
                [919] = new(
                    "March in Place with Fixed-Gaze Head Turns",
                    "Marching in Place with Head Turns"),
                [949] = new(
                    "Standing Reverse Wood Chop",
                    "Standing Low-to-High Wood Chop"),
                [962] = new(
                    "Alternating Standing Knee-to-Elbow Crunch",
                    "Alternating Standing Knee Taps"),
                [993] = new(
                    "Mirror Standing Vacuum Repetitions",
                    "Standing Stomach Vacuum, Then Release"),
            };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<string>>
        AdditionalApprovedExerciseCorrectionPreviousNames =
            new Dictionary<int, IReadOnlySet<string>>
            {
                [31] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Knee Raise with Overhead Reach",
                    "Single-Side Knee Raise with Two-Arm Pull-Down",
                },
                [524] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Front Double-Biceps Pose Hold",
                    "Mirror Front Double-Biceps Posing",
                },
                [525] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Front Lat-Spread Pose Hold",
                    "Mirror Front Lat-Spread Posing",
                },
                [526] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Side-Chest Pose Hold",
                    "Mirror Side-Chest Posing",
                },
                [527] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Side-Triceps Pose Hold",
                    "Mirror Side-Triceps Posing",
                },
                [528] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Abdominals-and-Thighs Pose Hold",
                    "Mirror Abdominals-and-Thighs Posing",
                },
                [565] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Mini Squat with Forward Reach",
                },
                [21] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Standing-Scale Balance",
                    "Standing-Scale Balance Hold",
                },
                [145] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Standing Knee Extension",
                    "Standing Knee-Extension Hold",
                },
                [231] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Karate Reverse Punch",
                    "Step-Through Karate Reverse Punch",
                },
                [394] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Open-and-Close Breathing",
                    "Inhale Open, Exhale Cross-Body Knee",
                    "Inhale Arms Open, Exhale Arms Close and Round",
                },
                [395] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Overhead Rib-Expansion Breathing",
                    "Single-Side Inhale Reach Up, Exhale Knee Lift",
                    "Overhead Hold with Deep Ribcage Breaths",
                    "Single-Side Knee Lift with Overhead Reach",
                },
                [500] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Controlled Jaw Open and Close",
                },
                [398] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Arm-Expansion Breathing",
                    "Inhale Arms Open, Exhale Self-Hug and Fold",
                },
                [399] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Shibashi Opening-the-Chest Breathing",
                },
                [400] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Shibashi Separating-the-Clouds Breathing",
                },
                [401] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Shibashi Alternating Swinging-Arms Breathing",
                },
                [617] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Standing Side-Leg Circles",
                },
                [95] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Single-Leg Pelvic Control",
                },
                [270] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Goalpost Chest-Opener Hold",
                    "Palm-Squeeze Forward Press",
                },
                [556] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Backfists",
                    "Standing Fist Clench and Release",
                },
                [615] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Hamstring Curls with Prayer Hands",
                    "Heel Raise with Prayer-to-Open Arms",
                },
                [119] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Tiptoe Walk",
                },
                [136] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Wide Turned-Out Squat Hold",
                },
                [193] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Wide-Stance Floor-to-Overhead Reach",
                },
                [197] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Parallel Squat-to-Calf Raise",
                },
                [199] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Wide-Stance Side-to-Side Squat",
                },
                [214] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Inward Wrist Circles",
                },
                [219] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating High-Knee Cross-Body Pull",
                },
                [223] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Inward Controlled Wrist Circles",
                },
                [257] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Finger Spread to Interlace Stretch",
                    "Self-Resisted Chest-Level Pull Hold",
                },
                [258] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Karate Downward Block",
                    "Self-Resisted Low Pull Hold",
                },
                [283] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Palm Strikes",
                    "Open Hand to Straight Fist",
                },
                [290] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Low Palm Scoop to Side Opening",
                },
                [293] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand Finger-Web Stretches",
                },
                [390] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Step-Touch with Overhead Arm Arcs",
                },
                [391] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating High Knee with Arm Opening",
                },
                [396] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Single-Leg Knee-Lift Balance Hold",
                    "Unsupported Single-Leg Balance Hold",
                },
                [397] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Side Tap with Diagonal Arm Sweep",
                },
                [403] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Alternating Weight Shift with Palm Push",
                },
                [408] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Split-Squat Torso Rotation with Hand Tracking",
                },
                [417] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Narrow-Stance Overhead-to-Floor Reach",
                },
                [508] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Side-Step with Two-Arm Overhead Reach",
                },
                [515] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Mirror One-Eyebrow Isolation Practice",
                },
                [522] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Mirror Tutting Box Sequence",
                },
                [523] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Mirror Arm-Wave Isolation Practice",
                },
                [561] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Tiptoe Running Steps with Head Spot",
                },
                [562] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Ballet Calf Raises with Arm Sweeps",
                },
                [577] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Single-Side Standing Side-Leg Raise with Side Reach",
                    "Standing Side-Leg Raise with Side Reach",
                },
                [618] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Single-Side High-Knee Raise with Side Reach",
                },
                [790] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Mirror Most-Muscular Posing",
                },
                [939] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Split-Stance Knee-to-Hand Press",
                },
                [958] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Overhead Side Bend",
                },
            };

    private static readonly IReadOnlyDictionary<int, RestoredReviewedExerciseIdentity>
        RestoredReviewedExerciseIdentities =
            new Dictionary<int, RestoredReviewedExerciseIdentity>
            {
                [266] = new(
                    "Zyzz Diagonal-Reach Pose Hold",
                    "Standing Palms-Up Arm Raise"),
            };

    // Reused IDs can still contain an exact obsolete identity on devices that
    // jump directly from an older catalog. Accept only these reviewed pairs so
    // the old row and score are discarded before the new exercise is inserted.
    private static readonly IReadOnlyDictionary<int,
        IReadOnlySet<DiscardedStoredExerciseIdentity>>
        DiscardedStoredExerciseIdentities =
            new Dictionary<int, IReadOnlySet<DiscardedStoredExerciseIdentity>>
            {
                [257] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Self-Resisted Chest-Level Pull", "exercise_videos/exercise_0257.mp4"),
                    new("Self-Resisted Chest-Level Pull Hold", "exercise_videos/exercise_0257.mp4"),
                },
                [425] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Chin-Tuck Hold", "exercise_videos/exercise_0425.mp4"),
                    new("Chin-Tuck Isometric", "exercise_videos/exercise_0425.mp4"),
                },
                [395] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Overhead Hold with Deep Ribcage Breaths", "exercise_videos/exercise_0395.mp4"),
                    new("Standing Overhead Arm Sweep", "exercise_videos/exercise_0395.mp4"),
                    new("Standing Overhead Rib-Expansion Breathing", "exercise_videos/exercise_0395.mp4"),
                },
                [394] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Inhale Arms Open, Exhale Arms Close and Round", "exercise_videos/exercise_0394.mp4"),
                    new("Standing Arms Open and Close", "exercise_videos/exercise_0394.mp4"),
                    new("Standing Open-and-Close Breathing", "exercise_videos/exercise_0394.mp4"),
                },
                [270] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Palm-Squeeze Forward Press", "exercise_videos/exercise_0270.mp4"),
                    new("Bodyweight Svend Press", "exercise_videos/exercise_0270.mp4"),
                },
                [262] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Standing Scapular Depression", "exercise_videos/exercise_0262.mp4"),
                    new("Standing Hands-to-Thigh Abdominal Press", "exercise_videos/exercise_0262.mp4"),
                },
                [258] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Self-Resisted Low Pull Hold", "exercise_videos/exercise_0258.mp4"),
                    new("Self-Resisted Low Pull", "exercise_videos/exercise_0258.mp4"),
                },
                [256] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Self-Resisted Overhead Pull Hold", "exercise_videos/exercise_0256.mp4"),
                    new("Self-Resisted Overhead Pull", "exercise_videos/exercise_0256.mp4"),
                },
                [202] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Hook Fist",
                        "exercise_videos/exercise_0202.mp4"),
                    new(
                        "Hook Fist",
                        "exercise_gifs/exercise_0202.gif"),
                    new(
                        "Finger Fan and Close — Four-Count Tempo",
                        "exercise_gifs/exercise_0202.gif"),
                },
                [204] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Full Fist",
                        "exercise_videos/exercise_0204.mp4"),
                    new(
                        "Full Fist",
                        "exercise_gifs/exercise_0204.gif"),
                    new(
                        "Finger Fan and Close — Half Range",
                        "exercise_gifs/exercise_0204.gif"),
                },
                [205] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Tabletop Fist",
                        "exercise_videos/exercise_0205.mp4"),
                    new(
                        "Tabletop Fist",
                        "exercise_gifs/exercise_0205.gif"),
                    new(
                        "Finger Fan and Close — Full Range",
                        "exercise_gifs/exercise_0205.gif"),
                },
                [218] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Sequential Finger Waves",
                        "exercise_videos/exercise_0218.mp4"),
                    new(
                        "Cumbia Two-Step",
                        "exercise_videos/exercise_0218.mp4"),
                },
                [234] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Straight Fingers to Knuckle Bend",
                        "exercise_videos/exercise_0234.mp4"),
                    new(
                        "Merengue Six-Count Step",
                        "exercise_videos/exercise_0234.mp4"),
                },
                [237] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Sequential Finger Curl Waves",
                        "exercise_videos/exercise_0237.mp4"),
                    new(
                        "Salsa Front-and-Back Basic",
                        "exercise_videos/exercise_0237.mp4"),
                },
                [239] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Tabletop Tendon Glide",
                        "exercise_videos/exercise_0239.mp4"),
                    new(
                        "Reggaeton Single-Single-Double Step",
                        "exercise_videos/exercise_0239.mp4"),
                },
                [240] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Hook Fingers to Full Fist",
                        "exercise_videos/exercise_0240.mp4"),
                },
                [241] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Ninja Water-Dragon 44 Hand-Seal Sequence", "exercise_videos/exercise_0241.mp4"),
                    new("Hook-Fist Tendon Glide", "exercise_videos/exercise_0241.mp4"),
                    new(
                        "Open Hand to Hook Fist",
                        "exercise_videos/exercise_0241.mp4"),
                    new(
                        "Basic Mambo Step",
                        "exercise_videos/exercise_0241.mp4"),
                },
                [242] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Ninja Shadow-Clone Hand-Seal Sequence", "exercise_videos/exercise_0242.mp4"),
                    new("Full-Fist Tendon Glide", "exercise_videos/exercise_0242.mp4"),
                    new(
                        "Open Hand to Full Fist",
                        "exercise_videos/exercise_0242.mp4"),
                },
                [283] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Qigong Fist Rotation", "exercise_videos/exercise_0283.mp4"),
                    new("Straight-Fist Tendon Glide", "exercise_videos/exercise_0283.mp4"),
                    new(
                        "Open Hand to Straight Fist",
                        "exercise_videos/exercise_0283.mp4"),
                    new(
                        "Cha-Cha Basic Step",
                        "exercise_videos/exercise_0283.mp4"),
                },
                [291] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new("Black Dragon Enters the Cave", "exercise_videos/exercise_0291.mp4"),
                    new("Open-to-Claw Tendon Glide", "exercise_videos/exercise_0291.mp4"),
                    new(
                        "Open Hand to Claw Fist",
                        "exercise_videos/exercise_0291.mp4"),
                    new(
                        "Bachata Side-to-Side Basic",
                        "exercise_videos/exercise_0291.mp4"),
                },
                [294] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Five-Position Tendon Glide",
                        "exercise_videos/exercise_0294.mp4"),
                },
                [556] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Standing Fist Clench and Release",
                        "exercise_videos/exercise_0556.mp4"),
                    new(
                        "Pony Step",
                        "exercise_videos/exercise_0556.mp4"),
                },
                [497] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Forehead Finger Sweep",
                        "exercise_videos/exercise_0497.mp4"),
                    new(
                        "Odissi Sundari Griva",
                        "exercise_direction_videos/exercise_0497.mp4"),
                    new(
                        "Track Finger in Circles",
                        "exercise_direction_videos/exercise_0497.mp4"),
                },
                [563] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Single-Leg Calf Raise with Head Turns",
                        "exercise_videos/exercise_0563.mp4"),
                },
                [564] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Parallel Calf Raises with Hands on Hips",
                        "exercise_videos/exercise_0564.mp4"),
                },
                [567] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Breathing Calf Raises with Arm Folds",
                        "exercise_videos/exercise_0567.mp4"),
                },
                [568] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Chest-Expansion Breathing Calf Raises",
                        "exercise_videos/exercise_0568.mp4"),
                },
                [574] = new HashSet<DiscardedStoredExerciseIdentity>
                {
                    new(
                        "Tiptoe Overhead Side Bends",
                        "exercise_videos/exercise_0574.mp4"),
                },
            };

    private static readonly IReadOnlyDictionary<int, PriorReviewedReplacementIdentity>
        PriorReviewedReplacementIdentities =
            new Dictionary<int, PriorReviewedReplacementIdentity>
            {
                [287] = new(
                    "Standing Uppercuts",
                    "Self-Resisted Reverse Curl"),
                [31] = new(
                    "Knee Raise with Overhead Reach",
                    "Tai Chi Golden-Rooster Balance Drill"),
                [439] = new(
                    "Pogo Bounces with Fixed-Gaze Head Turns",
                    "Bidirectional Triangle-Path Saccades"),
                [442] = new(
                    "Pogo Bounces with Fixed-Gaze Head Nods",
                    "Near-Point Convergence"),
                [444] = new(
                    "Pogo Bounces with Fixed-Gaze Head Tilts",
                    "Vertical Gaze Stabilization"),
                [478] = new(
                    "Eye-Tracking Rotational Jumps",
                    "Dance Head Accent Front"),
                [219] = new(
                    "High-Knee Cross-Body Pull",
                    "Four-Way Self-Resisted Wrist Sequence"),
                [395] = new(
                    "Inhale Reach Up, Exhale Knee Lift",
                    "Overhead Hold with Deep Ribcage Breaths"),
                [507] = new(
                    "Knee Raise with Elbow Pull",
                    "Firm Eye Close and Full Open"),
                [577] = new(
                    "Standing Side-Leg Raise with Side Reach",
                    "Qigong Swimming-Dragon Shoulder Roll"),
                [618] = new(
                    "High-Knee Side Reach",
                    "Pelvic-Floor Windmill"),
                [654] = new(
                    "Side Leg Lift to Overhead Knee Drive",
                    "Sideward-and-Backward Kick"),
                [834] = new(
                    "Diagonal Knee Drive with Overhead Pull",
                    "Alternating Cross-Step Lat Pulldown"),
                [135] = new(
                    "Standing Snow Angels",
                    "Mountain Pose to Upward Salute"),
                [195] = new(
                    "Lateral Lunge to Balance",
                    "Ballet Degage a la Seconde"),
                [201] = new(
                    "Shibashi Split-Stance Rock and Palm Press",
                    "Alternating Boxing Jab"),
                [211] = new(
                    "Open-Finger Wrist Extension",
                    "Karate Backfist Strike (Uraken-Uchi)"),
                [212] = new(
                    "Bent-Over Triceps Pulse",
                    "Karate Palm-Heel Strike (Teisho)"),
                [213] = new(
                    "Open-Finger Wrist Flexion",
                    "Karate Hammer-Fist Strike (Tetsui-Uchi)"),
                [214] = new(
                    "Neutral-Fist Wrist Flexion and Extension",
                    "Wing Chun Biu-Sau Palm Strike"),
                [215] = new(
                    "Up-and-Down Wrist Glides",
                    "Self-Resisted Wrist Radial-Deviation Pulses"),
                [216] = new(
                    "Side-to-Side Wrist Glides",
                    "Self-Resisted Wrist Ulnar-Deviation Pulses"),
                [217] = new(
                    "Bilateral Wrist Figure Eights",
                    "Self-Resisted Wrist-Extension Pulses"),
                [218] = new(
                    "Hook-to-Fist Tendon Glides",
                    "Self-Resisted Wrist-Flexion Pulses"),
                [223] = new(
                    "Self-Resisted Forearm Supination Hold",
                    "Alternating Karate Inside Block (Uchi-Uke)"),
                [224] = new(
                    "Opposite-Hand-Resisted Multi-Direction Wrist Hold",
                    "Alternating Karate Downward Sweep Block (Gedan-Barai)"),
                [229] = new(
                    "Standing Elbow-Squeeze Chest Press",
                    "Alternating Boxing Uppercut"),
                [232] = new(
                    "Palms-Down Fist Wrist Flexion and Extension",
                    "Karate Knife-Hand Chop"),
                [233] = new(
                    "Bilateral Wrist Circles",
                    "Karate Ridge-Hand Strike (Haito-Uchi)"),
                [234] = new(
                    "Opposite-Hand-Resisted Thumb Opposition Hold",
                    "Karate Flat-Fist Strike (Hiraken)"),
                [236] = new(
                    "Alternating Hand Open and Close",
                    "Karate Spear-Hand Strike (Nukite)"),
                [237] = new(
                    "Opposed Thumb-and-Index Extension Isometric",
                    "Forearm Pronation and Supination"),
                [239] = new(
                    "Self-Resisted Finger Spread",
                    "Ninja Fireball Hand-Seal Sequence"),
                [240] = new(
                    "Self-Resisted Finger Squeeze",
                    "Ninja Shadow-Possession Hand-Seal Sequence"),
                [241] = new(
                    "Ninja Monkey Hand-Seal Hold",
                    "Ninja Water-Dragon 44 Hand-Seal Sequence"),
                [242] = new(
                    "Ninja Boar Hand-Seal Hold",
                    "Ninja Shadow-Clone Hand-Seal Sequence"),
                [245] = new(
                    "Opposite-Hand-Resisted Elbow-Flexion Hold",
                    "Alternating Karate Rising Block (Age-Uke)"),
                [251] = new(
                    "Arm Sweep to Forward Hinge",
                    "Waiter's Bow"),
                [256] = new(
                    "Bent-Over Straight-Arm Lat Sweeps",
                    "Self-Resisted Overhead Pull Hold"),
                [257] = new(
                    "Karate Knife-Hand Block",
                    "Self-Resisted Chest-Level Pull Hold"),
                [260] = new(
                    "Standing Triceps Kickbacks",
                    "Behind-the-Back Self-Resisted Press"),
                [266] = new(
                    "Alternating T-Arm Lifts",
                    "Standing Palms-Up Arm Raise"),
                [267] = new(
                    "Floor Touch to Calf Raise",
                    "T-Position Shoulder Rotation"),
                [268] = new(
                    "Self-Resisted External-Rotation Push-Out",
                    "Self-Resisted External-Rotation Isometric"),
                [269] = new(
                    "C-Rotation Arm Curls",
                    "Self-Resisted Curl-and-Press"),
                [270] = new(
                    "Goalpost Elbow Open-and-Close",
                    "Palm-Squeeze Forward Press"),
                [274] = new(
                    "Side-Step Alternating High Curl",
                    "Dynamic-Resistance Lat Pulldown"),
                [276] = new(
                    "Alternating Diagonal Overhead Reach-and-Pull",
                    "Dynamic-Resistance High Chest Press"),
                [280] = new(
                    "Alternating Forward-and-Side Arm Press",
                    "Ringing-the-Towel Wrist Inversion"),
                [283] = new(
                    "Sequential Finger Waves",
                    "Qigong Fist Rotation"),
                [289] = new(
                    "Ninja Horse Hand-Seal Hold",
                    "Heaven-to-Earth Finger Rotation"),
                [291] = new(
                    "Ninja Tiger Hand-Seal Hold",
                    "Black Dragon Enters the Cave"),
                [293] = new(
                    "Ninja Dragon Hand-Seal Hold",
                    "Sword-Fingers Qigong Sequence"),
                [294] = new(
                    "Ninja Rat Hand-Seal Hold",
                    "Tiger-Claw Grip Flow"),
                [414] = new(
                    "Heel Raises with Fixed-Thumb Head Turns",
                    "Ear-to-Shoulder Glide"),
                [415] = new(
                    "Heel Raises with Fixed-Thumb Head Nods",
                    "Chin-to-Collarbone Turn"),
                [416] = new(
                    "Heel Raises with Fixed-Thumb Head Tilts",
                    "Diagonal Head Tilt"),
                [418] = new(
                    "Heel-Bounce Horizontal Thumb Tracking",
                    "Forward-and-Back Head Translation"),
                [419] = new(
                    "Heel-Bounce Vertical Thumb Tracking",
                    "Occipital Nod"),
                [482] = new(
                    "Front Half Neck Circles",
                    "Continuous Spot-Turn Drill"),
                [483] = new(
                    "Clockwise Full Neck Circles",
                    "Pirouette Spotting Drill"),
                [490] = new(
                    "Assisted Cheek Lift",
                    "Bharatanatyam Alolita Shiro"),
                [491] = new(
                    "Cheek-Firming Air Hold",
                    "Bharatanatyam Dhuta Shiro"),
                [492] = new(
                    "Forehead Knuckle Massage",
                    "Bharatanatyam Kampita Shiro"),
                [493] = new(
                    "Face-and-Neck Lymphatic Sweep",
                    "Alternating Bharatanatyam Paravritta Shiro"),
                [495] = new(
                    "Jawline Knuckle Massage",
                    "Bharatanatyam Parivahita Shiro"),
                [497] = new(
                    "Forehead Finger Sweep",
                    "Odissi Sundari Griva"),
                [499] = new(
                    "Eyebrow Pinch Massage",
                    "Bharatanatyam Tiraschina Griva"),
                [500] = new(
                    "Eye-Socket Finger Circles",
                    "Bharatanatyam Parivartita Griva"),
                [501] = new(
                    "Counterclockwise Full Neck Circles",
                    "Standing Horizontal Saccades"),
                [505] = new(
                    "Temple Circle Massage",
                    "Maximal Smile and Relax"),
                [506] = new(
                    "Cheek Pinch Massage",
                    "Eyebrow Raise and Relax"),
                [508] = new(
                    "Diagonal Arm Reach-to-Row",
                    "Tongue Protrusion and Retraction"),
                [512] = new(
                    "Upper-Cervical Erector Stretch",
                    "Scapular Protraction"),
                [513] = new(
                    "Standing Unilateral SCM Stretch",
                    "Scapular Retraction"),
                [520] = new(
                    "Silent Vowel-Shape Sequence",
                    "Scapular Clock"),
                [521] = new(
                    "Smile-to-Neutral Transitions",
                    "Scapular Figure Eight"),
                [572] = new(
                    "Wide-Stance Bent-Knee Rotational Stretch",
                    "Tai Chi White Crane Opens Wings"),
                [591] = new(
                    "Standing Speed-Bag Punches",
                    "Bharatanatyam Natyarambhe Hold"),
                [611] = new(
                    "Warrior II-Stance Hip Circles",
                    "Pelvic-Floor Heel-Raise Lift"),
                [636] = new(
                    "Alternating Curtsy Floor Reach",
                    "Deadlift Kickback"),
                [649] = new(
                    "Standing Clamshell",
                    "Standing Side-Leg Raise"),
                [677] = new(
                    "T-Arm Side-to-Side Sweep",
                    "Alternating Belly-Dance Hip Drop"),
                [681] = new(
                    "Rear-Arm Sweep to Front Squeeze",
                    "Belly-Dance Horizontal Figure Eight"),
                [687] = new(
                    "Karate Middle Side Punch",
                    "Belly-Dance Hip Shimmy"),
                [743] = new(
                    "Standing Backward Arm Circles",
                    "Clasped-Hands-Behind-Back Chest Opener"),
                [745] = new(
                    "Standing Overhead Presses",
                    "Dynamic Hug"),
                [843] = new(
                    "Behind-Back Wrist-Pull Neck Stretch",
                    "Standing Cobra Pose"),
            };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<string>>
        AdditionalPriorReviewedReplacementNames =
            new Dictionary<int, IReadOnlySet<string>>
            {
                [211] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand-Resisted Wrist Extension Hold",
                    "Assisted Wrist Flexion-Extension Glides",
                },
                [213] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand-Resisted Wrist Flexion Hold",
                    "Assisted Side-to-Side Wrist Glides",
                },
                [214] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand-Resisted Wrist Ulnar-Deviation Hold",
                },
                [215] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand-Resisted Wrist Radial-Deviation Hold",
                },
                [218] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand-Resisted Little-Finger Abduction Hold",
                },
                [234] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Palms-Up Fist Wrist Flexion and Extension",
                    "Alternating Thumb-to-Palm Tucks",
                },
                [239] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Ninja Snake Hand-Seal Hold",
                },
                [240] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Ninja Ram Hand-Seal Hold",
                },
                [241] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb C Hold",
                    "Straight-Hand Knuckle-Bend Flow",
                    "Opposite-Hand-Resisted Thumb Adduction Hold",
                },
                [242] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Five-Fingertip Press Isometric",
                },
                [236] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand-Resisted Thumb Extension Hold",
                },
                [283] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Opposite-Hand-Resisted Thumb Abduction Hold",
                },
                [289] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Adduction Hold",
                    "Alternating Thumb-to-Palm Tucks",
                    "Opposite-Hand-Resisted Thumb Flexion Hold",
                    "Thumb-to-Fingertip Opposition",
                },
                [291] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Abduction Hold",
                },
                [293] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Thumb Flexion Hold",
                },
                [251] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Swan-Dive Hinge",
                },
                [294] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Self-Resisted Little-Finger Abduction Hold",
                },
                [483] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Clockwise-First Full Neck Circles",
                },
                [501] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Counterclockwise-First Full Neck Circles",
                    "Single-Leg Thumb-Focus Head Turns",
                },
                [507] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Single-Side Knee Raise with Elbow Pull",
                },
                [513] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Single-Leg Thumb-Focus Head Nods",
                },
                [843] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Standing Scalene Wrist-Anchor Stretch",
                },
                [572] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Cossack Side-to-Side Shifts",
                },
            };

    private static readonly IReadOnlyDictionary<int, ApprovedExerciseCorrection>
        DirectionSplitPreviousIdentities =
            new Dictionary<int, ApprovedExerciseCorrection>
            {
                [214] = new("Wrist Circles", "Inward Wrist Circles"),
                [223] = new(
                    "Controlled Wrist Circles",
                    "Inward Controlled Wrist Circles"),
                [264] = new(
                    "Standing Arm Circles",
                    "Backward Standing Arm Circles"),
                [288] = new(
                    "Knee-and-Ankle Circles",
                    "Forward Knee-and-Ankle Circles"),
                [406] = new(
                    "Standing Wheel Arm Circles",
                    "Clockwise Standing Wheel Arm Circles"),
                [409] = new("Full Neck Circles", "Clockwise Full Neck Circles"),
                [588] = new(
                    "Belly-Dance Alternating Shoulder Rolls",
                    "Backward Belly-Dance Alternating Shoulder Rolls"),
                [608] = new("Hip Circle", "Counterclockwise Hip Circles"),
                [611] = new(
                    "Wide-Stance Hip Circles",
                    "Counterclockwise Wide-Stance Hip Circles"),
                [743] = new(
                    "Standing Large Arm Circles",
                    "Backward Standing Large Arm Circles"),
            };

    private static readonly IReadOnlyDictionary<int, string>
        CatalogClarityResetPreviousNames =
            new Dictionary<int, string>
            {
                [15] = "Skater-RDL Balance",
                [16] = "Star-Tap Balance",
                [17] = "Single-Leg RDL-Rotation Balance",
                [19] = "Side-to-Side Pendulum Balance",
                [20] = "Front-to-Back Pendulum Balance",
                [31] = "Tai Chi Golden-Rooster Balance Drill",
                [47] = "Walking with Horizontal Head Turns",
                [97] = "Heel-to-Toe Balance Rocks",
                [107] = "Half Squat",
                [135] = "Standing Lateral Arm Pulses",
                [150] = "Cross-Body Hip-Adduction Sweep",
                [169] = "Standing Knee Drive",
                [179] = "Axe-Kick Leg Raise",
                [180] = "Karate Front Snap Kick",
                [193] = "Shibashi Forward Scoop to Overhead Reach",
                [219] = "Shibashi Soft-Knee Palm Press-Down",
                [220] = "Interlocked Hook-Fist Pull-Apart Isometric",
                [229] = "Overhead Palm-Press Hold",
                [230] = "Low Palm-Press Hold",
                [239] = "Straight-Finger Knuckle Bends",
                [241] = "Flamenco Wrist Circles",
                [242] = "Five-Position Hand Flow",
                [248] = "Standing Palm-Press Hold",
                [251] = "Waiter's Bow",
                [256] = "Self-Resisted Overhead Pull Hold",
                [257] = "Self-Resisted Chest-Level Pull Hold",
                [258] = "Self-Resisted Low Pull Hold",
                [262] = "Standing Hands-to-Thigh Abdominal Press Hold",
                [266] = "Standing Palms-Up Arm Raise",
                [268] = "Thumbs-Up Diagonal Arm Raises",
                [269] = "Self-Resisted Curl-and-Press",
                [270] = "Palm-Squeeze Forward Press",
                [275] = "Standing Figure-Eight Side Reach",
                [278] = "Dynamic-Resistance Lateral Triceps Extension",
                [279] = "Dynamic-Resistance Triceps Pushdown",
                [282] = "Qigong Drilling Fists",
                [283] = "Flamenco Finger Flourish",
                [285] = "Opposite-Hand-Resisted Supinated Curl",
                [286] = "Opposite-Hand-Resisted Hammer Curl",
                [287] = "Opposite-Hand-Resisted Reverse Curl",
                [291] = "Fingertip Spider Presses",
                [294] = "Finger-Spread to Interlace Stretch",
                [314] = "Alternating Forward Lunge with Biceps Curl",
                [321] = "Alternating Cross-Step Arms Raise",
                [326] = "Staggered-Stance Jab-Cross",
                [329] = "Qigong Gathering Qi",
                [390] = "Three-Inhale Arm Sweep and Exhale Fold",
                [391] = "Bellows Breathing with Arm Pumps",
                [394] = "Inhale Arms Open, Exhale Arms Close and Round",
                [395] = "Overhead Hold with Deep Ribcage Breaths",
                [396] = "Unsupported Single-Leg Balance Hold",
                [397] = "Exhale Forward, Inhale Back Weight Shift",
                [425] = "Chin-Tuck Hold",
                [504] = "Hands-Behind-Head Splenius-Capitis Stretch",
                [507] = "First-Position Plié Elbow-Pull Pulse",
                [508] = "Curl Raised Leg with One Arm",
                [513] = "Collarbone-Anchored Diagonal Neck Stretch",
                [516] = "Shoulder Shrug",
                [572] = "Standing Side-Lunge Adductor Stretch",
                [576] = "Qigong Crane-Wing Shoulder Lift",
                [577] = "Qigong Swimming-Dragon Shoulder Roll",
                [615] = "Standing Forward-and-Back Pelvic Tilts",
                [618] = "Alternating Standing Windmill",
                [677] = "T-Arm Rear Pulse",
                [683] = "Goalpost Open-In-and-Lift",
                [685] = "Wing Chun Chain Punching",
                [745] = "Dynamic Hug",
                [816] = "Torso Circle",
                [834] = "Alternating Cross-Step Lat Pulldown",
            };

    private static readonly HashSet<int> ReplacedExerciseIdSet =
    [
        15, 16, 17, 19, 20, 31, 41, 47, 56, 59, 90, 94, 95, 97, 98, 99, 100, 102, 107, 115, 116,
        117, 120, 126, 133, 135, 146, 150, 159, 169, 176, 177, 179, 180, 182, 183, 184,
        185, 186, 187,
        191, 192, 193, 194, 195, 196, 199, 201, 202, 203, 204, 205, 211, 212, 213, 214, 215, 216, 217,
        218, 219, 220, 223, 224, 225, 227, 228, 229, 230, 231, 232, 233, 234, 236, 237, 239,
        240, 241, 242, 245, 246, 248, 251, 256, 257, 258, 260, 262, 263, 264, 265, 266, 267, 268, 269,
        270, 272, 274, 275, 276, 278, 279, 280, 281, 282, 283, 284, 285, 286, 287, 288,
        289, 290, 291, 292, 293, 294, 295, 296, 301, 302, 304, 305, 307, 308, 309, 310,
        314, 321, 326, 327, 329, 338, 367, 390, 391,
        392, 393, 394, 395, 396, 397, 398, 406, 407, 408, 409, 410, 411, 412, 413, 414, 415, 416, 417,
        418, 419, 420, 421, 422, 423, 424, 425, 426, 427, 428, 429, 430,
        431, 432, 433, 434, 467, 474, 475, 477, 481, 482, 483,
        435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445, 446, 447, 448,
        449, 450, 451, 452, 453, 454, 455, 456, 457, 458, 459, 460, 461, 462,
        463, 464, 465, 466, 468, 469, 470, 471, 472, 473, 476, 478, 479, 480,
        484, 485, 486, 487, 488, 489, 494, 496, 517, 518, 519,
        490, 491, 492, 493, 495, 497, 498, 499, 500, 501, 502, 503, 504, 505, 506, 507, 508,
        509, 510, 511, 512, 513, 514, 515, 516, 520, 521, 522, 523, 524, 525, 526, 527, 528,
        529, 530, 531, 532, 533, 534, 535, 536, 537, 538, 539, 540, 541, 542, 543, 545, 546,
        547, 548, 549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
        559, 560, 561, 562, 563, 564, 565, 566, 567, 568, 569, 570, 571,
        572, 573, 574, 575, 576, 577, 578, 581, 582, 583, 588, 591,
        608, 609, 610, 611, 612, 613, 614,
        615, 616, 618, 619, 625, 636, 647, 649, 654, 677, 678, 681, 683, 684, 685, 686,
        687, 712, 743, 745, 755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
        790, 816, 834, 843, 845, 886, 887, 911, 913, 916, 917, 918, 919, 971, 986, 987, 993, 996, 997, 998, 999,
    ];

    private static readonly HashSet<int> PermanentlyRetiredExerciseIdSet =
    [
        90, 229, 267, 412, 553, 558, 559, 757, 759, 760, 761, 762, 763, 764,
    ];

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<int>>
        ScopedWorkoutStateInvalidationsByRevision =
            new Dictionary<int, IReadOnlySet<int>>
            {
                [4] = new HashSet<int> { 591 },
                [5] = new HashSet<int> { 266 },
                [6] = new HashSet<int> { 266 },
                [7] = new HashSet<int> { 326 },
                [8] = new HashSet<int> { 211, 212, 213, 214, 232, 233, 234, 236 },
                [9] = new HashSet<int> { 195 },
                [10] = new HashSet<int> { 126, 135, 338, 686 },
                [11] = new HashSet<int>
                {
                    211, 213, 214, 215, 216, 217, 218, 232,
                    233, 234, 236, 237, 240, 241, 283, 289,
                },
                [12] = new HashSet<int> { 513, 843 },
                [13] = new HashSet<int> { 223, 224, 225, 245, 246 },
                [16] = new HashSet<int> { 234, 239, 240 },
                [18] = new HashSet<int>
                {
                    115, 119, 140, 212, 260, 326, 340, 512, 649,
                },
                [20] = new HashSet<int>
                {
                    211, 213, 214, 215, 218, 223, 224,
                    236, 237, 241, 242, 245, 283, 289,
                },
                [21] = new HashSet<int>
                {
                    15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
                    179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
                    256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
                    283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
                    394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
                    615, 618, 677, 683, 685, 745, 816, 834,
                },
                [22] = new HashSet<int>
                {
                    117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
                    263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
                },
                [23] = new HashSet<int>
                {
                    407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
                },
                [24] = new HashSet<int>
                {
                    420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
                },
                [25] = new HashSet<int>
                {
                    31, 219, 248, 282, 390, 394, 395,
                    397, 508, 576, 577, 618, 816, 834,
                },
                [26] = new HashSet<int> { 31, 282, 391, 507, 508, 577 },
                [27] = new HashSet<int> { 231, 685, 687 },
                [28] = new HashSet<int> { 251 },
                [29] = new HashSet<int>
                {
                    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
                    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
                    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
                    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
                    486, 487, 488, 489, 494, 496, 517, 518, 519,
                },
                [30] = new HashSet<int>
                {
                    229, 467, 474, 481, 483, 491, 493, 495, 497, 499,
                    501, 504, 513, 516,
                },
                [31] = new HashSet<int> { 414, 415, 416, 418, 419 },
                [32] = new HashSet<int>
                {
                    31, 219, 395, 507, 577, 618, 654, 834,
                },
                [33] = new HashSet<int>
                {
                    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
                    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
                },
                [34] = new HashSet<int> { 98, 390, 508, 576, 816 },
                [35] = new HashSet<int> { 219 },
                [36] = new HashSet<int> { 684 },
                [37] = new HashSet<int>
                {
                    31, 176, 195, 391, 413, 884, 885,
                },
                [41] = new HashSet<int> { 500 },
                [42] = new HashSet<int> { 105, 107, 108, 245, 280, 591, 884, 885, 905 },
                [43] = new HashSet<int> { 90, 94, 95, 99, 100, 497, 498, 511, 514 },
                [44] = new HashSet<int> { 90, 94, 95, 99, 100, 497, 498, 500, 511, 514 },
                [45] = new HashSet<int>
                {
                    264, 275, 406, 409, 460, 588, 608, 611, 617, 620, 743,
                    757, 759, 760, 761, 762, 763, 764,
                },
                [46] = new HashSet<int>
                {
                    265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
                },
                [47] = new HashSet<int>
                {
                    198, 398, 421, 427, 468, 512, 515,
                },
                [49] = new HashSet<int>
                {
                    520, 521, 529, 530, 531, 532, 533, 534, 535, 536, 537,
                    538, 539, 540, 541, 542, 543, 545, 546,
                },
                [50] = new HashSet<int> { 31, 169, 219, 547, 548 },
                [51] = new HashSet<int>
                {
                    439, 442, 444, 478,
                    549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
                    559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
                    569, 570, 571, 574, 575, 578, 581, 582, 583,
                },
                [52] = new HashSet<int>
                {
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
                },
                [53] = new HashSet<int>
                {
                    17, 19, 37, 41, 58, 60, 92, 93, 97, 103, 104, 105,
                    107, 108, 109, 112, 116, 117, 120, 121, 122, 123, 124, 125,
                    126, 127, 128, 129, 133, 136, 142, 143, 150, 156, 163, 174,
                    178, 180, 181, 182, 183, 184, 190, 192, 193, 195, 199, 203,
                    231, 232, 245, 278, 279, 280, 282, 303, 311, 314, 315,
                    326, 340, 404, 408, 412, 478, 484, 508, 509, 534, 535,
                    536, 538, 572, 576, 591, 610, 611, 626, 633, 636, 685, 687,
                    733, 746, 748, 750, 816, 884, 885, 886, 887, 905, 915, 971,
                    973, 986, 999,
                },
                [54] = new HashSet<int> { 563, 564, 567, 568, 574 },
                [55] = new HashSet<int> { 790, 993 },
                [56] = new HashSet<int>
                {
                    218, 234, 237, 239, 240, 241, 242, 283, 291, 556,
                },
                [57] = new HashSet<int> { 287 },
                [58] = new HashSet<int>
                {
                    218, 234, 237, 239, 241, 283, 291, 294, 556,
                },
                [59] = new HashSet<int> { 565 },
                [60] = new HashSet<int> { 397 },
                [61] = new HashSet<int> { 302, 304, 305, 307, 308, 309, 310 },
                [62] = new HashSet<int>
                {
                    248, 281, 286, 367, 393, 529, 537, 545,
                },
                [63] = new HashSet<int> { 439, 442, 444 },
                [64] = new HashSet<int>
                {
                    104, 113, 117, 120, 123, 135, 177, 184, 186, 199,
                    256, 261, 626, 677, 845, 996, 997,
                },
                [65] = new HashSet<int> { 507 },
                [66] = new HashSet<int> { 524, 525, 526, 527, 528, 790 },
                [67] = new HashSet<int> { 911, 913, 916, 917 },
                [69] = new HashSet<int> { 918, 919 },
                [70] = new HashSet<int>
                {
                    56, 59, 98, 108, 176, 185, 188, 190, 202, 203, 204, 205,
                    220, 224, 231, 258, 269, 283, 289, 290, 377, 379, 392,
                    398, 399, 400, 401, 402, 403, 404, 405, 410, 474, 481,
                    498, 543, 557, 608, 609, 678, 685, 687,
                },
                [71] = new HashSet<int> { 32, 483, 493 },
                [72] = new HashSet<int>
                {
                    307, 490, 491, 492, 495, 499, 501, 520, 528, 561, 958,
                },
                [73] = new HashSet<int>
                {
                    16, 20, 21, 32, 47, 95, 97, 99, 100, 117, 123, 125, 126,
                    128, 129, 131, 133, 143, 144, 145, 151, 152, 153, 160, 161, 170,
                    171, 173, 178, 181, 182, 195, 198, 203, 215, 219, 220, 223, 228,
                    233, 245, 258, 261, 264, 266, 274, 275, 279, 280, 283, 285, 286,
                    289, 290, 291, 292, 301, 307, 327, 329, 379, 395, 414, 418, 422, 423, 460, 473,
                    483, 485, 486, 488, 491, 500, 515, 519, 520, 521, 531, 533, 538,
                    541, 542, 546, 556, 560, 569, 570, 572, 585, 587, 588, 610, 614, 618,
                    633, 636, 654, 677, 681, 741, 743, 744, 745, 747, 748, 751, 834,
                    948,
                },
            };

    private static readonly IReadOnlyDictionary<int, IReadOnlySet<int>>
        ScopedScoreInvalidationsByRevision =
            new Dictionary<int, IReadOnlySet<int>>
            {
                [4] = new HashSet<int> { 591 },
                [5] = new HashSet<int> { 266 },
                [6] = new HashSet<int> { 266 },
                [7] = new HashSet<int> { 326 },
                [8] = new HashSet<int> { 211, 212, 213, 214, 232, 233, 234, 236 },
                [9] = new HashSet<int> { 195 },
                [10] = new HashSet<int> { 126, 135, 338, 686 },
                [11] = new HashSet<int>
                {
                    211, 213, 214, 215, 216, 217, 218, 232,
                    233, 234, 236, 237, 240, 241, 283, 289,
                },
                [12] = new HashSet<int> { 513, 843 },
                [13] = new HashSet<int> { 223, 224, 225, 245, 246 },
                [16] = new HashSet<int> { 234, 239, 240 },
                [18] = new HashSet<int> { 115, 212, 260, 512, 649 },
                [20] = new HashSet<int>
                {
                    211, 213, 214, 215, 218, 223, 224,
                    236, 237, 241, 242, 245, 283, 289,
                },
                [21] = new HashSet<int>
                {
                    15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
                    179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
                    256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
                    283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
                    394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
                    615, 618, 677, 683, 685, 745, 816, 834,
                },
                [22] = new HashSet<int>
                {
                    117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
                    263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
                },
                [23] = new HashSet<int>
                {
                    407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
                },
                [24] = new HashSet<int>
                {
                    420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
                },
                [27] = new HashSet<int> { 687 },
                [28] = new HashSet<int> { 251 },
                [29] = new HashSet<int>
                {
                    435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
                    446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
                    457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
                    469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
                    486, 487, 488, 489, 494, 496, 517, 518, 519,
                },
                [30] = new HashSet<int> { 229, 497, 501, 504, 513 },
                [31] = new HashSet<int> { 414, 415, 416, 418, 419 },
                [32] = new HashSet<int>
                {
                    31, 219, 395, 507, 577, 618, 654, 834,
                },
                [33] = new HashSet<int>
                {
                    214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
                    755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
                },
                [36] = new HashSet<int> { 684 },
                [43] = new HashSet<int> { 90, 94, 95, 99, 100, 497, 498, 511, 514 },
                [44] = new HashSet<int> { 90 },
                [45] = new HashSet<int>
                {
                    264, 275, 406, 409, 460, 588, 608, 611, 743,
                    757, 759, 760, 761, 762, 763, 764,
                },
                [49] = new HashSet<int>
                {
                    520, 521, 529, 530, 531, 532, 533, 534, 535, 536, 537,
                    538, 539, 540, 541, 542, 543, 545, 546,
                },
                [50] = new HashSet<int> { 547, 548 },
                [51] = new HashSet<int>
                {
                    439, 442, 444, 478,
                    549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
                    559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
                    569, 570, 571, 574, 575, 578, 581, 582, 583,
                },
                [54] = new HashSet<int> { 563, 564, 567, 568, 574 },
                [55] = new HashSet<int> { 790, 993 },
                [56] = new HashSet<int>
                {
                    218, 234, 237, 239, 240, 241, 242, 283, 291, 556,
                },
                [57] = new HashSet<int> { 287 },
                [58] = new HashSet<int>
                {
                    218, 234, 237, 239, 241, 283, 291, 294, 556,
                },
                [61] = new HashSet<int> { 302, 304, 305, 307, 308, 309, 310 },
                [65] = new HashSet<int> { 507 },
                [66] = new HashSet<int> { 524, 525, 526, 527, 528, 790 },
                [67] = new HashSet<int> { 911, 913, 916, 917 },
                [69] = new HashSet<int> { 918, 919 },
                [70] = new HashSet<int> { 202, 204, 205 },
                [73] = new HashSet<int>
                {
                    261, 266, 289, 290, 301, 473, 485, 486, 515, 519, 520, 521, 560,
                    569, 570, 614, 677,
                },
            };

    private static readonly HashSet<int> ContinuousAlternationNormalizationIdSet =
    [
    ];

    public static IReadOnlySet<int> ReplacedExerciseIds => ReplacedExerciseIdSet;

    public static IReadOnlySet<int> PermanentlyRetiredExerciseIds =>
        PermanentlyRetiredExerciseIdSet;

    public static IReadOnlyDictionary<int, IReadOnlySet<int>>
        WorkoutStateInvalidationsByRevision =>
            ScopedWorkoutStateInvalidationsByRevision;

    public static IReadOnlyDictionary<int, IReadOnlySet<int>>
        ScoreInvalidationsByRevision => ScopedScoreInvalidationsByRevision;

    public static IReadOnlySet<int> ValidatePreservedCatalog(
        IReadOnlyCollection<Exercise> bundledCatalog,
        IReadOnlyDictionary<int, StoredExerciseSnapshot> storedExercises)
    {
        ArgumentNullException.ThrowIfNull(bundledCatalog);
        ArgumentNullException.ThrowIfNull(storedExercises);

        Dictionary<int, Exercise> bundledById;
        try
        {
            bundledById = bundledCatalog.ToDictionary(exercise => exercise.Id);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The bundled catalog contains a duplicate stable exercise ID.",
                exception);
        }

        int[] bundledRetiredIds = PermanentlyRetiredExerciseIdSet
            .Where(bundledById.ContainsKey)
            .Order()
            .ToArray();
        if (bundledRetiredIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"The bundled catalog restores permanently retired exercises: " +
                $"{string.Join(", ", bundledRetiredIds)}.");
        }

        var alreadyReviewedReplacementIds = new HashSet<int>();
        var restoredReviewedExerciseIds = new HashSet<int>();
        var integrityReplacedExerciseIds = new HashSet<int>();

        foreach ((int exerciseId, StoredExerciseSnapshot stored) in storedExercises)
        {
            // Different-action replacements must not inherit the old action's
            // score. The exact reviewed name and video still have to match.
            if (ScopedScoreInvalidationsByRevision[73].Contains(exerciseId) &&
                bundledById.TryGetValue(exerciseId, out Exercise? integrityReplacement) &&
                !string.Equals(stored.Name, integrityReplacement.Name, StringComparison.Ordinal) &&
                IsApprovedExerciseCorrection(exerciseId, stored.Name, integrityReplacement.Name) &&
                string.Equals(stored.Video, integrityReplacement.Video, StringComparison.Ordinal))
            {
                integrityReplacedExerciseIds.Add(exerciseId);
                continue;
            }

            if (ReplacedExerciseIdSet.Contains(exerciseId))
            {
                if (!bundledById.TryGetValue(exerciseId, out Exercise? replacement))
                {
                    if (PermanentlyRetiredExerciseIdSet.Contains(exerciseId))
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"The bundled catalog is missing reviewed replacement {exerciseId}.");
                }

                bool currentReviewedIdentityMatches =
                    (string.Equals(
                            stored.Name,
                            replacement.Name,
                            StringComparison.Ordinal) ||
                        (replacement.SideSequence.UsesTimedSides() &&
                            stored.Name.StartsWith(
                                AlternatingPrefix,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                stored.Name[AlternatingPrefix.Length..],
                                replacement.Name,
                                StringComparison.Ordinal))) &&
                    string.Equals(
                        stored.Video,
                        replacement.Video,
                        StringComparison.Ordinal);
                bool approvedCorrectionMatches =
                    IsApprovedExerciseCorrection(
                        exerciseId,
                        stored.Name,
                        replacement.Name) &&
                    string.Equals(
                        stored.Video,
                        replacement.Video,
                        StringComparison.Ordinal);
                bool discardedStoredIdentityMatches =
                    DiscardedStoredExerciseIdentities.TryGetValue(
                        exerciseId,
                        out IReadOnlySet<DiscardedStoredExerciseIdentity>?
                            discardedIdentities) &&
                    discardedIdentities.Contains(new(
                        stored.Name,
                        stored.Video));
                if (discardedStoredIdentityMatches)
                {
                    continue;
                }

                // A historical name correction must never revive feedback for
                // an explicitly discarded, different exercise at the same ID.
                if (currentReviewedIdentityMatches || approvedCorrectionMatches)
                {
                    alreadyReviewedReplacementIds.Add(exerciseId);
                    continue;
                }

                bool baselineRetiredNameMatches =
                    !string.IsNullOrWhiteSpace(replacement.RetiredName) &&
                    NameMatchesWithOptionalAlternatingPrefix(
                        stored.Name,
                        replacement.RetiredName);
                bool priorReviewedIdentityMatches =
                    PriorReviewedReplacementIdentities.TryGetValue(
                        exerciseId,
                        out PriorReviewedReplacementIdentity? priorIdentity) &&
                    string.Equals(
                        replacement.RetiredName,
                        priorIdentity.BaselineRetiredName,
                        StringComparison.Ordinal) &&
                    NameMatchesWithOptionalAlternatingPrefix(
                        stored.Name,
                        priorIdentity.Name);
                bool additionalPriorReviewedIdentityMatches =
                    AdditionalPriorReviewedReplacementNames.TryGetValue(
                        exerciseId,
                        out IReadOnlySet<string>? priorNames) &&
                    priorIdentity is not null &&
                    string.Equals(
                        replacement.RetiredName,
                        priorIdentity.BaselineRetiredName,
                        StringComparison.Ordinal) &&
                    priorNames.Any(priorName =>
                        NameMatchesWithOptionalAlternatingPrefix(
                            stored.Name,
                            priorName));
                bool catalogClarityResetIdentityMatches =
                    CatalogClarityResetPreviousNames.TryGetValue(
                        exerciseId,
                        out string? clarityResetPreviousName) &&
                    NameMatchesWithOptionalAlternatingPrefix(
                        stored.Name,
                        clarityResetPreviousName);
                bool directionSplitPreviousIdentityMatches =
                    DirectionSplitPreviousIdentities.TryGetValue(
                        exerciseId,
                        out ApprovedExerciseCorrection? directionSplitIdentity) &&
                    string.Equals(
                        replacement.Name,
                        directionSplitIdentity.CurrentName,
                        StringComparison.Ordinal) &&
                    NameMatchesWithOptionalAlternatingPrefix(
                        stored.Name,
                        directionSplitIdentity.PreviousName);
                bool reviewedRestorationPreviousIdentityMatches =
                    RestoredReviewedExerciseIdentities.TryGetValue(
                        exerciseId,
                        out RestoredReviewedExerciseIdentity? restoredIdentity) &&
                    string.Equals(
                        replacement.RetiredName,
                        restoredIdentity.RestoredName,
                        StringComparison.Ordinal) &&
                    NameMatchesWithOptionalAlternatingPrefix(
                        stored.Name,
                        restoredIdentity.PreviousReplacementName);
                if ((!baselineRetiredNameMatches &&
                        !priorReviewedIdentityMatches &&
                        !additionalPriorReviewedIdentityMatches &&
                        !catalogClarityResetIdentityMatches &&
                        !directionSplitPreviousIdentityMatches &&
                        !reviewedRestorationPreviousIdentityMatches) ||
                    !string.Equals(
                        stored.Video,
                        replacement.Video,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The bundled catalog cannot verify the retired identity " +
                        $"of reviewed replacement {exerciseId}.");
                }

                continue;
            }

            if (!bundledById.TryGetValue(exerciseId, out Exercise? bundled))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would remove existing exercise {exerciseId}.");
            }

            bool nameIsPreserved = string.Equals(
                stored.Name,
                bundled.Name,
                StringComparison.Ordinal);
            bool nameIsApprovedTimedSideNormalization =
                bundled.SideSequence.UsesTimedSides() &&
                stored.Name.StartsWith(AlternatingPrefix, StringComparison.Ordinal) &&
                string.Equals(
                    stored.Name[AlternatingPrefix.Length..],
                    bundled.Name,
                    StringComparison.Ordinal);
            bool nameIsApprovedContinuousAlternationNormalization =
                ContinuousAlternationNormalizationIdSet.Contains(exerciseId) &&
                !bundled.SideSequence.UsesTimedSides() &&
                bundled.Name.StartsWith(AlternatingPrefix, StringComparison.Ordinal) &&
                string.Equals(
                    stored.Name,
                    bundled.Name[AlternatingPrefix.Length..],
                    StringComparison.Ordinal);
            bool nameIsApprovedExerciseCorrection =
                IsApprovedExerciseCorrection(
                    exerciseId,
                    stored.Name,
                    bundled.Name);
            bool nameIsApprovedReviewedRestoration =
                RestoredReviewedExerciseIdentities.TryGetValue(
                    exerciseId,
                    out RestoredReviewedExerciseIdentity? restoration) &&
                string.Equals(
                    stored.Name,
                    restoration.PreviousReplacementName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    bundled.Name,
                    restoration.RestoredName,
                    StringComparison.Ordinal);
            if ((!nameIsPreserved &&
                    !nameIsApprovedTimedSideNormalization &&
                    !nameIsApprovedContinuousAlternationNormalization &&
                    !nameIsApprovedExerciseCorrection &&
                    !nameIsApprovedReviewedRestoration) ||
                !string.Equals(stored.Video, bundled.Video, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The bundled catalog would change the stable identity or " +
                    $"demonstration of existing exercise {exerciseId}.");
            }

            if (nameIsApprovedReviewedRestoration)
            {
                restoredReviewedExerciseIds.Add(exerciseId);
            }
        }

        return storedExercises.Keys
            .Where(exerciseId =>
                (!ReplacedExerciseIdSet.Contains(exerciseId) ||
                    alreadyReviewedReplacementIds.Contains(exerciseId)) &&
                !restoredReviewedExerciseIds.Contains(exerciseId) &&
                !integrityReplacedExerciseIds.Contains(exerciseId))
            .ToHashSet();
    }

    private static bool NameMatchesWithOptionalAlternatingPrefix(
        string storedName,
        string expectedName) =>
        string.Equals(storedName, expectedName, StringComparison.Ordinal) ||
        (storedName.StartsWith(AlternatingPrefix, StringComparison.Ordinal) &&
            string.Equals(
                storedName[AlternatingPrefix.Length..],
                expectedName,
                StringComparison.Ordinal)) ||
        (expectedName.StartsWith(AlternatingPrefix, StringComparison.Ordinal) &&
            string.Equals(
                storedName,
                expectedName[AlternatingPrefix.Length..],
                StringComparison.Ordinal));

    private static bool IsApprovedExerciseCorrection(
        int exerciseId,
        string storedName,
        string bundledName) =>
        ApprovedExerciseCorrections.TryGetValue(
            exerciseId,
            out ApprovedExerciseCorrection? correction) &&
        (string.Equals(
                storedName,
                correction.PreviousName,
                StringComparison.Ordinal) ||
            (AdditionalApprovedExerciseCorrectionPreviousNames.TryGetValue(
                    exerciseId,
                    out IReadOnlySet<string>? additionalPreviousNames) &&
                additionalPreviousNames.Contains(storedName))) &&
        string.Equals(
            bundledName,
            correction.CurrentName,
            StringComparison.Ordinal);

    private static IReadOnlySet<int> GetWorkoutStateInvalidationExerciseIds(
        int priorCatalogRevision,
        int excludedRevision = 0,
        int secondExcludedRevision = 0)
    {
        var invalidatedExerciseIds = priorCatalogRevision <
            LastCumulativeWorkoutStateRevision
                ? new HashSet<int>(ReplacedExerciseIdSet)
                : [];

        foreach ((int revision, IReadOnlySet<int> exerciseIds) in
            ScopedWorkoutStateInvalidationsByRevision)
        {
            if (revision > priorCatalogRevision &&
                revision != excludedRevision &&
                revision != secondExcludedRevision)
            {
                invalidatedExerciseIds.UnionWith(exerciseIds);
            }
        }

        return invalidatedExerciseIds;
    }

    private static IReadOnlySet<int> GetScoreInvalidationExerciseIds(
        int priorCatalogRevision)
    {
        var invalidatedExerciseIds = priorCatalogRevision <
            LastCumulativeWorkoutStateRevision
                ? new HashSet<int>(ReplacedExerciseIdSet)
                : [];

        foreach ((int revision, IReadOnlySet<int> exerciseIds) in
            ScopedScoreInvalidationsByRevision)
        {
            if (revision > priorCatalogRevision)
            {
                invalidatedExerciseIds.UnionWith(exerciseIds);
            }
        }

        return invalidatedExerciseIds;
    }

    public static bool ReconcileWorkoutState(
        WorkoutState state,
        IReadOnlyDictionary<int, Exercise>? exercisesById = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.CatalogRevision >= CurrentCatalogRevision)
        {
            return false;
        }

        state.SelectedExerciseIds ??= [];
        state.Outcomes ??= [];
        state.LastKeptExerciseIds ??= [];
        state.KeptExerciseRootIdsBySelectionGroupId ??= [];
        state.ExerciseScoreAdjustmentsBySelectionGroupId ??= [];
        state.ExerciseScoreAdjustmentsByPhase ??= [];
        state.ActiveExtraSetSelectionGroupIds ??= [];
        state.ActiveSetCountsBySelectionGroupId ??= [];
        state.ActiveDirectionPartnerExerciseIds ??= [];
        state.ActiveFullSideRoundIds ??= [];
        state.PendingScoreUpdates ??= [];
        IReadOnlySet<int> trainingClaimChangedExerciseIds =
            exercisesById is null
                ? new HashSet<int>()
                : GetTrainingClaimChangedExerciseIds(state.CatalogRevision);
        bool reconcileTrainingClaims =
            exercisesById is not null &&
            trainingClaimChangedExerciseIds.Count > 0;
        HashSet<string> semanticallyInvalidSelectionStorageKeys =
            reconcileTrainingClaims
                ? state.SelectedExerciseIds
                    .Where(selection =>
                    {
                        (string selectionGroupId, _) =
                            ParseSelectionStorageKey(selection.Key);
                        return IsKnownSelectionGroupId(selectionGroupId) &&
                            IsTrainingClaimAffectedRoot(
                                selection.Value,
                                exercisesById!,
                                trainingClaimChangedExerciseIds) &&
                            !IsValidPreferenceRoot(
                                selectionGroupId,
                                selection.Value,
                                exercisesById!);
                    })
                    .Select(selection => selection.Key)
                    .ToHashSet(StringComparer.Ordinal)
                : [];
        IReadOnlySet<int> invalidatedExerciseIds =
            GetWorkoutStateInvalidationExerciseIds(
                state.CatalogRevision,
                HardFloorSlipperinessCatalogRevision,
                ReusedShyAuditCatalogRevision);
        IReadOnlySet<int> hardFloorInvalidatedExerciseIds =
            state.CatalogRevision < HardFloorSlipperinessCatalogRevision
                ? ScopedWorkoutStateInvalidationsByRevision[
                    HardFloorSlipperinessCatalogRevision]
                : new HashSet<int>();
        IReadOnlySet<int> shyAuditInvalidatedExerciseIds =
            state.CatalogRevision < ReusedShyAuditCatalogRevision
                ? ScopedWorkoutStateInvalidationsByRevision[
                    ReusedShyAuditCatalogRevision]
                : new HashSet<int>();
        IReadOnlySet<int> scoreInvalidatedExerciseIds =
            GetScoreInvalidationExerciseIds(state.CatalogRevision);

        if (state.CatalogRevision < ReusedShyAuditCatalogRevision)
        {
            ReconcileReusedExerciseKeeps(state, exercisesById, ReusedShyAuditExerciseIdSet);
        }
        if (state.CatalogRevision < 73)
        {
            ReconcileReusedExerciseKeeps(state, exercisesById, ScopedScoreInvalidationsByRevision[73]);
        }

        var selectionsWithInvalidatedExercises = state.SelectedExerciseIds
            .Select(selection => new
            {
                StorageKey = selection.Key,
                ExerciseId = selection.Value,
                Parsed = ParseSelectionStorageKey(selection.Key),
            })
            .Where(selection =>
                semanticallyInvalidSelectionStorageKeys.Contains(
                    selection.StorageKey) ||
                invalidatedExerciseIds.Contains(selection.ExerciseId) ||
                (state.CatalogRevision < ReusedShyAuditCatalogRevision &&
                    ReusedShyAuditExerciseIdSet.Contains(selection.ExerciseId)) ||
                (shyAuditInvalidatedExerciseIds.Contains(selection.ExerciseId) &&
                    (WorkoutModifierPolicy.Normalize(selection.Parsed.Modifiers) &
                        WorkoutModifiers.Shy) != 0) ||
                (hardFloorInvalidatedExerciseIds.Contains(selection.ExerciseId) &&
                    (WorkoutModifierPolicy.Normalize(selection.Parsed.Modifiers) &
                        WorkoutModifiers.HardFloor) != 0))
            .ToArray();
        WorkoutModifiers activeModifiers = WorkoutModifierPolicy.Normalize(
            state.ActiveWorkoutModifiers);
        HashSet<string> selectionGroupsWithRetiredSelections =
            selectionsWithInvalidatedExercises
                .Where(selection =>
                    WorkoutModifierPolicy.Normalize(selection.Parsed.Modifiers) ==
                        activeModifiers)
                .Select(selection => selection.Parsed.SelectionGroupId)
                .ToHashSet(StringComparer.Ordinal);

        foreach (var selection in selectionsWithInvalidatedExercises)
        {
            state.SelectedExerciseIds.Remove(selection.StorageKey);
        }
        foreach (string roundId in state.Outcomes.Keys.Where(roundId =>
                     selectionGroupsWithRetiredSelections.Contains(
                         GetSelectionGroupIdFromRoundId(roundId))).ToArray())
        {
            state.Outcomes.Remove(roundId);
        }

        if (state.PendingRestGroupId is not null &&
            selectionGroupsWithRetiredSelections.Contains(
                GetSelectionGroupIdFromRoundId(state.PendingRestGroupId)))
        {
            state.PendingRestGroupId = null;
            state.PendingRestEndsAtUnixMilliseconds = 0;
            state.PendingRestMillisecondsRemaining = 0;
            state.PendingRestPausedByUser = false;
            state.PendingRestKept = false;
        }
        if (state.PendingMovementGroupId is not null &&
            selectionGroupsWithRetiredSelections.Contains(
                GetSelectionGroupIdFromRoundId(state.PendingMovementGroupId)))
        {
            state.PendingMovementGroupId = null;
            state.PendingMovementEndsAtUnixMilliseconds = 0;
            state.PendingMovementMillisecondsRemaining = 0;
            state.PendingMovementPausedByUser = false;
        }

        if (reconcileTrainingClaims)
        {
            ReconcileInvalidSlotPreferences(
                state,
                exercisesById!,
                trainingClaimChangedExerciseIds);
        }

        if (scoreInvalidatedExerciseIds.Contains(state.PendingScoreExerciseId))
        {
            state.PendingScoreExerciseId = 0;
            state.PendingScoreValue = 0;
        }
        foreach (int exerciseId in state.PendingScoreUpdates.Keys
                     .Where(scoreInvalidatedExerciseIds.Contains)
                     .ToArray())
        {
            state.PendingScoreUpdates.Remove(exerciseId);
        }

        foreach (string selectionGroupId in
                 state.ExerciseScoreAdjustmentsBySelectionGroupId.Keys.ToArray())
        {
            foreach (int exerciseId in state
                         .ExerciseScoreAdjustmentsBySelectionGroupId[
                             selectionGroupId]
                         .Keys
                         .Where(exerciseId =>
                             IsScorePreferenceRootInvalidated(
                                 exerciseId,
                                 scoreInvalidatedExerciseIds,
                                 exercisesById))
                         .ToArray())
            {
                state.ExerciseScoreAdjustmentsBySelectionGroupId[
                    selectionGroupId].Remove(exerciseId);
            }
            if (state.ExerciseScoreAdjustmentsBySelectionGroupId[
                    selectionGroupId].Count == 0)
            {
                state.ExerciseScoreAdjustmentsBySelectionGroupId.Remove(
                    selectionGroupId);
            }
        }

        foreach (WorkoutExercisePhase phase in
                 state.ExerciseScoreAdjustmentsByPhase.Keys.ToArray())
        {
            foreach (int exerciseId in state.ExerciseScoreAdjustmentsByPhase[phase]
                         .Keys
                         .Where(exerciseId => IsScorePreferenceRootInvalidated(
                             exerciseId,
                             scoreInvalidatedExerciseIds,
                             exercisesById))
                         .ToArray())
            {
                state.ExerciseScoreAdjustmentsByPhase[phase].Remove(exerciseId);
            }
            if (state.ExerciseScoreAdjustmentsByPhase[phase].Count == 0)
            {
                state.ExerciseScoreAdjustmentsByPhase.Remove(phase);
            }
        }

        state.CatalogRevision = CurrentCatalogRevision;
        return true;
    }

    private static void ReconcileReusedExerciseKeeps(
        WorkoutState state,
        IReadOnlyDictionary<int, Exercise>? exercisesById,
        IReadOnlySet<int> reusedExerciseIds)
    {
        var removedRootIds = new HashSet<int>();
        foreach (string selectionGroupId in
                 state.KeptExerciseRootIdsBySelectionGroupId.Keys.ToArray())
        {
            HashSet<int> keptRootIds =
                state.KeptExerciseRootIdsBySelectionGroupId[selectionGroupId];
            foreach (int rootId in keptRootIds.Where(rootId =>
                         IsScorePreferenceRootInvalidated(
                             rootId,
                             reusedExerciseIds,
                             exercisesById)).ToArray())
            {
                keptRootIds.Remove(rootId);
                removedRootIds.Add(rootId);
            }

            if (keptRootIds.Count == 0)
            {
                state.KeptExerciseRootIdsBySelectionGroupId.Remove(
                    selectionGroupId);
            }
        }

        var removedExerciseIds = new HashSet<int>(reusedExerciseIds);
        if (exercisesById is not null)
        {
            foreach (int rootId in removedRootIds)
            {
                if (!exercisesById.TryGetValue(rootId, out Exercise? root))
                {
                    continue;
                }

                removedExerciseIds.UnionWith(
                    root.SequenceBlocks.Select(block => block.ExerciseId));
            }
        }

        state.LastKeptExerciseIds.RemoveWhere(removedExerciseIds.Contains);
    }

    private static bool IsScorePreferenceRootInvalidated(
        int rootExerciseId,
        IReadOnlySet<int> scoreInvalidatedExerciseIds,
        IReadOnlyDictionary<int, Exercise>? exercisesById)
    {
        if (scoreInvalidatedExerciseIds.Contains(rootExerciseId))
        {
            return true;
        }

        return exercisesById is not null &&
            exercisesById.TryGetValue(rootExerciseId, out Exercise? root) &&
            root.SequenceBlocks.Any(block =>
                scoreInvalidatedExerciseIds.Contains(block.ExerciseId));
    }

    private static void ReconcileInvalidSlotPreferences(
        WorkoutState state,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlySet<int> trainingClaimChangedExerciseIds)
    {
        foreach (string selectionGroupId in
                 state.KeptExerciseRootIdsBySelectionGroupId.Keys.ToArray())
        {
            state.KeptExerciseRootIdsBySelectionGroupId[selectionGroupId]
                .RemoveWhere(rootId =>
                    IsKnownSelectionGroupId(selectionGroupId) &&
                    IsTrainingClaimAffectedRoot(
                        rootId,
                        exercisesById,
                        trainingClaimChangedExerciseIds) &&
                    !IsValidPreferenceRoot(
                        selectionGroupId,
                        rootId,
                        exercisesById));
            if (state.KeptExerciseRootIdsBySelectionGroupId[
                    selectionGroupId].Count == 0)
            {
                state.KeptExerciseRootIdsBySelectionGroupId.Remove(
                    selectionGroupId);
            }
        }

        foreach (string selectionGroupId in
                 state.ExerciseScoreAdjustmentsBySelectionGroupId.Keys.ToArray())
        {
            Dictionary<int, int> adjustments =
                state.ExerciseScoreAdjustmentsBySelectionGroupId[
                    selectionGroupId];
            foreach (int rootId in adjustments.Keys.Where(rootId =>
                         IsKnownSelectionGroupId(selectionGroupId) &&
                         IsTrainingClaimAffectedRoot(
                             rootId,
                             exercisesById,
                             trainingClaimChangedExerciseIds) &&
                         !IsValidPreferenceRoot(
                             selectionGroupId,
                             rootId,
                             exercisesById)).ToArray())
            {
                adjustments.Remove(rootId);
            }

            if (adjustments.Count == 0)
            {
                state.ExerciseScoreAdjustmentsBySelectionGroupId.Remove(
                    selectionGroupId);
            }
        }

        state.LastKeptExerciseIds = state
            .KeptExerciseRootIdsBySelectionGroupId
            .Values
            .SelectMany(rootIds => rootIds)
            .Distinct()
            .Where(exercisesById.ContainsKey)
            .SelectMany(rootId => exercisesById[rootId].SequenceBlocks.Length > 0
                ? exercisesById[rootId].SequenceBlocks
                    .Select(block => block.ExerciseId)
                : [rootId])
            .Where(exercisesById.ContainsKey)
            .ToHashSet();
    }

    private static bool IsKnownSelectionGroupId(string selectionGroupId)
    {
        return MassGroupingTaxonomy.SupportedMinutes.Any(minutes =>
            MassGroupingTaxonomy.GetResolution(minutes).Groups.Any(group =>
                group.Id == selectionGroupId));
    }

    private static bool IsValidPreferenceRoot(
        string selectionGroupId,
        int rootId,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        if (!exercisesById.TryGetValue(rootId, out Exercise? root) ||
            root.SequenceBlocks.Length == 0)
        {
            return false;
        }

        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            WorkoutResolution resolution =
                MassGroupingTaxonomy.GetResolution(minutes);
            if (resolution.Groups.All(group => group.Id != selectionGroupId))
            {
                continue;
            }

            return WorkoutSequencePolicy.GetPlacementOptions(
                    root,
                    exercisesById,
                    resolution.Groups)
                .Any(option => option
                    .OrderBy(group => group.Order)
                    .First()
                    .Id == selectionGroupId);
        }

        return false;
    }

    private static IReadOnlySet<int> GetTrainingClaimChangedExerciseIds(
        int priorCatalogRevision)
    {
        var exerciseIds = new HashSet<int>();
        foreach ((int revision, IReadOnlySet<int> changedExerciseIds) in
                 TrainingClaimAssociationChangesByRevision)
        {
            if (revision > priorCatalogRevision)
            {
                exerciseIds.UnionWith(changedExerciseIds);
            }
        }

        return exerciseIds;
    }

    private static bool IsTrainingClaimAffectedRoot(
        int rootId,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlySet<int> trainingClaimChangedExerciseIds)
    {
        if (trainingClaimChangedExerciseIds.Contains(rootId))
        {
            return true;
        }

        return exercisesById.TryGetValue(rootId, out Exercise? root) &&
            root.SequenceBlocks.Any(block =>
                trainingClaimChangedExerciseIds.Contains(block.ExerciseId));
    }

    private static (string SelectionGroupId, WorkoutModifiers Modifiers)
        ParseSelectionStorageKey(string storageKey)
    {
        int separatorIndex = storageKey.IndexOf('|');
        if (storageKey.StartsWith('p') && separatorIndex > 1 &&
            int.TryParse(
                storageKey.AsSpan(1, separatorIndex - 1),
                out int modifierValue))
        {
            return (
                storageKey[(separatorIndex + 1)..],
                (WorkoutModifiers)modifierValue);
        }

        return (storageKey, WorkoutModifiers.None);
    }

    private static string GetSelectionGroupIdFromRoundId(string roundId)
    {
        string? currentSelectionGroupId = MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups)
            .Select(group => group.Id)
            .Where(groupId =>
                string.Equals(roundId, groupId, StringComparison.Ordinal) ||
                roundId.StartsWith(groupId + ".", StringComparison.Ordinal))
            .OrderByDescending(groupId => groupId.Length)
            .FirstOrDefault();
        if (currentSelectionGroupId is not null)
        {
            return currentSelectionGroupId;
        }

        const string directionMarker = ".direction";
        int directionIndex = roundId.LastIndexOf(
            directionMarker,
            StringComparison.Ordinal);
        if (directionIndex > 0 &&
            directionIndex + directionMarker.Length == roundId.Length)
        {
            return roundId[..directionIndex];
        }
        if (directionIndex > 0 &&
            int.TryParse(
                roundId.AsSpan(directionIndex + directionMarker.Length),
                out int directionSetNumber) &&
            directionSetNumber >= 1)
        {
            return roundId[..directionIndex];
        }

        int suffixIndex = roundId.LastIndexOf(".set", StringComparison.Ordinal);
        return suffixIndex > 0 &&
            int.TryParse(roundId.AsSpan(suffixIndex + 4), out int setNumber) &&
            setNumber >= 1
                ? roundId[..suffixIndex]
                : roundId;
    }
}
