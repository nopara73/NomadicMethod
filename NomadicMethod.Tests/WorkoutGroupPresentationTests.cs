using NomadicMethod.Models;

namespace NomadicMethod.Tests;

public sealed class WorkoutGroupPresentationTests
{
    [Theory]
    [InlineData(1, 1, false, false)]
    [InlineData(2, 1, true, false)]
    [InlineData(1, 2, false, true)]
    [InlineData(3, 2, true, true)]
    public void Exercise_sequences_and_repeated_sets_are_distinct(
        int sequenceBlockCount,
        int setCount,
        bool expectedSequence,
        bool expectedRepeatedSets)
    {
        var group = new WorkoutGroup(
            "test",
            "Test",
            1,
            new HashSet<CanonicalMuscleGroup> { CanonicalMuscleGroup.Chest },
            SequenceBlockCount: sequenceBlockCount,
            SetCount: setCount);

        Assert.Equal(expectedSequence, group.IsSequenceRound);
        Assert.Equal(expectedRepeatedSets, group.HasRepeatedSets);
    }
}
