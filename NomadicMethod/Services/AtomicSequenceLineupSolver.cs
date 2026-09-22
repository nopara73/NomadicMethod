using System.Numerics;

namespace NomadicMethod.Services;

internal sealed record AtomicSequenceCandidate(
    int ExerciseId,
    int MovementId,
    ulong CoverageMask,
    int BlockCount,
    BigInteger[] UtilitiesByGroup,
    int TieOrder)
{
    public BigInteger Utility => UtilitiesByGroup
        .Where((_, groupIndex) =>
            (CoverageMask & (1UL << groupIndex)) != 0)
        .Aggregate(BigInteger.Zero, (sum, value) => sum + value);
}

internal sealed record AtomicSequenceLineup(
    IReadOnlyDictionary<int, int> ExerciseIdByGroupIndex,
    BigInteger Utility);

internal static class AtomicSequenceLineupSolver
{
    public static AtomicSequenceLineup? SolveAllowingRepeatedMovements(
        int groupCount,
        int workoutMinutes,
        IReadOnlyList<AtomicSequenceCandidate> sourceCandidates)
    {
        // Separate complete placements may repeat a movement. Only the solver's
        // occupancy keys change: exercise identities, blocks and utilities stay
        // intact, and aliases still compete for the same placement.
        var occupancyKeys = new Dictionary<(int MovementId, ulong CoverageMask), int>();
        AtomicSequenceCandidate[] repeatableCandidates = sourceCandidates
            .Select(candidate =>
            {
                var key = (candidate.MovementId, candidate.CoverageMask);
                if (!occupancyKeys.TryGetValue(key, out int occupancyKey))
                {
                    occupancyKey = occupancyKeys.Count;
                    occupancyKeys[key] = occupancyKey;
                }
                return candidate with { MovementId = occupancyKey };
            })
            .ToArray();
        return Solve(groupCount, workoutMinutes, repeatableCandidates);
    }

    public static AtomicSequenceLineup? Solve(
        int groupCount,
        int workoutMinutes,
        IReadOnlyList<AtomicSequenceCandidate> sourceCandidates)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(groupCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(groupCount, 63);
        ArgumentOutOfRangeException.ThrowIfLessThan(workoutMinutes, groupCount);
        ArgumentNullException.ThrowIfNull(sourceCandidates);
        ulong allGroupsMask = (1UL << groupCount) - 1UL;
        AtomicSequenceCandidate[] candidates = sourceCandidates
            .Where(candidate =>
                candidate.CoverageMask != 0 &&
                (candidate.CoverageMask & ~allGroupsMask) == 0 &&
                candidate.BlockCount > 0 &&
                candidate.UtilitiesByGroup.Length == groupCount)
            .ToArray();

        AtomicSequenceCandidate[][] singletonCandidatesByGroup =
            Enumerable.Range(0, groupCount)
                .Select(groupIndex => candidates
                    .Where(candidate =>
                        candidate.CoverageMask == (1UL << groupIndex))
                    .ToArray())
                .ToArray();
        AtomicSequenceCandidate[] multiGroupCandidates = candidates
            .Where(candidate => BitOperations.PopCount(candidate.CoverageMask) > 1)
            .OrderByDescending(candidate => candidate.Utility)
            .ThenBy(candidate => candidate.BlockCount)
            .ToArray();

        if (multiGroupCandidates.Length == 0)
        {
            return CompleteWithSingletons(
                groupCount,
                workoutMinutes,
                allGroupsMask,
                [],
                new HashSet<int>(),
                singletonCandidatesByGroup);
        }

        BigInteger[] maximumUtilityByGroup = Enumerable.Range(0, groupCount)
            .Select(groupIndex => candidates
                .Where(candidate =>
                    (candidate.CoverageMask & (1UL << groupIndex)) != 0)
                .Select(candidate => candidate.UtilitiesByGroup[groupIndex])
                .DefaultIfEmpty(BigInteger.Zero)
                .Max())
            .ToArray();

        // The singleton matcher is polynomial and usually reaches the exact
        // per-group utility ceiling by itself. Seed branch-and-bound with that
        // complete lineup so multi-group placements are explored only when
        // they can genuinely improve the result.
        AtomicSequenceLineup? best = CompleteWithSingletons(
            groupCount,
            workoutMinutes,
            allGroupsMask,
            [],
            new HashSet<int>(),
            singletonCandidatesByGroup);
        var selected = new List<AtomicSequenceCandidate>();
        var selectedMovementIds = new HashSet<int>();

        void Search(ulong decidedMask, ulong coveredMask, BigInteger utility)
        {
            BigInteger upperBound = utility;
            for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                if ((coveredMask & (1UL << groupIndex)) == 0)
                {
                    upperBound += maximumUtilityByGroup[groupIndex];
                }
            }
            if (best is not null && upperBound <= best.Utility)
            {
                return;
            }

            if (decidedMask == allGroupsMask)
            {
                AtomicSequenceLineup? completed = CompleteWithSingletons(
                    groupCount,
                    workoutMinutes,
                    allGroupsMask & ~coveredMask,
                    selected,
                    selectedMovementIds,
                    singletonCandidatesByGroup);
                if (completed is not null &&
                    (best is null || completed.Utility > best.Utility))
                {
                    best = completed;
                }
                return;
            }

            int nextGroupIndex = Enumerable.Range(0, groupCount)
                .Where(groupIndex =>
                    (decidedMask & (1UL << groupIndex)) == 0)
                .OrderBy(groupIndex => multiGroupCandidates.Count(candidate =>
                    (candidate.CoverageMask & (1UL << groupIndex)) != 0 &&
                    (candidate.CoverageMask & decidedMask) == 0 &&
                    !selectedMovementIds.Contains(candidate.MovementId)))
                .First();
            ulong nextGroupMask = 1UL << nextGroupIndex;

            foreach (AtomicSequenceCandidate candidate in multiGroupCandidates)
            {
                if ((candidate.CoverageMask & nextGroupMask) == 0 ||
                    (candidate.CoverageMask & decidedMask) != 0 ||
                    !selectedMovementIds.Add(candidate.MovementId))
                {
                    continue;
                }

                selected.Add(candidate);
                Search(
                    decidedMask | candidate.CoverageMask,
                    coveredMask | candidate.CoverageMask,
                    utility + candidate.Utility);
                selected.RemoveAt(selected.Count - 1);
                selectedMovementIds.Remove(candidate.MovementId);
            }

            // Explore high-utility atomic placements before the singleton-only
            // branch. That establishes a strong exact incumbent early, so the
            // lexicographic upper bound can prune equivalent catalog choices
            // instead of enumerating them combinatorially.
            Search(decidedMask | nextGroupMask, coveredMask, utility);
        }

        Search(0, 0, BigInteger.Zero);
        return best;
    }

    private static AtomicSequenceLineup? CompleteWithSingletons(
        int groupCount,
        int workoutMinutes,
        ulong singletonGroupsMask,
        IReadOnlyList<AtomicSequenceCandidate> fixedCandidates,
        IReadOnlySet<int> fixedMovementIds,
        IReadOnlyList<AtomicSequenceCandidate[]> singletonCandidatesByGroup)
    {
        int[] singletonGroupIndexes = Enumerable.Range(0, groupCount)
            .Where(groupIndex =>
                (singletonGroupsMask & (1UL << groupIndex)) != 0)
            .ToArray();
        var selectedByGroupIndex = new Dictionary<int, AtomicSequenceCandidate>();
        int fixedBlockCount = fixedCandidates.Sum(candidate => candidate.BlockCount);
        int availableSingletonBlocks = workoutMinutes - fixedBlockCount;
        if (availableSingletonBlocks < singletonGroupIndexes.Length)
        {
            return null;
        }
        bool everySingletonMustUseOneBlock =
            availableSingletonBlocks == singletonGroupIndexes.Length;

        if (singletonGroupIndexes.Length > 0)
        {
            int[] movementIds = singletonGroupIndexes
                .SelectMany(groupIndex => singletonCandidatesByGroup[groupIndex])
                .Where(candidate => !everySingletonMustUseOneBlock ||
                    candidate.BlockCount == 1)
                .Where(candidate => !fixedMovementIds.Contains(candidate.MovementId))
                .GroupBy(candidate => candidate.MovementId)
                .OrderBy(movement => movement.Min(candidate => candidate.TieOrder))
                .Select(movement => movement.Key)
                .ToArray();
            if (movementIds.Length < singletonGroupIndexes.Length)
            {
                return null;
            }

            Dictionary<int, int> movementIndexes = movementIds
                .Select((movementId, movementIndex) => (movementId, movementIndex))
                .ToDictionary(entry => entry.movementId, entry => entry.movementIndex);
            var allowed = new bool[singletonGroupIndexes.Length, movementIds.Length];
            var utilities = new BigInteger[
                singletonGroupIndexes.Length,
                movementIds.Length];
            var chosenCandidates = new AtomicSequenceCandidate?[
                singletonGroupIndexes.Length,
                movementIds.Length];
            BigInteger maximumUtility = BigInteger.Zero;

            for (int row = 0; row < singletonGroupIndexes.Length; row++)
            {
                int groupIndex = singletonGroupIndexes[row];
                foreach (AtomicSequenceCandidate candidate in
                         singletonCandidatesByGroup[groupIndex])
                {
                    if (fixedMovementIds.Contains(candidate.MovementId) ||
                        everySingletonMustUseOneBlock && candidate.BlockCount != 1)
                    {
                        continue;
                    }
                    int column = movementIndexes[candidate.MovementId];
                    BigInteger utility = candidate.UtilitiesByGroup[groupIndex];
                    if (!allowed[row, column] || utility > utilities[row, column])
                    {
                        allowed[row, column] = true;
                        utilities[row, column] = utility;
                        chosenCandidates[row, column] = candidate;
                        maximumUtility = BigInteger.Max(maximumUtility, utility);
                    }
                }
            }

            int[] assignment = SolveMaximumWeightAssignment(
                utilities,
                allowed,
                maximumUtility);
            for (int row = 0; row < singletonGroupIndexes.Length; row++)
            {
                int column = assignment[row];
                if (column < 0 || !allowed[row, column] ||
                    chosenCandidates[row, column] is not AtomicSequenceCandidate chosen)
                {
                    return null;
                }
                selectedByGroupIndex[singletonGroupIndexes[row]] = chosen;
            }
        }

        if (!ReduceToBlockCapacity(
                workoutMinutes,
                fixedCandidates,
                selectedByGroupIndex,
                singletonCandidatesByGroup))
        {
            return null;
        }

        var exerciseIdByGroupIndex = new Dictionary<int, int>();
        BigInteger utilityTotal = BigInteger.Zero;
        foreach (AtomicSequenceCandidate candidate in fixedCandidates)
        {
            utilityTotal += candidate.Utility;
            for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                if ((candidate.CoverageMask & (1UL << groupIndex)) != 0)
                {
                    exerciseIdByGroupIndex[groupIndex] = candidate.ExerciseId;
                }
            }
        }
        foreach ((int groupIndex, AtomicSequenceCandidate candidate) in
                 selectedByGroupIndex)
        {
            exerciseIdByGroupIndex[groupIndex] = candidate.ExerciseId;
            utilityTotal += candidate.UtilitiesByGroup[groupIndex];
        }

        return exerciseIdByGroupIndex.Count == groupCount
            ? new AtomicSequenceLineup(exerciseIdByGroupIndex, utilityTotal)
            : null;
    }

    private static bool ReduceToBlockCapacity(
        int workoutMinutes,
        IReadOnlyList<AtomicSequenceCandidate> fixedCandidates,
        IDictionary<int, AtomicSequenceCandidate> selectedByGroupIndex,
        IReadOnlyList<AtomicSequenceCandidate[]> singletonCandidatesByGroup)
    {
        bool CanAllocate()
        {
            AtomicSequenceCandidate[] placements = fixedCandidates
                .Concat(selectedByGroupIndex.Values)
                .ToArray();
            int baseBlockCount = placements.Sum(candidate => candidate.BlockCount);
            if (baseBlockCount > workoutMinutes)
            {
                return false;
            }
            int remainingBlocks = workoutMinutes - baseBlockCount;
            if (remainingBlocks == 0)
            {
                return true;
            }
            int[] costs = placements
                .Select(candidate => candidate.BlockCount)
                .Distinct()
                .ToArray();
            var fillable = new bool[remainingBlocks + 1];
            fillable[0] = true;
            for (int value = 1; value <= remainingBlocks; value++)
            {
                fillable[value] = costs.Any(cost =>
                    cost <= value && fillable[value - cost]);
            }
            return fillable[remainingBlocks];
        }

        while (!CanAllocate())
        {
            HashSet<int> usedMovementIds = fixedCandidates
                .Concat(selectedByGroupIndex.Values)
                .Select(candidate => candidate.MovementId)
                .ToHashSet();
            (int GroupIndex, AtomicSequenceCandidate Candidate,
                BigInteger UtilityLoss, int SavedBlocks)? best = null;
            foreach ((int groupIndex, AtomicSequenceCandidate current) in
                     selectedByGroupIndex)
            {
                foreach (AtomicSequenceCandidate alternative in
                         singletonCandidatesByGroup[groupIndex])
                {
                    int savedBlocks = current.BlockCount - alternative.BlockCount;
                    if (savedBlocks <= 0 ||
                        alternative.MovementId != current.MovementId &&
                        usedMovementIds.Contains(alternative.MovementId))
                    {
                        continue;
                    }
                    BigInteger utilityLoss =
                        current.UtilitiesByGroup[groupIndex] -
                        alternative.UtilitiesByGroup[groupIndex];
                    if (best is null ||
                        utilityLoss < best.Value.UtilityLoss ||
                        utilityLoss == best.Value.UtilityLoss &&
                            savedBlocks < best.Value.SavedBlocks)
                    {
                        best = (groupIndex, alternative, utilityLoss, savedBlocks);
                    }
                }
            }

            if (best is null)
            {
                return false;
            }
            selectedByGroupIndex[best.Value.GroupIndex] = best.Value.Candidate;
        }

        return true;
    }

    private static int[] SolveMaximumWeightAssignment(
        BigInteger[,] utilities,
        bool[,] allowed,
        BigInteger maximumUtility)
    {
        int rowCount = utilities.GetLength(0);
        int columnCount = utilities.GetLength(1);
        if (columnCount < rowCount)
        {
            return Enumerable.Repeat(-1, rowCount).ToArray();
        }

        BigInteger invalidCost =
            (maximumUtility + BigInteger.One) * (rowCount + 1);
        var costs = new BigInteger[rowCount, columnCount];
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                costs[row, column] = allowed[row, column]
                    ? maximumUtility - utilities[row, column]
                    : invalidCost;
            }
        }

        var rowPotential = new BigInteger[rowCount + 1];
        var columnPotential = new BigInteger[columnCount + 1];
        var matchedRowByColumn = new int[columnCount + 1];
        var previousColumn = new int[columnCount + 1];
        for (int row = 1; row <= rowCount; row++)
        {
            matchedRowByColumn[0] = row;
            int column = 0;
            var minimumReducedCost = new BigInteger?[columnCount + 1];
            var visitedColumns = new bool[columnCount + 1];
            do
            {
                visitedColumns[column] = true;
                int currentRow = matchedRowByColumn[column];
                BigInteger? delta = null;
                int nextColumn = 0;
                for (int candidateColumn = 1;
                     candidateColumn <= columnCount;
                     candidateColumn++)
                {
                    if (visitedColumns[candidateColumn])
                    {
                        continue;
                    }
                    BigInteger reducedCost =
                        costs[currentRow - 1, candidateColumn - 1] -
                        rowPotential[currentRow] -
                        columnPotential[candidateColumn];
                    if (minimumReducedCost[candidateColumn] is null ||
                        reducedCost < minimumReducedCost[candidateColumn]!.Value)
                    {
                        minimumReducedCost[candidateColumn] = reducedCost;
                        previousColumn[candidateColumn] = column;
                    }
                    if (delta is null ||
                        minimumReducedCost[candidateColumn]!.Value < delta.Value)
                    {
                        delta = minimumReducedCost[candidateColumn];
                        nextColumn = candidateColumn;
                    }
                }
                if (delta is null)
                {
                    return Enumerable.Repeat(-1, rowCount).ToArray();
                }
                for (int candidateColumn = 0;
                     candidateColumn <= columnCount;
                     candidateColumn++)
                {
                    if (visitedColumns[candidateColumn])
                    {
                        rowPotential[matchedRowByColumn[candidateColumn]] +=
                            delta.Value;
                        columnPotential[candidateColumn] -= delta.Value;
                    }
                    else if (minimumReducedCost[candidateColumn] is not null)
                    {
                        minimumReducedCost[candidateColumn] =
                            minimumReducedCost[candidateColumn]!.Value - delta.Value;
                    }
                }
                column = nextColumn;
            }
            while (matchedRowByColumn[column] != 0);

            do
            {
                int priorColumn = previousColumn[column];
                matchedRowByColumn[column] = matchedRowByColumn[priorColumn];
                column = priorColumn;
            }
            while (column != 0);
        }

        var assignment = Enumerable.Repeat(-1, rowCount).ToArray();
        for (int column = 1; column <= columnCount; column++)
        {
            int row = matchedRowByColumn[column];
            if (row != 0)
            {
                assignment[row - 1] = column - 1;
            }
        }
        return assignment;
    }
}
