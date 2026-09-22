using System.Text.Json.Serialization;
using NomadicMethod.Models;

namespace NomadicMethod.Data;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(WorkoutState))]
[JsonSerializable(typeof(WorkoutSessionLog))]
[JsonSerializable(typeof(LegacyWorkoutState))]
internal partial class WorkoutJsonContext : JsonSerializerContext;
