using NomadicMethod.Models;

namespace NomadicMethod.Services;

public readonly record struct MovementPhasePresentation(
    ExerciseSequenceSideCue SideCue,
    ExerciseSequenceDirectionCue DirectionCue,
    bool MirrorMedia);

public static class MovementPhasePresentationPolicy
{
    public static MovementPhasePresentation GetPresentation(
        ExerciseSequenceSideCue sideCue,
        ExerciseSequenceDirectionCue directionCue,
        bool mirrorMedia,
        MovementPhase phase)
    {
        if (phase == MovementPhase.Complete)
        {
            return new MovementPhasePresentation(
                ExerciseSequenceSideCue.None,
                ExerciseSequenceDirectionCue.None,
                MirrorMedia: false);
        }

        return new MovementPhasePresentation(
            sideCue,
            directionCue,
            mirrorMedia);
    }
}
