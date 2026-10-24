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
    private bool _feedbackSettling, _feedbackMultitouch, _feedbackScreenGesture;
    private float _feedbackStartX, _feedbackStartY, _feedbackX, _feedbackY;
    private int _feedbackPointer = -1, _feedbackEpoch;
    private ExerciseOutcome? _feedbackDirection;
    private readonly Dictionary<ExerciseOutcome, TextView> _feedbackHints = [];
    private FrameLayout _feedbackSheet = null!;
    private TextView _feedbackCaption = null!;
    private LinearLayout _feedbackFooter = null!;
    private TextView _feedbackReceipt = null!;
    private Button _feedbackUndo = null!;
    private FrameLayout _feedbackBurst = null!;
    private View _feedbackBurstWash = null!;
    private TextView _feedbackBurstEmoji = null!;
    private Android.Animation.ValueAnimator? _feedbackBurstAnimator;
    private Android.Animation.ValueAnimator? _feedbackNudgeAnimator;
    private bool FeedbackReducedMotion => OperatingSystem.IsAndroidVersionAtLeast(26) &&
        !Android.Animation.ValueAnimator.AreAnimatorsEnabled();

    private void ConfigureExerciseFeedback()
    {
        _exerciseMediaArea.SetClipChildren(false);
        // The inset content would otherwise clip the entire media-area layer,
        // including its growing emoji, even with clipping disabled on the area.
        if (_exerciseMediaArea.Parent is ViewGroup content)
        {
            content.SetClipChildren(false);
            content.SetClipToPadding(false);
        }
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
        _feedbackSheet.SetAccessibilityDelegate(new FeedbackAccessibilityDelegate(this));
        foreach (ExerciseOutcome response in new[] { ExerciseOutcome.X, ExerciseOutcome.Tick,
                     ExerciseOutcome.TooEasy, ExerciseOutcome.TooHard })
        {
            var hint = new TextView(this) { Text = ExerciseFeedbackPolicy.Symbol(response),
                Gravity = GravityFlags.Center, Alpha = 0,
                ImportantForAccessibility = ImportantForAccessibility.No };
            hint.SetTextSize(Android.Util.ComplexUnitType.Dip, 256);
            hint.SetIncludeFontPadding(false);
            hint.SetTextColor(new Color(GetColor(Resource.Color.primary_text)));
            hint.SetPadding(0, 0, 0, 0);
            var gravity = response switch {
                ExerciseOutcome.X => GravityFlags.Left | GravityFlags.CenterVertical,
                ExerciseOutcome.Tick => GravityFlags.Right | GravityFlags.CenterVertical,
                ExerciseOutcome.TooEasy => GravityFlags.Top | GravityFlags.CenterHorizontal,
                _ => GravityFlags.Bottom | GravityFlags.CenterHorizontal,
            };
            var layout = new FrameLayout.LayoutParams(DpInt(320), DpInt(320), gravity);
            layout.SetMargins(-DpInt(108), -DpInt(108), -DpInt(108), -DpInt(108));
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
        _feedbackBurst = new FrameLayout(this) { Visibility = ViewStates.Gone,
            Clickable = true, Elevation = Dp(100), ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants };
        _feedbackBurstWash = new View(this);
        _feedbackBurstWash.SetBackgroundColor(new Color(GetColor(Resource.Color.screen_background)));
        _feedbackBurst.AddView(_feedbackBurstWash, new FrameLayout.LayoutParams(-1, -1));
        _feedbackBurstEmoji = new TextView(this) { Gravity = GravityFlags.Center };
        _feedbackBurstEmoji.SetTextSize(Android.Util.ComplexUnitType.Dip, 256);
        _feedbackBurstEmoji.SetIncludeFontPadding(false);
        _feedbackBurst.AddView(_feedbackBurstEmoji, new FrameLayout.LayoutParams(DpInt(320), DpInt(320), GravityFlags.Center));
        ((FrameLayout)_workoutScreen).AddView(_feedbackBurst, new FrameLayout.LayoutParams(-1, -1));
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
        _feedbackReceipt.SetTextSize(Android.Util.ComplexUnitType.Sp, 48);
        _feedbackReceipt.ContentDescription = selected is { } response
            ? ExerciseFeedbackPolicy.Description(response) + ". Feedback saved. Undo available." : "";
        _feedbackUndo.Visibility = selected is null ? ViewStates.Invisible : ViewStates.Visible;
        _feedbackSheet.ContentDescription = selected is null
            ? "Exercise feedback. Swipe left for Meh, right for Love it, up for too easy, down for too hard."
            : _feedbackReceipt.ContentDescription;
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e is null) return base.DispatchTouchEvent(e);
        if (e.ActionMasked == MotionEventActions.Down)
            _feedbackScreenGesture = _feedbackEnabled && _restActive && !_feedbackExpiring && !_feedbackSettling &&
                _workoutScreen.IsShown && !IsFeedbackControlAt(_workoutScreen, e.RawX, e.RawY);
        if (!_feedbackScreenGesture) return base.DispatchTouchEvent(e);
        HandleExerciseFeedbackTouch(e);
        if (e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel) _feedbackScreenGesture = false;
        return true;
    }

    // Test controls before owning the gesture, so pause/settings/Undo retain
    // normal taps. Blank header, footer and margins belong to the swipe surface.
    private bool IsFeedbackControlAt(View view, float x, float y)
    {
        if (!view.IsShown || !view.Enabled) return false;
        int[] position = new int[2];
        view.GetLocationOnScreen(position);
        if (x < position[0] || y < position[1] || x >= position[0] + view.Width || y >= position[1] + view.Height) return false;
        if (view is ViewGroup group)
            for (int i = group.ChildCount - 1; i >= 0; i--)
                if (group.GetChildAt(i) is { } child && IsFeedbackControlAt(child, x, y)) return true;
        return view != _feedbackSheet && (view.Clickable || view.LongClickable);
    }

    private void HandleExerciseFeedbackTouch(MotionEvent e)
    {
        if (e.ActionMasked == MotionEventActions.Down)
        {
            _feedbackNudgeAnimator?.Cancel();
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
                _feedbackDirection = ExerciseFeedbackPolicy.Direction(_feedbackX / Dp(1),
                    _feedbackY / Dp(1), _feedbackDirection);
                _feedbackSheet.TranslationX = _feedbackX;
                _feedbackSheet.TranslationY = _feedbackY;
                _feedbackSheet.Rotation = FeedbackReducedMotion ? 0 : Math.Clamp(_feedbackX / Dp(45), -7, 7);
                double progress = ExerciseFeedbackPolicy.Distance(_feedbackX / Dp(1), _feedbackY / Dp(1), _feedbackDirection)
                    / ExerciseFeedbackPolicy.Threshold(_exerciseMediaCard.Width / Dp(1));
                PreviewFeedback(_feedbackDirection, progress);
                if (e.ActionMasked == MotionEventActions.Up && progress >= 1 && _feedbackDirection is { } choice)
                    ChooseExerciseFeedback(choice);
            }
            if (e.ActionMasked == MotionEventActions.Up) EndFeedbackDrag(true);
        }
    }

    private void PreviewFeedback(ExerciseOutcome? direction, double progress)
    {
        var visual = ExerciseFeedbackPolicy.Visual(FeedbackReducedMotion ? 0 : progress,
            Math.Min(_workoutScreen.Width, _workoutScreen.Height) / Dp(1));
        int[] area = new int[2], screen = new int[2];
        _exerciseMediaArea.GetLocationOnScreen(area); _workoutScreen.GetLocationOnScreen(screen);
        foreach (var (response, hint) in _feedbackHints)
        {
            bool active = response == direction;
            hint.Alpha = direction is null ? 0 : active ? 1 : (float)visual.Others;
            hint.TranslationZ = active ? Dp(16) : 0;
            hint.ScaleX = hint.ScaleY = active ? (float)visual.Size / 256 : .25f;
            hint.TranslationX = active ? (screen[0] + _workoutScreen.Width / 2f - area[0] - hint.Left - hint.Width / 2f) * (float)visual.Travel : 0;
            hint.TranslationY = active ? (screen[1] + _workoutScreen.Height / 2f - area[1] - hint.Top - hint.Height / 2f) * (float)visual.Travel : 0;
        }
    }

    private void EndFeedbackDrag(bool animate)
    {
        _feedbackPointer = -1; _feedbackDirection = null;
        PreviewFeedback(null, 0);
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

    private void ChooseExerciseFeedback(ExerciseOutcome response, bool automatic = false)
    {
        if (!_feedbackEnabled || !_restActive || ExerciseSessionService.GetPendingRestFeedback(_state) is not null) return;
        if (!_sessionService.SetPendingRestFeedback(_state, response)) return;
        PauseRestCountdown();
        long remaining = _sessionService.GetPendingRestMillisecondsRemaining(_state, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (!_state.PendingRestPausedByUser)
            _sessionService.PauseRest(_state, _currentWorkoutGroup, Math.Max(1, remaining));
        _feedbackHeld = _feedbackResumeAfterHold = false;
        _stateStore.Save(_state); RenderExerciseFeedback(); UpdateRestCountdownText();
        AnimateFeedbackSelection(response, automatic);
        _feedbackSheet.PerformHapticFeedback(FeedbackConstants.KeyboardTap);
        AnnouncePhaseForAccessibility(_feedbackSheet, ExerciseFeedbackPolicy.Description(response) + ". Next exercise.");
    }

    private void AnimateAutomaticMeh()
    {
        _feedbackExpiring = true;
        ChooseExerciseFeedback(ExerciseOutcome.X, automatic: true);
    }

    private void AnimateFeedbackSelection(ExerciseOutcome response, bool automatic = false)
    {
        if (_feedbackSettling || !_restActive || !_activityResumed) return;
        _feedbackNudgeAnimator?.Cancel();
        _feedbackSettling = true;
        int epoch = _feedbackEpoch;
        _feedbackFooter.Alpha = 0;
        int[] hintPosition = new int[2], screenPosition = new int[2];
        TextView hint = _feedbackHints[response];
        _exerciseMediaArea.GetLocationOnScreen(hintPosition); _workoutScreen.GetLocationOnScreen(screenPosition);
        float dx = hintPosition[0] + hint.Left + hint.TranslationX + hint.Width / 2f - screenPosition[0] - _workoutScreen.Width / 2f;
        float dy = hintPosition[1] + hint.Top + hint.TranslationY + hint.Height / 2f - screenPosition[1] - _workoutScreen.Height / 2f;
        float startScale = hint.Alpha > 0 ? hint.ScaleX : .25f;
        foreach (var target in _feedbackHints.Values) { target.Animate()?.Cancel(); target.Alpha = 0; }
        _feedbackBurstEmoji.Text = ExerciseFeedbackPolicy.Symbol(response);
        _feedbackBurstEmoji.Alpha = 1;
        _feedbackBurstEmoji.TranslationX = dx; _feedbackBurstEmoji.TranslationY = dy;
        _feedbackBurstEmoji.ScaleX = _feedbackBurstEmoji.ScaleY = startScale;
        _feedbackBurstWash.Alpha = 0;
        _feedbackBurst.Visibility = ViewStates.Visible;
        if (!FeedbackReducedMotion)
        {
            if (automatic)
            {
                _feedbackSheet.Animate()?.TranslationX(-Dp(144)).Rotation(-3).SetDuration(160).Start();
                _feedbackSheet.PostDelayed(() => { if (epoch == _feedbackEpoch) ResetFeedbackPose(true); }, 160);
            }
            else ResetFeedbackPose(true);
            float fullScale = Math.Min(_workoutScreen.Width, _workoutScreen.Height) * .9f / _feedbackBurstEmoji.TextSize;
            _feedbackBurstAnimator = Android.Animation.ValueAnimator.OfFloat(0, 1)
                ?? throw new InvalidOperationException("Could not create feedback animation.");
            _feedbackBurstAnimator.SetDuration(640);
            _feedbackBurstAnimator.SetInterpolator(new Android.Views.Animations.LinearInterpolator());
            _feedbackBurstAnimator.Update += (_, args) => {
                float t = (float)args.Animation.AnimatedValue!;
                static float Ease(float value) => 1 - MathF.Pow(1 - Math.Clamp(value, 0, 1), 3);
                float travel = Ease(t / .45f);
                _feedbackBurstEmoji.TranslationX = dx * (1 - travel);
                _feedbackBurstEmoji.TranslationY = dy * (1 - travel);
                float size = t < .45f ? startScale + (fullScale * 1.06f - startScale) * travel
                    : t < .67f ? fullScale * (1.06f - .12f * Ease((t - .45f) / .22f))
                    : fullScale * (.94f + .06f * Ease((t - .67f) / .16f));
                _feedbackBurstEmoji.ScaleX = _feedbackBurstEmoji.ScaleY = size;
                _feedbackBurstEmoji.Alpha = t < .45f ? 1 : t < .67f ? 1 - .18f * Ease((t - .45f) / .22f)
                    : .82f + .18f * Ease((t - .67f) / .16f);
                _feedbackBurstWash.Alpha = Math.Min(1, t / .5f);
            };
            _feedbackBurstAnimator.Start();
        }
        _feedbackSheet.PostDelayed(() => {
            if (epoch != _feedbackEpoch || !_activityResumed || !_restActive) return;
            _feedbackSettling = _feedbackExpiring = false;
            CompleteRest();
        }, FeedbackReducedMotion ? 1 : 640);
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
                _feedbackNudgeAnimator?.Dispose();
                _feedbackNudgeAnimator = Android.Animation.ValueAnimator.OfFloat(0, 1)!;
                _feedbackNudgeAnimator.SetDuration(960);
                _feedbackNudgeAnimator.SetInterpolator(new Android.Views.Animations.LinearInterpolator());
                PreviewFeedback(null, 0);
                _feedbackNudgeAnimator.Update += (_, args) => {
                    float t = (float)args.Animation.AnimatedValue!;
                    float pull = t < .38f ? 1 - MathF.Pow(1 - t / .38f, 3)
                        : t < .52f ? 1 : t < .84f ? 1 - 1.1f * (1 - MathF.Pow(1 - (t - .52f) / .32f, 3))
                        : -.1f * (1 - (t - .84f) / .16f);
                    _feedbackSheet.TranslationX = -Dp(40) * pull;
                    _feedbackSheet.Rotation = -1.8f * pull;
                    _feedbackHints[ExerciseOutcome.X].Alpha = Math.Max(0, pull) * .8f;
                };
                _feedbackNudgeAnimator.Start();
            }
            ScheduleFeedbackHint(epoch);
        }, 4200);
    }

    private void CancelExerciseFeedback()
    {
        _feedbackEpoch++;
        _feedbackScreenGesture = false;
        _feedbackNudgeAnimator?.Cancel(); _feedbackNudgeAnimator?.Dispose(); _feedbackNudgeAnimator = null;
        _feedbackSettling = false;
        _feedbackBurstAnimator?.Cancel(); _feedbackBurstAnimator?.Dispose(); _feedbackBurstAnimator = null;
        if (_feedbackBurst is not null) _feedbackBurst.Visibility = ViewStates.Gone;
        if (_feedbackFooter is not null) _feedbackFooter.Alpha = 1;
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
