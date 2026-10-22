using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using NomadicMethod.Services;

namespace NomadicMethod;

public sealed class FitnessProfileView : View
{
    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private readonly Android.Graphics.Path _path = new();
    private double[] _levels = new double[10];

    public FitnessProfileView(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
        ImportantForAccessibility = ImportantForAccessibility.Yes;
        SetLevels(_levels);
    }

    public void SetLevels(double[] levels)
    {
        _levels = levels.Select(level => (double)FitnessProfilePolicy.RoundLevel(level)).ToArray();
        ContentDescription = "Fitness profile from hearted exercises. " +
            string.Join("; ", FitnessProfilePolicy.Labels.Select((label, index) =>
                string.Join(" ", label) + ": " + (_levels[index] > 0
                    ? _levels[index].ToString("0") + " of 5"
                    : "no qualifying hearted exercise")));
        Invalidate();
    }

    private float Dp(float value) => value * Resources!.DisplayMetrics!.Density;
    private float TextSize => TypedValue.ApplyDimension(ComplexUnitType.Sp, 12f, Resources!.DisplayMetrics);

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width = ResolveSize((int)Dp(520), widthMeasureSpec);
        float expertRadius = ExpertRadius(width);
        int height = (int)Math.Ceiling(Math.Max(Dp(300), (expertRadius + LabelGap) * 2 + TextSize * 3.75f));
        SetMeasuredDimension(width, ResolveSize(height, heightMeasureSpec));
    }

    private float LabelGap => Math.Max(Dp(12), TextSize * 0.75f);

    private float ExpertRadius(int width)
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
        float cx = Width / 2f, cy = Height / 2f;
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
        _paint.Color = graphite;
        _paint.TextSize = TextSize;
        for (int index = 0; index < 10; index++)
        {
            string[] lines = FitnessProfilePolicy.Labels[index];
            double horizontal = Math.Cos(index * Math.PI / 5 - Math.PI / 2);
            _paint.TextAlign = Math.Abs(horizontal) < 0.01 ? Paint.Align.Center :
                horizontal > 0 ? Paint.Align.Left : Paint.Align.Right;
            var p = Point(index, expertRadius + LabelGap);
            float lineHeight = TextSize * 1.25f;
            float baseline = p.Y - (lines.Length - 1) * lineHeight / 2 -
                (_paint.Ascent() + _paint.Descent()) / 2;
            for (int line = 0; line < lines.Length; line++)
                canvas.DrawText(lines[line], p.X, baseline + line * lineHeight, _paint);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _paint.Dispose(); _path.Dispose(); }
        base.Dispose(disposing);
    }
}
