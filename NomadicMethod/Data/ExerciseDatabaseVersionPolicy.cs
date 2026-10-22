using NomadicMethod.Services;

namespace NomadicMethod.Data;

internal static class ExerciseDatabaseVersionPolicy
{
    internal const int MinimumNonDestructiveVersion = 14;

    // SQLite stores the bundled catalog as well as its schema. Refresh it on
    // every catalog revision, before loading assets that reference new records.
    // At catalog revision 88 the database version was 104. Retain that historical
    // offset; increase it only for future schema-only upgrades.
    private const int SchemaVersionOffset = 16;
    internal const int CurrentVersion =
        CatalogMigrationRules.CurrentCatalogRevision + SchemaVersionOffset;

    internal static bool IsSupportedNonDestructiveUpgrade(
        int oldVersion,
        int newVersion) =>
        oldVersion >= MinimumNonDestructiveVersion &&
        oldVersion < CurrentVersion &&
        newVersion == CurrentVersion;
}
