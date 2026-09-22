namespace NomadicMethod.Data;

internal static class ExerciseDatabaseMigrationSql
{
    internal const string RebuiltExerciseTableName = "exercises_rebuilt";

    internal static readonly string DropRebuiltExerciseTableIfPresent =
        $"DROP TABLE IF EXISTS {RebuiltExerciseTableName}";

    internal static readonly string CreateRebuiltExerciseTable =
        $"""
        CREATE TABLE {RebuiltExerciseTableName} (
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
        """;

    internal static readonly string CopyExistingExercisesWithNeutralCatalogMetadata =
        $"""
        INSERT INTO {RebuiltExerciseTableName} (
            id, name, video, practice, motion_profile, score,
            muscular_demand, only_feet_touch_ground, shoe_agnostic,
            max_space_meters,
            equipment, silent, exercise_mode, presentation,
            hold_frame_percent, side_sequence, direction_sequence,
            insect_compatibility, hard_floor_compatibility,
            upper_body_clothing_requirement,
            shy_compatibility,
            mirror_relationship, mirror_coverage,
            wall_required, sole_wall_contact_required,
            session_movement_id)
        SELECT
            id, name, video, practice, motion_profile, score, 0,
            only_feet_touch_ground, shoe_agnostic,
            CASE WHEN max_space_meters BETWEEN 1 AND 2
                THEN max_space_meters ELSE 2 END,
            'None', silent, exercise_mode, presentation,
            hold_frame_percent, side_sequence, direction_sequence,
            insect_compatibility, 'Unreviewed', 'Unreviewed',
            'Unreviewed', 'Unreviewed', 'None',
            0, 0, 0
        FROM exercises
        """;

    internal static readonly string RenameRebuiltExerciseTable =
        $"ALTER TABLE {RebuiltExerciseTableName} RENAME TO exercises";
}
