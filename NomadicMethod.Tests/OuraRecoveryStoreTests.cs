using System.Text.Json;
using NomadicMethod.Data;
using NomadicMethod.Models;

namespace NomadicMethod.Tests;

public sealed class OuraRecoveryStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("nomadic-method-oura-tests-");
    private OuraRecoveryStore Store => new(_directory.FullName);
    private string CachePath => Path.Combine(_directory.FullName, "oura-recovery.json");

    [Fact]
    public void FirstUseNeedsOnlyAndroidConsentAndDoesNotCreateAnOptIn()
    {
        OuraRecoveryCache cache = Store.Load();
        Assert.False(cache.PermissionRequestAttempted);
        Assert.Null(cache.Snapshot);
        Assert.Empty(cache.Decisions);
        cache.PermissionRequestAttempted = true;
        Store.Save(cache);
        Assert.True(Store.Load().PermissionRequestAttempted);
        Assert.DoesNotContain("enabled", File.ReadAllText(CachePath), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpgradePreservesPrivateEvidenceAndIgnoresOldOptIn(bool enabled)
    {
        File.WriteAllText(CachePath, $$$"""
            {"enabled":{{{enabled.ToString().ToLowerInvariant()}}},
             "snapshot":{"version":1,"source":"com.ouraring.oura","fetchedAtUnixMilliseconds":123,
               "nights":[{"wakeDay":42,"heartRate":60,"hrv":45}]},
             "decisions":[{"evaluatedAtUnixMilliseconds":123,"cadenceDue":false,"lightRequired":true,
               "assessment":{"verdict":"Light","reason":"multiple-warnings"}}]}
            """);
        OuraRecoveryCache restored = Store.Load();
        Assert.Equal(123, restored.Snapshot!.FetchedAtUnixMilliseconds);
        Assert.Equal(60, Assert.Single(restored.Snapshot.Nights).HeartRate);
        Assert.Equal(OuraRecoveryVerdict.Light, Assert.Single(restored.Decisions).Assessment.Verdict);
        Store.Save(restored);
        Assert.DoesNotContain("enabled", File.ReadAllText(CachePath), StringComparison.OrdinalIgnoreCase);
        Assert.Single(Store.Load().Decisions);
    }

    [Fact]
    public void AuditRemainsBoundedAcrossAtomicReplacements()
    {
        var cache = new OuraRecoveryCache();
        for (int i = 0; i < 130; i++)
            cache.Decisions.Add(new(i, false, false, new(OuraRecoveryVerdict.Unknown, "no-sleep")));
        Store.Save(cache);
        Store.Save(Store.Load());
        OuraRecoveryCache restored = Store.Load();
        Assert.Equal(120, restored.Decisions.Count);
        Assert.Equal(10, restored.Decisions[0].EvaluatedAtUnixMilliseconds);
        Assert.Equal(129, restored.Decisions[^1].EvaluatedAtUnixMilliseconds);
        Assert.False(File.Exists(CachePath + ".tmp"));
    }

    [Fact]
    public void WorkoutStartEvidenceSurvivesRefreshAndRestartInPrivateStorageOnly()
    {
        var first = new OuraDecisionAudit(100, false, true,
            new(OuraRecoveryVerdict.Light, "multiple-warnings",
                RecentHeartRate: 62, BaselineHeartRate: 57,
                WarningSignals: OuraRecoveryWarning.HeartRate | OuraRecoveryWarning.Hrv),
            WorkoutSessionId: 1234);
        var cache = new OuraRecoveryCache { Decisions = [first] };
        cache.Decisions.Add(new(200, false, false, new(OuraRecoveryVerdict.Regular, "within-baseline")));
        Store.Save(cache);
        OuraDecisionAudit restored = Assert.Single(Store.Load().Decisions, d => d.WorkoutSessionId == 1234);
        Assert.Equal(first, restored);
        Assert.Equal(0, Store.Load().Decisions[^1].WorkoutSessionId);
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("null")]
    [InlineData("{\"decisions\":null}")]
    public void CorruptOrIncompleteCacheIsSafe(string contents)
    {
        File.WriteAllText(CachePath, contents);
        OuraRecoveryCache restored = Store.Load();
        Assert.Null(restored.Snapshot);
        Assert.Empty(restored.Decisions);
    }

    [Fact]
    public void RevocationClearsHealthEvidenceWithoutTouchingWorkoutFilesOrPromptingAgain()
    {
        string workoutPath = Path.Combine(_directory.FullName, "workout.json");
        File.WriteAllText(workoutPath, "unchanged workout state");
        Store.Save(new() { Snapshot = new() { Nights = [new() { HeartRate = 60 }] } });
        Store.Save(new() { PermissionRequestAttempted = true });
        OuraRecoveryCache restored = Store.Load();
        Assert.Null(restored.Snapshot);
        Assert.Empty(restored.Decisions);
        Assert.True(restored.PermissionRequestAttempted);
        Assert.Equal("unchanged workout state", File.ReadAllText(workoutPath));
        Assert.Null(JsonDocument.Parse(File.ReadAllText(CachePath)).RootElement.GetProperty("snapshot").GetString());
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
