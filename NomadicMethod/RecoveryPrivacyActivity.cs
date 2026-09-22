using Android.Content;

namespace NomadicMethod;

[Activity(Name = "com.local.nomadicmethod.RecoveryPrivacyActivity", Exported = true,
    Permission = "android.permission.START_VIEW_PERMISSION_USAGE")]
[IntentFilter([Intent.ActionViewPermissionUsage], Categories = ["android.intent.category.HEALTH_PERMISSIONS"])]
public sealed class RecoveryPrivacyActivity : Activity
{
    protected override void OnCreate(Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        int padding = (int)(24 * Resources!.DisplayMetrics!.Density);
        content.SetPadding(padding, padding, padding, padding);
        string appName = GetString(Resource.String.app_name);
        var text = new TextView(this)
        {
            TextSize = 18,
            Text = $"Health data in {appName}\n\n{appName} automatically uses available Oura sleep, heart rate and HRV from Health Connect to guide Light mode. Android controls access. {appName} does not write health records or use Oura composite scores.\n\nReadings stay on this device. Only nightly summaries and a limited decision log are saved in private storage, excluded from backups. Nothing is uploaded. Workout history records whether Light was required, without health readings.\n\nYou can revoke access in Health Connect. On its next foreground return, {appName} removes its cached health data without changing your workouts.\n\nMissing, stale or unclear data leaves the existing workout countdown in charge. These training rules are not a medical assessment.",
        };
        content.AddView(text);
        var close = new Button(this) { Text = "Close" };
        close.Click += (_, _) => Finish();
        content.AddView(close);
        var scroll = new ScrollView(this);
        scroll.AddView(content);
        SetContentView(scroll);
    }
}
