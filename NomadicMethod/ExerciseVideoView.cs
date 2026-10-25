using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Util;
using Android.Views;

namespace NomadicMethod;

// Keep video in the view hierarchy so side mirroring, rounded clipping and
// phase fades affect the decoded frames just as they affect held images.
public sealed class ExerciseVideoView : TextureView, TextureView.ISurfaceTextureListener
{
    private MediaPlayer? _player;
    private Surface? _surface;
    private SurfaceTexture? _texture;
    private string? _path;
    private bool _prepared;
    private bool _playWhenReady;
    private int _seekWhenReady = -1;
    private int _videoWidth;
    private int _videoHeight;
    private MediaPlayer.IOnPreparedListener? _preparedListener;
    private MediaPlayer.IOnErrorListener? _errorListener;
    private MediaPlayer.IOnInfoListener? _infoListener;

    public ExerciseVideoView(Context context) : this(context, null) { }

    public ExerciseVideoView(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
        SurfaceTextureListener = this;
        SetOpaque(false);
    }

    public event EventHandler? Completion;
    public event EventHandler? SurfaceLost;

    public void SetOnPreparedListener(MediaPlayer.IOnPreparedListener listener) => _preparedListener = listener;
    public void SetOnErrorListener(MediaPlayer.IOnErrorListener listener) => _errorListener = listener;
    public void SetOnInfoListener(MediaPlayer.IOnInfoListener listener) => _infoListener = listener;

    public void SetVideoPath(string path)
    {
        _path = path;
        _seekWhenReady = -1;
        _playWhenReady = false;
        OpenVideo();
    }

    public void Start()
    {
        _playWhenReady = true;
        if (_prepared) _player?.Start();
    }

    public int PositionMilliseconds
    {
        get
        {
            try { return _prepared ? Math.Max(0, _player?.CurrentPosition ?? 0) : 0; }
            catch (Java.Lang.IllegalStateException) { return 0; }
            catch (ObjectDisposedException) { return 0; }
        }
    }

    public void Pause()
    {
        _playWhenReady = false;
        if (_prepared && _player?.IsPlaying == true) _player.Pause();
    }

    public void SeekTo(int milliseconds)
    {
        if (_prepared) _player?.SeekTo(milliseconds);
        else _seekWhenReady = milliseconds;
    }

    public void StopPlayback()
    {
        _path = null;
        _playWhenReady = false;
        _seekWhenReady = -1;
        ReleasePlayer();
    }

    private void OpenVideo()
    {
        ReleasePlayer();
        if (_texture is null || _path is null) return;
        var player = new MediaPlayer();
        _player = player;
        player.Prepared += (_, _) =>
        {
            if (!ReferenceEquals(_player, player)) return;
            _prepared = true;
            _videoWidth = player.VideoWidth;
            _videoHeight = player.VideoHeight;
            FitVideo();
            // Preparing does not decode a frame on every device. Seek even at
            // zero so a cold-restored, paused workout shows its demonstration
            // without starting playback or advancing the workout timer.
            player.SeekTo(Math.Max(0, _seekWhenReady));
            _seekWhenReady = -1;
            _preparedListener?.OnPrepared(player);
            if (_playWhenReady && ReferenceEquals(_player, player)) player.Start();
        };
        player.Completion += (_, _) =>
        {
            if (ReferenceEquals(_player, player)) Completion?.Invoke(this, EventArgs.Empty);
        };
        player.Error += (_, args) =>
        {
            if (ReferenceEquals(_player, player)) _prepared = false;
            args.Handled = !ReferenceEquals(_player, player) ||
                (_errorListener?.OnError(player, args.What, args.Extra) ?? false);
        };
        player.Info += (_, args) =>
        {
            args.Handled = ReferenceEquals(_player, player) &&
                (_infoListener?.OnInfo(player, args.What, args.Extra) ?? false);
        };
        try
        {
            _surface = new Surface(_texture);
            player.SetSurface(_surface);
            player.SetDataSource(_path);
            player.PrepareAsync();
        }
        catch (Java.Lang.Exception)
        {
            _errorListener?.OnError(player, MediaError.Unknown, 0);
        }
    }

    private void FitVideo()
    {
        if (Width <= 0 || Height <= 0 || _videoWidth <= 0 || _videoHeight <= 0) return;
        float ratio = ((float)_videoWidth / _videoHeight) / ((float)Width / Height);
        using var matrix = new Matrix();
        matrix.SetScale(Math.Min(1f, ratio), Math.Min(1f, 1f / ratio), Width / 2f, Height / 2f);
        SetTransform(matrix);
    }

    private void ReleasePlayer()
    {
        MediaPlayer? player = _player;
        _player = null;
        _prepared = false;
        if (player is not null)
        {
            player.Release();
            player.Dispose();
        }
        _surface?.Dispose();
        _surface = null;
    }

    public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
    {
        _texture = surface;
        OpenVideo();
    }

    public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) => FitVideo();
    public void OnSurfaceTextureUpdated(SurfaceTexture surface) { }

    public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
    {
        if (_prepared && _player is not null)
        {
            try { _seekWhenReady = _player.CurrentPosition; }
            catch (Java.Lang.IllegalStateException) { _seekWhenReady = -1; }
            catch (ObjectDisposedException) { _seekWhenReady = -1; }
        }
        _texture = null;
        ReleasePlayer();
        SurfaceLost?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
