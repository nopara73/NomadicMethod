using System.Globalization;

namespace NomadicMethod.Services;

/// <summary>A small, honest comparison of the same fractional estimates used by the profile.</summary>
public static class CompletionProfilePolicy
{
    public sealed record Change(string Label, string Before, string After)
    {
        public string Description => $"{Label}: {Speak(Before)} to {Speak(After)}";
        private static string Speak(string value) => value == "—" ? "unknown" : value;
    }

    // Null is an unavailable baseline, unlike a valid all-zero (unknown-axis) profile.
    public static double[]? CopySnapshot(double[]? values) =>
        values is { Length: 10 } && values.All(value =>
            double.IsFinite(value) && (value == 0 || value is >= 1 and <= 5))
            ? [.. values] : null;

    public static IReadOnlyList<Change> Changes(double[]? before, double[]? after)
    {
        if (CopySnapshot(before) is null || CopySnapshot(after) is null)
            return [];
        var changes = new List<Change>();
        for (int index = 0; index < FitnessProfilePolicy.Dimensions.Length; index++)
        {
            if (before![index] == after![index]) continue;
            string oldValue = Format(before[index]);
            string newValue = Format(after[index]);
            // Do not imply a visible change when both fractional means round alike.
            if (oldValue != newValue)
                changes.Add(new(string.Join(" ", FitnessProfilePolicy.Labels[index]), oldValue, newValue));
        }
        return changes;
    }

    private static string Format(double value) => value == 0 ? "—"
        : (Math.Floor(value * 10 + 0.5) / 10).ToString("0.0", CultureInfo.InvariantCulture);
}
