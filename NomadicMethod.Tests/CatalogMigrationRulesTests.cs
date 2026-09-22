using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class CatalogMigrationRulesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void AdditiveCatalogPreservesExistingIdentityMediaAndScore()
    {
        Exercise existing = Exercise(
            7,
            "Existing movement",
            "exercise_0007.mp4",
            score: 99);
        Exercise secondExisting = Exercise(
            8,
            "Second existing movement",
            "exercise_0008.mp4",
            score: -99);
        Exercise added = Exercise(31, "Added movement", "exercise_0031.mp4");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [existing.Id] = new(existing.Name, existing.Video, -4),
            [secondExisting.Id] = new(
                secondExisting.Name,
                secondExisting.Video,
                6),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [existing, secondExisting, added],
            stored);

        Assert.Equal([existing.Id, secondExisting.Id], preserved.Order());
        Assert.Equal(-4, stored[existing.Id].Score);
        Assert.Equal(6, stored[secondExisting.Id].Score);
        Assert.Equal(99, existing.Score);
        Assert.Equal(-99, secondExisting.Score);
        Assert.Equal(0, added.Score);
        Assert.DoesNotContain(added.Id, preserved);
    }

    [Fact]
    public void MigrationRejectsRemovalOrDemonstrationReplacement()
    {
        Exercise existing = Exercise(7, "Existing movement", "exercise_0007.mp4");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [existing.Id] = new(existing.Name, existing.Video, -4),
        };

        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([], stored));

        Exercise changedMedia = Exercise(
            existing.Id,
            existing.Name,
            "replacement.mp4");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedMedia], stored));

        Exercise changedName = Exercise(
            existing.Id,
            "Renamed movement",
            existing.Video);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedName], stored));
    }

    [Fact]
    public void ReviewedReplacementIsRetiredInsteadOfPreservingItsScore()
    {
        const int replacedId = 56;
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(
                "Retired movement",
                "exercise_0056.mp4",
                -7),
        };
        Exercise replacement = Exercise(
            replacedId,
            "Clear replacement movement",
            "exercise_0056.mp4",
            retiredName: "Retired movement");

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.Equal(378, CatalogMigrationRules.ReplacedExerciseIds.Count);
        Assert.Contains(replacedId, CatalogMigrationRules.ReplacedExerciseIds);
        Assert.DoesNotContain(replacedId, preserved);
        Assert.Equal(-7, stored[replacedId].Score);
        Assert.Equal(0, replacement.Score);

        var preNormalizationStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(
                "Alternating Retired movement",
                replacement.Video,
                -6),
        };
        IReadOnlySet<int> preNormalizationPreserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                preNormalizationStored);
        Assert.DoesNotContain(replacedId, preNormalizationPreserved);
        Assert.Equal(-6, preNormalizationStored[replacedId].Score);

        Exercise wrongRetiredName = Exercise(
            replacedId,
            replacement.Name,
            replacement.Video,
            retiredName: "Some other retired movement");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongRetiredName],
                stored));

        Exercise missingRetiredName = Exercise(
            replacedId,
            replacement.Name,
            replacement.Video);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [missingRetiredName],
                stored));

        Exercise wrongRetiredVideo = Exercise(
            replacedId,
            replacement.Name,
            "replacement.mp4",
            retiredName: "Retired movement");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongRetiredVideo],
                stored));
    }

    [Fact]
    public void RepeatedUpgradePreservesAnAlreadyReviewedReplacementAndItsScore()
    {
        const int replacedId = 56;
        const string video = "exercise_videos/exercise_0056.mp4";
        Exercise replacement = Exercise(
            replacedId,
            "Shibashi Shallow Squat with Arm Float",
            video,
            retiredName: "Hula Kāholo");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(replacement.Name, video, -7),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.Contains(replacedId, preserved);
        Assert.Equal(-7, stored[replacedId].Score);
    }

    [Theory]
    [InlineData(
        520,
        "Silent Vowel-Shape Sequence",
        "Mirror Facial-Expression Practice",
        "Scapular Clock")]
    [InlineData(
        521,
        "Smile-to-Neutral Transitions",
        "Smile at Yourself in the Mirror",
        "Scapular Figure Eight")]
    public void Version67MirrorReplacementsDiscardThePriorIdentityAndScore(
        int exerciseId,
        string storedName,
        string replacementName,
        string baselineRetiredName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:0000}.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            replacementName,
            video,
            retiredName: baselineRetiredName);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(storedName, video, -7),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Fact]
    public void ReplacingRestoredExerciseAcceptsEveryReviewedIdentityWithoutItsScore()
    {
        const int exerciseId = 266;
        const string video = "exercise_videos/exercise_0266.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            "Alternating T-Arm Lifts",
            video,
            retiredName: "Standing Palms-Up Arm Raise");
        var replacedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Zyzz Diagonal-Reach Pose Hold",
                video,
                -7),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            replacedStored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, replacedStored[exerciseId].Score);

        var restoredStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Standing Palms-Up Arm Raise", video, -3),
        };
        preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            restoredStored);
        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-3, restoredStored[exerciseId].Score);

        var currentStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(replacement.Name, video, -2),
        };
        preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            currentStored);
        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-2, currentStored[exerciseId].Score);

        var unrelatedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Unrelated movement", video, -5),
        };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                unrelatedStored));

        var wrongVideoStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Zyzz Diagonal-Reach Pose Hold",
                "different-video.mp4",
                -5),
        };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                wrongVideoStored));
    }

    [Fact]
    public void ClarifiedStepThroughNamePreservesIdentityAndScore()
    {
        const int exerciseId = 231;
        const string video = "exercise_videos/exercise_0231.mp4";
        Exercise normalized = LoadBundledExercise(exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Karate Reverse Punch", video, -3),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-3, stored[exerciseId].Score);
    }

    [Fact]
    public void ClarifiedStepThroughNameAcceptsHistoricalAlternatingIdentity()
    {
        const int exerciseId = 231;
        const string video = "exercise_videos/exercise_0231.mp4";
        Exercise normalized = LoadBundledExercise(exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Alternating Karate Reverse Punch", video, -4),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-4, stored[exerciseId].Score);
    }

    [Fact]
    public void ReplacementAcceptsHistoricalUnprefixedRetiredIdentity()
    {
        const int exerciseId = 223;
        const string video = "exercise_videos/exercise_0223.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            "Self-Resisted Forearm Supination Hold",
            video,
            retiredName: "Alternating Karate Inside Block (Uchi-Uke)");
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Karate Inside Block (Uchi-Uke)", video, -6),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-6, stored[exerciseId].Score);
    }

    [Theory]
    [InlineData(
        617,
        "Alternating Standing Side-Leg Circles",
        "Standing Forward Side-Leg Circles")]
    public void ClarityCorrectionAcceptsHistoricalAlternatingIdentity(
        int exerciseId,
        string historicalName,
        string currentName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        Exercise corrected = Exercise(exerciseId, currentName, video);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(historicalName, video, -2),
        };

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [corrected],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-2, stored[exerciseId].Score);
    }

    [Theory]
    [InlineData(135, "Standing Snow Angels", "Mountain Pose to Upward Salute")]
    [InlineData(195, "Lateral Lunge to Balance", "Ballet Degage a la Seconde")]
    [InlineData(201, "Shibashi Split-Stance Rock and Palm Press", "Alternating Boxing Jab")]
    [InlineData(211, "Open-Finger Wrist Extension", "Karate Backfist Strike (Uraken-Uchi)")]
    [InlineData(212, "Bent-Over Triceps Pulse", "Karate Palm-Heel Strike (Teisho)")]
    [InlineData(213, "Open-Finger Wrist Flexion", "Karate Hammer-Fist Strike (Tetsui-Uchi)")]
    [InlineData(214, "Neutral-Fist Wrist Flexion and Extension", "Wing Chun Biu-Sau Palm Strike")]
    [InlineData(215, "Up-and-Down Wrist Glides", "Self-Resisted Wrist Radial-Deviation Pulses")]
    [InlineData(216, "Side-to-Side Wrist Glides", "Self-Resisted Wrist Ulnar-Deviation Pulses")]
    [InlineData(217, "Bilateral Wrist Figure Eights", "Self-Resisted Wrist-Extension Pulses")]
    [InlineData(218, "Hook-to-Fist Tendon Glides", "Self-Resisted Wrist-Flexion Pulses")]
    [InlineData(223, "Self-Resisted Forearm Supination Hold", "Alternating Karate Inside Block (Uchi-Uke)")]
    [InlineData(224, "Opposite-Hand-Resisted Multi-Direction Wrist Hold", "Alternating Karate Downward Sweep Block (Gedan-Barai)")]
    [InlineData(232, "Palms-Down Fist Wrist Flexion and Extension", "Karate Knife-Hand Chop")]
    [InlineData(233, "Bilateral Wrist Circles", "Karate Ridge-Hand Strike (Haito-Uchi)")]
    [InlineData(234, "Opposite-Hand-Resisted Thumb Opposition Hold", "Karate Flat-Fist Strike (Hiraken)")]
    [InlineData(236, "Alternating Hand Open and Close", "Karate Spear-Hand Strike (Nukite)")]
    [InlineData(237, "Opposed Thumb-and-Index Extension Isometric", "Forearm Pronation and Supination")]
    [InlineData(239, "Self-Resisted Finger Spread", "Ninja Fireball Hand-Seal Sequence")]
    [InlineData(240, "Self-Resisted Finger Squeeze", "Ninja Shadow-Possession Hand-Seal Sequence")]
    [InlineData(241, "Ninja Monkey Hand-Seal Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(242, "Ninja Boar Hand-Seal Hold", "Ninja Shadow-Clone Hand-Seal Sequence")]
    [InlineData(245, "Opposite-Hand-Resisted Elbow-Flexion Hold", "Alternating Karate Rising Block (Age-Uke)")]
    [InlineData(256, "Bent-Over Straight-Arm Lat Sweeps", "Self-Resisted Overhead Pull Hold")]
    [InlineData(257, "Karate Knife-Hand Block", "Self-Resisted Chest-Level Pull Hold")]
    [InlineData(260, "Standing Triceps Kickbacks", "Behind-the-Back Self-Resisted Press")]
    [InlineData(266, "Alternating T-Arm Lifts", "Standing Palms-Up Arm Raise")]
    [InlineData(268, "Self-Resisted External-Rotation Push-Out", "Self-Resisted External-Rotation Isometric")]
    [InlineData(269, "C-Rotation Arm Curls", "Self-Resisted Curl-and-Press")]
    [InlineData(270, "Goalpost Elbow Open-and-Close", "Palm-Squeeze Forward Press")]
    [InlineData(274, "Side-Step Alternating High Curl", "Dynamic-Resistance Lat Pulldown")]
    [InlineData(276, "Alternating Diagonal Overhead Reach-and-Pull", "Dynamic-Resistance High Chest Press")]
    [InlineData(280, "Alternating Forward-and-Side Arm Press", "Ringing-the-Towel Wrist Inversion")]
    [InlineData(283, "Sequential Finger Waves", "Qigong Fist Rotation")]
    [InlineData(289, "Ninja Horse Hand-Seal Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(291, "Ninja Tiger Hand-Seal Hold", "Black Dragon Enters the Cave")]
    [InlineData(293, "Ninja Dragon Hand-Seal Hold", "Sword-Fingers Qigong Sequence")]
    [InlineData(294, "Ninja Rat Hand-Seal Hold", "Tiger-Claw Grip Flow")]
    [InlineData(414, "Heel Raises with Fixed-Thumb Head Turns", "Ear-to-Shoulder Glide")]
    [InlineData(415, "Heel Raises with Fixed-Thumb Head Nods", "Chin-to-Collarbone Turn")]
    [InlineData(416, "Heel Raises with Fixed-Thumb Head Tilts", "Diagonal Head Tilt")]
    [InlineData(418, "Heel-Bounce Horizontal Thumb Tracking", "Forward-and-Back Head Translation")]
    [InlineData(419, "Heel-Bounce Vertical Thumb Tracking", "Occipital Nod")]
    [InlineData(482, "Front Half Neck Circles", "Continuous Spot-Turn Drill")]
    [InlineData(483, "Clockwise Full Neck Circles", "Pirouette Spotting Drill")]
    [InlineData(490, "Assisted Cheek Lift", "Bharatanatyam Alolita Shiro")]
    [InlineData(491, "Cheek-Firming Air Hold", "Bharatanatyam Dhuta Shiro")]
    [InlineData(492, "Forehead Knuckle Massage", "Bharatanatyam Kampita Shiro")]
    [InlineData(493, "Face-and-Neck Lymphatic Sweep", "Alternating Bharatanatyam Paravritta Shiro")]
    [InlineData(495, "Jawline Knuckle Massage", "Bharatanatyam Parivahita Shiro")]
    [InlineData(499, "Eyebrow Pinch Massage", "Bharatanatyam Tiraschina Griva")]
    [InlineData(500, "Eye-Socket Finger Circles", "Bharatanatyam Parivartita Griva")]
    [InlineData(501, "Counterclockwise Full Neck Circles", "Standing Horizontal Saccades")]
    [InlineData(505, "Temple Circle Massage", "Maximal Smile and Relax")]
    [InlineData(506, "Cheek Pinch Massage", "Eyebrow Raise and Relax")]
    [InlineData(508, "Diagonal Arm Reach-to-Row", "Tongue Protrusion and Retraction")]
    [InlineData(512, "Upper-Cervical Erector Stretch", "Scapular Protraction")]
    [InlineData(513, "Standing Unilateral SCM Stretch", "Scapular Retraction")]
    [InlineData(572, "Wide-Stance Bent-Knee Rotational Stretch", "Tai Chi White Crane Opens Wings")]
    [InlineData(591, "Standing Speed-Bag Punches", "Bharatanatyam Natyarambhe Hold")]
    [InlineData(611, "Warrior II-Stance Hip Circles", "Pelvic-Floor Heel-Raise Lift")]
    [InlineData(636, "Alternating Curtsy Floor Reach", "Deadlift Kickback")]
    [InlineData(649, "Standing Clamshell", "Standing Side-Leg Raise")]
    [InlineData(677, "T-Arm Side-to-Side Sweep", "Alternating Belly-Dance Hip Drop")]
    [InlineData(681, "Rear-Arm Sweep to Front Squeeze", "Belly-Dance Horizontal Figure Eight")]
    [InlineData(743, "Standing Backward Arm Circles", "Clasped-Hands-Behind-Back Chest Opener")]
    [InlineData(745, "Standing Overhead Presses", "Dynamic Hug")]
    [InlineData(843, "Behind-Back Wrist-Pull Neck Stretch", "Standing Cobra Pose")]
    public void SecondGenerationReplacementAcceptsImmediatelyPriorIdentity(
        int replacedId,
        string priorName,
        string baselineRetiredName)
    {
        string video = $"exercise_{replacedId:D4}.mp4";
        Exercise replacement = Exercise(
            replacedId,
            "Second-generation replacement",
            video,
            retiredName: baselineRetiredName);

        foreach (string storedName in
            new[] { priorName, $"Alternating {priorName}" })
        {
            var stored = new Dictionary<int, StoredExerciseSnapshot>
            {
                [replacedId] = new(storedName, video, -7),
            };

            IReadOnlySet<int> preserved =
                CatalogMigrationRules.ValidatePreservedCatalog(
                    [replacement],
                    stored);

            Assert.DoesNotContain(replacedId, preserved);
            Assert.Equal(-7, stored[replacedId].Score);
        }
    }

    [Fact]
    public void SecondGenerationReplacementStillRequiresBaselineAndStableVideo()
    {
        const int replacedId = 291;
        const string priorName = "Ninja Tiger Hand-Seal Hold";
        const string baselineRetiredName = "Black Dragon Enters the Cave";
        const string video = "exercise_0291.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new(priorName, video, -5),
        };

        Exercise wrongBaseline = Exercise(
            replacedId,
            "Second-generation replacement",
            video,
            retiredName: "Unrelated baseline");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongBaseline],
                stored));

        Exercise wrongVideo = Exercise(
            replacedId,
            "Second-generation replacement",
            "replacement.mp4",
            retiredName: baselineRetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongVideo],
                stored));

        var unrelatedStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [replacedId] = new("Unrelated prior name", video, -5),
        };
        Exercise validReplacement = Exercise(
            replacedId,
            "Second-generation replacement",
            video,
            retiredName: baselineRetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [validReplacement],
                unrelatedStored));
    }

    [Fact]
    public void CatalogRevisionDropsChangedTransientReferencesButPreservesKeepMarkers()
    {
        const int replacedId = 223;
        int[] latestReplacementIds =
        [
            211, 213, 214, 215, 218, 223, 224, 225, 234,
            236, 237, 239, 240, 241, 242, 245, 246, 283, 289,
        ];
        const int historicalReplacementId = 56;
        const int retainedId = 22;
        const string replacedGroup = "group.replaced";
        const string historicalReplacementGroup = "group.historical";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 12,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [replacedGroup] = replacedId,
                [historicalReplacementGroup] = historicalReplacementId,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [replacedGroup] = ExerciseOutcome.X,
                [historicalReplacementGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = replacedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = replacedId,
            PendingScoreValue = -8,
            LastKeptExerciseIds = [.. latestReplacementIds, historicalReplacementId, retainedId],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
        Assert.DoesNotContain(replacedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(replacedGroup, state.Outcomes);
        Assert.Equal(
            historicalReplacementId,
            state.SelectedExerciseIds[historicalReplacementGroup]);
        Assert.Equal(
            ExerciseOutcome.Tick,
            state.Outcomes[historicalReplacementGroup]);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.All(latestReplacementIds, exerciseId =>
            Assert.Equal(!CatalogMigrationRules.ScoreInvalidationsByRevision[73].Contains(exerciseId),
                state.LastKeptExerciseIds.Contains(exerciseId)));
        Assert.Contains(historicalReplacementId, state.LastKeptExerciseIds);
        Assert.Contains(retainedId, state.LastKeptExerciseIds);

        state.SelectedExerciseIds[replacedGroup] = replacedId;
        state.PendingScoreExerciseId = replacedId;
        state.PendingScoreValue = -1;

        Assert.False(CatalogMigrationRules.ReconcileWorkoutState(state));
        Assert.Equal(replacedId, state.SelectedExerciseIds[replacedGroup]);
        Assert.Equal(replacedId, state.PendingScoreExerciseId);
        Assert.Equal(-1, state.PendingScoreValue);
    }

    [Fact]
    public void LegacyCatalogRevisionStillDropsAllHistoricalReplacements()
    {
        const int historicalReplacementId = 56;
        const string groupId = "group.historical";
        var state = new WorkoutState
        {
            CatalogRevision = 2,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = historicalReplacementId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.X,
            },
            PendingScoreExerciseId = historicalReplacementId,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MigrationAllowsOnlyExactAlternatingPrefixRemovalForTimedSides()
    {
        const string video = "exercise_0007.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [7] = new("Alternating Side Stretch", video, -4),
        };
        Exercise normalized = Exercise(
            7,
            "Side Stretch",
            video,
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(7, preserved);
        Assert.Equal(-4, stored[7].Score);

        Exercise continuous = Exercise(7, "Side Stretch", video);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([continuous], stored));

        Exercise arbitrary = Exercise(
            7,
            "Different Stretch",
            video,
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([arbitrary], stored));

        Exercise changedMedia = Exercise(
            7,
            "Side Stretch",
            "replacement.mp4",
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedMedia], stored));
    }

    [Fact]
    public void TimedSideNormalizationPreservesAlreadyReviewedReplacement()
    {
        const int exerciseId = 845;
        const string video = "exercise_videos/exercise_0845.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Alternating Overhead Side Stretch",
                video,
                -7),
        };
        Exercise normalized = Exercise(
            exerciseId,
            "Overhead Side Stretch",
            video,
            sideSequence: ExerciseSideSequence.ScreenRightThenLeft,
            retiredName: "Extended-Mountain Backline Reach and Lower");

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [normalized],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);

        Exercise continuous = Exercise(
            exerciseId,
            normalized.Name,
            video,
            retiredName: normalized.RetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([continuous], stored));

        Exercise changedMedia = Exercise(
            exerciseId,
            normalized.Name,
            "replacement.mp4",
            sideSequence: normalized.SideSequence,
            retiredName: normalized.RetiredName);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([changedMedia], stored));
    }

    [Fact]
    public void ReviewedExternalRotationReplacementRetiresTheCorrectedPriorIdentity()
    {
        const string video = "exercise_0268.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [268] = new(
                "Self-Resisted External-Rotation Push-Out",
                video,
                -3),
        };
        Exercise replacement = Exercise(
            268,
            "Thumbs-Up Diagonal Arm Raises",
            video,
            retiredName: "Self-Resisted External-Rotation Isometric");

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(268, preserved);
        Assert.Equal(-3, stored[268].Score);

        Exercise wrongId = Exercise(
            266,
            replacement.Name,
            video,
            retiredName: "Self-Resisted External-Rotation Isometric");
        var wrongStored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [266] = new(
                "Self-Resisted External-Rotation Push-Out",
                video,
                -3),
        };
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [wrongId],
                wrongStored));
    }

    [Theory]
    [InlineData(21, "Standing-Scale Balance", "Alternating Single-Leg Hinge with Forward Reach")]
    [InlineData(105, "Plie Squat", "Wide Turned-Out Squat")]
    [InlineData(119, "Squat to Calf Raise", "Tiptoe Walking Back and Forth")]
    [InlineData(139, "Wide-Squat Heel Raise", "Wide-Squat Alternating Heel Raises")]
    [InlineData(188, "Parallel Demi-Plie", "Narrow Turned-Out Shallow Squat")]
    [InlineData(197, "First-Position Plie-Releve", "Squat to Calf Raise")]
    [InlineData(198, "Second-Position Plie-Releve", "Wide Squat to Feet-Together Calf Raise")]
    [InlineData(199, "Alternating Deep Side Lunge", "Horse-Stance Squat")]
    [InlineData(255, "Standing Bent-Knee Calf Raise", "Deep-Squat Calf Raise")]
    [InlineData(145, "Standing Knee Extension", "Wall-Supported Standing Knee-Extension Hold")]
    [InlineData(256, "Self-Resisted Overhead Pull", "Overhead Side-Stretch Hold")]
    [InlineData(257, "Self-Resisted Chest-Level Pull", "Finger Spreading with Arms Held Forward")]
    [InlineData(258, "Self-Resisted Low Pull", "Alternating Karate Downward Blocks")]
    [InlineData(262, "Standing Hands-to-Thigh Abdominal Press", "Standing Bicycle Crunches")]
    [InlineData(270, "Bodyweight Svend Press", "Goalpost Arm Hold")]
    [InlineData(290, "Universe-in-Motion Qigong", "Thumb and Little-Finger Switches")]
    [InlineData(394, "Standing Arms Open and Close", "Alternating Cross-Body Knee with Arm Sweep")]
    [InlineData(395, "Standing Overhead Arm Sweep", "Alternating Knee Lift and Overhead Reach")]
    [InlineData(398, "Standing Hug and Arm Expansion", "Inhale Arms Open, Exhale Arms Together")]
    [InlineData(399, "Shallow Squat with Chest-Opening Arms", "Inhale Chest Open, Exhale Arms Close with Shallow Squat")]
    [InlineData(400, "Shallow Squat with Overhead Arm Circle", "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down")]
    [InlineData(401, "Alternating Weight Shift with Arm Swing", "Alternating Inhale-Twist, Exhale-Push")]
    [InlineData(402, "Shibashi Rowing-a-Boat Breathing", "Shallow Squat with Rowing Arm Circle")]
    [InlineData(403, "Shibashi Alternating Pushing-Palms Breathing", "Alternating Palm Press with Weight Shift")]
    [InlineData(404, "Shibashi Alternating Punch Breathing", "Wide-Stance Alternating Slow Punch")]
    [InlineData(405, "Shibashi Flying-Wild-Goose Breathing", "Shallow Squat with Wing Arm Raise")]
    [InlineData(406, "Shibashi Spinning-Wheels Breathing", "Standing Wheel Arm Circles")]
    [InlineData(409, "Neck Controlled Articular Rotation", "Full Neck Circles")]
    [InlineData(493, "Track Finger Upper-Right to Lower-Left", "Diagonal Finger Tracking")]
    [InlineData(958, "Standing Alternating Side Bend", "Standing Overhead Side-Stretch Hold")]
    [InlineData(425, "Chin-Tuck Isometric", "Feet-Together Head Turns")]
    [InlineData(396, "Unsupported Single-Leg Balance", "Standing Front-to-Side Knee Lifts")]
    [InlineData(510, "Clasped-Hands Chest-Opening Forward Fold", "Clasped-Hands Chest-Opening Forward-Fold Hold")]
    [InlineData(588, "Belly-Dance Alternating Shoulder Roll", "Belly-Dance Alternating Shoulder Rolls")]
    [InlineData(617, "Standing Side-Leg Circles", "Standing Forward Side-Leg Circles")]
    [InlineData(626, "Sumo Stance", "Sumo Squat Hold")]
    [InlineData(712, "Standing Arms-Back Chest Opener", "Standing Arms-Back Chest-Opener Hold")]
    [InlineData(969, "Chair-Pose Core Hold", "Chair-Pose Hold")]
    [InlineData(1000, "Standing Forward Fold", "Standing Forward-Fold Hold")]
    [InlineData(136, "Goddess Pose", "Wide Squat Hold with Hands on Thighs")]
    [InlineData(225, "Clenched-Fist Wrist Extensor Stretch", "Opposite-Hand Fist-Down Wrist Stretch")]
    [InlineData(241, "Hook-Fist Tendon Glide", "Isometric Palm Press Hold")]
    [InlineData(242, "Full-Fist Tendon Glide", "Jazz Square")]
    [InlineData(248, "Side-Tap Palm Pushes", "Alternating Side-Tap Palm Pushes")]
    [InlineData(283, "Straight-Fist Tendon Glide", "Rear-Hand Palm Strike")]
    [InlineData(291, "Open-to-Claw Tendon Glide", "Inward Knife-Hand Strikes")]
    [InlineData(293, "Finger-Web Space Stretch", "Opposite-Hand Thumb-Web Stretch")]
    [InlineData(683, "Alternating Palm-Up T-Arm Flips", "Alternating Palm-Up Shoulder Rotations")]
    [InlineData(214, "Forward Wrist Circles", "Single-Arm Wrist Circles")]
    [InlineData(223, "Forward Controlled Wrist Circles", "Controlled Wrist Circles")]
    [InlineData(755, "Reverse Wrist Circles", "Outward Wrist Circles")]
    [InlineData(756, "Reverse Controlled Wrist Circles", "Outward Controlled Wrist Circles")]
    [InlineData(758, "Reverse Knee-and-Ankle Circles", "Backward Knee-and-Ankle Circles")]
    [InlineData(94, "Mirror-Guided Lateral Weight Shift", "Lateral Weight Shift")]
    [InlineData(95, "Mirror-Guided Single-Leg Pelvic Control", "Single-Leg Knee-Raise Hold")]
    [InlineData(95, "Single-Leg Pelvic Control", "Single-Leg Knee-Raise Hold")]
    [InlineData(417, "Narrow Squat and Overhead Reach with Thumb Tracking", "Narrow-Stance Overhead-to-Toe Reach")]
    [InlineData(99, "Mirror-Guided Bent-Knee Front-to-Back Leg Swing", "Bent-Knee Front-to-Back Leg Swing")]
    [InlineData(100, "Mirror-Guided Bent-Knee Leg Swing with Pause", "Bent-Knee Leg Swing with Pause")]
    [InlineData(497, "Mirror-Guided Eyebrow Raise", "Eyebrow Raise")]
    [InlineData(498, "Mirror-Guided Firm Eye Closure", "Firm Eye Closure")]
    [InlineData(500, "Controlled Jaw Open and Close", "Straight Jaw Opening")]
    [InlineData(500, "Mirror-Guided Straight Jaw Opening", "Straight Jaw Opening")]
    [InlineData(511, "Mirror-Guided Lip Pucker", "Lip Pucker")]
    [InlineData(514, "Mirror-Guided Symmetric Smile", "Symmetric Smile")]
    [InlineData(524, "Mirror Front Double-Biceps Pose Hold", "Front Double-Biceps Posing")]
    [InlineData(524, "Front Double-Biceps Pose Hold", "Front Double-Biceps Posing")]
    [InlineData(525, "Mirror Front Lat-Spread Pose Hold", "Front Lat-Spread Posing")]
    [InlineData(525, "Front Lat-Spread Pose Hold", "Front Lat-Spread Posing")]
    [InlineData(526, "Mirror Side-Chest Pose Hold", "Side-Chest Posing")]
    [InlineData(526, "Side-Chest Pose Hold", "Side-Chest Posing")]
    [InlineData(527, "Mirror Side-Triceps Pose Hold", "Side-Triceps Posing")]
    [InlineData(527, "Side-Triceps Pose Hold", "Side-Triceps Posing")]
    [InlineData(528, "Mirror Abdominals-and-Thighs Pose Hold", "Abdominals-and-Thighs Posing")]
    [InlineData(528, "Abdominals-and-Thighs Pose Hold", "Abdominals-and-Thighs Posing")]
    [InlineData(790, "Mirror Most-Muscular Pose Hold", "Most-Muscular Posing, Hands on Thighs")]
    [InlineData(565, "Mini Squat with Forward Reach", "Mini-Squat Calf Raises with Forward Reach")]
    [InlineData(397, "Inhale Open, Exhale Cross-Body Side Tap", "Alternating Side Tap with Diagonal Reach")]
    public void HistoricalClarityCorrectionsRespectSubsequentReplacements(
        int exerciseId,
        string previousName,
        string correctedName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(previousName, video, -4),
        };
        Exercise corrected = LoadBundledExercise(exerciseId);
        Assert.Equal(correctedName, corrected.Name);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [corrected],
            stored);

        // These historical aliases belonged to movements subsequently replaced.
        int[] discardedIds = [241, 242, 256, 257, 258, 262, 270, 283, 290, 291, 394, 395, 425];
        Assert.Equal(!discardedIds.Contains(exerciseId), preserved.Contains(exerciseId));
        Assert.Equal(-4, stored[exerciseId].Score);
    }

    [Fact]
    public void LatestCatalogRevisionDropsEveryChangedExerciseReference()
    {
        const int replacedId = 212;
        const int retainedId = 101;
        const string replacedGroup = "group.replaced";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 17,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [replacedGroup] = replacedId,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [replacedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = replacedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestMillisecondsRemaining = 8_000,
            PendingRestPausedByUser = true,
            PendingRestKept = true,
            PendingScoreExerciseId = replacedId,
            PendingScoreValue = -2,
            LastKeptExerciseIds = [replacedId, retainedId],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(replacedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(replacedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(0, state.PendingRestMillisecondsRemaining);
        Assert.False(state.PendingRestPausedByUser);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Contains(replacedId, state.LastKeptExerciseIds);
        Assert.Contains(retainedId, state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void CatalogRevisionClearsProfiledRepeatedRoundProgressForReplacement()
    {
        const int replacedId = 420;
        const string storageKey = "p2|r30.hip-abductors";
        const string firstRound = "r30.hip-abductors.set1";
        const string repeatedRound = "r30.hip-abductors.set2";
        var state = new WorkoutState
        {
            CatalogRevision = 23,
            ActiveWorkoutMinutes = 60,
            ActiveWorkoutModifiers = WorkoutModifiers.Silence,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [storageKey] = replacedId,
                ["p2|r30.chest"] = 101,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [firstRound] = ExerciseOutcome.Tick,
                [repeatedRound] = ExerciseOutcome.X,
                ["r30.chest"] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = firstRound,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(storageKey, state.SelectedExerciseIds);
        Assert.DoesNotContain(firstRound, state.Outcomes);
        Assert.DoesNotContain(repeatedRound, state.Outcomes);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes["r30.chest"]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
    }

    [Fact]
    public void CatalogRevisionPreservesActiveProgressWhenOnlyInactiveProfileIsRetired()
    {
        const string activeStorageKey = "p1|r30.hip-abductors";
        const string inactiveStorageKey = "p2|r30.hip-abductors";
        const string activeRound = "r30.hip-abductors.set1";
        var state = new WorkoutState
        {
            CatalogRevision = 23,
            ActiveWorkoutMinutes = 45,
            ActiveWorkoutModifiers = WorkoutModifiers.Insect,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [activeStorageKey] = 101,
                [inactiveStorageKey] = 420,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [activeRound] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = activeRound,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.Equal(101, state.SelectedExerciseIds[activeStorageKey]);
        Assert.DoesNotContain(inactiveStorageKey, state.SelectedExerciseIds);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[activeRound]);
        Assert.Equal(activeRound, state.PendingRestGroupId);
        Assert.Equal(123456, state.PendingRestEndsAtUnixMilliseconds);
        Assert.True(state.PendingRestKept);
    }

    [Theory]
    [InlineData(115)]
    [InlineData(119)]
    [InlineData(140)]
    [InlineData(260)]
    [InlineData(326)]
    [InlineData(340)]
    [InlineData(512)]
    [InlineData(649)]
    public void LatestCatalogRevisionDropsOtherChangedExerciseReferences(
        int changedExerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 17,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = changedExerciseId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void LatestCatalogRevisionResetsOnlySemanticReplacementScores()
    {
        Assert.Equal(
            new HashSet<int> { 115, 212, 260, 512, 649 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[18]);

        Assert.DoesNotContain(119, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
        Assert.DoesNotContain(140, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
        Assert.DoesNotContain(326, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
        Assert.DoesNotContain(340, CatalogMigrationRules.ScoreInvalidationsByRevision[18]);
    }

    [Fact]
    public void UnclearExerciseReplacementRevisionResetsEveryChangedScore()
    {
        Assert.Equal(
            new HashSet<int>
            {
                211, 213, 214, 215, 218, 223, 224,
                236, 237, 241, 242, 245, 283, 289,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[20]);
    }

    [Fact]
    public void CatalogClarityResetRevisionResetsEveryReplacedIdentity()
    {
        Assert.Equal(
            new HashSet<int>
            {
                15, 16, 17, 19, 20, 31, 47, 97, 107, 135, 150, 169,
                179, 180, 193, 219, 220, 229, 230, 239, 241, 242, 248, 251,
                256, 257, 258, 262, 266, 268, 269, 270, 275, 278, 279, 282,
                283, 285, 286, 287, 291, 294, 314, 321, 326, 329, 390, 391,
                394, 395, 396, 397, 425, 507, 508, 513, 516, 572, 576, 577,
                615, 618, 677, 683, 685, 745, 816, 834,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[21]);
    }

    [Fact]
    public void ReviewerAuditRevisionResetsOnlySemanticReplacementScores()
    {
        Assert.Equal(
            new HashSet<int>
            {
                117, 135, 184, 186, 201, 211, 213, 229, 231, 234, 256, 257,
                263, 265, 266, 267, 269, 270, 289, 301, 572, 636, 677, 745,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[22]);
    }

    [Fact]
    public void ModifierCoverageRevisionResetsEveryNewReplacementScore()
    {
        Assert.Equal(
            new HashSet<int>
            {
                407, 408, 410, 411, 412, 413, 414, 415, 416, 417, 418, 419,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[23]);
    }

    [Fact]
    public void SilenceCatalogRevisionResetsEveryNoisyReplacementScore()
    {
        Assert.Equal(
            new HashSet<int>
            {
                420, 421, 424, 426, 427, 428, 429, 430, 431, 432, 433, 434,
            },
            CatalogMigrationRules.ScoreInvalidationsByRevision[24]);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(219)]
    [InlineData(248)]
    [InlineData(282)]
    [InlineData(390)]
    [InlineData(394)]
    [InlineData(395)]
    [InlineData(397)]
    [InlineData(508)]
    [InlineData(576)]
    [InlineData(577)]
    [InlineData(618)]
    [InlineData(816)]
    [InlineData(834)]
    public void UnilateralTimingRevisionRebuildsWorkoutButPreservesScore(
        int exerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 24,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = exerciseId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 25);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(282)]
    [InlineData(391)]
    [InlineData(507)]
    [InlineData(508)]
    [InlineData(577)]
    public void IllustrationCorrectionRevisionRebuildsWorkoutButPreservesScore(
        int exerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 25,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = exerciseId,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 26);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(231)]
    [InlineData(685)]
    [InlineData(687)]
    public void KarateDemonstrationCorrectionRevisionRebuildsWorkout(
        int exerciseId)
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 26,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = exerciseId,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.Equal(
            new HashSet<int> { 687 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[27]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(239, "Straight-Finger Knuckle Bends", "Ninja Fireball Hand-Seal Sequence")]
    [InlineData(326, "Staggered-Stance Jab-Cross", "Wide-Stance Alternating Straight Punches")]
    [InlineData(687, "Karate Middle Side Punch", "Belly-Dance Hip Shimmy")]
    [InlineData(251, "Arm Sweep to Forward Hinge", "Waiter's Bow")]
    [InlineData(251, "Standing Swan-Dive Hinge", "Waiter's Bow")]
    public void CatalogClarityResetAcceptsReviewedPreviousIdentityAndResetsIt(
        int exerciseId,
        string previousName,
        string baselineRetiredName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(previousName, video, -7),
        };
        Exercise replacement = Exercise(
            exerciseId,
            "Clear replacement",
            video,
            retiredName: baselineRetiredName);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(exerciseId, preserved);

        stored[exerciseId] = new(previousName, "wrong.mp4", -7);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([replacement], stored));
    }

    [Fact]
    public void ForwardFoldReplacementRevisionRebuildsWorkoutAndResetsScore()
    {
        const string groupId = "changed.group";
        var state = new WorkoutState
        {
            CatalogRevision = 27,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = 251,
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.Equal(
            new HashSet<int> { 251 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[28]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void ReactivatedReplacementRevisionDropsOnlyChangedProgressAndScores()
    {
        int[] changedIds =
        [
            435, 436, 437, 438, 439, 440, 441, 442, 443, 444, 445,
            446, 447, 448, 449, 450, 451, 452, 453, 454, 455, 456,
            457, 458, 459, 460, 461, 462, 463, 464, 465, 466, 468,
            469, 470, 471, 472, 473, 476, 478, 479, 480, 484, 485,
            486, 487, 488, 489, 494, 496, 517, 518, 519,
        ];
        var expectedIds = changedIds.ToHashSet();

        Assert.Equal(
            expectedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[29]);
        Assert.All(changedIds, exerciseId =>
            Assert.Contains(exerciseId, CatalogMigrationRules.ReplacedExerciseIds));

        const int retainedId = 22;
        const string changedGroup = "group.changed";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 28,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = changedIds[0],
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = changedIds[0],
            PendingScoreValue = -4,
            LastKeptExerciseIds = [changedIds[0], retainedId],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal([changedIds[0], retainedId], state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);

        var stored = changedIds.ToDictionary(
            exerciseId => exerciseId,
            exerciseId => new StoredExerciseSnapshot(
                $"Retired {exerciseId}",
                $"exercise_videos/exercise_{exerciseId:D4}.mp4",
                -exerciseId));
        stored[retainedId] = new(
            "Retained movement",
            "exercise_videos/exercise_0022.mp4",
            -7);
        Exercise[] bundled =
        [
            .. changedIds.Select(exerciseId => Exercise(
                exerciseId,
                $"Replacement {exerciseId}",
                $"exercise_videos/exercise_{exerciseId:D4}.mp4",
                retiredName: $"Retired {exerciseId}")),
            Exercise(
                retainedId,
                "Retained movement",
                "exercise_videos/exercise_0022.mp4"),
        ];

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            bundled,
            stored);

        Assert.DoesNotContain(changedIds, preserved.Contains);
        Assert.Contains(retainedId, preserved);
    }

    [Fact]
    public void MediaRepairRevisionRetiresInvalidAssetsAndResetsSemanticScores()
    {
        int[] changedIds =
        [
            229, 467, 474, 481, 483, 491, 493, 495, 497, 499,
            501, 504, 513, 516,
        ];
        int[] semanticIds = [229, 497, 501, 504, 513];
        const int retainedId = 22;
        const string changedGroup = "group.changed";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 29,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 467,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 501,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            semanticIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[30]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
        Assert.All(changedIds, exerciseId =>
            Assert.Contains(exerciseId, CatalogMigrationRules.ReplacedExerciseIds));
    }

    [Fact]
    public void MediaOnlyRepairPreservesPendingScoreRecovery()
    {
        var state = new WorkoutState
        {
            CatalogRevision = 29,
            PendingScoreExerciseId = 467,
            PendingScoreValue = -3,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.Equal(467, state.PendingScoreExerciseId);
        Assert.Equal(-3, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void HeelIllustrationCorrectionRevisionResetsChangedWorkoutAndPendingScore()
    {
        int[] changedIds = [414, 415, 416, 418, 419];
        const int retainedId = 22;
        const string changedGroup = "group.changed";
        const string retainedGroup = "group.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 30,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 414,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 419,
            PendingScoreValue = -4,
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            changedIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[31]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void SingleSideClarityRevisionResetsCorrectedReplacements()
    {
        int[] correctedReplacementIds = [31, 219, 395, 507, 577, 618, 654, 834];
        const string kneePullGroup = "group.knee-pull";
        const string highKneeReachGroup = "group.high-knee-reach";
        const string nameOnlyGroup = "group.name-only";
        var state = new WorkoutState
        {
            CatalogRevision = 31,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [kneePullGroup] = 31,
                [highKneeReachGroup] = 618,
                [nameOnlyGroup] = 914,
            },
            PendingScoreExerciseId = 31,
            PendingScoreValue = -3,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(kneePullGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(highKneeReachGroup, state.SelectedExerciseIds);
        Assert.Equal(914, state.SelectedExerciseIds[nameOnlyGroup]);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            correctedReplacementIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[32]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void DirectionSplitRevisionResetsEveryLinkedIdentity()
    {
        int[] linkedDirectionIds =
        [
            214, 223, 264, 288, 406, 409, 588, 608, 611, 743,
            755, 756, 757, 758, 759, 760, 761, 762, 763, 764,
        ];
        const int retainedId = 22;
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 32,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 264,
                [retainedGroup] = retainedId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 264,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(retainedId, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            linkedDirectionIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[33]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void AlternatingCorrectionRevisionRebuildsWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [98, 390, 508, 576, 816];
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 33,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 576,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 576,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(576, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[34]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(34));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void HighKneeAlternationCorrectionRebuildsWorkoutWithoutResettingScore()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 34,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 219,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 219,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(219, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            new HashSet<int> { 219 },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[35]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(35));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void VagueElbowStrikeReplacementRebuildsWorkoutAndResetsScore()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 35,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 684,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 684,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Equal(
            new HashSet<int> { 684 },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[36]);
        Assert.Equal(
            new HashSet<int> { 684 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[36]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void AlternatingLoopCorrectionsRebuildWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [31, 176, 195, 391, 413, 884, 885];
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 36,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 884,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 884,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(884, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[37]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(37));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void DirectionNameCorrectionPreservesScoreAcrossLaterPlacementRebuild()
    {
        const string groupId = "direction.group";
        var state = new WorkoutState
        {
            CatalogRevision = 37,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = 223,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = groupId,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 223,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        // Revision 73 rebuilds this movement's corrected placement.
        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.False(state.PendingRestKept);
        Assert.Equal(223, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.False(
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision.ContainsKey(38));
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(38));
        Assert.False(
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision.ContainsKey(39));
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(39));
        Assert.False(
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision.ContainsKey(40));
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(40));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MirrorOnlyCorrectionDropsStaleSelectionButPreservesPendingScore()
    {
        const string groupId = "mirror.group";
        var state = new WorkoutState
        {
            CatalogRevision = 40,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groupId] = 500,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [groupId] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = groupId,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 500,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(groupId, state.SelectedExerciseIds);
        Assert.DoesNotContain(groupId, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(500, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            new HashSet<int> { 500 },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[41]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(41));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MirrorRelationshipAndMuscleCorrectionsRebuildWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [105, 107, 108, 245, 280, 591, 884, 885, 905];
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        string changedGroup = groups[0].Id;
        string retainedGroup = groups[1].Id;
        var state = new WorkoutState
        {
            CatalogRevision = 41,
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 884,
                [retainedGroup] = 22,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 884,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(22, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(884, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[42]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(42));
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void GenuineMirrorPracticeRevisionRetiresDuplicateButPreservesCorrectedScores()
    {
        int[] changedIds = [90, 94, 95, 99, 100, 497, 498, 500, 511, 514];
        Assert.Equal(
            changedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[44]);
        Assert.Equal(
            new HashSet<int> { 90 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[44]);

        const string correctedGroup = "mirror.corrected";
        var correctedState = new WorkoutState
        {
            CatalogRevision = 43,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [correctedGroup] = 94,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [correctedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = correctedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 94,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(correctedState));

        Assert.DoesNotContain(correctedGroup, correctedState.SelectedExerciseIds);
        Assert.DoesNotContain(correctedGroup, correctedState.Outcomes);
        Assert.Null(correctedState.PendingRestGroupId);
        Assert.Equal(94, correctedState.PendingScoreExerciseId);
        Assert.Equal(-4, correctedState.PendingScoreValue);
        Assert.Equal(
            CatalogMigrationRules.CurrentCatalogRevision,
            correctedState.CatalogRevision);

        var retiredState = new WorkoutState
        {
            CatalogRevision = 43,
            PendingScoreExerciseId = 90,
            PendingScoreValue = -6,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(retiredState));
        Assert.Equal(0, retiredState.PendingScoreExerciseId);
        Assert.Equal(0, retiredState.PendingScoreValue);
    }

    [Fact]
    public void CompleteDirectionRevisionRetiresDuplicatesAndPreservesRelinkedSideLegScore()
    {
        int[] workoutIds =
        [
            264, 275, 406, 409, 460, 588, 608, 611, 617, 620, 743,
            757, 759, 760, 761, 762, 763, 764,
        ];
        int[] scoreIds =
        [
            264, 275, 406, 409, 460, 588, 608, 611, 743,
            757, 759, 760, 761, 762, 763, 764,
        ];
        Assert.Equal(
            workoutIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[45]);
        Assert.Equal(
            scoreIds.ToHashSet(),
            CatalogMigrationRules.ScoreInvalidationsByRevision[45]);

        var changedState = new WorkoutState
        {
            CatalogRevision = 44,
            PendingScoreExerciseId = 409,
            PendingScoreValue = -4,
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(changedState));
        Assert.Equal(0, changedState.PendingScoreExerciseId);
        Assert.Equal(0, changedState.PendingScoreValue);

        var relinkedState = new WorkoutState
        {
            CatalogRevision = 44,
            PendingScoreExerciseId = 617,
            PendingScoreValue = -2,
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(relinkedState));
        Assert.Equal(617, relinkedState.PendingScoreExerciseId);
        Assert.Equal(-2, relinkedState.PendingScoreValue);
    }

    [Fact]
    public void LeadStanceTimingRevisionRebuildsWorkoutWithoutResettingScore()
    {
        int[] leadStanceIds =
        [
            265, 274, 280, 287, 473, 591, 884, 885, 886, 887,
        ];
        Assert.Equal(
            leadStanceIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[46]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(46));

        const string changedGroup = "lead-stance.changed";
        var state = new WorkoutState
        {
            CatalogRevision = 45,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 884,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 884,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(884, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void UnilateralSetupCorrectionRevisionRebuildsWorkoutWithoutResettingScore()
    {
        int[] correctedIds = [198, 398, 421, 427, 468, 512, 515];
        Assert.Equal(
            correctedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[47]);
        Assert.False(
            CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(47));

        const string changedGroup = "unilateral-setup.changed";
        var state = new WorkoutState
        {
            CatalogRevision = 46,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 512,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 512,
            PendingScoreValue = -4,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(512, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void HardFloorCoverageRevisionResetsOnlyChangedExerciseProgressAndScores()
    {
        int[] changedIds =
        [
            439, 442, 444, 478,
            549, 550, 551, 552, 553, 554, 555, 556, 557, 558,
            559, 560, 561, 562, 563, 564, 565, 566, 567, 568,
            569, 570, 571, 574, 575, 578, 581, 582, 583,
        ];
        HashSet<int> expectedIds = changedIds.ToHashSet();
        Assert.Equal(
            expectedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[51]);
        Assert.Equal(
            expectedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[51]);
        Assert.All(changedIds, exerciseId =>
            Assert.Contains(exerciseId, CatalogMigrationRules.ReplacedExerciseIds));

        const string changedGroup = "hard-floor.changed";
        const string retainedGroup = "hard-floor.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 50,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 550,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 550,
            PendingScoreValue = -4,
            LastKeptExerciseIds = [550, 15],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Contains(550, state.LastKeptExerciseIds);
        Assert.Contains(15, state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void HardFloorCoverageAcceptsPublishedVersion68IdentityAndResetsIt()
    {
        const int exerciseId = 478;
        const string version68Name = "Eye-Tracking Rotational Jumps";
        const string currentName = "Step-Out Pivot with Thumb Tracking";
        const string baselineRetiredName = "Dance Head Accent Front";
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(version68Name, video, -7),
        };
        Exercise replacement = Exercise(
            exerciseId,
            currentName,
            video,
            retiredName: baselineRetiredName);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);

        stored[exerciseId] = new(version68Name, "wrong.mp4", -7);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([replacement], stored));
    }

    [Theory]
    [InlineData(
        439,
        "Feet-Together Fixed-Gaze Head Turns",
        "Pogo Bounces with Fixed-Gaze Head Turns",
        "Bidirectional Triangle-Path Saccades")]
    [InlineData(
        442,
        "Feet-Together Fixed-Gaze Head Nods",
        "Pogo Bounces with Fixed-Gaze Head Nods",
        "Near-Point Convergence")]
    [InlineData(
        444,
        "Feet-Together Fixed-Gaze Head Tilts",
        "Pogo Bounces with Fixed-Gaze Head Tilts",
        "Vertical Gaze Stabilization")]
    public void PogoIdentityCorrectionPreservesFeedbackForTheUnchangedDemonstration(
        int exerciseId,
        string previousName,
        string currentName,
        string baselineRetiredName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(previousName, video, -7),
        };
        Exercise replacement = Exercise(
            exerciseId,
            currentName,
            video,
            retiredName: baselineRetiredName);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [replacement],
            stored);

        Assert.Contains(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);

        stored[exerciseId] = new(previousName, "wrong.mp4", -7);
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog([replacement], stored));
    }

    [Theory]
    [InlineData(327, "r30.chest")]
    [InlineData(414, "r30.cranial-muscles")]
    public void SplitFloorSequencesDiscardObsoleteRoundsButPreserveFeedback(int exerciseId, string groupId)
    {
        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
            JsonOptions)!;
        var state = new WorkoutState
        {
            CatalogRevision = 72,
            ActiveWorkoutMinutes = 30,
            ActiveWorkoutModifiers = WorkoutModifiers.None,
            SelectedExerciseIds = new() { [groupId] = exerciseId },
            KeptExerciseRootIdsBySelectionGroupId = new() { [groupId] = [exerciseId] },
            LastKeptExerciseIds = [exerciseId],
            PendingMovementGroupId = $"{groupId}.set1.block3",
            PendingMovementMillisecondsRemaining = 4000,
            PendingMovementPausedByUser = true,
            PendingScoreExerciseId = exerciseId,
            PendingScoreValue = 7,
        };

        CatalogMigrationRules.ReconcileWorkoutState(state, catalog.ToDictionary(exercise => exercise.Id));

        Assert.Null(state.PendingMovementGroupId);
        Assert.Equal(0, state.PendingMovementMillisecondsRemaining);
        Assert.False(state.PendingMovementPausedByUser);
        Assert.Equal(exerciseId, state.PendingScoreExerciseId);
        Assert.Equal(7, state.PendingScoreValue);
        Assert.Contains(exerciseId, state.KeptExerciseRootIdsBySelectionGroupId[groupId]);
        Assert.Contains(exerciseId, state.LastKeptExerciseIds);
    }

    [Fact]
    public void DemonstrationIntegrityRevisionRebuildsChangedWorkoutStateButPreservesScores()
    {
        int[] changedIds =
        [
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
        ];
        Assert.Equal(
            changedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[52]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(52));

        const string changedGroup = "integrity.changed";
        const string retiredGroup = "integrity.retired";
        const string retainedGroup = "integrity.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 51,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 417,
                [retiredGroup] = 267,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retiredGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 417,
            PendingScoreValue = -4,
            LastKeptExerciseIds = [417, 267, 15],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(retiredGroup, state.SelectedExerciseIds);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(417, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Contains(417, state.LastKeptExerciseIds);
        // Revision reconciliation invalidates prepared placements without
        // rewriting historical keeps. The catalog reconciliation that has the
        // bundled inventory removes the retired ID afterward.
        Assert.Contains(267, state.LastKeptExerciseIds);
        Assert.Contains(15, state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void SlipperyHardFloorRevisionRebuildsPlacementsButPreservesFeedback()
    {
        int[] changedIds =
        [
            17, 19, 37, 41, 58, 60, 92, 93, 97, 103, 104, 105,
            107, 108, 109, 112, 116, 117, 120, 121, 122, 123, 124, 125,
            126, 127, 128, 129, 133, 136, 142, 143, 150, 156, 163, 174,
            178, 180, 181, 182, 183, 184, 190, 192, 193, 195, 199, 203,
            231, 232, 245, 278, 279, 280, 282, 303, 311, 314, 315,
            326, 340, 404, 408, 412, 478, 484, 508, 509, 534, 535,
            536, 538, 572, 576, 591, 610, 611, 626, 633, 636, 685, 687,
            733, 746, 748, 750, 816, 884, 885, 886, 887, 905, 915, 971,
            973, 986, 999,
        ];
        Assert.Equal(
            changedIds.ToHashSet(),
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[53]);
        Assert.False(CatalogMigrationRules.ScoreInvalidationsByRevision.ContainsKey(53));

        const string changedGroup = "slippery.changed";
        const string retainedGroup = "slippery.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 52,
            ActiveWorkoutModifiers = WorkoutModifiers.HardFloor,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [$"p{(int)WorkoutModifiers.HardFloor}|{changedGroup}"] = 37,
                [$"p{(int)WorkoutModifiers.HardFloor}|{retainedGroup}"] = 101,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 37,
            PendingScoreValue = -3,
            LastKeptExerciseIds = [37, 101],
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [37] = -3,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(
            $"p{(int)WorkoutModifiers.HardFloor}|{changedGroup}",
            state.SelectedExerciseIds);
        Assert.Equal(
            101,
            state.SelectedExerciseIds[
                $"p{(int)WorkoutModifiers.HardFloor}|{retainedGroup}"]);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(37, state.PendingScoreExerciseId);
        Assert.Equal(-3, state.PendingScoreValue);
        Assert.Equal(
            -3,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][37]);
        Assert.Contains(37, state.LastKeptExerciseIds);
        Assert.Contains(101, state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);

        const string softFloorGroup = "slippery.soft-floor";
        var softFloorState = new WorkoutState
        {
            CatalogRevision = 52,
            ActiveWorkoutModifiers = WorkoutModifiers.None,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [softFloorGroup] = 37,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [softFloorGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = softFloorGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(softFloorState));
        Assert.Equal(37, softFloorState.SelectedExerciseIds[softFloorGroup]);
        Assert.Equal(ExerciseOutcome.Tick, softFloorState.Outcomes[softFloorGroup]);
        Assert.Equal(softFloorGroup, softFloorState.PendingRestGroupId);
        Assert.Equal(123456, softFloorState.PendingRestEndsAtUnixMilliseconds);
        Assert.True(softFloorState.PendingRestKept);
    }

    [Fact]
    public void SoleWallRevisionRebuildsChangedWorkoutStateAndResetsScores()
    {
        HashSet<int> changedIds = [563, 564, 567, 568, 574];
        Assert.Equal(79, CatalogMigrationRules.CurrentCatalogRevision);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[54]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[54]);

        const string changedGroup = "sole-wall.changed";
        const string retainedGroup = "sole-wall.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 53,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 563,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 563,
            PendingScoreValue = -4,
            LastKeptExerciseIds = [563, 15],
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.Contains(563, state.LastKeptExerciseIds);
        Assert.Contains(15, state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void PogoIdentityCorrectionRebuildsPlacementsWithoutResettingFeedback()
    {
        HashSet<int> changedIds = [439, 442, 444];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[63]);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 63);

        const string changedGroup = "pogo-identity.changed";
        const string retainedGroup = "pogo-identity.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 62,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 444,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.X,
            },
            PendingScoreExerciseId = 444,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [444] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(444, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.Equal(-4, phaseScores[444]);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void IndependentVariationRevisionRebuildsCoupledPlacementsWithoutResettingFeedback()
    {
        HashSet<int> changedIds =
        [
            104, 113, 117, 120, 123, 135, 177, 184, 186, 199,
            256, 261, 626, 677, 845, 996, 997,
        ];
        Assert.Equal(79, CatalogMigrationRules.CurrentCatalogRevision);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[64]);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 64);

        const string changedGroup = "independent-variation.changed";
        const string retainedGroup = "independent-variation.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 63,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 177,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.X,
            },
            PendingScoreExerciseId = 177,
            PendingScoreValue = -4,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [changedGroup] = [177],
            },
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [177] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(177, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal([177], state.KeptExerciseRootIdsBySelectionGroupId[changedGroup]);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.Equal(-4, phaseScores[177]);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void KneeCrunchMetadataCorrectionRebuildsPlacementAndResetsFeedback()
    {
        HashSet<int> changedIds = [507];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[65]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[65]);

        const string changedGroup = "knee-crunch.changed";
        const string retainedGroup = "knee-crunch.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 64,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 507,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.X,
            },
            PendingScoreExerciseId = 507,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [507] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.DoesNotContain(507, phaseScores);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void BodybuildingPosingRevisionRebuildsStaticPlacementsAndResetsFeedback()
    {
        HashSet<int> changedIds = [524, 525, 526, 527, 528, 790];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[66]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[66]);

        const string changedGroup = "bodybuilding-posing.changed";
        const string retainedGroup = "bodybuilding-posing.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 65,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 528,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.X,
            },
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [changedGroup] = [528],
            },
            PendingScoreExerciseId = 528,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [528] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal([528], state.KeptExerciseRootIdsBySelectionGroupId[changedGroup]);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.DoesNotContain(528, phaseScores);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MaterialTrainingRevisionRemovesOnlyAnatomicallyInvalidSlotsAndKeeps()
    {
        HashSet<int> addedIds = [911, 913, 916, 917];
        Assert.Equal(79, CatalogMigrationRules.CurrentCatalogRevision);
        Assert.Equal(
            addedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[67]);
        Assert.Equal(
            addedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[67]);
        Assert.Equal(
            new HashSet<int> { 918, 919 },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[69]);
        Assert.Equal(
            new HashSet<int> { 918, 919 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[69]);

        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                JsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        IReadOnlyDictionary<int, Exercise> exercisesById = catalog
            .ToDictionary(exercise => exercise.Id);
        const string invalidAbdominalSlot = "r30.abdominal-wall";
        const string validChestSlot = "r30.chest";
        const string unrelatedLegacySlot = "legacy.unrelated-slot";
        var state = new WorkoutState
        {
            CatalogRevision = 66,
            ActiveWorkoutMinutes = 30,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [invalidAbdominalSlot] = 701,
                [validChestSlot] = 701,
                [unrelatedLegacySlot] = 1,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [$"{invalidAbdominalSlot}.set1.block1"] = ExerciseOutcome.Tick,
                [validChestSlot] = ExerciseOutcome.X,
                [unrelatedLegacySlot] = ExerciseOutcome.Tick,
            },
            PendingMovementGroupId = $"{invalidAbdominalSlot}.set1.block1",
            PendingMovementEndsAtUnixMilliseconds = 123456,
            PendingMovementMillisecondsRemaining = 4000,
            PendingMovementPausedByUser = true,
            PendingRestGroupId = $"{invalidAbdominalSlot}.set1.block1",
            PendingRestEndsAtUnixMilliseconds = 234567,
            PendingRestMillisecondsRemaining = 5000,
            PendingRestPausedByUser = true,
            PendingRestKept = true,
            PendingScoreExerciseId = 701,
            PendingScoreValue = -1,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [invalidAbdominalSlot] = [701, 910],
                [validChestSlot] = [701],
            },
            LastKeptExerciseIds = [701, 910, 962],
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [701] = -3,
                    [910] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(
            state,
            exercisesById));

        Assert.DoesNotContain(invalidAbdominalSlot, state.SelectedExerciseIds);
        Assert.Equal(701, state.SelectedExerciseIds[validChestSlot]);
        Assert.Equal(1, state.SelectedExerciseIds[unrelatedLegacySlot]);
        Assert.DoesNotContain(
            $"{invalidAbdominalSlot}.set1.block1",
            state.Outcomes);
        Assert.Equal(ExerciseOutcome.X, state.Outcomes[validChestSlot]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[unrelatedLegacySlot]);
        Assert.Null(state.PendingMovementGroupId);
        Assert.Null(state.PendingRestGroupId);
        // The integrity audit also removed the old abdominal claim from 910.
        Assert.DoesNotContain(invalidAbdominalSlot, state.KeptExerciseRootIdsBySelectionGroupId);
        Assert.Equal([701], state.KeptExerciseRootIdsBySelectionGroupId[
            validChestSlot]);
        Assert.Equal(
            new HashSet<int> { 701 },
            state.LastKeptExerciseIds);
        Assert.Equal(701, state.PendingScoreExerciseId);
        Assert.Equal(-1, state.PendingScoreValue);
        Assert.Equal(-3, state.ExerciseScoreAdjustmentsByPhase[
            WorkoutExercisePhase.PeakPerformance][701]);
        Assert.Equal(-2, state.ExerciseScoreAdjustmentsByPhase[
            WorkoutExercisePhase.PeakPerformance][910]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision,
            state.CatalogRevision);
    }

    [Fact]
    public void TrainingClaimRevisionRemovesOnlyNewlyInvalidSlotFeedback()
    {
        HashSet<int> addedIds = [918, 919];
        Assert.Equal(79, CatalogMigrationRules.CurrentCatalogRevision);
        Assert.Equal(
            addedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[69]);
        Assert.Equal(
            addedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[69]);

        Exercise[] catalog = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(Path.Combine(
                    AppContext.BaseDirectory,
                    "Assets",
                    "exercises.json")),
                JsonOptions)
            ?? throw new InvalidOperationException("The test catalog is missing.");
        IReadOnlyDictionary<int, Exercise> exercisesById = catalog
            .ToDictionary(exercise => exercise.Id);
        const string invalidPelvicFloorSlot = "r30.pelvic-floor-perineum";
        const string validHipFlexorSlot = "r30.hip-flexors";
        const string invalidRound =
            $"{invalidPelvicFloorSlot}.set1.block1";
        var state = new WorkoutState
        {
            CatalogRevision = 68,
            ActiveWorkoutMinutes = 30,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [invalidPelvicFloorSlot] = 613,
                [validHipFlexorSlot] = 613,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [invalidRound] = ExerciseOutcome.Tick,
                [validHipFlexorSlot] = ExerciseOutcome.X,
            },
            PendingMovementGroupId = invalidRound,
            PendingMovementEndsAtUnixMilliseconds = 123456,
            PendingMovementMillisecondsRemaining = 4000,
            PendingMovementPausedByUser = true,
            PendingScoreExerciseId = 613,
            PendingScoreValue = -1,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [invalidPelvicFloorSlot] = [613],
                [validHipFlexorSlot] = [613],
            },
            LastKeptExerciseIds = [613],
            ExerciseScoreAdjustmentsBySelectionGroupId = new()
            {
                [invalidPelvicFloorSlot] = new() { [613] = -2 },
                [validHipFlexorSlot] = new() { [613] = -1 },
            },
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [613] = -3,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(
            state,
            exercisesById));

        Assert.DoesNotContain(invalidPelvicFloorSlot, state.SelectedExerciseIds);
        Assert.Equal(613, state.SelectedExerciseIds[validHipFlexorSlot]);
        Assert.DoesNotContain(invalidRound, state.Outcomes);
        Assert.Equal(ExerciseOutcome.X, state.Outcomes[validHipFlexorSlot]);
        Assert.Null(state.PendingMovementGroupId);
        Assert.DoesNotContain(
            invalidPelvicFloorSlot,
            state.KeptExerciseRootIdsBySelectionGroupId);
        Assert.Equal(
            [613],
            state.KeptExerciseRootIdsBySelectionGroupId[validHipFlexorSlot]);
        Assert.DoesNotContain(
            invalidPelvicFloorSlot,
            state.ExerciseScoreAdjustmentsBySelectionGroupId);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsBySelectionGroupId[
                validHipFlexorSlot][613]);
        Assert.Equal(-3, state.ExerciseScoreAdjustmentsByPhase[
            WorkoutExercisePhase.PeakPerformance][613]);
        Assert.Equal(613, state.PendingScoreExerciseId);
        Assert.Equal(-1, state.PendingScoreValue);
        Assert.Equal([613], state.LastKeptExerciseIds);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision,
            state.CatalogRevision);
    }

    [Fact]
    public void CompletePoseRevisionRebuildsEveryAffectedPlacementWithoutErasingFeedback()
    {
        int[] changedIds = [307, 490, 491, 492, 495, 499, 501, 520, 528, 561, 958];
        Assert.Equal(changedIds.ToHashSet(), CatalogMigrationRules.WorkoutStateInvalidationsByRevision[72]);
        Assert.DoesNotContain(CatalogMigrationRules.ScoreInvalidationsByRevision, pair => pair.Key == 72);
        foreach (int id in changedIds)
        {
            const string group = "complete-pose.changed";
            var state = new WorkoutState
            {
                CatalogRevision = 71,
                SelectedExerciseIds = new() { [group] = id },
                Outcomes = new() { [group] = ExerciseOutcome.Tick },
                KeptExerciseRootIdsBySelectionGroupId = new() { [group] = [id] },
                ExerciseScoreAdjustmentsBySelectionGroupId = new()
                {
                    [group] = new() { [id] = -1 },
                },
                ExerciseScoreAdjustmentsByPhase = new()
                {
                    [WorkoutExercisePhase.PeakPerformance] = new() { [id] = -3 },
                },
                PendingScoreExerciseId = id,
                PendingScoreValue = -4,
                PendingScoreUpdates = new() { [id] = -4 },
            };
            Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));
            Assert.Empty(state.SelectedExerciseIds);
            Assert.Empty(state.Outcomes);
            // The later integrity revision replaces action 520 entirely.
            if (id == 520)
            {
                Assert.Empty(state.KeptExerciseRootIdsBySelectionGroupId);
                Assert.Empty(state.ExerciseScoreAdjustmentsBySelectionGroupId);
                Assert.Empty(state.ExerciseScoreAdjustmentsByPhase);
                Assert.Empty(state.PendingScoreUpdates);
                Assert.Equal(0, state.PendingScoreExerciseId);
                continue;
            }
            Assert.Equal([id], state.KeptExerciseRootIdsBySelectionGroupId[group]);
            Assert.Equal(-1, state.ExerciseScoreAdjustmentsBySelectionGroupId[group][id]);
            Assert.Equal(-3, state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance][id]);
            Assert.Equal(id, state.PendingScoreExerciseId);
            Assert.Equal(-4, state.PendingScoreValue);
            Assert.Equal(-4, state.PendingScoreUpdates[id]);
            Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
        }
    }

    [Fact]
    public void CorrectedTwoSidedRevisionRebuildsPlacementsAndPreservesFeedback()
    {
        HashSet<int> changedIds = [32, 483, 493];
        Assert.Equal(79, CatalogMigrationRules.CurrentCatalogRevision);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[71]);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 71);

        const string changedGroup = "corrected-sides.changed";
        const string retainedGroup = "corrected-sides.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 70,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 483,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.X,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 483,
            PendingScoreValue = -4,
            PendingScoreUpdates = new Dictionary<int, int>
            {
                [483] = -4,
            },
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [changedGroup] = [483],
            },
            LastKeptExerciseIds = [483],
            ExerciseScoreAdjustmentsBySelectionGroupId = new()
            {
                [changedGroup] = new Dictionary<int, int> { [483] = -1 },
            },
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [483] = -3,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(ExerciseOutcome.X, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.False(state.PendingRestKept);
        Assert.Equal([483], state.KeptExerciseRootIdsBySelectionGroupId[changedGroup]);
        Assert.Contains(483, state.LastKeptExerciseIds);
        Assert.Equal(483, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(-4, state.PendingScoreUpdates[483]);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsBySelectionGroupId[changedGroup][483]);
        Assert.Equal(
            -3,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][483]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void ShyAuditRevisionRebuildsOnlyShyPlacementsAndPreservesFeedback()
    {
        HashSet<int> changedIds =
        [
            56, 59, 98, 108, 176, 185, 188, 190, 202, 203, 204, 205,
            220, 224, 231, 258, 269, 283, 289, 290, 377, 379, 392,
            398, 399, 400, 401, 402, 403, 404, 405, 410, 474, 481,
            498, 543, 557, 608, 609, 678, 685, 687,
        ];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[70]);
        Assert.Equal(
            new HashSet<int> { 202, 204, 205 },
            CatalogMigrationRules.ScoreInvalidationsByRevision[70]);

        const string shyGroup = "shy-audit.changed";
        const string nonShyGroup = "shy-audit.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 69,
            ActiveWorkoutModifiers = WorkoutModifiers.Shy,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [$"p{(int)WorkoutModifiers.Shy}|{shyGroup}"] = 56,
                [$"p{(int)WorkoutModifiers.None}|{nonShyGroup}"] = 56,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [shyGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = shyGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 56,
            PendingScoreValue = -4,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [shyGroup] = [56],
            },
            LastKeptExerciseIds = [56],
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [56] = -3,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(
            $"p{(int)WorkoutModifiers.Shy}|{shyGroup}",
            state.SelectedExerciseIds);
        Assert.Equal(
            56,
            state.SelectedExerciseIds[
                $"p{(int)WorkoutModifiers.None}|{nonShyGroup}"]);
        Assert.DoesNotContain(shyGroup, state.Outcomes);
        Assert.Null(state.PendingRestGroupId);
        Assert.False(state.PendingRestKept);
        Assert.Equal([56], state.KeptExerciseRootIdsBySelectionGroupId[shyGroup]);
        Assert.Contains(56, state.LastKeptExerciseIds);
        Assert.Equal(56, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            -3,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][56]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void ShyAuditRevisionFullyClearsReusedExerciseIdentities()
    {
        const string changedGroup = "shy-audit.reused";
        const string retainedGroup = "shy-audit.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 69,
            ActiveWorkoutModifiers = WorkoutModifiers.None,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 202,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
                [retainedGroup] = ExerciseOutcome.X,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 202,
            PendingScoreValue = -4,
            PendingScoreUpdates = new Dictionary<int, int>
            {
                [202] = -4,
                [15] = -2,
            },
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [changedGroup] = [202, 15],
            },
            LastKeptExerciseIds = [202, 15],
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [202] = -3,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(ExerciseOutcome.X, state.Outcomes[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.False(state.PendingRestKept);
        Assert.Equal([15], state.KeptExerciseRootIdsBySelectionGroupId[changedGroup]);
        Assert.DoesNotContain(202, state.LastKeptExerciseIds);
        Assert.Contains(15, state.LastKeptExerciseIds);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Assert.DoesNotContain(202, state.PendingScoreUpdates);
        Assert.Equal(-2, state.PendingScoreUpdates[15]);
        Assert.DoesNotContain(
            202,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance]);
        Assert.Equal(
            -2,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void BareUpperBodyExpansionRevisionDropsRetiredSlotStateAndScores()
    {
        HashSet<int> changedIds = [790, 993];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[55]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[55]);

        const string changedGroup = "bare-upper-body.changed";
        const string retainedGroup = "bare-upper-body.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 54,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 790,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [790] = -4,
                    [993] = -3,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.DoesNotContain(790, phaseScores);
        Assert.DoesNotContain(993, phaseScores);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void HandShapeReplacementRevisionRebuildsChangedWorkoutStateAndResetsScores()
    {
        HashSet<int> changedIds =
        [
            218, 234, 237, 239, 240, 241, 242, 283, 291, 556,
        ];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[56]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[56]);

        const string changedGroup = "hand-shape.changed";
        const string retainedGroup = "hand-shape.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 55,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 241,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 241,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.Warmup] = new()
                {
                    [241] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup];
        Assert.DoesNotContain(241, phaseScores);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void UppercutDemonstrationRevisionRebuildsOnlyItsWorkoutStateAndScore()
    {
        HashSet<int> changedIds = [287];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[57]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[57]);

        const string changedGroup = "uppercut-demonstration.changed";
        const string retainedGroup = "uppercut-demonstration.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 56,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 287,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 287,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [287] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.DoesNotContain(287, phaseScores);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void UppercutDemonstrationReplacementDiscardsPublishedIdentityAndScore()
    {
        const int exerciseId = 287;
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Exercise replacement = Assert.Single(
            bundled,
            exercise => exercise.Id == exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new("Standing Uppercuts", replacement.Video, -7),
        };

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Fact]
    public void DanceCleanupRevisionRebuildsChangedWorkoutStateAndResetsScores()
    {
        HashSet<int> changedIds =
        [
            218, 234, 237, 239, 241, 283, 291, 294, 556,
        ];
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[58]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[58]);

        const string changedGroup = "dance-cleanup.changed";
        const string retainedGroup = "dance-cleanup.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 57,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 294,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingRestGroupId = changedGroup,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
            PendingScoreExerciseId = 294,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [294] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.DoesNotContain(294, phaseScores);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void MiniSquatCalfRaiseCorrectionRebuildsWorkoutButPreservesScore()
    {
        const int exerciseId = 565;
        const string changedGroup = "mini-squat-calf-raise.changed";
        var state = new WorkoutState
        {
            CatalogRevision = 58,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = exerciseId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
            },
            PendingScoreExerciseId = exerciseId,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [exerciseId] = -4,
                },
            },
        };

        Assert.Equal(
            new HashSet<int> { exerciseId },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[59]);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 59);

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(exerciseId, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            -4,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][exerciseId]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void AlternatingSideTapCorrectionRebuildsWorkoutButPreservesFeedback()
    {
        const int exerciseId = 397;
        const string changedGroup = "alternating-side-tap.changed";
        var state = new WorkoutState
        {
            CatalogRevision = 59,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = exerciseId,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.Tick,
            },
            PendingScoreExerciseId = exerciseId,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [exerciseId] = -4,
                },
            },
        };

        Assert.Equal(
            new HashSet<int> { exerciseId },
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[60]);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 60);

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(exerciseId, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Assert.Equal(
            -4,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][exerciseId]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void DemandCoverageExpansionRebuildsReusedIdsAndResetsFeedback()
    {
        HashSet<int> changedIds = [302, 304, 305, 307, 308, 309, 310];
        Assert.Equal(79, CatalogMigrationRules.CurrentCatalogRevision);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[61]);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.ScoreInvalidationsByRevision[61]);

        const string changedGroup = "demand-coverage.changed";
        const string retainedGroup = "demand-coverage.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 60,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 302,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingScoreExerciseId = 302,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [302] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(0, state.PendingScoreExerciseId);
        Assert.Equal(0, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.DoesNotContain(302, phaseScores);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Fact]
    public void CoverageHierarchyRevisionRebuildsChangedPlacementsWithoutResettingFeedback()
    {
        HashSet<int> changedIds =
        [
            248, 281, 286, 367, 393, 529, 537, 545,
        ];
        Assert.Equal(79, CatalogMigrationRules.CurrentCatalogRevision);
        Assert.Equal(
            changedIds,
            CatalogMigrationRules.WorkoutStateInvalidationsByRevision[62]);
        Assert.DoesNotContain(
            CatalogMigrationRules.ScoreInvalidationsByRevision,
            revision => revision.Key == 62);

        const string changedGroup = "coverage-hierarchy.changed";
        const string retainedGroup = "coverage-hierarchy.retained";
        var state = new WorkoutState
        {
            CatalogRevision = 61,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [changedGroup] = 367,
                [retainedGroup] = 15,
            },
            Outcomes = new Dictionary<string, ExerciseOutcome>
            {
                [changedGroup] = ExerciseOutcome.X,
                [retainedGroup] = ExerciseOutcome.Tick,
            },
            PendingScoreExerciseId = 367,
            PendingScoreValue = -4,
            ExerciseScoreAdjustmentsByPhase = new Dictionary<
                WorkoutExercisePhase,
                Dictionary<int, int>>
            {
                [WorkoutExercisePhase.PeakPerformance] = new()
                {
                    [367] = -4,
                    [15] = -2,
                },
            },
        };

        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state));

        Assert.DoesNotContain(changedGroup, state.SelectedExerciseIds);
        Assert.DoesNotContain(changedGroup, state.Outcomes);
        Assert.Equal(15, state.SelectedExerciseIds[retainedGroup]);
        Assert.Equal(367, state.PendingScoreExerciseId);
        Assert.Equal(-4, state.PendingScoreValue);
        Dictionary<int, int> phaseScores =
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance];
        Assert.Equal(-4, phaseScores[367]);
        Assert.Equal(-2, phaseScores[15]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(302, "Large Bidirectional Arm Circles")]
    [InlineData(304, "Windmill Arms")]
    [InlineData(305, "Cross-Body Arm Swings")]
    [InlineData(307, "Front Arm Raise")]
    [InlineData(308, "Lateral Arm Raise")]
    [InlineData(309, "Scaption Raise")]
    [InlineData(310, "Standing Rear-Delt Sweep")]
    public void DemandCoverageReplacementDiscardsRetiredIdentityAndScore(
        int exerciseId,
        string oldName)
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Exercise replacement = Assert.Single(
            bundled,
            exercise => exercise.Id == exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(oldName, replacement.Video, -7),
        };

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog([replacement], stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Theory]
    [InlineData(218, "Cumbia Two-Step")]
    [InlineData(234, "Merengue Six-Count Step")]
    [InlineData(237, "Salsa Front-and-Back Basic")]
    [InlineData(239, "Reggaeton Single-Single-Double Step")]
    [InlineData(241, "Basic Mambo Step")]
    [InlineData(283, "Cha-Cha Basic Step")]
    [InlineData(291, "Bachata Side-to-Side Basic")]
    [InlineData(294, "Five-Position Tendon Glide")]
    [InlineData(556, "Pony Step")]
    public void DanceAndTendonCleanupDiscardsExactPublishedIdentityAndScore(
        int exerciseId,
        string oldName)
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Exercise replacement = Assert.Single(
            bundled,
            exercise => exercise.Id == exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(oldName, replacement.Video, -7),
        };

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Theory]
    [InlineData(218, "Sequential Finger Waves")]
    [InlineData(234, "Straight Fingers to Knuckle Bend")]
    [InlineData(237, "Sequential Finger Curl Waves")]
    [InlineData(239, "Tabletop Tendon Glide")]
    [InlineData(240, "Hook Fingers to Full Fist")]
    [InlineData(241, "Open Hand to Hook Fist")]
    [InlineData(242, "Open Hand to Full Fist")]
    [InlineData(283, "Open Hand to Straight Fist")]
    [InlineData(291, "Open Hand to Claw Fist")]
    [InlineData(556, "Standing Fist Clench and Release")]
    public void HandShapeReplacementsDiscardExactPublishedIdentityAndScore(
        int exerciseId,
        string oldName)
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Exercise replacement = Assert.Single(
            bundled,
            exercise => exercise.Id == exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(oldName, replacement.Video, -7),
        };

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Theory]
    [InlineData(202, "Hook Fist", "exercise_videos/exercise_0202.mp4")]
    [InlineData(202, "Hook Fist", "exercise_gifs/exercise_0202.gif")]
    [InlineData(202, "Finger Fan and Close — Four-Count Tempo", "exercise_gifs/exercise_0202.gif")]
    [InlineData(204, "Full Fist", "exercise_videos/exercise_0204.mp4")]
    [InlineData(204, "Full Fist", "exercise_gifs/exercise_0204.gif")]
    [InlineData(204, "Finger Fan and Close — Half Range", "exercise_gifs/exercise_0204.gif")]
    [InlineData(205, "Tabletop Fist", "exercise_videos/exercise_0205.mp4")]
    [InlineData(205, "Tabletop Fist", "exercise_gifs/exercise_0205.gif")]
    [InlineData(205, "Finger Fan and Close — Full Range", "exercise_gifs/exercise_0205.gif")]
    public void ShyAuditReplacementsDiscardEveryPublishedIdentityAndScore(
        int exerciseId,
        string oldName,
        string oldVideo)
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Exercise replacement = Assert.Single(
            bundled,
            exercise => exercise.Id == exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(oldName, oldVideo, -7),
        };

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored);

        Assert.Contains(exerciseId, CatalogMigrationRules.ReplacedExerciseIds);
        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Theory]
    [InlineData(202)]
    [InlineData(204)]
    [InlineData(205)]
    public void ShyAuditReplacementRejectsUnknownHistoricalIdentity(int exerciseId)
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Exercise replacement = Assert.Single(
            bundled,
            exercise => exercise.Id == exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(
                "Unverified historical movement",
                $"exercise_gifs/exercise_{exerciseId:D4}.gif",
                -7),
        };

        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored));
    }

    [Theory]
    [InlineData(563, "Single-Leg Calf Raise with Head Turns")]
    [InlineData(564, "Parallel Calf Raises with Hands on Hips")]
    [InlineData(567, "Breathing Calf Raises with Arm Folds")]
    [InlineData(568, "Chest-Expansion Breathing Calf Raises")]
    [InlineData(574, "Tiptoe Overhead Side Bends")]
    public void SoleWallReplacementsDiscardExactPublishedIdentityAndScore(
        int exerciseId,
        string oldName)
    {
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Exercise replacement = Assert.Single(
            bundled,
            exercise => exercise.Id == exerciseId);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(oldName, replacement.Video, -7),
        };

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Fact]
    public void PermanentlyRetiredExercisesMayBeRemovedButCannotReturn()
    {
        Assert.Equal(
            new HashSet<int>
            {
                90, 229, 267, 412, 553, 558, 559, 757, 759, 760, 761, 762, 763, 764,
            },
            CatalogMigrationRules.PermanentlyRetiredExerciseIds);
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [412] = new(
                "Lateral Lunge with Sideward Thumb Tracking",
                "exercise_videos/exercise_0412.mp4",
                -8),
            [90] = new(
                "Mirror-Guided Bodyweight Squat",
                "exercise_videos/exercise_0090.mp4",
                -6),
            [229] = new(
                "Alternating Boxing Jabs",
                "exercise_videos/exercise_0229.mp4",
                -4),
            [22] = new(
                "Retained movement",
                "exercise_videos/exercise_0022.mp4",
                -2),
        };
        Exercise retained = Exercise(
            22,
            "Retained movement",
            "exercise_videos/exercise_0022.mp4");

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog([retained], stored);

        Assert.Equal(new HashSet<int> { 22 }, preserved);
        Exercise restoredRetired = Exercise(
            229,
            "Invalid restoration",
            "exercise_videos/exercise_0229.mp4",
            retiredName: "Alternating Boxing Uppercut");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [retained, restoredRetired],
                stored));
        Exercise restoredSquat = Exercise(
            90,
            "Invalid squat restoration",
            "exercise_videos/exercise_0090.mp4",
            retiredName: "Alternating Step Pivot");
        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [retained, restoredSquat],
                stored));
    }

    [Theory]
    [InlineData("Forehead Finger Sweep", "exercise_videos/exercise_0497.mp4")]
    [InlineData("Odissi Sundari Griva", "exercise_direction_videos/exercise_0497.mp4")]
    [InlineData("Track Finger in Circles", "exercise_direction_videos/exercise_0497.mp4")]
    public void RestoredIdDiscardsExactHistoricalIdentityAndScore(
        string oldName,
        string oldVideo)
    {
        const int restoredId = 497;
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [restoredId] = new(oldName, oldVideo, -7),
        };
        Exercise replacement = Exercise(
            restoredId,
            "Eyebrow Raise",
            "exercise_videos/exercise_0497.mp4",
            retiredName: "Odissi Sundari Griva");

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored);

        Assert.DoesNotContain(restoredId, preserved);
        Assert.Equal(-7, stored[restoredId].Score);
        Assert.Equal(0, replacement.Score);
    }

    [Fact]
    public void RestoredIdRejectsUnknownHistoricalIdentity()
    {
        const int restoredId = 497;
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [restoredId] = new(
                "Unverified old movement",
                "exercise_direction_videos/exercise_0497.mp4",
                -7),
        };
        Exercise replacement = Exercise(
            restoredId,
            "Eyebrow Raise",
            "exercise_videos/exercise_0497.mp4",
            retiredName: "Odissi Sundari Griva");

        Assert.Throws<InvalidOperationException>(() =>
            CatalogMigrationRules.ValidatePreservedCatalog(
                [replacement],
                stored));
    }

    [Theory]
    [InlineData(21, "Alternating Standing-Scale Balance", "Alternating Single-Leg Hinge with Forward Reach")]
    [InlineData(145, "Alternating Standing Knee Extension", "Wall-Supported Standing Knee-Extension Hold")]
    [InlineData(394, "Standing Open-and-Close Breathing", "Alternating Cross-Body Knee with Arm Sweep")]
    [InlineData(395, "Standing Overhead Rib-Expansion Breathing", "Alternating Knee Lift and Overhead Reach")]
    [InlineData(398, "Standing Arm-Expansion Breathing", "Inhale Arms Open, Exhale Arms Together")]
    [InlineData(399, "Shibashi Opening-the-Chest Breathing", "Inhale Chest Open, Exhale Arms Close with Shallow Squat")]
    [InlineData(400, "Shibashi Separating-the-Clouds Breathing", "Inhale Rise and Lift Arms, Exhale Squat and Sweep Down")]
    [InlineData(401, "Shibashi Alternating Swinging-Arms Breathing", "Alternating Inhale-Twist, Exhale-Push")]
    public void MigrationAllowsEarlierNameAcrossSecondClarityCorrection(
        int exerciseId,
        string earlierName,
        string correctedName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(earlierName, video, -5),
        };
        Exercise corrected = LoadBundledExercise(exerciseId);
        Assert.Equal(correctedName, corrected.Name);

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            [corrected],
            stored);

        // These historical aliases belonged to movements subsequently replaced.
        int[] discardedIds = [241, 242, 256, 257, 258, 262, 270, 283, 290, 291, 394, 395, 425];
        Assert.Equal(!discardedIds.Contains(exerciseId), preserved.Contains(exerciseId));
        Assert.Equal(-5, stored[exerciseId].Score);
    }

    [Theory]
    [InlineData(234, "Palms-Up Fist Wrist Flexion and Extension", "Karate Flat-Fist Strike (Hiraken)")]
    [InlineData(234, "Alternating Thumb-to-Palm Tucks", "Karate Flat-Fist Strike (Hiraken)")]
    [InlineData(239, "Ninja Snake Hand-Seal Hold", "Ninja Fireball Hand-Seal Sequence")]
    [InlineData(240, "Ninja Ram Hand-Seal Hold", "Ninja Shadow-Possession Hand-Seal Sequence")]
    [InlineData(241, "Self-Resisted Thumb C Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(241, "Straight-Hand Knuckle-Bend Flow", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(211, "Opposite-Hand-Resisted Wrist Extension Hold", "Karate Backfist Strike (Uraken-Uchi)")]
    [InlineData(211, "Assisted Wrist Flexion-Extension Glides", "Karate Backfist Strike (Uraken-Uchi)")]
    [InlineData(213, "Opposite-Hand-Resisted Wrist Flexion Hold", "Karate Hammer-Fist Strike (Tetsui-Uchi)")]
    [InlineData(213, "Assisted Side-to-Side Wrist Glides", "Karate Hammer-Fist Strike (Tetsui-Uchi)")]
    [InlineData(214, "Opposite-Hand-Resisted Wrist Ulnar-Deviation Hold", "Wing Chun Biu-Sau Palm Strike")]
    [InlineData(215, "Opposite-Hand-Resisted Wrist Radial-Deviation Hold", "Self-Resisted Wrist Radial-Deviation Pulses")]
    [InlineData(218, "Opposite-Hand-Resisted Little-Finger Abduction Hold", "Self-Resisted Wrist-Flexion Pulses")]
    [InlineData(236, "Opposite-Hand-Resisted Thumb Extension Hold", "Karate Spear-Hand Strike (Nukite)")]
    [InlineData(241, "Opposite-Hand-Resisted Thumb Adduction Hold", "Ninja Water-Dragon 44 Hand-Seal Sequence")]
    [InlineData(242, "Five-Fingertip Press Isometric", "Ninja Shadow-Clone Hand-Seal Sequence")]
    [InlineData(283, "Opposite-Hand-Resisted Thumb Abduction Hold", "Qigong Fist Rotation")]
    [InlineData(289, "Opposite-Hand-Resisted Thumb Flexion Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(289, "Self-Resisted Thumb Adduction Hold", "Heaven-to-Earth Finger Rotation")]
    [InlineData(289, "Alternating Thumb-to-Palm Tucks", "Heaven-to-Earth Finger Rotation")]
    [InlineData(289, "Thumb-to-Fingertip Opposition", "Heaven-to-Earth Finger Rotation")]
    [InlineData(291, "Self-Resisted Thumb Abduction Hold", "Black Dragon Enters the Cave")]
    [InlineData(293, "Self-Resisted Thumb Flexion Hold", "Sword-Fingers Qigong Sequence")]
    [InlineData(294, "Self-Resisted Little-Finger Abduction Hold", "Tiger-Claw Grip Flow")]
    [InlineData(483, "Clockwise-First Full Neck Circles", "Pirouette Spotting Drill")]
    [InlineData(501, "Counterclockwise-First Full Neck Circles", "Standing Horizontal Saccades")]
    [InlineData(501, "Single-Leg Thumb-Focus Head Turns", "Standing Horizontal Saccades")]
    [InlineData(504, "Hands-Behind-Head Splenius-Capitis Stretch", "Vertical Eye-Head Shifts Between Thumbs")]
    [InlineData(507, "Single-Side Knee Raise with Elbow Pull", "Firm Eye Close and Full Open")]
    [InlineData(513, "Single-Leg Thumb-Focus Head Nods", "Scapular Retraction")]
    [InlineData(843, "Standing Scalene Wrist-Anchor Stretch", "Standing Cobra Pose")]
    [InlineData(572, "Cossack Side-to-Side Shifts", "Tai Chi White Crane Opens Wings")]
    public void LatestReplacementAcceptsAdditionalReviewedPriorIdentity(
        int exerciseId,
        string priorName,
        string baselineRetiredName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        Exercise replacement = Exercise(
            exerciseId,
            "Latest replacement",
            video,
            retiredName: baselineRetiredName);

        foreach (string storedName in new[]
        {
            priorName,
            $"Alternating {priorName}",
        })
        {
            var stored = new Dictionary<int, StoredExerciseSnapshot>
            {
                [exerciseId] = new(storedName, video, -6),
            };

            IReadOnlySet<int> preserved =
                CatalogMigrationRules.ValidatePreservedCatalog(
                    [replacement],
                    stored);

            Assert.DoesNotContain(exerciseId, preserved);
        }
    }

    [Theory]
    [InlineData(214, "Wrist Circles", "Inward Wrist Circles")]
    [InlineData(223, "Controlled Wrist Circles", "Inward Controlled Wrist Circles")]
    [InlineData(264, "Standing Arm Circles", "Backward Standing Arm Circles")]
    [InlineData(288, "Knee-and-Ankle Circles", "Forward Knee-and-Ankle Circles")]
    [InlineData(406, "Standing Wheel Arm Circles", "Clockwise Standing Wheel Arm Circles")]
    [InlineData(409, "Full Neck Circles", "Clockwise Full Neck Circles")]
    [InlineData(588, "Belly-Dance Alternating Shoulder Rolls", "Backward Belly-Dance Alternating Shoulder Rolls")]
    [InlineData(608, "Hip Circle", "Counterclockwise Hip Circles")]
    [InlineData(611, "Wide-Stance Hip Circles", "Counterclockwise Wide-Stance Hip Circles")]
    [InlineData(743, "Standing Large Arm Circles", "Backward Standing Large Arm Circles")]
    public void DirectionSplitAcceptsPreviouslyDeployedIdentityAndResetsScore(
        int exerciseId,
        string previousName,
        string currentName)
    {
        string video = $"exercise_videos/exercise_{exerciseId:D4}.mp4";
        var stored = new Dictionary<int, StoredExerciseSnapshot>
        {
            [exerciseId] = new(previousName, video, -7),
        };
        Exercise splitDirection = Exercise(
            exerciseId,
            currentName,
            video,
            retiredName: "Historical reviewed replacement");

        IReadOnlySet<int> preserved =
            CatalogMigrationRules.ValidatePreservedCatalog(
                [splitDirection],
                stored);

        Assert.DoesNotContain(exerciseId, preserved);
        Assert.Equal(-7, stored[exerciseId].Score);
    }

    [Fact]
    public void MixedHistoricalInventoryReconcilesIntoBundledCatalog()
    {
        int[] versionSixteenIds = [400, 401, 402, 403, 404, 405, 406];
        string catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "exercises.json");
        Exercise[] bundled = JsonSerializer.Deserialize<Exercise[]>(
                File.ReadAllText(catalogPath),
                JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is empty.");
        Assert.All(versionSixteenIds, id =>
            Assert.Contains(bundled, exercise => exercise.Id == id));

        Dictionary<int, StoredExerciseSnapshot> versionFifteen = bundled
            .Where(exercise => !versionSixteenIds.Contains(exercise.Id))
            .ToDictionary(
                exercise => exercise.Id,
                exercise => new StoredExerciseSnapshot(
                    exercise.RetiredName ?? exercise.Name,
                    exercise.Video,
                    -exercise.Id));

        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            bundled,
            versionFifteen);

        HashSet<int> expectedPreserved = versionFifteen.Keys
            .Except(CatalogMigrationRules.ReplacedExerciseIds)
            // Reviewed travel wording (231) and breathing cues (398) preserve identity.
            .Union([231, 398])
            .ToHashSet();
        Assert.True(expectedPreserved.SetEquals(preserved),
            $"Unexpected preservation: {string.Join(", ", preserved.Except(expectedPreserved))}; " +
            $"unexpected reset: {string.Join(", ", expectedPreserved.Except(preserved))}.");
        Assert.All(versionFifteen, entry =>
            Assert.Equal(-entry.Key, entry.Value.Score));
        Assert.DoesNotContain(versionSixteenIds, preserved.Contains);
    }

    private static Exercise LoadBundledExercise(int exerciseId) =>
        (JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), JsonOptions)
            ?? throw new InvalidOperationException("The bundled catalog is missing."))
        .Single(exercise => exercise.Id == exerciseId);

    private static Exercise Exercise(
        int id,
        string name,
        string video,
        int score = 0,
        ExerciseSideSequence sideSequence = ExerciseSideSequence.Continuous,
        string? retiredName = null)
    {
        return new Exercise
        {
            Id = id,
            Name = name,
            RetiredName = retiredName,
            Video = video,
            PrimaryCanonicalGroup =
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            SecondaryCanonicalGroups = [],
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = sideSequence,
            UpperBodyClothingRequirement =
                ExerciseUpperBodyClothingRequirement.Agnostic,
            ShyCompatibility = ExerciseShyCompatibility.Compatible,
            Score = score,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }
}
