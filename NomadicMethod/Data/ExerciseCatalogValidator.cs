using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Data;

// Shared by Android startup and domain tests. Reviewed display titles may repeat;
// canonical storage identities, IDs and media retain their uniqueness checks.
public static class ExerciseCatalogValidator
{
    public static void Validate(
        IReadOnlyCollection<Exercise> exercises,
        bool requireInitialScores)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        if (exercises.Select(exercise => exercise.Id).Distinct().Count() != exercises.Count)
            throw new InvalidOperationException("The exercise catalog has duplicate exercise IDs.");
        bool hasUndersizedWallCatalog =
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises)
                .Count > 0;
        bool hasUndersizedSoleWallCatalog =
            WorkoutModifierPolicy
                .FindSoleWallContactRequiredCatalogDeficiencies(exercises)
                .Count > 0;
        bool hasUndersizedLocomotionCatalog =
            WorkoutModifierPolicy.FindLocomotionCatalogDeficiencies(exercises)
                .Count > 0;
        bool hasUndersizedFloorCatalog =
            WorkoutModifierPolicy.FindFloorCatalogDeficiencies(exercises)
                .Count > 0;
        bool hasUndersizedFloorAndWallCatalog =
            WorkoutModifierPolicy.FindFloorAndWallCatalogDeficiencies(exercises)
                .Count > 0;
        bool hasUndersizedFloorAndLocomotionCatalog =
            WorkoutModifierPolicy.FindFloorAndLocomotionCatalogDeficiencies(exercises)
                .Count > 0;
        bool violatesRequirements = exercises.Any(exercise =>
            exercise.Id <= 0 || string.IsNullOrWhiteSpace(exercise.Name) ||
            !Enum.IsDefined(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Distinct().Count() !=
                exercise.SecondaryCanonicalGroups.Length ||
            exercise.SecondaryCanonicalGroups.Contains(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Any(group => !Enum.IsDefined(group)) ||
            !FloorContactPolicy.IsMetadataReviewed(exercise) ||
            !exercise.ShoeAgnostic ||
            !WorkoutModifierPolicy.IsSpaceMetadataValid(exercise) ||
            (exercise.MirrorRelationship ==
                    ExerciseMirrorRelationship.MirrorOnly
                ? exercise.Equipment != "Mirror"
                : exercise.Equipment != "None") ||
            exercise.MuscularDemand < Exercise.MinimumMuscularDemand ||
            exercise.MuscularDemand > Exercise.MaximumMuscularDemand ||
            exercise.ReactionCueLevel is < 0 or > 5 ||
            string.IsNullOrWhiteSpace(exercise.Practice) ||
            string.IsNullOrWhiteSpace(exercise.MotionProfile) ||
            !Enum.IsDefined(exercise.Mode) ||
            !Enum.IsDefined(exercise.Presentation) ||
            !Enum.IsDefined(exercise.SideSequence) ||
            !Enum.IsDefined(exercise.DirectionSequence) ||
            !Enum.IsDefined(exercise.InsectCompatibility) ||
            !Enum.IsDefined(exercise.HardFloorCompatibility) ||
            !Enum.IsDefined(exercise.UpperBodyClothingRequirement) ||
            !Enum.IsDefined(exercise.ShyCompatibility) ||
            !Enum.IsDefined(exercise.MirrorRelationship) ||
            !Enum.IsDefined(exercise.MinimumMirrorCoverage) ||
            (exercise.SoleWallContactRequired && !exercise.WallRequired) ||
            (exercise.DirectionSequence != ExerciseDirectionSequence.None &&
                (exercise.Mode != ExerciseMode.Repetition ||
                    exercise.Presentation != ExercisePresentation.Motion)) ||
            (exercise.Presentation == ExercisePresentation.Still &&
                exercise.Mode != ExerciseMode.Hold) ||
            (exercise.Mode == ExerciseMode.Repetition && exercise.HoldFramePercent != 0) ||
            (exercise.Mode == ExerciseMode.Hold &&
                exercise.HoldFramePercent is <= 0 or > 99));
        bool hasInvalidInitialScore =
            requireInitialScores && exercises.Any(exercise => exercise.Score != 0);
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        bool hasInvalidSessionMovement =
            exercises.Any(exercise => exercise.SessionMovementId < 0) ||
            exercises
                .Where(exercise => exercise.SessionMovementId > 0)
                .GroupBy(exercise => exercise.SessionMovementId)
                .Any(movement =>
                    movement.Count() < 2 ||
                    !exercisesById.TryGetValue(
                        movement.Key,
                        out Exercise? root) ||
                    root.SessionMovementId != root.Id ||
                    movement.Any(exercise =>
                        !exercise.Trains(root.PrimaryCanonicalGroup) &&
                        !root.Trains(exercise.PrimaryCanonicalGroup) &&
                        !exercise.SecondaryCanonicalGroups.Any(root.Trains)));
        var sequenceOwnerByExerciseId = new Dictionary<int, int>();
        bool hasInvalidSequence = false;
        foreach (Exercise root in exercises.Where(exercise =>
                     exercise.SequenceBlocks.Length > 0))
        {
            if (root.SequenceBlocks[0].ExerciseId != root.Id)
            {
                hasInvalidSequence = true;
            }

            foreach (ExerciseSequenceBlock block in root.SequenceBlocks)
            {
                if (!Enum.IsDefined(block.SideCue) ||
                    !Enum.IsDefined(block.DirectionCue) ||
                    !Enum.IsDefined(block.MediaSegment) ||
                    !exercisesById.TryGetValue(
                        block.ExerciseId,
                        out Exercise? member))
                {
                    hasInvalidSequence = true;
                    continue;
                }
                if (sequenceOwnerByExerciseId.TryGetValue(
                        member.Id,
                        out int existingRootId) &&
                    existingRootId != root.Id)
                {
                    hasInvalidSequence = true;
                }
                sequenceOwnerByExerciseId[member.Id] = root.Id;
                if (member.Id != root.Id && member.SequenceBlocks.Length > 0 ||
                    member.WallRequired != root.WallRequired ||
                    member.ElevationRequired != root.ElevationRequired ||
                    member.JumpPlatformRequired != root.JumpPlatformRequired ||
                    member.SoleWallContactRequired !=
                        root.SoleWallContactRequired ||
                    member.UpperBodyClothingRequirement !=
                        root.UpperBodyClothingRequirement ||
                    member.ShyCompatibility != root.ShyCompatibility ||
                    member.LocomotionSpace != root.LocomotionSpace ||
                    block.MediaSegment != ExerciseSequenceMediaSegment.Full &&
                        member.DirectionSequence == ExerciseDirectionSequence.None)
                {
                    hasInvalidSequence = true;
                }
            }
        }
        hasInvalidSequence |= sequenceOwnerByExerciseId.Count != exercises.Count ||
            exercises.Any(exercise =>
                !sequenceOwnerByExerciseId.ContainsKey(exercise.Id));
        bool hasInvalidReplacementMetadata = false;
        if (requireInitialScores)
        {
            HashSet<int> activeReplacementIds = CatalogMigrationRules
                .ReplacedExerciseIds
                .Except(CatalogMigrationRules.PermanentlyRetiredExerciseIds)
                .ToHashSet();
            int[] declaredReplacementIds = exercises
                .Where(exercise => !string.IsNullOrWhiteSpace(exercise.RetiredName))
                .Select(exercise => exercise.Id)
                .Order()
                .ToArray();
            hasInvalidReplacementMetadata =
                !declaredReplacementIds.SequenceEqual(
                    activeReplacementIds.Order()) ||
                exercises.Any(exercise =>
                    activeReplacementIds.Contains(exercise.Id) !=
                        !string.IsNullOrWhiteSpace(exercise.RetiredName) ||
                    (!string.IsNullOrWhiteSpace(exercise.RetiredName) &&
                        string.Equals(
                            exercise.RetiredName,
                            exercise.Name,
                            StringComparison.Ordinal)));
        }

        (string Name, bool Invalid)[] checks =
        [
            ("wall coverage", hasUndersizedWallCatalog),
            ("elevation coverage", WorkoutModifierPolicy.FindElevationCatalogDeficiencies(exercises).Count > 0),
            ("sole-wall coverage", hasUndersizedSoleWallCatalog),
            ("locomotion coverage", hasUndersizedLocomotionCatalog),
            ("floor coverage", hasUndersizedFloorCatalog),
            ("floor/wall coverage", hasUndersizedFloorAndWallCatalog),
            ("floor/locomotion coverage", hasUndersizedFloorAndLocomotionCatalog),
            ("modifier metadata", !WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises)),
            ("identity names", !ExerciseNamePolicy.HasDistinctIdentityNames(exercises)),
            ("duplicate media", exercises.Select(exercise => exercise.Video).Distinct().Count() != exercises.Count),
            ("exercise metadata", violatesRequirements),
            ("initial scores", hasInvalidInitialScore),
            ("session movement identities", hasInvalidSessionMovement),
            ("execution sequences", hasInvalidSequence),
            ("replacement identities", hasInvalidReplacementMetadata),
        ];
        string[] failures = checks.Where(check => check.Invalid).Select(check => check.Name).ToArray();
        if (failures.Length > 0)
            throw new InvalidOperationException("The exercise catalog is invalid: " + string.Join(", ", failures) + ".");
    }

}
