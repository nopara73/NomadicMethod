using System.Text.Json.Serialization;
using NomadicMethod.Models;

namespace NomadicMethod.Data;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Exercise[]))]
internal partial class ExerciseCatalogJsonContext : JsonSerializerContext;
