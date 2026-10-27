using System.Text.Json;

namespace NomadicMethod.Services;

// A duration is a time budget, independent of anatomical resolution and catalog
// audit thresholds. Setup, editing and restoration share this same contract.
public static class WorkoutDurationPolicy
{
    public static IReadOnlyList<int> Minutes { get; }
    public static int DefaultMinutes { get; }
    public static int MinimumMinutes => Minutes[0];
    public static int MaximumMinutes => Minutes[^1];

    static WorkoutDurationPolicy()
    {
        using Stream source = typeof(WorkoutDurationPolicy).Assembly
            .GetManifestResourceStream("NomadicMethod.WorkoutDurations")
            ?? throw new InvalidOperationException("The workout duration contract is missing.");
        using JsonDocument document = JsonDocument.Parse(source);
        int[] minutes = document.RootElement.GetProperty("minutes")
            .EnumerateArray().Select(value => value.GetInt32()).ToArray();
        DefaultMinutes = document.RootElement.GetProperty("defaultMinutes").GetInt32();
        if (minutes.Length == 0 || minutes.Any(value => value <= 0) ||
            !minutes.SequenceEqual(minutes.Distinct().Order()) || !minutes.Contains(DefaultMinutes))
            throw new InvalidOperationException("Invalid workout duration contract.");
        Minutes = Array.AsReadOnly(minutes);
    }

    public static bool IsSupported(int minutes) => Minutes.Contains(minutes);

    public static int Normalize(int minutes) => Minutes
        .OrderBy(candidate => Math.Abs((long)candidate - minutes))
        .ThenByDescending(candidate => candidate)
        .First();
}
