using System.Numerics;
using NomadicMethod.Models;

namespace NomadicMethod.Services;

public sealed record WorkoutModifierPairCoverageDeficiency(
    int Minutes,
    string GroupId,
    string GroupName,
    WorkoutModifiers FirstModifier,
    bool FirstModifierEnabled,
    WorkoutModifiers SecondModifier,
    bool SecondModifierEnabled,
    MirrorEquipment MirrorEquipment,
    int MatchingExerciseCount,
    int RequiredExerciseCount);

public sealed record WorkoutHardFloorCategoryCoverageDeficiency(
    int Minutes,
    string GroupId,
    string GroupName,
    ExerciseHardFloorCompatibility HardFloorCompatibility,
    WorkoutModifiers PartnerModifier,
    bool PartnerModifierEnabled,
    int MatchingExerciseCount,
    int RequiredExerciseCount);

public sealed record WorkoutMuscularDemandCoverageDeficiency(
    int Minutes,
    string GroupId,
    string GroupName,
    int MuscularDemand,
    WorkoutModifiers Profile,
    int MatchingExerciseCount,
    int RequiredExerciseCount);

public sealed record WorkoutWallRequiredCatalogDeficiency(
    int MatchingSessionMovementCount,
    int RequiredSessionMovementCount);

public sealed record WorkoutSoleWallContactRequiredCatalogDeficiency(
    int MatchingSessionMovementCount,
    int RequiredSessionMovementCount);

public sealed record WorkoutProfileLineupDeficiency(
    int Minutes,
    WorkoutModifiers Profile,
    int MaximumDistinctExerciseCount,
    int RequiredDistinctExerciseCount);

public sealed record WorkoutProfileCompletionDeficiency(
    int Minutes,
    WorkoutModifiers Profile,
    int MaximumCoveredGroupCount,
    int RequiredGroupCount);

public sealed record AcceptedWorkoutCoverageException(
    string GroupId,
    WorkoutModifiers RequiredModifiers);

public sealed record WorkoutModifierMaterialityDeficiency(
    WorkoutModifiers Modifier,
    WorkoutModifiers ContextProfile,
    WorkoutModifiers ModifiedProfile,
    int BaselineExerciseCount,
    int ModifiedExerciseCount,
    int MaterialExerciseCount,
    int RequiredMaterialExerciseCount,
    int AffectedBucketCount,
    int RequiredAffectedBucketCount);

public static class WorkoutModifierPolicy
{
    public const int BroadCoverageResolutionMinutes = 3;
    // Availability means a real choice exists. Complete-lineup validation
    // separately enforces breadth and the complete atomic block budget.
    public const int MinimumExercisesPerBroadPairStatePerGroup = 1;
    public const int MinimumExercisesPerFinePairStatePerGroup = 1;
    // Historical demand and materiality targets are diagnostic inventories,
    // not admission requirements or release gates. Never fill them with
    // altered anatomy, demand ratings or invented variations.
    public const int MinimumExercisesPerMuscularDemandCategoryPerGroup = 1;
    public const int MinimumWallRequiredSessionMovements = 20;
    public const int MinimumSoleWallContactRequiredSessionMovements = 5;
    public const int MinimumMaterialExercises = 5;
    public const int MinimumMaterialExercisePercent = 5;
    public const int MinimumAffectedBucketPercent = 10;

    private const int MaterialityResolutionMinutes = 30;

    // Owner-accepted catalog gaps, 12 September 2026. These exact slots are
    // omitted in their affected setup; remaining rounds retain the full duration.
    // This does not change any exercise's anatomy or compatibility.
    public static IReadOnlyList<AcceptedWorkoutCoverageException> AcceptedCoverageExceptions { get; } =
        Array.AsReadOnly<AcceptedWorkoutCoverageException>(
        [
            new("r15.scapular-chest-breathing", WorkoutModifiers.Insect),
            new("r15.shoulder", WorkoutModifiers.Insect | WorkoutModifiers.HardFloor),
            new("r20.shoulder-adduction-extension", WorkoutModifiers.Insect | WorkoutModifiers.HardFloor),
            new("r30.rotator-cuff", WorkoutModifiers.Insect | WorkoutModifiers.HardFloor),
            new("r30.shoulder-adductors-extensors", WorkoutModifiers.Insect | WorkoutModifiers.HardFloor),
            new("r30.breathing-muscles", WorkoutModifiers.Insect | WorkoutModifiers.Silence),
            new("r30.breathing-muscles", WorkoutModifiers.Insect | WorkoutModifiers.Shy),
        ]);

    // Insect mode needs visible continuous whole-body movement. Pelvic-floor
    // isolation cannot honestly meet that contract under NomadicMethod's feet-only
    // rules. Intrinsic-hand work can meet it only when a wall is available.
    // Keep these exceptions anatomical and exact instead of inventing
    // secondary claims or artificial marching variants to make a quota pass.
    private static readonly HashSet<CanonicalMuscleGroup>
        InsectFineCoverageExceptions =
    [
        CanonicalMuscleGroup.PelvicFloorAndPerineum,
    ];

    private static readonly HashSet<CanonicalMuscleGroup>
        WallFreeInsectFineCoverageExceptions =
    [
        CanonicalMuscleGroup.IntrinsicHand,
    ];

    private sealed record ModifierRule(
        WorkoutModifiers Flag,
        Func<Exercise, bool> IsReviewed,
        Func<Exercise, WorkoutModifiers, bool> IsCompatibleForProfile);

    private static readonly ModifierRule[] Rules =
    [
        new(
            WorkoutModifiers.UpperBodyClothing,
            exercise => exercise.UpperBodyClothingRequirement !=
                ExerciseUpperBodyClothingRequirement.Unreviewed,
            (exercise, profile) => exercise.UpperBodyClothingRequirement switch
            {
                ExerciseUpperBodyClothingRequirement.ClothingRequired =>
                    profile.HasFlag(WorkoutModifiers.UpperBodyClothing),
                ExerciseUpperBodyClothingRequirement.BareUpperBodyRequired =>
                    !profile.HasFlag(WorkoutModifiers.UpperBodyClothing),
                ExerciseUpperBodyClothingRequirement.Agnostic => true,
                _ => false,
            }),
        new(
            WorkoutModifiers.HardFloor,
            exercise => exercise.HardFloorCompatibility !=
                ExerciseHardFloorCompatibility.Unreviewed,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.HardFloor) ||
                exercise.HardFloorCompatibility ==
                    ExerciseHardFloorCompatibility.Compatible),
        new(
            WorkoutModifiers.Insect,
            exercise => exercise.InsectCompatibility !=
                ExerciseInsectCompatibility.Unreviewed,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.Insect) ||
                exercise.InsectCompatibility ==
                    ExerciseInsectCompatibility.Compatible),
        new(
            WorkoutModifiers.Silence,
            _ => true,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.Silence) || exercise.Silent),
        new(
            WorkoutModifiers.Shy,
            exercise => exercise.ShyCompatibility !=
                ExerciseShyCompatibility.Unreviewed,
            (exercise, profile) =>
                !profile.HasFlag(WorkoutModifiers.Shy) ||
                exercise.ShyCompatibility ==
                    ExerciseShyCompatibility.Compatible),
        new(
            WorkoutModifiers.Mirror,
            IsMirrorMetadataReviewed,
            IsMirrorCompatible),
    ];

    private static readonly WorkoutModifiers SupportedModifierMask =
        Rules.Aggregate(
            WorkoutModifiers.TallMirror |
                WorkoutModifiers.Wall |
                WorkoutModifiers.SoleWallContact |
                WorkoutModifiers.Light,
            (mask, rule) => mask | rule.Flag);

    private static readonly IReadOnlyList<WorkoutModifiers> ProfilesForValidation =
        Array.AsReadOnly(CreatePairwiseValidationProfiles());

    public static WorkoutModifiers SupportedMask => SupportedModifierMask;

    public static IReadOnlyList<WorkoutModifiers> ValidationProfiles =>
        ProfilesForValidation;

    public static WorkoutModifiers GetPersistentSetupModifiers(
        WorkoutModifiers modifiers) =>
        Normalize(modifiers) & ~WorkoutModifiers.Light;

    public static int GetSessionMovementId(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.SessionMovementId > 0
            ? exercise.SessionMovementId
            : exercise.Id;
    }

    public static WorkoutModifiers Normalize(WorkoutModifiers modifiers)
    {
        WorkoutModifiers normalized = modifiers & SupportedModifierMask;
        if (!normalized.HasFlag(WorkoutModifiers.Mirror))
        {
            normalized &= ~WorkoutModifiers.TallMirror;
        }
        if (!normalized.HasFlag(WorkoutModifiers.Wall))
        {
            normalized &= ~WorkoutModifiers.SoleWallContact;
        }

        return normalized;
    }

    public static MirrorEquipment GetMirrorEquipment(WorkoutModifiers profile)
    {
        WorkoutModifiers normalized = Normalize(profile);
        if (!normalized.HasFlag(WorkoutModifiers.Mirror))
        {
            return MirrorEquipment.None;
        }

        return normalized.HasFlag(WorkoutModifiers.TallMirror)
            ? MirrorEquipment.Tall
            : MirrorEquipment.Compact;
    }

    public static WorkoutModifiers WithMirrorEquipment(
        WorkoutModifiers profile,
        MirrorEquipment equipment)
    {
        if (!Enum.IsDefined(equipment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null);
        }

        WorkoutModifiers withoutMirror = Normalize(profile) &
            ~(WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror);
        return equipment switch
        {
            MirrorEquipment.None => withoutMirror,
            MirrorEquipment.Compact => withoutMirror | WorkoutModifiers.Mirror,
            MirrorEquipment.Tall => withoutMirror |
                WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror,
            _ => throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null),
        };
    }

    public static WallEquipment GetWallEquipment(WorkoutModifiers profile)
    {
        WorkoutModifiers normalized = Normalize(profile);
        if (!normalized.HasFlag(WorkoutModifiers.Wall))
        {
            return WallEquipment.None;
        }

        return normalized.HasFlag(WorkoutModifiers.SoleWallContact)
            ? WallEquipment.SolesMayTouch
            : WallEquipment.SolesStayOff;
    }

    public static WorkoutModifiers WithWallEquipment(
        WorkoutModifiers profile,
        WallEquipment equipment)
    {
        if (!Enum.IsDefined(equipment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null);
        }

        WorkoutModifiers withoutWall = Normalize(profile) &
            ~(WorkoutModifiers.Wall | WorkoutModifiers.SoleWallContact);
        return equipment switch
        {
            WallEquipment.None => withoutWall,
            WallEquipment.SolesStayOff =>
                withoutWall | WorkoutModifiers.Wall,
            WallEquipment.SolesMayTouch =>
                withoutWall |
                    WorkoutModifiers.Wall |
                    WorkoutModifiers.SoleWallContact,
            _ => throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null),
        };
    }

    public static bool IsCatalogMetadataComplete(
        IEnumerable<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return exercises.All(exercise =>
            (!exercise.SoleWallContactRequired || exercise.WallRequired) &&
            Rules.All(rule => rule.IsReviewed(exercise)));
    }

    public static bool IsCompatible(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        WorkoutModifiers normalized = Normalize(profile);
        WallEquipment wallEquipment = GetWallEquipment(normalized);
        return (!exercise.WallRequired ||
                wallEquipment != WallEquipment.None) &&
            (!exercise.SoleWallContactRequired ||
                wallEquipment == WallEquipment.SolesMayTouch) &&
            Rules.All(rule =>
            rule.IsCompatibleForProfile(exercise, normalized));
    }

    public static bool IsWallPreferred(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.WallRequired &&
            GetWallEquipment(profile) != WallEquipment.None;
    }

    public static int GetEquipmentPreferenceCount(
        Exercise exercise,
        WorkoutModifiers profile) =>
        (IsWallPreferred(exercise, profile) ? 1 : 0) +
        (IsMirrorPreferred(exercise, profile) ? 1 : 0);

    public static IReadOnlyList<WorkoutWallRequiredCatalogDeficiency>
        FindWallRequiredCatalogDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        int movementCount = exercises
            .Where(exercise =>
                exercise.WallRequired &&
                !exercise.SoleWallContactRequired)
            .Select(GetSessionMovementId)
            .Distinct()
            .Count();
        return movementCount >= MinimumWallRequiredSessionMovements
            ? []
            :
            [
                new WorkoutWallRequiredCatalogDeficiency(
                    movementCount,
                    MinimumWallRequiredSessionMovements),
            ];
    }

    public static IReadOnlyList<WorkoutSoleWallContactRequiredCatalogDeficiency>
        FindSoleWallContactRequiredCatalogDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        int movementCount = exercises
            .Where(exercise => exercise.SoleWallContactRequired)
            .Select(GetSessionMovementId)
            .Distinct()
            .Count();
        return movementCount >= MinimumSoleWallContactRequiredSessionMovements
            ? []
            :
            [
                new WorkoutSoleWallContactRequiredCatalogDeficiency(
                    movementCount,
                    MinimumSoleWallContactRequiredSessionMovements),
            ];
    }

    public static bool IsMirrorRelevant(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        return exercise.MirrorRelationship is
            ExerciseMirrorRelationship.MirrorOnly or
            ExerciseMirrorRelationship.BenefitsGreatly;
    }

    public static bool IsMirrorPreferred(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        MirrorEquipment equipment = GetMirrorEquipment(profile);
        if (equipment == MirrorEquipment.None)
        {
            return false;
        }

        return exercise.MirrorRelationship switch
        {
            ExerciseMirrorRelationship.MirrorOnly =>
                IsMirrorCompatible(exercise, Normalize(profile)),
            ExerciseMirrorRelationship.BenefitsGreatly =>
                exercise.MinimumMirrorCoverage ==
                    ExerciseMirrorCoverage.UpperBody ||
                equipment == MirrorEquipment.Tall,
            _ => false,
        };
    }

    public static bool IsSelectable(
        Exercise exercise,
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        ArgumentNullException.ThrowIfNull(group);
        return WorkoutCoveragePolicy.IsSelectable(exercise, group) &&
            IsCompatible(exercise, profile);
    }

    public static IReadOnlyList<WorkoutModifierPairCoverageDeficiency>
        FindPairwiseCoverageDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        return MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups.SelectMany(group =>
                    GetModifierRulePairs().SelectMany(pair =>
                        GetRuleStateProfiles(pair.First).SelectMany(firstState =>
                            GetRuleStateProfiles(pair.Second).Select(secondState =>
                            {
                                WorkoutModifiers profile = Normalize(
                                    firstState | secondState);
                                // Availability includes every selectable mirror relationship.
                                // Mirror benefit populations are reported separately as diagnostics.
                                return new
                                {
                                    Minutes = minutes,
                                    Group = group,
                                    FirstRule = pair.First,
                                    FirstEnabled = firstState !=
                                        WorkoutModifiers.None,
                                    SecondRule = pair.Second,
                                    SecondEnabled = secondState !=
                                        WorkoutModifiers.None,
                                    MirrorEquipment = GetMirrorEquipment(profile),
                                    RequiredCount =
                                        IsCoverageException(
                                            group,
                                            profile)
                                            ? 0
                                            : GetMinimumExercisesPerPairStatePerGroup(
                                                minutes),
                                    Count = exercises
                                        .Where(exercise =>
                                            Rules.All(rule =>
                                                rule.IsReviewed(exercise)) &&
                                            IsSequenceUnitEligible(
                                                exercise,
                                                exercisesById,
                                                group,
                                                profile))
                                        .Select(GetSessionMovementId)
                                        .Distinct()
                                        .Count(),
                                };
                            })))))
            .Where(result => result.Count < result.RequiredCount)
            .Select(result => new WorkoutModifierPairCoverageDeficiency(
                result.Minutes,
                result.Group.Id,
                result.Group.DisplayName,
                result.FirstRule.Flag,
                result.FirstEnabled,
                result.SecondRule.Flag,
                result.SecondEnabled,
                result.MirrorEquipment,
                result.Count,
                result.RequiredCount))
            .ToArray();
    }

    public static IReadOnlyList<WorkoutHardFloorCategoryCoverageDeficiency>
        FindHardFloorCategoryCoverageDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        ExerciseHardFloorCompatibility[] requiredCategories =
        [
            ExerciseHardFloorCompatibility.Compatible,
        ];
        (WorkoutModifiers Modifier, bool Enabled)[] partnerStates =
        [
            (WorkoutModifiers.Insect, false),
            (WorkoutModifiers.Insect, true),
            (WorkoutModifiers.Silence, false),
            (WorkoutModifiers.Silence, true),
            (WorkoutModifiers.Mirror, false),
        ];

        return MassGroupingTaxonomy.SupportedMinutes
            .SelectMany(minutes =>
                MassGroupingTaxonomy.GetResolution(minutes).Groups.SelectMany(group =>
                    requiredCategories.SelectMany(category =>
                        partnerStates.Select(partnerState =>
                        {
                            WorkoutModifiers profile = WorkoutModifiers.HardFloor;
                            if (partnerState.Enabled)
                            {
                                profile |= partnerState.Modifier;
                            }
                            profile = Normalize(profile);

                            int matchingExerciseCount = exercises
                                .Where(exercise =>
                                    exercise.HardFloorCompatibility == category &&
                                    IsSequenceHardFloorCategory(
                                        exercise,
                                        exercisesById,
                                        category) &&
                                    IsSequenceUnitEligible(
                                        exercise,
                                        exercisesById,
                                        group,
                                        profile))
                                .Select(GetSessionMovementId)
                                .Distinct()
                                .Count();
                            return new WorkoutHardFloorCategoryCoverageDeficiency(
                                minutes,
                                group.Id,
                                group.DisplayName,
                                category,
                                partnerState.Modifier,
                                partnerState.Enabled,
                                matchingExerciseCount,
                                IsCoverageException(
                                    group,
                                    profile)
                                    ? 0
                                    : GetMinimumExercisesPerPairStatePerGroup(
                                        minutes));
                        }))))
            .Where(deficiency =>
                deficiency.MatchingExerciseCount <
                    deficiency.RequiredExerciseCount)
            .ToArray();
    }

    private static bool IsCoverageException(
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        if (AcceptedCoverageExceptions.Any(exception =>
            exception.GroupId == group.SelectionKey &&
            (profile & exception.RequiredModifiers) == exception.RequiredModifiers))
        {
            return true;
        }

        if (!profile.HasFlag(WorkoutModifiers.Insect) ||
            group.CanonicalGroups.Count == 0)
        {
            return false;
        }

        bool wallAvailable = profile.HasFlag(WorkoutModifiers.Wall);
        return group.CanonicalGroups.All(canonicalGroup =>
            InsectFineCoverageExceptions.Contains(canonicalGroup) ||
            (!wallAvailable &&
                WallFreeInsectFineCoverageExceptions.Contains(canonicalGroup)));
    }

    public static bool IsSelectionGroupAvailable(
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(group);
        return !IsCoverageException(group, profile);
    }

    public static IReadOnlyList<WorkoutModifierMaterialityDeficiency>
        FindMaterialityDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        IReadOnlyList<WorkoutGroup> buckets = MassGroupingTaxonomy
            .GetResolution(MaterialityResolutionMinutes)
            .Groups;
        Exercise[] reviewedExercises = exercises
            .Where(exercise => Rules.All(rule => rule.IsReviewed(exercise)))
            .ToArray();
        IReadOnlyDictionary<int, Exercise> reviewedExercisesById =
            reviewedExercises.ToDictionary(exercise => exercise.Id);

        return GetMaterialityEdges()
            .Select(edge =>
            {
                WorkoutModifiers contextProfile = Normalize(edge.ContextProfile);
                WorkoutModifiers modifiedProfile = Normalize(
                    contextProfile | edge.EnabledStateProfile);
                HashSet<int> baselineExerciseIds = GetSelectableExerciseIds(
                    reviewedExercises,
                    buckets,
                    contextProfile);
                HashSet<int> modifiedExerciseIds = GetSelectableExerciseIds(
                    reviewedExercises,
                    buckets,
                    modifiedProfile);
                bool isMirror = edge.Rule.Flag == WorkoutModifiers.Mirror;
                HashSet<int> materialExerciseIds = isMirror
                    ? reviewedExercises
                        .Where(exercise =>
                            IsMirrorPreferred(exercise, modifiedProfile) &&
                            buckets.Any(bucket => IsSequenceUnitEligible(
                                exercise,
                                reviewedExercisesById,
                                bucket,
                                modifiedProfile)))
                        .Select(GetSessionMovementId)
                        .ToHashSet()
                    : baselineExerciseIds
                        .Except(modifiedExerciseIds)
                        .ToHashSet();
                int requiredMaterialExerciseCount = Math.Max(
                    MinimumMaterialExercises,
                    GetPercentageFloor(
                        isMirror
                            ? modifiedExerciseIds.Count
                            : baselineExerciseIds.Count,
                        MinimumMaterialExercisePercent));
                int affectedBucketCount = buckets.Count(bucket => isMirror
                    ? reviewedExercises.Any(exercise =>
                        IsMirrorPreferred(exercise, modifiedProfile) &&
                        IsSequenceUnitEligible(
                            exercise,
                            reviewedExercisesById,
                            bucket,
                            modifiedProfile))
                    : GetSelectableExerciseIds(
                            reviewedExercises,
                            [bucket],
                            contextProfile)
                        .Except(GetSelectableExerciseIds(
                            reviewedExercises,
                            [bucket],
                            modifiedProfile))
                        .Any());
                int requiredAffectedBucketCount = GetPercentageFloor(
                    buckets.Count,
                    MinimumAffectedBucketPercent);

                return new WorkoutModifierMaterialityDeficiency(
                    edge.Rule.Flag,
                    contextProfile,
                    modifiedProfile,
                    baselineExerciseIds.Count,
                    modifiedExerciseIds.Count,
                    materialExerciseIds.Count,
                    requiredMaterialExerciseCount,
                    affectedBucketCount,
                    requiredAffectedBucketCount);
            })
            .Where(deficiency =>
                deficiency.MaterialExerciseCount <
                    deficiency.RequiredMaterialExerciseCount ||
                deficiency.AffectedBucketCount <
                    deficiency.RequiredAffectedBucketCount)
            .ToArray();
    }

    public static IReadOnlyList<WorkoutMuscularDemandCoverageDeficiency>
        FindMuscularDemandCoverageDeficiencies(
            IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        int[] requiredCategories =
        [
            Exercise.MinimumMuscularDemand,
            Exercise.MaximumMuscularDemand,
        ];

        int minutes = BroadCoverageResolutionMinutes;
        return MassGroupingTaxonomy.GetResolution(minutes).Groups
            .SelectMany(group =>
                requiredCategories.SelectMany(muscularDemand =>
                    ValidationProfiles.Select(profile =>
                    {
                        int matchingExerciseCount = exercises
                            .Where(exercise =>
                                IsSequenceCompatible(
                                    exercise,
                                    exercisesById,
                                    profile) &&
                                IsSequenceMuscularDemandCategoryForGroup(
                                    exercise,
                                    exercisesById,
                                    group,
                                    muscularDemand))
                            .Select(GetSessionMovementId)
                            .Distinct()
                            .Count();
                        return new WorkoutMuscularDemandCoverageDeficiency(
                            minutes,
                            group.Id,
                            group.DisplayName,
                            muscularDemand,
                            profile,
                            matchingExerciseCount,
                            MinimumExercisesPerMuscularDemandCategoryPerGroup);
                    })))
            .Where(deficiency =>
                deficiency.MatchingExerciseCount <
                    deficiency.RequiredExerciseCount)
            .ToArray();
    }

    public static int GetMinimumExercisesPerPairStatePerGroup(int minutes) =>
        minutes == BroadCoverageResolutionMinutes
            ? MinimumExercisesPerBroadPairStatePerGroup
            : MinimumExercisesPerFinePairStatePerGroup;

    public static IReadOnlyList<WorkoutProfileLineupDeficiency>
        FindDistinctLineupDeficiencies(IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return ExerciseSessionService.SupportedWorkoutMinutes
            .SelectMany(minutes =>
            {
                IReadOnlyList<WorkoutGroup> resolutionGroups = MassGroupingTaxonomy
                    .GetResolution(minutes > 30 ? 30 : minutes)
                    .Groups;
                return ValidationProfiles.Select(profile =>
                {
                    WorkoutGroup[] groups = resolutionGroups
                        .Where(group => IsSelectionGroupAvailable(group, profile))
                        .ToArray();
                    return new
                    {
                        Minutes = minutes,
                        Profile = profile,
                        MaximumDistinctExerciseCount = GetMaximumDistinctLineupSize(
                            exercises,
                            groups,
                            profile,
                            minutes),
                        RequiredDistinctExerciseCount = groups.Length,
                    };
                });
            })
            .Where(result =>
                result.MaximumDistinctExerciseCount <
                    result.RequiredDistinctExerciseCount)
            .Select(result => new WorkoutProfileLineupDeficiency(
                result.Minutes,
                result.Profile,
                result.MaximumDistinctExerciseCount,
                result.RequiredDistinctExerciseCount))
            .ToArray();
    }

    public static IReadOnlyList<WorkoutProfileCompletionDeficiency>
        FindCompleteLineupDeficiencies(IReadOnlyCollection<Exercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        return ExerciseSessionService.SupportedWorkoutMinutes.SelectMany(minutes =>
            ValidationProfiles.Select(profile =>
            {
                WorkoutGroup[] groups = MassGroupingTaxonomy
                    .GetResolution(Math.Min(minutes, 30)).Groups
                    .Where(group => IsSelectionGroupAvailable(group, profile)).ToArray();
                return new WorkoutProfileCompletionDeficiency(minutes, profile,
                    GetMaximumCompleteLineupSize(exercises, groups, profile, minutes),
                    groups.Length);
            })).Where(result => result.MaximumCoveredGroupCount < result.RequiredGroupCount)
            .ToArray();
    }

    public static int GetRequiredDistinctLineupSize(
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers profile)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return groups.Count(group => IsSelectionGroupAvailable(group, profile));
    }

    public static int GetMaximumDistinctLineupSize(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers profile,
        int? workoutMinutes = null) =>
        GetMaximumLineupSize(exercises, groups, profile, workoutMinutes,
            allowRepeatedMovements: false);

    public static int GetMaximumCompleteLineupSize(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers profile,
        int? workoutMinutes = null) =>
        GetMaximumLineupSize(exercises, groups, profile, workoutMinutes,
            allowRepeatedMovements: true);

    private static int GetMaximumLineupSize(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers profile,
        int? workoutMinutes,
        bool allowRepeatedMovements)
    {
        ArgumentNullException.ThrowIfNull(exercises);
        ArgumentNullException.ThrowIfNull(groups);
        int availableBlocks = workoutMinutes ?? groups.Count;
        if (availableBlocks < groups.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(workoutMinutes));
        }

        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        int oneBlockLineupSize = GetMaximumOneBlockLineupSize(
            exercises,
            exercisesById,
            groups,
            profile,
            allowRepeatedMovements);
        if (oneBlockLineupSize == groups.Count)
        {
            return groups.Count;
        }

        var groupIndexById = groups
            .Select((group, index) => (group.Id, index))
            .ToDictionary(entry => entry.Id, entry => entry.index);
        var candidates = new List<AtomicSequenceCandidate>();
        foreach (Exercise exercise in exercises.Where(exercise =>
                     IsSequenceCompatible(exercise, exercisesById, profile)))
        {
            foreach (WorkoutGroup[] placement in
                     WorkoutSequencePolicy.GetPlacementOptions(
                         exercise,
                         exercisesById,
                         groups))
            {
                if (exercise.SequenceBlocks.Length +
                        (groups.Count - placement.Length) > availableBlocks)
                {
                    continue;
                }
                ulong coverageMask = placement.Aggregate(
                    0UL,
                    (mask, group) => mask | 1UL << groupIndexById[group.Id]);
                var utilities = new BigInteger[groups.Count];
                foreach (WorkoutGroup coveredGroup in placement)
                {
                    utilities[groupIndexById[coveredGroup.Id]] = BigInteger.One;
                }
                candidates.Add(new AtomicSequenceCandidate(
                    exercise.Id,
                    GetSessionMovementId(exercise),
                    coverageMask,
                    exercise.SequenceBlocks.Length,
                    utilities,
                    exercise.Id));
            }
        }

        // Empty one-block placements make the solver return the maximum number
        // of real muscle slots that can be filled atomically, rather than only
        // answering whether a complete lineup exists.
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var utilities = new BigInteger[groups.Count];
            candidates.Add(new AtomicSequenceCandidate(
                int.MinValue + groupIndex,
                int.MinValue + groupIndex,
                1UL << groupIndex,
                1,
                utilities,
                int.MaxValue - groupIndex));
        }

        AtomicSequenceLineup lineup = (allowRepeatedMovements
            ? AtomicSequenceLineupSolver.SolveAllowingRepeatedMovements(
                groups.Count, availableBlocks, candidates)
            : AtomicSequenceLineupSolver.Solve(groups.Count, availableBlocks, candidates)) ?? throw new InvalidOperationException(
                "Atomic lineup validation could not place empty muscle slots.");
        return lineup.ExerciseIdByGroupIndex.Values.Count(exerciseId =>
            exerciseId > 0);
    }

    private static int GetMaximumOneBlockLineupSize(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        IReadOnlyList<WorkoutGroup> groups,
        WorkoutModifiers profile,
        bool allowRepeatedMovements)
    {
        var candidateMovementIdsByGroupId = groups.ToDictionary(
            group => group.Id,
            _ => new HashSet<int>());
        foreach (Exercise exercise in exercises)
        {
            if (exercise.SequenceBlocks.Length != 1 ||
                !IsSequenceCompatible(exercise, exercisesById, profile))
            {
                continue;
            }

            int movementId = GetSessionMovementId(exercise);
            foreach (WorkoutGroup[] option in
                     WorkoutSequencePolicy.GetPlacementOptions(
                         exercise,
                         exercisesById,
                         groups))
            {
                if (option.Length == 1 &&
                    candidateMovementIdsByGroupId.TryGetValue(
                        option[0].Id,
                        out HashSet<int>? candidateMovementIds))
                {
                    candidateMovementIds.Add(movementId);
                }
            }
        }

        int[][] candidateMovementIdsByGroup = groups
            .Select(group => candidateMovementIdsByGroupId[group.Id].ToArray())
            .OrderBy(candidateIds => candidateIds.Length)
            .ToArray();
        if (allowRepeatedMovements)
            return candidateMovementIdsByGroup.Count(candidateIds => candidateIds.Length > 0);
        var assignedGroupByMovementId = new Dictionary<int, int>();
        int matchedGroupCount = 0;
        for (int groupIndex = 0;
             groupIndex < candidateMovementIdsByGroup.Length;
             groupIndex++)
        {
            if (TryAssignDistinctOneBlockMovement(
                    groupIndex,
                    candidateMovementIdsByGroup,
                    assignedGroupByMovementId,
                    []))
            {
                matchedGroupCount++;
            }
        }
        return matchedGroupCount;
    }

    private static bool TryAssignDistinctOneBlockMovement(
        int groupIndex,
        IReadOnlyList<int[]> candidateMovementIdsByGroup,
        IDictionary<int, int> assignedGroupByMovementId,
        HashSet<int> visitedMovementIds)
    {
        foreach (int movementId in candidateMovementIdsByGroup[groupIndex])
        {
            if (!visitedMovementIds.Add(movementId))
            {
                continue;
            }
            if (!assignedGroupByMovementId.TryGetValue(
                    movementId,
                    out int assignedGroupIndex) ||
                TryAssignDistinctOneBlockMovement(
                    assignedGroupIndex,
                    candidateMovementIdsByGroup,
                    assignedGroupByMovementId,
                    visitedMovementIds))
            {
                assignedGroupByMovementId[movementId] = groupIndex;
                return true;
            }
        }
        return false;
    }

    private static bool IsSequenceCompatible(
        Exercise exercise,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutModifiers profile)
    {
        if (exercise.SequenceBlocks.Length == 0)
        {
            return false;
        }

        return exercise.SequenceBlocks
            .Select(block => exercisesById.GetValueOrDefault(block.ExerciseId))
            .Where(member => member is not null)
            .DistinctBy(member => member!.Id)
            .Count() == exercise.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .Count() &&
            exercise.SequenceBlocks.All(block =>
                exercisesById.TryGetValue(block.ExerciseId, out Exercise? member) &&
                Rules.All(rule => rule.IsReviewed(member)) &&
                IsCompatible(member, profile));
    }

    private static bool IsSequenceUnitEligible(
        Exercise exercise,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutGroup group,
        WorkoutModifiers profile)
    {
        if (!WorkoutSequencePolicy.IsSelectable(exercise, exercisesById, group))
        {
            return false;
        }

        return IsSequenceCompatible(exercise, exercisesById, profile);
    }

    private static bool IsSequenceHardFloorCategory(
        Exercise exercise,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        ExerciseHardFloorCompatibility category)
    {
        return exercise.SequenceBlocks.Length > 0 &&
            exercise.SequenceBlocks
                .Select(block => block.ExerciseId)
                .Distinct()
                .All(exerciseId =>
                    exercisesById.TryGetValue(exerciseId, out Exercise? member) &&
                    member.HardFloorCompatibility == category);
    }

    private static bool IsSequenceMuscularDemandCategoryForGroup(
        Exercise exercise,
        IReadOnlyDictionary<int, Exercise> exercisesById,
        WorkoutGroup group,
        int muscularDemand)
    {
        Exercise[] members = exercise.SequenceBlocks
            .Select(block => exercisesById.GetValueOrDefault(block.ExerciseId))
            .Where(member => member is not null)
            .Select(member => member!)
            .DistinctBy(member => member.Id)
            .ToArray();
        if (members.Length == 0)
        {
            return false;
        }

        return muscularDemand switch
        {
            Exercise.MinimumMuscularDemand => members.All(member =>
                    member.MuscularDemand == Exercise.MinimumMuscularDemand) &&
                members.Any(member =>
                    group.CanonicalGroups.Contains(
                        member.PrimaryCanonicalGroup)),
            Exercise.MaximumMuscularDemand => members.Any(member =>
                member.MuscularDemand == Exercise.MaximumMuscularDemand &&
                group.CanonicalGroups.Contains(member.PrimaryCanonicalGroup)),
            _ => false,
        };
    }

    private static WorkoutModifiers[] CreatePairwiseValidationProfiles()
    {
        var profiles = new List<WorkoutModifiers> { WorkoutModifiers.None };
        profiles.AddRange(Rules.SelectMany(GetRuleStateProfiles));
        profiles.AddRange(GetModifierRulePairs().SelectMany(pair =>
            GetRuleStateProfiles(pair.First)
                .SelectMany(firstState => GetRuleStateProfiles(pair.Second)
                    .Select(secondState => Normalize(firstState | secondState)))));
        return profiles.Distinct().ToArray();
    }

    private static IEnumerable<(ModifierRule First, ModifierRule Second)>
        GetModifierRulePairs()
    {
        for (int firstIndex = 0; firstIndex < Rules.Length - 1; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1;
                 secondIndex < Rules.Length;
                 secondIndex++)
            {
                yield return (Rules[firstIndex], Rules[secondIndex]);
            }
        }
    }

    private static bool IsMirrorMetadataReviewed(Exercise exercise)
    {
        return exercise.MirrorRelationship switch
        {
            ExerciseMirrorRelationship.MirrorOnly =>
                string.Equals(
                    exercise.Equipment,
                    "Mirror",
                    StringComparison.Ordinal) &&
                exercise.MinimumMirrorCoverage is
                    ExerciseMirrorCoverage.UpperBody or
                    ExerciseMirrorCoverage.FullBody,
            ExerciseMirrorRelationship.BenefitsGreatly =>
                string.Equals(
                    exercise.Equipment,
                    "None",
                    StringComparison.Ordinal) &&
                exercise.MinimumMirrorCoverage is
                    ExerciseMirrorCoverage.UpperBody or
                    ExerciseMirrorCoverage.FullBody,
            ExerciseMirrorRelationship.Agnostic =>
                string.Equals(
                    exercise.Equipment,
                    "None",
                    StringComparison.Ordinal) &&
                exercise.MinimumMirrorCoverage == ExerciseMirrorCoverage.None,
            _ => false,
        };
    }

    private static bool IsMirrorCompatible(
        Exercise exercise,
        WorkoutModifiers profile)
    {
        if (exercise.MirrorRelationship != ExerciseMirrorRelationship.MirrorOnly)
        {
            return true;
        }

        return GetMirrorEquipment(profile) switch
        {
            MirrorEquipment.None => false,
            MirrorEquipment.Compact =>
                exercise.MinimumMirrorCoverage ==
                    ExerciseMirrorCoverage.UpperBody,
            MirrorEquipment.Tall => true,
            _ => false,
        };
    }

    private static IEnumerable<WorkoutModifiers> GetRuleStateProfiles(
        ModifierRule rule)
    {
        yield return WorkoutModifiers.None;
        yield return rule.Flag;
        if (rule.Flag == WorkoutModifiers.Mirror)
        {
            yield return WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror;
        }
    }

    private static IEnumerable<(
        ModifierRule Rule,
        WorkoutModifiers ContextProfile,
        WorkoutModifiers EnabledStateProfile)>
        GetMaterialityEdges()
    {
        ModifierRule[] materialityRules = Rules
            .Where(rule => rule.Flag != WorkoutModifiers.UpperBodyClothing)
            .ToArray();
        foreach (ModifierRule rule in materialityRules)
        {
            foreach (WorkoutModifiers enabledState in
                     GetRuleStateProfiles(rule).Where(state =>
                         state != WorkoutModifiers.None))
            {
                yield return (rule, WorkoutModifiers.None, enabledState);
            }
        }

        for (int firstIndex = 0;
             firstIndex < materialityRules.Length - 1;
             firstIndex++)
        {
            for (int secondIndex = firstIndex + 1;
                 secondIndex < materialityRules.Length;
                 secondIndex++)
            {
                ModifierRule first = materialityRules[firstIndex];
                ModifierRule second = materialityRules[secondIndex];
                foreach (WorkoutModifiers firstEnabledState in
                     GetRuleStateProfiles(first).Where(state =>
                         state != WorkoutModifiers.None))
                {
                    foreach (WorkoutModifiers secondEnabledState in
                         GetRuleStateProfiles(second).Where(state =>
                             state != WorkoutModifiers.None))
                    {
                        yield return (
                            first,
                            secondEnabledState,
                            firstEnabledState);
                        yield return (
                            second,
                            firstEnabledState,
                            secondEnabledState);
                    }
                }
            }
        }
    }

    private static HashSet<int> GetSelectableExerciseIds(
        IReadOnlyCollection<Exercise> exercises,
        IReadOnlyList<WorkoutGroup> buckets,
        WorkoutModifiers profile)
    {
        IReadOnlyDictionary<int, Exercise> exercisesById = exercises
            .ToDictionary(exercise => exercise.Id);
        return exercises
            .Where(exercise => buckets.Any(bucket =>
                IsSequenceUnitEligible(
                    exercise,
                    exercisesById,
                    bucket,
                    profile)))
            .Select(GetSessionMovementId)
            .ToHashSet();
    }

    private static int GetPercentageFloor(int count, int percent)
    {
        return (int)Math.Ceiling(count * percent / 100d);
    }

}
