using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class MovementPhasePresentationPolicyTests
{
    [Fact]
    public void Side_and_direction_cues_remain_independent()
    {
        MovementPhasePresentation presentation =
            MovementPhasePresentationPolicy.GetPresentation(
                ExerciseSequenceSideCue.ScreenLeft,
                ExerciseSequenceDirectionCue.Inward,
                mirrorMedia: true,
                MovementPhase.Continuous);

        Assert.Equal(ExerciseSequenceSideCue.ScreenLeft, presentation.SideCue);
        Assert.Equal(
            ExerciseSequenceDirectionCue.Inward,
            presentation.DirectionCue);
        Assert.True(presentation.MirrorMedia);
    }

    [Fact]
    public void Complete_phase_has_no_cues_or_mirroring()
    {
        MovementPhasePresentation presentation =
            MovementPhasePresentationPolicy.GetPresentation(
                ExerciseSequenceSideCue.ScreenRight,
                ExerciseSequenceDirectionCue.Backward,
                mirrorMedia: true,
                MovementPhase.Complete);

        Assert.Equal(ExerciseSequenceSideCue.None, presentation.SideCue);
        Assert.Equal(
            ExerciseSequenceDirectionCue.None,
            presentation.DirectionCue);
        Assert.False(presentation.MirrorMedia);
    }
}
