using System.Text.Json;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

/// <summary>Equal-weight means of precise, strongest ratings of currently kept movements.</summary>
public static class FitnessProfilePolicy
{
    public const int MaximumLevel = 5;
    public const int WebBoundaryLevel = 4;

    // Preserve the unrounded mean in the profile; display and matching share
    // explicit half-up rounding rather than platform-specific midpoint rules.
    public static int RoundLevel(double level) => !double.IsFinite(level) || level <= 0
        ? 0 : (int)Math.Clamp(Math.Floor(level + 0.5), 1, MaximumLevel);

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
    {
        // Current saved preferences only. No history, vote counts, age or workout filtering.
        var kept = state.KeptExerciseRootIdsBySelectionGroupId.Count > 0
            ? state.KeptExerciseRootIdsBySelectionGroupId.Values.SelectMany(ids => ids).ToHashSet()
            : state.LastKeptExerciseIds.ToHashSet();
        var movements = new Dictionary<int, int[]>();
        foreach (Exercise exercise in exercises.Where(exercise => kept.Contains(exercise.Id)))
        {
            if (!ratings.TryGetValue(exercise.Id, out int[][]? levels))
                continue;
            int strongest = levels.SelectMany(set => set).DefaultIfEmpty(0).Max();
            int movementId = exercise.SessionMovementId > 0 ? exercise.SessionMovementId : exercise.Id;
            if (!movements.TryGetValue(movementId, out int[]? values))
                movements[movementId] = values = new int[Dimensions.Length];
            for (int dimension = 0; dimension < Dimensions.Length; dimension++)
            {
                int[] set = levels[dimension];
                // An ambiguous strongest range never promotes a lower secondary rating.
                if (set.Length == 1 && set[0] == strongest)
                    values[dimension] = Math.Max(values[dimension], set[0]);
            }
        }

        return Enumerable.Range(0, Dimensions.Length).Select(dimension =>
        {
            int[] values = movements.Values.Select(value => value[dimension])
                .Where(value => value > 0).ToArray();
            return values.Length == 0 ? 0d : values.Average();
        }).ToArray();
    }
}
