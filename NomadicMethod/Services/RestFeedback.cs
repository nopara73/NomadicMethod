using System.Globalization;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

// Presentation only. Warning membership comes from the decision itself, never
// a second implementation of its thresholds in the UI.
public static class RestFeedback
{
    public const string Cadence = "Training cycle complete";
    public const string MuscleRecovery = "Muscles recovering";
    public const string FrozenWorkout = "Recovery for this workout";

    public static string Oura(OuraRecoveryAssessment assessment)
    {
        if (assessment.Verdict != OuraRecoveryVerdict.Light) return string.Empty;
        var reasons = new List<string>();
        OuraRecoveryWarning signals = assessment.WarningSignals;
        if (signals.HasFlag(OuraRecoveryWarning.HeartRate))
            reasons.Add(Comparison("RHR", assessment.BaselineHeartRate, assessment.RecentHeartRate, "<", "above"));
        if (signals.HasFlag(OuraRecoveryWarning.Hrv))
            reasons.Add(Comparison("HRV", Exponent(assessment.BaselineLogHrv), Exponent(assessment.RecentLogHrv), ">", "below"));
        if (signals.HasFlag(OuraRecoveryWarning.LatestSleep) && assessment.LatestSleepMinutes is double latest)
            reasons.Add($"Sleep: {SleepTime(latest)} < {(assessment.Reason == "very-short-sleep" ? 5 : 6)}h");
        else if (signals.HasFlag(OuraRecoveryWarning.AverageSleep) && assessment.RecentSleepMinutes is double average)
            reasons.Add($"Sleep avg: {SleepTime(average)} < 7h");
        return reasons.Count == 0 ? FrozenWorkout : string.Join(" · ", reasons);
    }

    private static double? Exponent(double? value) => value.HasValue ? Math.Exp(value.Value) : null;

    private static string Comparison(string label, double? baseline, double? recent, string comparison, string direction)
    {
        if (baseline is not double normal || recent is not double current ||
            !double.IsFinite(normal) || !double.IsFinite(current)) return $"{label} {direction} baseline";
        string left = normal.ToString("0.#", CultureInfo.InvariantCulture);
        string right = current.ToString("0.#", CultureInfo.InvariantCulture);
        // Do not print e.g. 50 > 50 when a real small difference rounds away.
        return left == right ? $"{label} {direction} baseline" : $"{label}: {left} {comparison} {right}";
    }

    private static string SleepTime(double minutes)
    {
        // Floor so a duration below the boundary cannot display as equal to it.
        int total = (int)Math.Floor(minutes);
        return total % 60 == 0 ? $"{total / 60}h" : $"{total / 60}h{total % 60:00}";
    }
}
