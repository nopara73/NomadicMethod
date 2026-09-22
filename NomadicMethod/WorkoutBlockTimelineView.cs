using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using NomadicMethod.Services;

namespace NomadicMethod;

public sealed class WorkoutBlockTimelineView : View
{
    private const float MinimumWidthDp = 42f;
    private const float MaximumWidthDp = 166f;
    private const float HeightDp = 52f;
    private const float TrackTopDp = 12f;
    private const float TrackHeightDp = 32f;
    private const float BorderDp = 3f;
    private const float InnerPaddingDp = 3f;
    private const float PreferredGapDp = 3f;
    private const float SetGapDp = 5f;
    private const float SegmentWidthDp = 21f;

    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private readonly Android.Graphics.Path _playheadPath = new();
    private readonly int _graphiteColor;
    private readonly int _surfaceColor;
    private readonly int _blueColor;
    private readonly int _redColor;
    private readonly int _neutralColor;
    private WorkoutBlockAccent[] _blocks = [WorkoutBlockAccent.Neutral];
    private int[] _setStartBlockIndices = [0];
    private int _currentBlockIndex;

    public WorkoutBlockTimelineView(Context context)
        : this(context, null)
    {
    }

    public WorkoutBlockTimelineView(Context context, IAttributeSet? attrs)
        : base(context, attrs)
    {
        _graphiteColor = context.GetColor(Resource.Color.brand_graphite);
        _surfaceColor = context.GetColor(Resource.Color.surface);
        _blueColor = context.GetColor(Resource.Color.rest_accent);
        _redColor = context.GetColor(Resource.Color.move_accent);
        _neutralColor = context.GetColor(Resource.Color.brand_chartreuse);
        ImportantForAccessibility = ImportantForAccessibility.Yes;
    }

    public void SetTimeline(
        IReadOnlyList<WorkoutBlockAccent> blocks,
        IReadOnlyList<int> setStartBlockIndices,
        int currentBlockIndex)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(setStartBlockIndices);
        if (blocks.Count == 0)
        {
            throw new ArgumentException(
                "An execution timeline requires at least one work block.",
                nameof(blocks));
        }
        if (currentBlockIndex < 0 || currentBlockIndex >= blocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(currentBlockIndex));
        }
        if (setStartBlockIndices.Count == 0 ||
            setStartBlockIndices[0] != 0 ||
            setStartBlockIndices.Any(index => index < 0 || index >= blocks.Count) ||
            !setStartBlockIndices.SequenceEqual(
                setStartBlockIndices.Distinct().Order()))
        {
            throw new ArgumentException(
                "Set boundaries must be unique ascending block indices starting at zero.",
                nameof(setStartBlockIndices));
        }

        _blocks = blocks.ToArray();
        _setStartBlockIndices = setStartBlockIndices.ToArray();
        _currentBlockIndex = currentBlockIndex;
        RequestLayout();
        Invalidate();
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        int setCount = _setStartBlockIndices.Length;
        float desiredWidthDp = Math.Clamp(
            setCount * (2f * (BorderDp + InnerPaddingDp)) +
                _blocks.Length * SegmentWidthDp +
                (_blocks.Length - setCount) * PreferredGapDp +
                (setCount - 1) * SetGapDp,
            MinimumWidthDp,
            MaximumWidthDp);
        SetMeasuredDimension(
            ResolveSize(Dp(desiredWidthDp), widthMeasureSpec),
            ResolveSize(Dp(HeightDp), heightMeasureSpec));
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        float trackTop = Dp(TrackTopDp);
        float trackBottom = trackTop + Dp(TrackHeightDp);
        float border = Dp(BorderDp);
        float innerPadding = Dp(InnerPaddingDp);
        float cornerRadius = Dp(11f);
        int setCount = _setStartBlockIndices.Length;
        float setGap = setCount == 1 ? 0f : Dp(SetGapDp);
        float blockGap = Dp(PreferredGapDp);
        float frameOverhead = 2f * (border + innerPadding);
        float availableForSegments = Width -
            setGap * (setCount - 1) -
            frameOverhead * setCount -
            blockGap * (_blocks.Length - setCount);
        float segmentWidth = Math.Max(1f, availableForSegments / _blocks.Length);
        float segmentRadius = Math.Min(Dp(4f), segmentWidth / 3f);
        float frameLeft = 0f;
        float currentCenter = 0f;

        for (int setIndex = 0; setIndex < setCount; setIndex++)
        {
            int startIndex = _setStartBlockIndices[setIndex];
            int endIndex = setIndex + 1 < setCount
                ? _setStartBlockIndices[setIndex + 1]
                : _blocks.Length;
            int blockCount = endIndex - startIndex;
            float frameWidth = frameOverhead +
                blockCount * segmentWidth +
                (blockCount - 1) * blockGap;
            var outer = new RectF(
                frameLeft,
                trackTop,
                frameLeft + frameWidth,
                trackBottom);
            _paint.Color = new Color(_graphiteColor);
            canvas.DrawRoundRect(outer, cornerRadius, cornerRadius, _paint);

            var inner = new RectF(
                outer.Left + border,
                outer.Top + border,
                outer.Right - border,
                outer.Bottom - border);
            _paint.Color = new Color(_surfaceColor);
            canvas.DrawRoundRect(
                inner,
                Math.Max(0f, cornerRadius - border),
                Math.Max(0f, cornerRadius - border),
                _paint);

            float contentLeft = inner.Left + innerPadding;
            float contentTop = inner.Top + innerPadding;
            float contentBottom = inner.Bottom - innerPadding;
            for (int index = startIndex; index < endIndex; index++)
            {
                float left = contentLeft +
                    (index - startIndex) * (segmentWidth + blockGap);
                var segment = new RectF(
                    left,
                    contentTop,
                    left + segmentWidth,
                    contentBottom);
                _paint.Color = new Color(GetBlockColor(_blocks[index]));
                canvas.DrawRoundRect(
                    segment,
                    segmentRadius,
                    segmentRadius,
                    _paint);
                if (index == _currentBlockIndex)
                {
                    currentCenter = segment.CenterX();
                }
            }

            frameLeft += frameWidth + setGap;
        }

        float markerTop = trackTop - Dp(10f);
        float markerBottom = trackTop - Dp(3f);
        float markerHalfWidth = Dp(5f);
        _playheadPath.Reset();
        _playheadPath.MoveTo(currentCenter - markerHalfWidth, markerTop);
        _playheadPath.LineTo(currentCenter + markerHalfWidth, markerTop);
        _playheadPath.LineTo(currentCenter, markerBottom);
        _playheadPath.Close();
        _paint.Color = new Color(_graphiteColor);
        canvas.DrawPath(_playheadPath, _paint);
    }

    private int GetBlockColor(WorkoutBlockAccent accent) => accent switch
    {
        WorkoutBlockAccent.Blue => _blueColor,
        WorkoutBlockAccent.Red => _redColor,
        WorkoutBlockAccent.Neutral => _neutralColor,
        _ => throw new ArgumentOutOfRangeException(nameof(accent), accent, null),
    };

    private int Dp(float value) =>
        (int)MathF.Round(value * Resources!.DisplayMetrics!.Density);
}
