using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod;

public partial class MainActivity
{
    private bool _feedbackEnabled, _feedbackHeld, _feedbackResumeAfterHold, _feedbackExpiring;
    private bool _feedbackSettling, _feedbackMultitouch;
    private float _feedbackStartX, _feedbackStartY, _feedbackX, _feedbackY;
    private int _feedbackPointer = -1, _feedbackEpoch;
    private ExerciseOutcome? _feedbackDirection;
    private readonly Dictionary<ExerciseOutcome, TextView> _feedbackHints = [];
    private FrameLayout _feedbackSheet = null!;
    private TextView _feedbackCaption = null!;
    private LinearLayout _feedbackFooter = null!;
    private TextView _feedbackReceipt = null!;
    private Button _feedbackUndo = null!;
    private bool FeedbackReducedMotion => OperatingSystem.IsAndroidVersionAtLeast(26) &&
        !Android.Animation.ValueAnimator.AreAnimatorsEnabled();

    private void ConfigureExerciseFeedback()
    {
        _exerciseMediaArea.SetClipChildren(false);
        _feedbackSheet = new FrameLayout(this) { Clickable = true };
        _exerciseMediaArea.RemoveView(_exerciseMediaCard);
        _feedbackSheet.AddView(_exerciseMediaCard);
        _exerciseMediaArea.AddView(_feedbackSheet, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center));
        _feedbackCaption = new TextView(this) { Gravity = GravityFlags.Center, Visibility = ViewStates.Gone };
        _feedbackCaption.SetTextSize(Android.Util.ComplexUnitType.Sp, 15);
        _feedbackCaption.SetTextColor(new Color(GetColor(Resource.Color.primary_text)));
        _feedbackCaption.SetPadding(DpInt(12), DpInt(8), DpInt(12), DpInt(8));
        _feedbackCaption.SetMaxLines(3);
        _feedbackSheet.AddView(_feedbackCaption, new FrameLayout.LayoutParams(-1, DpInt(64), GravityFlags.Bottom));
        _feedbackSheet.Touch += OnExerciseFeedbackTouch;
        _feedbackSheet.SetAccessibilityDelegate(new FeedbackAccessibilityDelegate(this));
        foreach (ExerciseOutcome response in new[] { ExerciseOutcome.X, ExerciseOutcome.Tick,
                     ExerciseOutcome.TooEasy, ExerciseOutcome.TooHard })
        {
            var hint = new TextView(this) { Text = ExerciseFeedbackPolicy.Symbol(response),
                Gravity = GravityFlags.Center, Alpha = 0, Elevation = Dp(12),
                ImportantForAccessibility = ImportantForAccessibility.No };
            hint.SetTextSize(Android.Util.ComplexUnitType.Sp,
                response is ExerciseOutcome.X or ExerciseOutcome.Tick ? 42 : 15);
            hint.SetTextColor(new Color(GetColor(Resource.Color.primary_text)));
            hint.SetPadding(DpInt(8), DpInt(4), DpInt(8), DpInt(4));
            var background = new GradientDrawable();
            background.SetColor(new Color(GetColor(Resource.Color.surface)));
            background.SetCornerRadius(Dp(24));
            hint.Background = background;
            var gravity = response switch {
                ExerciseOutcome.X => GravityFlags.Left | GravityFlags.CenterVertical,
                ExerciseOutcome.Tick => GravityFlags.Right | GravityFlags.CenterVertical,
                ExerciseOutcome.TooEasy => GravityFlags.Top | GravityFlags.CenterHorizontal,
                _ => GravityFlags.Bottom | GravityFlags.CenterHorizontal,
            };
            var layout = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent,
                DpInt(56), gravity);
            _exerciseMediaArea.AddView(hint, layout);
            _feedbackHints.Add(response, hint);
        }
        _feedbackFooter = new LinearLayout(this) { Orientation = Orientation.Horizontal,
            Visibility = ViewStates.Gone };
        _feedbackFooter.SetGravity(GravityFlags.CenterVertical);
        _feedbackReceipt = new TextView(this) { Gravity = GravityFlags.CenterVertical,
            AccessibilityLiveRegion = AccessibilityLiveRegion.Polite };
        _feedbackReceipt.SetTextColor(new Color(GetColor(Resource.Color.primary_text)));
        _feedbackUndo = new Button(this) { Text = "↶  undo", ContentDescription = "Undo exercise feedback" };
        _feedbackUndo.SetAllCaps(false);
        _feedbackUndo.SetTextColor(new Color(GetColor(Resource.Color.primary_text)));
        _feedbackUndo.SetBackgroundColor(Color.Transparent);
        _feedbackUndo.Click += (_, _) => {
            if (!_feedbackEnabled || _feedbackExpiring) return;
            _sessionService.SetPendingRestFeedback(_state, null);
            _stateStore.Save(_state); RenderExerciseFeedback(); UpdateRestCountdownText();
        };
        _feedbackFooter.AddView(_feedbackReceipt, new LinearLayout.LayoutParams(0, DpInt(52), 1));
        _feedbackFooter.AddView(_feedbackUndo, new LinearLayout.LayoutParams(DpInt(88), DpInt(52)));
        _restPanel.AddView(_feedbackFooter, new LinearLayout.LayoutParams(-1, DpInt(52)));
    }

    private void ShowExerciseFeedback(bool enabled)
    {
        CancelExerciseFeedback();
        _feedbackEnabled = enabled;
        _feedbackFooter.Visibility = enabled ? ViewStates.Visible : ViewStates.Gone;
        _exerciseName.Visibility = enabled ? ViewStates.Gone : ViewStates.Visible;
        _feedbackCaption.Visibility = enabled ? ViewStates.Visible : ViewStates.Gone;
        _feedbackCaption.Text = _currentExercise?.Name;
        _feedbackSheet.Focusable = enabled;
        _exerciseMediaArea.Post(ResizeMediaCard);
        _feedbackSheet.ImportantForAccessibility = enabled
            ? ImportantForAccessibility.Yes : ImportantForAccessibility.No;
        if (enabled)
        {
            RenderFullWorkoutPhase(Resource.Color.screen_background);
            _exerciseMediaCard.Animate()?.Cancel();
            _exerciseMediaCard.Alpha = _exerciseMediaCard.ScaleX = _exerciseMediaCard.ScaleY = 1;
            var paper = new GradientDrawable();
            paper.SetColor(new Color(GetColor(Resource.Color.surface)));
            paper.SetCornerRadius(Dp(22));
            _feedbackSheet.Background = paper; _feedbackSheet.ClipToOutline = true;
            _feedbackSheet.Elevation = Dp(5);
            _exerciseMediaCard.SetBackgroundColor(Color.Transparent);
            _exerciseMediaCard.Elevation = 0;
            _exerciseMediaCard.SetPadding(0, 0, 0, 0);
            _restCountdownText.SetTextColor(new Color(GetColor(Resource.Color.primary_text)));
            _restPlaybackAction.SetColorFilter(new Color(GetColor(Resource.Color.primary_text)));
            _restProgress.ProgressTintList = Android.Content.Res.ColorStateList.ValueOf(
                new Color(GetColor(Resource.Color.brand_graphite)));
            _restProgress.ProgressBackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(new Color(GetColor(Resource.Color.surface_outline_soft)));
            RenderExerciseFeedback();
            ScheduleFeedbackHint(_feedbackEpoch);
        }
        else
        {
            _restCountdownText.SetTextColor(new Color(GetColor(Resource.Color.rest_text)));
            _restPlaybackAction.SetColorFilter(new Color(GetColor(Resource.Color.rest_text)));
            _restProgress.ProgressTintList = Android.Content.Res.ColorStateList.ValueOf(
                new Color(GetColor(Resource.Color.rest_accent)));
            _restProgress.ProgressBackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(new Color(GetColor(Resource.Color.rest_track)));
        }
    }

    private void RenderExerciseFeedback()
    {
        ExerciseOutcome? selected = ExerciseSessionService.GetPendingRestFeedback(_state);
        _feedbackReceipt.Text = selected is { } value ? ExerciseFeedbackPolicy.Symbol(value) : "";
        _feedbackReceipt.SetTextSize(Android.Util.ComplexUnitType.Sp,
            selected is ExerciseOutcome.X or ExerciseOutcome.Tick ? 36 : 16);
        _feedbackReceipt.ContentDescription = selected is { } response
            ? ExerciseFeedbackPolicy.Description(response) + ". Feedback saved. Undo available." : "";
        _feedbackUndo.Visibility = selected is null ? ViewStates.Invisible : ViewStates.Visible;
        _feedbackSheet.ContentDescription = selected is null
            ? "Exercise feedback. Swipe left for Meh, right for Love it, up for too easy, down for too hard."
            : _feedbackReceipt.ContentDescription;
    }

    private void OnExerciseFeedbackTouch(object? sender, View.TouchEventArgs args)
    {
        MotionEvent? e = args.Event;
        if (!_feedbackEnabled || !_restActive || e is null || _feedbackExpiring) return;
        args.Handled = true;
        if (e.ActionMasked == MotionEventActions.Down)
        {
            _feedbackSheet.Animate()?.Cancel();
            _feedbackPointer = e.GetPointerId(0);
            _feedbackX = _feedbackSheet.TranslationX; _feedbackY = _feedbackSheet.TranslationY;
            _feedbackStartX = e.RawX - _feedbackX; _feedbackStartY = e.RawY - _feedbackY;
            _feedbackDirection = null; _feedbackMultitouch = false;
            HoldFeedbackClock(true);
            return;
        }
        if (!_feedbackHeld) return;
        if (e.ActionMasked == MotionEventActions.PointerDown)
        {
            _feedbackMultitouch = true; _feedbackDirection = null;
            foreach (var hint in _feedbackHints.Values) { hint.Animate()?.Cancel(); hint.Alpha = 0; }
            ResetFeedbackPose(true); return;
        }
        if (e.ActionMasked == MotionEventActions.Cancel) { EndFeedbackDrag(false); return; }
        if (_feedbackMultitouch)
        {
            if (e.ActionMasked == MotionEventActions.Up) EndFeedbackDrag(false);
            return;
        }
        if (e.GetPointerId(e.ActionIndex) != _feedbackPointer) return;
        if (e.ActionMasked is MotionEventActions.Move or MotionEventActions.Up)
        {
            _feedbackX = e.RawX - _feedbackStartX; _feedbackY = e.RawY - _feedbackStartY;
            if (ExerciseSessionService.GetPendingRestFeedback(_state) is null)
            {
                ExerciseOutcome? previousDirection = _feedbackDirection;
                _feedbackDirection = ExerciseFeedbackPolicy.Direction(_feedbackX / Dp(1),
                    _feedbackY / Dp(1), _feedbackDirection);
                _feedbackSheet.TranslationX = _feedbackX;
                _feedbackSheet.TranslationY = _feedbackY;
                _feedbackSheet.Rotation = FeedbackReducedMotion ? 0 : Math.Clamp(_feedbackX / Dp(45), -7, 7);
                double progress = ExerciseFeedbackPolicy.Distance(_feedbackX / Dp(1), _feedbackY / Dp(1), _feedbackDirection)
                    / ExerciseFeedbackPolicy.Threshold(_exerciseMediaCard.Width / Dp(1));
                foreach (var (response, hint) in _feedbackHints)
                {
                    if (previousDirection != _feedbackDirection)
                    {
                        hint.Animate()?.Cancel();
                        hint.Animate()?.Alpha(_feedbackDirection is null ? 0 : response == _feedbackDirection ? 1 : .35f)
                            .SetDuration(FeedbackReducedMotion ? 0 : 100).Start();
                    }
                    if (hint.Background is GradientDrawable backdrop)
                        backdrop.SetColor(new Color(GetColor(response == _feedbackDirection && progress >= 1
                            ? Resource.Color.brand_chartreuse : Resource.Color.surface)));
                    hint.ScaleX = hint.ScaleY = response == _feedbackDirection && progress >= 1 ? 1.12f : 1;
                }
                if (e.ActionMasked == MotionEventActions.Up && progress >= 1 && _feedbackDirection is { } choice)
                    ChooseExerciseFeedback(choice);
            }
            if (e.ActionMasked == MotionEventActions.Up) EndFeedbackDrag(true);
        }
    }

    private void EndFeedbackDrag(bool animate)
    {
        _feedbackPointer = -1; _feedbackDirection = null;
        foreach (var hint in _feedbackHints.Values) { hint.Animate()?.Cancel(); hint.Alpha = 0; }
        ResetFeedbackPose(animate);
        HoldFeedbackClock(false);
    }

    private void HoldFeedbackClock(bool held, bool resume = true)
    {
        if (!_restActive) return;
        if (held)
        {
            _feedbackResumeAfterHold = !_state.PendingRestPausedByUser;
            _feedbackHeld = true;
            long remaining = _sessionService.GetPendingRestMillisecondsRemaining(_state,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (_feedbackResumeAfterHold && remaining > 0)
                _sessionService.PauseRest(_state, _currentWorkoutGroup, remaining);
            PauseRestCountdown(); _stateStore.Save(_state);
        }
        else
        {
            bool shouldResume = _feedbackHeld && _feedbackResumeAfterHold && resume && _activityResumed;
            _feedbackHeld = _feedbackResumeAfterHold = false;
            if (shouldResume && _state.PendingRestPausedByUser)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long remaining = _sessionService.GetPendingRestMillisecondsRemaining(_state, now);
                _sessionService.ResumeRest(_state, _currentWorkoutGroup, now + Math.Max(1, remaining));
                _stateStore.Save(_state); ResumeRestCountdown();
            }
        }
        UpdateRestPlaybackActionVisual();
    }

    private void ChooseExerciseFeedback(ExerciseOutcome response)
    {
        if (!_feedbackEnabled || !_restActive || ExerciseSessionService.GetPendingRestFeedback(_state) is not null) return;
        if (!_sessionService.SetPendingRestFeedback(_state, response)) return;
        _stateStore.Save(_state); RenderExerciseFeedback(); UpdateRestCountdownText();
        _feedbackSettling = true;
        int epoch = _feedbackEpoch;
        _feedbackSheet.PostDelayed(() => {
            if (epoch != _feedbackEpoch) return;
            _feedbackSettling = false;
            if (_restActive && !_feedbackHeld && !_feedbackExpiring &&
                _sessionService.GetPendingRestMillisecondsRemaining(_state, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) <= 0)
                CompleteRest();
        }, FeedbackReducedMotion ? 1 : 280);
        _feedbackSheet.PerformHapticFeedback(FeedbackConstants.KeyboardTap);
        AnnouncePhaseForAccessibility(_feedbackReceipt, _feedbackReceipt.ContentDescription ?? "Feedback saved");
    }

    private void AnimateAutomaticMeh()
    {
        _feedbackExpiring = true;
        PauseRestCountdown();
        _sessionService.PauseRest(_state, _currentWorkoutGroup, 1);
        ChooseExerciseFeedback(ExerciseOutcome.X);
        int epoch = _feedbackEpoch;
        if (!FeedbackReducedMotion)
            _feedbackSheet.Animate()?.TranslationX(-Dp(70)).Rotation(-2).SetDuration(180).Start();
        _exerciseMediaCard.PostDelayed(() => {
            if (epoch != _feedbackEpoch || !_activityResumed || !_restActive) return;
            ResetFeedbackPose(true);
            _exerciseMediaCard.PostDelayed(() => {
                if (epoch != _feedbackEpoch || !_activityResumed || !_restActive) return;
                _feedbackExpiring = false;
                CompleteRest();
            }, FeedbackReducedMotion ? 1 : 280);
        }, FeedbackReducedMotion ? 1 : 180);
    }

    private void ResetFeedbackPose(bool animate)
    {
        _feedbackSheet.Animate()?.Cancel();
        if (animate && !FeedbackReducedMotion)
            _feedbackSheet.Animate()?.TranslationX(0).TranslationY(0).Rotation(0)
                .SetDuration(260).SetInterpolator(new Android.Views.Animations.DecelerateInterpolator(1.8f)).Start();
        else { _feedbackSheet.TranslationX = _feedbackSheet.TranslationY = _feedbackSheet.Rotation = 0; }
    }

    private void ScheduleFeedbackHint(int epoch)
    {
        _exerciseMediaCard.PostDelayed(() => {
            if (epoch != _feedbackEpoch || !_feedbackEnabled || !_activityResumed) return;
            if (!_feedbackHeld && !_feedbackExpiring && !_state.PendingRestPausedByUser &&
                ExerciseSessionService.GetPendingRestFeedback(_state) is null && !FeedbackReducedMotion)
            {
                _feedbackSheet.Animate()?.TranslationX(-Dp(9)).SetDuration(240).Start();
                _exerciseMediaCard.PostDelayed(() => {
                    if (epoch == _feedbackEpoch && !_feedbackHeld) ResetFeedbackPose(true);
                }, 260);
            }
            ScheduleFeedbackHint(epoch);
        }, 4200);
    }

    private void CancelExerciseFeedback()
    {
        _feedbackEpoch++;
        _feedbackSettling = false;
        if (_feedbackHeld) HoldFeedbackClock(false, resume: false);
        _feedbackHeld = _feedbackResumeAfterHold = _feedbackExpiring = _feedbackEnabled = false;
        foreach (var hint in _feedbackHints.Values) { hint.Animate()?.Cancel(); hint.Alpha = 0; }
        if (_feedbackSheet is not null)
        {
            ResetFeedbackPose(false);
            _feedbackSheet.SetBackgroundColor(Color.Transparent); _feedbackSheet.ClipToOutline = false;
            _feedbackSheet.Elevation = 0;
            _feedbackCaption.Visibility = ViewStates.Gone;
            _exerciseName.Visibility = ViewStates.Visible;
            _exerciseMediaCard.Elevation = Dp(5);
            _exerciseMediaCard.SetPadding(DpInt(4), DpInt(4), DpInt(4), DpInt(4));
            _exerciseMediaArea.Post(ResizeMediaCard);
        }
    }

    private sealed class FeedbackAccessibilityDelegate(MainActivity owner) : View.AccessibilityDelegate
    {
        public override void OnInitializeAccessibilityNodeInfo(View host, AccessibilityNodeInfo info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (!owner._feedbackEnabled || ExerciseSessionService.GetPendingRestFeedback(owner._state) is not null) return;
            foreach (ExerciseOutcome response in new[] { ExerciseOutcome.X, ExerciseOutcome.Tick, ExerciseOutcome.TooEasy, ExerciseOutcome.TooHard })
                info.AddAction(new AccessibilityNodeInfo.AccessibilityAction(5000 + (int)response,
                    new Java.Lang.String(ExerciseFeedbackPolicy.Description(response))));
        }
        public override bool PerformAccessibilityAction(View host, [Android.Runtime.GeneratedEnum] Android.Views.Accessibility.Action action, Bundle? arguments)
        {
            ExerciseOutcome response = (ExerciseOutcome)((int)action - 5000);
            if (owner._feedbackEnabled && ExerciseFeedbackPolicy.IsResponse(response))
            { owner.ChooseExerciseFeedback(response); return true; }
            return base.PerformAccessibilityAction(host, action, arguments);
        }
    }
}
