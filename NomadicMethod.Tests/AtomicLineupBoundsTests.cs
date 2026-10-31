using System.Numerics;
using System.Text.Json;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class AtomicLineupBoundsTests
{
    [Fact]
    public void RemainingBoundsPreserveTheOptimumWithoutEnumeratingImpossiblePlacements()
    {
        using JsonDocument cases = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Assets", "atomic-lineup-bounds.json")));
        foreach (JsonElement entry in cases.RootElement.EnumerateArray())
        {
            AtomicSequenceCandidate[] candidates = entry.GetProperty("candidates").EnumerateArray()
                .Select((candidate, index) => new AtomicSequenceCandidate(
                    candidate.GetProperty("id").GetInt32(),
                    candidate.GetProperty("movement").GetInt32(),
                    candidate.GetProperty("groups").EnumerateArray()
                        .Aggregate(0UL, (mask, group) => mask | (1UL << group.GetInt32())),
                    candidate.GetProperty("blocks").GetInt32(),
                    candidate.GetProperty("utilities").EnumerateArray()
                        .Select(value => new BigInteger(value.GetInt32())).ToArray(), index)).ToArray();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            AtomicSequenceLineup? result = AtomicSequenceLineupSolver.Solve(
                entry.GetProperty("groups").GetInt32(), entry.GetProperty("minutes").GetInt32(),
                candidates, timeout.Token);
            Assert.NotNull(result);
            Assert.Equal(BigInteger.Parse(entry.GetProperty("expectedUtility").GetString()!), result.Utility);
        }
    }
}
