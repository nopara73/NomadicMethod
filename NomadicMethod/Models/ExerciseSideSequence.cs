namespace NomadicMethod.Models;

public enum ExerciseSideSequence
{
    Continuous,
    Alternating,
    ScreenLeftThenRight,
    ScreenRightThenLeft,
    ScreenLeftLeadThenRightLead,
    ScreenRightLeadThenLeftLead,
}

public static class ExerciseSideSequenceExtensions
{
    public static bool UsesTimedSides(this ExerciseSideSequence sequence) =>
        sequence is ExerciseSideSequence.ScreenLeftThenRight or
            ExerciseSideSequence.ScreenRightThenLeft or
            ExerciseSideSequence.ScreenLeftLeadThenRightLead or
            ExerciseSideSequence.ScreenRightLeadThenLeftLead;

    public static bool UsesTimedLeadStances(this ExerciseSideSequence sequence) =>
        sequence is ExerciseSideSequence.ScreenLeftLeadThenRightLead or
            ExerciseSideSequence.ScreenRightLeadThenLeftLead;
}
