using System.Text.Json;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutLightDayPolicyTests
{
    private static readonly DateTimeOffset DayOne =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(3, 60)]
    [InlineData(5, 36)]
    [InlineData(7, 26)]
    [InlineData(10, 18)]
    [InlineData(15, 12)]
    [InlineData(20, 9)]
    [InlineData(30, 6)]
    [InlineData(45, 4)]
    [InlineData(60, 3)]
    [InlineData(90, 3)]
    public void DurationScalesCompletedWorkRatherThanCalendarDayCount(
        int duration, int regularDays)
    {
        var history = new List<WorkoutSessionLog>();
        Assert.Equal(regularDays, Remaining(history, duration, DayOne));
        for (int day = 0; day < regularDays; day++)
        {
            DateTimeOffset now = DayOne.AddDays(day);
            Assert.False(Due(history, now));
            history.Add(Session(now, duration));
        }
        Assert.True(Due(history, DayOne.AddDays(regularDays)));
    }

    [Fact]
    public void TenThreeMinuteWorkoutsDailyMakeTheSeventhDayLightAfterReload()
    {
        var history = new List<WorkoutSessionLog>();
        for (int day = 0; day < 6; day++)
        {
            Assert.False(Due(history, DayOne.AddDays(day)));
            for (int workout = 0; workout < 10; workout++)
            {
                history.Add(Session(DayOne.AddDays(day).AddMinutes(workout * 10), 3));
            }
        }
        var state = new WorkoutState
        {
            WorkoutHistory = history,
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.Warmup] = new() { [507] = -2 },
            },
            LastKeptExerciseIds = [507],
        };
        string json = JsonSerializer.Serialize(state);
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>(json)!;
        Assert.Equal(60, restored.WorkoutHistory.Count);
        Assert.True(Due(restored.WorkoutHistory, DayOne.AddDays(6)));
        Assert.Equal(-2, restored.ExerciseScoreAdjustmentsByPhase[
            WorkoutExercisePhase.Warmup][507]);
        Assert.Contains(507, restored.LastKeptExerciseIds);
    }

    [Fact]
    public void DailyCapIsAppliedAfterAggregatingSessions()
    {
        WorkoutSessionLog[] history = Enumerable.Range(0, 10)
            .Select(index => Session(DayOne.AddMinutes(index * 10), 10)).ToArray();
        Assert.Equal(2, Remaining(history, 60, DayOne.AddHours(3)));
        Assert.Equal(40, Remaining(history, 3, DayOne.AddHours(3)));
    }

    [Fact]
    public void InterruptedWorkCountsCompletedBlocksAndAllSkippedWorkCountsZero()
    {
        WorkoutSessionLog partial = Session(DayOne, 10);
        partial.WorkoutMinutes = 90;
        partial.Status = WorkoutSessionStatus.Interrupted;
        WorkoutSessionLog skipped = Session(DayOne.AddDays(1), 0);
        skipped.WorkoutMinutes = 90;
        skipped.InitialSelections = [new WorkoutSelectionSnapshot()];
        skipped.Decisions = [new WorkoutDecisionLog()];
        Assert.Equal(17, Remaining([partial, skipped], 10, DayOne.AddDays(1).AddHours(2)));
        Assert.Equal(18, Remaining([skipped], 10, DayOne.AddDays(1).AddHours(2)));
    }

    [Fact]
    public void DueLightRemainsLockedAfterTokenCompletionUntilNextLocalDay()
    {
        List<WorkoutSessionLog> history = Enumerable.Range(0, 3)
            .Select(day => Session(DayOne.AddDays(day), 60)).ToList();
        DateTimeOffset dueDay = DayOne.AddDays(3);
        Assert.True(Due(history, dueDay));
        history.Add(Session(dueDay, 3, light: true));
        Assert.True(Due(history, dueDay.AddHours(1)));
        Assert.True(Due(history, dueDay.Date.AddHours(23).AddMinutes(59)));
        Assert.False(Due(history, dueDay.AddDays(1)));
        Assert.Equal(6, Remaining(history, 30, dueDay.AddDays(1)));
    }

    [Theory]
    [InlineData(3, 2, 2)]
    [InlineData(5, 4, 4)]
    [InlineData(7, 6, 7)]
    [InlineData(10, 8, 8)]
    [InlineData(10, 9, 10)]
    [InlineData(20, 16, 16)]
    [InlineData(20, 17, 20)]
    [InlineData(60, 50, 50)]
    [InlineData(60, 51, 60)]
    [InlineData(60, 53, 60)]
    [InlineData(60, 55, 60)]
    [InlineData(60, 56, 60)]
    [InlineData(90, 59, 59)]
    [InlineData(90, 77, 60)]
    public void NearlyCompletedRegularWorkoutGetsFullCadenceCreditOnlyAboveThreshold(
        int plannedMinutes, int completedBlocks, int creditedMinutes)
    {
        WorkoutSessionLog session = Session(DayOne, completedBlocks);
        session.WorkoutMinutes = plannedMinutes;
        Assert.Equal(180 - creditedMinutes,
            Remaining([session], 1, DayOne.AddHours(2)));
    }

    [Fact]
    public void ThreeNearlyCompletedHoursAreDueAfterReloadWithoutRewritingHistory()
    {
        var state = new WorkoutState
        {
            WorkoutHistory = new[] { 53, 56, 55 }.Select((count, day) =>
            {
                WorkoutSessionLog session = Session(DayOne.AddDays(day), count);
                session.WorkoutMinutes = 60;
                return session;
            }).ToList(),
            LastKeptExerciseIds = [507],
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.Warmup] = new() { [507] = -2 },
            },
            LastHardWorkUnixMillisecondsByPrimaryMuscle = new()
            {
                [CanonicalMuscleGroup.AbdominalWall.ToString()] =
                    DayOne.ToUnixTimeMilliseconds(),
            },
        };
        string saved = JsonSerializer.Serialize(state);
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>(saved)!;
        DateTimeOffset now = DayOne.AddDays(2).AddHours(2);
        Assert.True(Due(restored.WorkoutHistory, now));
        Assert.Equal(0, Remaining(restored.WorkoutHistory, 60, now));
        Assert.Equal(WorkoutModifiers.Silence | WorkoutModifiers.Light,
            WorkoutLightDayPolicy.GetDefaultWorkoutModifiers(
                WorkoutModifiers.Silence, restored.WorkoutHistory,
                now.ToUnixTimeMilliseconds(), TimeZoneInfo.Utc));
        Assert.Equal(saved, JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData(WorkoutSessionStatus.Interrupted)]
    [InlineData(WorkoutSessionStatus.InProgress)]
    public void UnfinishedNearlyCompleteWorkoutStillReceivesOnlyActualWork(
        WorkoutSessionStatus status)
    {
        WorkoutSessionLog session = Session(DayOne, 55);
        session.WorkoutMinutes = 60;
        session.Status = status;
        Assert.Equal(125, Remaining([session], 1, DayOne.AddHours(2)));
    }

    [Fact]
    public void MixedLightSessionDoesNotRoundUpButPhysicalModifierChangesCan()
    {
        WorkoutSessionLog session = Session(DayOne, 55);
        session.WorkoutMinutes = 60;
        session.ModifierChanges =
        [
            new() { ChangedAtUnixMilliseconds = DayOne.AddMinutes(5).ToUnixTimeMilliseconds(),
                NewModifiers = WorkoutModifiers.Light },
            new() { ChangedAtUnixMilliseconds = DayOne.AddMinutes(6).ToUnixTimeMilliseconds(),
                NewModifiers = WorkoutModifiers.Wall },
        ];
        // Only the fifth block is Light; the other 54 are not rounded to 60.
        Assert.Equal(126, Remaining([session], 1, DayOne.AddHours(2)));
        session.ModifierChanges[0].NewModifiers = WorkoutModifiers.Wall;
        Assert.Equal(120, Remaining([session], 1, DayOne.AddHours(2)));
        session.IsLightDay = true;
        session.ModifierChanges[0].ChangedAtUnixMilliseconds =
            DayOne.ToUnixTimeMilliseconds();
        Assert.Equal(125, Remaining([session], 1, DayOne.AddHours(2)));
    }

    [Fact]
    public void CompletionCreditKeepsDailyCapAndWaitsForTheRecordedEnd()
    {
        WorkoutSessionLog session = Session(DayOne, 55);
        session.WorkoutMinutes = 60;
        session.EndedAtUnixMilliseconds = DayOne.AddHours(1).ToUnixTimeMilliseconds();
        Assert.Equal(125, Remaining([session], 1, DayOne.AddMinutes(56)));
        Assert.Equal(120, Remaining([session], 1, DayOne.AddHours(1)));
        WorkoutSessionLog another = Session(DayOne.AddHours(2), 55);
        another.WorkoutMinutes = 60;
        Assert.Equal(120, Remaining([session, another], 1, DayOne.AddHours(4)));
    }

    [Fact]
    public void MidnightCompletionTopUpDoesNotMoveActualBlocksBetweenDates()
    {
        DateTimeOffset start = new(2026, 9, 1, 23, 30, 0, TimeSpan.Zero);
        WorkoutSessionLog session = Session(start, 53);
        session.WorkoutMinutes = 60;
        WorkoutSessionLog earlier = Session(DayOne, 60);
        // September 1 is already capped. September 2 gets 24 actual + 7 credit.
        Assert.Equal(89, Remaining([earlier, session], 1, start.AddHours(2)));
    }

    [Fact]
    public void LightAfterThresholdReachedTodayResetsOnlyTomorrow()
    {
        List<WorkoutSessionLog> history = Enumerable.Range(0, 3)
            .Select(day => Session(DayOne.AddDays(day), 60)).ToList();
        DateTimeOffset today = DayOne.AddDays(2).AddHours(2);
        history.Add(Session(today, 3, light: true));
        Assert.True(Due(history, today.AddHours(1)));
        Assert.False(Due(history, today.AddDays(1)));
    }

    [Fact]
    public void FullRestDayResetsButOvernightDoesNot()
    {
        WorkoutSessionLog[] history = Enumerable.Range(0, 3)
            .Select(day => Session(DayOne.AddDays(day), 60)).ToArray();
        Assert.True(Due(history, DayOne.AddDays(3)));
        Assert.False(Due(history, DayOne.AddDays(4)));
        Assert.Equal(60, Remaining(history, 3, DayOne.AddDays(4)));
    }

    [Fact]
    public void ModifierChangesDoNotRewriteWorkAlreadyCompleted()
    {
        WorkoutSessionLog session = Session(DayOne, 30);
        session.Status = WorkoutSessionStatus.Interrupted;
        session.ModifierChanges = [new WorkoutModifierChangeLog
        {
            ChangedAtUnixMilliseconds = DayOne.AddMinutes(10).ToUnixTimeMilliseconds(),
            NewModifiers = WorkoutModifiers.Light,
        }];
        // Blocks finish at :01 through :30; nine preceded the mode change.
        Assert.Equal(171, Remaining([session], 1, DayOne.AddHours(1)));
    }

    [Fact]
    public void BlocksCrossingMidnightUseTheirOwnLocalCalendarDate()
    {
        DateTimeOffset start = new(2026, 9, 1, 23, 30, 0, TimeSpan.Zero);
        WorkoutSessionLog session = Session(start, 90);
        // 29 blocks on September 1, 61 on September 2: 29 + capped 60.
        Assert.Equal(91, Remaining([session], 1, start.AddHours(2)));
    }

    [Fact]
    public void LegacyDurationAndInferredDaysRemainBackwardCompatible()
    {
        WorkoutSessionLog old = Session(DayOne, 30);
        old.Blocks.Clear();
        Assert.Equal(5, Remaining([old], 30, DayOne.AddDays(1)));
        Assert.Equal(1, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            [Session(DayOne.AddDays(1), 60)], 60,
            DayOne.AddDays(2).ToUnixTimeMilliseconds(), TimeZoneInfo.Utc,
            [DayOne.ToUnixTimeMilliseconds()]));
        Assert.Equal(5, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            [old], 30, DayOne.AddDays(1).ToUnixTimeMilliseconds(),
            TimeZoneInfo.Utc, [DayOne.ToUnixTimeMilliseconds()]));
    }

    private static bool Due(IEnumerable<WorkoutSessionLog> history, DateTimeOffset now) =>
        WorkoutLightDayPolicy.IsLightDayDue(history, now.ToUnixTimeMilliseconds(),
            TimeZoneInfo.Utc);

    private static int Remaining(IEnumerable<WorkoutSessionLog> history,
        int duration, DateTimeOffset now) =>
        WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(history, duration,
            now.ToUnixTimeMilliseconds(), TimeZoneInfo.Utc);

    private static WorkoutSessionLog Session(DateTimeOffset start, int minutes,
        bool light = false) => new()
    {
        StartedAtUnixMilliseconds = start.ToUnixTimeMilliseconds(),
        EndedAtUnixMilliseconds = start.AddMinutes(minutes).ToUnixTimeMilliseconds(),
        WorkoutMinutes = minutes,
        Status = WorkoutSessionStatus.Completed,
        IsLightDay = light,
        Modifiers = light ? WorkoutModifiers.Light : WorkoutModifiers.None,
        Blocks = Enumerable.Range(1, minutes).Select(minute => new WorkoutBlockLog
        {
            CompletedAtUnixMilliseconds = start.AddMinutes(minute).ToUnixTimeMilliseconds(),
        }).ToList(),
    };
}
