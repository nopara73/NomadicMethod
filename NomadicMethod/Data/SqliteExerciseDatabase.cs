using System.Globalization;
using System.Text.Json;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Data;

public sealed class SqliteExerciseDatabase : SQLiteOpenHelper, IExerciseDatabase
{
    private const string DatabaseFileName = "nomadic_method_exercises.db";
    private const int DatabaseVersion = ExerciseDatabaseVersionPolicy.CurrentVersion;
    private const string ExerciseTable = "exercises";
    private const string CanonicalGroupTable = "canonical_muscle_groups";
    private const string ExerciseCanonicalGroupTable = "exercise_canonical_groups";
    private const string ExerciseSequenceBlockTable = "exercise_sequence_blocks";
    private const string WorkoutBucketTable = "workout_buckets";
    private const string RollupTable = "canonical_group_rollups";
    private const string CatalogAsset = "exercises.json";
    private static readonly string[] ExerciseColumns =
    [
        "id",
        "name",
        "video",
        "practice",
        "motion_profile",
        "score",
        "muscular_demand",
        "only_feet_touch_ground",
        "shoe_agnostic",
        "max_space_meters",
        "equipment",
        "silent",
        "exercise_mode",
        "presentation",
        "hold_frame_percent",
        "side_sequence",
        "direction_sequence",
        "insect_compatibility",
        "hard_floor_compatibility",
        "upper_body_clothing_requirement",
        "shy_compatibility",
        "mirror_relationship",
        "mirror_coverage",
        "wall_required",
        "sole_wall_contact_required",
        "session_movement_id",
    ];

    private readonly Context _context;
    private IReadOnlyList<Exercise>? _exercises;

    public SqliteExerciseDatabase(Context context)
        : base(context, DatabaseFileName, null, DatabaseVersion)
    {
        _context = context.ApplicationContext ?? context;
    }

    public IReadOnlyList<Exercise> Exercises =>
        _exercises ??= LoadExercises();

    public override void OnConfigure(SQLiteDatabase? database)
    {
        base.OnConfigure(database);
        database?.SetForeignKeyConstraintsEnabled(true);
    }

    public override void OnCreate(SQLiteDatabase? database)
    {
        ArgumentNullException.ThrowIfNull(database);

        CreateExerciseSchema(database);
        CreateExerciseSequenceSchema(database);
        CreateMassGroupingSchema(database);
        InsertTaxonomy(database);
        InsertCatalog(database, ReadAndValidateBundledCatalog());
    }

    public override void OnUpgrade(SQLiteDatabase? database, int oldVersion, int newVersion)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (!ExerciseDatabaseVersionPolicy.IsSupportedNonDestructiveUpgrade(
                oldVersion,
                newVersion))
        {
            throw new NotSupportedException(
                $"No non-destructive exercise database migration exists from " +
                $"{oldVersion} to {newVersion}.");
        }

        Exercise[] catalog = ReadAndValidateBundledCatalog();
        Dictionary<int, StoredExerciseSnapshot> existingExercises =
            ReadExistingExercises(database);
        IReadOnlySet<int> preservedExerciseIds =
            CatalogMigrationRules.ValidatePreservedCatalog(catalog, existingExercises);
        Dictionary<int, StoredExerciseSnapshot> preservedExercises = existingExercises
            .Where(entry => preservedExerciseIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);

        database.BeginTransaction();
        try
        {
            if (oldVersion < 18)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN side_sequence TEXT NOT NULL " +
                    "DEFAULT 'Continuous' CHECK (side_sequence IN " +
                    "('Continuous', 'Alternating', " +
                    "'ScreenLeftThenRight', 'ScreenRightThenLeft'))");
            }
            if (oldVersion < 20)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN presentation TEXT NOT NULL " +
                    "DEFAULT 'Motion' CHECK (presentation IN ('Motion', 'Still'))");
            }
            if (oldVersion < 21)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN direction_sequence TEXT NOT NULL " +
                    "DEFAULT 'None' CHECK (direction_sequence IN " +
                    "('None', 'ForwardThenBackward', 'BackwardThenForward', " +
                    "'ClockwiseThenCounterclockwise', " +
                    "'CounterclockwiseThenClockwise', 'InwardThenOutward', " +
                    "'OutwardThenInward'))");
            }
            if (oldVersion < 39)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN insect_compatibility " +
                    "TEXT NOT NULL DEFAULT 'Unreviewed' CHECK " +
                    "(insect_compatibility IN " +
                        "('Unreviewed', 'Compatible', 'Incompatible'))");
            }
            if (oldVersion < 69)
            {
                database.ExecSQL(
                    "ALTER TABLE exercises ADD COLUMN hard_floor_compatibility " +
                    "TEXT NOT NULL DEFAULT 'Unreviewed' CHECK " +
                    "(hard_floor_compatibility IN " +
                    "('Unreviewed', 'Compatible', 'Incompatible'))");
            }
            CreateMassGroupingSchema(database);
            ClearMassGroupingReferenceData(database);
            database.ExecSQL(
                $"DROP TABLE IF EXISTS {ExerciseSequenceBlockTable}");
            RebuildExerciseTableForCurrentMetadata(database);
            CreateExerciseSequenceSchema(database);
            DeleteReplacedExercises(database, existingExercises.Keys, preservedExerciseIds);
            InsertTaxonomy(database);
            SynchronizeCatalog(database, catalog, preservedExercises);
            ValidatePreservedExercises(database, preservedExercises, catalog);
            database.SetTransactionSuccessful();
        }
        finally
        {
            database.EndTransaction();
        }
    }

    public void UpdateScore(Exercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        using var values = new ContentValues();
        values.Put("score", exercise.Score);
        SQLiteDatabase database = WritableDatabase
            ?? throw new InvalidOperationException("Unable to open the exercise database.");
        int updatedRows = database.Update(
            ExerciseTable,
            values,
            "id = ?",
            [exercise.Id.ToString(CultureInfo.InvariantCulture)]);

        if (updatedRows != 1)
        {
            throw new InvalidOperationException(
                $"Could not persist the score for exercise {exercise.Id}.");
        }
    }

    private static void CreateExerciseSchema(SQLiteDatabase database)
    {
        database.ExecSQL(
            """
            CREATE TABLE exercises (
                id INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL UNIQUE,
                video TEXT NOT NULL UNIQUE,
                practice TEXT NOT NULL,
                motion_profile TEXT NOT NULL,
                score INTEGER NOT NULL DEFAULT 0,
                muscular_demand INTEGER NOT NULL
                    CHECK (muscular_demand BETWEEN 0 AND 2),
                only_feet_touch_ground INTEGER NOT NULL DEFAULT 1
                    CHECK (only_feet_touch_ground = 1),
                shoe_agnostic INTEGER NOT NULL DEFAULT 1
                    CHECK (shoe_agnostic = 1),
                max_space_meters INTEGER NOT NULL DEFAULT 2
                    CHECK (max_space_meters > 0 AND max_space_meters <= 2),
                equipment TEXT NOT NULL DEFAULT 'None'
                    CHECK (equipment IN ('None', 'Mirror')),
                silent INTEGER NOT NULL DEFAULT 1
                    CHECK (silent IN (0, 1)),
                exercise_mode TEXT NOT NULL DEFAULT 'Repetition'
                    CHECK (exercise_mode IN ('Repetition', 'Hold')),
                presentation TEXT NOT NULL DEFAULT 'Motion'
                    CHECK (presentation IN ('Motion', 'Still')),
                hold_frame_percent INTEGER NOT NULL DEFAULT 0
                    CHECK (hold_frame_percent >= 0 AND hold_frame_percent <= 99),
                side_sequence TEXT NOT NULL DEFAULT 'Continuous'
                    CHECK (side_sequence IN (
                        'Continuous',
                        'Alternating',
                        'ScreenLeftThenRight',
                        'ScreenRightThenLeft',
                        'ScreenLeftLeadThenRightLead',
                        'ScreenRightLeadThenLeftLead')),
                direction_sequence TEXT NOT NULL DEFAULT 'None'
                    CHECK (direction_sequence IN (
                        'None',
                        'ForwardThenBackward',
                        'BackwardThenForward',
                        'ClockwiseThenCounterclockwise',
                        'CounterclockwiseThenClockwise',
                        'InwardThenOutward',
                        'OutwardThenInward')),
                insect_compatibility TEXT NOT NULL DEFAULT 'Unreviewed'
                    CHECK (insect_compatibility IN (
                        'Unreviewed',
                        'Compatible',
                        'Incompatible')),
                hard_floor_compatibility TEXT NOT NULL DEFAULT 'Unreviewed'
                    CHECK (hard_floor_compatibility IN (
                        'Unreviewed',
                        'Compatible',
                        'Incompatible')),
                upper_body_clothing_requirement TEXT NOT NULL DEFAULT 'Unreviewed'
                    CHECK (upper_body_clothing_requirement IN (
                        'Unreviewed',
                        'ClothingRequired',
                        'BareUpperBodyRequired',
                        'Agnostic')),
                shy_compatibility TEXT NOT NULL DEFAULT 'Unreviewed'
                    CHECK (shy_compatibility IN (
                        'Unreviewed',
                        'Compatible',
                        'Incompatible')),
                mirror_relationship TEXT NOT NULL DEFAULT 'Unreviewed'
                    CHECK (mirror_relationship IN (
                        'Unreviewed',
                        'MirrorOnly',
                        'BenefitsGreatly',
                        'Agnostic')),
                mirror_coverage TEXT NOT NULL DEFAULT 'None'
                    CHECK (mirror_coverage IN (
                        'None',
                        'UpperBody',
                        'FullBody')),
                wall_required INTEGER NOT NULL DEFAULT 0
                    CHECK (wall_required IN (0, 1)),
                sole_wall_contact_required INTEGER NOT NULL DEFAULT 0
                    CHECK (sole_wall_contact_required IN (0, 1)),
                session_movement_id INTEGER NOT NULL DEFAULT 0
                    CHECK (session_movement_id >= 0),
                CHECK (
                    sole_wall_contact_required = 0 OR wall_required = 1),
                CHECK (
                    (mirror_relationship = 'MirrorOnly' AND
                        equipment = 'Mirror' AND
                        mirror_coverage IN ('UpperBody', 'FullBody')) OR
                    (mirror_relationship = 'BenefitsGreatly' AND
                        equipment = 'None' AND
                        mirror_coverage IN ('UpperBody', 'FullBody')) OR
                    (mirror_relationship IN ('Unreviewed', 'Agnostic') AND
                        equipment = 'None' AND
                        mirror_coverage = 'None')),
                CHECK (
                    (exercise_mode = 'Repetition' AND hold_frame_percent = 0) OR
                    (exercise_mode = 'Hold' AND hold_frame_percent > 0))
            )
            """);
        database.ExecSQL(
            "CREATE INDEX index_exercises_score ON exercises (score DESC)");
    }

    private static void CreateExerciseSequenceSchema(SQLiteDatabase database)
    {
        database.ExecSQL(
            """
            CREATE TABLE exercise_sequence_blocks (
                sequence_root_exercise_id INTEGER NOT NULL,
                sequence_order INTEGER NOT NULL CHECK (sequence_order > 0),
                exercise_id INTEGER NOT NULL,
                side_cue TEXT NOT NULL CHECK (side_cue IN (
                    'None',
                    'ScreenLeft',
                    'ScreenRight',
                    'ShownLeadStance',
                    'OppositeLeadStance')),
                direction_cue TEXT NOT NULL CHECK (direction_cue IN (
                    'None',
                    'Forward',
                    'Backward',
                    'Clockwise',
                    'Counterclockwise',
                    'Inward',
                    'Outward')),
                mirror_media INTEGER NOT NULL CHECK (mirror_media IN (0, 1)),
                media_segment TEXT NOT NULL CHECK (media_segment IN (
                    'Full',
                    'FirstDirection',
                    'SecondDirection')),
                PRIMARY KEY (sequence_root_exercise_id, sequence_order),
                FOREIGN KEY (sequence_root_exercise_id)
                    REFERENCES exercises(id) ON DELETE CASCADE,
                FOREIGN KEY (exercise_id)
                    REFERENCES exercises(id) ON DELETE CASCADE
            )
            """);
        database.ExecSQL(
            "CREATE INDEX index_exercise_sequence_blocks_member " +
            "ON exercise_sequence_blocks (exercise_id)");
    }

    private static void RebuildExerciseTableForCurrentMetadata(
        SQLiteDatabase database)
    {
        database.ExecSQL(
            ExerciseDatabaseMigrationSql.DropRebuiltExerciseTableIfPresent);
        database.ExecSQL(
            ExerciseDatabaseMigrationSql.CreateRebuiltExerciseTable);
        database.ExecSQL(
            ExerciseDatabaseMigrationSql
                .CopyExistingExercisesWithNeutralCatalogMetadata);
        database.ExecSQL("DROP INDEX IF EXISTS index_exercises_score");
        database.ExecSQL("DROP TABLE exercises");
        database.ExecSQL(
            ExerciseDatabaseMigrationSql.RenameRebuiltExerciseTable);
        database.ExecSQL(
            "CREATE INDEX index_exercises_score ON exercises (score DESC)");
    }

    private static void CreateMassGroupingSchema(SQLiteDatabase database)
    {
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS canonical_muscle_groups (
                id INTEGER NOT NULL PRIMARY KEY,
                stable_key TEXT NOT NULL UNIQUE,
                display_name TEXT NOT NULL,
                mass_order INTEGER NOT NULL UNIQUE
            )
            """);
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS exercise_canonical_groups (
                exercise_id INTEGER NOT NULL,
                canonical_group_id INTEGER NOT NULL,
                assignment_role INTEGER NOT NULL
                    CHECK (assignment_role IN (1, 2)),
                PRIMARY KEY (exercise_id, canonical_group_id),
                FOREIGN KEY (exercise_id) REFERENCES exercises(id) ON DELETE CASCADE,
                FOREIGN KEY (canonical_group_id)
                    REFERENCES canonical_muscle_groups(id) ON DELETE RESTRICT
            )
            """);
        database.ExecSQL(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS one_primary_group_per_exercise
            ON exercise_canonical_groups (exercise_id)
            WHERE assignment_role = 1
            """);
        database.ExecSQL(
            """
            CREATE INDEX IF NOT EXISTS index_exercise_canonical_groups_group
            ON exercise_canonical_groups
                (canonical_group_id, assignment_role, exercise_id)
            """);
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS workout_buckets (
                stable_key TEXT NOT NULL PRIMARY KEY,
                resolution_minutes INTEGER NOT NULL,
                schedule_order INTEGER NOT NULL,
                display_name TEXT NOT NULL,
                UNIQUE (resolution_minutes, schedule_order)
            )
            """);
        database.ExecSQL(
            """
            CREATE TABLE IF NOT EXISTS canonical_group_rollups (
                resolution_minutes INTEGER NOT NULL,
                canonical_group_id INTEGER NOT NULL,
                bucket_key TEXT NOT NULL,
                PRIMARY KEY (resolution_minutes, canonical_group_id),
                FOREIGN KEY (canonical_group_id)
                    REFERENCES canonical_muscle_groups(id) ON DELETE CASCADE,
                FOREIGN KEY (bucket_key)
                    REFERENCES workout_buckets(stable_key) ON DELETE CASCADE
            )
            """);
    }

    private static void ClearMassGroupingReferenceData(SQLiteDatabase database)
    {
        database.Delete(ExerciseCanonicalGroupTable, null, null);
        database.Delete(RollupTable, null, null);
        database.Delete(WorkoutBucketTable, null, null);
        database.Delete(CanonicalGroupTable, null, null);
    }

    private static void DeleteReplacedExercises(
        SQLiteDatabase database,
        IEnumerable<int> existingExerciseIds,
        IReadOnlySet<int> preservedExerciseIds)
    {
        foreach (int exerciseId in existingExerciseIds.Where(
            exerciseId => !preservedExerciseIds.Contains(exerciseId)))
        {
            int deleted = database.Delete(
                ExerciseTable,
                "id = ?",
                [exerciseId.ToString(CultureInfo.InvariantCulture)]);
            if (deleted != 1)
            {
                throw new InvalidOperationException(
                    $"Could not retire replaced exercise {exerciseId}.");
            }
        }
    }

    private static void InsertTaxonomy(SQLiteDatabase database)
    {
        foreach (CanonicalMuscleGroup group in Enum.GetValues<CanonicalMuscleGroup>())
        {
            using var values = new ContentValues();
            values.Put("id", (int)group);
            values.Put("stable_key", group.ToString());
            values.Put("display_name", MassGroupingTaxonomy.GetCanonicalDisplayName(group));
            values.Put("mass_order", (int)group);
            database.InsertOrThrow(CanonicalGroupTable, null, values);
        }

        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            WorkoutResolution resolution = MassGroupingTaxonomy.GetResolution(minutes);
            foreach (WorkoutGroup group in resolution.Groups)
            {
                using (var bucketValues = new ContentValues())
                {
                    bucketValues.Put("stable_key", group.Id);
                    bucketValues.Put("resolution_minutes", minutes);
                    bucketValues.Put("schedule_order", group.Order);
                    bucketValues.Put("display_name", group.DisplayName);
                    database.InsertOrThrow(WorkoutBucketTable, null, bucketValues);
                }

                foreach (CanonicalMuscleGroup canonicalGroup in group.CanonicalGroups)
                {
                    using var rollupValues = new ContentValues();
                    rollupValues.Put("resolution_minutes", minutes);
                    rollupValues.Put("canonical_group_id", (int)canonicalGroup);
                    rollupValues.Put("bucket_key", group.Id);
                    database.InsertOrThrow(RollupTable, null, rollupValues);
                }
            }
        }
    }

    private Exercise[] ReadAndValidateBundledCatalog()
    {
        using Stream stream = _context.Assets!.Open(CatalogAsset);
        Exercise[] catalog = JsonSerializer.Deserialize(
                stream,
                ExerciseCatalogJsonContext.Default.ExerciseArray)
            ?? throw new InvalidOperationException("The exercise catalog is empty.");
        ValidateCatalog(catalog, requireInitialScores: true);
        return catalog;
    }

    private static void InsertCatalog(
        SQLiteDatabase database,
        IReadOnlyCollection<Exercise> catalog)
    {
        foreach (Exercise exercise in catalog)
        {
            InsertExercise(database, exercise, exercise.Score);
            InsertCanonicalAssignments(database, exercise);
        }

        InsertExerciseSequenceBlocks(database, catalog);
    }

    private static void SynchronizeCatalog(
        SQLiteDatabase database,
        IReadOnlyCollection<Exercise> catalog,
        IReadOnlyDictionary<int, StoredExerciseSnapshot> existingExercises)
    {
        foreach (Exercise exercise in catalog)
        {
            if (existingExercises.ContainsKey(exercise.Id))
            {
                UpdateExerciseMetadata(database, exercise);
            }
            else
            {
                InsertExercise(database, exercise, exercise.Score);
            }

            InsertCanonicalAssignments(database, exercise);
        }

        InsertExerciseSequenceBlocks(database, catalog);
    }

    private static void InsertExerciseSequenceBlocks(
        SQLiteDatabase database,
        IReadOnlyCollection<Exercise> catalog)
    {
        database.Delete(ExerciseSequenceBlockTable, null, null);
        foreach (Exercise root in catalog.Where(exercise =>
                     exercise.SequenceBlocks.Length > 0))
        {
            for (int index = 0; index < root.SequenceBlocks.Length; index++)
            {
                ExerciseSequenceBlock block = root.SequenceBlocks[index];
                using var values = new ContentValues();
                values.Put("sequence_root_exercise_id", root.Id);
                values.Put("sequence_order", index + 1);
                values.Put("exercise_id", block.ExerciseId);
                values.Put("side_cue", block.SideCue.ToString());
                values.Put("direction_cue", block.DirectionCue.ToString());
                values.Put("mirror_media", block.MirrorMedia ? 1 : 0);
                values.Put("media_segment", block.MediaSegment.ToString());
                database.InsertOrThrow(
                    ExerciseSequenceBlockTable,
                    null,
                    values);
            }
        }
    }

    private static void InsertExercise(
        SQLiteDatabase database,
        Exercise exercise,
        int score)
    {
        using ContentValues values = CreateExerciseValues(exercise, includeId: true);
        values.Put("score", score);
        database.InsertOrThrow(ExerciseTable, null, values);
    }

    private static void UpdateExerciseMetadata(
        SQLiteDatabase database,
        Exercise exercise)
    {
        using ContentValues values = CreateExerciseValues(
            exercise,
            includeId: false,
            includeIdentity: false);
        values.Put("name", exercise.Name);
        int updated = database.Update(
            ExerciseTable,
            values,
            "id = ?",
            [exercise.Id.ToString(CultureInfo.InvariantCulture)]);
        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"Could not migrate exercise {exercise.Id} without replacing it.");
        }
    }

    private static ContentValues CreateExerciseValues(
        Exercise exercise,
        bool includeId,
        bool includeIdentity = true)
    {
        var values = new ContentValues();
        if (includeId)
        {
            values.Put("id", exercise.Id);
        }

        if (includeIdentity)
        {
            values.Put("name", exercise.Name);
            values.Put("video", exercise.Video);
        }
        values.Put("practice", exercise.Practice);
        values.Put("motion_profile", exercise.MotionProfile);
        values.Put("muscular_demand", exercise.MuscularDemand);
        values.Put("only_feet_touch_ground", exercise.OnlyFeetTouchGround ? 1 : 0);
        values.Put("shoe_agnostic", exercise.ShoeAgnostic ? 1 : 0);
        values.Put("max_space_meters", exercise.MaxSpaceMeters);
        values.Put("equipment", exercise.Equipment);
        values.Put("silent", exercise.Silent ? 1 : 0);
        values.Put("exercise_mode", exercise.Mode.ToString());
        values.Put("presentation", exercise.Presentation.ToString());
        values.Put("hold_frame_percent", exercise.HoldFramePercent);
        values.Put("side_sequence", exercise.SideSequence.ToString());
        values.Put("direction_sequence", exercise.DirectionSequence.ToString());
        values.Put("insect_compatibility", exercise.InsectCompatibility.ToString());
        values.Put(
            "hard_floor_compatibility",
            exercise.HardFloorCompatibility.ToString());
        values.Put(
            "upper_body_clothing_requirement",
            exercise.UpperBodyClothingRequirement.ToString());
        values.Put("shy_compatibility", exercise.ShyCompatibility.ToString());
        values.Put("mirror_relationship", exercise.MirrorRelationship.ToString());
        values.Put("mirror_coverage", exercise.MinimumMirrorCoverage.ToString());
        values.Put("wall_required", exercise.WallRequired ? 1 : 0);
        values.Put(
            "sole_wall_contact_required",
            exercise.SoleWallContactRequired ? 1 : 0);
        values.Put("session_movement_id", exercise.SessionMovementId);
        return values;
    }

    private static void InsertCanonicalAssignments(
        SQLiteDatabase database,
        Exercise exercise)
    {
        InsertCanonicalAssignment(
            database,
            exercise.Id,
            exercise.PrimaryCanonicalGroup,
            assignmentRole: 1);
        foreach (CanonicalMuscleGroup group in exercise.SecondaryCanonicalGroups)
        {
            InsertCanonicalAssignment(
                database,
                exercise.Id,
                group,
                assignmentRole: 2);
        }
    }

    private static void InsertCanonicalAssignment(
        SQLiteDatabase database,
        int exerciseId,
        CanonicalMuscleGroup group,
        int assignmentRole)
    {
        using var values = new ContentValues();
        values.Put("exercise_id", exerciseId);
        values.Put("canonical_group_id", (int)group);
        values.Put("assignment_role", assignmentRole);
        database.InsertOrThrow(ExerciseCanonicalGroupTable, null, values);
    }

    private static Dictionary<int, StoredExerciseSnapshot> ReadExistingExercises(
        SQLiteDatabase database)
    {
        var existingExercises = new Dictionary<int, StoredExerciseSnapshot>();
        using ICursor? cursor = database.Query(
            ExerciseTable,
            ["id", "name", "video", "score"],
            null,
            null,
            null,
            null,
            null);

        if (cursor is null)
        {
            throw new InvalidOperationException(
                "Unable to read existing exercise identities and scores.");
        }

        while (cursor.MoveToNext())
        {
            existingExercises[cursor.GetInt(0)] = new StoredExerciseSnapshot(
                cursor.GetString(1)
                    ?? throw new InvalidOperationException(
                        "An existing exercise has no name."),
                cursor.GetString(2)
                    ?? throw new InvalidOperationException(
                        "An existing exercise has no demonstration."),
                cursor.GetInt(3));
        }

        return existingExercises;
    }

    private static void ValidatePreservedExercises(
        SQLiteDatabase database,
        IReadOnlyDictionary<int, StoredExerciseSnapshot> before,
        IReadOnlyCollection<Exercise> bundledCatalog)
    {
        Dictionary<int, StoredExerciseSnapshot> after = ReadExistingExercises(database);
        IReadOnlyDictionary<int, Exercise> bundledById = bundledCatalog.ToDictionary(
            exercise => exercise.Id);
        foreach ((int exerciseId, StoredExerciseSnapshot previous) in before)
        {
            if (!after.TryGetValue(exerciseId, out StoredExerciseSnapshot? current) ||
                current.Name != bundledById[exerciseId].Name ||
                current.Video != previous.Video ||
                current.Score != previous.Score ||
                string.IsNullOrWhiteSpace(current.Video))
            {
                throw new InvalidOperationException(
                    $"Exercise {exerciseId} lost its score or demonstration during migration.");
            }
        }
    }

    private IReadOnlyList<Exercise> LoadExercises()
    {
        SQLiteDatabase database = ReadableDatabase
            ?? throw new InvalidOperationException("Unable to open the exercise database.");
        Dictionary<int, CanonicalAssignments> assignmentsByExerciseId =
            LoadCanonicalAssignments(database);
        Dictionary<int, ExerciseSequenceBlock[]> sequenceBlocksByRootId =
            LoadExerciseSequenceBlocks(database);
        var exercises = new List<Exercise>();

        using ICursor? cursor = database.Query(
            ExerciseTable,
            ExerciseColumns,
            null,
            null,
            null,
            null,
            "id ASC");

        if (cursor is null)
        {
            throw new InvalidOperationException("Unable to read the exercise database.");
        }

        while (cursor.MoveToNext())
        {
            int id = cursor.GetInt(0);
            if (!assignmentsByExerciseId.TryGetValue(
                    id,
                    out CanonicalAssignments? assignments) ||
                assignments.Primary is null)
            {
                throw new InvalidOperationException(
                    $"Exercise {id} has no primary canonical assignment.");
            }

            exercises.Add(new Exercise
            {
                Id = id,
                Name = cursor.GetString(1)
                    ?? throw new InvalidOperationException("An exercise has no name."),
                Video = cursor.GetString(2)
                    ?? throw new InvalidOperationException("An exercise has no video."),
                PrimaryCanonicalGroup = assignments.Primary.Value,
                SecondaryCanonicalGroups = assignments.Secondary.ToArray(),
                Practice = cursor.GetString(3)
                    ?? throw new InvalidOperationException("An exercise has no practice."),
                MotionProfile = cursor.GetString(4)
                    ?? throw new InvalidOperationException("An exercise has no motion profile."),
                Score = cursor.GetInt(5),
                MuscularDemand = cursor.GetInt(6),
                OnlyFeetTouchGround = cursor.GetInt(7) == 1,
                ShoeAgnostic = cursor.GetInt(8) == 1,
                MaxSpaceMeters = cursor.GetInt(9),
                Equipment = cursor.GetString(10)
                    ?? throw new InvalidOperationException("An exercise has no equipment value."),
                Silent = cursor.GetInt(11) == 1,
                Mode = Enum.Parse<ExerciseMode>(cursor.GetString(12)
                    ?? throw new InvalidOperationException("An exercise has no mode.")),
                Presentation = Enum.Parse<ExercisePresentation>(cursor.GetString(13)
                    ?? throw new InvalidOperationException(
                        "An exercise has no presentation.")),
                HoldFramePercent = cursor.GetInt(14),
                SideSequence = Enum.Parse<ExerciseSideSequence>(cursor.GetString(15)
                    ?? throw new InvalidOperationException(
                        "An exercise has no side sequence.")),
                DirectionSequence = Enum.Parse<ExerciseDirectionSequence>(
                    cursor.GetString(16)
                        ?? throw new InvalidOperationException(
                            "An exercise has no direction sequence.")),
                SequenceBlocks = sequenceBlocksByRootId.GetValueOrDefault(
                    id,
                    []),
                InsectCompatibility = Enum.Parse<ExerciseInsectCompatibility>(
                    cursor.GetString(17)
                        ?? throw new InvalidOperationException(
                            "An exercise has no insect compatibility review.")),
                HardFloorCompatibility =
                    Enum.Parse<ExerciseHardFloorCompatibility>(
                        cursor.GetString(18)
                            ?? throw new InvalidOperationException(
                                "An exercise has no hard-floor compatibility review.")),
                UpperBodyClothingRequirement =
                    Enum.Parse<ExerciseUpperBodyClothingRequirement>(
                        cursor.GetString(19)
                            ?? throw new InvalidOperationException(
                                "An exercise has no upper-body clothing review.")),
                ShyCompatibility = Enum.Parse<ExerciseShyCompatibility>(
                    cursor.GetString(20)
                        ?? throw new InvalidOperationException(
                            "An exercise has no Shy compatibility review.")),
                MirrorRelationship = Enum.Parse<ExerciseMirrorRelationship>(
                    cursor.GetString(21)
                        ?? throw new InvalidOperationException(
                            "An exercise has no mirror relationship review.")),
                MinimumMirrorCoverage = Enum.Parse<ExerciseMirrorCoverage>(
                    cursor.GetString(22)
                        ?? throw new InvalidOperationException(
                            "An exercise has no mirror coverage review.")),
                WallRequired = cursor.GetInt(23) == 1,
                SoleWallContactRequired = cursor.GetInt(24) == 1,
                SessionMovementId = cursor.GetInt(25),
            });
        }

        ValidateCatalog(exercises, requireInitialScores: false);
        return exercises.AsReadOnly();
    }

    private static Dictionary<int, ExerciseSequenceBlock[]>
        LoadExerciseSequenceBlocks(SQLiteDatabase database)
    {
        var blocksByRootId = new Dictionary<int, List<ExerciseSequenceBlock>>();
        using ICursor? cursor = database.Query(
            ExerciseSequenceBlockTable,
            [
                "sequence_root_exercise_id",
                "exercise_id",
                "side_cue",
                "direction_cue",
                "mirror_media",
                "media_segment",
            ],
            null,
            null,
            null,
            null,
            "sequence_root_exercise_id ASC, sequence_order ASC");
        if (cursor is null)
        {
            throw new InvalidOperationException(
                "Unable to read exercise sequence blocks.");
        }

        while (cursor.MoveToNext())
        {
            int rootId = cursor.GetInt(0);
            if (!blocksByRootId.TryGetValue(
                    rootId,
                    out List<ExerciseSequenceBlock>? blocks))
            {
                blocks = [];
                blocksByRootId.Add(rootId, blocks);
            }

            blocks.Add(new ExerciseSequenceBlock
            {
                ExerciseId = cursor.GetInt(1),
                SideCue = Enum.Parse<ExerciseSequenceSideCue>(cursor.GetString(2)
                    ?? throw new InvalidOperationException(
                        "An exercise sequence block has no side cue.")),
                DirectionCue = Enum.Parse<ExerciseSequenceDirectionCue>(
                    cursor.GetString(3)
                        ?? throw new InvalidOperationException(
                            "An exercise sequence block has no direction cue.")),
                MirrorMedia = cursor.GetInt(4) == 1,
                MediaSegment = Enum.Parse<ExerciseSequenceMediaSegment>(
                    cursor.GetString(5)
                        ?? throw new InvalidOperationException(
                            "An exercise sequence block has no media segment.")),
            });
        }

        return blocksByRootId.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray());
    }

    private static Dictionary<int, CanonicalAssignments> LoadCanonicalAssignments(
        SQLiteDatabase database)
    {
        var assignmentsByExerciseId = new Dictionary<int, CanonicalAssignments>();
        using ICursor? cursor = database.Query(
            ExerciseCanonicalGroupTable,
            ["exercise_id", "canonical_group_id", "assignment_role"],
            null,
            null,
            null,
            null,
            "exercise_id ASC, assignment_role ASC, canonical_group_id ASC");

        if (cursor is null)
        {
            throw new InvalidOperationException(
                "Unable to read canonical exercise assignments.");
        }

        while (cursor.MoveToNext())
        {
            int exerciseId = cursor.GetInt(0);
            var group = (CanonicalMuscleGroup)cursor.GetInt(1);
            int role = cursor.GetInt(2);
            if (!assignmentsByExerciseId.TryGetValue(
                    exerciseId,
                    out CanonicalAssignments? assignments))
            {
                assignments = new CanonicalAssignments();
                assignmentsByExerciseId.Add(exerciseId, assignments);
            }

            if (role == 1)
            {
                if (assignments.Primary is not null)
                {
                    throw new InvalidOperationException(
                        $"Exercise {exerciseId} has multiple primary assignments.");
                }

                assignments.Primary = group;
            }
            else
            {
                assignments.Secondary.Add(group);
            }
        }

        return assignmentsByExerciseId;
    }

    private static void ValidateCatalog(
        IReadOnlyCollection<Exercise> exercises,
        bool requireInitialScores)
    {
        bool hasUndersizedWallCatalog =
            WorkoutModifierPolicy.FindWallRequiredCatalogDeficiencies(exercises)
                .Count > 0;
        bool hasUndersizedSoleWallCatalog =
            WorkoutModifierPolicy
                .FindSoleWallContactRequiredCatalogDeficiencies(exercises)
                .Count > 0;
        bool violatesRequirements = exercises.Any(exercise =>
            !Enum.IsDefined(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Distinct().Count() !=
                exercise.SecondaryCanonicalGroups.Length ||
            exercise.SecondaryCanonicalGroups.Contains(exercise.PrimaryCanonicalGroup) ||
            exercise.SecondaryCanonicalGroups.Any(group => !Enum.IsDefined(group)) ||
            !exercise.OnlyFeetTouchGround ||
            !exercise.ShoeAgnostic ||
            exercise.MaxSpaceMeters is <= 0 or > 2 ||
            (exercise.MirrorRelationship ==
                    ExerciseMirrorRelationship.MirrorOnly
                ? exercise.Equipment != "Mirror"
                : exercise.Equipment != "None") ||
            exercise.MuscularDemand < Exercise.MinimumMuscularDemand ||
            exercise.MuscularDemand > Exercise.MaximumMuscularDemand ||
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
                    member.SoleWallContactRequired !=
                        root.SoleWallContactRequired ||
                    member.UpperBodyClothingRequirement !=
                        root.UpperBodyClothingRequirement ||
                    member.ShyCompatibility != root.ShyCompatibility ||
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

        if (hasUndersizedWallCatalog ||
            hasUndersizedSoleWallCatalog ||
            !WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises) ||
            exercises.Select(exercise => exercise.Id).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Name).Distinct().Count() != exercises.Count ||
            exercises.Select(exercise => exercise.Video).Distinct().Count() != exercises.Count ||
            violatesRequirements ||
            hasInvalidInitialScore ||
            hasInvalidSessionMovement ||
            hasInvalidSequence ||
            hasInvalidReplacementMetadata)
        {
            throw new InvalidOperationException(
                "The bundled exercise catalog does not satisfy its required invariants.");
        }
    }

    private sealed class CanonicalAssignments
    {
        public CanonicalMuscleGroup? Primary { get; set; }

        public List<CanonicalMuscleGroup> Secondary { get; } = [];
    }
}
