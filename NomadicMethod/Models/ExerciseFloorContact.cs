namespace NomadicMethod.Models;

// All contact occurring during the complete demonstrated movement, including
// transitions. These are physical contact points, not muscle-training claims.
[Flags]
public enum ExerciseFloorContact
{
    Unreviewed = 0,
    Feet = 1,
    Hands = 2,
    Forearms = 4,
    Elbows = 8,
    Knees = 16,
    Head = 32,
    Torso = 64,
    Shoulders = 128,
    Hips = 256,
    Thighs = 512,
    LowerLegs = 1024,
}
