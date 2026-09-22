using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;

namespace NomadicMethod.Data;

internal sealed class OuraRecoveryCache
{
    public bool PermissionRequestAttempted { get; set; }
    public OuraRecoverySnapshot? Snapshot { get; set; }
    public List<OuraDecisionAudit> Decisions { get; set; } = [];
}

internal sealed record OuraDecisionAudit(long EvaluatedAtUnixMilliseconds,
    bool CadenceDue, bool LightRequired, OuraRecoveryAssessment Assessment,
    long WorkoutSessionId = 0);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(OuraRecoveryCache))]
[JsonSerializable(typeof(OuraRecoverySnapshot))]
internal partial class OuraJsonContext : JsonSerializerContext;

internal sealed class OuraRecoveryStore
{
    private readonly string _path;
    internal OuraRecoveryStore(string privateDirectory) =>
        _path = Path.Combine(privateDirectory, "oura-recovery.json");

    internal OuraRecoveryCache Load()
    {
        try
        {
            OuraRecoveryCache result = File.Exists(_path) && new FileInfo(_path).Length <= 1_000_000
                ? JsonSerializer.Deserialize(File.ReadAllText(_path), OuraJsonContext.Default.OuraRecoveryCache) ?? new()
                : new();
            result.Decisions ??= [];
            return result;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    internal void Save(OuraRecoveryCache cache)
    {
        cache.Decisions = cache.Decisions.TakeLast(120).ToList();
        string temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(cache, OuraJsonContext.Default.OuraRecoveryCache));
        File.Move(temporary, _path, true);
    }
}
