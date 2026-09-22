using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutModifierPolicyTests
{
    [Fact]
    public void UpperBodyClothingStatesExcludeOnlyTheOppositeRequirement()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise clothingRequired = Exercise(
            1,
            group,
            upperBodyClothingRequirement:
                ExerciseUpperBodyClothingRequirement.ClothingRequired);
        Exercise bareRequired = Exercise(
            2,
            group,
            upperBodyClothingRequirement:
                ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired);
        Exercise agnostic = Exercise(3, group);

        Assert.True(WorkoutModifierPolicy.IsCompatible(
            clothingRequired,
            WorkoutModifiers.UpperBodyClothing));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            clothingRequired,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            bareRequired,
            WorkoutModifiers.UpperBodyClothing));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            bareRequired,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            agnostic,
            WorkoutModifiers.UpperBodyClothing));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            agnostic,
            WorkoutModifiers.None));
    }

    [Fact]
    public void NeutralProfileKeepsBothCompatibleAndExcludedExercisesEligible()
    {
        Exercise compatible = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Compatible);
        Exercise excluded = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Incompatible);

        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            excluded,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.Insect));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            excluded,
            WorkoutModifiers.Insect));
    }

    [Fact]
    public void HardFloorFiltersOnlyReviewedIncompatibleExercises()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise compatible = Exercise(
            1,
            group,
            hardFloorCompatibility: ExerciseHardFloorCompatibility.Compatible);
        Exercise incompatible = Exercise(
            2,
            group,
            hardFloorCompatibility: ExerciseHardFloorCompatibility.Incompatible);

        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            incompatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.HardFloor));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            incompatible,
            WorkoutModifiers.HardFloor));
    }

    [Fact]
    public void ShyFiltersOnlyReviewedIncompatibleExercises()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise compatible = Exercise(
            1,
            group,
            shyCompatibility: ExerciseShyCompatibility.Compatible);
        Exercise incompatible = Exercise(
            2,
            group,
            shyCompatibility: ExerciseShyCompatibility.Incompatible);
        Exercise unreviewed = Exercise(
            3,
            group,
            shyCompatibility: ExerciseShyCompatibility.Unreviewed);

        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            incompatible,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            compatible,
            WorkoutModifiers.Shy));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            incompatible,
            WorkoutModifiers.Shy));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            unreviewed,
            WorkoutModifiers.Shy));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete([unreviewed]));
    }

    [Fact]
    public void SilenceAndInsectComposeAsIndependentPositiveRequirements()
    {
        Exercise quietBug = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Compatible);
        Exercise noisyBug = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Compatible,
            silent: false);
        Exercise quietNoBug = Exercise(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Incompatible);
        Exercise noisyNoBug = Exercise(
            4,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            insectCompatibility: ExerciseInsectCompatibility.Incompatible,
            silent: false);

        Assert.All([quietBug, noisyBug, quietNoBug, noisyNoBug], exercise =>
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.None)));
        Assert.Equal(
            [quietBug.Id, noisyBug.Id],
            new[] { quietBug, noisyBug, quietNoBug, noisyNoBug }
                .Where(exercise => WorkoutModifierPolicy.IsCompatible(
                    exercise,
                    WorkoutModifiers.Insect))
                .Select(exercise => exercise.Id));
        Assert.Equal(
            [quietBug.Id, quietNoBug.Id],
            new[] { quietBug, noisyBug, quietNoBug, noisyNoBug }
                .Where(exercise => WorkoutModifierPolicy.IsCompatible(
                    exercise,
                    WorkoutModifiers.Silence))
                .Select(exercise => exercise.Id));
        Assert.Equal(
            [quietBug.Id],
            new[] { quietBug, noisyBug, quietNoBug, noisyNoBug }
                .Where(exercise => WorkoutModifierPolicy.IsCompatible(
                    exercise,
                    WorkoutModifiers.Insect | WorkoutModifiers.Silence))
                .Select(exercise => exercise.Id));
    }

    [Fact]
    public void WallEquipmentStatesUnlockExactlyTheirAllowedExercises()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise wallRequired = Exercise(1, group, wallRequired: true);
        Exercise soleWallRequired = Exercise(
            2,
            group,
            wallRequired: true,
            soleWallContactRequired: true);
        Exercise ordinary = Exercise(3, group);
        WorkoutModifiers solesMayTouch = WorkoutModifierPolicy.WithWallEquipment(
            WorkoutModifiers.None,
            WallEquipment.SolesMayTouch);

        Assert.False(WorkoutModifierPolicy.IsCompatible(
            wallRequired,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            soleWallRequired,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            wallRequired,
            WorkoutModifiers.Wall));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            soleWallRequired,
            WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            wallRequired,
            solesMayTouch));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            soleWallRequired,
            solesMayTouch));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            ordinary,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            ordinary,
            WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsWallPreferred(
            wallRequired,
            WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsWallPreferred(
            soleWallRequired,
            solesMayTouch));
        Assert.False(WorkoutModifierPolicy.IsWallPreferred(
            wallRequired,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsWallPreferred(
            ordinary,
            WorkoutModifiers.Wall));
    }

    [Fact]
    public void WallAndMirrorPreferencesComposeWithoutOneHidingTheOther()
    {
        Exercise wallAndMirrorRelevant = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
            wallRequired: true);

        Assert.Equal(0, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.None));
        Assert.Equal(1, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.Wall));
        Assert.Equal(1, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.Mirror));
        Assert.Equal(2, WorkoutModifierPolicy.GetEquipmentPreferenceCount(
            wallAndMirrorRelevant,
            WorkoutModifiers.Wall | WorkoutModifiers.Mirror));
    }

    [Fact]
    public void WallSingletonFloorCountsDistinctSessionMovementsOnly()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise[] exercises = Enumerable.Range(
                1,
                WorkoutModifierPolicy.MinimumWallRequiredSessionMovements)
            .Select(id => Exercise(
                id,
                group,
                sessionMovementId: id,
                wallRequired: true))
            .ToArray();

        Assert.Empty(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));

        exercises[^1] = Exercise(
            exercises[^1].Id,
            group,
            sessionMovementId: exercises[^2].SessionMovementId,
            wallRequired: true);
        WorkoutWallRequiredCatalogDeficiency deficiency = Assert.Single(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));
        Assert.Equal(
            WorkoutModifierPolicy.MinimumWallRequiredSessionMovements - 1,
            deficiency.MatchingSessionMovementCount);
        Assert.Equal(
            WorkoutModifierPolicy.MinimumWallRequiredSessionMovements,
            deficiency.RequiredSessionMovementCount);
    }

    [Fact]
    public void SoleWallFloorIsSeparateAndCountsDistinctSessionMovementsOnly()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise[] exercises = Enumerable.Range(
                1,
                WorkoutModifierPolicy
                    .MinimumSoleWallContactRequiredSessionMovements)
            .Select(id => Exercise(
                id,
                group,
                sessionMovementId: id,
                wallRequired: true,
                soleWallContactRequired: true))
            .ToArray();

        Assert.Empty(WorkoutModifierPolicy
            .FindSoleWallContactRequiredCatalogDeficiencies(exercises));
        Assert.Single(
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises));

        exercises[^1] = Exercise(
            exercises[^1].Id,
            group,
            sessionMovementId: exercises[^2].SessionMovementId,
            wallRequired: true,
            soleWallContactRequired: true);
        WorkoutSoleWallContactRequiredCatalogDeficiency deficiency =
            Assert.Single(WorkoutModifierPolicy
                .FindSoleWallContactRequiredCatalogDeficiencies(exercises));
        Assert.Equal(
            WorkoutModifierPolicy
                .MinimumSoleWallContactRequiredSessionMovements - 1,
            deficiency.MatchingSessionMovementCount);
        Assert.Equal(
            WorkoutModifierPolicy
                .MinimumSoleWallContactRequiredSessionMovements,
            deficiency.RequiredSessionMovementCount);
    }

    [Fact]
    public void MirrorEquipmentAppliesCoverageWithoutFilteringOrdinaryExercises()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise mirrorOnly = Exercise(
            1,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody);
        Exercise fullBodyMirrorOnly = Exercise(
            2,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.FullBody);
        Exercise benefitsGreatly = Exercise(
            3,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody);
        Exercise fullBodyBenefitsGreatly = Exercise(
            4,
            group,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
            minimumMirrorCoverage: ExerciseMirrorCoverage.FullBody);
        Exercise agnostic = Exercise(5, group);

        Assert.False(WorkoutModifierPolicy.IsCompatible(
            mirrorOnly,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            benefitsGreatly,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsCompatible(
            agnostic,
            WorkoutModifiers.None));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            fullBodyMirrorOnly,
            WorkoutModifiers.Mirror));
        Assert.All([mirrorOnly, benefitsGreatly, fullBodyBenefitsGreatly, agnostic], exercise =>
            Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                WorkoutModifiers.Mirror)));
        WorkoutModifiers tallMirror = WorkoutModifierPolicy.WithMirrorEquipment(
            WorkoutModifiers.None,
            MirrorEquipment.Tall);
        Assert.All(
            [mirrorOnly, fullBodyMirrorOnly, benefitsGreatly,
                fullBodyBenefitsGreatly, agnostic],
            exercise => Assert.True(WorkoutModifierPolicy.IsCompatible(
                exercise,
                tallMirror)));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            mirrorOnly,
            WorkoutModifiers.Mirror));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            benefitsGreatly,
            WorkoutModifiers.Mirror));
        Assert.False(WorkoutModifierPolicy.IsMirrorPreferred(
            fullBodyBenefitsGreatly,
            WorkoutModifiers.Mirror));
        Assert.True(WorkoutModifierPolicy.IsMirrorPreferred(
            fullBodyBenefitsGreatly,
            tallMirror));
        Assert.False(WorkoutModifierPolicy.IsMirrorPreferred(
            agnostic,
            WorkoutModifiers.Mirror));
        Assert.False(WorkoutModifierPolicy.IsMirrorPreferred(
            benefitsGreatly,
            WorkoutModifiers.None));
    }

    [Fact]
    public void MirrorMetadataIsCompleteOnlyWhenRelationshipMatchesEquipment()
    {
        CanonicalMuscleGroup group =
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors;
        Exercise[] valid =
        [
            Exercise(
                1,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody),
            Exercise(
                2,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.FullBody),
            Exercise(3, group),
        ];

        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete(valid));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                4,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.Unreviewed),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                5,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
                equipment: "None",
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                6,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly,
                equipment: "Mirror",
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                7,
                group,
                mirrorRelationship: ExerciseMirrorRelationship.MirrorOnly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.None),
        ]));
        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(
        [
            Exercise(
                8,
                group,
                soleWallContactRequired: true),
        ]));
    }

    [Fact]
    public void WallEquipmentRoundTripsAndOrphanSoleQualifierIsDiscarded()
    {
        WorkoutModifiers context = WorkoutModifiers.Insect |
            WorkoutModifiers.Silence;

        Assert.Equal(
            WallEquipment.None,
            WorkoutModifierPolicy.GetWallEquipment(
                WorkoutModifiers.SoleWallContact));
        Assert.Equal(
            WorkoutModifiers.None,
            WorkoutModifierPolicy.Normalize(
                WorkoutModifiers.SoleWallContact));

        WorkoutModifiers solesStayOff =
            WorkoutModifierPolicy.WithWallEquipment(
                context,
                WallEquipment.SolesStayOff);
        Assert.Equal(
            WallEquipment.SolesStayOff,
            WorkoutModifierPolicy.GetWallEquipment(solesStayOff));
        Assert.Equal(context | WorkoutModifiers.Wall, solesStayOff);

        WorkoutModifiers solesMayTouch =
            WorkoutModifierPolicy.WithWallEquipment(
                solesStayOff,
                WallEquipment.SolesMayTouch);
        Assert.Equal(
            WallEquipment.SolesMayTouch,
            WorkoutModifierPolicy.GetWallEquipment(solesMayTouch));
        Assert.Equal(
            context | WorkoutModifiers.Wall |
                WorkoutModifiers.SoleWallContact,
            solesMayTouch);

        Assert.Equal(
            context,
            WorkoutModifierPolicy.WithWallEquipment(
                solesMayTouch,
                WallEquipment.None));
    }

    [Fact]
    public void MirrorEquipmentRoundTripsAndOrphanTallQualifierIsDiscarded()
    {
        WorkoutModifiers context = WorkoutModifiers.Insect |
            WorkoutModifiers.Silence;

        Assert.Equal(
            MirrorEquipment.None,
            WorkoutModifierPolicy.GetMirrorEquipment(
                WorkoutModifiers.TallMirror));
        Assert.Equal(
            WorkoutModifiers.None,
            WorkoutModifierPolicy.Normalize(WorkoutModifiers.TallMirror));

        WorkoutModifiers compact = WorkoutModifierPolicy.WithMirrorEquipment(
            context,
            MirrorEquipment.Compact);
        Assert.Equal(MirrorEquipment.Compact,
            WorkoutModifierPolicy.GetMirrorEquipment(compact));
        Assert.Equal(context | WorkoutModifiers.Mirror, compact);

        WorkoutModifiers tall = WorkoutModifierPolicy.WithMirrorEquipment(
            compact,
            MirrorEquipment.Tall);
        Assert.Equal(MirrorEquipment.Tall,
            WorkoutModifierPolicy.GetMirrorEquipment(tall));
        Assert.Equal(
            context | WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror,
            tall);

        Assert.Equal(
            context,
            WorkoutModifierPolicy.WithMirrorEquipment(
                tall,
                MirrorEquipment.None));
    }

    [Theory]
    [InlineData(ExerciseMirrorRelationship.Agnostic, ExerciseMirrorCoverage.None)]
    [InlineData(ExerciseMirrorRelationship.MirrorOnly, ExerciseMirrorCoverage.UpperBody)]
    [InlineData(ExerciseMirrorRelationship.MirrorOnly, ExerciseMirrorCoverage.FullBody)]
    [InlineData(ExerciseMirrorRelationship.BenefitsGreatly, ExerciseMirrorCoverage.UpperBody)]
    [InlineData(ExerciseMirrorRelationship.BenefitsGreatly, ExerciseMirrorCoverage.FullBody)]
    public void MirrorMetadataAllowsEmptyOtherRelationshipCells(
        ExerciseMirrorRelationship relationship, ExerciseMirrorCoverage coverage)
    {
        Exercise single = Exercise(1, CanonicalMuscleGroup.ShoulderAbductors,
            mirrorRelationship: relationship, minimumMirrorCoverage: coverage);
        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete([single]));
    }

    [Fact]
    public void InsectFineAvailabilityUsesWallOnlyWhereDirectWorkExists()
    {
        WorkoutGroup pelvicFloor = MassGroupingTaxonomy.GetGroup(
            30,
            CanonicalMuscleGroup.PelvicFloorAndPerineum);
        WorkoutGroup intrinsicHand = MassGroupingTaxonomy.GetGroup(
            30,
            CanonicalMuscleGroup.IntrinsicHand);

        Assert.False(WorkoutModifierPolicy.IsSelectionGroupAvailable(
            pelvicFloor,
            WorkoutModifiers.Insect));
        Assert.False(WorkoutModifierPolicy.IsSelectionGroupAvailable(
            intrinsicHand,
            WorkoutModifiers.Insect));
        Assert.False(WorkoutModifierPolicy.IsSelectionGroupAvailable(
            pelvicFloor,
            WorkoutModifiers.Insect | WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(
            intrinsicHand,
            WorkoutModifiers.Insect | WorkoutModifiers.Wall));
        Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(
            pelvicFloor,
            WorkoutModifiers.None));
        Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(
            intrinsicHand,
            WorkoutModifiers.None));
    }

    [Fact]
    public void FinePairwiseBucketsMeasureAvailabilityWithoutForcingMirrorPreference()
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(30).Groups[0];
        CanonicalMuscleGroup primary = group.CanonicalGroups.Single();
        Exercise agnosticExercise = Exercise(
            1,
            primary,
            mirrorRelationship: ExerciseMirrorRelationship.Agnostic);
        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                [agnosticExercise]),
            result => result.Minutes == 30 &&
                result.GroupId == group.Id &&
                result.FirstModifier == WorkoutModifiers.Insect &&
                result.SecondModifier == WorkoutModifiers.Mirror &&
                result.SecondModifierEnabled);
        Assert.Equal(
            1,
            WorkoutModifierPolicy.GetMinimumExercisesPerPairStatePerGroup(30));
    }

    [Fact]
    public void BroadPairwiseBucketsCountSelectableAgnosticMovements()
    {
        WorkoutGroup group = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[0];
        CanonicalMuscleGroup[] canonicalGroups = group.CanonicalGroups.ToArray();
        var catalog = new List<Exercise>();
        for (int index = 0; index < 5; index++)
        {
            int rootId = index * 2 + 1;
            int memberId = rootId + 1;
            catalog.Add(Exercise(
                rootId,
                canonicalGroups[0],
                canonicalGroups[1],
                canonicalGroups[2],
                sequenceBlocks:
                [
                    new ExerciseSequenceBlock
                    {
                        ExerciseId = rootId,
                        MirrorMedia = false,
                    },
                    new ExerciseSequenceBlock
                    {
                        ExerciseId = memberId,
                        MirrorMedia = false,
                    },
                ]));
            catalog.Add(Exercise(
                memberId,
                canonicalGroups[3],
                canonicalGroups[4],
                canonicalGroups[5],
                sequenceBlocks: []));
        }

        WorkoutModifierPairCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(catalog)
                .Where(result => result.Minutes ==
                        WorkoutModifierPolicy.BroadCoverageResolutionMinutes &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Mirror &&
                    result.SecondModifierEnabled)
                .ToArray();

        Assert.Empty(deficiencies);
        var shortage = WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                Array.Empty<Exercise>())
            .Where(result => result.Minutes == 3 && result.GroupId == group.Id &&
                result.FirstModifier == WorkoutModifiers.Insect &&
                result.SecondModifier == WorkoutModifiers.Mirror &&
                result.SecondModifierEnabled).ToArray();
        Assert.Equal(4, shortage.Length);
        Assert.All(shortage, result => Assert.Equal(0, result.MatchingExerciseCount));
        Assert.Contains(WorkoutModifierPolicy.FindMaterialityDeficiencies(catalog),
            result => result.Modifier == WorkoutModifiers.Mirror &&
                result.ContextProfile == WorkoutModifiers.None);
    }

    [Fact]
    public void MirrorMaterialityIsJointlySuppliedByOnlyAndGreatlyBenefitedExercises()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] mirrorRelevant = Enumerable.Range(1, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                mirrorRelationship: id == 1
                    ? ExerciseMirrorRelationship.MirrorOnly
                    : ExerciseMirrorRelationship.BenefitsGreatly,
                minimumMirrorCoverage: ExerciseMirrorCoverage.UpperBody))
            .ToArray();
        Exercise[] agnostic = Enumerable.Range(6, 15)
            .Select((id, index) => Exercise(id, groups[index % groups.Length]))
            .ToArray();

        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindMaterialityDeficiencies(
                mirrorRelevant.Concat(agnostic).ToArray()),
            result => result.Modifier == WorkoutModifiers.Mirror &&
                result.ContextProfile == WorkoutModifiers.None);
    }

    [Fact]
    public void ValidationProfilesGrowOnlyWithSinglesAndModifierPairs()
    {
        Assert.Equal(
            WorkoutModifierPolicy.ValidationProfiles.Count,
            WorkoutModifierPolicy.ValidationProfiles.Distinct().Count());
        Assert.All(WorkoutModifierPolicy.ValidationProfiles, profile =>
            Assert.Equal(profile, WorkoutModifierPolicy.Normalize(profile)));
        Assert.Contains(WorkoutModifiers.None, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Equal(28, WorkoutModifierPolicy.ValidationProfiles.Count);
        Assert.Contains(
            WorkoutModifiers.UpperBodyClothing,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.HardFloor, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.Silence, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.Shy, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Insect | WorkoutModifiers.Silence,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(WorkoutModifiers.Mirror, WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Silence | WorkoutModifiers.Mirror,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.Contains(
            WorkoutModifiers.Silence | WorkoutModifiers.Mirror |
                WorkoutModifiers.TallMirror,
            WorkoutModifierPolicy.ValidationProfiles);
        Assert.DoesNotContain(
            WorkoutModifierPolicy.ValidationProfiles,
            profile => profile.HasFlag(WorkoutModifiers.Wall));
        Assert.DoesNotContain(
            WorkoutModifierPolicy.ValidationProfiles,
            profile => profile.HasFlag(WorkoutModifiers.Light));
    }

    [Fact]
    public void LightIsSupportedButNeverPersistedOrUsedAsAQuotaAxis()
    {
        WorkoutModifiers profile = WorkoutModifiers.Insect |
            WorkoutModifiers.Light;

        Assert.Equal(profile, WorkoutModifierPolicy.Normalize(profile));
        Assert.Equal(
            WorkoutModifiers.Insect,
            WorkoutModifierPolicy.GetPersistentSetupModifiers(profile));
        Assert.DoesNotContain(
            WorkoutModifierPolicy.ValidationProfiles,
            candidate => candidate.HasFlag(WorkoutModifiers.Light));
    }

    [Fact]
    public void PairwiseAvailabilityTreatsDisabledModifiersAsRelaxed()
    {
        WorkoutGroup group = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[1];
        CanonicalMuscleGroup[] canonicalGroups = group.CanonicalGroups.ToArray();
        Exercise[] exercises = Enumerable.Range(1, 5)
            .Select(id => Exercise(
                id,
                canonicalGroups[0],
                canonicalGroups[1],
                canonicalGroups[2],
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: true))
            .ToArray();

        WorkoutModifierPairCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises)
                .Where(result =>
                    result.Minutes ==
                        WorkoutModifierPolicy.BroadCoverageResolutionMinutes &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToArray();

        Assert.Empty(deficiencies);

        WorkoutModifierPairCoverageDeficiency[] emptyCatalogDeficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(
                    Array.Empty<Exercise>())
                .Where(result =>
                    result.Minutes ==
                        WorkoutModifierPolicy.BroadCoverageResolutionMinutes &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToArray();

        Assert.Equal(4, emptyCatalogDeficiencies.Length);
        Assert.All(emptyCatalogDeficiencies, deficiency =>
            Assert.Equal(0, deficiency.MatchingExerciseCount));
        Assert.Equal(
            4,
            emptyCatalogDeficiencies
                .Select(deficiency => (
                    deficiency.FirstModifierEnabled,
                    deficiency.SecondModifierEnabled))
                .Distinct()
                .Count());
    }

    [Fact]
    public void HardFloorCoverageRequiresSafeChoicesWithoutSoftOnlyCounterparts()
    {
        WorkoutGroup group = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[1];
        CanonicalMuscleGroup[] canonicalGroups = group.CanonicalGroups.ToArray();
        Exercise[] compatible = Enumerable.Range(1, 5)
            .Select(id => Exercise(
                id,
                canonicalGroups[0],
                canonicalGroups[1],
                canonicalGroups[2],
                hardFloorCompatibility:
                    ExerciseHardFloorCompatibility.Compatible))
            .ToArray();
        Exercise[] incompatible = Enumerable.Range(6, 4)
            .Select(id => Exercise(
                id,
                canonicalGroups[0],
                canonicalGroups[1],
                canonicalGroups[2],
                hardFloorCompatibility:
                    ExerciseHardFloorCompatibility.Incompatible))
            .ToArray();

        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindHardFloorCategoryCoverageDeficiencies(compatible),
            result => result.Minutes == 3 && result.GroupId == group.Id);
        var deficiencies = WorkoutModifierPolicy.FindHardFloorCategoryCoverageDeficiencies(
                incompatible)
            .Where(result => result.Minutes == 3 && result.GroupId == group.Id).ToArray();
        Assert.Equal(5, deficiencies.Length);
        Assert.All(deficiencies, result =>
        {
            Assert.Equal(ExerciseHardFloorCompatibility.Compatible, result.HardFloorCompatibility);
            Assert.Equal(0, result.MatchingExerciseCount);
        });
        WorkoutGroup fine = MassGroupingTaxonomy.GetGroup(30, canonicalGroups[0]);
        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindHardFloorCategoryCoverageDeficiencies(compatible),
            result => result.Minutes == 30 && result.GroupId == fine.Id);
        Assert.Contains(
            WorkoutModifierPolicy.FindHardFloorCategoryCoverageDeficiencies(incompatible),
            result => result.Minutes == 30 && result.GroupId == fine.Id &&
                result.MatchingExerciseCount == 0 && result.RequiredExerciseCount == 1);
        Assert.All(
            WorkoutModifierPolicy.FindHardFloorCategoryCoverageDeficiencies([.. compatible, .. incompatible]),
            result => Assert.Equal(ExerciseHardFloorCompatibility.Compatible, result.HardFloorCompatibility));
    }

    [Fact]
    public void DemandCoverageRequiresWholeLightSequencesAndSlotOwnedHardMembers()
    {
        WorkoutGroup targetGroup = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[1];
        WorkoutGroup otherGroup = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[0];
        CanonicalMuscleGroup[] target = targetGroup.CanonicalGroups.ToArray();
        CanonicalMuscleGroup other = otherGroup.CanonicalGroups.First();
        Exercise pureLight = Exercise(
            1,
            target[0],
            target[1],
            target[2],
            muscularDemand: 0);
        Exercise mixedRoot = Exercise(
            2,
            target[0],
            target[1],
            target[2],
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 2, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 3, MirrorMedia = false },
            ],
            muscularDemand: 0);
        Exercise mixedMember = Exercise(
            3,
            target[0],
            sequenceBlocks: [],
            muscularDemand: 1);
        Exercise hardElsewhereRoot = Exercise(
            4,
            target[0],
            target[1],
            target[2],
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 4, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 5, MirrorMedia = false },
            ],
            muscularDemand: 0);
        Exercise hardElsewhereMember = Exercise(
            5,
            other,
            sequenceBlocks: [],
            muscularDemand: 2);
        Exercise hardForTargetRoot = Exercise(
            6,
            other,
            target[1],
            target[2],
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 6, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 7, MirrorMedia = false },
            ],
            muscularDemand: 1);
        Exercise hardForTargetMember = Exercise(
            7,
            target[0],
            sequenceBlocks: [],
            muscularDemand: 2);
        WorkoutMuscularDemandCoverageDeficiency[] TargetDeficiencies(
            params Exercise[] catalog) =>
            WorkoutModifierPolicy.FindMuscularDemandCoverageDeficiencies(catalog)
                .Where(result =>
                    result.Minutes ==
                        WorkoutModifierPolicy.BroadCoverageResolutionMinutes &&
                    result.GroupId == targetGroup.Id &&
                    result.Profile == WorkoutModifiers.None)
                .ToArray();

        WorkoutMuscularDemandCoverageDeficiency lightDeficiency = Assert.Single(
            TargetDeficiencies(
                mixedRoot,
                mixedMember,
                hardForTargetRoot,
                hardForTargetMember));
        Assert.Equal(0, lightDeficiency.MuscularDemand);
        Assert.Equal(0, lightDeficiency.MatchingExerciseCount);
        Assert.Equal(1, lightDeficiency.RequiredExerciseCount);

        WorkoutMuscularDemandCoverageDeficiency hardDeficiency = Assert.Single(
            TargetDeficiencies(
                pureLight,
                hardElsewhereRoot,
                hardElsewhereMember));
        Assert.Equal(2, hardDeficiency.MuscularDemand);
        Assert.Equal(0, hardDeficiency.MatchingExerciseCount);
        Assert.Equal(1, hardDeficiency.RequiredExerciseCount);

        Assert.Empty(TargetDeficiencies(
            pureLight,
            hardForTargetRoot,
            hardForTargetMember));
    }

    [Fact]
    public void DemandCoverageUsesOneGenuineSessionMovementPerCategory()
    {
        WorkoutGroup targetGroup = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[1];
        CanonicalMuscleGroup[] target = targetGroup.CanonicalGroups.ToArray();
        Exercise[] exercises =
        [
            Exercise(1, target[0], target[1], target[2], muscularDemand: 0),
            Exercise(2, target[0], target[1], target[2], muscularDemand: 2),
        ];

        Assert.DoesNotContain(
            WorkoutModifierPolicy.FindMuscularDemandCoverageDeficiencies(exercises),
            result => result.Minutes ==
                    WorkoutModifierPolicy.BroadCoverageResolutionMinutes &&
                result.GroupId == targetGroup.Id);
        Assert.Equal(
            1,
            WorkoutModifierPolicy
                .MinimumExercisesPerMuscularDemandCategoryPerGroup);
    }

    [Fact]
    public void PairwiseAvailabilityCountsTheActualNestedCandidateSets()
    {
        WorkoutGroup group = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[1];
        CanonicalMuscleGroup[] canonicalGroups = group.CanonicalGroups.ToArray();
        Exercise[] exercises =
        [
            Exercise(1, canonicalGroups[0], canonicalGroups[1], canonicalGroups[2],
                insectCompatibility: ExerciseInsectCompatibility.Incompatible,
                silent: false),
            Exercise(2, canonicalGroups[0], canonicalGroups[1], canonicalGroups[2],
                insectCompatibility: ExerciseInsectCompatibility.Incompatible,
                silent: true),
            Exercise(3, canonicalGroups[0], canonicalGroups[1], canonicalGroups[2],
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: false),
            Exercise(4, canonicalGroups[0], canonicalGroups[1], canonicalGroups[2],
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: true),
        ];

        Dictionary<(bool Insect, bool Silence), int> counts =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises)
                .Where(result => result.Minutes ==
                        WorkoutModifierPolicy.BroadCoverageResolutionMinutes &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToDictionary(
                    result => (
                        result.FirstModifierEnabled,
                        result.SecondModifierEnabled),
                    result => result.MatchingExerciseCount);

        Assert.Empty(counts);
        WorkoutModifierPairCoverageDeficiency missingIntersection = Assert.Single(
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises.Take(3).ToArray()),
            result => result.Minutes == 3 && result.GroupId == group.Id &&
                result.FirstModifier == WorkoutModifiers.Insect &&
                result.SecondModifier == WorkoutModifiers.Silence);
        Assert.True(missingIntersection.FirstModifierEnabled);
        Assert.True(missingIntersection.SecondModifierEnabled);
        Assert.Equal(0, missingIntersection.MatchingExerciseCount);
    }

    [Fact]
    public void PairwiseAvailabilityNeverCountsUnreviewedMetadata()
    {
        WorkoutGroup group = MassGroupingTaxonomy
            .GetResolution(WorkoutModifierPolicy.BroadCoverageResolutionMinutes)
            .Groups[1];
        CanonicalMuscleGroup[] canonicalGroups = group.CanonicalGroups.ToArray();
        Exercise[] exercises = [Exercise(
                5,
                canonicalGroups[0],
                canonicalGroups[1],
                canonicalGroups[2],
                insectCompatibility: ExerciseInsectCompatibility.Unreviewed)];

        WorkoutModifierPairCoverageDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindPairwiseCoverageDeficiencies(exercises)
                .Where(result => result.Minutes ==
                        WorkoutModifierPolicy.BroadCoverageResolutionMinutes &&
                    result.GroupId == group.Id &&
                    result.FirstModifier == WorkoutModifiers.Insect &&
                    result.SecondModifier == WorkoutModifiers.Silence)
                .ToArray();

        Assert.Equal(4, deficiencies.Length);
        Assert.All(deficiencies, deficiency =>
            Assert.Equal(0, deficiency.MatchingExerciseCount));
    }

    [Fact]
    public void MaterialityChecksGrowQuadratically()
    {
        // Clothing is a bidirectional setup state, not a restrictive filter,
        // so materiality is six restrictive single-state checks, twelve
        // directed binary/binary edges, and four edges for each of the four
        // binary/Mirror pairs.
        Assert.Equal(
            34,
            WorkoutModifierPolicy.FindMaterialityDeficiencies([]).Count);
    }

    [Fact]
    public void TokenModifierFailsRelativeMaterialityFloor()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] compatibleExercises = Enumerable.Range(1, 115)
            .Select(index => Exercise(
                index,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] releasedExercises = Enumerable.Range(116, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Incompatible))
            .ToArray();

        WorkoutModifierMaterialityDeficiency deficiency =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(
                    compatibleExercises.Concat(releasedExercises).ToArray())
                .Single(result =>
                    result.Modifier == WorkoutModifiers.Insect &&
                    result.ContextProfile == WorkoutModifiers.None);

        Assert.Equal(120, deficiency.BaselineExerciseCount);
        Assert.Equal(115, deficiency.ModifiedExerciseCount);
        Assert.Equal(5, deficiency.MaterialExerciseCount);
        Assert.Equal(6, deficiency.RequiredMaterialExerciseCount);
        Assert.Equal(3, deficiency.AffectedBucketCount);
        Assert.Equal(3, deficiency.RequiredAffectedBucketCount);
    }

    [Fact]
    public void MaterialityMustAffectEnoughCanonicalBuckets()
    {
        CanonicalMuscleGroup group = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups[0]
            .CanonicalGroups
            .Single();
        Exercise[] exercises = Enumerable.Range(1, 10)
            .Select(id => Exercise(
                id,
                group,
                insectCompatibility: id <= 5
                    ? ExerciseInsectCompatibility.Compatible
                    : ExerciseInsectCompatibility.Incompatible))
            .ToArray();

        WorkoutModifierMaterialityDeficiency deficiency =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises)
                .Single(result =>
                    result.Modifier == WorkoutModifiers.Insect &&
                    result.ContextProfile == WorkoutModifiers.None);

        Assert.Equal(5, deficiency.MaterialExerciseCount);
        Assert.Equal(5, deficiency.RequiredMaterialExerciseCount);
        Assert.Equal(1, deficiency.AffectedBucketCount);
        Assert.Equal(3, deficiency.RequiredAffectedBucketCount);
    }

    [Fact]
    public void MaterialityMustRemainWhenAnotherModifierIsEnabled()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] quietInsectCompatible = Enumerable.Range(1, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Compatible,
                silent: true))
            .ToArray();
        Exercise[] noisyInsectIncompatible = Enumerable.Range(6, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Incompatible,
                silent: false))
            .ToArray();
        Exercise[] exercises = quietInsectCompatible
            .Concat(noisyInsectIncompatible)
            .ToArray();

        WorkoutModifierMaterialityDeficiency[] deficiencies =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises)
                .ToArray();

        Assert.DoesNotContain(deficiencies, result =>
            result.Modifier == WorkoutModifiers.Insect &&
            result.ContextProfile == WorkoutModifiers.None);
        WorkoutModifierMaterialityDeficiency conditionalDeficiency =
            Assert.Single(deficiencies, result =>
                result.Modifier == WorkoutModifiers.Insect &&
                result.ContextProfile == WorkoutModifiers.Silence);
        Assert.Equal(0, conditionalDeficiency.MaterialExerciseCount);
        Assert.Equal(WorkoutModifierPolicy.MinimumMaterialExercises,
            conditionalDeficiency.RequiredMaterialExerciseCount);
        Assert.Equal(0, conditionalDeficiency.AffectedBucketCount);
    }

    [Fact]
    public void MaterialityNeverCreditsUnreviewedMetadata()
    {
        CanonicalMuscleGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .Take(3)
            .Select(group => group.CanonicalGroups.Single())
            .ToArray();
        Exercise[] exercises = Enumerable.Range(1, 5)
            .Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .Concat(Enumerable.Range(6, 5).Select((id, index) => Exercise(
                id,
                groups[index % groups.Length],
                insectCompatibility: ExerciseInsectCompatibility.Unreviewed)))
            .ToArray();

        WorkoutModifierMaterialityDeficiency deficiency =
            WorkoutModifierPolicy.FindMaterialityDeficiencies(exercises)
                .Single(result =>
                    result.Modifier == WorkoutModifiers.Insect &&
                    result.ContextProfile == WorkoutModifiers.None);

        Assert.Equal(0, deficiency.MaterialExerciseCount);
        Assert.Equal(0, deficiency.AffectedBucketCount);
    }

    [Fact]
    public void MaximumDistinctLineupUsesAugmentingPathsInsteadOfGreedyCounts()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise[] exercises =
        [
            Exercise(
                1,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors),
            Exercise(2, CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Exercise(3, CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
        ];

        Assert.Equal(
            3,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.Insect));
    }

    [Fact]
    public void MaximumDistinctLineupDetectsHallDeficitAfterModifierFiltering()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise[] exercises =
        [
            Exercise(
                1,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors),
            Exercise(
                2,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors),
            Exercise(
                3,
                CanonicalMuscleGroup.MajorHipAdductors,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible),
        ];

        Assert.Equal(
            3,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.None));
        Assert.Equal(
            2,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.Insect));
    }

    [Fact]
    public void MaximumDistinctLineupCountsAliasesAsOneSessionMovement()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise[] exercises =
        [
            Exercise(
                1,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors,
                sessionMovementId: 1),
            Exercise(
                2,
                CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
                CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
                CanonicalMuscleGroup.MajorHipAdductors,
                sessionMovementId: 1),
            Exercise(3, CanonicalMuscleGroup.MajorHipAdductors),
        ];

        Assert.Equal(
            2,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                exercises,
                groups,
                WorkoutModifiers.Insect));
    }

    [Fact]
    public void MaximumDistinctLineupCreditsCrossPrimarySequenceSlots()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise member = Exercise(
            2,
            CanonicalMuscleGroup.PosteriorThighAndKneeFlexors,
            sequenceBlocks: []);
        Exercise root = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 1, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 2, MirrorMedia = false },
            ]);
        Exercise singleton = Exercise(
            3,
            CanonicalMuscleGroup.MajorHipAdductors);

        Assert.Equal(
            3,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                [root, member, singleton],
                groups,
                WorkoutModifiers.Insect,
                workoutMinutes: 3));
    }

    [Fact]
    public void MaximumDistinctLineupLetsSamePrimarySequenceYieldToCapacity()
    {
        WorkoutGroup[] groups =
        [
            Group("a", CanonicalMuscleGroup.MedialAndDeepKneeExtensors),
            Group("b", CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
            Group("c", CanonicalMuscleGroup.MajorHipAdductors),
        ];
        Exercise member = Exercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            sequenceBlocks: []);
        Exercise root = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            sequenceBlocks:
            [
                new ExerciseSequenceBlock { ExerciseId = 1, MirrorMedia = false },
                new ExerciseSequenceBlock { ExerciseId = 2, MirrorMedia = false },
            ]);

        Assert.Equal(
            2,
            WorkoutModifierPolicy.GetMaximumDistinctLineupSize(
                [
                    root,
                    member,
                    Exercise(3, CanonicalMuscleGroup.PosteriorThighAndKneeFlexors),
                    Exercise(4, CanonicalMuscleGroup.MajorHipAdductors),
                ],
                groups,
                WorkoutModifiers.Insect,
                workoutMinutes: 3));
    }

    private static WorkoutGroup Group(
        string id,
        CanonicalMuscleGroup canonicalGroup)
    {
        return new WorkoutGroup(
            id,
            id,
            1,
            new HashSet<CanonicalMuscleGroup> { canonicalGroup });
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primaryCanonicalGroup,
        CanonicalMuscleGroup secondaryCanonicalGroup = default,
        CanonicalMuscleGroup tertiaryCanonicalGroup = default,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Compatible,
        bool silent = true,
        ExerciseMirrorRelationship mirrorRelationship =
            ExerciseMirrorRelationship.Agnostic,
        string? equipment = null,
        ExerciseMirrorCoverage? minimumMirrorCoverage = null,
        int sessionMovementId = 0,
        ExerciseSequenceBlock[]? sequenceBlocks = null,
        ExerciseHardFloorCompatibility hardFloorCompatibility =
            ExerciseHardFloorCompatibility.Compatible,
        bool wallRequired = false,
        bool soleWallContactRequired = false,
        ExerciseUpperBodyClothingRequirement upperBodyClothingRequirement =
            ExerciseUpperBodyClothingRequirement.Agnostic,
        ExerciseShyCompatibility shyCompatibility =
            ExerciseShyCompatibility.Compatible,
        int muscularDemand = 0)
    {
        CanonicalMuscleGroup[] secondaryCanonicalGroups =
            new[] { secondaryCanonicalGroup, tertiaryCanonicalGroup }
                .Where(group => group != default)
                .ToArray();
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primaryCanonicalGroup,
            SecondaryCanonicalGroups = secondaryCanonicalGroups,
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = ExerciseSideSequence.Continuous,
            SequenceBlocks = sequenceBlocks ??
                [
                    new ExerciseSequenceBlock
                    {
                        ExerciseId = id,
                        SideCue = ExerciseSequenceSideCue.None,
                        DirectionCue = ExerciseSequenceDirectionCue.None,
                        MirrorMedia = false,
                    },
                ],
            SessionMovementId = sessionMovementId,
            InsectCompatibility = insectCompatibility,
            HardFloorCompatibility = hardFloorCompatibility,
            UpperBodyClothingRequirement = upperBodyClothingRequirement,
            ShyCompatibility = shyCompatibility,
            MirrorRelationship = mirrorRelationship,
            MinimumMirrorCoverage = minimumMirrorCoverage ??
                (mirrorRelationship is ExerciseMirrorRelationship.MirrorOnly or
                    ExerciseMirrorRelationship.BenefitsGreatly
                    ? ExerciseMirrorCoverage.UpperBody
                    : ExerciseMirrorCoverage.None),
            WallRequired = wallRequired,
            SoleWallContactRequired = soleWallContactRequired,
            MuscularDemand = muscularDemand,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 2,
            Equipment = equipment ??
                (mirrorRelationship == ExerciseMirrorRelationship.MirrorOnly
                    ? "Mirror"
                    : "None"),
            Silent = silent,
        };
    }
}
