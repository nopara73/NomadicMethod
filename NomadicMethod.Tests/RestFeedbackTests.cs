using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class RestFeedbackTests
{
    [Fact]
    public void ShowsOnlyTriggeredSignalsInHumanUnitsWithBaselineFirst()
    {
        var assessment = new OuraRecoveryAssessment(OuraRecoveryVerdict.Light, "multiple-warnings",
            RecentHeartRate: 62.01, BaselineHeartRate: 56.985,
            RecentLogHrv: Math.Log(29.8), BaselineLogHrv: Math.Log(35.5),
            WarningSignals: OuraRecoveryWarning.HeartRate | OuraRecoveryWarning.Hrv);
        Assert.Equal("RHR: 57 < 62 · HRV: 35.5 > 29.8", RestFeedback.Oura(assessment));
        Assert.Equal("RHR: 57 < 62", RestFeedback.Oura(assessment with
            { WarningSignals = OuraRecoveryWarning.HeartRate }));
        Assert.Equal(string.Empty, RestFeedback.Oura(assessment with
            { Verdict = OuraRecoveryVerdict.Unknown }));
    }

    [Theory]
    [InlineData(299.99, "Sleep: 4h59 < 5h")]
    [InlineData(240, "Sleep: 4h < 5h")]
    public void SevereSleepShowsActualDurationWithoutRoundingAwayItsBoundary(double minutes, string expected)
    {
        var assessment = new OuraRecoveryAssessment(OuraRecoveryVerdict.Light, "very-short-sleep",
            LatestSleepMinutes: minutes, WarningSignals: OuraRecoveryWarning.LatestSleep);
        Assert.Equal(expected, RestFeedback.Oura(assessment));
    }

    [Fact]
    public void DistinguishesLastSleepFromThreeNightAverageWithoutCountingBothAsSeparateCauses()
    {
        var assessment = new OuraRecoveryAssessment(OuraRecoveryVerdict.Light, "multiple-warnings",
            RecentSleepMinutes: 400, LatestSleepMinutes: 350,
            WarningSignals: OuraRecoveryWarning.LatestSleep | OuraRecoveryWarning.AverageSleep);
        Assert.Equal("Sleep: 5h50 < 6h", RestFeedback.Oura(assessment));
        Assert.Equal("Sleep avg: 6h40 < 7h", RestFeedback.Oura(assessment with
            { LatestSleepMinutes = 480, WarningSignals = OuraRecoveryWarning.AverageSleep }));
    }

    [Fact]
    public void SmallChangesAndOldAuditsNeverInventAnInequality()
    {
        var assessment = new OuraRecoveryAssessment(OuraRecoveryVerdict.Light, "multiple-warnings",
            RecentLogHrv: Math.Log(49.99), BaselineLogHrv: Math.Log(50.01),
            WarningSignals: OuraRecoveryWarning.Hrv);
        Assert.Equal("HRV below baseline", RestFeedback.Oura(assessment));
        Assert.Equal(RestFeedback.FrozenWorkout, RestFeedback.Oura(assessment with
            { WarningSignals = OuraRecoveryWarning.None }));
    }
}
