using System.Text.Json;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed record FitnessContribution(int MovementId, int ExerciseId, string Name, int Level,
    int RatedLevel = 0, ExerciseOutcome? Feedback = null);
public sealed record FitnessProfileAxis(double Mean, IReadOnlyList<FitnessContribution> Contributions);

/// <summary>Equal-weight challenge estimates from each movement's latest difficulty response.</summary>
public static class FitnessProfilePolicy
{
    public const int EvidenceVersion = 1;
    public const int MaximumLevel = 5;
    public const int WebBoundaryLevel = 4;

    // Preserve the unrounded mean in the profile; display and matching share
    // explicit half-up rounding rather than platform-specific midpoint rules.
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

    // Select the latest difficulty response BEFORE checking its ratings. A newer
    // ambiguous variant must not leave an older precise response masquerading as current.
    public static FitnessProfileAxis[] Inspect(
        IReadOnlyList<Exercise> exercises,
        WorkoutState state,
        IReadOnlyDictionary<int, int[][]> ratings)
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
            if (decision.Outcome is not (ExerciseOutcome.TooEasy or ExerciseOutcome.TooHard) ||
                !byId.TryGetValue(decision.RootExerciseId, out Exercise? exercise)) continue;
            int movementId = exercise.SessionMovementId > 0 ? exercise.SessionMovementId : exercise.Id;
            latest[movementId] = (exercise, decision);
        }

        var contributions = Dimensions.Select(_ => new List<FitnessContribution>()).ToArray();
        foreach (var (movementId, (exercise, decision)) in latest.OrderBy(entry => entry.Key))
        {
            if (!ratings.TryGetValue(exercise.Id, out int[][]? levels)) continue;
            int strongest = levels.SelectMany(set => set).DefaultIfEmpty(0).Max();
            for (int dimension = 0; dimension < Dimensions.Length; dimension++)
            {
                int[] set = levels[dimension];
                if (set.Length != 1 || set[0] != strongest) continue;
                int estimate = Math.Clamp(set[0] + (decision.Outcome == ExerciseOutcome.TooEasy ? 1 : -1),
                    1, MaximumLevel);
                contributions[dimension].Add(new(movementId, exercise.Id, exercise.Name,
                    estimate, set[0], decision.Outcome));
            }
        }
        return contributions.Select(values => new FitnessProfileAxis(
            values.Count == 0 ? 0d : values.Average(value => value.Level), values.ToArray())).ToArray();
    }

    // The planner deliberately retains its existing favourite-based input. Updating
    // the displayed estimate must not silently change fresh choices or saved lineups.
    public static double[] CalculateFavourites(
        IReadOnlyList<Exercise> exercises,
        WorkoutState state,
        IReadOnlyDictionary<int, int[][]> ratings)
        => InspectFavourites(exercises, state, ratings).Select(axis => axis.Mean).ToArray();

    public static FitnessProfileAxis[] InspectFavourites(
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
            return new FitnessProfileAxis(values.Length == 0 ? 0d : values.Average(value => value.Level), values);
        }).ToArray();
    }
}
