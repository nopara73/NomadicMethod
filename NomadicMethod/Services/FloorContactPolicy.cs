using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class FloorContactPolicy
{
    public const ExerciseFloorContact KnownContacts =
        ExerciseFloorContact.Feet | ExerciseFloorContact.Hands |
        ExerciseFloorContact.Forearms | ExerciseFloorContact.Elbows |
        ExerciseFloorContact.Knees | ExerciseFloorContact.Head |
        ExerciseFloorContact.Torso | ExerciseFloorContact.Shoulders |
        ExerciseFloorContact.Hips | ExerciseFloorContact.Thighs |
        ExerciseFloorContact.LowerLegs;

    // Upper-body clothing protects the trunk and shoulders. Hands, forearms,
    // and elbows do not require a shirt (owner clarification, 15 October 2026).
    public const ExerciseFloorContact ClothingRequiredContacts =
        ExerciseFloorContact.Torso | ExerciseFloorContact.Shoulders;

    public const ExerciseFloorContact HardFloorForbiddenContacts =
        ExerciseFloorContact.Knees | ExerciseFloorContact.Elbows |
        ExerciseFloorContact.Head | ClothingRequiredContacts;

    public static bool IsMetadataReviewed(Exercise exercise) =>
        exercise.FloorContactPoints != ExerciseFloorContact.Unreviewed &&
        (exercise.FloorContactPoints & ~KnownContacts) == 0 &&
        exercise.OnlyFeetTouchGround ==
            (exercise.FloorContactPoints == ExerciseFloorContact.Feet);

    public static bool RequiresFloor(Exercise exercise) =>
        IsMetadataReviewed(exercise) && !exercise.OnlyFeetTouchGround;

    public static bool IsCompatible(Exercise exercise, WorkoutModifiers profile)
    {
        if (!IsMetadataReviewed(exercise)) return false;
        if (!profile.HasFlag(WorkoutModifiers.Floor) && RequiresFloor(exercise))
            return false;
        if (!profile.HasFlag(WorkoutModifiers.UpperBodyClothing) &&
            (exercise.FloorContactPoints & ClothingRequiredContacts) != 0)
            return false;
        if (profile.HasFlag(WorkoutModifiers.HardFloor) &&
            (exercise.FloorContactPoints & HardFloorForbiddenContacts) != 0)
            return false;
        return true;
    }
}
