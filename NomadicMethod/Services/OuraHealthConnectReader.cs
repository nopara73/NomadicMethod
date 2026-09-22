using Android.Content;
using Android.Content.PM;
using Android.Health.Connect;
using Android.Health.Connect.DataTypes;
using Android.OS;
using NomadicMethod.Models;
using Java.Time;
using System.Runtime.Versioning;

namespace NomadicMethod.Services;

[SupportedOSPlatform("android34.0")]
internal sealed class OuraHealthConnectReader(Context context)
{
    internal static readonly string[] Permissions =
    [
        "android.permission.health.READ_SLEEP",
        "android.permission.health.READ_HEART_RATE",
        "android.permission.health.READ_HEART_RATE_VARIABILITY",
    ];

    internal static bool HasAccess(Context context) => OperatingSystem.IsAndroidVersionAtLeast(34) &&
        Permissions.All(p => context.CheckSelfPermission(p) == Permission.Granted);

    internal async Task<OuraRecoverySnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        if (!HasAccess(context)) throw new UnauthorizedAccessException("Health Connect access is missing.");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // The algorithm uses at most 28 earlier wake dates. Thirty days fit the
        // default grant window; no history, background or write permissions.
        long start = now - 30 * 24 * 60 * 60 * 1000L;
        var sleep = await ReadAsync<SleepSessionRecord>(start, now, cancellationToken);
        var heartRate = await ReadAsync<HeartRateRecord>(start, now, cancellationToken);
        var hrv = await ReadAsync<HeartRateVariabilityRmssdRecord>(start, now, cancellationToken);
        if (!HasAccess(context)) throw new UnauthorizedAccessException("Health Connect access was revoked.");
        return await Task.Run(() =>
        {
            var sleeps = sleep.Select(record =>
            {
                long end = record.EndTime.ToEpochMilli();
                int offset = record.EndZoneOffset.TotalSeconds;
                DateTimeOffset local = DateTimeOffset.FromUnixTimeMilliseconds(end).ToOffset(TimeSpan.FromSeconds(offset));
                int day = (int)(local.Date - DateTime.UnixEpoch).TotalDays;
                var stages = record.Stages.ToArray();
                return new OuraSleep(record.StartTime.ToEpochMilli(), end, day, local.Hour,
                    stages.Where(s => (int)s.Type is 2 or 4 or 5 or 6)
                        .Select(s => new OuraInterval(s.StartTime.ToEpochMilli(), s.EndTime.ToEpochMilli())).ToArray(),
                    stages.Where(s => (int)s.Type != 0)
                        .Select(s => new OuraInterval(s.StartTime.ToEpochMilli(), s.EndTime.ToEpochMilli())).ToArray());
            });
            return OuraNightBuilder.Build(sleeps,
                heartRate.SelectMany(r => r.Samples).Select(s => new OuraSample(s.Time.ToEpochMilli(), s.BeatsPerMinute)),
                hrv.Select(r => new OuraSample(r.Time.ToEpochMilli(), r.HeartRateVariabilityMillis)), now);
        }, cancellationToken);
    }

    private async Task<List<T>> ReadAsync<T>(long start, long end, CancellationToken cancellationToken)
        where T : Android.Health.Connect.DataTypes.Record
    {
        var manager = context.GetSystemService(Context.HealthconnectService) as HealthConnectManager
            ?? throw new NotSupportedException("Health Connect is unavailable.");
        var result = new List<T>();
        long token = -1;
        var seenTokens = new HashSet<long>();
        for (int page = 0; page < 100; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var origin = new DataOrigin.Builder().SetPackageName("com.ouraring.oura")!.Build();
            using var range = new TimeInstantRangeFilter.Builder()
                .SetStartTime(Instant.OfEpochMilli(start))!.SetEndTime(Instant.OfEpochMilli(end))!.Build();
            using var builder = new ReadRecordsRequestUsingFilters.Builder(Java.Lang.Class.FromType(typeof(T)));
            builder.AddDataOrigins(origin!);
            builder.SetTimeRangeFilter(range!);
            builder.SetPageSize(1000);
            if (token != -1) builder.SetPageToken(token);
            using var request = builder.Build();
            // The platform can finish after a timeout/cancellation. Do not
            // dispose its Java callback while that request remains in flight.
            var receiver = new ReadReceiver();
            manager.ReadRecords(request!, context.MainExecutor!, receiver);
            var response = await receiver.Completion.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            foreach (var record in response.Records!.OfType<T>())
            {
                // Defense in depth: never consume a merged Health Connect total
                // or an imported reading from a different data origin.
                if (record.Metadata.DataOrigin.PackageName == "com.ouraring.oura" &&
                    (int)record.Metadata.RecordingMethod != 3)
                    result.Add(record);
            }
            token = response.NextPageToken;
            if (token == -1) return result.DistinctBy(r => r.Metadata.Id).ToList();
            if (!seenTokens.Add(token)) break;
        }
        throw new InvalidDataException("Incomplete Health Connect pagination.");
    }

    private sealed class ReadReceiver : Java.Lang.Object, IOutcomeReceiver
    {
        internal TaskCompletionSource<ReadRecordsResponse> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnResult(Java.Lang.Object? result)
        {
            if (result is ReadRecordsResponse response) Completion.TrySetResult(response);
            else Completion.TrySetException(new InvalidDataException("Unexpected Health Connect response."));
        }
        public void OnError(Java.Lang.Object? error) =>
            Completion.TrySetException(new IOException("Health Connect could not read recovery data."));
    }
}
