namespace NomadicMethod.Models;

[Flags]
public enum WorkoutModifiers
{
    None = 0,
    Insect = 1,
    Silence = 2,
    Mirror = 4,
    // Internal qualifier for the Mirror equipment modifier. A compact mirror is
    // represented by Mirror; a tall mirror by Mirror | TallMirror. The
    // qualifier is never valid without Mirror.
    TallMirror = 8,
    // A rigid, slippery floor. This is one modifier;
    // slipperiness is not a separate equipment state.
    HardFloor = 16,
    Wall = 32,
    // Internal qualifier for the Wall equipment modifier. Wall alone excludes
    // movements that require sole-to-wall contact; Wall | SoleWallContact
    // allows them. The qualifier is never valid without Wall.
    SoleWallContact = 64,
    // Whether the user is currently wearing upper-body clothing. This is a
    // physical setup condition: some contact exercises require clothing while
    // a small set of physique-inspection exercises require a bare upper body.
    UpperBodyClothing = 128,
    // A session-scoped workout-intensity choice. Unlike the physical setup and
    // equipment flags, Light changes candidate priority rather than exercise
    // compatibility. It is not a quota axis; demand-zero availability is
    // audited independently across the existing modifier profiles.
    Light = 256,
    // Restricts the session to movements reviewed as less conspicuous in a
    // shared space, including around other people who are exercising.
    Shy = 512,
}
