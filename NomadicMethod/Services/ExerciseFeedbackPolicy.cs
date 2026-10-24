using NomadicMethod.Models;

namespace NomadicMethod.Services;

public static class ExerciseFeedbackPolicy
{
    public static bool IsResponse(ExerciseOutcome value) => value is
        ExerciseOutcome.X or ExerciseOutcome.Tick or ExerciseOutcome.TooEasy or ExerciseOutcome.TooHard;

    // Re-evaluate the displacement, rather than locking the first direction.
    // A small axis hysteresis prevents flicker around the diagonals; the centre
    // always releases it, including when the user crosses to the opposite side.
    public static ExerciseOutcome? Direction(double x, double y, ExerciseOutcome? previous = null)
    {
        double ax = Math.Abs(x), ay = Math.Abs(y);
        if (Math.Max(ax, ay) < 12) return null;
        bool horizontal = previous is ExerciseOutcome.X or ExerciseOutcome.Tick;
        bool vertical = previous is ExerciseOutcome.TooEasy or ExerciseOutcome.TooHard;
        if (horizontal && ay <= ax * 1.2 || ax > ay * 1.2)
            return x >= 0 ? ExerciseOutcome.Tick : ExerciseOutcome.X;
        if (vertical && ax <= ay * 1.2 || ay > ax * 1.2)
            return y >= 0 ? ExerciseOutcome.TooHard : ExerciseOutcome.TooEasy;
        return null;
    }

    public static double Threshold(double width) => width * .36;
    public static (double Travel, double Size, double Others) Visual(double progress, double shortEdge)
    {
        double p = Math.Clamp(progress, 0, 1);
        double travel = p * p * (3 - 2 * p);
        return (travel, 248 * (.2 + .84 * travel), 0);
    }
    public const int AutomaticMilliseconds = 1270, StampMilliseconds = 1700, RevealMilliseconds = 420;
    public const int ReducedMilliseconds = 900, ReducedRevealMilliseconds = 180;
    private static double Ease(double p) => 1 - Math.Pow(1 - Math.Clamp(p, 0, 1), 3);
    private static double Mix(double a, double b, double p) => a + (b - a) * p;
    public static double AutomaticMehX(double t, double width)
    {
        if (t < 240) return 13 * Math.Sin(Math.Clamp(t / 240, 0, 1) * Math.PI);
        double p = Math.Clamp((t - 240) / 850, 0, 1);
        return -width * .39 * p * p * (3 - 2 * p);
    }
    // Ported from the approved Stamp preview. Keep phase times absolute so
    // a global easing curve cannot shorten the impact or confirmation hold.
    public static StampPose Stamp(double t, double width, double initialX = 0, double initialY = 0, double initialScale = 1.04)
    {
        double basis = Math.Clamp(width / 360, .78, 1);
        double x = 0, y = 0, sx = basis, sy = basis, rotation = -7;
        if (t < 270)
        {
            double p = Ease(t / 270); x = Mix(initialX, 0, p); y = Mix(initialY, -25, p);
            sx = sy = Mix(initialScale, 1.32 * basis, p); rotation = -12 * p;
        }
        else if (t < 390)
        {
            double p = (t - 270) / 120; y = Mix(-25, 12, p * p);
            sx = Mix(1.32, 1.12, p) * basis; sy = Mix(1.32, .88, p) * basis; rotation = Mix(-12, -7, p);
        }
        else if (t < 610)
        {
            double p = Ease((t - 390) / 220); y = Mix(12, 0, p);
            sx = Mix(1.12, 1, p) * basis; sy = Mix(.88, 1, p) * basis;
        }
        double j = Math.Clamp((t - 390) / 330, 0, 1), settled = Ease(t / 440);
        return new(x, y, sx, sy, rotation, settled, 1 - .055 * settled,
            t >= 390 && t < 560 ? Math.Sin((t - 390) / 170 * Math.PI * 2) * 6 * (1 - (t - 390) / 170) : 0,
            t < 390 ? 0 : Math.Sin(Math.PI * j) * .8, Mix(.68, 1.1, j), .12 * Ease(t / 400), Ease((t - 620) / 150));
    }
    public readonly record struct StampPose(double X, double Y, double Sx, double Sy, double Rotation,
        double Settled, double SheetScale, double Shake, double Impact, double ImpactScale, double Wash, double Receipt);
    public static double Distance(double x, double y, ExerciseOutcome? direction) => direction switch
    {
        ExerciseOutcome.X => -x,
        ExerciseOutcome.Tick => x,
        ExerciseOutcome.TooEasy => -y,
        ExerciseOutcome.TooHard => y,
        _ => 0,
    };
    public static string Symbol(ExerciseOutcome value) => value switch
    {
        ExerciseOutcome.Tick => "❤️", ExerciseOutcome.X => "💩",
        ExerciseOutcome.TooEasy => "🥱", _ => "💀",
    };
    public static string Description(ExerciseOutcome value) => value switch
    {
        ExerciseOutcome.Tick => "Love it", ExerciseOutcome.X => "Meh",
        ExerciseOutcome.TooEasy => "Too easy", _ => "Too hard",
    };
}
