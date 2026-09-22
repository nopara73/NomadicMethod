using System.Text.Json;
using System.Text.Json.Nodes;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class OuraRecoveryPolicyTests
{
    private const long Day = 86_400_000;
    private static readonly long SleepEnd = new DateTimeOffset(2026, 9, 11, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static IEnumerable<object[]> Cases() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "oura-recovery-cases.json")))!
        .AsArray().Select(c => new object[] { c!["name"]!.GetValue<string>(), c.ToJsonString() });

    private static OuraRecoverySnapshot Snapshot() => new()
    {
        FetchedAtUnixMilliseconds = SleepEnd + 30 * 60_000,
        Nights = Enumerable.Range(0, 21).Select(i => new OuraRecoveryNight
        {
            WakeDay = (int)(SleepEnd / Day) - i,
            SleepEndUnixMilliseconds = SleepEnd - i * Day,
            SleepMinutes = 480, MainSleepMinutes = 480, DurationReliable = true,
            HeartRate = 60 + (i % 3 - 1), Hrv = 50 + (i % 3 - 1) * 2,
            HeartRateCoverage = .95, HrvCoverage = .95,
        }).ToList(),
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void RecoveryDecisionCases(string name, string serialized)
    {
        _ = name;
        JsonNode test = JsonNode.Parse(serialized)!;
        JsonNode snapshot = JsonSerializer.SerializeToNode(Snapshot(), JsonOptions)!;
        var nights = snapshot["nights"]!.AsArray();
        foreach (JsonNode? change in test["changes"]?.AsArray() ?? [])
            foreach (JsonNode? index in change!["days"]!.AsArray())
                foreach (var value in change["values"]!.AsObject())
                    nights[index!.GetValue<int>()]![value.Key] = value.Value!.DeepClone();
        foreach (int i in (test["omitDays"]?.AsArray() ?? []).Select(n => n!.GetValue<int>()).OrderDescending())
            nights.RemoveAt(i);
        if (test["keepLatestOnly"]?.GetValue<bool>() == true)
            while (nights.Count > 1) nights.RemoveAt(nights.Count - 1);
        for (int i = 0; i < (test["duplicateLatest"]?.GetValue<int>() ?? 0); i++) nights.Add(nights[0]!.DeepClone());
        if (test["source"] is { } source) snapshot["source"] = source.DeepClone();
        OuraRecoverySnapshot? input = test["nullSnapshot"]?.GetValue<bool>() == true ? null :
            snapshot.Deserialize<OuraRecoverySnapshot>(JsonOptions);
        long now = SleepEnd + (test["nowHours"]?.GetValue<int>() ?? 1) * 3_600_000L;
        var result = OuraRecoveryPolicy.Evaluate(input, now,
            test["workAfterSleep"]?.GetValue<bool>() == true ? SleepEnd + 60_000 : 0);
        Assert.Equal(test["verdict"]!.GetValue<string>(), result.Verdict.ToString());
        Assert.Equal(test["reason"]!.GetValue<string>(), result.Reason);
        if (result.BaselineNights >= 14)
        {
            OuraRecoveryWarning flags = result.WarningSignals;
            int explainedWarnings = (flags.HasFlag(OuraRecoveryWarning.Hrv) ? 1 : 0) +
                (flags.HasFlag(OuraRecoveryWarning.HeartRate) ? 1 : 0) +
                ((flags & (OuraRecoveryWarning.LatestSleep | OuraRecoveryWarning.AverageSleep)) != 0 ? 1 : 0);
            Assert.Equal(result.Warnings, explainedWarnings);
        }
        if (result.Reason == "very-short-sleep")
        {
            Assert.Equal(OuraRecoveryWarning.LatestSleep, result.WarningSignals);
            Assert.True(result.LatestSleepMinutes < 300);
        }
        Assert.Equal(result.Verdict != OuraRecoveryVerdict.Regular,
            OuraRecoveryPolicy.RequiresLight(true, result));
        Assert.Equal(result.Verdict == OuraRecoveryVerdict.Light,
            OuraRecoveryPolicy.RequiresLight(false, result));
    }

    [Fact]
    public void HealthContextIsNotPersistedWithWorkoutData()
    {
        var state = new WorkoutState { OuraRecovery = Snapshot() };
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup] = new() { [1] = -2 };
        state.ActiveWorkoutSession = new() { AutomaticLightRequiredAtStart = false };
        string json = JsonSerializer.Serialize(state);
        Assert.DoesNotContain("oura", json, StringComparison.OrdinalIgnoreCase);
        var restored = JsonSerializer.Deserialize<WorkoutState>(json)!;
        Assert.Null(restored.OuraRecovery);
        Assert.False(restored.ActiveWorkoutSession!.AutomaticLightRequiredAtStart);
        Assert.Equal(-2, restored.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][1]);
    }

    [Fact]
    public void MissingMeasurementsCannotManufactureRecovery()
    {
        var snapshot = Snapshot();
        snapshot.Nights[0].HeartRate = double.NaN;
        Assert.Equal("invalid-data", OuraRecoveryPolicy.Evaluate(snapshot, SleepEnd + 3_600_000).Reason);
        snapshot = Snapshot();
        snapshot.FetchedAtUnixMilliseconds = SleepEnd + Day;
        Assert.Equal("invalid-data", OuraRecoveryPolicy.Evaluate(snapshot, SleepEnd + 3_600_000).Reason);
    }

    [Fact]
    public void ServiceUsesOuraOnlyForAutomaticGateAndPreservesManualLightAndRecovery()
    {
        var exercises = JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;
        var service = new ExerciseSessionService(exercises,
            utcNowProvider: () => DateTimeOffset.FromUnixTimeMilliseconds(SleepEnd + 3_600_000),
            localTimeZone: TimeZoneInfo.Utc);
        var state = new WorkoutState { OuraRecovery = Snapshot() };
        state.WorkoutHistory = Enumerable.Range(1, 3).Select(i => new WorkoutSessionLog
        {
            SessionId = i, StartedAtUnixMilliseconds = SleepEnd - i * Day,
            EndedAtUnixMilliseconds = SleepEnd - i * Day + 3_600_000,
            WorkoutMinutes = 60, Status = WorkoutSessionStatus.Completed,
            Blocks = Enumerable.Range(1, 60).Select(n => new WorkoutBlockLog
                { CompletedAtUnixMilliseconds = SleepEnd - i * Day + n * 60_000 }).ToList(),
        }).ToList();
        Assert.True(WorkoutLightDayPolicy.IsLightDayDue(state.WorkoutHistory, SleepEnd + 3_600_000, TimeZoneInfo.Utc));
        Assert.False(service.IsAutomaticLightDayDue(state));
        Assert.False(service.GetDefaultWorkoutModifiers(state).HasFlag(WorkoutModifiers.Light));
        state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle["Chest"] = SleepEnd + 60_000;
        Assert.True(service.IsAutomaticLightDayDue(state));
        state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle.Clear();
        state.ActiveWorkoutSession = new() { AutomaticLightRequiredAtStart = false };
        state.OuraRecovery = null;
        Assert.False(service.IsAutomaticLightDayDue(state));
        state.ActiveWorkoutSession.AutomaticLightRequiredAtStart = true;
        Assert.True(service.IsAutomaticLightDayDue(state));
        Assert.Equal(3, state.WorkoutHistory.Count);
        Assert.All(state.WorkoutHistory, log => Assert.Equal(60, log.Blocks.Count));
    }

    [Fact]
    public void NightBuilderDeduplicatesAndDoesNotFillGapsOrAwakeTime()
    {
        long start = SleepEnd - 8 * 3_600_000;
        var sleep = new OuraSleep(start, SleepEnd, (int)(SleepEnd / Day), 8,
            [new(start, SleepEnd)], [new(start, SleepEnd)]);
        OuraSample[] samples = Enumerable.Range(0, 96)
            .Select(i => new OuraSample(start + i * 300_000 + 150_000, 50)).ToArray();
        var complete = OuraNightBuilder.Build([sleep, sleep], samples.Concat(samples), samples, SleepEnd + 60_000);
        Assert.Single(complete.Nights);
        Assert.Equal(480, complete.Nights[0].SleepMinutes);
        Assert.Equal(1, complete.Nights[0].HrvCoverage);
        var sparse = OuraNightBuilder.Build([sleep], samples, samples.Take(10), SleepEnd + 60_000);
        Assert.InRange(sparse.Nights[0].HrvCoverage, .10, .11);
        var awake = sleep with { Asleep = [new(start + 3_600_000, SleepEnd)] };
        var staged = OuraNightBuilder.Build([awake], samples, samples, SleepEnd + 60_000);
        Assert.Equal(420, staged.Nights[0].MainSleepMinutes);
        Assert.Equal(1, staged.Nights[0].HrvCoverage);
    }

    [Fact]
    public void NightBuilderIncludesNapsButDoesNotTreatANapAsMainSleep()
    {
        long start = SleepEnd - 4 * 3_600_000;
        var main = new OuraSleep(start, SleepEnd, (int)(SleepEnd / Day), 8,
            [new(start, SleepEnd)], [new(start, SleepEnd)]);
        var nap = new OuraSleep(SleepEnd - 18 * 3_600_000, SleepEnd - 16 * 3_600_000,
            (int)(SleepEnd / Day) - 1, 16, [new(SleepEnd - 18 * 3_600_000, SleepEnd - 16 * 3_600_000)],
            [new(SleepEnd - 18 * 3_600_000, SleepEnd - 16 * 3_600_000)]);
        var result = OuraNightBuilder.Build([nap, main], [], [], SleepEnd + 60_000);
        Assert.Equal(360, result.Nights[0].SleepMinutes);
        Assert.True(result.Nights[0].DurationReliable);
        Assert.False(result.Nights[1].DurationReliable);
        var afternoonNap = new OuraSleep(SleepEnd + 2 * 3_600_000, SleepEnd + 4 * 3_600_000,
            (int)(SleepEnd / Day), 12, [new(SleepEnd + 2 * 3_600_000, SleepEnd + 4 * 3_600_000)],
            [new(SleepEnd + 2 * 3_600_000, SleepEnd + 4 * 3_600_000)]);
        var later = OuraNightBuilder.Build([main, afternoonNap], [], [], SleepEnd + 5 * 3_600_000);
        Assert.Equal(SleepEnd, later.Nights[0].SleepEndUnixMilliseconds);
        Assert.Equal(360, later.Nights[0].SleepMinutes);
    }
}
