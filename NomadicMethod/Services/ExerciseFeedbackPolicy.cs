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

    public static double Threshold(double width) => Math.Clamp(width * .23, 56, 88);
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
