namespace NomadicMethod.Models;

public sealed record WorkoutGroup(
    string Id,
    string DisplayName,
    int Order,
    IReadOnlySet<CanonicalMuscleGroup> CanonicalGroups,
    string? SelectionGroupId = null,
    int ExerciseOverrideId = 0,
    int SequenceBlockIndex = 0,
    int SequenceBlockCount = 1,
    int SetNumber = 1,
    int SetCount = 1,
    ExerciseSequenceSideCue SequenceSideCue = ExerciseSequenceSideCue.None,
    ExerciseSequenceDirectionCue SequenceDirectionCue =
        ExerciseSequenceDirectionCue.None,
    bool MirrorSequenceMedia = false,
    ExerciseSequenceMediaSegment SequenceMediaSegment =
        ExerciseSequenceMediaSegment.Full)
{
    public string SelectionKey => SelectionGroupId ?? Id;

    public bool IsSequenceRound => SequenceBlockCount > 1;

    public bool HasRepeatedSets => SetCount > 1;

    public bool IsFinalSequenceRound =>
        SequenceBlockIndex == SequenceBlockCount - 1 && SetNumber == SetCount;
}

public sealed record WorkoutResolution(
    int Minutes,
    IReadOnlyList<WorkoutGroup> Groups);
