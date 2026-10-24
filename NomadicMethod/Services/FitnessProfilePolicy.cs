using System.Text.Json;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed record FitnessContribution(int MovementId, int ExerciseId, string Name, int Level);
public sealed record FavouriteFitnessProfileAxis(double Mean, IReadOnlyList<FitnessContribution> Contributions);
public sealed record FitnessEvidence(int MovementId, int ExerciseId, string Name, int[] RatedLevels,
    ExerciseOutcome Feedback, int[] SupportedLevels);
public sealed record FitnessProfileAxis(double Mean, IReadOnlyList<FitnessEvidence> Contributions,
    int[]? BestLevels = null);

/// <summary>Equal-weight challenge estimates from each movement's latest difficulty response.</summary>
public static class FitnessProfilePolicy
{
    public const int EvidenceVersion = 2;
    public const int MaximumLevel = 5;
    public const int WebBoundaryLevel = 4;

    // Rounding is only for display. Selection uses the complete best-fit set.
    public static int RoundLevel(double level) => !double.IsFinite(level) || level <= 0
        ? 0 : (int)Math.Clamp(Math.Floor(level + 0.5), 1, MaximumLevel);

    public static string FormatMean(double mean)
    {
        // Keep the displayed mean on the same side of a rounding boundary as the exact mean.
        double shown = Math.Floor(mean * 100 + 1e-9) / 100;
        return shown.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
            (Math.Abs(mean - shown) > 1e-9 ? "…" : "");
    }

    // Reserve the full expert extent; the drawn web stops one level inside it.
    public static double ChartRadiusFraction(double level) => level / MaximumLevel;

    public static readonly string[] Dimensions =
    [
        "cardiorespiratoryEndurance", "muscularStrength", "muscularEndurance",
        "flexibility", "agility", "balance", "coordination", "power", "reactionTime", "speed",
    ];

    public static readonly string[][] Labels =
    [
        ["cardio"], ["strength"], ["muscle", "endurance"], ["flexibility"],
        ["agility"], ["balance"], ["coordination"], ["power"], ["reaction"], ["speed"],
    ];

    public static IReadOnlyDictionary<int, int[][]> Load(Stream source)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            !root.GetProperty("dimensions").EnumerateArray()
                .Select(value => value.GetString()).SequenceEqual(Dimensions))
        {
            throw new InvalidDataException("Invalid fitness rating schema.");
        }
        var result = new Dictionary<int, int[][]>();
        foreach (JsonProperty property in root.GetProperty("ratings").EnumerateObject())
        {
            int[][] levels = property.Value.EnumerateArray()
                .Select(value => value.EnumerateArray().Select(level => level.GetInt32()).ToArray())
                .ToArray();
            if (!int.TryParse(property.Name, out int id) || id <= 0 ||
                levels.Length != Dimensions.Length || levels.Any(set =>
                    set.Any(value => value is < 1 or > 5) ||
                    !set.SequenceEqual(set.Distinct().Order())))
            {
                throw new InvalidDataException("Invalid exercise fitness ratings.");
            }
            result.Add(id, levels);
        }
        return result;
    }

    public static double[] Calculate(
        IReadOnlyList<Exercise> exercises,
        WorkoutState state,
        IReadOnlyDictionary<int, int[][]> ratings)
        => Inspect(exercises, state, ratings).Select(axis => axis.Mean).ToArray();

    // Select the latest informative response BEFORE checking its ratings. Meh,
    // timeouts and pending gestures neither contribute nor erase earlier evidence.
    public static FitnessProfileAxis[] Inspect(
        IReadOnlyList<Exercise> exercises,
        WorkoutState state,
        IReadOnlyDictionary<int, int[][]> ratings)
    {
        var latest = LatestMovementDecisions(exercises, state);

        var contributions = Dimensions.Select(_ => new List<FitnessEvidence>()).ToArray();
        foreach (var (movementId, (exercise, decision)) in latest.OrderBy(entry => entry.Key))
        {
            if (!ratings.TryGetValue(exercise.Id, out int[][]? levels)) continue;
            int strongest = levels.SelectMany(set => set).DefaultIfEmpty(0).Max();
            for (int dimension = 0; dimension < Dimensions.Length; dimension++)
            {
                int[] set = levels[dimension];
                if (set.Length == 0 || set[^1] != strongest) continue;
                contributions[dimension].Add(new(movementId, exercise.Id, exercise.Name,
                    [.. set], decision.Outcome, SupportedLevels(set, decision.Outcome)));
            }
        }
        return contributions.Select(Fit).ToArray();
    }

    public static IReadOnlyDictionary<int, (Exercise Exercise, WorkoutDecisionLog Decision)> LatestMovementDecisions(
        IReadOnlyList<Exercise> exercises, WorkoutState state)
    {
        var byId = exercises.ToDictionary(exercise => exercise.Id);
        var latest = new Dictionary<int, (Exercise Exercise, WorkoutDecisionLog Decision)>();
        IEnumerable<WorkoutSessionLog> sessions = state.WorkoutHistory;
        if (state.ActiveWorkoutSession is { } active) sessions = sessions.Append(active);
        // Stable ordering makes the last recorded decision win a timestamp tie;
        // session identity orders ties across history, with active evidence last.
        foreach (WorkoutDecisionLog decision in sessions.OrderBy(session => session.SessionId)
                     .SelectMany(session => session.Decisions)
                     .OrderBy(decision => decision.DecidedAtUnixMilliseconds))
        {
            if (decision.Outcome is not (ExerciseOutcome.Tick or ExerciseOutcome.TooEasy or ExerciseOutcome.TooHard) ||
                !byId.TryGetValue(decision.RootExerciseId, out Exercise? exercise)) continue;
            int movementId = exercise.SessionMovementId > 0 ? exercise.SessionMovementId : exercise.Id;
            latest[movementId] = (exercise, decision);
        }

        return latest;
    }

    private static int[] SupportedLevels(int[] rated, ExerciseOutcome outcome) => outcome switch
    {
        ExerciseOutcome.Tick => [.. rated],
        ExerciseOutcome.TooEasy => Enumerable.Range(Math.Min(MaximumLevel, rated[^1] + 1),
            MaximumLevel - Math.Min(MaximumLevel, rated[^1] + 1) + 1).ToArray(),
        ExerciseOutcome.TooHard => Enumerable.Range(1, Math.Max(1, rated[0] - 1)).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static FitnessProfileAxis Fit(List<FitnessEvidence> evidence)
    {
        if (evidence.Count == 0) return new(0, [], []);
        // Each movement has one equal vote. Evaluate only the five discrete levels;
        // distance to the nearest supported member preserves noncontiguous sets.
        int[] penalties = Enumerable.Range(1, MaximumLevel).Select(candidate => evidence.Sum(entry =>
            entry.SupportedLevels.Min(level => (candidate - level) * (candidate - level)))).ToArray();
        int minimum = penalties.Min();
        int[] best = Enumerable.Range(1, MaximumLevel).Where(level => penalties[level - 1] == minimum).ToArray();
        return new(best.Length == MaximumLevel ? 0 : best.Average(), evidence.ToArray(), best);
    }

    public static string FormatLevels(IReadOnlyList<int> levels)
    {
        var parts = new List<string>();
        for (int i = 0; i < levels.Count; i++)
        {
            int start = levels[i], end = start;
            while (i + 1 < levels.Count && levels[i + 1] == end + 1) end = levels[++i];
            parts.Add(start == end ? start.ToString() : $"{start}–{end}");
        }
        return string.Join(", ", parts);
    }

    // The planner deliberately retains its existing favourite-based input. Updating
    // the displayed estimate must not silently change fresh choices or saved lineups.
    public static double[] CalculateFavourites(
        IReadOnlyList<Exercise> exercises,
        WorkoutState state,
        IReadOnlyDictionary<int, int[][]> ratings)
        => InspectFavourites(exercises, state, ratings).Select(axis => axis.Mean).ToArray();

    public static FavouriteFitnessProfileAxis[] InspectFavourites(
        IReadOnlyList<Exercise> exercises,
        WorkoutState state,
        IReadOnlyDictionary<int, int[][]> ratings)
    {
        // Current saved preferences only. No history, vote counts, age or workout filtering.
        var kept = state.KeptExerciseRootIdsBySelectionGroupId.Count > 0
            ? state.KeptExerciseRootIdsBySelectionGroupId.Values.SelectMany(ids => ids).ToHashSet()
            : state.LastKeptExerciseIds.ToHashSet();
        var movements = new Dictionary<int, FitnessContribution?[]>();
        foreach (Exercise exercise in exercises.Where(exercise => kept.Contains(exercise.Id)))
        {
            if (!ratings.TryGetValue(exercise.Id, out int[][]? levels))
                continue;
            int strongest = levels.SelectMany(set => set).DefaultIfEmpty(0).Max();
            int movementId = exercise.SessionMovementId > 0 ? exercise.SessionMovementId : exercise.Id;
            if (!movements.TryGetValue(movementId, out FitnessContribution?[]? values))
                movements[movementId] = values = new FitnessContribution?[Dimensions.Length];
            for (int dimension = 0; dimension < Dimensions.Length; dimension++)
            {
                int[] set = levels[dimension];
                // An ambiguous strongest range never promotes a lower secondary rating.
                // Equal saved variants use the lowest id, independent of input order.
                FitnessContribution? prior = values[dimension];
                if (set.Length == 1 && set[0] == strongest && (prior is null ||
                    set[0] > prior.Level || (set[0] == prior.Level && exercise.Id < prior.ExerciseId)))
                    values[dimension] = new(movementId, exercise.Id, exercise.Name, set[0]);
            }
        }

        return Enumerable.Range(0, Dimensions.Length).Select(dimension =>
        {
            FitnessContribution[] values = movements.Values.Select(value => value[dimension])
                .OfType<FitnessContribution>().OrderBy(value => value.MovementId).ToArray();
            return new FavouriteFitnessProfileAxis(values.Length == 0 ? 0d : values.Average(value => value.Level), values);
        }).ToArray();
    }
}
