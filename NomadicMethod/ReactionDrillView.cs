using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using MediaPlayer = Android.Media.MediaPlayer;
using Android.Views;
using Android.Widget;
using NomadicMethod.Services;

namespace NomadicMethod;

// Every response has its own prepared decoder. The target is unpredictable,
// but feedback never waits for a new source to load after the cue.
internal sealed class ReactionDrillView : FrameLayout
{
    private readonly ReactionPlan _plan;
    private readonly Action _onReady;
    private readonly Action _onError;
    private readonly MediaPlayer[] _players;
    private readonly TextureView[] _videos;
    private readonly ImageView[] _images;
    private readonly TextView[] _labels;
    private readonly TextView[] _numbers;
    private readonly bool[] _prepared;
    private readonly List<SurfaceListener> _listeners = [];
    private readonly List<Bitmap> _bitmaps = [];
    private readonly TextView _context;
    private readonly TextView _cue;
    private bool _released;
    private bool _failed;
    private bool _previewEnabled;
    private int _previewIndex;
    private int _activeIndex = -1;
    private readonly List<View> _cards = [];
    public bool Ready => !_released && !_failed && _prepared.All(value => value);
    public bool PreviewEnabled => _previewEnabled;

    public ReactionDrillView(Context context, ReactionPlan plan,
        Func<string, string> videoPath, Action onReady, Action onError) : base(context)
    {
        _plan = plan;
        _onReady = onReady;
        _onError = onError;
        SetBackgroundColor(new Color(context.GetColor(Resource.Color.media_background)));
        var column = new LinearLayout(context) { Orientation = Orientation.Vertical };
        AddView(column, new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
        _context = Text(context, 12);
        _context.SetMinHeight(Dp(28));
        column.AddView(_context, new LinearLayout.LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent));
        var stage = new FrameLayout(context);
        column.AddView(stage, new LinearLayout.LayoutParams(LayoutParams.MatchParent, 0, 1));
        _cue = Text(context, 28);
        _cue.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        _players = new MediaPlayer[plan.Actions.Length];
        _videos = new TextureView[plan.Actions.Length];
        _images = new ImageView[plan.Actions.Length];
        _labels = new TextView[plan.Actions.Length];
        _numbers = new TextView[plan.Actions.Length];
        _prepared = new bool[plan.Actions.Length];
        var cards = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        cards.SetGravity(GravityFlags.Center);
        try
        {
        for (int index = 0; index < plan.Actions.Length; index++)
        {
            int actionIndex = index;
            ReactionAction action = plan.Actions[index];
            var video = new TextureView(context) { Alpha = 0f, ImportantForAccessibility = ImportantForAccessibility.No };
            var player = new MediaPlayer();
            _players[index] = player;
            _videos[index] = video;
            player.Prepared += (_, _) =>
            {
                if (_released) return;
                if (action.DurationSeconds * 1000 > player.Duration + 100) { Fail(); return; }
                player.SetVolume(0f, 0f);
                player.Looping = false;
                Prepared(actionIndex);
            };
            player.SeekComplete += (_, _) => { if (!_released) Prepared(actionIndex); };
            player.Error += (_, args) => { args.Handled = true; Fail(); };
            var listener = new SurfaceListener(texture =>
            {
                if (_released) return;
                try
                {
                    _prepared[actionIndex] = false;
                    player.Reset();
                    using var surface = new Surface(texture);
                    player.SetSurface(surface);
                    FitVideo(video);
                    player.SetDataSource(videoPath(action.Video));
                    player.PrepareAsync();
                }
                catch (Exception) { Fail(); }
            }, () =>
            {
                if (_released) return;
                _prepared[actionIndex] = false;
                try { if (player.IsPlaying) player.Pause(); } catch (Exception) { }
                // A recreated texture prepares again; this never advances work.
                _onError();
            }, () => FitVideo(video));
            _listeners.Add(listener);
            video.SurfaceTextureListener = listener;
            stage.AddView(video, new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));

            var card = new LinearLayout(context) { Orientation = Orientation.Vertical, Focusable = true };
            card.ContentDescription = $"Preview {action.Label}";
            card.Click += (_, _) => PreviewAction(actionIndex);
            _cards.Add(card);
            card.SetPadding(Dp(2), Dp(2), Dp(2), Dp(3));
            var background = new GradientDrawable();
            background.SetColor(Color.White);
            background.SetCornerRadius(Dp(9));
            background.SetStroke(Dp(1), Color.Argb(41, 20, 20, 20));
            card.Background = background;
            var image = new ImageView(context);
            image.SetScaleType(ImageView.ScaleType.FitCenter);
            using Stream source = context.Assets!.Open(action.Preview);
            Bitmap bitmap = BitmapFactory.DecodeStream(source)
                ?? throw new InvalidDataException("Invalid reaction preview.");
            _bitmaps.Add(bitmap);
            image.SetImageBitmap(bitmap);
            _images[index] = image;
            var thumbnail = new FrameLayout(context);
            thumbnail.AddView(image, new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
            var number = Text(context, 14);
            number.SetMinHeight(Dp(20));
            number.SetTextColor(Color.White);
            var numberBackground = new GradientDrawable();
            numberBackground.SetColor(Color.Rgb(32, 35, 38));
            numberBackground.SetCornerRadius(Dp(6));
            number.Background = numberBackground;
            _numbers[index] = number;
            thumbnail.AddView(number, new LayoutParams(Dp(20), LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.Left)
                { LeftMargin = Dp(3), TopMargin = Dp(3) });
            card.AddView(thumbnail, new LinearLayout.LayoutParams(LayoutParams.MatchParent, Dp(46)));
            var label = Text(context, 11);
            label.SetTextColor(Color.Rgb(32, 35, 38));
            label.SetMaxLines(3);
            _labels[index] = label;
            card.AddView(label, new LinearLayout.LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent));
            cards.AddView(card, new LinearLayout.LayoutParams(0, LayoutParams.WrapContent, 1)
                { LeftMargin = Dp(2), RightMargin = Dp(2) });
        }
        stage.AddView(_cue, new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
        column.AddView(cards, new LinearLayout.LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent));
        Render(null, false);
        }
        catch
        {
            Release();
            throw;
        }
    }

    private int Dp(int value) => (int)Math.Round(value * Resources!.DisplayMetrics!.Density);
    private static void FitVideo(TextureView video)
    {
        if (video.Width <= 0 || video.Height <= 0) return;
        using var matrix = new Matrix();
        matrix.SetScale(Math.Min(1f, (float)video.Height / video.Width),
            Math.Min(1f, (float)video.Width / video.Height), video.Width / 2f, video.Height / 2f);
        video.SetTransform(matrix);
    }
    private static TextView Text(Context context, float size) => new(context)
    {
        TextSize = size,
        Gravity = GravityFlags.Center,
        TextAlignment = TextAlignment.Center,
    };
    private void Prepared(int index)
    {
        bool wasReady = Ready;
        _prepared[index] = true;
        if (Ready && !wasReady) _onReady();
        if (Ready && _previewEnabled) PreviewAction(_previewIndex);
    }
    private void Fail()
    {
        if (_released || _failed) return;
        _failed = true;
        foreach (MediaPlayer player in _players)
            try { if (player.IsPlaying) player.Pause(); } catch (Exception) { }
        Array.Fill(_prepared, false);
        _onError();
    }

    public void Render(ReactionDrill? drill, bool mirror)
    {
        if (_released) return;
        if (_previewEnabled) SetPreviewEnabled(false);
        foreach (View card in _cards) card.Clickable = false;
        bool reverse = drill?.Reverse == true;
        _context.Text = _plan.Protocol == "StopOnly" ? "3 · 2 · 1 → action; STOP → stay"
            : _plan.Protocol == "ContextChoice"
                ? (reverse ? "⇄  Reverse" : "→  In order") : "Number → action";
        int active = drill?.DemoActionIndex ?? -1;
        int previous = _activeIndex;
        _activeIndex = active;
        for (int index = 0; index < _players.Length; index++)
        {
            int number = reverse ? _players.Length - index : index + 1;
            _labels[index].Text = _plan.Actions[index].Label;
            _numbers[index].Text = number.ToString();
            _cards[index].ContentDescription = $"Preview {number}: {_plan.Actions[index].Label}";
            float scale = mirror ^ _plan.Actions[index].Mirror ? -1f : 1f;
            _videos[index].ScaleX = scale;
            _images[index].ScaleX = scale;
            bool shown = index == active;
            _videos[index].Alpha = shown ? 1f : 0f;
            try
            {
                _players[index].Looping = false;
                if (shown && Ready && previous != active) _players[index].Start();
                else if (!shown && _prepared[index] && _players[index].IsPlaying) _players[index].Pause();
            }
            catch (Exception) { Fail(); return; }
        }
        _cue.Visibility = active >= 0 ? ViewStates.Invisible : ViewStates.Visible;
        _cue.Text = drill?.Cue ?? "Ready";
        _cue.TextSize = drill?.Phase is "Prompt" or "Countdown" or "Stop" ? 64 : 28;
        _cue.SetTextColor(drill?.Phase is "Prompt" or "Countdown"
            ? Color.Rgb(37, 122, 73) : Color.Rgb(32, 35, 38));
    }

    public bool Frozen { get; private set; }
    public void Freeze()
    {
        if (_released) return;
        Frozen = true;
        _previewEnabled = false;
        foreach (View card in _cards) card.Clickable = false;
        foreach (MediaPlayer player in _players)
        {
            try { if (player.IsPlaying) player.Pause(); }
            catch (Java.Lang.IllegalStateException) { }
        }
    }

    public void ResetPlayback()
    {
        if (_released) return;
        Frozen = false;
        _activeIndex = -1;
        for (int index = 0; index < _players.Length; index++)
        {
            _videos[index].Alpha = 0f;
            if (!_prepared[index]) continue;
            try
            {
                MediaPlayer player = _players[index];
                player.Looping = false;
                if (player.IsPlaying) player.Pause();
                if (player.CurrentPosition > 20)
                {
                    _prepared[index] = false;
                    player.SeekTo(0);
                }
            }
            catch (Exception) { Fail(); return; }
        }
    }

    public void SetPreviewEnabled(bool enabled)
    {
        if (_released || _previewEnabled == enabled) return;
        _previewEnabled = enabled;
        foreach (View card in _cards) card.Clickable = enabled;
        if (enabled) PreviewAction(_previewIndex);
        else ResetPlayback();
    }

    private void PreviewAction(int index)
    {
        if (!_previewEnabled || !Ready) return;
        _previewIndex = index;
        for (int position = 0; position < _players.Length; position++)
        {
            bool shown = position == index;
            _videos[position].Alpha = shown ? 1f : 0f;
            try
            {
                _players[position].Looping = shown;
                if (shown) _players[position].Start();
                else if (_players[position].IsPlaying) _players[position].Pause();
            }
            catch (Exception) { Fail(); return; }
        }
        _cue.Visibility = ViewStates.Invisible;
    }

    public void Release()
    {
        if (_released) return;
        _released = true;
        foreach (MediaPlayer? player in _players) { player?.Release(); player?.Dispose(); }
        foreach (TextureView? video in _videos) if (video is not null) video.SurfaceTextureListener = null;
        foreach (SurfaceListener listener in _listeners) listener.Dispose();
        foreach (ImageView? image in _images) image?.SetImageDrawable(null);
        foreach (Bitmap bitmap in _bitmaps) bitmap.Dispose();
    }

    private sealed class SurfaceListener(Action<SurfaceTexture> available, Action unavailable, Action resized)
        : Java.Lang.Object, TextureView.ISurfaceTextureListener
    {
        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height) => available(surface);
        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) { unavailable(); return true; }
        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) => resized();
        public void OnSurfaceTextureUpdated(SurfaceTexture surface) { }
    }
}
