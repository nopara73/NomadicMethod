namespace NomadicMethod.Models;

public sealed class Exercise
{
    private ExerciseSideSequence _sideSequence;
    private ExerciseSequenceBlock[] _sequenceBlocks = [];
    private ExerciseShyCompatibility _shyCompatibility = ExerciseShyCompatibility.Unreviewed;
    private CanonicalMuscleGroup[] _secondaryCanonicalGroups = [];
    public const int MinimumMuscularDemand = 0;

    public const int ModerateMuscularDemand = 1;

    public const int MaximumMuscularDemand = 2;

    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? RetiredName { get; init; }

    public required string Video { get; init; }

    public required CanonicalMuscleGroup PrimaryCanonicalGroup { get; init; }

    public required CanonicalMuscleGroup[] SecondaryCanonicalGroups
        { get => _secondaryCanonicalGroups; init => _secondaryCanonicalGroups = value; }

    public required string Practice { get; init; }

    public required string MotionProfile { get; init; }

    public required ExerciseMode Mode { get; init; }

    public required ExercisePresentation Presentation { get; init; }

    public required int HoldFramePercent { get; init; }

    public required ExerciseSideSequence SideSequence { get => _sideSequence; init => _sideSequence = value; }

    public ExerciseDirectionSequence DirectionSequence { get; init; } =
        ExerciseDirectionSequence.None;

    public ExerciseSequenceBlock[] SequenceBlocks { get => _sequenceBlocks; init => _sequenceBlocks = value; }

    public int SessionMovementId { get; init; }

    public ExerciseInsectCompatibility InsectCompatibility { get; init; } =
        ExerciseInsectCompatibility.Unreviewed;

    public ExerciseHardFloorCompatibility HardFloorCompatibility { get; init; } =
        ExerciseHardFloorCompatibility.Unreviewed;

    public ExerciseUpperBodyClothingRequirement UpperBodyClothingRequirement
        { get; init; } = ExerciseUpperBodyClothingRequirement.Unreviewed;

    public ExerciseShyCompatibility ShyCompatibility { get => _shyCompatibility; init => _shyCompatibility = value; }

    public ExerciseMirrorRelationship MirrorRelationship { get; init; } =
        ExerciseMirrorRelationship.Unreviewed;

    public ExerciseMirrorCoverage MinimumMirrorCoverage { get; init; } =
        ExerciseMirrorCoverage.None;

    public bool WallRequired { get; init; }

    public bool SoleWallContactRequired { get; init; }

    public int MuscularDemand { get; init; }

    public int Score { get; set; } = 0;

    public required bool OnlyFeetTouchGround { get; init; }

    // Historical exercises admitted before floor support were all feet-only.
    // A non-feet-only exercise without explicit contact metadata fails closed.
    public ExerciseFloorContact FloorContactPoints { get; init; } =
        ExerciseFloorContact.Feet;

    public required bool ShoeAgnostic { get; init; }

    public required int MaxSpaceMeters { get; init; }

    public LocomotionSpace LocomotionSpace { get; init; } = LocomotionSpace.None;

    public required string Equipment { get; init; }

    public required bool Silent { get; init; }

    public Exercise WithWorkoutMetadata(ExerciseSideSequence sides,
        ExerciseSequenceBlock[] blocks, ExerciseShyCompatibility shy,
        CanonicalMuscleGroup[]? secondaryGroups = null)
    {
        Exercise copy = (Exercise)MemberwiseClone();
        copy._sideSequence = sides;
        copy._sequenceBlocks = blocks;
        copy._shyCompatibility = shy;
        copy._secondaryCanonicalGroups = secondaryGroups ?? _secondaryCanonicalGroups;
        return copy;
    }

    public string GetVideoAssetPath(ExerciseSequenceMediaSegment segment) =>
        segment == ExerciseSequenceMediaSegment.SecondDirection
            ? $"exercise_direction_videos/exercise_{Id:D4}.mp4"
            : Video;

    public bool Trains(CanonicalMuscleGroup group)
    {
        return PrimaryCanonicalGroup == group ||
            SecondaryCanonicalGroups.Contains(group);
    }
}
