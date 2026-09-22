namespace NomadicMethod.Models;

public enum ExerciseSequenceSideCue
{
    None,
    ScreenLeft,
    ScreenRight,
    ShownLeadStance,
    OppositeLeadStance,
}

public enum ExerciseSequenceDirectionCue
{
    None,
    Forward,
    Backward,
    Clockwise,
    Counterclockwise,
    Inward,
    Outward,
}

public enum ExerciseSequenceMediaSegment
{
    Full,
    FirstDirection,
    SecondDirection,
}

public sealed record ExerciseSequenceBlock
{
    public required int ExerciseId { get; init; }

    public ExerciseSequenceSideCue SideCue { get; init; } =
        ExerciseSequenceSideCue.None;

    public ExerciseSequenceDirectionCue DirectionCue { get; init; } =
        ExerciseSequenceDirectionCue.None;

    public required bool MirrorMedia { get; init; }

    public ExerciseSequenceMediaSegment MediaSegment { get; init; } =
        ExerciseSequenceMediaSegment.Full;
}
