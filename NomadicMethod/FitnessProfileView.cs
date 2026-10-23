using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using NomadicMethod.Services;

namespace NomadicMethod;

public sealed class FitnessProfileView : FrameLayout
{
    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private readonly Android.Graphics.Path _path = new();
    private double[] _levels = new double[10];
    private FitnessProfileAxis[] _profile = Enumerable.Range(0, 10)
        .Select(_ => new FitnessProfileAxis(0, [])).ToArray();
    private readonly Button[] _buttons = new Button[10];
    private AlertDialog? _dialog;
    private bool _stacked;
    private float _chartHeight;
    private int _highlighted = -1;
    private int _touchAxis = -1;
    private float _touchX, _touchY;

    public FitnessProfileView(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
        SetWillNotDraw(false);
        ImportantForAccessibility = ImportantForAccessibility.No;
        Clickable = true;
        Focusable = false;
        for (int index = 0; index < 10; index++)
        {
            int axis = index;
            var button = new Button(context)
            {
                Id = GenerateViewId(), Gravity = GravityFlags.Center,
                Typeface = Typeface.Default,
                Background = AxisBackground(),
            };
            button.SetAllCaps(false);
            button.SetIncludeFontPadding(false);
            button.SetTextSize(ComplexUnitType.Sp, 12f);
            button.SetTextColor(Graphite);
            button.SetPadding((int)Dp(4), (int)Dp(4), (int)Dp(4), (int)Dp(4));
            button.SetMinWidth(0);
            button.SetMinHeight(0);
            button.Click += (_, _) => OpenDetails(axis);
            button.FocusChange += (_, args) => { _highlighted = args.HasFocus ? axis : -1; Invalidate(); };
            _buttons[index] = button;
            AddView(button);
        }
        SetProfile(_profile);
        // Canonical ratings and saved Keeps arrive together after startup.
        foreach (Button button in _buttons) button.Enabled = false;
    }

    private Color Graphite => new(Context!.GetColor(Resource.Color.brand_graphite));
    private Color Green => new(Context!.GetColor(Resource.Color.brand_chartreuse));
    private Color Bone => new(Context!.GetColor(Resource.Color.brand_bone));

    private StateListDrawable AxisBackground()
    {
        var states = new StateListDrawable();
        foreach (int state in new[] { Android.Resource.Attribute.StateFocused, Android.Resource.Attribute.StatePressed })
        {
            var shape = new GradientDrawable();
            shape.SetColor(Green);
            shape.SetCornerRadius(Dp(8));
            shape.SetStroke((int)Dp(1), Graphite);
            states.AddState([state], shape);
        }
        states.AddState([], new ColorDrawable(Color.Transparent));
        return states;
    }

    public void SetProfile(FitnessProfileAxis[] profile)
    {
        _profile = profile;
        _levels = profile.Select(axis => (double)FitnessProfilePolicy.RoundLevel(axis.Mean)).ToArray();
        for (int index = 0; index < 10; index++)
        {
            _buttons[index].Enabled = true;
            _buttons[index].ContentDescription = string.Join(" ", FitnessProfilePolicy.Labels[index]) + ": " +
                (_levels[index] > 0 ? "level " + _levels[index].ToString("0") + " of 5" : "no evidence yet") +
                ". Show favourite movements";
            _buttons[index].SetTextColor(_levels[index] > 0 ? Graphite
                : new Color(Context!.GetColor(Resource.Color.secondary_text)));
        }
        RequestLayout();
        Invalidate();
    }

    private float Dp(float value) => value * Resources!.DisplayMetrics!.Density;
    private float TextSize => TypedValue.ApplyDimension(ComplexUnitType.Sp, 12f, Resources!.DisplayMetrics);

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width = ResolveSize((int)Dp(520), widthMeasureSpec);
        _stacked = NeedsStackedLabels(width, FittedRadius(width));
        float expertRadius = ExpertRadius(width);
        _chartHeight = _stacked ? expertRadius * 2 + Dp(32)
            : Math.Max(Dp(300), (expertRadius + LabelGap) * 2 + TextSize * 3.75f);
        float rowHeight = Math.Max(Dp(48), TextSize * 2.5f + Dp(8));
        int height = (int)Math.Ceiling(_chartHeight + (_stacked ? rowHeight * 5 : 0));
        SetMeasuredDimension(width, ResolveSize(height, heightMeasureSpec));
        for (int index = 0; index < 10; index++)
        {
            var button = _buttons[index];
            string label = string.Join(_stacked ? " " : "\n", FitnessProfilePolicy.Labels[index]);
            if (button.Text != label) button.Text = label;
            int buttonWidth = _stacked ? width / 2 : (int)Math.Ceiling(Math.Max(Dp(48),
                FitnessProfilePolicy.Labels[index].Max(line => _paint.MeasureText(line)) + Dp(8)));
            int buttonHeight = (int)Math.Ceiling(_stacked ? rowHeight : Math.Max(Dp(48),
                TextSize * 1.25f * FitnessProfilePolicy.Labels[index].Length + Dp(8)));
            button.Measure(MeasureSpec.MakeMeasureSpec(buttonWidth, MeasureSpecMode.Exactly),
                MeasureSpec.MakeMeasureSpec(buttonHeight, MeasureSpecMode.Exactly));
        }
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        float radius = ExpertRadius(Width) + LabelGap;
        for (int index = 0; index < 10; index++)
        {
            var button = _buttons[index];
            double angle = index * Math.PI / 5 - Math.PI / 2;
            double horizontal = Math.Cos(angle);
            float x = Width / 2f + (float)horizontal * radius;
            float y = _chartHeight / 2 + (float)Math.Sin(angle) * radius;
            int buttonLeft = _stacked ? (index % 2) * (Width / 2) : (int)Math.Clamp(
                Math.Abs(horizontal) < 0.01 ? x - button.MeasuredWidth / 2f
                : horizontal > 0 ? x - Dp(4) : x - button.MeasuredWidth + Dp(4), 0, Width - button.MeasuredWidth);
            int buttonTop = _stacked ? (int)_chartHeight + index / 2 * button.MeasuredHeight
                : (int)(y - button.MeasuredHeight / 2f);
            button.Layout(buttonLeft, buttonTop, buttonLeft + button.MeasuredWidth, buttonTop + button.MeasuredHeight);
        }
    }

    private float LabelGap => Math.Max(Dp(12), TextSize * 0.75f);

    private bool NeedsStackedLabels(int width, float expertRadius)
    {
        if (expertRadius < Dp(40)) return true;
        var bounds = new List<(float Left, float Top, float Right, float Bottom)>();
        for (int index = 0; index < 10; index++)
        {
            string[] lines = FitnessProfilePolicy.Labels[index];
            double angle = index * Math.PI / 5 - Math.PI / 2;
            double horizontal = Math.Cos(angle);
            float x = width / 2f + (float)horizontal * (expertRadius + LabelGap);
            float w = Math.Max(Dp(48), lines.Max(line => _paint.MeasureText(line)) + Dp(8));
            if (w > width) return true;
            float h = Math.Max(Dp(48), TextSize * 1.25f * lines.Length + Dp(8));
            float left = Math.Clamp(Math.Abs(horizontal) < 0.01 ? x - w / 2
                : horizontal > 0 ? x - Dp(4) : x - w + Dp(4), 0, width - w);
            float top = (float)Math.Sin(angle) * (expertRadius + LabelGap) - h / 2;
            if (bounds.Any(b => left < b.Right && b.Left < left + w && top < b.Bottom && b.Top < top + h)) return true;
            bounds.Add((left, top, left + w, top + h));
        }
        return false;
    }

    private float ExpertRadius(int width) => _stacked ? Math.Min(Dp(120), width / 2f - Dp(24)) : FittedRadius(width);

    private float FittedRadius(int width)
    {
        _paint.TextSize = TextSize;
        double radius = Dp(180);
        for (int index = 0; index < 10; index++)
        {
            double horizontal = Math.Abs(Math.Cos(index * Math.PI / 5 - Math.PI / 2));
            if (horizontal < 0.01) continue;
            float labelWidth = FitnessProfilePolicy.Labels[index].Max(line => _paint.MeasureText(line));
            radius = Math.Min(radius, (width / 2f - labelWidth - Dp(2)) / horizontal - LabelGap);
        }
        return (float)Math.Max(Dp(12), radius);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float expertRadius = ExpertRadius(Width);
        float cx = Width / 2f, cy = _chartHeight / 2f;
        (float X, float Y) Point(int index, double distance)
        {
            double angle = index * Math.PI / 5 - Math.PI / 2;
            return (cx + (float)(Math.Cos(angle) * distance),
                cy + (float)(Math.Sin(angle) * distance));
        }
        void Polygon(IReadOnlyList<double> values)
        {
            _path.Reset();
            for (int index = 0; index < 10; index++)
            {
                var p = Point(index, expertRadius * FitnessProfilePolicy.ChartRadiusFraction(values[index]));
                if (index == 0) _path.MoveTo(p.X, p.Y);
                else _path.LineTo(p.X, p.Y);
            }
            _path.Close();
        }
        void Web(Color color, int alpha)
        {
            _paint.Color = color;
            _paint.Alpha = alpha;
            _paint.SetStyle(Paint.Style.Stroke);
            _paint.StrokeWidth = Dp(1);
            for (int level = 1; level <= FitnessProfilePolicy.WebBoundaryLevel; level++)
            {
                Polygon(Enumerable.Repeat((double)level, 10).ToArray());
                canvas.DrawPath(_path, _paint);
            }
            for (int index = 0; index < 10; index++)
            {
                var p = Point(index, expertRadius * FitnessProfilePolicy.ChartRadiusFraction(FitnessProfilePolicy.WebBoundaryLevel));
                canvas.DrawLine(cx, cy, p.X, p.Y, _paint);
            }
        }
        Color graphite = new(Context!.GetColor(Resource.Color.brand_graphite));
        Color green = new(Context.GetColor(Resource.Color.brand_chartreuse));
        Web(graphite, 38);
        Polygon(_levels);
        _paint.Color = graphite;
        _paint.Alpha = 255;
        _paint.SetStyle(Paint.Style.Fill);
        canvas.DrawPath(_path, _paint);
        canvas.Save();
        canvas.ClipPath(_path);
        Web(green, 51);
        canvas.Restore();
        Polygon(_levels);
        _paint.Color = green;
        _paint.Alpha = 255;
        _paint.SetStyle(Paint.Style.Stroke);
        _paint.StrokeWidth = Dp(2);
        _paint.StrokeJoin = Paint.Join.Round;
        canvas.DrawPath(_path, _paint);
        _paint.SetStyle(Paint.Style.Fill);
        for (int index = 0; index < 10; index++)
        {
            var p = Point(index, expertRadius * FitnessProfilePolicy.ChartRadiusFraction(_levels[index]));
            if (_levels[index] <= 0) continue;
            _paint.Color = graphite;
            canvas.DrawCircle(p.X, p.Y, Dp(4.25f), _paint);
            _paint.Color = green;
            canvas.DrawCircle(p.X, p.Y, Dp(3.5f), _paint);
        }
        for (int index = 0; index < 10; index++)
        {
            bool unknown = _levels[index] <= 0;
            var p = Point(index, expertRadius * FitnessProfilePolicy.ChartRadiusFraction(FitnessProfilePolicy.WebBoundaryLevel));
            _paint.Color = graphite;
            _paint.Alpha = index == _highlighted ? 204 : 102;
            _paint.SetStyle(Paint.Style.Stroke);
            _paint.StrokeWidth = Dp(index == _highlighted ? 2 : 1);
            using var dash = new DashPathEffect([Dp(3), Dp(4)], 0);
            _paint.SetPathEffect(unknown ? dash : null);
            if (unknown || index == _highlighted) canvas.DrawLine(cx, cy, p.X, p.Y, _paint);
            _paint.SetPathEffect(null);
            if (!unknown) continue;
            _paint.Color = Bone;
            _paint.Alpha = 255;
            _paint.SetStyle(Paint.Style.Fill);
            canvas.DrawCircle(p.X, p.Y, Dp(4), _paint);
            _paint.Color = graphite;
            _paint.Alpha = 140;
            _paint.SetStyle(Paint.Style.Stroke);
            _paint.StrokeWidth = Dp(1.5f);
            canvas.DrawCircle(p.X, p.Y, Dp(4), _paint);
        }
    }

    public override bool OnTouchEvent(Android.Views.MotionEvent? e)
    {
        if (e is null || !_buttons[0].Enabled) return false;
        if (e.Action == MotionEventActions.Down)
        {
            float x = e.GetX() - Width / 2f, y = e.GetY() - _chartHeight / 2;
            double distance = Math.Sqrt(x * x + y * y);
            if (distance < Dp(18) || distance > ExpertRadius(Width) + Dp(12)) return false;
            double axis = (Math.Atan2(y, x) + Math.PI / 2) / (Math.PI / 5);
            _touchAxis = ((int)Math.Round(axis) + 10) % 10;
            _touchX = e.GetX(); _touchY = e.GetY();
            return true;
        }
        if (e.Action == MotionEventActions.Up && _touchAxis >= 0)
        {
            if (Math.Abs(e.GetX() - _touchX) < Dp(8) && Math.Abs(e.GetY() - _touchY) < Dp(8)) PerformClick();
            _touchAxis = -1;
            return true;
        }
        if (e.Action == MotionEventActions.Cancel) _touchAxis = -1;
        return _touchAxis >= 0;
    }

    public override bool PerformClick()
    {
        base.PerformClick();
        if (_touchAxis >= 0) OpenDetails(_touchAxis);
        return true;
    }

    private void OpenDetails(int index)
    {
        if (_dialog is not null || !_buttons[index].Enabled) return;
        FitnessProfileAxis axis = _profile[index];
        TextView Text(string value, float size, Color color)
        {
            var text = new TextView(Context) { Text = value };
            text.SetTextSize(ComplexUnitType.Sp, size);
            text.SetTextColor(color);
            text.SetPadding(0, (int)Dp(8), 0, (int)Dp(8));
            return text;
        }
        var title = Text(string.Join(" ", FitnessProfilePolicy.Labels[index]), 20, Bone);
        title.SetPadding((int)Dp(24), (int)Dp(20), (int)Dp(24), (int)Dp(8));
        var content = new LinearLayout(Context) { Orientation = Orientation.Vertical };
        content.SetPadding((int)Dp(24), 0, (int)Dp(24), (int)Dp(16));
        content.AddView(Text(axis.Contributions.Count > 0 ? "level " + _levels[index].ToString("0") + " of 5"
            : "No evidence yet", 24, Green));
        Color secondary = new(195, 194, 187);
        content.AddView(Text(axis.Contributions.Count > 0
            ? "Mean " + FitnessProfilePolicy.FormatMean(axis.Mean) + " · " + axis.Contributions.Count + " favourite movement" +
                (axis.Contributions.Count == 1 ? "" : "s")
            : "No current favourite has a qualifying rating for this axis.", 14, secondary));
        foreach (FitnessContribution entry in axis.Contributions)
        {
            var row = new LinearLayout(Context) { Orientation = Orientation.Horizontal };
            row.SetGravity(GravityFlags.CenterVertical);
            var name = Text(entry.Name, 16, Bone);
            row.AddView(name, new LinearLayout.LayoutParams(0, LayoutParams.WrapContent, 1));
            var level = Text(entry.Level.ToString(), 16, Green);
            level.Gravity = GravityFlags.Center;
            level.ContentDescription = "level " + entry.Level + " of 5";
            var badge = new GradientDrawable();
            badge.SetColor(new Color(38, 42, 45)); badge.SetCornerRadius(Dp(24));
            level.Background = badge;
            row.AddView(level, new LinearLayout.LayoutParams((int)Dp(48), LayoutParams.WrapContent) { LeftMargin = (int)Dp(16) });
            content.AddView(row);
        }
        content.AddView(Text("From precise strongest ratings of favourites. NM estimates, not measured ability.", 14, secondary));
        var scroll = new ScrollView(Context);
        scroll.AddView(content);
        var builder = new AlertDialog.Builder(Context!);
        builder.SetCustomTitle(title);
        builder.SetView(scroll);
        builder.SetPositiveButton("Close", (_, _) => { });
        var dialog = builder.Create()!;
        dialog.DismissEvent += (_, _) => { _dialog = null; _buttons[index].RequestFocus(); };
        _dialog = dialog;
        dialog.Show();
        dialog.Window?.SetBackgroundDrawable(new ColorDrawable(Graphite));
        dialog.GetButton((int)DialogButtonType.Positive)?.SetTextColor(Green);
    }

    protected override void OnDetachedFromWindow()
    {
        _dialog?.Dismiss();
        base.OnDetachedFromWindow();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _paint.Dispose(); _path.Dispose(); }
        base.Dispose(disposing);
    }
}
