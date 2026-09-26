using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed partial class ExerciseSessionService
{
    // A short duration is a block budget, not a requirement that every block
    // own a different narrow target. Try the existing, complete anatomical
    // partitions when the finest plan would bring back rejected movements.
    // This runs only during new-workout preparation, never during restoration.
    private void PrepareAdaptiveLineup(WorkoutState state)
    {
        int finestResolution = Math.Min(state.ActiveWorkoutMinutes, 30);
        WorkoutState best = BuildPreparationPlan(state, finestResolution);
        PreparationBurden bestBurden = GetPreparationBurden(best);
        if (state.ActiveWorkoutMinutes <= 30 && bestBurden.RejectedBlocks > 0)
        {
            foreach (int resolution in MassGroupingTaxonomy.SupportedMinutes
                         .Where(minutes => minutes < finestResolution)
                         .OrderDescending())
            {
                WorkoutState candidate;
                try
                {
                    candidate = BuildPreparationPlan(state, resolution);
                }
                catch (InvalidOperationException)
                {
                    // A broader bucket can be harder to cover with the current
                    // equipment/catalog. It never licenses dropping that bucket.
                    continue;
                }

                PreparationBurden burden = GetPreparationBurden(candidate);
                if (burden.LightBlocks < bestBurden.LightBlocks ||
                    burden.RejectedBlocks > bestBurden.RejectedBlocks ||
                    (burden.RejectedBlocks == bestBurden.RejectedBlocks &&
                     burden.RejectionDepth >= bestBurden.RejectionDepth))
                {
                    continue;
                }

                best = candidate;
                bestBurden = burden;
                if (burden.RejectedBlocks == 0)
                {
                    break;
                }
            }
        }

        state.SelectedExerciseIds = best.SelectedExerciseIds;
        state.KeptExerciseRootIdsBySelectionGroupId = best.KeptExerciseRootIdsBySelectionGroupId;
        state.LastKeptExerciseIds = best.LastKeptExerciseIds;
        state.ActiveDurationSelectionGroupIds = best.ActiveDurationSelectionGroupIds;
        state.ActiveSetCountsBySelectionGroupId = best.ActiveSetCountsBySelectionGroupId;
        state.ActiveExtraSetSelectionGroupIds = best.ActiveExtraSetSelectionGroupIds;
        state.ActiveSelectionGroupOrder = best.ActiveSelectionGroupOrder;
    }

    private WorkoutState BuildPreparationPlan(WorkoutState source, int resolution)
    {
        // Candidate evaluation must not leak carried Keeps, cached selections,
        // allocations or feedback into the user's state when that plan loses.
        var plan = new WorkoutState
        {
            ActiveWorkoutMinutes = source.ActiveWorkoutMinutes,
            ActiveWorkoutModifiers = source.ActiveWorkoutModifiers,
            ActiveWorkoutIsLightDay = source.ActiveWorkoutIsLightDay,
            ActiveDurationSelectionGroupIds = resolution == Math.Min(source.ActiveWorkoutMinutes, 30)
                ? null : MassGroupingTaxonomy.GetResolution(resolution).Groups.Select(g => g.Id).ToList(),
            SelectedExerciseIds = new(source.SelectedExerciseIds, StringComparer.Ordinal),
            LastKeptExerciseIds = [.. source.LastKeptExerciseIds],
            KeptExerciseRootIdsBySelectionGroupId = source.KeptExerciseRootIdsBySelectionGroupId
                .ToDictionary(e => e.Key, e => new HashSet<int>(e.Value), StringComparer.Ordinal),
            ExerciseScoreAdjustmentsByPhase = source.ExerciseScoreAdjustmentsByPhase
                .ToDictionary(e => e.Key, e => new Dictionary<int, int>(e.Value)),
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new(source.LastHardWorkUnixMillisecondsByPrimaryMuscle, StringComparer.Ordinal),
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
                new(source.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle, StringComparer.Ordinal),
        };
        CarrySlotPreferencesForward(plan);
        RepairActiveLineup(plan, preserveCurrentSelections:
            !plan.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light));
        RebalanceNewExercisesByMuscleBalance(plan);
        SetActiveLongWorkoutAllocation(plan);
        ReconcileLineupWithScheduledPhases(plan);
        return plan;
    }

    private PreparationBurden GetPreparationBurden(WorkoutState plan)
    {
        int rejectedBlocks = 0;
        long rejectionDepth = 0;
        int lightBlocks = 0;
        foreach (var selection in GetActiveGroups(plan).GroupBy(round => round.SelectionKey))
        {
            WorkoutGroup final = selection.MaxBy(round => round.Order)!;
            Exercise root = GetSequenceRoot(GetSelectedExercise(plan, final));
            int blocks = selection.Count();
            int score = GetSelectionScore(plan, root, GetExercisePhase(final));
            if (score < 0)
            {
                rejectedBlocks += blocks;
                rejectionDepth -= (long)score * blocks;
            }
            if (plan.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light) &&
                IsDemandZeroSequence(root))
            {
                lightBlocks += blocks;
            }
        }
        return new(rejectedBlocks, rejectionDepth, lightBlocks);
    }

    private readonly record struct PreparationBurden(int RejectedBlocks, long RejectionDepth, int LightBlocks);
}
