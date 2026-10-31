using System.Text.Json;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

// Protocol geometry belongs to the workout that selected it. A catalog repair
// applies to new workouts; reopening an old one must not add sides or sets.
public static class WorkoutProtocolHistory
{
    private sealed record Protocol(int Id, ExerciseSideSequence SideSequence,
        ExerciseDirectionSequence DirectionSequence, ExerciseSequenceBlock[] SequenceBlocks);
    private sealed record Snapshot(int BeforeRevision, Protocol[] Exercises);

    private static readonly Snapshot[] Frozen = Load();

    private static Snapshot[] Load()
    {
        using Stream source = typeof(WorkoutProtocolHistory).Assembly
            .GetManifestResourceStream("NomadicMethod.WorkoutProtocolHistory")
            ?? throw new InvalidOperationException("Workout protocol history is missing.");
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement root = document.RootElement;
        var snapshots = new List<Snapshot> { Parse(root) };
        if (root.TryGetProperty("laterSnapshots", out JsonElement later))
            snapshots.AddRange(later.EnumerateArray().Select(Parse));
        return snapshots.OrderByDescending(snapshot => snapshot.BeforeRevision).ToArray();
    }

    private static Snapshot Parse(JsonElement snapshot) =>
        new(snapshot.GetProperty("beforeRevision").GetInt32(),
            snapshot.GetProperty("exercises").EnumerateArray().Select(exercise => new Protocol(
                exercise.GetProperty("id").GetInt32(),
                Enum.Parse<ExerciseSideSequence>(exercise.GetProperty("sideSequence").GetString()!),
                Enum.Parse<ExerciseDirectionSequence>(exercise.GetProperty("directionSequence").GetString()!),
                exercise.GetProperty("sequenceBlocks").EnumerateArray().Select(block => new ExerciseSequenceBlock
                {
                    ExerciseId = block.GetProperty("exerciseId").GetInt32(),
                    SideCue = Enum.Parse<ExerciseSequenceSideCue>(block.GetProperty("sideCue").GetString()!),
                    DirectionCue = Enum.Parse<ExerciseSequenceDirectionCue>(block.GetProperty("directionCue").GetString()!),
                    MirrorMedia = block.GetProperty("mirrorMedia").GetBoolean(),
                    MediaSegment = Enum.Parse<ExerciseSequenceMediaSegment>(block.GetProperty("mediaSegment").GetString()!),
                }).ToArray())).ToArray());

    public static IReadOnlyList<Exercise> ForWorkout(IReadOnlyList<Exercise> catalog, int revision)
    {
        if (revision <= 0) return catalog;
        // Rewind newest corrections first, then the older snapshots. A later
        // repair of the same exercise must never overwrite an earlier protocol.
        foreach (Snapshot snapshot in Frozen.Where(s => revision < s.BeforeRevision))
        {
            catalog = catalog.Select(exercise =>
            {
                Protocol? protocol = snapshot.Exercises.FirstOrDefault(p => p.Id == exercise.Id);
                return protocol is null ? exercise : exercise.WithWorkoutMetadata(
                    protocol.SideSequence, protocol.SequenceBlocks.ToArray(),
                    exercise.ShyCompatibility, directions: protocol.DirectionSequence);
            }).ToArray();
        }
        return catalog;
    }
}
