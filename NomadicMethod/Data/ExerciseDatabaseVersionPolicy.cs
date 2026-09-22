namespace NomadicMethod.Data;

internal static class ExerciseDatabaseVersionPolicy
{
    internal const int MinimumNonDestructiveVersion = 14;

    internal const int CurrentVersion = 95;

    internal static bool IsSupportedNonDestructiveUpgrade(
        int oldVersion,
        int newVersion) =>
        oldVersion >= MinimumNonDestructiveVersion &&
        oldVersion < CurrentVersion &&
        newVersion == CurrentVersion;
}
