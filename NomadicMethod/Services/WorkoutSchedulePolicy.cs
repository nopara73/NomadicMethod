using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class WorkoutSchedulePolicy
{
    // A station may be in another room. Keep each station in one visit before
    // considering posture changes or demand. These are requirements, not an
    // inference that every enabled piece of equipment is in the same place.
    private const int Wall = 1, Mirror = 2, Elevation = 4;
    private const int EquipmentMask = Wall | Mirror | Elevation;
    private const int Room = 8, Field = 16, SoftSurface = 32;
    private const int LocomotionMask = Room | Field;
    private const int SetupGroupMask = EquipmentMask | LocomotionMask;
    private const int StationChangeCost = 1_000;
    private const int RequirementChangeCost = 100;
    private const int FloorChangeCost = 10;
    private const int ExactStationLimit = 12;

    public readonly record struct Setup(int Station, bool Floor);

    public static Setup GetSequenceSetup(Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById, WorkoutModifiers modifiers)
    {
        int station = 0;
        bool floor = false;
        foreach (ExerciseSequenceBlock block in root.SequenceBlocks)
        {
            Exercise member = exercisesById[block.ExerciseId];
            if (member.WallRequired || member.SoleWallContactRequired) station |= Wall;
            if (WorkoutModifierPolicy.IsMirrorPreferred(member, modifiers)) station |= Mirror;
            if (member.ElevationRequired || member.JumpPlatformRequired) station |= Elevation;
            if (member.LocomotionSpace >= LocomotionSpace.Room) station |= Room;
            if (member.LocomotionSpace == LocomotionSpace.Field) station |= Field;
            if (member.HardFloorCompatibility == ExerciseHardFloorCompatibility.Incompatible)
                station |= SoftSurface;
            floor |= !member.OnlyFeetTouchGround;
        }
        return new(station, floor);
    }

    public static T[] OrderBySetup<T>(IEnumerable<T> selections,
        Func<T, Exercise> root, Func<T, int> anatomyOrder,
        IReadOnlyDictionary<int, Exercise> exercisesById, WorkoutModifiers modifiers,
        Exercise? current = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        T[] ranked = selections.OrderBy(item => GetMuscularDemandPriority(
                GetSequenceMuscularDemand(root(item), exercisesById)))
            .ThenBy(anatomyOrder).ToArray();
        if (ranked.Length < 2) return ranked;
        Setup[] setups = ranked.Select(item => GetSequenceSetup(
            root(item), exercisesById, modifiers)).ToArray();
        Setup? start = current is null ? null : GetSequenceSetup(current, exercisesById, modifiers);

        // Actual equipment and travel requirements define separate groups.
        // Stationary, Room and Field work must not be mixed merely because a
        // larger space could host them all. Compatible surfaces can still share
        // a station within the same equipment/travel group.
        bool CanHost(int station, int requirements) =>
            (station & SetupGroupMask) == (requirements & SetupGroupMask) &&
            (station & requirements) == requirements;
        int[] stations = setups.Select(s => s.Station).Distinct()
            .Where(station => !setups.Any(s => s.Station != station &&
                CanHost(s.Station, station))).Order().ToArray();
        var members = stations.Select(_ => new List<int>()).ToArray();
        foreach (int i in Enumerable.Range(0, ranked.Length)
                     .OrderByDescending(i => BitCount(setups[i].Station)))
        {
            int target = Enumerable.Range(0, stations.Length)
                .Where(g => CanHost(stations[g], setups[i].Station))
                .OrderBy(g => start?.Station == stations[g] ? 0 : 1)
                .ThenBy(g => setups.Any(s => s.Station == stations[g] && s.Floor == setups[i].Floor) ? 0 : 1)
                .ThenBy(g => BitCount(stations[g] ^ setups[i].Station))
                .ThenBy(g => Array.FindIndex(setups, s => s.Station == stations[g]))
                .First();
            members[target].Add(i);
        }

        var visits = new List<Visit>();
        for (int g = 0; g < stations.Length; g++)
        {
            int[] standing = members[g].Where(i => !setups[i].Floor).Order().ToArray();
            int[] floor = members[g].Where(i => setups[i].Floor).Order().ToArray();
            int[] forward = [.. standing, .. floor];
            visits.Add(new(g, stations[g], setups[forward[0]].Floor,
                setups[forward[^1]].Floor, forward));
            if (standing.Length > 0 && floor.Length > 0)
            {
                int[] reverse = [.. floor, .. standing];
                visits.Add(new(g, stations[g], true, false, reverse));
            }
        }
        Visit[] choices = visits.OrderBy(v => v.Indices[0]).ToArray();
        int Transition(Setup? previous, Visit next) =>
            (previous is { } p && p.Station != next.Station
                ? StationChangeCost + RequirementChangeCost * BitCount(p.Station ^ next.Station) : 0) +
            ((previous?.Floor ?? false) != next.StartsOnFloor ? FloorChangeCost : 0) +
            (next.StartsOnFloor != next.EndsOnFloor ? FloorChangeCost : 0);

        // Finish equipment, then its moving/stationary section, then the exact
        // travel class before switching. This also finishes the current class
        // first after an explicit edit. Both routing strategies use this rule.
        int ContinuingGroups(int remaining, Setup? previous)
        {
            if (previous is not { } p) return remaining;
            int equipment = 0, locomotion = 0, exact = 0;
            int previousTravel = p.Station & LocomotionMask;
            for (int g = 0; g < stations.Length; g++)
            {
                int bit = 1 << g;
                if ((remaining & bit) == 0 ||
                    (stations[g] & EquipmentMask) != (p.Station & EquipmentMask)) continue;
                equipment |= bit;
                int travel = stations[g] & LocomotionMask;
                if ((travel != 0) != (previousTravel != 0)) continue;
                locomotion |= bit;
                if (travel == previousTravel) exact |= bit;
            }
            return exact != 0 ? exact : locomotion != 0 ? locomotion :
                equipment != 0 ? equipment : remaining;
        }

        // Exact routing is over stations, never over every exercise. A bounded
        // fallback keeps unusual future catalogs from making preparation slow.
        var memo = new Dictionary<(int Remaining, int Previous), int>();
        int Best(int remaining, int previous)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining == 0) return 0;
            if (memo.TryGetValue((remaining, previous), out int saved)) return saved;
            Setup? from = previous < 0 ? start :
                new Setup(choices[previous].Station, choices[previous].EndsOnFloor);
            int allowed = ContinuingGroups(remaining, from);
            int cost = int.MaxValue;
            foreach ((Visit next, int index) in choices.Select((v, i) => (v, i)))
            {
                if ((allowed & (1 << next.Group)) == 0) continue;
                cost = Math.Min(cost, Transition(from, next) +
                    Best(remaining & ~(1 << next.Group), index));
            }
            memo[(remaining, previous)] = cost;
            return cost;
        }
        var result = new List<T>(ranked.Length);
        int mask = (1 << stations.Length) - 1;
        int last = -1;
        while (mask != 0)
        {
            Setup? from = last < 0 ? start : new Setup(choices[last].Station, choices[last].EndsOnFloor);
            int allowed = ContinuingGroups(mask, from);
            int selected = -1, best = int.MaxValue;
            for (int i = 0; i < choices.Length; i++)
            {
                Visit next = choices[i];
                if ((allowed & (1 << next.Group)) == 0) continue;
                int cost = Transition(from, next) + (stations.Length <= ExactStationLimit
                    ? Best(mask & ~(1 << next.Group), i) : 0);
                if (cost < best) { best = cost; selected = i; }
            }
            Visit visit = choices[selected];
            result.AddRange(visit.Indices.Select(i => ranked[i]));
            mask &= ~(1 << visit.Group);
            last = selected;
        }
        return result.ToArray();
    }

    private sealed record Visit(int Group, int Station, bool StartsOnFloor,
        bool EndsOnFloor, int[] Indices);

    private static int BitCount(int bits)
    {
        int count = 0;
        for (; bits != 0; bits &= bits - 1) count++;
        return count;
    }

    public static int GetMuscularDemandPriority(int muscularDemand) =>
        muscularDemand switch
        {
            Exercise.MinimumMuscularDemand => 0,
            Exercise.MaximumMuscularDemand => 1,
            Exercise.ModerateMuscularDemand => 2,
            _ => throw new ArgumentOutOfRangeException(
                nameof(muscularDemand),
                muscularDemand,
                "Muscular demand must be 0, 1, or 2."),
        };

    public static int GetSequenceMuscularDemand(
        Exercise root,
        IReadOnlyDictionary<int, Exercise> exercisesById)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(exercisesById);

        if (root.SequenceBlocks.Length == 0)
        {
            throw new ArgumentException(
                "A scheduled exercise sequence must contain at least one block.",
                nameof(root));
        }

        return root.SequenceBlocks
            .Select(block => exercisesById.TryGetValue(
                    block.ExerciseId,
                    out Exercise? exercise)
                ? exercise.MuscularDemand
                : throw new ArgumentException(
                    $"Sequence block exercise {block.ExerciseId} is missing.",
                    nameof(exercisesById)))
            .Max();
    }
}
