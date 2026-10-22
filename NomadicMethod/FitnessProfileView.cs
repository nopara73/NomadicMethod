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
        _levels = levels.ToArray();
        ContentDescription = "Fitness profile from hearted exercises. " +
            string.Join("; ", FitnessProfilePolicy.Labels.Select((label, index) =>
                string.Join(" ", label) + ": " + (levels[index] > 0
                    ? levels[index].ToString("0.##") + " of 5"
                    : "no qualifying hearted exercise")));
        Invalidate();
    }

    private float Dp(float value) => value * Resources!.DisplayMetrics!.Density;
    private float TextSize => TypedValue.ApplyDimension(ComplexUnitType.Sp, 12f, Resources!.DisplayMetrics);

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int width = ResolveSize((int)Dp(420), widthMeasureSpec);
        float radius = Math.Max(Dp(24), Math.Min(Dp(112), (width - 2 * LabelRoom) / 2));
        int height = (int)Math.Ceiling(Math.Max(Dp(300), (radius + TextSize * 2.7f) * 2 + TextSize * 3.75f));
        SetMeasuredDimension(width, ResolveSize(height, heightMeasureSpec));
    }

    private float LabelRoom => Math.Max(Dp(76), TextSize * 6.6f);

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float radius = Math.Max(Dp(24), Math.Min(Dp(112), (Width - 2 * LabelRoom) / 2));
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
                var p = Point(index, radius * values[index] / 5);
                if (index == 0) _path.MoveTo(p.X, p.Y);
                else _path.LineTo(p.X, p.Y);
            }
            _path.Close();
        }
        _paint.Color = new Color(Context!.GetColor(Resource.Color.surface_outline_soft));
        _paint.SetStyle(Paint.Style.Stroke);
        _paint.StrokeWidth = Dp(1);
        for (int level = 1; level <= 5; level++)
        {
            Polygon(Enumerable.Repeat((double)level, 10).ToArray());
            canvas.DrawPath(_path, _paint);
        }
        for (int index = 0; index < 10; index++)
        {
            var p = Point(index, radius);
            canvas.DrawLine(cx, cy, p.X, p.Y, _paint);
        }
        Polygon(_levels);
        _paint.Color = new Color(Context!.GetColor(Resource.Color.brand_chartreuse));
        _paint.Alpha = 44;
        _paint.SetStyle(Paint.Style.Fill);
        canvas.DrawPath(_path, _paint);
        _paint.Color = new Color(Context!.GetColor(Resource.Color.brand_graphite));
        _paint.Alpha = 255;
        _paint.SetStyle(Paint.Style.Stroke);
        _paint.StrokeWidth = Dp(2);
        canvas.DrawPath(_path, _paint);
        _paint.SetStyle(Paint.Style.Fill);
        for (int index = 0; index < 10; index++)
        {
            var p = Point(index, radius * _levels[index] / 5);
            if (_levels[index] > 0) canvas.DrawCircle(p.X, p.Y, Dp(3), _paint);
        }
        _paint.TextSize = TextSize;
        _paint.TextAlign = Paint.Align.Center;
        for (int index = 0; index < 10; index++)
        {
            string[] lines = FitnessProfilePolicy.Labels[index];
            var p = Point(index, radius + TextSize * 2.7f);
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
