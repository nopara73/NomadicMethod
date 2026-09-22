using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutLightDayPolicy
{
    public const int DailyRegularMinutesCap = 60;
    public const int RegularMinutesBeforeLightDay = 180;
    public const int MinimumRegularCompletionPercentForFullCredit = 85;
    public const int MinimumLegacyHardPrimaryMuscles = 3;

    public static WorkoutModifiers GetDefaultWorkoutModifiers(
        WorkoutModifiers persistentSetupModifiers,
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds = null)
    {
        WorkoutModifiers modifiers = WorkoutModifierPolicy
            .GetPersistentSetupModifiers(persistentSetupModifiers);
        return IsLightDayDue(
                workoutHistory,
                nowUnixMilliseconds,
                localTimeZone,
                legacyCompletedTrainingDayUnixMilliseconds)
            ? modifiers | WorkoutModifiers.Light
            : modifiers;
    }

    public static bool IsLightDayDue(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds = null)
    {
        return GetAccumulatedRegularMinutes(
            workoutHistory,
            nowUnixMilliseconds,
            localTimeZone,
            legacyCompletedTrainingDayUnixMilliseconds) >=
            RegularMinutesBeforeLightDay;
    }

    public static int GetWorkoutsUntilLightDay(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        int prospectiveWorkoutMinutes,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds = null)
    {
        if (prospectiveWorkoutMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(prospectiveWorkoutMinutes));
        }

        int accumulatedMinutes = GetAccumulatedRegularMinutes(
            workoutHistory,
            nowUnixMilliseconds,
            localTimeZone,
            legacyCompletedTrainingDayUnixMilliseconds);
        int remainingMinutes = Math.Max(
            0,
            RegularMinutesBeforeLightDay - accumulatedMinutes);
        if (remainingMinutes == 0)
        {
            return 0;
        }

        int prospectiveContribution = Math.Min(
            prospectiveWorkoutMinutes,
            DailyRegularMinutesCap);
        return (remainingMinutes + prospectiveContribution - 1) /
            prospectiveContribution;
    }

    private static int GetAccumulatedRegularMinutes(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone,
        IEnumerable<long>? legacyCompletedTrainingDayUnixMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(workoutHistory);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }

        TimeZoneInfo timeZone = localTimeZone ?? TimeZoneInfo.Local;
        int today = GetLocalDayNumber(nowUnixMilliseconds, timeZone);
        var activityByDay = new Dictionary<int, TrainingDayActivity>();
        foreach (WorkoutSessionLog session in workoutHistory.Where(session =>
                     session is not null))
        {
            long timestamp = session.StartedAtUnixMilliseconds > 0
                ? session.StartedAtUnixMilliseconds
                : session.EndedAtUnixMilliseconds;
            int? dayNumber = timestamp > 0
                ? TryGetLocalDayNumber(timestamp, timeZone)
                : null;
            if (!dayNumber.HasValue)
            {
                continue;
            }

            WorkoutModifierChangeLog[] changes = (session.ModifierChanges ?? [])
                .OfType<WorkoutModifierChangeLog>()
                .Where(change => change.ChangedAtUnixMilliseconds > 0)
                .OrderBy(change => change.ChangedAtUnixMilliseconds)
                .ToArray();
            bool IsLightAt(long at)
            {
                WorkoutModifiers modifiers = session.Modifiers;
                bool light = session.IsLightDay ||
                    modifiers.HasFlag(WorkoutModifiers.Light);
                foreach (WorkoutModifierChangeLog change in changes)
                {
                    if (change.ChangedAtUnixMilliseconds > at)
                    {
                        break;
                    }
                    light = change.NewModifiers.HasFlag(WorkoutModifiers.Light);
                }
                return light;
            }

            void AddActivity(long at, int minutes, bool completedLight = false)
            {
                int? activityDay = at > 0 && at <= nowUnixMilliseconds
                    ? TryGetLocalDayNumber(at, timeZone)
                    : null;
                if (!activityDay.HasValue)
                {
                    return;
                }
                TrainingDayActivity activity = activityByDay.GetValueOrDefault(
                    activityDay.Value);
                activityByDay[activityDay.Value] = activity with
                {
                    RegularMinutes = Math.Min(DailyRegularMinutesCap,
                        activity.RegularMinutes + minutes),
                    HasCompletedLightWorkout =
                        activity.HasCompletedLightWorkout || completedLight,
                };
            }

            WorkoutBlockLog[] blocks = (session.Blocks ?? [])
                .OfType<WorkoutBlockLog>().ToArray();
            long endedAt = session.EndedAtUnixMilliseconds > 0
                ? session.EndedAtUnixMilliseconds : timestamp;
            int completedRegularBlocks = 0;
            foreach (WorkoutBlockLog block in blocks)
            {
                long at = block.CompletedAtUnixMilliseconds > 0
                    ? block.CompletedAtUnixMilliseconds : timestamp;
                bool light = IsLightAt(at);
                AddActivity(at, light ? 0 : 1);
                if (!light && at > 0 && at <= endedAt && at <= nowUnixMilliseconds)
                {
                    completedRegularBlocks++;
                }
            }

            if (session.Status == WorkoutSessionStatus.Completed)
            {
                if (IsLightAt(endedAt))
                {
                    AddActivity(endedAt, 0, completedLight: true);
                }
                else if (blocks.Length == 0 &&
                    (session.StartedBeforeLogging ||
                     ((session.InitialSelections?.Count ?? 0) == 0 &&
                      (session.Decisions?.Count ?? 0) == 0)))
                {
                    // Only pre-block-history completions may use the old
                    // duration. A modern all-skipped workout contributes zero.
                    AddActivity(endedAt, Math.Clamp(session.WorkoutMinutes,
                        0, DailyRegularMinutesCap));
                }
                else if (session.WorkoutMinutes > 0 &&
                    completedRegularBlocks < session.WorkoutMinutes &&
                    completedRegularBlocks * 100L >=
                        session.WorkoutMinutes *
                            (long)MinimumRegularCompletionPercentForFullCredit &&
                    !session.IsLightDay &&
                    !session.Modifiers.HasFlag(WorkoutModifiers.Light) &&
                    !changes.Any(change =>
                        change.ChangedAtUnixMilliseconds <= endedAt &&
                        change.NewModifiers.HasFlag(WorkoutModifiers.Light)))
                {
                    // Cadence-only completion tolerance, never invented work
                    // in the log or muscle recovery history. Actual blocks
                    // keep their dates; only the top-up belongs to completion.
                    AddActivity(endedAt, Math.Min(DailyRegularMinutesCap,
                        session.WorkoutMinutes - completedRegularBlocks));
                }
            }
        }
        foreach (int dayNumber in (legacyCompletedTrainingDayUnixMilliseconds ?? [])
                     .Where(timestamp => timestamp > 0 && timestamp <= nowUnixMilliseconds)
                     .Select(timestamp => TryGetLocalDayNumber(timestamp, timeZone))
                     .Where(dayNumber => dayNumber.HasValue)
                     .Select(dayNumber => dayNumber!.Value))
        {
            // A legacy timestamp proves only that the old cadence counted a
            // full training day. Never add it on top of a reconstructable log.
            activityByDay.TryAdd(
                dayNumber,
                new TrainingDayActivity(DailyRegularMinutesCap, false));
        }

        int day = activityByDay.ContainsKey(today)
            ? today
            : today - 1;
        int accumulatedMinutes = 0;
        while (activityByDay.TryGetValue(day, out TrainingDayActivity activity))
        {
            if (activity.HasCompletedLightWorkout && day < today)
            {
                break;
            }

            accumulatedMinutes += Math.Min(
                DailyRegularMinutesCap,
                activity.RegularMinutes);
            if (accumulatedMinutes >= RegularMinutesBeforeLightDay)
            {
                return RegularMinutesBeforeLightDay;
            }

            // A Light workout completed today keeps an already-due Light day
            // locked until tomorrow. It becomes the cadence reset only after
            // the local date changes.
            day--;
        }
        return accumulatedMinutes;
    }

    public static IReadOnlyList<long> InferLegacyCompletedTrainingDays(
        IEnumerable<WorkoutSessionLog> workoutHistory,
        IReadOnlyDictionary<string, long> lastHardWorkByPrimaryMuscle,
        IEnumerable<long> existingLegacyTrainingDayTimestamps,
        long nowUnixMilliseconds,
        TimeZoneInfo? localTimeZone = null)
    {
        ArgumentNullException.ThrowIfNull(workoutHistory);
        ArgumentNullException.ThrowIfNull(lastHardWorkByPrimaryMuscle);
        ArgumentNullException.ThrowIfNull(existingLegacyTrainingDayTimestamps);
        if (nowUnixMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nowUnixMilliseconds));
        }

        TimeZoneInfo timeZone = localTimeZone ?? TimeZoneInfo.Local;
        int today = GetLocalDayNumber(nowUnixMilliseconds, timeZone);
        HashSet<int> loggedCompletedDays = workoutHistory
            .Where(session =>
                session is not null &&
                session.Status == WorkoutSessionStatus.Completed)
            .Select(session => session.StartedAtUnixMilliseconds > 0
                ? session.StartedAtUnixMilliseconds
                : session.EndedAtUnixMilliseconds)
            .Where(timestamp => timestamp > 0)
            .Select(timestamp => TryGetLocalDayNumber(timestamp, timeZone))
            .Where(dayNumber => dayNumber.HasValue)
            .Select(dayNumber => dayNumber!.Value)
            .ToHashSet();
        Dictionary<int, long> existingLegacyDays = existingLegacyTrainingDayTimestamps
            .Where(timestamp => timestamp > 0)
            .Select(timestamp => new
            {
                Timestamp = timestamp,
                Day = TryGetLocalDayNumber(timestamp, timeZone),
            })
            .Where(entry => entry.Day.HasValue)
            .GroupBy(entry => entry.Day!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Max(entry => entry.Timestamp));
        Dictionary<int, long[]> hardEvidenceByDay = lastHardWorkByPrimaryMuscle
            .Where(entry =>
                !string.IsNullOrWhiteSpace(entry.Key) &&
                entry.Value > 0)
            .Select(entry => new
            {
                entry.Value,
                Day = TryGetLocalDayNumber(entry.Value, timeZone),
            })
            .Where(entry => entry.Day.HasValue)
            .GroupBy(entry => entry.Day!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.Value).ToArray());

        // Only bridge the uninterrupted streak immediately preceding the
        // current logged streak. Sparse older recovery timestamps must never
        // turn isolated historical activity into fabricated workout sessions.
        int cursor = loggedCompletedDays.Contains(today) ? today : today - 1;
        while (loggedCompletedDays.Contains(cursor) ||
               existingLegacyDays.ContainsKey(cursor))
        {
            cursor--;
        }

        var inferred = new List<long>();
        while (hardEvidenceByDay.TryGetValue(cursor, out long[]? timestamps) &&
               timestamps.Length >= MinimumLegacyHardPrimaryMuscles)
        {
            inferred.Add(timestamps.Max());
            cursor--;
        }

        return inferred;
    }

    private static int GetLocalDayNumber(
        long unixMilliseconds,
        TimeZoneInfo localTimeZone)
    {
        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds),
            localTimeZone);
        return DateOnly.FromDateTime(localTime.DateTime).DayNumber;
    }

    private static int? TryGetLocalDayNumber(
        long unixMilliseconds,
        TimeZoneInfo localTimeZone)
    {
        try
        {
            return GetLocalDayNumber(unixMilliseconds, localTimeZone);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private readonly record struct TrainingDayActivity(
        int RegularMinutes,
        bool HasCompletedLightWorkout);
}
