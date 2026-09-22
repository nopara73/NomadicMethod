using NomadicMethod.Models;

namespace NomadicMethod.Services;

public readonly record struct OuraInterval(long Start, long End);
public readonly record struct OuraSample(long At, double Value);
public sealed record OuraSleep(long Start, long End, int WakeDay, int WakeHour,
    OuraInterval[] Asleep, OuraInterval[] Known);

public static class OuraNightBuilder
{
    // Oura exports approximately five-minute HR/HRV samples. Each distinct
    // timestamp covers at most +/- 2.5 minutes, clipped to known asleep time.
    // Gaps are not interpolated, and overlapping records cannot inflate coverage.
    public static OuraRecoverySnapshot Build(IEnumerable<OuraSleep> sleeps,
        IEnumerable<OuraSample> heartRate, IEnumerable<OuraSample> hrv, long now)
    {
        OuraSleep[] records = sleeps.Where(s => s.Start > 0 && s.End > s.Start && s.End <= now)
            .DistinctBy(s => (s.Start, s.End)).OrderBy(s => s.Start).ToArray();
        OuraSample[] hr = Samples(heartRate), variability = Samples(hrv);
        OuraInterval[] allAsleep = Union(records.SelectMany(s => s.Asleep));
        int latestWakeDay = records.Select(s => s.WakeDay).DefaultIfEmpty(0).Max();
        var result = new OuraRecoverySnapshot { FetchedAtUnixMilliseconds = now };
        foreach (var day in records.GroupBy(s => s.WakeDay).OrderByDescending(g => g.Key).Take(30))
        {
            OuraSleep main = day.OrderByDescending(s => Length(Union(s.Asleep)))
                .ThenByDescending(s => s.End).First();
            OuraInterval[] asleep = Union(main.Asleep);
            double mainMinutes = Length(asleep) / 60_000d;
            if (mainMinutes <= 0) continue;
            // Health Connect does not identify main sleep versus a nap. Do not
            // mistake a short afternoon nap for a disastrously short night.
            bool principal = mainMinutes >= 180 ||
                (mainMinutes >= 90 && main.WakeHour is >= 4 and <= 12);
            long known = Length(Clip(Union(main.Known), main.Start, main.End));
            bool reliable = principal && main.End - main.Start - known <= 1000;
            // Latest total is the last 24 hours at assessment, so a nap after
            // main sleep counts immediately. Historical totals end at wake-up.
            long windowEnd = day.Key == latestWakeDay ? now : main.End;
            long windowStart = windowEnd - 24 * 60 * 60 * 1000L;
            // An incompletely staged overlapping session makes the total unknown.
            reliable &= records.Where(s => s.End > windowStart && s.Start < windowEnd)
                .All(s => Length(Clip(Union(s.Known), Math.Max(windowStart, s.Start),
                    Math.Min(windowEnd, s.End))) >=
                    Math.Min(windowEnd, s.End) - Math.Max(windowStart, s.Start) - 1000);
            (double hrMean, double hrCoverage) = Summarize(hr, asleep);
            (double hrvMean, double hrvCoverage) = Summarize(variability, asleep);
            result.Nights.Add(new OuraRecoveryNight
            {
                WakeDay = main.WakeDay, SleepEndUnixMilliseconds = main.End,
                MainSleepMinutes = mainMinutes,
                SleepMinutes = Length(Clip(allAsleep, windowStart, windowEnd)) / 60_000d,
                DurationReliable = reliable, HeartRate = hrMean, Hrv = hrvMean,
                HeartRateCoverage = hrCoverage, HrvCoverage = hrvCoverage,
            });
        }
        return result;
    }

    private static OuraSample[] Samples(IEnumerable<OuraSample> source) => source
        .Where(s => s.At > 0 && double.IsFinite(s.Value) && s.Value > 0)
        .GroupBy(s => s.At).Select(g => new OuraSample(g.Key, g.Average(s => s.Value)))
        .OrderBy(s => s.At).ToArray();

    private static (double Mean, double Coverage) Summarize(OuraSample[] samples, OuraInterval[] asleep)
    {
        OuraSample[] eligible = samples.Where(s => asleep.Any(i => s.At >= i.Start && s.At < i.End)).ToArray();
        if (eligible.Length == 0 || Length(asleep) == 0) return (0, 0);
        OuraInterval[] covered = Union(eligible.SelectMany(s => Clip(asleep, s.At - 150_000, s.At + 150_000)));
        return (eligible.Average(s => s.Value), Math.Min(1, Length(covered) / (double)Length(asleep)));
    }

    private static long Length(IEnumerable<OuraInterval> intervals) => intervals.Sum(i => i.End - i.Start);
    private static OuraInterval[] Clip(IEnumerable<OuraInterval> intervals, long start, long end) =>
        intervals.Select(i => new OuraInterval(Math.Max(i.Start, start), Math.Min(i.End, end)))
            .Where(i => i.End > i.Start).ToArray();
    private static OuraInterval[] Union(IEnumerable<OuraInterval> source)
    {
        var result = new List<OuraInterval>();
        foreach (OuraInterval range in source.Where(i => i.End > i.Start).OrderBy(i => i.Start))
        {
            if (result.Count > 0 && range.Start <= result[^1].End)
                result[^1] = new(result[^1].Start, Math.Max(range.End, result[^1].End));
            else result.Add(range);
        }
        return result.ToArray();
    }
}
