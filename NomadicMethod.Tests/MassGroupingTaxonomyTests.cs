using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class MassGroupingTaxonomyTests
{
    private static readonly IReadOnlyDictionary<int, (string Id, string Name)[]>
        ExpectedGroupsDescendingByMass = new Dictionary<int, (string, string)[]>
        {
            [3] =
            [
                ("r3.lower-limbs", "Lower limbs"),
                ("r3.torso-pelvic-complex", "Torso and pelvic complex"),
                ("r3.head-neck-upper-limbs", "Head, neck and upper limbs"),
            ],
            [5] =
            [
                ("r5.hips-thighs", "Hips and thighs"),
                ("r5.torso", "Torso"),
                ("r5.lower-legs-feet", "Lower legs and feet"),
                ("r5.upper-limbs", "Upper limbs"),
                ("r5.head-neck-shoulder-girdle", "Head, neck and shoulder girdle"),
            ],
            [7] =
            [
                ("r7.torso", "Torso"),
                ("r7.knee-extensors", "Knee extensors"),
                ("r7.head-neck-upper-limbs", "Head, neck and upper limbs"),
                ("r7.lower-legs-feet", "Lower legs and feet"),
                ("r7.hip-flexors-adductors", "Hip flexors and adductors"),
                ("r7.gluteals-deep-hip", "Gluteals and deep hip stabilizers"),
                ("r7.posterior-thigh-knee-flexors", "Posterior thigh and knee flexors"),
            ],
            [10] =
            [
                ("r10.medial-deep-knee-extensors", "Medial and deep knee extensors"),
                ("r10.posterior-thigh-knee-flexors", "Posterior thigh and knee flexors"),
                ("r10.hip-flexors-adductors", "Hip flexors and adductors"),
                ("r10.gluteals-deep-hip", "Gluteals and deep hip stabilizers"),
                ("r10.back-abdominal-pelvic-floor", "Back, abdominal wall and pelvic floor"),
                ("r10.lateral-knee-extensors", "Lateral knee extensors"),
                ("r10.head-neck-scapular-chest-breathing", "Head, neck, scapular girdle, chest and breathing"),
                ("r10.posterior-lower-leg-plantar-foot", "Posterior lower leg and plantar foot"),
                ("r10.upper-limbs", "Upper limbs"),
                ("r10.anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot"),
            ],
            [15] =
            [
                ("r15.medial-deep-knee-extensors", "Medial and deep knee extensors"),
                ("r15.posterior-thigh-knee-flexors", "Posterior thigh and knee flexors"),
                ("r15.hip-adductors", "Hip adductors"),
                ("r15.lateral-knee-extensors", "Lateral knee extensors"),
                ("r15.gluteal-extensors", "Gluteal extensors"),
                ("r15.posterior-lower-leg-plantar-foot", "Posterior lower leg and plantar foot"),
                ("r15.back-spinal-stabilization", "Back and spinal stabilization"),
                ("r15.scapular-chest-breathing", "Scapular girdle, chest and breathing"),
                ("r15.lateral-deep-hip-stabilizers", "Lateral and deep hip stabilizers"),
                ("r15.arm-forearm-hand", "Arm, forearm and hand"),
                ("r15.abdominal-pelvic-floor", "Abdominal wall and pelvic floor"),
                ("r15.shoulder", "Shoulder"),
                ("r15.hip-flexors", "Hip flexors"),
                ("r15.anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot"),
                ("r15.head-neck", "Head and neck"),
            ],
            [20] =
            [
                ("r20.medial-deep-knee-extensors", "Medial and deep knee extensors"),
                ("r20.posterior-thigh-knee-flexors", "Posterior thigh and knee flexors"),
                ("r20.major-hip-adductors", "Major hip adductors"),
                ("r20.lateral-knee-extensors", "Lateral knee extensors"),
                ("r20.gluteal-extensors", "Gluteal extensors"),
                ("r20.soleus", "Soleus"),
                ("r20.back-spinal-stabilization", "Back and spinal stabilization"),
                ("r20.calf-flexors-plantar-foot", "Calf flexors and plantar foot"),
                ("r20.scapular-girdle", "Scapular girdle"),
                ("r20.chest-breathing", "Chest and breathing"),
                ("r20.shoulder-adduction-extension", "Shoulder adduction and extension"),
                ("r20.abdominal-pelvic-floor", "Abdominal wall and pelvic floor"),
                ("r20.lateral-deep-hip-stabilizers", "Lateral and deep hip stabilizers"),
                ("r20.upper-arm", "Upper arm"),
                ("r20.hip-flexors", "Hip flexors"),
                ("r20.anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot"),
                ("r20.shoulder-abduction-rotation", "Shoulder abduction and rotation"),
                ("r20.forearm-hand", "Forearm and hand"),
                ("r20.accessory-hip-adductors", "Accessory hip adductors"),
                ("r20.head-neck", "Head and neck"),
            ],
            [30] =
            [
                ("r30.medial-deep-knee-extensors", "Medial and deep knee extensors"),
                ("r30.posterior-thigh-knee-flexors", "Posterior thigh and knee flexors"),
                ("r30.major-hip-adductors", "Major hip adductors"),
                ("r30.lateral-knee-extensors", "Lateral knee extensors"),
                ("r30.gluteal-extensors", "Gluteal extensors"),
                ("r30.spinal-extensors", "Spinal extensors"),
                ("r30.calf-deep-posterior-leg-plantar-foot", "Calf, deep posterior leg and plantar foot"),
                ("r30.soleus", "Soleus"),
                ("r30.scapular-girdle", "Scapular girdle"),
                ("r30.shoulder-adductors-extensors", "Shoulder adductors and extensors"),
                ("r30.abdominal-wall", "Abdominal wall"),
                ("r30.hip-abductors", "Hip abductors"),
                ("r30.chest", "Chest"),
                ("r30.elbow-extensors", "Elbow extensors"),
                ("r30.hip-flexors", "Hip flexors"),
                ("r30.anterior-lateral-lower-leg-dorsal-foot", "Anterior/lateral lower leg and dorsal foot"),
                ("r30.deep-hip-rotators", "Deep hip rotators"),
                ("r30.shoulder-abductors", "Shoulder abductors"),
                ("r30.forearm-flexors-pronators", "Forearm flexors and pronators"),
                ("r30.deep-intersegmental-back", "Deep and intersegmental back"),
                ("r30.elbow-flexors", "Elbow flexors"),
                ("r30.breathing-muscles", "Breathing muscles"),
                ("r30.forearm-extensors-supinators", "Forearm extensors and supinators"),
                ("r30.rotator-cuff", "Rotator cuff"),
                ("r30.accessory-hip-adductors", "Accessory hip adductors"),
                ("r30.posterior-neck-suboccipital", "Posterior neck and suboccipital muscles"),
                ("r30.cranial-muscles", "Cranial muscles"),
                ("r30.anterior-lateral-neck-hyoid", "Anterior/lateral neck and hyoid muscles"),
                ("r30.intrinsic-hand", "Intrinsic hand"),
                ("r30.pelvic-floor-perineum", "Pelvic floor and perineum"),
            ],
        };

    private static readonly int[][] ExpectedParentOrdersDescendingByMass =
    [
        [1, 1, 2, 1, 1, 1, 1],
        [1, 1, 7, 2, 2, 2, 2],
        [1, 1, 5, 3, 3, 3, 3],
        [1, 1, 2, 6, 4, 4, 4],
        [1, 1, 6, 4, 5, 5, 5],
        [2, 2, 1, 5, 7, 7, 6],
        [1, 3, 4, 8, 6, 8, 7],
        [1, 3, 4, 8, 6, 6, 8],
        [3, 5, 3, 7, 8, 9, 9],
        [3, 4, 3, 9, 12, 11, 10],
        [2, 2, 1, 5, 11, 12, 11],
        [1, 1, 6, 4, 9, 13, 12],
        [2, 2, 1, 7, 8, 10, 13],
        [3, 4, 3, 9, 10, 14, 14],
        [1, 1, 5, 3, 13, 15, 15],
        [1, 3, 4, 10, 14, 16, 16],
        [1, 1, 6, 4, 9, 13, 17],
        [3, 4, 3, 9, 12, 17, 18],
        [3, 4, 3, 9, 10, 18, 19],
        [2, 2, 1, 5, 7, 7, 20],
        [3, 4, 3, 9, 10, 14, 21],
        [2, 2, 1, 7, 8, 10, 22],
        [3, 4, 3, 9, 10, 18, 23],
        [3, 4, 3, 9, 12, 17, 24],
        [1, 1, 5, 3, 3, 19, 25],
        [3, 5, 3, 7, 15, 20, 26],
        [3, 5, 3, 7, 15, 20, 27],
        [3, 5, 3, 7, 15, 20, 28],
        [3, 4, 3, 9, 10, 18, 29],
        [2, 2, 1, 5, 11, 12, 30],
    ];

    [Fact]
    public void SupportedMinutesAreExactAndOrdered()
    {
        Assert.Equal(
            [3, 5, 7, 10, 15, 20, 30],
            MassGroupingTaxonomy.SupportedMinutes);
        Assert.Equal(
            [3, 5, 7, 10, 15, 20, 30, 45, 60, 90],
            ExerciseSessionService.SupportedWorkoutMinutes);
    }

    [Fact]
    public void ResolutionSchedulesRunFromSmallestToLargestMass()
    {
        foreach ((int minutes, (string Id, string Name)[] descending) in
            ExpectedGroupsDescendingByMass)
        {
            WorkoutResolution resolution = MassGroupingTaxonomy.GetResolution(minutes);
            (string Id, string Name)[] expected = descending.Reverse().ToArray();

            Assert.Equal(minutes, resolution.Minutes);
            Assert.Equal(minutes, resolution.Groups.Count);
            Assert.Equal(Enumerable.Range(1, minutes), resolution.Groups.Select(group => group.Order));
            Assert.Equal(expected.Select(group => group.Id), resolution.Groups.Select(group => group.Id));
            Assert.Equal(expected.Select(group => group.Name),
                resolution.Groups.Select(group => group.DisplayName));
        }
    }

    [Fact]
    public void EveryCanonicalLeafAppearsExactlyOnceAtEveryResolution()
    {
        CanonicalMuscleGroup[] expected = Enum.GetValues<CanonicalMuscleGroup>();
        Assert.Equal(30, expected.Length);

        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            CanonicalMuscleGroup[] actual = MassGroupingTaxonomy.GetResolution(minutes)
                .Groups
                .SelectMany(group => group.CanonicalGroups)
                .ToArray();

            Assert.Equal(30, actual.Length);
            Assert.Equal(expected.Order(), actual.Distinct().Order());
        }
    }

    [Fact]
    public void LeafToBucketMappingsAndAscendingScheduleOrdersAreStable()
    {
        int[] resolutions = [3, 5, 7, 10, 15, 20, 30];
        CanonicalMuscleGroup[] leaves = Enum.GetValues<CanonicalMuscleGroup>();

        for (int leafIndex = 0; leafIndex < leaves.Length; leafIndex++)
        {
            int[] actual = resolutions
                .Select(minutes => MassGroupingTaxonomy.GetGroup(minutes, leaves[leafIndex]).Order)
                .ToArray();
            int[] expected = ExpectedParentOrdersDescendingByMass[leafIndex]
                .Select((descendingOrder, resolutionIndex) =>
                    resolutions[resolutionIndex] + 1 - descendingOrder)
                .ToArray();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void CanonicalNumericIdsAndDisplayNamesAreStable()
    {
        string[] expectedNames =
        [
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
        ];

        CanonicalMuscleGroup[] groups = Enum.GetValues<CanonicalMuscleGroup>();
        Assert.Equal(Enumerable.Range(1, 30), groups.Select(group => (int)group));
        Assert.Equal(expectedNames, groups.Select(MassGroupingTaxonomy.GetCanonicalDisplayName));
    }

    [Fact]
    public void CranialInventoryIsExplicitAndStable()
    {
        string[] expected =
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
        ];
        Assert.Equal(expected, MassGroupingTaxonomy.CranialMuscleInventory);
    }

    [Theory]
    [InlineData(-100, 3)]
    [InlineData(0, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    [InlineData(6, 7)]
    [InlineData(8, 7)]
    [InlineData(9, 10)]
    [InlineData(12, 10)]
    [InlineData(13, 15)]
    [InlineData(17, 15)]
    [InlineData(18, 20)]
    [InlineData(25, 30)]
    [InlineData(100, 30)]
    public void NormalizeTaxonomyMinutesUsesNearestResolutionAndRoundsTiesUp(
        int requested,
        int expected)
    {
        Assert.Equal(expected, MassGroupingTaxonomy.NormalizeMinutes(requested));
    }

    [Theory]
    [InlineData(-100, 3)]
    [InlineData(0, 3)]
    [InlineData(6, 7)]
    [InlineData(25, 30)]
    [InlineData(37, 30)]
    [InlineData(38, 45)]
    [InlineData(52, 45)]
    [InlineData(53, 60)]
    [InlineData(75, 90)]
    [InlineData(100, 90)]
    public void NormalizeWorkoutMinutesIncludesLongDurationsAndRoundsTiesUp(
        int requested,
        int expected)
    {
        Assert.Equal(
            expected,
            ExerciseSessionService.NormalizeLastWorkoutMinutes(requested));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(25)]
    [InlineData(31)]
    public void UnsupportedResolutionThrows(int minutes)
    {
        Assert.False(ExerciseSessionService.IsValidWorkoutMinutes(minutes));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MassGroupingTaxonomy.GetResolution(minutes));
    }
}
