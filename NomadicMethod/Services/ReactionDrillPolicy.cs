using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed record ReactionAction
{
    public string Label { get; init; } = "";
    public int SourceId { get; init; }
    public double StartSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public bool Mirror { get; init; }
    public string Preview { get; init; } = "";
    public string Video { get; init; } = "";
}
public sealed record ReactionFamily
{
    public string Id { get; init; } = "";
    public ReactionAction[] Actions { get; init; } = [];
}
public sealed record ReactionPlan
{
    public int ExerciseId { get; init; }
    public string FamilyId { get; init; } = "";
    public string Protocol { get; init; } = "";
    [JsonIgnore] public ReactionAction[] Actions { get; init; } = [];
}
public sealed record ReactionDrillDocument
{
    public int Version { get; init; }
    public ReactionFamily[] Families { get; init; } = [];
    public ReactionPlan[] Plans { get; init; } = [];
}
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ReactionDrillDocument))]
public partial class ReactionDrillJsonContext : JsonSerializerContext { }

public sealed class ReactionDrill
{
    public const int PromptMilliseconds = 650;
    public const int ResetMilliseconds = 1200;
    public const int WaitMinimumMilliseconds = 1200;
    public const int WaitMaximumMilliseconds = 2800;
    public ReactionPlan Plan { get; }
    private readonly Func<double> _random;
    public string Phase { get; private set; } = "Idle";
    public bool Reverse { get; private set; }
    public int Number { get; private set; }
    public int ActionIndex { get; private set; } = -1;
    public long Deadline { get; private set; }
    public int DemoActionIndex => Phase == "Feedback" ? ActionIndex : -1;
    public string Cue => Phase switch
    {
        "Prompt" => Number.ToString(),
        "Feedback" => Plan.Actions[ActionIndex].Label,
        "Reset" => "Reset",
        _ => "Ready",
    };
    public ReactionDrill(ReactionPlan plan, Func<double>? random = null)
    { Plan = plan; _random = random ?? Random.Shared.NextDouble; }
    private double Unit()
    {
        double value = _random();
        if (!double.IsFinite(value) || value is < 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(value));
        return value;
    }
    private void BeginReady(long now, long remaining)
    {
        Number = 0;
        ActionIndex = -1;
        Reverse = Plan.Protocol == "ContextChoice" && Unit() >= 0.5;
        int wait = WaitMinimumMilliseconds + (int)Math.Floor(Unit() *
            (WaitMaximumMilliseconds - WaitMinimumMilliseconds + 1));
        double longest = Plan.Actions.Max(action => action.DurationSeconds * 1000);
        Phase = remaining >= wait + PromptMilliseconds + longest + ResetMilliseconds
            ? "Ready" : "Complete";
        Deadline = now + wait;
    }
    public bool Advance(long now, long remaining, bool mediaReady = true)
    {
        if (!mediaReady) return false;
        string previous = Phase;
        if (Phase == "Idle") BeginReady(now, remaining);
        else if (Phase != "Complete" && now >= Deadline)
        {
            if (Phase == "Ready")
            {
                double longest = Plan.Actions.Max(action => action.DurationSeconds * 1000);
                if (remaining < PromptMilliseconds + longest + ResetMilliseconds) Phase = "Complete";
                else
                {
                    Number = (int)Math.Floor(Unit() * Plan.Actions.Length) + 1;
                    ActionIndex = Reverse ? Plan.Actions.Length - Number : Number - 1;
                    Phase = "Prompt";
                    Deadline = now + PromptMilliseconds;
                }
            }
            else if (Phase == "Prompt")
            {
                long duration = (long)Math.Round(Plan.Actions[ActionIndex].DurationSeconds * 1000);
                if (remaining < duration + ResetMilliseconds) Phase = "Complete";
                else { Phase = "Feedback"; Deadline = now + duration; }
            }
            else if (Phase == "Feedback") { Phase = "Reset"; Deadline = now + ResetMilliseconds; }
            else if (Phase == "Reset") BeginReady(now, remaining);
        }
        return previous != Phase;
    }

    public static IReadOnlyDictionary<int, ReactionPlan> Load(Stream stream, IReadOnlyList<Exercise> catalog)
    {
        ReactionDrillDocument document = JsonSerializer.Deserialize(stream,
            ReactionDrillJsonContext.Default.ReactionDrillDocument)
            ?? throw new InvalidDataException("Invalid reaction drill asset.");
        if (document.Version != 1 || document.Families is null || document.Plans is null ||
            document.Families.Any(family => string.IsNullOrWhiteSpace(family.Id)) ||
            document.Families.Select(family => family.Id).Distinct().Count() != document.Families.Length)
            throw new InvalidDataException("Invalid reaction drill version or families.");
        var result = new Dictionary<int, ReactionPlan>();
        foreach (ReactionPlan plan in document.Plans)
        {
            ReactionFamily? family = document.Families.SingleOrDefault(item => item.Id == plan.FamilyId);
            Exercise? exercise = catalog.SingleOrDefault(item => item.Id == plan.ExerciseId);
            int count = plan.Protocol switch { "Initiation" => 1, "TwoChoice" => 2,
                "FourChoice" or "ContextChoice" => 4, _ => 0 };
            if (count == 0 || family?.Actions.Length != 4 || result.ContainsKey(plan.ExerciseId) ||
                exercise is null || exercise.Practice != "Reaction training" || exercise.ReactionCueLevel <= 0)
                throw new InvalidDataException("Invalid reaction plan.");
            ReactionAction[] actions = family.Actions.Take(count).ToArray();
            foreach (ReactionAction action in actions)
                if (!catalog.Any(item => item.Id == action.SourceId) ||
                    string.IsNullOrWhiteSpace(action.Label) || !Asset(action.Preview, "reaction_previews/", ".png") ||
                    !Asset(action.Video, "reaction_videos/", ".mp4") || action.StartSeconds != 0 ||
                    !double.IsFinite(action.DurationSeconds) || action.DurationSeconds is <= 0 or > 10)
                    throw new InvalidDataException("Invalid reaction action.");
            CanonicalMuscleGroup[] claims = [exercise.PrimaryCanonicalGroup, .. exercise.SecondaryCanonicalGroups];
            if (actions.Any(action => claims.Any(group => !catalog.Single(item => item.Id == action.SourceId).Trains(group))))
                throw new InvalidDataException("Invalid reaction training claim: every response must train each claimed muscle.");
            result.Add(plan.ExerciseId, plan with { Actions = actions });
        }
        if (catalog.Any(item => item.Practice == "Reaction training" && !result.ContainsKey(item.Id)))
            throw new InvalidDataException("Missing reaction plan.");
        return result;
    }

    private static bool Asset(string value, string prefix, string suffix) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(prefix, StringComparison.Ordinal) &&
        value.EndsWith(suffix, StringComparison.Ordinal) &&
        !value.Contains("..", StringComparison.Ordinal) &&
        !value[prefix.Length..].Contains('/') && !value.Contains('\\');
}
