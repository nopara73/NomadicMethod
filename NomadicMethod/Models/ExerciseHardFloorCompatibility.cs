namespace NomadicMethod.Models;

/// <summary>
/// Whether an exercise is ergonomic on a rigid, slippery floor.
/// Compatibility therefore covers both impact comfort and
/// slipping risk.
/// </summary>
public enum ExerciseHardFloorCompatibility
{
    Unreviewed,
    Compatible,
    Incompatible,
}
