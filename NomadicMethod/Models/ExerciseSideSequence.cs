namespace NomadicMethod.Models;

public enum ExerciseSideSequence
{
    Continuous,
    Alternating,
    ScreenLeftThenRight,
    ScreenRightThenLeft,
    ScreenLeftLeadThenRightLead,
    ScreenRightLeadThenLeftLead,
    ShownHandPositionThenOpposite,
}

public static class ExerciseSideSequenceExtensions
{
    public static bool UsesTimedSides(this ExerciseSideSequence sequence) =>
        sequence is ExerciseSideSequence.ScreenLeftThenRight or
            ExerciseSideSequence.ScreenRightThenLeft or
            ExerciseSideSequence.ScreenLeftLeadThenRightLead or
            ExerciseSideSequence.ScreenRightLeadThenLeftLead or
            ExerciseSideSequence.ShownHandPositionThenOpposite;

    public static bool UsesTimedLeadStances(this ExerciseSideSequence sequence) =>
        sequence is ExerciseSideSequence.ScreenLeftLeadThenRightLead or
            ExerciseSideSequence.ScreenRightLeadThenLeftLead;
}
