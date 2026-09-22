using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class MassGroupingTaxonomy
{
    public static readonly IReadOnlyList<int> SupportedMinutes =
        Array.AsReadOnly([3, 5, 7, 10, 15, 20, 30]);

    public static readonly IReadOnlyList<string> CranialMuscleInventory =
        Array.AsReadOnly(
        [
            "Eye movement",
            "Eyelid elevation",
            "Eyelid closure",
            "Facial expression",
            "Scalp",
            "Mastication",
            "Tongue",
            "Pharyngeal",
            "Laryngeal",
            "Middle-ear",
        ]);

    private static readonly IReadOnlyDictionary<int, WorkoutResolution> Resolutions =
        CreateAndValidateResolutions();

    public static WorkoutResolution GetResolution(int minutes)
    {
        return Resolutions.TryGetValue(minutes, out WorkoutResolution? resolution)
            ? resolution
            : throw new ArgumentOutOfRangeException(
                nameof(minutes),
                minutes,
                "Workout duration must be one of 3, 5, 7, 10, 15, 20, or 30 minutes.");
    }

    public static int NormalizeMinutes(int minutes)
    {
        return SupportedMinutes
            .OrderBy(candidate => Math.Abs(candidate - minutes))
            .ThenByDescending(candidate => candidate)
            .First();
    }

    public static WorkoutGroup GetGroup(int minutes, string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        return GetResolution(minutes).Groups.Single(group => group.Id == groupId);
    }

    public static WorkoutGroup GetGroup(
        int minutes,
        CanonicalMuscleGroup canonicalGroup)
    {
        return GetResolution(minutes).Groups.Single(group =>
            group.CanonicalGroups.Contains(canonicalGroup));
    }

    public static string GetCanonicalDisplayName(CanonicalMuscleGroup group)
    {
        return group switch
        {
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors =>
                "Medial and deep knee extensors",
            CanonicalMuscleGroup.PosteriorThighAndKneeFlexors =>
                "Posterior thigh and knee flexors",
            CanonicalMuscleGroup.MajorHipAdductors => "Major hip adductors",
            CanonicalMuscleGroup.LateralKneeExtensors => "Lateral knee extensors",
            CanonicalMuscleGroup.GlutealExtensors => "Gluteal extensors",
            CanonicalMuscleGroup.SpinalExtensors => "Spinal extensors",
            CanonicalMuscleGroup.CalfDeepPosteriorLegAndPlantarFoot =>
                "Calf, deep posterior leg and plantar foot",
            CanonicalMuscleGroup.Soleus => "Soleus",
            CanonicalMuscleGroup.ScapularGirdle => "Scapular girdle",
            CanonicalMuscleGroup.ShoulderAdductorsAndExtensors =>
                "Shoulder adductors and extensors",
            CanonicalMuscleGroup.AbdominalWall => "Abdominal wall",
            CanonicalMuscleGroup.HipAbductors => "Hip abductors",
            CanonicalMuscleGroup.Chest => "Chest",
            CanonicalMuscleGroup.ElbowExtensors => "Elbow extensors",
            CanonicalMuscleGroup.HipFlexors => "Hip flexors",
            CanonicalMuscleGroup.AnteriorLateralLowerLegAndDorsalFoot =>
                "Anterior/lateral lower leg and dorsal foot",
            CanonicalMuscleGroup.DeepHipRotators => "Deep hip rotators",
            CanonicalMuscleGroup.ShoulderAbductors => "Shoulder abductors",
            CanonicalMuscleGroup.ForearmFlexorsAndPronators =>
                "Forearm flexors and pronators",
            CanonicalMuscleGroup.DeepAndIntersegmentalBack =>
                "Deep and intersegmental back",
            CanonicalMuscleGroup.ElbowFlexors => "Elbow flexors",
            CanonicalMuscleGroup.BreathingMuscles => "Breathing muscles",
            CanonicalMuscleGroup.ForearmExtensorsAndSupinators =>
                "Forearm extensors and supinators",
            CanonicalMuscleGroup.RotatorCuff => "Rotator cuff",
            CanonicalMuscleGroup.AccessoryHipAdductors => "Accessory hip adductors",
            CanonicalMuscleGroup.PosteriorNeckAndSuboccipitalMuscles =>
                "Posterior neck and suboccipital muscles",
            CanonicalMuscleGroup.CranialMuscles => "Cranial muscles",
            CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles =>
                "Anterior/lateral neck and hyoid muscles",
            CanonicalMuscleGroup.IntrinsicHand => "Intrinsic hand",
            CanonicalMuscleGroup.PelvicFloorAndPerineum =>
                "Pelvic floor and perineum",
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };
    }

    private static IReadOnlyDictionary<int, WorkoutResolution>
        CreateAndValidateResolutions()
    {
        var resolutions = new Dictionary<int, WorkoutResolution>
        {
            [3] = Resolution(3,
                Bucket("lower-limbs", "Lower limbs", 1,
                    1, 2, 3, 4, 5, 7, 8, 12, 15, 16, 17, 25),
                Bucket("torso-pelvic-complex", "Torso and pelvic complex", 2,
                    6, 11, 13, 20, 22, 30),
                Bucket("head-neck-upper-limbs", "Head, neck and upper limbs", 3,
                    9, 10, 14, 18, 19, 21, 23, 24, 26, 27, 28, 29)),
            [5] = Resolution(5,
                Bucket("hips-thighs", "Hips and thighs", 1,
                    1, 2, 3, 4, 5, 12, 15, 17, 25),
                Bucket("torso", "Torso", 2, 6, 11, 13, 20, 22, 30),
                Bucket("lower-legs-feet", "Lower legs and feet", 3, 7, 8, 16),
                Bucket("upper-limbs", "Upper limbs", 4,
                    10, 14, 18, 19, 21, 23, 24, 29),
                Bucket("head-neck-shoulder-girdle", "Head, neck and shoulder girdle", 5,
                    9, 26, 27, 28)),
            [7] = Resolution(7,
                Bucket("torso", "Torso", 1, 6, 11, 13, 20, 22, 30),
                Bucket("knee-extensors", "Knee extensors", 2, 1, 4),
                Bucket("head-neck-upper-limbs", "Head, neck and upper limbs", 3,
                    9, 10, 14, 18, 19, 21, 23, 24, 26, 27, 28, 29),
                Bucket("lower-legs-feet", "Lower legs and feet", 4, 7, 8, 16),
                Bucket("hip-flexors-adductors", "Hip flexors and adductors", 5,
                    3, 15, 25),
                Bucket("gluteals-deep-hip", "Gluteals and deep hip stabilizers", 6,
                    5, 12, 17),
                Bucket("posterior-thigh-knee-flexors",
                    "Posterior thigh and knee flexors", 7, 2)),
            [10] = Resolution(10,
                Bucket("medial-deep-knee-extensors",
                    "Medial and deep knee extensors", 1, 1),
                Bucket("posterior-thigh-knee-flexors",
                    "Posterior thigh and knee flexors", 2, 2),
                Bucket("hip-flexors-adductors", "Hip flexors and adductors", 3,
                    3, 15, 25),
                Bucket("gluteals-deep-hip", "Gluteals and deep hip stabilizers", 4,
                    5, 12, 17),
                Bucket("back-abdominal-pelvic-floor",
                    "Back, abdominal wall and pelvic floor", 5, 6, 11, 20, 30),
                Bucket("lateral-knee-extensors", "Lateral knee extensors", 6, 4),
                Bucket("head-neck-scapular-chest-breathing",
                    "Head, neck, scapular girdle, chest and breathing", 7,
                    9, 13, 22, 26, 27, 28),
                Bucket("posterior-lower-leg-plantar-foot",
                    "Posterior lower leg and plantar foot", 8, 7, 8),
                Bucket("upper-limbs", "Upper limbs", 9,
                    10, 14, 18, 19, 21, 23, 24, 29),
                Bucket("anterior-lateral-lower-leg-dorsal-foot",
                    "Anterior/lateral lower leg and dorsal foot", 10, 16)),
            [15] = Resolution(15,
                Bucket("medial-deep-knee-extensors",
                    "Medial and deep knee extensors", 1, 1),
                Bucket("posterior-thigh-knee-flexors",
                    "Posterior thigh and knee flexors", 2, 2),
                Bucket("hip-adductors", "Hip adductors", 3, 3, 25),
                Bucket("lateral-knee-extensors", "Lateral knee extensors", 4, 4),
                Bucket("gluteal-extensors", "Gluteal extensors", 5, 5),
                Bucket("posterior-lower-leg-plantar-foot",
                    "Posterior lower leg and plantar foot", 6, 7, 8),
                Bucket("back-spinal-stabilization",
                    "Back and spinal stabilization", 7, 6, 20),
                Bucket("scapular-chest-breathing",
                    "Scapular girdle, chest and breathing", 8, 9, 13, 22),
                Bucket("lateral-deep-hip-stabilizers",
                    "Lateral and deep hip stabilizers", 9, 12, 17),
                Bucket("arm-forearm-hand", "Arm, forearm and hand", 10,
                    14, 19, 21, 23, 29),
                Bucket("abdominal-pelvic-floor",
                    "Abdominal wall and pelvic floor", 11, 11, 30),
                Bucket("shoulder", "Shoulder", 12, 10, 18, 24),
                Bucket("hip-flexors", "Hip flexors", 13, 15),
                Bucket("anterior-lateral-lower-leg-dorsal-foot",
                    "Anterior/lateral lower leg and dorsal foot", 14, 16),
                Bucket("head-neck", "Head and neck", 15, 26, 27, 28)),
            [20] = Resolution(20,
                Bucket("medial-deep-knee-extensors",
                    "Medial and deep knee extensors", 1, 1),
                Bucket("posterior-thigh-knee-flexors",
                    "Posterior thigh and knee flexors", 2, 2),
                Bucket("major-hip-adductors", "Major hip adductors", 3, 3),
                Bucket("lateral-knee-extensors", "Lateral knee extensors", 4, 4),
                Bucket("gluteal-extensors", "Gluteal extensors", 5, 5),
                Bucket("soleus", "Soleus", 6, 8),
                Bucket("back-spinal-stabilization",
                    "Back and spinal stabilization", 7, 6, 20),
                Bucket("calf-flexors-plantar-foot",
                    "Calf flexors and plantar foot", 8, 7),
                Bucket("scapular-girdle", "Scapular girdle", 9, 9),
                Bucket("chest-breathing", "Chest and breathing", 10, 13, 22),
                Bucket("shoulder-adduction-extension",
                    "Shoulder adduction and extension", 11, 10),
                Bucket("abdominal-pelvic-floor",
                    "Abdominal wall and pelvic floor", 12, 11, 30),
                Bucket("lateral-deep-hip-stabilizers",
                    "Lateral and deep hip stabilizers", 13, 12, 17),
                Bucket("upper-arm", "Upper arm", 14, 14, 21),
                Bucket("hip-flexors", "Hip flexors", 15, 15),
                Bucket("anterior-lateral-lower-leg-dorsal-foot",
                    "Anterior/lateral lower leg and dorsal foot", 16, 16),
                Bucket("shoulder-abduction-rotation",
                    "Shoulder abduction and rotation", 17, 18, 24),
                Bucket("forearm-hand", "Forearm and hand", 18, 19, 23, 29),
                Bucket("accessory-hip-adductors", "Accessory hip adductors", 19, 25),
                Bucket("head-neck", "Head and neck", 20, 26, 27, 28)),
            [30] = Resolution(30,
                CanonicalBucket(1, "medial-deep-knee-extensors"),
                CanonicalBucket(2, "posterior-thigh-knee-flexors"),
                CanonicalBucket(3, "major-hip-adductors"),
                CanonicalBucket(4, "lateral-knee-extensors"),
                CanonicalBucket(5, "gluteal-extensors"),
                CanonicalBucket(6, "spinal-extensors"),
                CanonicalBucket(7, "calf-deep-posterior-leg-plantar-foot"),
                CanonicalBucket(8, "soleus"),
                CanonicalBucket(9, "scapular-girdle"),
                CanonicalBucket(10, "shoulder-adductors-extensors"),
                CanonicalBucket(11, "abdominal-wall"),
                CanonicalBucket(12, "hip-abductors"),
                CanonicalBucket(13, "chest"),
                CanonicalBucket(14, "elbow-extensors"),
                CanonicalBucket(15, "hip-flexors"),
                CanonicalBucket(16, "anterior-lateral-lower-leg-dorsal-foot"),
                CanonicalBucket(17, "deep-hip-rotators"),
                CanonicalBucket(18, "shoulder-abductors"),
                CanonicalBucket(19, "forearm-flexors-pronators"),
                CanonicalBucket(20, "deep-intersegmental-back"),
                CanonicalBucket(21, "elbow-flexors"),
                CanonicalBucket(22, "breathing-muscles"),
                CanonicalBucket(23, "forearm-extensors-supinators"),
                CanonicalBucket(24, "rotator-cuff"),
                CanonicalBucket(25, "accessory-hip-adductors"),
                CanonicalBucket(26, "posterior-neck-suboccipital"),
                CanonicalBucket(27, "cranial-muscles"),
                CanonicalBucket(28, "anterior-lateral-neck-hyoid"),
                CanonicalBucket(29, "intrinsic-hand"),
                CanonicalBucket(30, "pelvic-floor-perineum")),
        };

        ValidateResolutions(resolutions);
        return resolutions;
    }

    private static WorkoutResolution Resolution(
        int minutes,
        params WorkoutGroup[] groups)
    {
        // Buckets are declared from largest to smallest estimated muscle mass so
        // their anatomical hierarchy remains easy to audit. Workouts deliberately
        // traverse that hierarchy in reverse for a small-to-large progression.
        WorkoutGroup[] resolutionGroups = groups
            .Reverse()
            .Select((group, index) => group with
            {
                Id = $"r{minutes}.{group.Id}",
                Order = index + 1,
            })
            .ToArray();
        return new WorkoutResolution(minutes, Array.AsReadOnly(resolutionGroups));
    }

    private static WorkoutGroup Bucket(
        string key,
        string displayName,
        int order,
        params int[] canonicalGroupIds)
    {
        return new WorkoutGroup(
            key,
            displayName,
            order,
            canonicalGroupIds
                .Select(id => (CanonicalMuscleGroup)id)
                .ToHashSet());
    }

    private static WorkoutGroup CanonicalBucket(int groupId, string key)
    {
        var canonicalGroup = (CanonicalMuscleGroup)groupId;
        return new WorkoutGroup(
            key,
            GetCanonicalDisplayName(canonicalGroup),
            groupId,
            new HashSet<CanonicalMuscleGroup> { canonicalGroup });
    }

    private static void ValidateResolutions(
        IReadOnlyDictionary<int, WorkoutResolution> resolutions)
    {
        CanonicalMuscleGroup[] canonicalGroups =
            Enum.GetValues<CanonicalMuscleGroup>();

        if (!resolutions.Keys.Order().SequenceEqual(SupportedMinutes) ||
            CranialMuscleInventory.Count != 10)
        {
            throw new InvalidOperationException(
                "The mass-grouping taxonomy has an invalid resolution inventory.");
        }

        foreach ((int minutes, WorkoutResolution resolution) in resolutions)
        {
            CanonicalMuscleGroup[] flattened = resolution.Groups
                .SelectMany(group => group.CanonicalGroups)
                .ToArray();
            bool invalid = resolution.Minutes != minutes ||
                resolution.Groups.Count != minutes ||
                !resolution.Groups.Select(group => group.Order)
                    .SequenceEqual(Enumerable.Range(1, minutes)) ||
                resolution.Groups.Select(group => group.Id).Distinct().Count() != minutes ||
                flattened.Length != canonicalGroups.Length ||
                flattened.Distinct().Count() != canonicalGroups.Length ||
                !flattened.Order().SequenceEqual(canonicalGroups.Order());

            if (invalid)
            {
                throw new InvalidOperationException(
                    $"The {minutes}-minute mass-grouping roll-up is incomplete or duplicated.");
            }
        }
    }
}
