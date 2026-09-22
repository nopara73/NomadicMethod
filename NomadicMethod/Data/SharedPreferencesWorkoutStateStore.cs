using System.Text.Json;
using Android.Content;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Data;

public sealed class SharedPreferencesWorkoutStateStore : IWorkoutStateStore
{
    private const string PreferencesName = "flux_workout_state";
    private const string StateKey = "state";

    private readonly ISharedPreferences _preferences;

    public SharedPreferencesWorkoutStateStore(Context context)
    {
        _preferences = context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Unable to open workout preferences.");
    }

    public WorkoutState Load()
    {
        string? json = _preferences.GetString(StateKey, null);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new WorkoutState();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new WorkoutState();
            }

            int version = 4;
            if (document.RootElement.TryGetProperty(
                    "version",
                    out JsonElement versionElement) &&
                (versionElement.ValueKind != JsonValueKind.Number ||
                 !versionElement.TryGetInt32(out version)))
            {
                return new WorkoutState();
            }

            if (version >= 5)
            {
                return JsonSerializer.Deserialize(
                        json,
                        WorkoutJsonContext.Default.WorkoutState)
                    ?? new WorkoutState();
            }

            LegacyWorkoutState legacy = JsonSerializer.Deserialize(
                    json,
                    WorkoutJsonContext.Default.LegacyWorkoutState)
                ?? new LegacyWorkoutState();
            return LegacyWorkoutStateMigration.Migrate(legacy);
        }
        catch (JsonException)
        {
            return new WorkoutState();
        }
    }

    public void Save(WorkoutState state)
    {
        ISharedPreferencesEditor editor = CreateEditor(state);

        if (!editor.Commit())
        {
            throw new InvalidOperationException("Unable to save workout progress.");
        }
    }

    public void SaveDeferred(WorkoutState state)
    {
        // Movement start/resume checkpoints are followed by a synchronous save
        // whenever the activity pauses. Apply updates the in-process preference
        // immediately while keeping the tap-to-frame path free of a disk fsync.
        CreateEditor(state).Apply();
    }

    private ISharedPreferencesEditor CreateEditor(WorkoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string json = JsonSerializer.Serialize(
            state,
            WorkoutJsonContext.Default.WorkoutState);
        ISharedPreferencesEditor editor = _preferences.Edit()
            ?? throw new InvalidOperationException("Unable to edit workout preferences.");
        editor.PutString(StateKey, json);
        return editor;
    }
}
