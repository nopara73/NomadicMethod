using Android.App;
using Android.Graphics;
using Android.Views;
using System.Runtime.Versioning;

namespace NomadicMethod;

/// <summary>
/// Keeps edge-to-edge content clear of system bars while coordinating system-bar icon colors.
/// </summary>
public sealed class SystemBarsController : Java.Lang.Object, View.IOnApplyWindowInsetsListener
{
    private static readonly Color LegacyLightScreenNavigationColor = Color.Rgb(18, 20, 22);

    private readonly Window _window;
    private readonly View _contentView;
    private readonly int _basePaddingLeft;
    private readonly int _basePaddingTop;
    private readonly int _basePaddingRight;
    private readonly int _basePaddingBottom;

    /// <summary>
    /// Creates a controller and begins applying safe system-bar padding to <paramref name="contentView" />.
    /// </summary>
    public SystemBarsController(Activity activity, View contentView)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(contentView);

        _window = activity.Window
            ?? throw new InvalidOperationException("The activity does not have a window.");
        _contentView = contentView;
        _basePaddingLeft = contentView.PaddingLeft;
        _basePaddingTop = contentView.PaddingTop;
        _basePaddingRight = contentView.PaddingRight;
        _basePaddingBottom = contentView.PaddingBottom;

        ConfigureEdgeToEdge();
        _contentView.SetOnApplyWindowInsetsListener(this);
        UseLightScreen();
        _contentView.RequestApplyInsets();
    }

    /// <summary>
    /// Uses dark system-bar icons for a light app background.
    /// </summary>
    public void UseLightScreen() => SetAppearance(lightScreen: true);

    /// <summary>
    /// Uses light system-bar icons for a dark app background.
    /// </summary>
    public void UseDarkScreen() => SetAppearance(lightScreen: false);

    /// <summary>
    /// Selects system-bar icon and fallback background colors for the current screen.
    /// </summary>
    public void SetAppearance(bool lightScreen)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            SetModernAppearance(lightScreen);
        }
        else
        {
            SetLegacyAppearance(lightScreen);
        }

        SetFallbackBarColors(lightScreen);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _contentView.SetOnApplyWindowInsetsListener(null);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(insets);

        var safeInsets = GetSafeInsets(insets);
        view.SetPadding(
            _basePaddingLeft + safeInsets.Left,
            _basePaddingTop + safeInsets.Top,
            _basePaddingRight + safeInsets.Right,
            _basePaddingBottom + safeInsets.Bottom);

        return insets;
    }

    private void ConfigureEdgeToEdge()
    {
#pragma warning disable CA1422 // Clearing deprecated translucent flags keeps pre-Android 15 fallback colors reliable.
        _window.ClearFlags(
            WindowManagerFlags.TranslucentStatus | WindowManagerFlags.TranslucentNavigation);
#pragma warning restore CA1422
        _window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            // Target SDK 35+ is edge-to-edge by definition; this call is only needed
            // on Android 11-14, where the app still opts in explicitly.
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            {
                _window.SetDecorFitsSystemWindows(false);
            }

            return;
        }

#pragma warning disable CA1422 // Required fallback for Android 7-10, before WindowInsetsController existed.
        _window.DecorView.SystemUiFlags = SystemUiFlags.LayoutStable |
                                          SystemUiFlags.LayoutFullscreen |
                                          SystemUiFlags.LayoutHideNavigation;
#pragma warning restore CA1422
    }

    [SupportedOSPlatform("android30.0")]
    private void SetModernAppearance(bool lightScreen)
    {
        var insetsController = _window.InsetsController;
        if (insetsController is null)
        {
            return;
        }

        var lightBarMask = (int)(WindowInsetsControllerAppearance.LightStatusBars |
                                 WindowInsetsControllerAppearance.LightNavigationBars);
        insetsController.SetSystemBarsAppearance(lightScreen ? lightBarMask : 0, lightBarMask);
    }

    private void SetLegacyAppearance(bool lightScreen)
    {
#pragma warning disable CA1422 // Required fallback for Android 7-10, before WindowInsetsController existed.
        var flags = SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutFullscreen |
                    SystemUiFlags.LayoutHideNavigation;

        if (lightScreen)
        {
            flags |= SystemUiFlags.LightStatusBar;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                flags |= SystemUiFlags.LightNavigationBar;
            }
        }

        _window.DecorView.SystemUiFlags = flags;
#pragma warning restore CA1422
    }

    private void SetFallbackBarColors(bool lightScreen)
    {
        // Android 15+ enforces edge-to-edge and draws app content behind transparent bars.
        if (OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            return;
        }

#pragma warning disable CA1422 // These colors remain the platform fallback through Android 14.
        _window.SetStatusBarColor(Color.Transparent);
        _window.SetNavigationBarColor(lightScreen && !OperatingSystem.IsAndroidVersionAtLeast(26)
            ? LegacyLightScreenNavigationColor
            : Color.Transparent);

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            _window.StatusBarContrastEnforced = false;
            _window.NavigationBarContrastEnforced = false;
        }
#pragma warning restore CA1422
    }

    private static SafeInsets GetSafeInsets(WindowInsets insets)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var platformInsets = insets.GetInsets(
                WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            return new SafeInsets(
                platformInsets.Left,
                platformInsets.Top,
                platformInsets.Right,
                platformInsets.Bottom);
        }

#pragma warning disable CA1422 // Required fallback for Android 7-10, before typed insets existed.
        var left = insets.SystemWindowInsetLeft;
        var top = insets.SystemWindowInsetTop;
        var right = insets.SystemWindowInsetRight;
        var bottom = insets.SystemWindowInsetBottom;

        if (OperatingSystem.IsAndroidVersionAtLeast(28) && insets.DisplayCutout is { } cutout)
        {
            left = Math.Max(left, cutout.SafeInsetLeft);
            top = Math.Max(top, cutout.SafeInsetTop);
            right = Math.Max(right, cutout.SafeInsetRight);
            bottom = Math.Max(bottom, cutout.SafeInsetBottom);
        }
#pragma warning restore CA1422

        return new SafeInsets(left, top, right, bottom);
    }

    private readonly record struct SafeInsets(int Left, int Top, int Right, int Bottom);
}
