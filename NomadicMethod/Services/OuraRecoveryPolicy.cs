using NomadicMethod.Models;

namespace NomadicMethod.Services;

// An explicitly heuristic recovery gate, not a diagnosis or a muscle-recovery
// measurement. Android-only by owner decision; see docs/OURA_RECOVERY.md.
public static class OuraRecoveryPolicy
{
    public const long FreshnessMilliseconds = 18 * 60 * 60 * 1000L;

    public static OuraRecoveryAssessment Evaluate(
        OuraRecoverySnapshot? snapshot, long nowUnixMilliseconds,
        long lastMeaningfulWorkUnixMilliseconds = 0)
    {
        if (snapshot is null) return Unknown("not-connected");
        if (snapshot.Version != 1 || snapshot.Source != "com.ouraring.oura" ||
            snapshot.FetchedAtUnixMilliseconds <= 0 ||
            snapshot.FetchedAtUnixMilliseconds > nowUnixMilliseconds ||
            snapshot.Nights is null || snapshot.Nights.Count > 40 ||
            snapshot.Nights.Any(night => !Valid(night)))
            return Unknown("invalid-data");

        // One principal sleep per recorded local wake date. Duplicate summaries
        // must never manufacture the three-night trend or fourteen-night baseline.
        OuraRecoveryNight[] nights = snapshot.Nights
            .GroupBy(night => night.WakeDay)
            .Select(group => group.OrderByDescending(n => n.MainSleepMinutes)
                .ThenByDescending(n => n.SleepEndUnixMilliseconds).First())
            .OrderByDescending(night => night.WakeDay).ToArray();
        if (nights.Length == 0) return Unknown("no-sleep");
        OuraRecoveryNight latest = nights[0];
        long end = latest.SleepEndUnixMilliseconds;
        if (end > snapshot.FetchedAtUnixMilliseconds || end > nowUnixMilliseconds ||
            nowUnixMilliseconds - end >= FreshnessMilliseconds)
            return Unknown("stale-sleep", end);
        if (!latest.DurationReliable) return Unknown("incomplete-sleep", end);

        // Missing sleep never means zero sleep. This exception requires a real,
        // completed, fully staged principal sleep and its preceding 24-hour total.
        if (latest.SleepMinutes < 300)
            return new(OuraRecoveryVerdict.Light, "very-short-sleep", end,
                LatestSleepMinutes: latest.SleepMinutes,
                WarningSignals: OuraRecoveryWarning.LatestSleep);
        if (!Usable(latest)) return Unknown("insufficient-coverage", end);

        OuraRecoveryNight[] recent = nights
            .Where(n => n.WakeDay >= latest.WakeDay - 3 && Usable(n))
            .Take(3).ToArray();
        if (recent.Length < 3)
            return new(OuraRecoveryVerdict.Unknown, "insufficient-recent-nights",
                end, recent.Length);
        OuraRecoveryNight[] baseline = nights.Where(n =>
            n.WakeDay >= latest.WakeDay - 28 &&
            n.WakeDay < recent[^1].WakeDay && Usable(n)).ToArray();
        if (baseline.Length < 14)
            return new(OuraRecoveryVerdict.Unknown, "insufficient-baseline",
                end, recent.Length, baseline.Length);

        double hr = recent.Average(n => n.HeartRate);
        double logHrv = recent.Average(n => Math.Log(n.Hrv));
        double sleep = recent.Average(n => n.SleepMinutes);
        double baselineHr = baseline.Average(n => n.HeartRate);
        double baselineHrv = baseline.Average(n => Math.Log(n.Hrv));
        double hrSd = StandardDeviation(baseline.Select(n => n.HeartRate));
        double hrvSd = StandardDeviation(baseline.Select(n => Math.Log(n.Hrv)));
        bool lowHrv = baselineHrv - logHrv > hrvSd + 1e-9;
        bool highHr = hr - baselineHr >= 5 - 1e-9 && hr - baselineHr > hrSd + 1e-9;
        bool shortLatestSleep = latest.SleepMinutes < 360;
        bool shortAverageSleep = sleep < 420;
        int warnings = (lowHrv ? 1 : 0) + (highHr ? 1 : 0) +
            (shortLatestSleep || shortAverageSleep ? 1 : 0);
        OuraRecoveryWarning signals =
            (highHr ? OuraRecoveryWarning.HeartRate : OuraRecoveryWarning.None) |
            (lowHrv ? OuraRecoveryWarning.Hrv : OuraRecoveryWarning.None) |
            (shortLatestSleep ? OuraRecoveryWarning.LatestSleep : OuraRecoveryWarning.None) |
            (shortAverageSleep ? OuraRecoveryWarning.AverageSleep : OuraRecoveryWarning.None);
        // Strong adverse evidence takes precedence over an unusually high HRV;
        // the high-HRV safeguard must not erase short sleep plus elevated HR.
        OuraRecoveryVerdict verdict = warnings >= 2 ? OuraRecoveryVerdict.Light :
            warnings == 0 ? OuraRecoveryVerdict.Regular : OuraRecoveryVerdict.Unknown;
        string reason = warnings >= 2 ? "multiple-warnings" :
            warnings == 0 ? "within-baseline" : "mixed-signals";
        if (verdict != OuraRecoveryVerdict.Light && logHrv - baselineHrv > 2 * hrvSd + 1e-9)
        {
            verdict = OuraRecoveryVerdict.Unknown;
            reason = "unusually-high-hrv";
        }
        if (verdict == OuraRecoveryVerdict.Regular && lastMeaningfulWorkUnixMilliseconds > end)
        {
            verdict = OuraRecoveryVerdict.Unknown;
            reason = "work-since-sleep";
        }
        return new(verdict, reason, end, recent.Length, baseline.Length, warnings,
            hr, baselineHr, logHrv, baselineHrv, sleep, latest.SleepMinutes, signals);
    }

    public static OuraRecoveryAssessment Evaluate(WorkoutState state, long now) =>
        Evaluate(state.OuraRecovery, now,
            state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle.Values
                .Concat(state.LastHardWorkUnixMillisecondsByPrimaryMuscle.Values)
                .DefaultIfEmpty(0).Max());

    public static bool RequiresLight(bool cadenceDue, OuraRecoveryAssessment assessment) =>
        assessment.Verdict switch
        {
            OuraRecoveryVerdict.Light => true,
            OuraRecoveryVerdict.Regular => false,
            _ => cadenceDue,
        };

    private static OuraRecoveryAssessment Unknown(string reason, long end = 0) =>
        new(OuraRecoveryVerdict.Unknown, reason, end);

    private static bool Valid(OuraRecoveryNight? n) => n is not null &&
        n.WakeDay > 0 && n.SleepEndUnixMilliseconds > 0 &&
        double.IsFinite(n.MainSleepMinutes) && n.MainSleepMinutes > 0 &&
        n.MainSleepMinutes <= 1440 && double.IsFinite(n.SleepMinutes) &&
        n.SleepMinutes > 0 && n.SleepMinutes <= 1440 &&
        double.IsFinite(n.HeartRate) && n.HeartRate >= 0 && n.HeartRate <= 300 &&
        double.IsFinite(n.Hrv) && n.Hrv >= 0 && n.Hrv <= 1000 &&
        double.IsFinite(n.HeartRateCoverage) && n.HeartRateCoverage is >= 0 and <= 1 &&
        double.IsFinite(n.HrvCoverage) && n.HrvCoverage is >= 0 and <= 1;

    private static bool Usable(OuraRecoveryNight n) => n.DurationReliable &&
        n.HeartRate > 0 && n.Hrv > 0 && n.HeartRateCoverage >= 0.7 && n.HrvCoverage >= 0.7;

    private static double StandardDeviation(IEnumerable<double> source)
    {
        double[] values = source.ToArray();
        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1));
    }
}
