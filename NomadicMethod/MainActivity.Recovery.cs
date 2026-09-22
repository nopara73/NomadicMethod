using Android.Content.PM;
using NomadicMethod.Data;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod;

public partial class MainActivity
{
    private const int OuraPermissionRequest = 6321;
    private OuraRecoveryStore? _ouraStore;
    private OuraRecoveryCache _ouraCache = new();
    private CancellationTokenSource? _ouraReadCancellation;
    private bool _ouraRefreshing;
    private bool _manualLightModeRequested;

    private bool HasOuraPermission => OperatingSystem.IsAndroidVersionAtLeast(34) &&
        OuraHealthConnectReader.HasAccess(this);

    private void InitializeOuraRecovery()
    {
        _ouraStore = new OuraRecoveryStore(NoBackupFilesDir!.AbsolutePath);
        _ouraCache = _ouraStore.Load();
        _state.OuraRecovery = GetAvailableOuraSnapshot();
    }

    private OuraRecoverySnapshot? GetAvailableOuraSnapshot() =>
        HasOuraPermission ? _ouraCache.Snapshot : null;

    private async Task RefreshOuraRecoveryAsync(bool force = false)
    {
        if (_ouraStore is null || _ouraRefreshing || !_activityResumed ||
            !OperatingSystem.IsAndroidVersionAtLeast(34)) return;
        if (!HasOuraPermission)
        {
            if (_ouraCache.Snapshot is not null || _ouraCache.Decisions.Count > 0)
            {
                // Revocation removes only private health summaries, not workouts.
                _ouraCache = new() { PermissionRequestAttempted = true };
                _state.OuraRecovery = null;
                SaveOuraCache();
                RefreshRecoverySetup();
            }
            // Android owns consent. Ask once on a normal setup screen, never
            // interrupt a restored workout or add an app-level opt-in control.
            if (!_ouraCache.PermissionRequestAttempted && _applicationStartupCompleted &&
                _appScreen == AppScreen.Duration && _state.ActiveWorkoutSession is null &&
                IsOuraInstalled())
            {
                _ouraCache.PermissionRequestAttempted = true;
                SaveOuraCache();
                RequestPermissions(OuraHealthConnectReader.Permissions, OuraPermissionRequest);
            }
            return;
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!force && _ouraCache.Snapshot is { } cached &&
            now >= cached.FetchedAtUnixMilliseconds && now - cached.FetchedAtUnixMilliseconds < 300_000)
            return;
        _ouraRefreshing = true;
        _ouraReadCancellation?.Dispose();
        _ouraReadCancellation = new CancellationTokenSource();
        CancellationToken cancellation = _ouraReadCancellation.Token;
        try
        {
            OuraRecoverySnapshot snapshot = await new OuraHealthConnectReader(this).ReadAsync(cancellation);
            if (_activityDestroyed || cancellation.IsCancellationRequested) return;
            _ouraCache.Snapshot = snapshot;
            _state.OuraRecovery = GetAvailableOuraSnapshot();
            LogOuraDecision();
            RefreshRecoverySetup();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            if (_activityDestroyed || cancellation.IsCancellationRequested) return;
            // An unavailable sensor must not crash a workout or retain an old
            // positive clearance. Never log health records or exception messages.
            Android.Util.Log.Warn("NomadicMethodRecovery", $"Health read failed: {e.GetType().Name}");
            _ouraCache.Snapshot = null;
            _state.OuraRecovery = null;
            SaveOuraCache();
            RefreshRecoverySetup();
        }
        finally { _ouraRefreshing = false; }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android34.0")]
    private bool IsOuraInstalled()
    {
        try
        {
            using var flags = Android.Content.PM.PackageManager.ApplicationInfoFlags.Of(0);
            using var info = PackageManager?.GetApplicationInfo("com.ouraring.oura", flags);
            return info is not null;
        }
        catch (PackageManager.NameNotFoundException) { return false; }
    }

    private void RefreshRecoverySetup()
    {
        if (_appScreen != AppScreen.Duration || !_applicationStartupCompleted) return;
        UpdateLightModifierPresentation((_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);
        // New evidence affects the next workout, never a running one. Invalidating
        // a ready plan preserves instant Start without using stale evidence.
        if (!_editingActiveWorkoutSetup)
        {
            _workoutPreparationCancellation?.Cancel();
            _preparedWorkout = null;
            _workoutPreparationTask = null;
            QueueWorkoutPreparation();
        }
    }

    private void SaveOuraCache()
    {
        try { _ouraStore?.Save(_ouraCache); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Android.Util.Log.Warn("NomadicMethodRecovery", "Could not save private health cache.");
        }
    }

    private string GetRestFeedbackReason()
    {
        if (IsAutomaticLightModeLocked())
        {
            if (_state.ActiveWorkoutSession is { AutomaticLightRequiredAtStart: true } active)
            {
                // A frozen workout must describe its original decision, not a
                // later refresh. Health evidence stays in the private cache.
                OuraDecisionAudit? start = HasOuraPermission ? _ouraCache.Decisions.FirstOrDefault(
                    decision => decision.WorkoutSessionId > 0 && decision.WorkoutSessionId == active.SessionId) : null;
                return start?.Assessment.Verdict == OuraRecoveryVerdict.Light
                    ? RestFeedback.Oura(start.Assessment)
                    : start?.CadenceDue == true ? RestFeedback.Cadence : RestFeedback.FrozenWorkout;
            }
            OuraRecoveryAssessment result = OuraRecoveryPolicy.Evaluate(_state,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            return result.Verdict == OuraRecoveryVerdict.Light
                ? RestFeedback.Oura(result) : RestFeedback.Cadence;
        }
        return RestFeedback.MuscleRecovery;
    }

    private void LogOuraDecision(bool workoutStarted = false)
    {
        if (!HasOuraPermission) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool cadence = WorkoutLightDayPolicy.IsLightDayDue(_state.WorkoutHistory, now,
            TimeZoneInfo.Local, _state.LegacyCompletedTrainingDayUnixMilliseconds);
        var result = OuraRecoveryPolicy.Evaluate(_state, now);
        _ouraCache.Decisions.Add(new(now, cadence,
            OuraRecoveryPolicy.RequiresLight(cadence, result), result,
            workoutStarted ? _state.ActiveWorkoutSession?.SessionId ?? 0 : 0));
        SaveOuraCache();
    }

    public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == OuraPermissionRequest && HasOuraPermission)
            await RefreshOuraRecoveryAsync(force: true);
    }
}
