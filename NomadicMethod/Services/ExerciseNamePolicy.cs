using System.Text.Json;

namespace NomadicMethod.Services;

// Display wording can improve without becoming a new movement or reviving
// feedback for a different action that historically used the same numeric ID.
public static class ExerciseNamePolicy
{
    private sealed record ReviewedName(string Identity, string Display, HashSet<string> Names);
    private static readonly IReadOnlyDictionary<int, ReviewedName> Reviewed = Load();

    private static Dictionary<int, ReviewedName> Load()
    {
        using Stream source = typeof(ExerciseNamePolicy).Assembly
            .GetManifestResourceStream("NomadicMethod.ExerciseNames")
            ?? throw new InvalidOperationException("Reviewed exercise names are missing.");
        using JsonDocument document = JsonDocument.Parse(source);
        if (document.RootElement.GetProperty("version").GetInt32() != 1)
            throw new InvalidOperationException("Unsupported exercise-name policy.");
        var result = new Dictionary<int, ReviewedName>();
        foreach (JsonElement entry in document.RootElement.GetProperty("names").EnumerateArray())
        {
            int id = entry.GetProperty("id").GetInt32();
            string identity = entry.GetProperty("identityName").GetString()!;
            string display = entry.GetProperty("name").GetString()!;
            var names = entry.GetProperty("previousNames").EnumerateArray()
                .Select(name => name.GetString()!).ToHashSet(StringComparer.Ordinal);
            names.Add(identity);
            names.Add(display);
            if (id <= 0 || names.Any(string.IsNullOrWhiteSpace) ||
                !result.TryAdd(id, new(identity, display, names)))
                throw new InvalidOperationException("Invalid reviewed exercise name.");
        }
        return result;
    }

    public static string IdentityName(int id, string name) =>
        Reviewed.TryGetValue(id, out ReviewedName? reviewed) && reviewed.Names.Contains(name)
            ? reviewed.Identity : name;

    public static string DisplayName(int id, string name) =>
        Reviewed.TryGetValue(id, out ReviewedName? reviewed) && reviewed.Names.Contains(name)
            ? reviewed.Display : name;
}
