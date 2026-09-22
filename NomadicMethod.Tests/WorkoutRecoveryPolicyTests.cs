using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutRecoveryPolicyTests
{
    private static readonly long Now =
        new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();

    [Fact]
    public void RecoveryWindowsUseRollingBoundariesAtPrimaryMuscleLevel()
    {
        const string muscle = nameof(CanonicalMuscleGroup.GlutealExtensors);

        Assert.True(WorkoutRecoveryPolicy.IsPrimaryMuscleRecovering(
            new Dictionary<string, long>
            {
                [muscle] = Now -
                    WorkoutRecoveryPolicy.HardRecoveryWindowMilliseconds + 1,
            },
            CanonicalMuscleGroup.GlutealExtensors,
            Now));
        Assert.False(WorkoutRecoveryPolicy.IsPrimaryMuscleRecovering(
            new Dictionary<string, long>
            {
                [muscle] = Now -
                    WorkoutRecoveryPolicy.HardRecoveryWindowMilliseconds,
            },
            CanonicalMuscleGroup.GlutealExtensors,
            Now));
        Assert.False(WorkoutRecoveryPolicy.IsPrimaryMuscleRecovering(
            new Dictionary<string, long>(),
            CanonicalMuscleGroup.GlutealExtensors,
            Now));

        Assert.True(WorkoutRecoveryPolicy.IsPrimaryMuscleInModerateRecovery(
            new Dictionary<string, long>
            {
                [muscle] = Now -
                    WorkoutRecoveryPolicy.ModerateRecoveryWindowMilliseconds + 1,
            },
            CanonicalMuscleGroup.GlutealExtensors,
            Now));
        Assert.False(WorkoutRecoveryPolicy.IsPrimaryMuscleInModerateRecovery(
            new Dictionary<string, long>
            {
                [muscle] = Now -
                    WorkoutRecoveryPolicy.ModerateRecoveryWindowMilliseconds,
            },
            CanonicalMuscleGroup.GlutealExtensors,
            Now));
    }

    [Fact]
    public void RotationTierRewardsOnlyFreshHardPrimaryWorkAndPenalizesRecovery()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(3).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.First();
        CanonicalMuscleGroup outsidePrimary =
            MassGroupingTaxonomy.GetResolution(3).Groups[1].CanonicalGroups.First();
        Exercise freshHard = Exercise(1, primary, muscularDemand: 2);
        Exercise recoveringHard = Exercise(2, primary, muscularDemand: 2);
        Exercise moderate = Exercise(3, primary, muscularDemand: 1);
        Exercise hardOwnedByAnotherGroup = Exercise(
            4,
            outsidePrimary,
            muscularDemand: 2);
        var recovery = new Dictionary<string, long>
        {
            [primary.ToString()] = Now - (long)TimeSpan.FromHours(4).TotalMilliseconds,
        };

        Assert.Equal(
            HardExerciseRotationStatus.FreshHard,
            WorkoutRecoveryPolicy.GetRotationStatus(
                freshHard,
                group,
                new Dictionary<string, long>(),
                Now));
        Assert.Equal(
            HardExerciseRotationStatus.RecoveringHard,
            WorkoutRecoveryPolicy.GetRotationStatus(
                recoveringHard,
                group,
                recovery,
                Now));
        Assert.Equal(
            HardExerciseRotationStatus.Neutral,
            WorkoutRecoveryPolicy.GetRotationStatus(
                moderate,
                group,
                recovery,
                Now));
        Assert.True(WorkoutRecoveryPolicy.IsModerateExerciseRecovering(
            moderate,
            recovery,
            Now));
        Assert.False(WorkoutRecoveryPolicy.IsModerateExerciseRecovering(
            freshHard,
            recovery,
            Now));
        Assert.Equal(
            HardExerciseRotationStatus.Neutral,
            WorkoutRecoveryPolicy.GetRotationStatus(
                hardOwnedByAnotherGroup,
                group,
                new Dictionary<string, long>(),
                Now));
    }

    [Fact]
    public void CompletedMuscularWorkRecordsOnlyItsApplicableRecoveryHistories()
    {
        Exercise hard = Exercise(
            1,
            CanonicalMuscleGroup.GlutealExtensors,
            muscularDemand: 2);
        Exercise moderate = Exercise(
            2,
            CanonicalMuscleGroup.Chest,
            muscularDemand: 1);
        Exercise easy = Exercise(
            3,
            CanonicalMuscleGroup.AnteriorLateralNeckAndHyoidMuscles,
            muscularDemand: 0);
        var hardHistory = new Dictionary<string, long>
        {
            [hard.PrimaryCanonicalGroup.ToString()] = Now,
        };
        var meaningfulHistory = new Dictionary<string, long>();

        WorkoutRecoveryPolicy.RecordCompletedMuscularWork(
            meaningfulHistory,
            hardHistory,
            hard,
            Now - 1);
        WorkoutRecoveryPolicy.RecordCompletedMuscularWork(
            meaningfulHistory,
            hardHistory,
            moderate,
            Now + 1);
        WorkoutRecoveryPolicy.RecordCompletedMuscularWork(
            meaningfulHistory,
            hardHistory,
            easy,
            Now + 2);

        Assert.Equal(Now, hardHistory[hard.PrimaryCanonicalGroup.ToString()]);
        Assert.DoesNotContain(moderate.PrimaryCanonicalGroup.ToString(), hardHistory.Keys);
        Assert.Equal(
            Now - 1,
            meaningfulHistory[hard.PrimaryCanonicalGroup.ToString()]);
        Assert.Equal(
            Now + 1,
            meaningfulHistory[moderate.PrimaryCanonicalGroup.ToString()]);
        Assert.DoesNotContain(easy.PrimaryCanonicalGroup.ToString(), meaningfulHistory.Keys);
    }

    [Fact]
    public void RecoveryLightRequiresFourFifthsOfSelectableDemandMuscles()
    {
        CanonicalMuscleGroup[] muscles =
        [
            CanonicalMuscleGroup.GlutealExtensors,
            CanonicalMuscleGroup.Chest,
            CanonicalMuscleGroup.ElbowFlexors,
            CanonicalMuscleGroup.Soleus,
            CanonicalMuscleGroup.AbdominalWall,
        ];
        Exercise[] exercises = muscles
            .Select((muscle, index) => Exercise(
                index + 1,
                muscle,
                muscularDemand: NomadicMethod.Models.Exercise.ModerateMuscularDemand))
            .ToArray();
        var meaningfulHistory = muscles
            .Take(4)
            .ToDictionary(
                muscle => muscle.ToString(),
                _ => Now - (long)TimeSpan.FromHours(1).TotalMilliseconds);

        WorkoutRecoveryLightStatus active = WorkoutRecoveryLightPolicy.Evaluate(
            exercises,
            meaningfulHistory,
            new Dictionary<string, long>(),
            Now);
        WorkoutRecoveryLightStatus inactive = WorkoutRecoveryLightPolicy.Evaluate(
            exercises,
            meaningfulHistory
                .Where(entry => entry.Key != muscles[3].ToString())
                .ToDictionary(),
            new Dictionary<string, long>(),
            Now);

        Assert.True(active.IsActive);
        Assert.Equal(4, active.RecoveringMuscleCount);
        Assert.Equal(5, active.EligibleMuscleCount);
        Assert.False(inactive.IsActive);
        Assert.Equal(3, inactive.RecoveringMuscleCount);
    }

    [Fact]
    public void RecoveryLightRequiresEveryAvailableDemandPathToRecover()
    {
        CanonicalMuscleGroup muscle = CanonicalMuscleGroup.GlutealExtensors;
        Exercise[] exercises =
        [
            Exercise(1, muscle, NomadicMethod.Models.Exercise.ModerateMuscularDemand),
            Exercise(2, muscle, NomadicMethod.Models.Exercise.MaximumMuscularDemand),
        ];
        var meaningfulHistory = new Dictionary<string, long>
        {
            [muscle.ToString()] = Now -
                (long)TimeSpan.FromHours(1).TotalMilliseconds,
        };

        WorkoutRecoveryLightStatus freshHardPath =
            WorkoutRecoveryLightPolicy.Evaluate(
                exercises,
                meaningfulHistory,
                new Dictionary<string, long>(),
                Now);
        WorkoutRecoveryLightStatus allPathsRecovering =
            WorkoutRecoveryLightPolicy.Evaluate(
                exercises,
                meaningfulHistory,
                new Dictionary<string, long>
                {
                    [muscle.ToString()] = Now -
                        (long)TimeSpan.FromHours(20).TotalMilliseconds,
                },
                Now);

        Assert.False(freshHardPath.IsActive);
        Assert.True(allPathsRecovering.IsActive);
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int muscularDemand) =>
        new()
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_videos/exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primary,
            SecondaryCanonicalGroups = [],
            Practice = "Test",
            MotionProfile = "Test",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
            InsectCompatibility = ExerciseInsectCompatibility.Compatible,
            ShyCompatibility = ExerciseShyCompatibility.Compatible,
            MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
            MuscularDemand = muscularDemand,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 2,
            Equipment = "None",
            Silent = true,
        };
}
