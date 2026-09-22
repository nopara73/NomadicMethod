using Android.Views;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Flux.Data;
using Flux.Models;
using Flux.Services;

namespace Flux;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    ConfigurationChanges =
        Android.Content.PM.ConfigChanges.Orientation |
        Android.Content.PM.ConfigChanges.ScreenSize |
        Android.Content.PM.ConfigChanges.ScreenLayout |
        Android.Content.PM.ConfigChanges.SmallestScreenSize |
        Android.Content.PM.ConfigChanges.UiMode |
        Android.Content.PM.ConfigChanges.Locale |
        Android.Content.PM.ConfigChanges.LayoutDirection |
        Android.Content.PM.ConfigChanges.FontScale)]
public partial class MainActivity : Activity
{
    private const int CountdownSeconds = 45;
    private const int RestSeconds = 15;
    private const long PhaseMotionDurationMilliseconds = 160L;
    private const long HueMotionDurationMilliseconds = 120L;
    private const long ModifierFeedbackEnterDurationMilliseconds = 140L;
    private const long ModifierFeedbackHoldMilliseconds = 1_200L;
    private const long ModifierFeedbackFadeDurationMilliseconds = 700L;
    private const float PlaybackControlEnabledAlpha = 1f;
    private const float PlaybackControlDisabledAlpha = 0.35f;

    private enum AppScreen
    {
        Duration,
        Workout,
        Completion,
    }

    private enum WorkoutPhase
    {
        Ready,
        Move,
        Rest,
    }

    private SqliteExerciseDatabase _exerciseDatabase = null!;
    private ExerciseSessionService _sessionService = null!;
    private IWorkoutStateStore _stateStore = null!;
    private WorkoutState _state = null!;
    private WorkoutGroup _currentWorkoutGroup = null!;
    private Exercise? _currentExercise;
    private WorkoutGroup? _mediaWorkoutGroup;
    private Exercise? _mediaExercise;
    private bool _previewingUpcomingSequenceBlock;
    private int _selectedWorkoutMinutes = ExerciseSessionService.DefaultWorkoutMinutes;
    private WorkoutModifiers _selectedWorkoutModifiers =
        ExerciseSessionService.DefaultWorkoutModifiers;

    private View _durationScreen = null!;
    private LinearLayout _durationInsetContent = null!;
    private ScrollView _durationScroll = null!;
    private LinearLayout _durationContent = null!;
    private LinearLayout _durationIdentity = null!;
    private ImageView _durationAppIcon = null!;
    private View _durationDial = null!;
    private LinearLayout _durationControls = null!;
    private LinearLayout _durationStepRow = null!;
    private FrameLayout _durationOptionLabels = null!;
    private HorizontalScrollView _durationModifierScroller = null!;
    private LinearLayout _durationModifierGroups = null!;
    private LinearLayout _durationContextModifierGroup = null!;
    private LinearLayout _durationIntensityModifierGroup = null!;
    private LinearLayout _durationEquipmentModifierGroup = null!;
    private CheckBox _upperBodyClothingModifierButton = null!;
    private CheckBox _hardFloorModifierButton = null!;
    private CheckBox _insectModifierButton = null!;
    private CheckBox _silenceModifierButton = null!;
    private CheckBox _shyModifierButton = null!;
    private FrameLayout _lightModifierContainer = null!;
    private CheckBox _lightModifierButton = null!;
    private bool _automaticLightModePresented;
    private bool _lightModifierLocked;
    private TextView _lightModifierCountdownBadge = null!;
    private CheckBox _wallModifierButton = null!;
    private CheckBox _mirrorModifierButton = null!;
    private TextView _durationModifierFeedback = null!;
    private int _modifierFeedbackGeneration;
    private FrameLayout _durationActionBar = null!;
    private ImageView _durationLockIcon = null!;
    private ImageView _durationActionIcon = null!;
    private TextView _durationMinutesValue = null!;
    private Button _durationDecreaseButton = null!;
    private SeekBar _durationSeekBar = null!;
    private Button _durationIncreaseButton = null!;
    private LinearLayout _durationOptionSegments = null!;
    private Button _beginWorkoutButton = null!;
    private ImageButton _endSessionButton = null!;
    private View _workoutScreen = null!;
    private View _workoutPhaseSurface = null!;
    private View _workoutPhaseLeft = null!;
    private View _workoutPhaseRight = null!;
    private LinearLayout _workoutInsetContent = null!;
    private LinearLayout _workoutControlColumn = null!;
    private LinearLayout _workoutHeader = null!;
    private ImageButton _workoutSetupButton = null!;
    private TextView _workoutProgressText = null!;
    private ProgressBar _workoutProgressBar = null!;
    private View _congratulationsScreen = null!;
    private LinearLayout _completionInsetContent = null!;
    private FrameLayout _completionHero = null!;
    private View _completionHalo = null!;
    private FrameLayout _completionActionBar = null!;
    private TextView _exerciseName = null!;
    private TextView _exerciseModeBadge = null!;
    private FrameLayout _executionSignifierHost = null!;
    private WorkoutBlockTimelineView _executionTimeline = null!;
    private FrameLayout _exerciseMediaArea = null!;
    private View _exerciseMediaCard = null!;
    private VideoView _exerciseVideo = null!;
    private ImageView _holdFrameImage = null!;
    private View _mediaScrim = null!;
    private ProgressBar _mediaLoadingIndicator = null!;
    private View _mediaErrorPanel = null!;
    private Button _mediaRetryButton = null!;
    private FrameLayout _workoutActionHost = null!;
    private LinearLayout _readyPanel = null!;
    private ImageButton _shuffleButton = null!;
    private ImageButton _startButton = null!;
    private LinearLayout _countdownPanel = null!;
    private TextView _countdownText = null!;
    private ProgressBar _countdownProgress = null!;
    private ImageButton _repeatAction = null!;
    private ImageButton _playbackAction = null!;
    private ImageButton _nextAction = null!;
    private LinearLayout _restPanel = null!;
    private ImageButton _restPlaybackAction = null!;
    private TextView _restCountdownText = null!;
    private ProgressBar _restProgress = null!;
    private ImageButton _keepButton = null!;
    private ImageView _completionMark = null!;
    private Button _doneButton = null!;
    private SystemBarsController[] _systemBarsControllers = [];
    private DurationSeekAccessibilityDelegate? _durationSeekAccessibilityDelegate;

    private WorkoutCountDownTimer? _countdownTimer;
    private WorkoutCountDownTimer? _restTimer;
    private Android.Media.SoundPool? _whistleSoundPool;
    private int _movementStartWhistleId;
    private int _restStartWhistleId;
    private int _workoutCompleteWhistleId;
    private Android.Media.MediaPlayer? _activeMediaPlayer;
    private VideoPreparedListener? _videoPreparedListener;
    private VideoErrorListener? _videoErrorListener;
    private VideoInfoListener? _videoInfoListener;
    private Android.Graphics.Bitmap? _holdFrameBitmap;
    private bool _countdownActive;
    private bool _countdownPaused;
    private long _countdownEndsAtElapsedMilliseconds;
    private long _countdownMillisecondsRemaining;
    private bool _restActive;
    private bool _activityResumed;
    private bool _loopExerciseVideo = true;
    private bool _freezeHoldAtEnd;
    private bool _mediaReady;
    private bool _countdownPausedForMediaError;
    private bool _countdownPausedByUser;
    private bool _automaticSequenceStartPending;
    private bool _applicationStartupCompleted;
    private bool _startWorkoutWhenReady;
    private CancellationTokenSource? _workoutPreparationCancellation;
    private Task<PreparedWorkout>? _workoutPreparationTask;
    private PreparedWorkout? _preparedWorkout;
    private int _preparingWorkoutMinutes;
    private WorkoutModifiers _preparingWorkoutModifiers;
    private bool _preparingWorkoutIsReconfiguration;
    private string? _preparingCurrentWorkoutGroupId;
    private bool _durationSelectionChangedDuringStartup;
    private bool _activityDestroyed;
    private int _mediaLoadGeneration;
    private int _revealedMediaGeneration = -1;
    private bool _hasRenderedScreen;
    private bool _editingActiveWorkoutSetup;
    private WorkoutPhase _workoutSetupReturnPhase = WorkoutPhase.Ready;
    private bool _workoutSetupShouldResume;
    private string? _workoutSetupCurrentGroupId;
    private string? _exerciseVideoCacheRoot;
    private AppScreen _appScreen = AppScreen.Duration;
    private WorkoutPhase _workoutPhase = WorkoutPhase.Ready;
    private MovementPhase? _lastMovementPhase;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Keep hardware volume keys on the whistle/media stream even while
        // the app is between cues and no sound is actively playing.
        VolumeControlStream = Android.Media.Stream.Music;

        SetContentView(Resource.Layout.activity_main);

        BindViews();
        ConfigureResponsiveText();
        ApplyResponsiveDimensions();
        ConfigureAccessibility();
        ConfigureSystemBars();
        BindEvents();
        ConfigureVideoView();
        ConfigureWhistleCues();

        _stateStore = new SharedPreferencesWorkoutStateStore(this);
        _state = _stateStore.Load();
        InitializeOuraRecovery();
        _selectedWorkoutMinutes = _state.LastWorkoutMinutes;
        _selectedWorkoutModifiers = _state.LastWorkoutModifiers;
        ShowDurationSelection();
        _ = InitializeApplicationAsync();
    }

    private async Task InitializeApplicationAsync()
    {
        Android.Content.Context context = ApplicationContext ?? this;
        ApplicationStartupResult startup;
        try
        {
            startup = await Task.Run(() => InitializeApplication(context))
                .ConfigureAwait(false);
        }
        catch (Exception error)
        {
            if (!_activityDestroyed)
            {
                RunOnUiThread(() => ShowApplicationStartupFailure(error));
            }
            return;
        }

        if (_activityDestroyed)
        {
            startup.Database.Dispose();
            return;
        }

        RunOnUiThread(() => CompleteApplicationStartup(startup));
    }

    private ApplicationStartupResult InitializeApplication(
        Android.Content.Context context)
    {
        var database = new SqliteExerciseDatabase(context);
        try
        {
            var sessionService = new ExerciseSessionService(database.Exercises);
            sessionService.RestoreAfterReopen(_state);
            RecoverPendingScoreUpdate(database, _state);

            WorkoutGroup? pendingMovementGroup =
                sessionService.GetPendingMovementGroup(_state);
            WorkoutGroup? pendingRestGroup =
                sessionService.GetPendingRestGroup(_state);

            _stateStore.Save(_state);

            return new ApplicationStartupResult(
                database,
                sessionService,
                pendingMovementGroup,
                pendingRestGroup);
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    private void CompleteApplicationStartup(ApplicationStartupResult startup)
    {
        if (_activityDestroyed)
        {
            startup.Database.Dispose();
            return;
        }

        _exerciseDatabase = startup.Database;
        _sessionService = startup.SessionService;
        _applicationStartupCompleted = true;

        if (_state.WorkoutCompleted && !_state.CompletionAcknowledged)
        {
            CancelQueuedWorkoutStart();
            ShowCongratulations();
        }
        else if (startup.PendingMovementGroup is not null)
        {
            CancelQueuedWorkoutStart();
            RestorePendingMovement(startup.PendingMovementGroup);
        }
        else if (startup.PendingRestGroup is not null)
        {
            CancelQueuedWorkoutStart();
            RestorePendingRest(startup.PendingRestGroup);
        }
        else if (_state.ActiveWorkoutMinutes != 0)
        {
            CancelQueuedWorkoutStart();
            ShowNextExercise();
        }
        else
        {
            if (!_durationSelectionChangedDuringStartup)
            {
                ShowDurationSelection();
            }

            if (_startWorkoutWhenReady)
            {
                _startWorkoutWhenReady = false;
                _beginWorkoutButton.Enabled = true;
                _beginWorkoutButton.Alpha = 1f;
                StartSelectedWorkout();
            }
        }
        _ = RefreshOuraRecoveryAsync();
    }

    private void CancelQueuedWorkoutStart()
    {
        _startWorkoutWhenReady = false;
        _beginWorkoutButton.Enabled = true;
        _beginWorkoutButton.Alpha = 1f;
    }

    private void QueueWorkoutPreparation()
    {
        if (!_applicationStartupCompleted ||
            _activityDestroyed ||
            _appScreen != AppScreen.Duration ||
            (_editingActiveWorkoutSetup &&
                string.IsNullOrWhiteSpace(_workoutSetupCurrentGroupId)))
        {
            return;
        }

        int minutes = _selectedWorkoutMinutes;
        WorkoutModifiers modifiers = WorkoutModifierPolicy.Normalize(
            _selectedWorkoutModifiers);
        bool isReconfiguration = _editingActiveWorkoutSetup;
        string? currentWorkoutGroupId = _workoutSetupCurrentGroupId;
        if (_preparedWorkout is { } prepared &&
            prepared.Minutes == minutes &&
            prepared.Modifiers == modifiers &&
            prepared.IsReconfiguration == isReconfiguration &&
            prepared.CurrentWorkoutGroupId == currentWorkoutGroupId)
        {
            return;
        }
        if (_workoutPreparationTask is { IsCompleted: false } &&
            _preparingWorkoutMinutes == minutes &&
            _preparingWorkoutModifiers == modifiers &&
            _preparingWorkoutIsReconfiguration == isReconfiguration &&
            _preparingCurrentWorkoutGroupId == currentWorkoutGroupId)
        {
            return;
        }

        _workoutPreparationCancellation?.Cancel();
        _workoutPreparationCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _workoutPreparationCancellation = cancellation;
        _preparedWorkout = null;
        _preparingWorkoutMinutes = minutes;
        _preparingWorkoutModifiers = modifiers;
        _preparingWorkoutIsReconfiguration = isReconfiguration;
        _preparingCurrentWorkoutGroupId = currentWorkoutGroupId;
        string stateJson = JsonSerializer.Serialize(
            _state,
            WorkoutJsonContext.Default.WorkoutState);
        OuraRecoverySnapshot? recoveryContext = _state.OuraRecovery;
        IReadOnlyList<Exercise> exercises = _exerciseDatabase.Exercises;
        _workoutPreparationTask = Task.Run(() =>
        {
            cancellation.Token.ThrowIfCancellationRequested();
            WorkoutState preparedState = JsonSerializer.Deserialize(
                    stateJson,
                    WorkoutJsonContext.Default.WorkoutState)
                ?? throw new InvalidOperationException(
                    "Unable to clone the workout state for preparation.");
            preparedState.OuraRecovery = recoveryContext;
            var preparationService = new ExerciseSessionService(exercises);
            if (isReconfiguration)
            {
                preparationService.ReconfigureActiveWorkout(
                    preparedState,
                    modifiers,
                    currentWorkoutGroupId!);
                preparationService.ResizeActiveWorkout(preparedState, minutes);
            }
            else
            {
                preparationService.PrepareWorkout(
                    preparedState,
                    minutes,
                    modifiers);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            return new PreparedWorkout(
                preparedState,
                minutes,
                modifiers,
                isReconfiguration,
                currentWorkoutGroupId);
        }, cancellation.Token);
        _ = ObserveWorkoutPreparationAsync(
            _workoutPreparationTask,
            cancellation);
    }

    private async Task ObserveWorkoutPreparationAsync(
        Task<PreparedWorkout> preparationTask,
        CancellationTokenSource cancellation)
    {
        try
        {
            PreparedWorkout prepared = await preparationTask.ConfigureAwait(false);
            if (_activityDestroyed || cancellation.IsCancellationRequested)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                if (!_activityDestroyed &&
                    ReferenceEquals(
                        _workoutPreparationCancellation,
                        cancellation) &&
                    !cancellation.IsCancellationRequested &&
                    _appScreen == AppScreen.Duration &&
                    prepared.IsReconfiguration ==
                        _editingActiveWorkoutSetup &&
                    prepared.CurrentWorkoutGroupId ==
                        _workoutSetupCurrentGroupId &&
                    (_editingActiveWorkoutSetup
                        ? _state.ActiveWorkoutMinutes != 0
                        : _state.ActiveWorkoutMinutes == 0) &&
                    _selectedWorkoutMinutes == prepared.Minutes &&
                    WorkoutModifierPolicy.Normalize(
                        _selectedWorkoutModifiers) == prepared.Modifiers)
                {
                    _preparedWorkout = prepared;
                }
            });
        }
        catch (OperationCanceledException)
        {
            // A newer duration or modifier selection superseded this plan.
        }
        catch (Exception error)
        {
            Android.Util.Log.Error(
                "Flux",
                $"Workout preparation failed: {error}");
        }
    }

    private void ShowApplicationStartupFailure(Exception error)
    {
        if (_activityDestroyed)
        {
            return;
        }

        Android.Util.Log.Error("Flux", error.ToString());
        _startWorkoutWhenReady = false;
        _beginWorkoutButton.Enabled = false;
        _beginWorkoutButton.Alpha = 0.6f;
        _durationModifierFeedback.Text = "Nomadic Method is unavailable.";
        _durationModifierFeedback.Alpha = 1f;
        _durationModifierFeedback.Visibility = ViewStates.Visible;
    }

    protected override void OnResume()
    {
        base.OnResume();
        _activityResumed = true;
        if (_state is not null) _ = RefreshOuraRecoveryAsync();
        ApplySystemBarAppearance();
        if (_appScreen == AppScreen.Duration && _applicationStartupCompleted)
        {
            UpdateLightModifierPresentation(
                (_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);
        }
        if (_editingActiveWorkoutSetup)
        {
            return;
        }
        if (_restActive)
        {
            ResumeRestCountdown();
        }
        else if (_countdownPaused &&
                 !_countdownPausedByUser &&
                 (!_countdownPausedForMediaError || _mediaReady))
        {
            _countdownPausedForMediaError = false;
            ResumeCountdown();
        }
        else if (_mediaExercise is not null && ShouldExerciseVideoBePlaying())
        {
            _exerciseVideo?.Start();
        }
    }

    protected override void OnPause()
    {
        _activityResumed = false;
        PauseActiveWorkoutForBackground();
        PauseRestCountdown();
        _exerciseVideo?.Pause();
        CancelUiAnimations();
        base.OnPause();
    }

#pragma warning disable CA1422 // The compatibility override remains required across the app's API range.
    public override void OnBackPressed()
    {
        if (_editingActiveWorkoutSetup)
        {
            _workoutPreparationCancellation?.Cancel();
            _workoutPreparationCancellation?.Dispose();
            _workoutPreparationCancellation = null;
            _workoutPreparationTask = null;
            _preparedWorkout = null;
            _selectedWorkoutModifiers = _state.ActiveWorkoutModifiers;
            RestoreWorkoutAfterSetup();
            return;
        }

        base.OnBackPressed();
    }
#pragma warning restore CA1422

    public override void OnConfigurationChanged(
        Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ConfigureResponsiveText();
        ApplyResponsiveDimensions();
        GetInsetContent(_appScreen).RequestApplyInsets();
        _exerciseMediaArea.Post(ResizeMediaCard);
    }

    protected override void OnDestroy()
    {
        _activityDestroyed = true;
        _ouraReadCancellation?.Cancel();
        _workoutPreparationCancellation?.Cancel();
        _workoutPreparationCancellation?.Dispose();
        _workoutPreparationCancellation = null;
        _mediaReady = false;
        _mediaLoadGeneration++;
        CancelCountdown(resetToStart: false);
        PauseRestCountdown();
        _whistleSoundPool?.Release();
        _whistleSoundPool?.Dispose();
        _whistleSoundPool = null;
        _exerciseVideo?.StopPlayback();
        ClearHoldFrame();
        _activeMediaPlayer = null;
        _videoPreparedListener = null;
        _videoErrorListener = null;
        _videoInfoListener = null;
        _durationSeekBar.SetAccessibilityDelegate(null);
        _durationSeekAccessibilityDelegate?.Dispose();
        _durationSeekAccessibilityDelegate = null;
        foreach (SystemBarsController controller in _systemBarsControllers)
        {
            controller.Dispose();
        }
        _systemBarsControllers = [];
        _exerciseDatabase?.Dispose();
        base.OnDestroy();
    }

    private void BindViews()
    {
        _durationScreen = FindRequiredView<View>(Resource.Id.duration_screen);
        _durationInsetContent = FindRequiredView<LinearLayout>(
            Resource.Id.duration_inset_content);
        _durationScroll = FindRequiredView<ScrollView>(Resource.Id.duration_scroll);
        _durationContent = FindRequiredView<LinearLayout>(Resource.Id.duration_content);
        _durationIdentity = FindRequiredView<LinearLayout>(Resource.Id.duration_identity);
        _durationAppIcon = FindRequiredView<ImageView>(Resource.Id.duration_app_icon);
        _durationDial = FindRequiredView<View>(Resource.Id.duration_dial);
        _durationControls = FindRequiredView<LinearLayout>(Resource.Id.duration_controls);
        _durationStepRow = FindRequiredView<LinearLayout>(Resource.Id.duration_step_row);
        _durationOptionLabels = FindRequiredView<FrameLayout>(
            Resource.Id.duration_option_labels);
        _durationModifierScroller = FindRequiredView<HorizontalScrollView>(
            Resource.Id.duration_modifier_scroller);
        _durationModifierGroups = FindRequiredView<LinearLayout>(
            Resource.Id.duration_modifier_groups);
        _durationContextModifierGroup = FindRequiredView<LinearLayout>(
            Resource.Id.duration_context_modifier_group);
        _durationIntensityModifierGroup = FindRequiredView<LinearLayout>(
            Resource.Id.duration_intensity_modifier_group);
        _durationEquipmentModifierGroup = FindRequiredView<LinearLayout>(
            Resource.Id.duration_equipment_modifier_group);
        _upperBodyClothingModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.upper_body_clothing_modifier_button);
        _hardFloorModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.hard_floor_modifier_button);
        _insectModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.insect_modifier_button);
        _silenceModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.silence_modifier_button);
        _shyModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.shy_modifier_button);
        _lightModifierContainer = FindRequiredView<FrameLayout>(
            Resource.Id.light_workout_modifier_container);
        _lightModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.light_workout_modifier_button);
        _lightModifierCountdownBadge = FindRequiredView<TextView>(
            Resource.Id.light_workout_countdown_badge);
        _wallModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.wall_modifier_button);
        _mirrorModifierButton = FindRequiredView<CheckBox>(
            Resource.Id.mirror_modifier_button);
        _durationModifierFeedback = FindRequiredView<TextView>(
            Resource.Id.duration_modifier_feedback);
        _durationActionBar = FindRequiredView<FrameLayout>(
            Resource.Id.duration_action_bar);
        _durationLockIcon = FindRequiredView<ImageView>(
            Resource.Id.duration_lock_icon);
        _durationActionIcon = FindRequiredView<ImageView>(
            Resource.Id.duration_action_icon);
        _durationMinutesValue = FindRequiredView<TextView>(
            Resource.Id.duration_minutes_value);
        _durationDecreaseButton = FindRequiredView<Button>(
            Resource.Id.duration_decrease_button);
        _durationSeekBar = FindRequiredView<SeekBar>(Resource.Id.duration_seek_bar);
        _durationIncreaseButton = FindRequiredView<Button>(
            Resource.Id.duration_increase_button);
        _durationOptionSegments = FindRequiredView<LinearLayout>(
            Resource.Id.duration_option_segments);
        _beginWorkoutButton = FindRequiredView<Button>(Resource.Id.begin_workout_button);
        _endSessionButton = FindRequiredView<ImageButton>(Resource.Id.end_session_button);
        _workoutScreen = FindRequiredView<View>(Resource.Id.workout_screen);
        _workoutPhaseSurface = FindRequiredView<View>(
            Resource.Id.workout_phase_surface);
        _workoutPhaseLeft = FindRequiredView<View>(Resource.Id.workout_phase_left);
        _workoutPhaseRight = FindRequiredView<View>(Resource.Id.workout_phase_right);
        _workoutInsetContent = FindRequiredView<LinearLayout>(
            Resource.Id.workout_inset_content);
        _workoutControlColumn = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            LayoutDirection = LayoutDirection.Ltr,
        };
        _workoutHeader = FindRequiredView<LinearLayout>(Resource.Id.workout_header);
        _workoutSetupButton = FindRequiredView<ImageButton>(
            Resource.Id.workout_setup_button);
        _workoutProgressText = FindRequiredView<TextView>(Resource.Id.workout_progress_text);
        _workoutProgressBar = FindRequiredView<ProgressBar>(Resource.Id.workout_progress_bar);
        _congratulationsScreen = FindRequiredView<View>(Resource.Id.congratulations_screen);
        _completionInsetContent = FindRequiredView<LinearLayout>(
            Resource.Id.completion_inset_content);
        _completionHero = FindRequiredView<FrameLayout>(Resource.Id.completion_hero);
        _completionHalo = FindRequiredView<View>(Resource.Id.completion_halo);
        _completionActionBar = FindRequiredView<FrameLayout>(
            Resource.Id.completion_action_bar);
        _exerciseName = FindRequiredView<TextView>(Resource.Id.exercise_name);
        _exerciseModeBadge = FindRequiredView<TextView>(Resource.Id.exercise_mode_badge);
        _executionSignifierHost = FindRequiredView<FrameLayout>(
            Resource.Id.execution_signifier);
        _executionTimeline = new WorkoutBlockTimelineView(this);
        _executionSignifierHost.AddView(
            _executionTimeline,
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.MatchParent,
                GravityFlags.Center));
        _exerciseMediaArea = FindRequiredView<FrameLayout>(
            Resource.Id.exercise_media_area);
        _exerciseMediaCard = FindRequiredView<View>(Resource.Id.exercise_media_card);
        _exerciseVideo = FindRequiredView<VideoView>(Resource.Id.exercise_video);
        _holdFrameImage = FindRequiredView<ImageView>(Resource.Id.hold_frame_image);
        _mediaScrim = FindRequiredView<View>(Resource.Id.media_scrim);
        _mediaLoadingIndicator = FindRequiredView<ProgressBar>(
            Resource.Id.media_loading_indicator);
        _mediaErrorPanel = FindRequiredView<View>(Resource.Id.media_error_panel);
        _mediaRetryButton = FindRequiredView<Button>(Resource.Id.media_retry_button);
        _workoutActionHost = FindRequiredView<FrameLayout>(
            Resource.Id.workout_action_host);
        _readyPanel = FindRequiredView<LinearLayout>(Resource.Id.ready_panel);
        _shuffleButton = FindRequiredView<ImageButton>(Resource.Id.shuffle_button);
        _startButton = FindRequiredView<ImageButton>(Resource.Id.start_button);
        _countdownPanel = FindRequiredView<LinearLayout>(Resource.Id.countdown_panel);
        _countdownText = FindRequiredView<TextView>(Resource.Id.countdown_text);
        _countdownProgress = FindRequiredView<ProgressBar>(Resource.Id.countdown_progress);
        _repeatAction = FindRequiredView<ImageButton>(Resource.Id.repeat_action);
        _playbackAction = FindRequiredView<ImageButton>(Resource.Id.playback_action);
        _nextAction = FindRequiredView<ImageButton>(Resource.Id.next_action);
        _restPanel = FindRequiredView<LinearLayout>(Resource.Id.rest_panel);
        _restPlaybackAction = FindRequiredView<ImageButton>(
            Resource.Id.rest_playback_action);
        _restCountdownText = FindRequiredView<TextView>(Resource.Id.rest_countdown_text);
        _restProgress = FindRequiredView<ProgressBar>(Resource.Id.rest_progress);
        _keepButton = FindRequiredView<ImageButton>(Resource.Id.keep_button);
        _completionMark = FindRequiredView<ImageView>(Resource.Id.completion_mark);
        _doneButton = FindRequiredView<Button>(Resource.Id.done_button);

        _exerciseMediaArea.LayoutChange += (_, _) => ResizeMediaCard();
        _completionHero.LayoutChange += (_, _) => ResizeCompletionHalo();
        _durationSeekBar.LayoutChange += (_, _) => AlignDurationOptionLabels();
        _durationOptionLabels.LayoutChange += (_, _) =>
            AlignDurationOptionLabels();
    }

    private void BindEvents()
    {
        _durationDecreaseButton.Click += (_, _) =>
            StepSelectedWorkoutMinutes(-1);
        _durationIncreaseButton.Click += (_, _) =>
            StepSelectedWorkoutMinutes(1);
        _durationSeekBar.ProgressChanged += (_, eventArgs) =>
        {
            if (eventArgs.FromUser)
            {
                SetSelectedWorkoutMinutes(
                    ExerciseSessionService.SupportedWorkoutMinutes[eventArgs.Progress],
                    userInitiated: true);
            }
        };
        _upperBodyClothingModifierButton.Click += (_, _) =>
        {
            bool enabled = _upperBodyClothingModifierButton.Checked;
            SetSelectedWorkoutModifier(
                WorkoutModifiers.UpperBodyClothing,
                enabled,
                _upperBodyClothingModifierButton,
                Resource.String.upper_body_clothing_modifier_description,
                Resource.String.upper_body_clothing_modifier_on,
                Resource.String.upper_body_clothing_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.UpperBodyClothing,
                enabled));
        };
        _hardFloorModifierButton.Click += (_, _) =>
        {
            bool enabled = _hardFloorModifierButton.Checked;
            SetSelectedWorkoutModifier(
                WorkoutModifiers.HardFloor,
                enabled,
                _hardFloorModifierButton,
                Resource.String.hard_floor_modifier_description,
                Resource.String.hard_floor_modifier_on,
                Resource.String.hard_floor_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.HardFloor,
                enabled));
        };
        _insectModifierButton.Click += (_, _) =>
        {
            bool enabled = _insectModifierButton.Checked;
            SetSelectedWorkoutModifier(
                WorkoutModifiers.Insect,
                enabled,
                _insectModifierButton,
                Resource.String.insect_modifier_description,
                Resource.String.insect_modifier_on,
                Resource.String.insect_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.Insect,
                enabled));
        };
        _silenceModifierButton.Click += (_, _) =>
        {
            bool enabled = _silenceModifierButton.Checked;
            SetSelectedWorkoutModifier(
                WorkoutModifiers.Silence,
                enabled,
                _silenceModifierButton,
                Resource.String.silence_modifier_description,
                Resource.String.silence_modifier_on,
                Resource.String.silence_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.Silence,
                enabled));
        };
        _shyModifierButton.Click += (_, _) =>
        {
            bool enabled = _shyModifierButton.Checked;
            SetSelectedWorkoutModifier(
                WorkoutModifiers.Shy,
                enabled,
                _shyModifierButton,
                Resource.String.shy_modifier_description,
                Resource.String.shy_modifier_on,
                Resource.String.shy_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.Shy,
                enabled));
        };
        _lightModifierButton.Click += (_, _) =>
        {
            bool enabled = _lightModifierButton.Checked;
            // CheckBox toggles before Click. Restore the effective state before
            // giving feedback, without changing modifiers or preparing a lineup.
            UpdateLightModifierPresentation(
                (_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);
            if (_lightModifierLocked)
            {
                AnimateModifierTile(_lightModifierContainer);
                ShowModifierFeedback(Resource.String.light_workout_locked_feedback, GetRestFeedbackReason());
                return;
            }
            SetSelectedWorkoutModifier(
                WorkoutModifiers.Light,
                enabled,
                _lightModifierButton,
                Resource.String.light_workout_modifier_description,
                Resource.String.light_workout_modifier_on,
                Resource.String.light_workout_modifier_off,
                userInitiated: true);
            ShowModifierFeedback(GetModifierFeedbackResourceId(
                WorkoutModifiers.Light,
                enabled));
        };
        _wallModifierButton.Click += (_, _) =>
        {
            WallEquipment nextEquipment = WorkoutModifierPolicy
                .GetWallEquipment(_selectedWorkoutModifiers) switch
            {
                WallEquipment.None => WallEquipment.SolesMayTouch,
                WallEquipment.SolesMayTouch => WallEquipment.SolesStayOff,
                WallEquipment.SolesStayOff => WallEquipment.None,
                _ => throw new InvalidOperationException(
                    "Unknown wall equipment state."),
            };
            SetSelectedWallEquipment(nextEquipment, userInitiated: true);
            ShowModifierFeedback(GetWallFeedbackResourceId(nextEquipment));
        };
        _mirrorModifierButton.Click += (_, _) =>
        {
            MirrorEquipment nextEquipment = WorkoutModifierPolicy
                .GetMirrorEquipment(_selectedWorkoutModifiers) switch
            {
                MirrorEquipment.None => MirrorEquipment.Tall,
                MirrorEquipment.Tall => MirrorEquipment.Compact,
                MirrorEquipment.Compact => MirrorEquipment.None,
                _ => throw new InvalidOperationException(
                    "Unknown mirror equipment state."),
            };
            SetSelectedMirrorEquipment(nextEquipment, userInitiated: true);
            ShowModifierFeedback(
                GetMirrorFeedbackResourceId(nextEquipment));
        };
        _beginWorkoutButton.Click += (_, _) => StartSelectedWorkout();
        _endSessionButton.Click += (_, _) => ConfirmEndSession();
        _workoutSetupButton.Click += (_, _) => ShowActiveWorkoutSetup();
        _exerciseName.LongClick += (_, eventArgs) =>
        {
            CopyDisplayedExerciseName();
            eventArgs.Handled = true;
        };
        _shuffleButton.Click += (_, _) => ShuffleCurrentExercise();
        _startButton.Click += (_, _) => StartCountdown();
        _repeatAction.Click += (_, _) => RepeatExercise();
        _playbackAction.Click += (_, _) => TogglePlayback();
        _nextAction.Click += (_, _) => GoToNextExercise();
        _restPlaybackAction.Click += (_, _) => ToggleRestPlayback();
        _keepButton.Click += (_, _) => KeepCurrentExercise();
        _mediaRetryButton.Click += (_, _) =>
        {
            if (_mediaExercise is not null && _mediaWorkoutGroup is not null)
            {
                LoadExerciseMedia(
                    _mediaExercise,
                    _mediaWorkoutGroup,
                    _previewingUpcomingSequenceBlock,
                    forceCacheRefresh: true);
            }
        };
        _doneButton.Click += (_, _) => CloseCompletedWorkout();
    }

    private void ConfigureSystemBars()
    {
        _systemBarsControllers =
        [
            new SystemBarsController(this, _durationInsetContent),
            new SystemBarsController(this, _workoutInsetContent),
            new SystemBarsController(this, _completionInsetContent),
        ];
    }

    private void ConfigureResponsiveText()
    {
        bool landscape = IsLandscape();
        _exerciseName.SetMaxLines(landscape ? 3 : 4);
        _countdownText.SetMaxLines(1);
        _restCountdownText.SetMaxLines(1);
        _durationDecreaseButton.SetMaxLines(1);
        _durationIncreaseButton.SetMaxLines(1);
        _doneButton.SetMaxLines(2);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _durationMinutesValue.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 36 : 56,
                landscape ? 88 : 108,
                2,
                (int)Android.Util.ComplexUnitType.Sp);
            _exerciseName.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 11 : 16,
                landscape ? 19 : 23,
                1,
                (int)Android.Util.ComplexUnitType.Sp);
            _countdownText.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 28 : 32,
                landscape ? 56 : 60,
                2,
                (int)Android.Util.ComplexUnitType.Sp);
            _restCountdownText.SetAutoSizeTextTypeUniformWithConfiguration(
                landscape ? 24 : 28,
                40,
                2,
                (int)Android.Util.ComplexUnitType.Sp);
            _beginWorkoutButton.SetAutoSizeTextTypeUniformWithConfiguration(
                24,
                34,
                1,
                (int)Android.Util.ComplexUnitType.Sp);
            foreach (Button stepButton in new[]
                     {
                         _durationDecreaseButton,
                         _durationIncreaseButton,
                     })
            {
                stepButton.SetAutoSizeTextTypeUniformWithConfiguration(
                    18,
                    28,
                    1,
                    (int)Android.Util.ComplexUnitType.Sp);
            }
            _doneButton.SetAutoSizeTextTypeUniformWithConfiguration(
                14,
                20,
                1,
                (int)Android.Util.ComplexUnitType.Sp);

            return;
        }

        // Native TextView auto-sizing starts at API 26. Keep the same fitted
        // hierarchy on API 24-25 by compensating for large system font scales;
        // the values remain at least as large as the auto-size minimums above.
        float fontScale = Math.Max(
            1f,
            Resources?.Configuration?.FontScale ?? 1f);
        SetResponsiveTextSize(
            _durationMinutesValue,
            landscape ? 36f : 56f,
            landscape ? 88f : 108f,
            fontScale);
        SetResponsiveTextSize(
            _exerciseName,
            landscape ? 11f : 16f,
            landscape ? 19f : 23f,
            fontScale);
        SetResponsiveTextSize(
            _countdownText,
            landscape ? 28f : 32f,
            landscape ? 56f : 60f,
            fontScale);
        SetResponsiveTextSize(
            _restCountdownText,
            landscape ? 24f : 28f,
            40f,
            fontScale);
        SetResponsiveTextSize(_beginWorkoutButton, 24f, 34f, fontScale);
        SetResponsiveTextSize(_durationDecreaseButton, 18f, 28f, fontScale);
        SetResponsiveTextSize(_durationIncreaseButton, 18f, 28f, fontScale);
        SetResponsiveTextSize(_doneButton, 14f, 20f, fontScale);
    }

    private void ApplyResponsiveDimensions()
    {
        bool landscape = IsLandscape();
        _exerciseMediaArea.SetMinimumHeight(
            Resources!.GetDimensionPixelSize(Resource.Dimension.workout_media_min_height));
        _readyPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.ready_panel_min_height));
        _countdownPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.move_panel_min_height));
        _restPanel.SetMinimumHeight(
            Resources.GetDimensionPixelSize(Resource.Dimension.rest_panel_min_height));

        ApplyDurationLayout(landscape);
        ApplyWorkoutLayout(landscape);
        ApplyCompletionLayout(landscape);
        _exerciseMediaArea.Post(ResizeMediaCard);
        _completionHero.Post(ResizeCompletionHalo);
    }

    private static void SetResponsiveTextSize(
        TextView view,
        float minimumSp,
        float preferredSp,
        float fontScale)
    {
        view.SetTextSize(
            Android.Util.ComplexUnitType.Sp,
            Math.Max(minimumSp, preferredSp / fontScale));
    }

    private bool IsLandscape()
    {
        return Resources?.Configuration?.Orientation ==
            Android.Content.Res.Orientation.Landscape;
    }

    private void ApplyDurationLayout(bool landscape)
    {
        int matchParent = ViewGroup.LayoutParams.MatchParent;
        int wrapContent = ViewGroup.LayoutParams.WrapContent;
        var resources = Resources!;
        int screenWidthDp = resources.Configuration!.ScreenWidthDp;
        bool compactLandscape = landscape &&
            screenWidthDp < 640;
        bool stackLandscapeModifierGroups = landscape &&
            screenWidthDp < 760;
        int iconSize = compactLandscape
            ? DpInt(32)
            : resources.GetDimensionPixelSize(Resource.Dimension.duration_icon_size);
        int dialSize = compactLandscape
            ? DpInt(140)
            : resources.GetDimensionPixelSize(Resource.Dimension.duration_dial_size);

        _durationInsetContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;
        _durationContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;
        _durationContent.SetGravity(landscape
            ? GravityFlags.Center
            : GravityFlags.CenterHorizontal);
        _durationIdentity.Orientation = Orientation.Vertical;
        _durationIdentity.SetGravity(GravityFlags.Center);

        _durationAppIcon.LayoutParameters = new LinearLayout.LayoutParams(
            iconSize,
            iconSize);
        var dialLayout = new LinearLayout.LayoutParams(dialSize, dialSize);
        dialLayout.TopMargin = DpInt(
            compactLandscape ? 4 : landscape ? 8 : 22);
        _durationDial.LayoutParameters = dialLayout;

        if (landscape)
        {
            _durationModifierGroups.Orientation = stackLandscapeModifierGroups
                ? Orientation.Vertical
                : Orientation.Horizontal;
            _durationModifierGroups.SetGravity(GravityFlags.Center);
            var landscapeIntensityLayout =
                (LinearLayout.LayoutParams)
                    _durationIntensityModifierGroup.LayoutParameters!;
            landscapeIntensityLayout.SetMargins(
                stackLandscapeModifierGroups ? 0 : DpInt(10),
                stackLandscapeModifierGroups ? DpInt(8) : 0,
                0,
                0);
            landscapeIntensityLayout.Gravity = GravityFlags.Center;
            _durationIntensityModifierGroup.LayoutParameters =
                landscapeIntensityLayout;
            var landscapeEquipmentLayout =
                (LinearLayout.LayoutParams)
                    _durationEquipmentModifierGroup.LayoutParameters!;
            landscapeEquipmentLayout.SetMargins(
                stackLandscapeModifierGroups ? 0 : DpInt(10),
                stackLandscapeModifierGroups ? DpInt(8) : 0,
                0,
                0);
            landscapeEquipmentLayout.Gravity = GravityFlags.Center;
            _durationEquipmentModifierGroup.LayoutParameters =
                landscapeEquipmentLayout;
            _durationScroll.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                matchParent,
                1f);
            _durationActionBar.LayoutParameters = new LinearLayout.LayoutParams(
                compactLandscape
                    ? DpInt(80)
                    : resources.GetDimensionPixelSize(
                        Resource.Dimension.duration_landscape_action_width),
                matchParent);
            _durationActionBar.SetBackgroundResource(
                Resource.Drawable.duration_action_rail_background);
            _durationActionBar.SetPadding(
                DpInt(compactLandscape ? 10 : 16),
                DpInt(compactLandscape ? 10 : 16),
                DpInt(compactLandscape ? 10 : 16),
                DpInt(compactLandscape ? 10 : 16));
            _durationContent.SetPadding(
                DpInt(compactLandscape ? 8 : 16),
                DpInt(compactLandscape ? 6 : 10),
                DpInt(compactLandscape ? 8 : 16),
                DpInt(compactLandscape ? 6 : 10));

            var identityLayout = new LinearLayout.LayoutParams(
                dialSize + DpInt(compactLandscape ? 20 : 28),
                wrapContent)
            {
                Gravity = GravityFlags.CenterVertical,
            };
            _durationIdentity.LayoutParameters = identityLayout;

            var controlsLayout = new LinearLayout.LayoutParams(
                0,
                wrapContent,
                1f)
            {
                Gravity = GravityFlags.CenterVertical,
            };
            controlsLayout.SetMargins(DpInt(compactLandscape ? 8 : 12), 0, 0, 0);
            _durationControls.LayoutParameters = controlsLayout;

            var stepRowLayout = new LinearLayout.LayoutParams(
                matchParent,
                DpInt(compactLandscape ? 48 : 56));
            _durationStepRow.LayoutParameters = stepRowLayout;
            _durationDecreaseButton.LayoutParameters =
                new LinearLayout.LayoutParams(
                    DpInt(compactLandscape ? 48 : 56),
                    DpInt(compactLandscape ? 48 : 56));
            _durationIncreaseButton.LayoutParameters =
                new LinearLayout.LayoutParams(
                    DpInt(compactLandscape ? 48 : 56),
                    DpInt(compactLandscape ? 48 : 56));
            _durationOptionLabels.SetPadding(0, 0, 0, 0);

            var modifierScrollerLayout = new LinearLayout.LayoutParams(
                matchParent,
                wrapContent)
            {
                Gravity = GravityFlags.CenterHorizontal,
            };
            modifierScrollerLayout.TopMargin = DpInt(
                compactLandscape ? 10 : 16);
            _durationModifierScroller.LayoutParameters =
                modifierScrollerLayout;
            _durationModifierGroups.LayoutParameters =
                new FrameLayout.LayoutParams(
                    wrapContent,
                    wrapContent);
            _durationModifierScroller.ScrollTo(0, 0);
            SetModifierTileSizes(DpInt(
                compactLandscape || stackLandscapeModifierGroups ? 48 : 56));

            var segmentLayout = new LinearLayout.LayoutParams(
                matchParent,
                DpInt(24));
            segmentLayout.TopMargin = DpInt(compactLandscape ? 12 : 20);
            _durationOptionSegments.LayoutParameters = segmentLayout;
            _beginWorkoutButton.LayoutParameters = new FrameLayout.LayoutParams(
                DpInt(compactLandscape ? 60 : 68),
                DpInt(compactLandscape ? 60 : 68),
                GravityFlags.Center);
            _durationOptionLabels.Post(AlignDurationOptionLabels);
            UpdateWorkoutSettingsActions();
            return;
        }

        _durationScroll.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            0,
            1f);
        _durationModifierGroups.Orientation = Orientation.Vertical;
        _durationModifierGroups.SetGravity(GravityFlags.Center);
        var portraitIntensityLayout =
            (LinearLayout.LayoutParams)
                _durationIntensityModifierGroup.LayoutParameters!;
        portraitIntensityLayout.SetMargins(0, DpInt(10), 0, 0);
        portraitIntensityLayout.Gravity = GravityFlags.Center;
        _durationIntensityModifierGroup.LayoutParameters =
            portraitIntensityLayout;
        var portraitEquipmentLayout =
            (LinearLayout.LayoutParams)
                _durationEquipmentModifierGroup.LayoutParameters!;
        portraitEquipmentLayout.SetMargins(0, DpInt(10), 0, 0);
        portraitEquipmentLayout.Gravity = GravityFlags.Center;
        _durationEquipmentModifierGroup.LayoutParameters =
            portraitEquipmentLayout;
        _durationActionBar.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);
        _durationActionBar.SetBackgroundResource(
            Resource.Drawable.duration_action_background);
        _durationActionBar.SetPadding(
            DpInt(24),
            DpInt(12),
            DpInt(24),
            DpInt(16));
        _durationContent.SetPadding(
            DpInt(24),
            DpInt(24),
            DpInt(24),
            DpInt(24));
        _durationIdentity.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);
        _durationControls.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);

        var portraitStepRowLayout = new LinearLayout.LayoutParams(
            matchParent,
            DpInt(64));
        portraitStepRowLayout.TopMargin = DpInt(28);
        _durationStepRow.LayoutParameters = portraitStepRowLayout;
        _durationDecreaseButton.LayoutParameters =
            new LinearLayout.LayoutParams(DpInt(64), DpInt(64));
        _durationIncreaseButton.LayoutParameters =
            new LinearLayout.LayoutParams(DpInt(64), DpInt(64));
        _durationOptionLabels.SetPadding(0, 0, 0, 0);

        var portraitModifierScrollerLayout = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent)
        {
            Gravity = GravityFlags.CenterHorizontal,
        };
        portraitModifierScrollerLayout.TopMargin = DpInt(32);
        _durationModifierScroller.LayoutParameters =
            portraitModifierScrollerLayout;
        _durationModifierGroups.LayoutParameters =
            new FrameLayout.LayoutParams(
                wrapContent,
                wrapContent);
        _durationModifierScroller.ScrollTo(0, 0);
        SetModifierTileSizes(DpInt(48));

        var portraitSegmentLayout = new LinearLayout.LayoutParams(
            matchParent,
            DpInt(24));
        portraitSegmentLayout.TopMargin = DpInt(36);
        _durationOptionSegments.LayoutParameters = portraitSegmentLayout;
        _beginWorkoutButton.LayoutParameters = new FrameLayout.LayoutParams(
            matchParent,
            DpInt(68));
        _durationOptionLabels.Post(AlignDurationOptionLabels);
        UpdateWorkoutSettingsActions();
    }

    private void AlignDurationOptionLabels()
    {
        int optionCount = ExerciseSessionService.SupportedWorkoutMinutes.Count;
        if (_durationOptionLabels.Width <= 0 ||
            _durationSeekBar.Width <= 0 ||
            _durationOptionLabels.ChildCount != optionCount ||
            optionCount < 2)
        {
            return;
        }

        int[] seekLocation = new int[2];
        int[] labelsLocation = new int[2];
        _durationSeekBar.GetLocationInWindow(seekLocation);
        _durationOptionLabels.GetLocationInWindow(labelsLocation);

        float trackStart =
            seekLocation[0] - labelsLocation[0] + _durationSeekBar.PaddingLeft;
        float trackEnd =
            seekLocation[0] - labelsLocation[0] +
            _durationSeekBar.Width - _durationSeekBar.PaddingRight;
        bool rightToLeft =
            _durationSeekBar.LayoutDirection == LayoutDirection.Rtl;
        int labelSlotWidth = Math.Max(
            1,
            (int)Math.Floor(
                (trackEnd - trackStart) / (optionCount - 1)));

        for (int index = 0; index < optionCount; index++)
        {
            View label = _durationOptionLabels.GetChildAt(index)
                ?? throw new InvalidOperationException(
                    "A duration option label is missing.");
            if (label.LayoutParameters is FrameLayout.LayoutParams layout &&
                layout.Width != labelSlotWidth)
            {
                layout.Width = labelSlotWidth;
                label.LayoutParameters = layout;
            }

            float fraction = index / (float)(optionCount - 1);
            if (rightToLeft)
            {
                fraction = 1f - fraction;
            }

            float center = trackStart + ((trackEnd - trackStart) * fraction);
            float targetLeft = center - (labelSlotWidth / 2f);
            label.TranslationX = targetLeft - label.Left;
        }
    }

    private void ApplyWorkoutLayout(bool landscape)
    {
        int matchParent = ViewGroup.LayoutParams.MatchParent;
        int wrapContent = ViewGroup.LayoutParams.WrapContent;
        int gap = Resources!.GetDimensionPixelSize(
            Resource.Dimension.landscape_content_gap);

        ArrangeWorkoutContent(landscape);
        _workoutInsetContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;
        _workoutHeader.SetGravity(landscape
            ? GravityFlags.CenterVertical
            : GravityFlags.Top);

        if (landscape)
        {
            _workoutControlColumn.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                matchParent,
                0.96f);

            var headerLayout = new LinearLayout.LayoutParams(
                matchParent,
                0,
                1f);
            headerLayout.BottomMargin = gap;
            _workoutHeader.LayoutParameters = headerLayout;

            _workoutActionHost.LayoutParameters = new LinearLayout.LayoutParams(
                matchParent,
                0,
                1f);

            var mediaLayout = new LinearLayout.LayoutParams(
                0,
                matchParent,
                1.28f);
            mediaLayout.MarginStart = gap;
            _exerciseMediaArea.LayoutParameters = mediaLayout;
        }
        else
        {
            _workoutHeader.LayoutParameters = new LinearLayout.LayoutParams(
                matchParent,
                wrapContent);

            var mediaLayout = new LinearLayout.LayoutParams(
                matchParent,
                0,
                1f);
            mediaLayout.SetMargins(0, DpInt(12), 0, DpInt(12));
            _exerciseMediaArea.LayoutParameters = mediaLayout;

            _workoutActionHost.LayoutParameters = new LinearLayout.LayoutParams(
                matchParent,
                Resources.GetDimensionPixelSize(
                    Resource.Dimension.workout_action_height));
        }

        foreach (LinearLayout panel in new[]
                 {
                     _readyPanel,
                     _countdownPanel,
                     _restPanel,
                 })
        {
            panel.LayoutParameters = new FrameLayout.LayoutParams(
                matchParent,
                matchParent,
                GravityFlags.Center);
        }
    }

    private void ArrangeWorkoutContent(bool landscape)
    {
        if (landscape)
        {
            MoveWorkoutView(_workoutHeader, _workoutControlColumn, 0);
            MoveWorkoutView(_workoutActionHost, _workoutControlColumn, 1);
            MoveWorkoutView(_workoutControlColumn, _workoutInsetContent, 0);
            MoveWorkoutView(_exerciseMediaArea, _workoutInsetContent, 1);
            return;
        }

        MoveWorkoutView(_workoutHeader, _workoutInsetContent, 0);
        MoveWorkoutView(_exerciseMediaArea, _workoutInsetContent, 1);
        MoveWorkoutView(_workoutActionHost, _workoutInsetContent, 2);
        if (_workoutControlColumn.Parent is ViewGroup parent)
        {
            parent.RemoveView(_workoutControlColumn);
        }
    }

    private static void MoveWorkoutView(
        View view,
        ViewGroup destination,
        int destinationIndex)
    {
        if (view.Parent is ViewGroup currentParent)
        {
            if (ReferenceEquals(currentParent, destination) &&
                currentParent.IndexOfChild(view) == destinationIndex)
            {
                return;
            }

            currentParent.RemoveView(view);
        }

        destination.AddView(
            view,
            Math.Min(destinationIndex, destination.ChildCount));
    }

    private void ApplyCompletionLayout(bool landscape)
    {
        int matchParent = ViewGroup.LayoutParams.MatchParent;
        int wrapContent = ViewGroup.LayoutParams.WrapContent;

        _completionInsetContent.Orientation = landscape
            ? Orientation.Horizontal
            : Orientation.Vertical;

        if (landscape)
        {
            _completionHero.LayoutParameters = new LinearLayout.LayoutParams(
                0,
                matchParent,
                1f);
            _completionActionBar.LayoutParameters = new LinearLayout.LayoutParams(
                Resources!.GetDimensionPixelSize(
                    Resource.Dimension.completion_landscape_action_width),
                matchParent);
            _completionActionBar.SetPadding(
                DpInt(16),
                DpInt(16),
                DpInt(16),
                DpInt(16));
            _doneButton.LayoutParameters = new FrameLayout.LayoutParams(
                matchParent,
                DpInt(68),
                GravityFlags.Center);
            return;
        }

        _completionHero.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            0,
            1f);
        _completionActionBar.LayoutParameters = new LinearLayout.LayoutParams(
            matchParent,
            wrapContent);
        _completionActionBar.SetPadding(
            DpInt(24),
            DpInt(12),
            DpInt(24),
            DpInt(16));
        _doneButton.LayoutParameters = new FrameLayout.LayoutParams(
            matchParent,
            DpInt(68));
    }

    private void ConfigureAccessibility()
    {
        _durationSeekBar.Max =
            ExerciseSessionService.SupportedWorkoutMinutes.Count - 1;
        _durationSeekAccessibilityDelegate = new DurationSeekAccessibilityDelegate(
            () => _selectedWorkoutMinutes,
            () => GetSupportedMinuteIndex(_selectedWorkoutMinutes),
            optionIndex => SetSelectedWorkoutMinutes(
                ExerciseSessionService.SupportedWorkoutMinutes[optionIndex],
                userInitiated: true));
        _durationSeekBar.SetAccessibilityDelegate(_durationSeekAccessibilityDelegate);
    }

    private void ShowAppScreen(AppScreen screen)
    {
        bool animate = _hasRenderedScreen && screen != _appScreen;
        _appScreen = screen;

        View target = screen switch
        {
            AppScreen.Duration => _durationScreen,
            AppScreen.Workout => _workoutScreen,
            AppScreen.Completion => _congratulationsScreen,
            _ => throw new ArgumentOutOfRangeException(nameof(screen)),
        };

        foreach (View candidate in new[]
                 {
                     _durationScreen,
                     _workoutScreen,
                     _congratulationsScreen,
                 })
        {
            candidate.Animate()?.Cancel();
            candidate.Alpha = 1f;
            candidate.TranslationY = 0f;
            candidate.Visibility = candidate == target
                ? ViewStates.Visible
                : ViewStates.Gone;
            candidate.ImportantForAccessibility = candidate == target
                ? ImportantForAccessibility.Auto
                : ImportantForAccessibility.NoHideDescendants;
        }

        if (screen == AppScreen.Workout)
        {
            Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        }
        else
        {
            Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
        }

        ApplySystemBarAppearance();
        GetInsetContent(screen).RequestApplyInsets();

        if (animate)
        {
            if (screen == AppScreen.Workout)
            {
                AnimateViewIn(_workoutHeader, 8f);
            }
            else
            {
                AnimateViewIn(target, 10f);
            }
        }

        _hasRenderedScreen = true;
    }

    private void ApplySystemBarAppearance()
    {
        if (_systemBarsControllers.Length == 0)
        {
            return;
        }

        _systemBarsControllers[0].SetAppearance(_appScreen != AppScreen.Completion);
    }

    private View GetInsetContent(AppScreen screen)
    {
        return screen switch
        {
            AppScreen.Duration => _durationInsetContent,
            AppScreen.Workout => _workoutInsetContent,
            AppScreen.Completion => _completionInsetContent,
            _ => throw new ArgumentOutOfRangeException(nameof(screen)),
        };
    }

    private void ShowWorkoutPhase(WorkoutPhase phase, bool animate = true)
    {
        _workoutPhase = phase;
        View target = phase switch
        {
            WorkoutPhase.Ready => _readyPanel,
            WorkoutPhase.Move => _countdownPanel,
            WorkoutPhase.Rest => _restPanel,
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };

        foreach (View candidate in new[] { _readyPanel, _countdownPanel, _restPanel })
        {
            candidate.Animate()?.Cancel();
            candidate.Alpha = 1f;
            candidate.TranslationY = 0f;
            candidate.Visibility = candidate == target
                ? ViewStates.Visible
                : ViewStates.Invisible;
            candidate.ImportantForAccessibility = candidate == target
                ? ImportantForAccessibility.Auto
                : ImportantForAccessibility.NoHideDescendants;
        }

        if (animate && _hasRenderedScreen)
        {
            AnimateViewIn(target, 4f);
        }
    }

    private void AnimateViewIn(View view, float translationDp)
    {
        view.Animate()?.Cancel();
        view.Alpha = 0f;
        view.TranslationY = Dp(translationDp);
        if (view.Animate() is { } animator)
        {
            animator
                .Alpha(1f)
                .TranslationY(0f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private void CancelUiAnimations()
    {
        foreach (View view in new[]
                 {
                     _durationScreen,
                     _durationDial,
                     _durationMinutesValue,
                     _workoutScreen,
                     _workoutPhaseSurface,
                     _congratulationsScreen,
                     _workoutHeader,
                     _exerciseMediaCard,
                     _readyPanel,
                     _countdownPanel,
                     _restPanel,
                     _completionHalo,
                     _completionMark,
                     _doneButton,
                 })
        {
            view.Animate()?.Cancel();
            view.TranslationY = 0f;
            view.ScaleX = 1f;
            view.ScaleY = 1f;
            if (view.Visibility == ViewStates.Visible)
            {
                view.Alpha = 1f;
            }
        }

        bool mediaIsResting =
            _appScreen == AppScreen.Workout &&
            (_workoutPhase == WorkoutPhase.Rest ||
             (_workoutPhase == WorkoutPhase.Move &&
              _lastMovementPhase == MovementPhase.Preparation));
        _exerciseMediaCard.Alpha = mediaIsResting ? 0.92f : 1f;
        _exerciseMediaCard.ScaleX = mediaIsResting ? 0.985f : 1f;
        _exerciseMediaCard.ScaleY = mediaIsResting ? 0.985f : 1f;

        int selectedDurationIndex = GetSupportedMinuteIndex(
            _selectedWorkoutMinutes);
        for (int index = 0; index < _durationOptionLabels.ChildCount; index++)
        {
            View? label = _durationOptionLabels.GetChildAt(index);
            label?.Animate()?.Cancel();
            if (label is null)
            {
                continue;
            }

            bool selected = index == selectedDurationIndex;
            label.Alpha = selected ? 1f : 0.72f;
            label.ScaleX = selected ? 1.08f : 1f;
            label.ScaleY = selected ? 1.08f : 1f;
        }

        _mediaScrim.Animate()?.Cancel();
        if (_mediaLoadingIndicator.Visibility == ViewStates.Visible ||
            _mediaErrorPanel.Visibility == ViewStates.Visible)
        {
            _mediaScrim.Alpha = 1f;
            _mediaScrim.Visibility = ViewStates.Visible;
        }
        else
        {
            _mediaScrim.Alpha = 0f;
            _mediaScrim.Visibility = ViewStates.Gone;
        }
    }

    private float Dp(float value)
    {
        return value * Resources!.DisplayMetrics!.Density;
    }

    private int DpInt(float value)
    {
        return (int)Math.Round(Dp(value));
    }

    private LinearLayout.LayoutParams CreateModifierTileLayout(int size)
    {
        var layout = new LinearLayout.LayoutParams(size, size)
        {
            Gravity = GravityFlags.Center,
        };
        int margin = DpInt(5);
        layout.SetMargins(margin, margin, margin, margin);
        return layout;
    }

    private void SetModifierTileSizes(int size)
    {
        int padding = GetModifierTilePadding(size);
        foreach (CheckBox tile in new[]
                 {
                     _upperBodyClothingModifierButton,
                     _hardFloorModifierButton,
                     _insectModifierButton,
                     _silenceModifierButton,
                     _shyModifierButton,
                     _wallModifierButton,
                     _mirrorModifierButton,
                 })
        {
            tile.LayoutParameters = CreateModifierTileLayout(size);
            tile.SetPadding(padding, padding, padding, padding);
        }
        _lightModifierContainer.LayoutParameters =
            CreateModifierTileLayout(size);
        _lightModifierButton.LayoutParameters =
            new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent,
                GravityFlags.Center);
        _lightModifierButton.SetPadding(padding, padding, padding, padding);

        UpdateMirrorModifierPresentation(
            WorkoutModifierPolicy.GetMirrorEquipment(
                _selectedWorkoutModifiers),
            size);
        UpdateWallModifierPresentation(
            WorkoutModifierPolicy.GetWallEquipment(
                _selectedWorkoutModifiers),
            size);
        UpdateHardFloorModifierPresentation(
            _selectedWorkoutModifiers.HasFlag(WorkoutModifiers.HardFloor),
            size);
    }

    private int GetModifierTilePadding(int size) =>
        size <= DpInt(48)
            ? DpInt(10)
            : size <= DpInt(56)
                ? DpInt(12)
                : DpInt(16);

    private void UpdateMirrorModifierPresentation(
        MirrorEquipment equipment,
        int? tileSize = null)
    {
        int drawableResourceId = equipment switch
        {
            MirrorEquipment.Compact => Resource.Drawable.ic_mirror_compact,
            MirrorEquipment.Tall => Resource.Drawable.ic_mirror_tall,
            _ => Resource.Drawable.ic_mirror,
        };
        _mirrorModifierButton.SetCompoundDrawablesWithIntrinsicBounds(
            0,
            drawableResourceId,
            0,
            0);
        _mirrorModifierButton.SetTextSize(
            Android.Util.ComplexUnitType.Sp,
            0f);
        int size = tileSize ?? _mirrorModifierButton.LayoutParameters?.Width
            ?? DpInt(64);
        int padding = GetModifierTilePadding(size);
        _mirrorModifierButton.SetPadding(
            padding,
            padding,
            padding,
            padding);
    }

    private void UpdateHardFloorModifierPresentation(
        bool hardFloorEnabled,
        int? tileSize = null)
    {
        _hardFloorModifierButton.SetCompoundDrawablesWithIntrinsicBounds(
            0,
            hardFloorEnabled
                ? Resource.Drawable.ic_hard_floor
                : Resource.Drawable.ic_soft_floor,
            0,
            0);
        _hardFloorModifierButton.SetTextSize(
            Android.Util.ComplexUnitType.Sp,
            0f);
        int size = tileSize ?? _hardFloorModifierButton.LayoutParameters?.Width
            ?? DpInt(64);
        int padding = GetModifierTilePadding(size);
        _hardFloorModifierButton.SetPadding(
            padding,
            padding,
            padding,
            padding);
    }

    private void UpdateWallModifierPresentation(
        WallEquipment equipment,
        int? tileSize = null)
    {
        int drawableResourceId = equipment switch
        {
            WallEquipment.None => Resource.Drawable.ic_wall_off,
            WallEquipment.SolesStayOff => Resource.Drawable.ic_wall_no_sole,
            WallEquipment.SolesMayTouch => Resource.Drawable.ic_wall,
            _ => Resource.Drawable.ic_wall_off,
        };
        _wallModifierButton.SetCompoundDrawablesWithIntrinsicBounds(
            0,
            drawableResourceId,
            0,
            0);
        _wallModifierButton.SetTextSize(
            Android.Util.ComplexUnitType.Sp,
            0f);
        int size = tileSize ?? _wallModifierButton.LayoutParameters?.Width
            ?? DpInt(64);
        int padding = GetModifierTilePadding(size);
        _wallModifierButton.SetPadding(
            padding,
            padding,
            padding,
            padding);
    }

    private void ResizeMediaCard()
    {
        int size = Math.Min(_exerciseMediaArea.Width, _exerciseMediaArea.Height);
        if (size <= 0 ||
            (_exerciseMediaCard.LayoutParameters?.Width == size &&
             _exerciseMediaCard.LayoutParameters.Height == size))
        {
            return;
        }

        _exerciseMediaCard.LayoutParameters = new FrameLayout.LayoutParams(
            size,
            size,
            GravityFlags.Center);
    }

    private void ResizeCompletionHalo()
    {
        int availableSize = Math.Min(
            _completionHero.Width,
            _completionHero.Height);
        int maximumSize = Resources!.GetDimensionPixelSize(
            Resource.Dimension.completion_halo_max_size);
        int size = Math.Min(maximumSize, availableSize - DpInt(20));
        if (size <= 0)
        {
            return;
        }

        int padding = Math.Max(DpInt(16), (int)Math.Round(size * 0.16d));
        _completionHalo.SetPadding(padding, padding, padding, padding);
        if (_completionHalo.LayoutParameters?.Width == size &&
            _completionHalo.LayoutParameters.Height == size)
        {
            return;
        }

        _completionHalo.LayoutParameters = new FrameLayout.LayoutParams(
            size,
            size,
            GravityFlags.Center);
    }

    private void ShowDurationSelection()
    {
        _manualLightModeRequested = false;
        _editingActiveWorkoutSetup = false;
        _workoutSetupCurrentGroupId = null;
        CancelCountdown(resetToStart: false);
        PauseRestCountdown();
        _restActive = false;
        _exerciseVideo.StopPlayback();
        ClearHoldFrame();
        ResetMovementVisuals();
        _currentExercise = null;
        _beginWorkoutButton.Enabled = true;
        _beginWorkoutButton.Alpha = 1f;

        ShowAppScreen(AppScreen.Duration);
        SetSelectedWorkoutMinutes(_state.LastWorkoutMinutes);
        WorkoutModifiers defaultModifiers = GetDefaultDurationModifiers();
        SetSelectedWorkoutModifier(
            WorkoutModifiers.UpperBodyClothing,
            (defaultModifiers &
                WorkoutModifiers.UpperBodyClothing) != 0,
            _upperBodyClothingModifierButton,
            Resource.String.upper_body_clothing_modifier_description,
            Resource.String.upper_body_clothing_modifier_on,
            Resource.String.upper_body_clothing_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.HardFloor,
            (defaultModifiers & WorkoutModifiers.HardFloor) != 0,
            _hardFloorModifierButton,
            Resource.String.hard_floor_modifier_description,
            Resource.String.hard_floor_modifier_on,
            Resource.String.hard_floor_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Insect,
            (defaultModifiers & WorkoutModifiers.Insect) != 0,
            _insectModifierButton,
            Resource.String.insect_modifier_description,
            Resource.String.insect_modifier_on,
            Resource.String.insect_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Silence,
            (defaultModifiers & WorkoutModifiers.Silence) != 0,
            _silenceModifierButton,
            Resource.String.silence_modifier_description,
            Resource.String.silence_modifier_on,
            Resource.String.silence_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Shy,
            (defaultModifiers & WorkoutModifiers.Shy) != 0,
            _shyModifierButton,
            Resource.String.shy_modifier_description,
            Resource.String.shy_modifier_on,
            Resource.String.shy_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Light,
            (defaultModifiers & WorkoutModifiers.Light) != 0,
            _lightModifierButton,
            Resource.String.light_workout_modifier_description,
            Resource.String.light_workout_modifier_on,
            Resource.String.light_workout_modifier_off);
        SetSelectedWallEquipment(
            WorkoutModifierPolicy.GetWallEquipment(
                defaultModifiers));
        SetSelectedMirrorEquipment(
            WorkoutModifierPolicy.GetMirrorEquipment(
                defaultModifiers));
        ConfigureDurationScreenForActiveWorkout(editing: false);
        QueueWorkoutPreparation();
        _ = RefreshOuraRecoveryAsync();
    }

    private void ShowActiveWorkoutSetup()
    {
        _manualLightModeRequested = _state.ActiveWorkoutModifiers.HasFlag(WorkoutModifiers.Light) &&
            _state.ActiveWorkoutSession?.AutomaticLightRequiredAtStart != true;
        if (_editingActiveWorkoutSetup ||
            _appScreen != AppScreen.Workout ||
            _currentWorkoutGroup is null ||
            _state.ActiveWorkoutMinutes == 0 ||
            _state.WorkoutCompleted)
        {
            return;
        }

        _workoutSetupReturnPhase = _workoutPhase;
        _workoutSetupShouldResume = false;
        _workoutSetupCurrentGroupId = _currentWorkoutGroup.Id;
        if (_workoutPhase == WorkoutPhase.Move)
        {
            _workoutSetupShouldResume = _countdownActive;
            if (_countdownActive)
            {
                _countdownPausedByUser = false;
                PauseCountdown();
            }
        }
        else if (_workoutPhase == WorkoutPhase.Rest && _restActive)
        {
            _workoutSetupShouldResume = !_state.PendingRestPausedByUser;
            if (_workoutSetupShouldResume)
            {
                long remaining = _sessionService
                    .GetPendingRestMillisecondsRemaining(
                        _state,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (remaining <= 0)
                {
                    CompleteRest();
                    return;
                }
                _sessionService.PauseRest(
                    _state,
                    _currentWorkoutGroup,
                    remaining);
                _stateStore.Save(_state);
            }
            PauseRestCountdown();
        }

        _exerciseVideo.Pause();
        _editingActiveWorkoutSetup = true;
        _selectedWorkoutMinutes = _state.ActiveWorkoutMinutes;
        _selectedWorkoutModifiers = _state.ActiveWorkoutModifiers;
        ShowAppScreen(AppScreen.Duration);
        SetSelectedWorkoutMinutes(_selectedWorkoutMinutes);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.UpperBodyClothing,
            (_selectedWorkoutModifiers &
                WorkoutModifiers.UpperBodyClothing) != 0,
            _upperBodyClothingModifierButton,
            Resource.String.upper_body_clothing_modifier_description,
            Resource.String.upper_body_clothing_modifier_on,
            Resource.String.upper_body_clothing_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.HardFloor,
            (_selectedWorkoutModifiers & WorkoutModifiers.HardFloor) != 0,
            _hardFloorModifierButton,
            Resource.String.hard_floor_modifier_description,
            Resource.String.hard_floor_modifier_on,
            Resource.String.hard_floor_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Insect,
            (_selectedWorkoutModifiers & WorkoutModifiers.Insect) != 0,
            _insectModifierButton,
            Resource.String.insect_modifier_description,
            Resource.String.insect_modifier_on,
            Resource.String.insect_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Silence,
            (_selectedWorkoutModifiers & WorkoutModifiers.Silence) != 0,
            _silenceModifierButton,
            Resource.String.silence_modifier_description,
            Resource.String.silence_modifier_on,
            Resource.String.silence_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Shy,
            (_selectedWorkoutModifiers & WorkoutModifiers.Shy) != 0,
            _shyModifierButton,
            Resource.String.shy_modifier_description,
            Resource.String.shy_modifier_on,
            Resource.String.shy_modifier_off);
        SetSelectedWorkoutModifier(
            WorkoutModifiers.Light,
            (_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0,
            _lightModifierButton,
            Resource.String.light_workout_modifier_description,
            Resource.String.light_workout_modifier_on,
            Resource.String.light_workout_modifier_off);
        SetSelectedWallEquipment(
            WorkoutModifierPolicy.GetWallEquipment(
                _selectedWorkoutModifiers));
        SetSelectedMirrorEquipment(
            WorkoutModifierPolicy.GetMirrorEquipment(
                _selectedWorkoutModifiers));
        ConfigureDurationScreenForActiveWorkout(editing: true);
        _beginWorkoutButton.Enabled = true;
        _beginWorkoutButton.Alpha = 1f;
        QueueWorkoutPreparation();
    }

    private WorkoutModifiers GetDefaultDurationModifiers()
    {
        long nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        WorkoutModifiers modifiers = _sessionService is not null
            ? _sessionService.GetDefaultWorkoutModifiers(
                _state,
                nowUnixMilliseconds)
            : WorkoutLightDayPolicy.GetDefaultWorkoutModifiers(
                _state.LastWorkoutModifiers,
                _state.WorkoutHistory,
                nowUnixMilliseconds,
                TimeZoneInfo.Local,
                _state.LegacyCompletedTrainingDayUnixMilliseconds);
        modifiers &= ~WorkoutModifiers.Light;
        return IsAutomaticLightModeLocked() ? modifiers | WorkoutModifiers.Light : modifiers;
    }

    private int GetWorkoutsUntilLightMode()
    {
        long nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _sessionService is not null
            ? _sessionService.GetWorkoutsUntilLightDay(
                _state,
                _selectedWorkoutMinutes,
                nowUnixMilliseconds)
            : WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
                _state.WorkoutHistory,
                _selectedWorkoutMinutes,
                nowUnixMilliseconds,
                TimeZoneInfo.Local,
                _state.LegacyCompletedTrainingDayUnixMilliseconds);
    }

    private bool IsAutomaticLightModeLocked()
    {
        long nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool lightDayDue = _sessionService is not null
            ? _sessionService.IsAutomaticLightDayDue(
                _state,
                nowUnixMilliseconds)
            : WorkoutLightDayPolicy.IsLightDayDue(
                _state.WorkoutHistory,
                nowUnixMilliseconds,
                TimeZoneInfo.Local,
                _state.LegacyCompletedTrainingDayUnixMilliseconds);
        return _sessionService is not null ? lightDayDue :
            OuraRecoveryPolicy.RequiresLight(lightDayDue,
                OuraRecoveryPolicy.Evaluate(_state, nowUnixMilliseconds));
    }

    private void UpdateLightModifierPresentation(bool enabled)
    {
        bool recoveryLightMode = _sessionService is not null &&
            _sessionService.GetRecoveryLightStatus(
                _state,
                _selectedWorkoutModifiers).IsActive;
        bool automaticLightMode = IsAutomaticLightModeLocked();
        if (automaticLightMode)
        {
            _selectedWorkoutModifiers |= WorkoutModifiers.Light;
            enabled = true;
        }
        else if (_automaticLightModePresented && !_editingActiveWorkoutSetup)
        {
            enabled = _manualLightModeRequested;
            if (!enabled) _selectedWorkoutModifiers &= ~WorkoutModifiers.Light;
        }
        _automaticLightModePresented = automaticLightMode;
        bool effectivelyEnabled = enabled || recoveryLightMode ||
            automaticLightMode;
        _lightModifierButton.Checked = effectivelyEnabled;
        _lightModifierLocked = recoveryLightMode || automaticLightMode;
        // Keep the tile tappable so a locked attempt can explain itself.
        _lightModifierButton.Enabled = true;
        int workoutsRemaining = GetWorkoutsUntilLightMode();
        _lightModifierCountdownBadge.Text = workoutsRemaining.ToString();
        bool ouraDecides = OuraRecoveryPolicy.Evaluate(_state,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Verdict != OuraRecoveryVerdict.Unknown;
        _lightModifierCountdownBadge.Visibility = effectivelyEnabled || ouraDecides
            ? ViewStates.Gone
            : ViewStates.Visible;

        string description = GetString(
            Resource.String.light_workout_modifier_description);
        _lightModifierButton.ContentDescription = automaticLightMode
            ? $"{description}: automatic light mode is required today"
            : recoveryLightMode
                ? $"{description}: effectively light while muscles recover"
            : enabled
                ? $"{description}: light mode on"
                : ouraDecides
                    ? $"{description}: light mode off"
                : $"{description}: approximately {workoutsRemaining} " +
                    $"workout{(workoutsRemaining == 1 ? string.Empty : "s")} " +
                    "at the selected duration until automatic light mode";
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _lightModifierButton.TooltipText = GetString(_lightModifierLocked
                ? Resource.String.light_workout_locked_feedback
                : effectivelyEnabled
                    ? Resource.String.light_workout_enabled_feedback
                    : Resource.String.light_workout_disabled_feedback);
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            _lightModifierButton.StateDescription = GetString(
                effectivelyEnabled
                    ? Resource.String.light_workout_modifier_on
                    : Resource.String.light_workout_modifier_off);
        }
    }

    private void ConfigureDurationScreenForActiveWorkout(bool editing)
    {
        _durationSeekBar.Enabled = true;
        _durationDecreaseButton.Enabled = _selectedWorkoutMinutes >
            (editing ? _sessionService.GetMinimumActiveWorkoutMinutes(_state) : 3);
        _durationIncreaseButton.Enabled =
            GetSupportedMinuteIndex(_selectedWorkoutMinutes) <
                ExerciseSessionService.SupportedWorkoutMinutes.Count - 1;
        _durationDecreaseButton.Alpha = _durationDecreaseButton.Enabled
            ? 1f
            : 0.34f;
        _durationIncreaseButton.Alpha = _durationIncreaseButton.Enabled
            ? 1f
            : 0.34f;
        _durationStepRow.Alpha = 1f;
        _durationOptionLabels.Alpha = 1f;
        _durationLockIcon.Visibility = ViewStates.Gone;
        UpdateWorkoutSettingsActions();
        _durationActionIcon.SetImageResource(editing
            ? Resource.Drawable.ic_phase_active
            : Resource.Drawable.ic_arrow_forward);
        _beginWorkoutButton.ContentDescription = editing
            ? GetString(Resource.String.resume_workout_description)
            : $"Continue with a {_selectedWorkoutMinutes} minute workout";
    }

    private void UpdateWorkoutSettingsActions()
    {
        if (_endSessionButton is null) return;
        _endSessionButton.Visibility = _editingActiveWorkoutSetup
            ? ViewStates.Visible : ViewStates.Gone;
        bool landscape = Resources?.Configuration?.Orientation ==
            Android.Content.Res.Orientation.Landscape;
        var restartLayout = new FrameLayout.LayoutParams(DpInt(52), DpInt(52),
            landscape ? GravityFlags.Top | GravityFlags.CenterHorizontal :
                GravityFlags.Start | GravityFlags.CenterVertical);
        _endSessionButton.LayoutParameters = restartLayout;
        if (_beginWorkoutButton.LayoutParameters is FrameLayout.LayoutParams beginLayout)
        {
            beginLayout.MarginStart = _editingActiveWorkoutSetup && !landscape ? DpInt(72) : 0;
            _beginWorkoutButton.LayoutParameters = beginLayout;
        }
        _durationActionIcon.TranslationX = _editingActiveWorkoutSetup && !landscape ? DpInt(36) : 0;
    }

    private void ConfirmEndSession()
    {
        if (!_editingActiveWorkoutSetup || !_beginWorkoutButton.Enabled) return;
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle(Resource.String.end_session_confirmation);
        dialog.SetMessage(Resource.String.end_session_explanation);
        dialog.SetNegativeButton(Resource.String.cancel_action, (_, _) => { });
        dialog.SetPositiveButton(Resource.String.end_session_action, (_, _) => EndActiveWorkout());
        dialog.Show();
    }

    private async void EndActiveWorkout()
    {
        if (!_editingActiveWorkoutSetup || !_beginWorkoutButton.Enabled) return;
        _beginWorkoutButton.Enabled = false;
        _endSessionButton.Enabled = false;
        _workoutPreparationCancellation?.Cancel();
        _preparedWorkout = null;
        string json = JsonSerializer.Serialize(_state, WorkoutJsonContext.Default.WorkoutState);
        OuraRecoverySnapshot? recovery = _state.OuraRecovery;
        IReadOnlyList<Exercise> exercises = _exerciseDatabase.Exercises;
        try
        {
            WorkoutState ended = await Task.Run(() =>
            {
                WorkoutState copy = JsonSerializer.Deserialize(json, WorkoutJsonContext.Default.WorkoutState)!;
                copy.OuraRecovery = recovery;
                new ExerciseSessionService(exercises).EndActiveWorkout(copy);
                return copy;
            });
            if (_activityDestroyed || !_editingActiveWorkoutSetup) return;
            _stateStore.Save(ended);
            _state = ended;
            _editingActiveWorkoutSetup = false;
            _workoutSetupCurrentGroupId = null;
            _workoutSetupShouldResume = false;
            StopCountdownTimer();
            _restActive = false;
            ConfigureDurationScreenForActiveWorkout(false);
            CancelQueuedWorkoutStart();
            ShowDurationSelection();
        }
        catch (Exception error)
        {
            Android.Util.Log.Error("Flux", $"Unable to end session: {error}");
            Toast.MakeText(this, Resource.String.workout_change_failed, ToastLength.Long)?.Show();
        }
        finally
        {
            if (!_activityDestroyed)
            {
                _beginWorkoutButton.Enabled = true;
                _endSessionButton.Enabled = true;
            }
        }
    }

    private void RestoreWorkoutAfterSetup()
    {
        WorkoutPhase returnPhase = _workoutSetupReturnPhase;
        bool shouldResume = _workoutSetupShouldResume;
        _editingActiveWorkoutSetup = false;
        ConfigureDurationScreenForActiveWorkout(editing: false);

        if (returnPhase == WorkoutPhase.Rest)
        {
            WorkoutGroup pendingGroup = _sessionService.GetPendingRestGroup(_state)
                ?? throw new InvalidOperationException(
                    "The paused rest could not be restored.");
            if (shouldResume && _state.PendingRestPausedByUser)
            {
                long remaining = _sessionService
                    .GetPendingRestMillisecondsRemaining(
                        _state,
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                _sessionService.ResumeRest(
                    _state,
                    pendingGroup,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + remaining);
                _stateStore.Save(_state);
            }
            RestorePendingRest(pendingGroup);
        }
        else if (returnPhase == WorkoutPhase.Move)
        {
            WorkoutGroup? pendingGroup =
                _sessionService.GetPendingMovementGroup(_state);
            if (pendingGroup is not null)
            {
                RestorePendingMovement(pendingGroup);
            }
            else
            {
                // The new modifier profile selected a different current
                // exercise. Show its replacement from a fresh Ready state
                // instead of restoring the obsolete countdown.
                StopCountdownTimer();
                ShowNextExercise();
            }
        }
        else
        {
            ShowNextExercise();
        }

        _workoutSetupCurrentGroupId = null;
        _workoutSetupShouldResume = false;
    }

    private void SetSelectedWorkoutModifier(
        WorkoutModifiers modifier,
        bool enabled,
        CheckBox button,
        int descriptionResourceId,
        int enabledStateResourceId,
        int disabledStateResourceId,
        bool userInitiated = false)
    {
        if (modifier == WorkoutModifiers.Light && userInitiated && !_lightModifierLocked)
            _manualLightModeRequested = enabled;
        if (modifier == WorkoutModifiers.Light && IsAutomaticLightModeLocked())
        {
            enabled = true;
        }
        _selectedWorkoutModifiers = enabled
            ? _selectedWorkoutModifiers | modifier
            : _selectedWorkoutModifiers & ~modifier;
        _selectedWorkoutModifiers =
            WorkoutModifierPolicy.Normalize(_selectedWorkoutModifiers);
        button.Checked = enabled;
        if (modifier == WorkoutModifiers.HardFloor)
        {
            UpdateHardFloorModifierPresentation(enabled);
        }
        button.ContentDescription = GetString(descriptionResourceId);
        if (modifier == WorkoutModifiers.Light)
        {
            UpdateLightModifierPresentation(enabled);
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            button.TooltipText = GetString(
                GetModifierFeedbackResourceId(modifier, enabled));
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            button.StateDescription = GetString(enabled
                ? enabledStateResourceId
                : disabledStateResourceId);
        }
        UpdateLightModifierPresentation(
            (_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);

        if (!userInitiated)
        {
            return;
        }

        _durationSelectionChangedDuringStartup |=
            !_applicationStartupCompleted;
        AnimateModifierTile(modifier == WorkoutModifiers.Light
            ? _lightModifierContainer
            : button);
        QueueWorkoutPreparation();
    }

    private void SetSelectedMirrorEquipment(
        MirrorEquipment equipment,
        bool userInitiated = false)
    {
        _selectedWorkoutModifiers = WorkoutModifierPolicy.WithMirrorEquipment(
            _selectedWorkoutModifiers,
            equipment);
        _mirrorModifierButton.Checked = equipment != MirrorEquipment.None;
        _mirrorModifierButton.Text = string.Empty;
        UpdateMirrorModifierPresentation(equipment);
        int stateResourceId = equipment switch
        {
            MirrorEquipment.Compact => Resource.String.mirror_modifier_compact,
            MirrorEquipment.Tall => Resource.String.mirror_modifier_tall,
            _ => Resource.String.mirror_modifier_off,
        };
        _mirrorModifierButton.ContentDescription =
            $"{GetString(Resource.String.mirror_modifier_description)}: " +
            GetString(stateResourceId);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _mirrorModifierButton.TooltipText = GetString(
                GetMirrorFeedbackResourceId(equipment));
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            _mirrorModifierButton.StateDescription = GetString(stateResourceId);
        }
        UpdateLightModifierPresentation(
            (_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);

        if (userInitiated)
        {
            _durationSelectionChangedDuringStartup |=
                !_applicationStartupCompleted;
            AnimateModifierTile(_mirrorModifierButton);
            QueueWorkoutPreparation();
        }
    }

    private void SetSelectedWallEquipment(
        WallEquipment equipment,
        bool userInitiated = false)
    {
        _selectedWorkoutModifiers = WorkoutModifierPolicy.WithWallEquipment(
            _selectedWorkoutModifiers,
            equipment);
        _wallModifierButton.Checked = equipment != WallEquipment.None;
        _wallModifierButton.Text = string.Empty;
        UpdateWallModifierPresentation(equipment);
        int stateResourceId = equipment switch
        {
            WallEquipment.SolesStayOff =>
                Resource.String.wall_modifier_soles_stay_off,
            WallEquipment.SolesMayTouch =>
                Resource.String.wall_modifier_soles_may_touch,
            _ => Resource.String.wall_modifier_off,
        };
        _wallModifierButton.ContentDescription =
            $"{GetString(Resource.String.wall_modifier_description)}: " +
            GetString(stateResourceId);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _wallModifierButton.TooltipText = GetString(
                GetWallFeedbackResourceId(equipment));
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            _wallModifierButton.StateDescription = GetString(stateResourceId);
        }
        UpdateLightModifierPresentation(
            (_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);

        if (userInitiated)
        {
            _durationSelectionChangedDuringStartup |=
                !_applicationStartupCompleted;
            AnimateModifierTile(_wallModifierButton);
            QueueWorkoutPreparation();
        }
    }

    private static int GetMirrorFeedbackResourceId(MirrorEquipment equipment) =>
        equipment switch
        {
            MirrorEquipment.None =>
                Resource.String.mirror_equipment_disabled_feedback,
            MirrorEquipment.Compact =>
                Resource.String.compact_mirror_equipment_enabled_feedback,
            MirrorEquipment.Tall =>
                Resource.String.tall_mirror_equipment_enabled_feedback,
            _ => throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null),
        };

    private static int GetWallFeedbackResourceId(WallEquipment equipment) =>
        equipment switch
        {
            WallEquipment.None =>
                Resource.String.wall_equipment_disabled_feedback,
            WallEquipment.SolesStayOff =>
                Resource.String.wall_equipment_enabled_feedback,
            WallEquipment.SolesMayTouch =>
                Resource.String.wall_sole_contact_enabled_feedback,
            _ => throw new ArgumentOutOfRangeException(
                nameof(equipment), equipment, null),
        };

    private static void AnimateModifierTile(View button)
    {
        button.PerformHapticFeedback(FeedbackConstants.ClockTick);
        button.Animate()?.Cancel();
        button.ScaleX = 0.92f;
        button.ScaleY = 0.92f;
        if (button.Animate() is { } animator)
        {
            animator
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private static int GetModifierFeedbackResourceId(
        WorkoutModifiers modifier,
        bool enabled) => modifier switch
    {
        WorkoutModifiers.UpperBodyClothing => enabled
            ? Resource.String.upper_body_clothing_enabled_feedback
            : Resource.String.upper_body_clothing_disabled_feedback,
        WorkoutModifiers.HardFloor => enabled
            ? Resource.String.hard_floor_enabled_feedback
            : Resource.String.hard_floor_disabled_feedback,
        WorkoutModifiers.Insect => enabled
            ? Resource.String.insect_mode_enabled_feedback
            : Resource.String.insect_mode_disabled_feedback,
        WorkoutModifiers.Silence => enabled
            ? Resource.String.noisy_exercises_disabled_feedback
            : Resource.String.noisy_exercises_enabled_feedback,
        WorkoutModifiers.Shy => enabled
            ? Resource.String.shy_mode_enabled_feedback
            : Resource.String.shy_mode_disabled_feedback,
        WorkoutModifiers.Light => enabled
            ? Resource.String.light_workout_enabled_feedback
            : Resource.String.light_workout_disabled_feedback,
        WorkoutModifiers.Wall => enabled
            ? Resource.String.wall_equipment_enabled_feedback
            : Resource.String.wall_equipment_disabled_feedback,
        _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, null),
    };

    private void ShowModifierFeedback(int messageResourceId, string? detail = null)
    {
        int generation = ++_modifierFeedbackGeneration;
        TextView feedback = _durationModifierFeedback;
        feedback.Animate()?.Cancel();
        string message = GetString(messageResourceId);
        feedback.SetMaxLines(detail is null ? 1 : 4);
        feedback.SetMaxWidth(Math.Max(1, _durationInsetContent.Width - DpInt(32)));
        if (detail is null) feedback.Text = message;
        else
        {
            var text = new Android.Text.SpannableString(message + "\n" + detail);
            text.SetSpan(new Android.Text.Style.RelativeSizeSpan(0.65f),
                message.Length + 1, text.Length(), Android.Text.SpanTypes.ExclusiveExclusive);
            feedback.SetText(text, TextView.BufferType.Spannable);
        }
        feedback.Visibility = ViewStates.Visible;
        feedback.Alpha = 0f;
        feedback.ScaleX = 0.82f;
        feedback.ScaleY = 0.82f;

        feedback.Animate()?
            .Alpha(1f)
            .ScaleX(1f)
            .ScaleY(1f)
            .SetDuration(ModifierFeedbackEnterDurationMilliseconds)
            .WithEndAction(new Java.Lang.Runnable(() =>
                _ = feedback.PostDelayed(
                    new Java.Lang.Runnable(() =>
                    {
                        if (generation != _modifierFeedbackGeneration)
                        {
                            return;
                        }

                        feedback.Animate()?
                            .Alpha(0f)
                            .ScaleX(1.08f)
                            .ScaleY(1.08f)
                            .SetDuration(ModifierFeedbackFadeDurationMilliseconds)
                            .WithEndAction(new Java.Lang.Runnable(() =>
                            {
                                if (generation == _modifierFeedbackGeneration)
                                {
                                    feedback.Visibility = ViewStates.Gone;
                                }
                            }))
                            .Start();
                    }),
                    detail is null ? ModifierFeedbackHoldMilliseconds : 2_400L)))
            .Start();
    }

    private void SetSelectedWorkoutMinutes(int minutes, bool userInitiated = false)
    {
        int previousOptionIndex = GetSupportedMinuteIndex(_selectedWorkoutMinutes);
        int normalizedMinutes = ExerciseSessionService.NormalizeLastWorkoutMinutes(minutes);
        if (_editingActiveWorkoutSetup)
            normalizedMinutes = Math.Max(normalizedMinutes, _sessionService.GetMinimumActiveWorkoutMinutes(_state));
        _selectedWorkoutMinutes = normalizedMinutes;
        int optionIndex = GetSupportedMinuteIndex(normalizedMinutes);

        if (_durationSeekBar.Progress != optionIndex)
        {
            _durationSeekBar.Progress = optionIndex;
        }

        const string minuteLabel = "minutes";

        _durationMinutesValue.Text = normalizedMinutes.ToString();
        _durationMinutesValue.ContentDescription =
            $"{normalizedMinutes} minutes selected";
        _beginWorkoutButton.ContentDescription =
            $"Continue with a {normalizedMinutes} {minuteLabel} workout";
        _durationSeekBar.ContentDescription =
            $"Workout duration, {normalizedMinutes} {minuteLabel}. " +
            "Options: 3, 5, 7, 10, 15, 20, 30, 45, 60, and 90 minutes";
        _durationOptionSegments.ContentDescription =
            $"{normalizedMinutes} minute workout selected";

        UpdateLightModifierPresentation(
            (_selectedWorkoutModifiers & WorkoutModifiers.Light) != 0);

        _durationDecreaseButton.Enabled = optionIndex > 0;
        _durationIncreaseButton.Enabled =
            optionIndex < ExerciseSessionService.SupportedWorkoutMinutes.Count - 1;
        _durationDecreaseButton.Alpha = _durationDecreaseButton.Enabled ? 1f : 0.42f;
        _durationIncreaseButton.Alpha = _durationIncreaseButton.Enabled ? 1f : 0.42f;

        if (userInitiated)
        {
            _durationSelectionChangedDuringStartup |=
                !_applicationStartupCompleted;
            _durationMinutesValue.PerformHapticFeedback(FeedbackConstants.ClockTick);
        }

        for (int index = 0; index < _durationOptionSegments.ChildCount; index++)
        {
            View segment = _durationOptionSegments.GetChildAt(index)
                ?? throw new InvalidOperationException("A duration route segment is missing.");
            segment.SetBackgroundResource(index <= optionIndex
                ? Resource.Drawable.duration_segment_active
                : Resource.Drawable.duration_segment_inactive);

            if (_durationOptionLabels.GetChildAt(index) is TextView label)
            {
                label.Animate()?.Cancel();
                bool selected = index == optionIndex;
                label.SetTextColor(new Android.Graphics.Color(GetColor(
                    selected
                        ? Resource.Color.primary_text
                        : Resource.Color.secondary_text)));
                label.Alpha = selected ? 1f : 0.72f;
                label.ScaleX = selected ? 1.08f : 1f;
                label.ScaleY = selected ? 1.08f : 1f;
            }
        }

        if (userInitiated && previousOptionIndex != optionIndex)
        {
            AnimateDurationSelectionChange(optionIndex);
            QueueWorkoutPreparation();
        }
        if (_editingActiveWorkoutSetup) ConfigureDurationScreenForActiveWorkout(true);
    }

    private void AnimateDurationSelectionChange(int optionIndex)
    {
        _durationDial.Animate()?.Cancel();
        _durationDial.ScaleX = 0.98f;
        _durationDial.ScaleY = 0.98f;
        if (_durationDial.Animate() is { } dialAnimator)
        {
            dialAnimator
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        _durationMinutesValue.Animate()?.Cancel();
        _durationMinutesValue.Alpha = 0.62f;
        _durationMinutesValue.ScaleX = 0.9f;
        _durationMinutesValue.ScaleY = 0.9f;
        if (_durationMinutesValue.Animate() is { } valueAnimator)
        {
            valueAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        if (_durationOptionLabels.GetChildAt(optionIndex) is not TextView label)
        {
            return;
        }

        label.Animate()?.Cancel();
        label.Alpha = 0.72f;
        label.ScaleX = 0.92f;
        label.ScaleY = 0.92f;
        if (label.Animate() is { } labelAnimator)
        {
            labelAnimator
                .Alpha(1f)
                .ScaleX(1.08f)
                .ScaleY(1.08f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private void StepSelectedWorkoutMinutes(int direction)
    {
        int currentIndex = GetSupportedMinuteIndex(_selectedWorkoutMinutes);
        int nextIndex = Math.Clamp(
            currentIndex + direction,
            0,
            ExerciseSessionService.SupportedWorkoutMinutes.Count - 1);
        SetSelectedWorkoutMinutes(
            ExerciseSessionService.SupportedWorkoutMinutes[nextIndex],
            userInitiated: true);
    }

    private static int GetSupportedMinuteIndex(int minutes)
    {
        for (int index = 0;
             index < ExerciseSessionService.SupportedWorkoutMinutes.Count;
             index++)
        {
            if (ExerciseSessionService.SupportedWorkoutMinutes[index] == minutes)
            {
                return index;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(minutes), minutes, null);
    }

    private async void StartSelectedWorkout()
    {
        if (!_applicationStartupCompleted)
        {
            _startWorkoutWhenReady = true;
            _beginWorkoutButton.Enabled = false;
            _beginWorkoutButton.Alpha = 0.6f;
            return;
        }

        if (!_beginWorkoutButton.Enabled)
        {
            return;
        }

        _beginWorkoutButton.Enabled = false;
        _beginWorkoutButton.Alpha = 0.6f;
        try
        {
            int minutes = _selectedWorkoutMinutes;
            WorkoutModifiers modifiers = WorkoutModifierPolicy.Normalize(
                _selectedWorkoutModifiers);
            QueueWorkoutPreparation();
            PreparedWorkout? prepared = _preparedWorkout is
                { Minutes: var preparedMinutes, Modifiers: var preparedModifiers }
                && preparedMinutes == minutes
                && preparedModifiers == modifiers
                && _preparedWorkout.IsReconfiguration ==
                    _editingActiveWorkoutSetup
                && _preparedWorkout.CurrentWorkoutGroupId ==
                    _workoutSetupCurrentGroupId
                    ? _preparedWorkout
                    : null;
            if (prepared is null &&
                _workoutPreparationTask is { } preparationTask &&
                _preparingWorkoutMinutes == minutes &&
                _preparingWorkoutModifiers == modifiers &&
                _preparingWorkoutIsReconfiguration ==
                    _editingActiveWorkoutSetup &&
                _preparingCurrentWorkoutGroupId ==
                    _workoutSetupCurrentGroupId)
            {
                prepared = await preparationTask;
            }
            if (prepared is null)
            {
                throw new InvalidOperationException(
                    "The selected workout could not be prepared.");
            }
            if (_activityDestroyed)
            {
                return;
            }

            _workoutPreparationCancellation?.Cancel();
            _workoutPreparationCancellation?.Dispose();
            _workoutPreparationCancellation = null;
            _workoutPreparationTask = null;
            _preparedWorkout = null;
            if (minutes != _selectedWorkoutMinutes || modifiers != _selectedWorkoutModifiers ||
                prepared.IsReconfiguration != _editingActiveWorkoutSetup ||
                prepared.CurrentWorkoutGroupId != _workoutSetupCurrentGroupId)
            {
                _beginWorkoutButton.Enabled = true;
                _beginWorkoutButton.Alpha = 1f;
                return;
            }
            WorkoutState nextState = prepared.State;
            nextState.OuraRecovery = GetAvailableOuraSnapshot();
            if (prepared.IsReconfiguration)
            {
                _stateStore.Save(nextState);
                _state = nextState;
                RestoreWorkoutAfterSetup();
                return;
            }

            _sessionService.ActivatePreparedWorkout(nextState);
            _stateStore.Save(nextState);
            _state = nextState;
            LogOuraDecision(workoutStarted: true);
            ShowNextExercise();
        }
        catch (OperationCanceledException)
        {
            _beginWorkoutButton.Enabled = true;
            _beginWorkoutButton.Alpha = 1f;
        }
        catch (Exception error)
        {
            _beginWorkoutButton.Enabled = true;
            _beginWorkoutButton.Alpha = 1f;
            Android.Util.Log.Error("Flux", $"Unable to start workout: {error}");
            Toast.MakeText(this, Resource.String.workout_change_failed, ToastLength.Long)?.Show();
        }
    }

    private static void RecoverPendingScoreUpdate(
        SqliteExerciseDatabase database,
        WorkoutState state)
    {
        if (state.PendingScoreExerciseId > 0)
        {
            state.PendingScoreUpdates.TryAdd(
                state.PendingScoreExerciseId,
                state.PendingScoreValue);
        }

        foreach ((int exerciseId, int score) in
                 state.PendingScoreUpdates.ToArray())
        {
            Exercise? exercise = database.Exercises.SingleOrDefault(
                candidate => candidate.Id == exerciseId);
            if (exercise is not null)
            {
                exercise.Score = score;
                database.UpdateScore(exercise);
            }

            state.PendingScoreUpdates.Remove(exerciseId);
        }

        state.PendingScoreExerciseId = 0;
        state.PendingScoreValue = 0;
        // OnCreate saves after legacy conversion/restoration. Saving
        // here would serialize away the compatibility-only legacy fields.
    }

    private void SaveStateAndScores(IReadOnlyList<Exercise> scoreUpdates) =>
        SaveStateAndScores(
            _exerciseDatabase,
            _stateStore,
            _state,
            scoreUpdates);

    private static void SaveStateAndScores(
        SqliteExerciseDatabase database,
        IWorkoutStateStore stateStore,
        WorkoutState state,
        IReadOnlyList<Exercise> scoreUpdates)
    {
        Exercise[] distinctUpdates = scoreUpdates
            .DistinctBy(exercise => exercise.Id)
            .ToArray();
        foreach (Exercise exercise in distinctUpdates)
        {
            state.PendingScoreUpdates[exercise.Id] = exercise.Score;
        }
        state.PendingScoreExerciseId = 0;
        state.PendingScoreValue = 0;

        stateStore.Save(state);

        if (distinctUpdates.Length == 0)
        {
            return;
        }

        foreach (Exercise exercise in distinctUpdates)
        {
            database.UpdateScore(exercise);
        }
        state.PendingScoreUpdates.Clear();
        stateStore.Save(state);
    }

    private void ConfigureVideoView()
    {
        _videoPreparedListener = new VideoPreparedListener(mediaPlayer =>
        {
            _mediaReady = true;
            _activeMediaPlayer = mediaPlayer;
            try
            {
                _activeMediaPlayer.Looping = _loopExerciseVideo;
                _activeMediaPlayer.SetVolume(0f, 0f);
            }
            catch (Java.Lang.IllegalStateException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
            catch (ObjectDisposedException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
            if (_countdownPausedForMediaError &&
                !_countdownPausedByUser &&
                _activityResumed)
            {
                _countdownPausedForMediaError = false;
                ResumeCountdown();
            }
            SetPlaybackControlsAvailability(
                _workoutPhase == WorkoutPhase.Move &&
                (_countdownActive || _countdownPausedByUser));
            ApplyCurrentMediaPlaybackState();

            SetStartAvailability(available: true);
            int preparedGeneration = _mediaLoadGeneration;
            _exerciseVideo.PostDelayed(
                () =>
                {
                    if (_mediaReady &&
                        preparedGeneration == _mediaLoadGeneration &&
                        _mediaErrorPanel.Visibility != ViewStates.Visible)
                    {
                        RevealExerciseMedia();
                    }
                },
                250L);
        });
        _exerciseVideo.SetOnPreparedListener(_videoPreparedListener);
        _videoErrorListener = new VideoErrorListener(() =>
        {
            ShowMediaError();
            return true;
        });
        _exerciseVideo.SetOnErrorListener(_videoErrorListener);
        _videoInfoListener = new VideoInfoListener(mediaInfo =>
        {
            if (mediaInfo == Android.Media.MediaInfo.VideoRenderingStart)
            {
                RevealExerciseMedia();
            }

            return false;
        });
        _exerciseVideo.SetOnInfoListener(_videoInfoListener);
        _exerciseVideo.Completion += (_, _) =>
        {
            if (_mediaWorkoutGroup?.SequenceMediaSegment !=
                ExerciseSequenceMediaSegment.Full)
            {
                if (_workoutPhase == WorkoutPhase.Ready ||
                    (_workoutPhase == WorkoutPhase.Rest &&
                        _previewingUpcomingSequenceBlock))
                {
                    _exerciseVideo.SeekTo(0);
                    ApplyCurrentMediaPlaybackState();
                    return;
                }

                if (_workoutPhase == WorkoutPhase.Move &&
                    _lastMovementPhase == MovementPhase.Continuous)
                {
                    RestartExerciseMediaForPhase(_lastMovementPhase.Value);
                    return;
                }
            }

            if (_freezeHoldAtEnd)
            {
                FreezeHoldOnFinalFrame();
            }
        };
    }

    private void LoadExerciseMedia(
        Exercise exercise,
        WorkoutGroup workoutGroup,
        bool previewingUpcomingSequenceBlock = false,
        bool forceCacheRefresh = false)
    {
        _mediaExercise = exercise;
        _mediaWorkoutGroup = workoutGroup;
        _previewingUpcomingSequenceBlock = previewingUpcomingSequenceBlock;
        bool usesStill = exercise.Presentation == ExercisePresentation.Still;
        bool holdDuringMove =
            exercise.Mode == ExerciseMode.Hold && _workoutPhase == WorkoutPhase.Move;
        bool holdDuringRest =
            exercise.Mode == ExerciseMode.Hold &&
            _workoutPhase == WorkoutPhase.Rest &&
            !previewingUpcomingSequenceBlock;
        _mediaLoadGeneration++;
        _mediaReady = false;
        _loopExerciseVideo =
            !holdDuringMove &&
            !holdDuringRest;
        _freezeHoldAtEnd = holdDuringMove || holdDuringRest;
        _activeMediaPlayer = null;

        if (usesStill)
        {
            ClearHoldFrame();
            _exerciseVideo.Pause();
            _exerciseVideo.StopPlayback();
            _mediaScrim.Animate()?.Cancel();
            _mediaScrim.Alpha = 1f;
            _mediaScrim.Visibility = ViewStates.Visible;
            _mediaErrorPanel.Visibility = ViewStates.Gone;
            _mediaLoadingIndicator.Visibility = ViewStates.Gone;
            ShowHoldFrame(exercise.Id);
            SetStartAvailability(available: _mediaReady);
            if (_workoutPhase == WorkoutPhase.Move && _mediaReady)
            {
                SetPlaybackControlsAvailability(
                    _countdownActive || _countdownPausedByUser);
            }
            if (_mediaReady &&
                _countdownPausedForMediaError &&
                !_countdownPausedByUser &&
                _activityResumed)
            {
                _countdownPausedForMediaError = false;
                ResumeCountdown();
                SetPlaybackControlsAvailability(available: true);
            }
            return;
        }

        if (holdDuringRest)
        {
            _exerciseVideo.Pause();
            _mediaErrorPanel.Visibility = ViewStates.Gone;
            _mediaLoadingIndicator.Visibility = ViewStates.Gone;
            ShowHoldFrame(exercise.Id);
            return;
        }

        ClearHoldFrame();
        _exerciseVideo.StopPlayback();
        _mediaScrim.Animate()?.Cancel();
        _mediaScrim.Alpha = 1f;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaLoadingIndicator.Visibility = ViewStates.Visible;
        _mediaErrorPanel.Visibility = ViewStates.Gone;
        SetStartAvailability(available: false);
        if (_workoutPhase == WorkoutPhase.Move)
        {
            SetPlaybackControlsAvailability(available: false);
        }

        try
        {
            _exerciseVideo.SetVideoPath(
                CacheVideoAsset(
                    GetExerciseVideoAssetPath(exercise),
                    forceCacheRefresh));
        }
        catch (Exception)
        {
            ShowMediaError();
        }
    }

    private string CacheVideoAsset(string assetPath, bool forceRefresh)
    {
        string cacheRoot = GetVersionedVideoCacheRoot();
        Directory.CreateDirectory(cacheRoot);
        string assetKind = assetPath.StartsWith(
            "exercise_direction_videos/",
            StringComparison.Ordinal)
            ? "directions-"
            : "standard-";
        long expectedLength;
        string assetFingerprint;
        using (Stream fingerprintSource = Assets!.Open(assetPath))
        {
            expectedLength = fingerprintSource.CanSeek ? fingerprintSource.Length : -1;
            assetFingerprint = Convert.ToHexString(
                SHA256.HashData(fingerprintSource)).ToLowerInvariant();
        }

        string assetFileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        string assetExtension = System.IO.Path.GetExtension(assetPath);
        string cachedPath = System.IO.Path.Combine(
            cacheRoot,
            $"{assetKind}{assetFileName}-{assetFingerprint}{assetExtension}");
        string temporaryPath = cachedPath + ".tmp";

        if (!forceRefresh &&
            File.Exists(cachedPath) &&
            new FileInfo(cachedPath).Length > 0 &&
            (expectedLength < 0 || new FileInfo(cachedPath).Length == expectedLength))
        {
            return cachedPath;
        }

        try
        {
            using Stream source = Assets!.Open(assetPath);
            using (FileStream destination = File.Create(temporaryPath))
            {
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
                if (expectedLength >= 0 && destination.Length != expectedLength)
                {
                    throw new IOException("The cached exercise video is incomplete.");
                }
            }

            File.Move(temporaryPath, cachedPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }

        return cachedPath;
    }

    private string GetVersionedVideoCacheRoot()
    {
        if (_exerciseVideoCacheRoot is not null)
        {
            return _exerciseVideoCacheRoot;
        }

        long versionCode = GetInstalledVersionCode();
        string cacheParent = System.IO.Path.Combine(
            CacheDir!.AbsolutePath,
            "exercise-videos");
        string versionRoot = System.IO.Path.Combine(
            cacheParent,
            versionCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(versionRoot);

        foreach (string staleRoot in Directory.EnumerateDirectories(cacheParent))
        {
            if (string.Equals(staleRoot, versionRoot, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                Directory.Delete(staleRoot, recursive: true);
            }
            catch (IOException)
            {
                // Android may still have an old cached video open briefly.
            }
            catch (UnauthorizedAccessException)
            {
                // Cache cleanup is best-effort and must not block playback.
            }
        }

        _exerciseVideoCacheRoot = versionRoot;
        return versionRoot;
    }

    private long GetInstalledVersionCode()
    {
        Android.Content.PM.PackageManager packageManager = PackageManager!;
        string packageName = PackageName!;

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            Android.Content.PM.PackageInfo packageInfo = packageManager.GetPackageInfo(
                packageName,
                Android.Content.PM.PackageManager.PackageInfoFlags.Of(0L))
                ?? throw new InvalidOperationException(
                    "Android could not resolve the installed Nomadic Method package.");
            return packageInfo.LongVersionCode;
        }

#pragma warning disable CS0618
        Android.Content.PM.PackageInfo legacyPackageInfo = packageManager.GetPackageInfo(
            packageName,
            Android.Content.PM.PackageInfoFlags.Activities)
            ?? throw new InvalidOperationException(
                "Android could not resolve the installed Nomadic Method package.");
#pragma warning restore CS0618
        return OperatingSystem.IsAndroidVersionAtLeast(28)
            ? legacyPackageInfo.LongVersionCode
#pragma warning disable CS0618
            : legacyPackageInfo.VersionCode;
#pragma warning restore CS0618
    }

    private string GetExerciseVideoAssetPath(Exercise exercise)
    {
        return exercise.GetVideoAssetPath(
            _mediaWorkoutGroup?.SequenceMediaSegment ??
                ExerciseSequenceMediaSegment.Full);
    }

    private void SetStartAvailability(bool available)
    {
        if (_workoutPhase != WorkoutPhase.Ready)
        {
            return;
        }

        _startButton.Enabled = available;
        _startButton.Alpha = available ? 1f : 0.5f;
        _startButton.SetImageResource(Resource.Drawable.ic_phase_active);
        _startButton.ContentDescription = available
            ? GetString(Resource.String.start)
            : GetString(Resource.String.media_loading);
    }

    private void SetPlaybackControlsAvailability(bool available)
    {
        SetPlaybackControlAvailability(_repeatAction, available);
        SetPlaybackControlAvailability(_playbackAction, available);

        // A missing or buffering demonstration must never trap the workout.
        // Repeat and play/pause require ready media; Next only requires an
        // active movement that can be rejected and advanced.
        bool nextAvailable = _workoutPhase == WorkoutPhase.Move &&
            (_countdownActive || _countdownPaused);
        SetPlaybackControlAvailability(_nextAction, nextAvailable);
    }

    private static void SetPlaybackControlAvailability(
        ImageButton control,
        bool available)
    {
        control.Enabled = available;
        control.Alpha = available
            ? PlaybackControlEnabledAlpha
            : PlaybackControlDisabledAlpha;
    }

    private void UpdatePlaybackActionVisual()
    {
        bool paused = _countdownPausedByUser;
        _playbackAction.SetImageResource(paused
            ? Resource.Drawable.ic_phase_active
            : Resource.Drawable.ic_phase_pause);
        _playbackAction.ContentDescription = GetString(paused
            ? Resource.String.resume_exercise_description
            : Resource.String.pause_exercise_description);
    }

    private void RevealExerciseMedia()
    {
        if (_mediaErrorPanel.Visibility == ViewStates.Visible ||
            _revealedMediaGeneration == _mediaLoadGeneration)
        {
            return;
        }

        _revealedMediaGeneration = _mediaLoadGeneration;
        int revealGeneration = _mediaLoadGeneration;
        _mediaErrorPanel.Visibility = ViewStates.Gone;
        _mediaLoadingIndicator.Visibility = ViewStates.Gone;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaScrim.Animate()?.Cancel();
        if (_mediaScrim.Animate() is { } animator)
        {
            animator
                .Alpha(0f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        _mediaScrim.PostDelayed(
            () =>
            {
                if (revealGeneration == _mediaLoadGeneration &&
                    _mediaLoadingIndicator.Visibility != ViewStates.Visible &&
                    _mediaErrorPanel.Visibility != ViewStates.Visible)
                {
                    _mediaScrim.Alpha = 0f;
                    _mediaScrim.Visibility = ViewStates.Gone;
                }
            },
            PhaseMotionDurationMilliseconds + 20L);
    }

    private void ShowMediaError()
    {
        _mediaReady = false;
        if (_workoutPhase == WorkoutPhase.Move && _countdownActive)
        {
            PauseCountdown();
            _countdownPausedForMediaError = true;
        }

        SetStartAvailability(available: false);
        if (_workoutPhase == WorkoutPhase.Ready)
        {
            _startButton.ContentDescription = GetString(Resource.String.media_error);
        }
        SetPlaybackControlsAvailability(available: false);
        _mediaScrim.Animate()?.Cancel();
        _mediaScrim.Alpha = 1f;
        _mediaScrim.Visibility = ViewStates.Visible;
        _mediaLoadingIndicator.Visibility = ViewStates.Gone;
        _mediaErrorPanel.Visibility = ViewStates.Visible;
        AnnouncePhaseForAccessibility(
            _mediaErrorPanel,
            GetString(Resource.String.media_error));
    }

    private void PlayHoldOnce()
    {
        if (_currentExercise?.Presentation == ExercisePresentation.Still)
        {
            ShowHoldFrame(_currentExercise.Id);
            return;
        }

        ClearHoldFrame();
        _loopExerciseVideo = false;
        _freezeHoldAtEnd = true;
        if (_activeMediaPlayer is not null)
        {
            try
            {
                _activeMediaPlayer.Looping = false;
            }
            catch (Java.Lang.IllegalStateException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
            catch (ObjectDisposedException)
            {
                RecoverInvalidMediaPlayerState();
                return;
            }
        }

        _exerciseVideo.SeekTo(0);
        _exerciseVideo.Start();
    }

    private void FreezeHoldOnFinalFrame()
    {
        Exercise? exercise = _currentExercise;
        if (exercise?.Mode != ExerciseMode.Hold)
        {
            return;
        }

        _exerciseVideo.Pause();
        ShowHoldFrame(exercise.Id);
    }

    private void ShowHoldFrame(int exerciseId)
    {
        if (_holdFrameImage.Visibility == ViewStates.Visible)
        {
            return;
        }

        try
        {
            string assetPath = $"exercise_hold_frames/exercise_{exerciseId:D4}.png";
            using Stream stream = Assets!.Open(assetPath);
            Android.Graphics.Bitmap bitmap = Android.Graphics.BitmapFactory.DecodeStream(stream)
                ?? throw new InvalidOperationException(
                    $"Unable to decode the reviewed hold frame for exercise {exerciseId}.");

            _mediaReady = true;
            _holdFrameBitmap = bitmap;
            _holdFrameImage.SetImageBitmap(bitmap);
            _holdFrameImage.Visibility = ViewStates.Visible;
            RevealExerciseMedia();
        }
        catch (Exception)
        {
            ShowMediaError();
        }
    }

    private void ClearHoldFrame()
    {
        if (_holdFrameImage is null)
        {
            return;
        }

        _holdFrameImage.Visibility = ViewStates.Gone;
        _holdFrameImage.SetImageDrawable(null);
        _holdFrameBitmap?.Dispose();
        _holdFrameBitmap = null;
    }

    private void ShowNextExercise()
    {
        bool continuingWorkout =
            _appScreen == AppScreen.Workout &&
            _workoutScreen.Visibility == ViewStates.Visible;
        WorkoutGroup? nextGroup = _sessionService.GetNextGroup(_state);

        if (nextGroup is null)
        {
            ShowCongratulations();
            return;
        }

        _currentWorkoutGroup = nextGroup;
        Exercise exercise = _sessionService.GetSelectedExercise(
            _state,
            _currentWorkoutGroup);
        _currentExercise = exercise;
        _lastMovementPhase = null;
        SetExerciseMediaMirrored(mirrored: false);
        IReadOnlyList<WorkoutGroup> activeGroups =
            _sessionService.GetActiveGroups(_state);
        WorkoutDisplayProgress displayProgress =
            WorkoutDisplayPolicy.GetProgress(
                activeGroups,
                _currentWorkoutGroup);
        int position = displayProgress.Position;
        int totalExercises = displayProgress.Total;
        int countdownDurationMilliseconds = GetCurrentCountdownDurationMilliseconds();

        _workoutProgressText.Text = $"{position:D2}  /  {totalExercises:D2}";
        _workoutProgressText.ContentDescription =
            $"Exercise {position} of {totalExercises}";
        _workoutProgressBar.Max = totalExercises;
        _workoutProgressBar.SetProgress(position, continuingWorkout);
        _countdownProgress.Max = countdownDurationMilliseconds;
        _countdownProgress.Progress = countdownDurationMilliseconds;
        RenderExerciseIdentity(exercise);
        RenderExecutionTimeline();
        ShowAppScreen(AppScreen.Workout);
        ShowStartButton();
        LoadExerciseMedia(exercise, _currentWorkoutGroup);
        ResizeMediaCard();
        if (continuingWorkout)
        {
            AnimateExerciseChange();
        }
        AnnouncePhaseForAccessibility(
            _workoutHeader,
            $"Exercise {position} of {totalExercises}. " +
            $"{exercise.Name}. " +
            (exercise.Mode == ExerciseMode.Hold ? "Static." : "Repetition."));
    }

    private void RenderExerciseIdentity(Exercise exercise, bool upcoming = false)
    {
        _exerciseName.Text = exercise.Name;
        string modeDescription = exercise.Mode == ExerciseMode.Hold
            ? "Static."
            : "Repetition.";
        string copyDescription = GetString(
            Resource.String.copy_exercise_name_description);
        _exerciseName.ContentDescription = upcoming
            ? $"Next block: {exercise.Name}. {modeDescription} {copyDescription}"
            : $"{exercise.Name}. {modeDescription} {copyDescription}";
        _exerciseModeBadge.Visibility = exercise.Mode == ExerciseMode.Hold
            ? ViewStates.Visible
            : ViewStates.Gone;
    }

    private void CopyDisplayedExerciseName()
    {
        string exerciseName = _exerciseName.Text?.Trim() ?? string.Empty;
        if (exerciseName.Length == 0)
        {
            return;
        }

        var clipboard = GetSystemService(
            Android.Content.Context.ClipboardService)
            as Android.Content.ClipboardManager;
        if (clipboard is null)
        {
            return;
        }

        clipboard.PrimaryClip = Android.Content.ClipData.NewPlainText(
            GetString(Resource.String.exercise_name_clip_label),
            exerciseName);
        _exerciseName.PerformHapticFeedback(FeedbackConstants.LongPress);

        // Android 13+ provides its own clipboard preview. Older releases need
        // an explicit confirmation so the gesture never feels unresponsive.
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            Android.Widget.Toast.MakeText(
                this,
                Resource.String.exercise_name_copied_feedback,
                Android.Widget.ToastLength.Short)?.Show();
        }
    }

    private void RestorePendingMovement(WorkoutGroup pendingGroup)
    {
        ShowNextExercise();
        if (_currentWorkoutGroup?.Id != pendingGroup.Id)
        {
            throw new InvalidOperationException(
                "The persisted movement is not the next workout round.");
        }

        long millisecondsRemaining =
            _sessionService.GetPendingMovementMillisecondsRemaining(
                _state,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (millisecondsRemaining <= 0)
        {
            throw new InvalidOperationException(
                "The persisted movement has no remaining time.");
        }

        _countdownActive = false;
        _countdownPaused = true;
        _countdownMillisecondsRemaining = millisecondsRemaining;
        _countdownEndsAtElapsedMilliseconds = 0;
        _countdownPausedByUser = _state.PendingMovementPausedByUser;
        _automaticSequenceStartPending =
            !_countdownPausedByUser &&
            _sessionService.IsSequenceContinuationBlock(_state, pendingGroup) &&
            millisecondsRemaining == GetCurrentMovementDurationMilliseconds();
        _countdownPausedForMediaError =
            !_countdownPausedByUser && !_mediaReady;
        _sessionService.PauseMovement(
            _state,
            pendingGroup,
            millisecondsRemaining,
            _countdownPausedByUser);
        _stateStore.Save(_state);
        _lastMovementPhase = null;
        ShowWorkoutPhase(WorkoutPhase.Move);
        UpdateMoveCountdown(millisecondsRemaining);
        SetPlaybackControlsAvailability(
            _mediaReady && _countdownPausedByUser);
        UpdatePlaybackActionVisual();
    }

    private void RestorePendingRest(WorkoutGroup pendingGroup)
    {
        ShowNextExercise();
        if (_currentWorkoutGroup?.Id != pendingGroup.Id)
        {
            throw new InvalidOperationException(
                "The persisted rest is not for the next workout round.");
        }

        _restActive = true;
        _exerciseVideo.Pause();
        ShowRestPanel();
        AnnouncePhaseForAccessibility(_restPanel, GetRestDescription());
        ResumeRestCountdown();
    }

    private void RenderExecutionTimeline(bool selectUpcomingBlock = false)
    {
        WorkoutExecutionTimeline timeline = WorkoutDisplayPolicy.GetTimeline(
            _sessionService.GetActiveGroups(_state),
            _currentWorkoutGroup,
            selectUpcomingBlock);
        _executionTimeline.SetTimeline(
            timeline.Blocks,
            timeline.SetStartBlockIndices,
            timeline.CurrentBlockIndex);
        int currentSetIndex = timeline.SetStartBlockIndices
            .TakeWhile(index => index <= timeline.CurrentBlockIndex)
            .Count() - 1;
        int currentSetStart = timeline.SetStartBlockIndices[currentSetIndex];
        int currentSetEnd = currentSetIndex + 1 <
                timeline.SetStartBlockIndices.Count
            ? timeline.SetStartBlockIndices[currentSetIndex + 1]
            : timeline.Blocks.Count;
        int blockPosition = timeline.CurrentBlockIndex - currentSetStart + 1;
        int blocksInSet = currentSetEnd - currentSetStart;
        string setDescription = timeline.SetStartBlockIndices.Count > 1
            ? $"Set {currentSetIndex + 1} of " +
                $"{timeline.SetStartBlockIndices.Count}. "
            : string.Empty;
        string description = setDescription +
            $"Work block {blockPosition} of {blocksInSet}. " +
            "Each colored segment is one 45-second work block. " +
            "The 15-second transitions are shown separately.";
        _executionTimeline.ContentDescription = description;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _executionTimeline.TooltipText = setDescription +
                $"Work block {blockPosition} of {blocksInSet}";
        }
        _executionSignifierHost.Visibility = ViewStates.Visible;
    }

    private void AnimateExerciseChange()
    {
        _workoutHeader.Animate()?.Cancel();
        _workoutHeader.Alpha = 0.2f;
        _workoutHeader.TranslationY = Dp(4f);
        if (_workoutHeader.Animate() is { } headerAnimator)
        {
            headerAnimator
                .Alpha(1f)
                .TranslationY(0f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }

        _exerciseMediaCard.Animate()?.Cancel();
        _exerciseMediaCard.Alpha = 0.72f;
        _exerciseMediaCard.ScaleX = 0.985f;
        _exerciseMediaCard.ScaleY = 0.985f;
        if (_exerciseMediaCard.Animate() is { } mediaAnimator)
        {
            mediaAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(PhaseMotionDurationMilliseconds)
                .Start();
        }
    }

    private void ShowStartButton()
    {
        _automaticSequenceStartPending = false;
        _countdownPausedByUser = false;
        _startButton.Enabled = true;
        _startButton.Alpha = 1f;
        _startButton.SetImageResource(Resource.Drawable.ic_phase_active);
        _startButton.ContentDescription = GetString(Resource.String.start);
        UpdateShuffleAvailability();
        SetPlaybackControlsAvailability(available: false);
        UpdatePlaybackActionVisual();
        ResetMovementVisuals();
        ShowWorkoutPhase(WorkoutPhase.Ready);
    }

    private void UpdateShuffleAvailability()
    {
        bool available = _currentWorkoutGroup is not null &&
            _sessionService.CanShuffleNextExercise(_state, _currentWorkoutGroup);
        _shuffleButton.Enabled = available;
        _shuffleButton.Visibility = available
            ? ViewStates.Visible
            : ViewStates.Gone;
        if (_startButton.LayoutParameters is LinearLayout.LayoutParams layoutParameters)
        {
            layoutParameters.MarginStart = available ? DpInt(12) : 0;
            _startButton.LayoutParameters = layoutParameters;
        }
    }

    private void ShuffleCurrentExercise()
    {
        if (_workoutPhase != WorkoutPhase.Ready ||
            _currentWorkoutGroup is null ||
            !_shuffleButton.Enabled)
        {
            return;
        }

        _shuffleButton.Enabled = false;
        ShuffledExerciseResult? result = _sessionService.ShuffleNextExercise(
            _state,
            _currentWorkoutGroup);
        if (result is null)
        {
            UpdateShuffleAvailability();
            return;
        }

        SaveStateAndScores(result.ScoreUpdates);
        ShowNextExercise();
        AnnouncePhaseForAccessibility(
            _workoutHeader,
            $"Rejected {result.RejectedExercise.Name}. " +
            $"Changed to {result.ReplacementExercise.Name}.");
    }

    private void StartCountdown()
    {
        if (_countdownActive || _countdownPaused || !_mediaReady)
        {
            return;
        }

        _countdownPausedByUser = false;
        _lastMovementPhase = null;
        ShowWorkoutPhase(WorkoutPhase.Move);
        StartCountdownTimer(GetCurrentCountdownDurationMilliseconds());
        SetPlaybackControlsAvailability(available: true);
        UpdatePlaybackActionVisual();
    }

    private void TogglePlayback()
    {
        if (_countdownPausedByUser)
        {
            if (!_mediaReady)
            {
                return;
            }

            _countdownPausedByUser = false;
            ResumeCountdown();
            SetPlaybackControlsAvailability(available: true);
            UpdatePlaybackActionVisual();
            AnnouncePhaseForAccessibility(_countdownPanel, "Exercise resumed.");
            return;
        }

        if (!_countdownActive)
        {
            return;
        }

        _countdownPausedByUser = true;
        PauseCountdown();
        _exerciseVideo.Pause();
        SetPlaybackControlsAvailability(available: true);
        UpdatePlaybackActionVisual();
        AnnouncePhaseForAccessibility(_countdownPanel, "Exercise paused.");
    }

    private void RepeatExercise()
    {
        if ((!_countdownActive && !_countdownPausedByUser) ||
            !_mediaReady ||
            _currentExercise is null)
        {
            return;
        }

        StopCountdownTimer();
        _exerciseVideo.Pause();
        SetExerciseMediaMirrored(mirrored: false);
        if (_currentExercise.Presentation != ExercisePresentation.Still)
        {
            ClearHoldFrame();
            _exerciseVideo.SeekTo(0);
        }
        _lastMovementPhase = null;
        StartCountdownTimer(GetCurrentCountdownDurationMilliseconds());
        SetPlaybackControlsAvailability(available: true);
        UpdatePlaybackActionVisual();
        AnnouncePhaseForAccessibility(
            _countdownPanel,
            "Exercise restarted from the beginning.");
    }

    private void GoToNextExercise()
    {
        if (!_countdownActive && !_countdownPaused)
        {
            return;
        }

        StopCountdownTimer();
        SetPlaybackControlsAvailability(available: false);
        RecordedWorkoutOutcome result =
            _sessionService.RejectCurrentSequenceWithScoreUpdates(
                _state,
                _currentWorkoutGroup);
        SaveStateAndScores(result.ScoreUpdates);
        if (_state.WorkoutCompleted)
        {
            PlayWhistleCue(_workoutCompleteWhistleId);
            ShowCongratulations();
        }
        else
        {
            ShowNextExercise();
        }
    }

    private void CompleteCountdown()
    {
        if (!_countdownActive)
        {
            return;
        }

        StopCountdownTimer();
        UpdateMoveCountdown(0L);
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            FreezeHoldOnFinalFrame();
        }
        PlayWhistleCue(_restStartWhistleId);

        BeginRest();
    }

    private void CancelCountdown(bool resetToStart)
    {
        if (!_countdownActive && !_countdownPaused)
        {
            return;
        }

        StopCountdownTimer();

        if (resetToStart && !_state.WorkoutCompleted)
        {
            if (_currentExercise?.Mode == ExerciseMode.Hold)
            {
                LoadExerciseMedia(_currentExercise, _currentWorkoutGroup);
            }
            ShowStartButton();
        }
    }

    private void StopCountdownTimer()
    {
        _countdownActive = false;
        _countdownPaused = false;
        _countdownPausedForMediaError = false;
        _countdownPausedByUser = false;
        _countdownEndsAtElapsedMilliseconds = 0;
        _countdownMillisecondsRemaining = 0;
        _countdownTimer?.Cancel();
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        SetPlaybackControlsAvailability(available: false);
        UpdatePlaybackActionVisual();
    }

    private void PauseCountdown()
    {
        if (!_countdownActive)
        {
            return;
        }

        _countdownMillisecondsRemaining = Math.Max(
            1,
            _countdownEndsAtElapsedMilliseconds -
                Android.OS.SystemClock.ElapsedRealtime());
        _countdownActive = false;
        _countdownPaused = true;
        _countdownTimer?.Cancel();
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        if (_currentWorkoutGroup is not null)
        {
            _sessionService.PauseMovement(
                _state,
                _currentWorkoutGroup,
                _countdownMillisecondsRemaining,
                _countdownPausedByUser);
            _stateStore.Save(_state);
        }
    }

    private void PauseActiveWorkoutForBackground()
    {
        if (_editingActiveWorkoutSetup ||
            _appScreen != AppScreen.Workout ||
            _state.WorkoutCompleted)
        {
            return;
        }

        if (_countdownActive)
        {
            _countdownPausedByUser = true;
            _countdownPausedForMediaError = false;
            PauseCountdown();
            SetPlaybackControlsAvailability(available: _mediaReady);
            UpdatePlaybackActionVisual();
        }

        if (!_restActive ||
            _state.PendingRestGroupId != _currentWorkoutGroup.Id ||
            _state.PendingRestPausedByUser)
        {
            return;
        }

        long nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long millisecondsRemaining = _sessionService
            .GetPendingRestMillisecondsRemaining(_state, nowUnixMilliseconds);
        if (millisecondsRemaining <= 0)
        {
            return;
        }

        _sessionService.PauseRest(
            _state,
            _currentWorkoutGroup,
            millisecondsRemaining);
        _stateStore.Save(_state);
        UpdateRestCountdownText();
        UpdateRestPlaybackActionVisual();
    }

    private void ResumeCountdown()
    {
        if (!_countdownPaused || _countdownMillisecondsRemaining <= 0)
        {
            return;
        }

        bool cueAutomaticSequenceStart = _automaticSequenceStartPending;
        _automaticSequenceStartPending = false;
        StartCountdownTimer(_countdownMillisecondsRemaining);
        if (cueAutomaticSequenceStart)
        {
            CueMovementRestart();
        }
        SetPlaybackControlsAvailability(available: _mediaReady);
        UpdatePlaybackActionVisual();
    }

    private void StartCountdownTimer(long millisecondsRemaining)
    {
        if (_currentWorkoutGroup is null)
        {
            throw new InvalidOperationException(
                "A movement timer requires a current workout group.");
        }

        _sessionService.BeginMovement(
            _state,
            _currentWorkoutGroup,
            millisecondsRemaining,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + millisecondsRemaining);
        _stateStore.SaveDeferred(_state);
        _countdownActive = true;
        _countdownPaused = false;
        _countdownMillisecondsRemaining = millisecondsRemaining;
        _countdownEndsAtElapsedMilliseconds =
            Android.OS.SystemClock.ElapsedRealtime() + millisecondsRemaining;
        UpdateMoveCountdown(millisecondsRemaining);
        UpdatePlaybackActionVisual();

        if (!_countdownActive)
        {
            return;
        }

        _countdownTimer = new WorkoutCountDownTimer(
            millisecondsRemaining,
            250L,
            UpdateMoveCountdown,
            CompleteCountdown);
        ApplyCurrentMediaPlaybackState();
        _countdownTimer.Start();
    }

    private void UpdateMoveCountdown(long millisecondsRemaining)
    {
        int countdownDurationMilliseconds = GetCurrentCountdownDurationMilliseconds();
        long boundedMilliseconds = Math.Clamp(
            millisecondsRemaining,
            0L,
            countdownDurationMilliseconds);
        _countdownMillisecondsRemaining = boundedMilliseconds;
        MovementPhaseState state = MovementPhaseSchedule.GetState(
            boundedMilliseconds,
            includePreparation: !_sessionService.IsSequenceContinuationBlock(
                _state,
                _currentWorkoutGroup));
        string secondsText = state.SecondsRemaining.ToString();
        if (_countdownText.Text != secondsText ||
            state.Phase != _lastMovementPhase)
        {
            _countdownText.Text = secondsText;
            _countdownText.ContentDescription =
                GetMovementCountdownDescription(state);
        }
        _countdownProgress.Progress = (int)boundedMilliseconds;
        ApplyMovementPhase(state);
    }

    private int GetCurrentMovementDurationMilliseconds() =>
        CountdownSeconds * 1_000;

    private int GetCurrentCountdownDurationMilliseconds() =>
        MovementPhaseSchedule.GetCountdownDurationSeconds(
            includePreparation: !_sessionService.IsSequenceContinuationBlock(
                _state,
                _currentWorkoutGroup)) * 1_000;

    private string GetMovementCountdownDescription(
        MovementPhaseState state)
    {
        if (state.Phase == MovementPhase.Preparation)
        {
            return $"Prepare, {state.SecondsRemaining} seconds remaining";
        }

        return state.Phase switch
        {
            MovementPhase.Preparation =>
                $"Prepare, {state.SecondsRemaining} seconds remaining",
            MovementPhase.Continuous =>
                $"{GetMovementCueDescription(GetCurrentMovementPresentation(state.Phase))}, " +
                $"{state.SecondsRemaining} seconds remaining",
            MovementPhase.Complete => "Movement complete",
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private void ApplyMovementPhase(MovementPhaseState state)
    {
        if (state.Phase == MovementPhase.Complete ||
            state.Phase == _lastMovementPhase)
        {
            return;
        }

        MovementPhase? previousPhase = _lastMovementPhase;
        _lastMovementPhase = state.Phase;

        if (state.Phase == MovementPhase.Preparation)
        {
            SetExerciseMediaMirrored(mirrored: false);
            RenderPreparationPhase();
            RenderFullWorkoutPhase(Resource.Color.rest_surface);
            AnimateMediaPhase(resting: true);
            _exerciseVideo.Pause();
            AnnouncePhaseForAccessibility(
                _countdownPanel,
                $"Prepare, {MovementPhaseSchedule.PreparationDurationSeconds} seconds.");
            return;
        }

        if (state.Phase != MovementPhase.Continuous)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        MovementPhasePresentation presentation =
            GetCurrentMovementPresentation(state.Phase);
        RenderCountdownPhase();
        SetExerciseMediaMirrored(presentation.MirrorMedia);
        RenderFullWorkoutPhase(Resource.Color.move_surface);
        AnimateMediaPhase(resting: false);
        if (previousPhase is null or MovementPhase.Preparation)
        {
            RestartExerciseMediaForPhase(state.Phase);
            CueMovementRestart();
            AnnouncePhaseForAccessibility(
                _countdownPanel,
                $"{GetMovementCueDescription(presentation)}, 45 seconds.");
        }
        else
        {
            RestartHoldOrResumeRepetition();
        }
    }

    private void RestartHoldOrResumeRepetition()
    {
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            PlayHoldOnce();
            return;
        }

        ApplyCurrentMediaPlaybackState();
    }

    private void RestartExerciseMediaForPhase(MovementPhase phase)
    {
        Exercise exercise = _currentExercise
            ?? throw new InvalidOperationException(
                "A timed movement phase requires a current exercise.");
        if (exercise.Presentation == ExercisePresentation.Still)
        {
            ShowHoldFrame(exercise.Id);
            return;
        }

        ClearHoldFrame();
        _exerciseVideo.Pause();
        int positionMilliseconds = 0;
        _exerciseVideo.SeekTo(positionMilliseconds);
        RestartHoldOrResumeRepetition();
    }

    private void RecoverInvalidMediaPlayerState()
    {
        _mediaReady = false;
        _activeMediaPlayer = null;
        if (_workoutPhase == WorkoutPhase.Move && _countdownActive)
        {
            PauseCountdown();
        }
        if (_workoutPhase == WorkoutPhase.Move &&
            _countdownPaused &&
            !_countdownPausedByUser)
        {
            _countdownPausedForMediaError = true;
        }

        SetStartAvailability(available: false);
        SetPlaybackControlsAvailability(available: false);
        if (_mediaExercise is not null && _mediaWorkoutGroup is not null)
        {
            LoadExerciseMedia(
                _mediaExercise,
                _mediaWorkoutGroup,
                _previewingUpcomingSequenceBlock);
        }
        else
        {
            ShowMediaError();
        }
    }

    private void CueMovementRestart()
    {
        PlayWhistleCue(_movementStartWhistleId);
        _countdownPanel.PerformHapticFeedback(FeedbackConstants.ClockTick);
    }

    private void RenderCountdownPhase()
    {
        var textColor = new Android.Graphics.Color(
            GetColor(Resource.Color.move_text));
        _countdownText.SetTextColor(textColor);
        StylePlaybackControls(
            textColor,
            Resource.Drawable.phase_move_chip_background);
        _countdownProgress.ProgressDrawable =
            GetDrawable(Resource.Drawable.move_progress_track);
    }

    private void RenderPreparationPhase()
    {
        var textColor = new Android.Graphics.Color(
            GetColor(Resource.Color.rest_text));
        _countdownText.SetTextColor(textColor);
        StylePlaybackControls(
            textColor,
            Resource.Drawable.phase_rest_chip_background);
        _countdownProgress.ProgressDrawable =
            GetDrawable(Resource.Drawable.rest_progress_track);
    }

    private void StylePlaybackControls(
        Android.Graphics.Color tint,
        int backgroundResource)
    {
        Android.Content.Res.ColorStateList tintList =
            Android.Content.Res.ColorStateList.ValueOf(tint);
        foreach (ImageButton control in new[]
                 {
                     _repeatAction,
                     _playbackAction,
                     _nextAction,
                 })
        {
            control.ImageTintList = tintList;
            control.SetBackgroundResource(backgroundResource);
        }
    }

    private bool ShouldExerciseVideoBePlaying()
    {
        if (!_activityResumed || !_mediaReady ||
            _holdFrameImage.Visibility == ViewStates.Visible)
        {
            return false;
        }

        return _workoutPhase == WorkoutPhase.Ready ||
            (_workoutPhase == WorkoutPhase.Rest &&
                _previewingUpcomingSequenceBlock) ||
            (_workoutPhase == WorkoutPhase.Move &&
                _countdownActive &&
                _lastMovementPhase == MovementPhase.Continuous);
    }

    private void ApplyCurrentMediaPlaybackState()
    {
        bool mirrorMedia = _workoutPhase switch
        {
            WorkoutPhase.Rest when _previewingUpcomingSequenceBlock &&
                _mediaWorkoutGroup is not null =>
                GetMovementPresentation(
                    _mediaWorkoutGroup,
                    MovementPhase.Continuous).MirrorMedia,
            WorkoutPhase.Move when _lastMovementPhase is MovementPhase phase &&
                phase != MovementPhase.Preparation =>
                GetCurrentMovementPresentation(phase).MirrorMedia,
            _ => false,
        };
        SetExerciseMediaMirrored(mirrorMedia);

        if (ShouldExerciseVideoBePlaying())
        {
            _exerciseVideo.Start();
        }
        else
        {
            _exerciseVideo.Pause();
        }
    }

    private void SetExerciseMediaMirrored(bool mirrored)
    {
        float scale = mirrored ? -1f : 1f;
        _exerciseVideo.ScaleX = scale;
        _holdFrameImage.ScaleX = scale;
    }

    private MovementPhasePresentation GetCurrentMovementPresentation(
        MovementPhase phase)
    {
        return GetMovementPresentation(_currentWorkoutGroup, phase);
    }

    private static MovementPhasePresentation GetMovementPresentation(
        WorkoutGroup workoutGroup,
        MovementPhase phase) =>
        MovementPhasePresentationPolicy.GetPresentation(
            workoutGroup.SequenceSideCue,
            workoutGroup.SequenceDirectionCue,
            workoutGroup.MirrorSequenceMedia,
            phase);

    private static string GetMovementCueDescription(
        MovementPhasePresentation presentation)
    {
        var parts = new List<string>(2);
        string? side = presentation.SideCue switch
        {
            ExerciseSequenceSideCue.None => null,
            ExerciseSequenceSideCue.ScreenLeft => "Left side",
            ExerciseSequenceSideCue.ScreenRight => "Right side",
            ExerciseSequenceSideCue.ShownLeadStance => "Shown lead stance",
            ExerciseSequenceSideCue.OppositeLeadStance => "Opposite lead stance",
            _ => throw new ArgumentOutOfRangeException(
                nameof(presentation), presentation.SideCue, null),
        };
        string? direction = presentation.DirectionCue switch
        {
            ExerciseSequenceDirectionCue.None => null,
            ExerciseSequenceDirectionCue.Forward => "Forward",
            ExerciseSequenceDirectionCue.Backward => "Backward",
            ExerciseSequenceDirectionCue.Clockwise => "Clockwise",
            ExerciseSequenceDirectionCue.Counterclockwise => "Counterclockwise",
            ExerciseSequenceDirectionCue.Inward => "Inward",
            ExerciseSequenceDirectionCue.Outward => "Outward",
            _ => throw new ArgumentOutOfRangeException(
                nameof(presentation), presentation.DirectionCue, null),
        };
        if (side is not null)
        {
            parts.Add(side);
        }
        if (direction is not null)
        {
            parts.Add(direction);
        }
        return parts.Count == 0 ? "Move" : string.Join(", ", parts);
    }

    private void RenderFullWorkoutPhase(int colorResource)
    {
        var color = new Android.Graphics.Color(GetColor(colorResource));
        _workoutPhaseSurface.Visibility = ViewStates.Visible;
        _workoutPhaseLeft.SetBackgroundColor(color);
        _workoutPhaseRight.SetBackgroundColor(color);
        SetExerciseMediaPhase(
            resting: colorResource == Resource.Color.rest_surface);
        AnimatePhaseSurface();
    }

    private void SetExerciseMediaPhase(bool resting)
    {
        SetExerciseMediaBackground(resting
            ? Resource.Drawable.media_card_rest_background
            : Resource.Drawable.media_card_move_background);
    }

    private void SetExerciseMediaBackground(int drawableResource)
    {
        _exerciseMediaCard.SetBackgroundResource(drawableResource);
        int mediaPadding = DpInt(4f);
        _exerciseMediaCard.SetPadding(
            mediaPadding,
            mediaPadding,
            mediaPadding,
            mediaPadding);
    }

    private void AnimatePhaseSurface()
    {
        _workoutPhaseSurface.Animate()?.Cancel();
        _workoutPhaseSurface.Alpha = 0.86f;
        if (_workoutPhaseSurface.Animate() is { } animator)
        {
            animator
                .Alpha(1f)
                .SetDuration(HueMotionDurationMilliseconds)
                .Start();
        }
    }

    private void AnimateMediaPhase(bool resting)
    {
        _exerciseMediaCard.Animate()?.Cancel();
        if (_exerciseMediaCard.Animate() is not { } animator)
        {
            return;
        }

        animator
            .Alpha(resting ? 0.92f : 1f)
            .ScaleX(resting ? 0.985f : 1f)
            .ScaleY(resting ? 0.985f : 1f)
            .SetDuration(HueMotionDurationMilliseconds)
            .Start();
    }

    private void RenderRestVisuals()
    {
        SetExerciseMediaMirrored(mirrored: false);
        RenderFullWorkoutPhase(Resource.Color.rest_surface);
        AnimateMediaPhase(resting: true);
    }

    private void ResetMovementVisuals()
    {
        _lastMovementPhase = null;
        RenderCountdownPhase();
        SetExerciseMediaMirrored(mirrored: false);
        _exerciseMediaCard.Animate()?.Cancel();
        _exerciseMediaCard.Alpha = 1f;
        _exerciseMediaCard.ScaleX = 1f;
        _exerciseMediaCard.ScaleY = 1f;
        SetExerciseMediaBackground(Resource.Drawable.media_card_background);
        _workoutPhaseSurface.Animate()?.Cancel();
        _workoutPhaseSurface.Alpha = 1f;
        _workoutPhaseSurface.Visibility = ViewStates.Gone;
    }

    private void BeginRest()
    {
        _sessionService.BeginRest(
            _state,
            _currentWorkoutGroup,
            DateTimeOffset.UtcNow.AddSeconds(RestSeconds).ToUnixTimeMilliseconds());
        _stateStore.Save(_state);

        _restActive = true;
        _exerciseVideo.Pause();
        if (_currentExercise?.Mode == ExerciseMode.Hold)
        {
            FreezeHoldOnFinalFrame();
        }
        ShowRestPanel();
        AnnouncePhaseForAccessibility(_restPanel, GetRestDescription());
        ResumeRestCountdown();
    }

    private string GetRestDescription()
    {
        long millisecondsRemaining = _sessionService
            .GetPendingRestMillisecondsRemaining(
                _state,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        int secondsRemaining = (int)Math.Ceiling(millisecondsRemaining / 1000d);
        if (_state.PendingRestPausedByUser)
        {
            return $"Rest paused, {secondsRemaining} seconds remaining.";
        }

        WorkoutGroup? nextBlock = _sessionService.GetNextSequenceBlock(
            _state,
            _currentWorkoutGroup);
        if (nextBlock is not null)
        {
            Exercise nextExercise = _sessionService.GetSelectedExercise(
                _state,
                nextBlock);
            return $"Rest, {secondsRemaining} seconds. " +
                $"Next block: {nextExercise.Name}. It starts automatically.";
        }

        return $"Rest, {secondsRemaining} seconds. " +
            "Tap the heart to keep this sequence.";
    }

    private void ShowRestPanel()
    {
        WorkoutGroup? nextBlock = _sessionService.GetNextSequenceBlock(
            _state,
            _currentWorkoutGroup);
        RenderRestVisuals();
        ShowWorkoutPhase(WorkoutPhase.Rest);
        if (nextBlock is not null)
        {
            ShowUpcomingSequenceBlockPreview(nextBlock);
        }
        else
        {
            _previewingUpcomingSequenceBlock = false;
        }
        RenderExecutionTimeline(
            selectUpcomingBlock: nextBlock is not null);
        UpdateRestPlaybackActionVisual();
        UpdateKeepButtonState();
        UpdateRestCountdownText();
    }

    private void ShowUpcomingSequenceBlockPreview(WorkoutGroup nextBlock)
    {
        Exercise nextExercise = _sessionService.GetSelectedExercise(
            _state,
            nextBlock);
        RenderExerciseIdentity(nextExercise, upcoming: true);
        LoadExerciseMedia(
            nextExercise,
            nextBlock,
            previewingUpcomingSequenceBlock: true);
        SetExerciseMediaMirrored(
            GetMovementPresentation(
                nextBlock,
                MovementPhase.Continuous).MirrorMedia);
    }

    private void UpdateKeepButtonState()
    {
        if (_sessionService.IsIntermediateSequenceBlock(
                _state,
                _currentWorkoutGroup))
        {
            _keepButton.Enabled = false;
            _keepButton.Visibility = ViewStates.Gone;
            return;
        }

        _keepButton.Visibility = ViewStates.Visible;
        _keepButton.Enabled = !_state.PendingRestKept;
        _keepButton.Alpha = 1f;
        _keepButton.SetBackgroundResource(_state.PendingRestKept
            ? Resource.Drawable.kept_button_background
            : Resource.Drawable.rest_button_background);
        _keepButton.SetColorFilter(new Android.Graphics.Color(GetColor(
            _state.PendingRestKept
                ? Resource.Color.accent_text
                : Resource.Color.white)));
        _keepButton.ContentDescription = _state.PendingRestKept
            ? "Exercise kept for the next session"
            : GetString(Resource.String.keep_exercise_description);
    }

    private void ResumeRestCountdown()
    {
        if (!_restActive)
        {
            return;
        }

        _restTimer?.Cancel();
        _restTimer?.Dispose();
        _restTimer = null;

        long millisecondsRemaining = _sessionService
            .GetPendingRestMillisecondsRemaining(
                _state,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        UpdateRestCountdownText();
        UpdateRestPlaybackActionVisual();
        if (_state.PendingRestPausedByUser)
        {
            return;
        }
        if (millisecondsRemaining <= 0)
        {
            CompleteRest();
            return;
        }

        _restTimer = new WorkoutCountDownTimer(
            millisecondsRemaining,
            250L,
            _ => UpdateRestCountdownText(),
            CompleteRest);
        _restTimer.Start();
    }

    private void UpdateRestCountdownText()
    {
        long millisecondsRemaining = _sessionService
            .GetPendingRestMillisecondsRemaining(
                _state,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        int secondsRemaining = (int)Math.Ceiling(millisecondsRemaining / 1000d);
        string secondsText = secondsRemaining.ToString();
        if (_restCountdownText.Text != secondsText)
        {
            _restCountdownText.Text = secondsText;
            _restCountdownText.ContentDescription =
                $"Rest, {secondsRemaining} seconds remaining";
        }
        _restProgress.Progress = (int)Math.Min(
            millisecondsRemaining,
            RestSeconds * 1000L);
    }

    private void ToggleRestPlayback()
    {
        if (!_restActive ||
            _state.PendingRestGroupId != _currentWorkoutGroup.Id)
        {
            return;
        }

        long nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long millisecondsRemaining = _sessionService
            .GetPendingRestMillisecondsRemaining(_state, nowUnixMilliseconds);
        if (millisecondsRemaining <= 0)
        {
            CompleteRest();
            return;
        }

        if (_state.PendingRestPausedByUser)
        {
            _sessionService.ResumeRest(
                _state,
                _currentWorkoutGroup,
                nowUnixMilliseconds + millisecondsRemaining);
            _stateStore.Save(_state);
            _restPlaybackAction.PerformHapticFeedback(
                FeedbackConstants.KeyboardTap);
            ResumeRestCountdown();
            int secondsRemaining =
                (int)Math.Ceiling(millisecondsRemaining / 1000d);
            AnnouncePhaseForAccessibility(
                _restPlaybackAction,
                $"Rest resumed, {secondsRemaining} seconds remaining.");
            return;
        }

        _sessionService.PauseRest(
            _state,
            _currentWorkoutGroup,
            millisecondsRemaining);
        _stateStore.Save(_state);
        PauseRestCountdown();
        UpdateRestCountdownText();
        UpdateRestPlaybackActionVisual();
        _restPlaybackAction.PerformHapticFeedback(
            FeedbackConstants.KeyboardTap);
        int pausedSecondsRemaining =
            (int)Math.Ceiling(millisecondsRemaining / 1000d);
        AnnouncePhaseForAccessibility(
            _restPlaybackAction,
            $"Rest paused, {pausedSecondsRemaining} seconds remaining.");
    }

    private void UpdateRestPlaybackActionVisual()
    {
        bool paused = _state.PendingRestPausedByUser;
        _restPlaybackAction.SetImageResource(
            paused
                ? Resource.Drawable.ic_phase_active
                : Resource.Drawable.ic_phase_pause);
        _restPlaybackAction.ContentDescription = GetString(
            paused
                ? Resource.String.resume_rest_description
                : Resource.String.pause_rest_description);
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _restPlaybackAction.TooltipText =
                _restPlaybackAction.ContentDescription;
        }
    }

    private void PauseRestCountdown()
    {
        _restTimer?.Cancel();
        _restTimer?.Dispose();
        _restTimer = null;
    }

    private void KeepCurrentExercise()
    {
        if (!_restActive ||
            _state.PendingRestKept ||
            !_sessionService.KeepPendingRest(_state))
        {
            return;
        }

        _stateStore.Save(_state);
        _keepButton.Enabled = false;
        _keepButton.PerformHapticFeedback(FeedbackConstants.KeyboardTap);
        CompleteRest();
    }

    private void CompleteRest()
    {
        if (!_restActive || _state.PendingRestGroupId != _currentWorkoutGroup.Id)
        {
            return;
        }

        _restActive = false;
        PauseRestCountdown();
        if (_sessionService.IsIntermediateSequenceBlock(
                _state,
                _currentWorkoutGroup))
        {
            ContinueWithNextSequenceBlock();
            return;
        }

        bool keep = _state.PendingRestKept;
        FinalizeCurrentRound(keep);
    }

    private void ContinueWithNextSequenceBlock()
    {
        _sessionService.AdvanceSequence(_state, _currentWorkoutGroup);
        _sessionService.ClearPendingRest(_state);
        WorkoutGroup nextBlock = _sessionService.GetNextGroup(_state)
            ?? throw new InvalidOperationException(
                "An intermediate sequence block has no following block.");
        int movementDurationMilliseconds =
            MovementPhaseSchedule.TotalDurationSeconds * 1_000;
        _sessionService.PauseMovement(
            _state,
            nextBlock,
            movementDurationMilliseconds,
            pausedByUser: false);
        _stateStore.Save(_state);
        RestorePendingMovement(nextBlock);
        if (_mediaReady && _activityResumed)
        {
            _countdownPausedForMediaError = false;
            ResumeCountdown();
        }
    }

    private void FinalizeCurrentRound(bool keep)
    {
        RecordedWorkoutOutcome result = _sessionService.RecordOutcomeWithScoreUpdates(
            _state,
            _currentWorkoutGroup,
            keep);
        _sessionService.ClearPendingRest(_state);

        SaveStateAndScores(result.ScoreUpdates);
        if (_state.WorkoutCompleted)
        {
            PlayWhistleCue(_workoutCompleteWhistleId);
            ShowCongratulations();
        }
        else
        {
            ShowNextExercise();
        }
    }

    private void ShowCongratulations()
    {
        CancelCountdown(resetToStart: false);
        PauseRestCountdown();
        _restActive = false;
        _exerciseVideo.StopPlayback();
        ResetMovementVisuals();
        _currentExercise = null;
        ShowAppScreen(AppScreen.Completion);

        _completionHalo.Animate()?.Cancel();
        _completionHalo.Alpha = 0f;
        _completionHalo.ScaleX = 0.88f;
        _completionHalo.ScaleY = 0.88f;
        if (_completionHalo.Animate() is { } haloAnimator)
        {
            haloAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetDuration(240L)
                .Start();
        }

        _completionMark.Animate()?.Cancel();
        _completionMark.Alpha = 0f;
        _completionMark.ScaleX = 0.8f;
        _completionMark.ScaleY = 0.8f;
        if (_completionMark.Animate() is { } markAnimator)
        {
            markAnimator
                .Alpha(1f)
                .ScaleX(1f)
                .ScaleY(1f)
                .SetStartDelay(40L)
                .SetDuration(200L)
                .Start();
        }

        _doneButton.Animate()?.Cancel();
        _doneButton.Alpha = 0f;
        _doneButton.TranslationY = Dp(5f);
        if (_doneButton.Animate() is { } doneAnimator)
        {
            doneAnimator
                .Alpha(1f)
                .TranslationY(0f)
                .SetStartDelay(60L)
                .SetDuration(180L)
                .Start();
        }
        AnnouncePhaseForAccessibility(
            _congratulationsScreen,
            $"Workout complete. {_state.ActiveWorkoutMinutes} minutes finished.");
    }

    private void CloseCompletedWorkout()
    {
        _sessionService.AcknowledgeCompletion(_state);
        _stateStore.Save(_state);
        FinishAndRemoveTask();
    }

    private void ConfigureWhistleCues()
    {
        try
        {
            ConfigureWhistleCuesCore();
        }
        catch (Exception)
        {
            _whistleSoundPool?.Release();
            _whistleSoundPool?.Dispose();
            _whistleSoundPool = null;
            _movementStartWhistleId = 0;
            _restStartWhistleId = 0;
            _workoutCompleteWhistleId = 0;
        }
    }

    private void ConfigureWhistleCuesCore()
    {
        using var attributesBuilder = new Android.Media.AudioAttributes.Builder();
        Android.Media.AudioAttributes.Builder configuredAttributesBuilder =
            attributesBuilder.SetUsage(
                Android.Media.AudioUsageKind.Media)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle audio usage.");
        configuredAttributesBuilder = configuredAttributesBuilder.SetContentType(
            Android.Media.AudioContentType.Sonification)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle audio content.");
        using Android.Media.AudioAttributes attributes =
            configuredAttributesBuilder.Build()
            ?? throw new InvalidOperationException(
                "Unable to create whistle audio attributes.");
        using var soundPoolBuilder = new Android.Media.SoundPool.Builder();
        Android.Media.SoundPool.Builder configuredSoundPoolBuilder =
            soundPoolBuilder.SetMaxStreams(1)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle playback streams.");
        configuredSoundPoolBuilder = configuredSoundPoolBuilder.SetAudioAttributes(
            attributes)
            ?? throw new InvalidOperationException(
                "Unable to configure whistle playback attributes.");
        Android.Media.SoundPool soundPool = configuredSoundPoolBuilder.Build()
            ?? throw new InvalidOperationException(
                "Unable to create whistle playback.");
        _whistleSoundPool = soundPool;
        _movementStartWhistleId = soundPool.Load(
            this,
            Resource.Raw.whistle_start,
            1);
        _restStartWhistleId = soundPool.Load(
            this,
            Resource.Raw.whistle_rest,
            1);
        _workoutCompleteWhistleId = soundPool.Load(
            this,
            Resource.Raw.whistle_complete,
            1);
    }

    private void PlayWhistleCue(int soundId)
    {
        try
        {
            if (soundId > 0)
            {
                _whistleSoundPool?.Play(
                    soundId,
                    0.78f,
                    0.78f,
                    1,
                    0,
                    1f);
            }
        }
        catch (Exception)
        {
            // A missing or unavailable audio output should not stop a workout.
        }
    }

    [SuppressMessage(
        "Interoperability",
        "CA1422:Validate platform compatibility",
        Justification = "This private API 24+ app uses the established one-shot " +
            "announcement behavior; Android 36 deprecation does not remove it.")]
    private static void AnnouncePhaseForAccessibility(View view, string announcement)
    {
        view.AnnounceForAccessibility(announcement);
    }

    private T FindRequiredView<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>(int resourceId)
        where T : View
    {
        View? view = FindViewById(resourceId);
        if (view is T typedView)
        {
            return typedView;
        }

        string resourceName;
        try
        {
            resourceName = Resources?.GetResourceEntryName(resourceId) ?? resourceId.ToString();
        }
        catch (Android.Content.Res.Resources.NotFoundException)
        {
            resourceName = resourceId.ToString();
        }

        string actualType = view?.GetType().FullName ?? "missing";
        throw new InvalidOperationException(
            $"View resource {resourceName} is {actualType}; expected {typeof(T).FullName}.");
    }

    private sealed class WorkoutCountDownTimer : Android.OS.CountDownTimer
    {
        private readonly Action<long> _onTick;
        private readonly Action _onFinish;

        public WorkoutCountDownTimer(
            long millisInFuture,
            long countDownInterval,
            Action<long> onTick,
            Action onFinish)
            : base(millisInFuture, countDownInterval)
        {
            _onTick = onTick;
            _onFinish = onFinish;
        }

        public override void OnTick(long millisUntilFinished)
        {
            _onTick(millisUntilFinished);
        }

        public override void OnFinish()
        {
            _onFinish();
        }
    }

    private sealed class VideoPreparedListener(Action<Android.Media.MediaPlayer> onPrepared)
        : Java.Lang.Object, Android.Media.MediaPlayer.IOnPreparedListener
    {
        public void OnPrepared(Android.Media.MediaPlayer? mediaPlayer)
        {
            if (mediaPlayer is not null)
            {
                onPrepared(mediaPlayer);
            }
        }
    }

    private sealed class VideoErrorListener(Func<bool> onError)
        : Java.Lang.Object, Android.Media.MediaPlayer.IOnErrorListener
    {
        public bool OnError(
            Android.Media.MediaPlayer? mediaPlayer,
            Android.Media.MediaError what,
            int extra)
        {
            return onError();
        }
    }

    private sealed class VideoInfoListener(Func<Android.Media.MediaInfo, bool> onInfo)
        : Java.Lang.Object, Android.Media.MediaPlayer.IOnInfoListener
    {
        public bool OnInfo(
            Android.Media.MediaPlayer? mediaPlayer,
            Android.Media.MediaInfo what,
            int extra)
        {
            return onInfo(what);
        }
    }

    private sealed record PreparedWorkout(
        WorkoutState State,
        int Minutes,
        WorkoutModifiers Modifiers,
        bool IsReconfiguration,
        string? CurrentWorkoutGroupId);

    private sealed record ApplicationStartupResult(
        SqliteExerciseDatabase Database,
        ExerciseSessionService SessionService,
        WorkoutGroup? PendingMovementGroup,
        WorkoutGroup? PendingRestGroup);

    private sealed class DurationSeekAccessibilityDelegate(
        Func<int> getMinutes,
        Func<int> getOptionIndex,
        Action<int> setOptionIndex) : View.AccessibilityDelegate
    {
        public override void OnInitializeAccessibilityNodeInfo(
            View host,
            Android.Views.Accessibility.AccessibilityNodeInfo info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            int minutes = getMinutes();
            int optionIndex = getOptionIndex();
#pragma warning disable CA1422 // Obtain is required for the supported API 24-29 range.
            info.SetRangeInfo(
                Android.Views.Accessibility.AccessibilityNodeInfo.RangeInfo.Obtain(
                    Android.Views.Accessibility.RangeType.Int,
                    0,
                    ExerciseSessionService.SupportedWorkoutMinutes.Count - 1,
                    optionIndex));
#pragma warning restore CA1422
            info.ContentDescription =
                $"Workout duration, {minutes} minutes. " +
                "Options: 3, 5, 7, 10, 15, 20, 30, 45, 60, and 90 minutes";
        }

        public override bool PerformAccessibilityAction(
            View host,
            Android.Views.Accessibility.Action action,
            Bundle? arguments)
        {
            if ((int)action == Android.Resource.Id.AccessibilityActionSetProgress &&
                arguments is not null)
            {
                float requestedOptionIndex = arguments.GetFloat(
                    Android.Views.Accessibility.AccessibilityNodeInfo
                        .ActionArgumentProgressValue);
                int optionIndex = Math.Clamp(
                    (int)MathF.Round(requestedOptionIndex),
                    0,
                    ExerciseSessionService.SupportedWorkoutMinutes.Count - 1);
                setOptionIndex(optionIndex);
                return true;
            }

            return base.PerformAccessibilityAction(host, action, arguments);
        }
    }
}
