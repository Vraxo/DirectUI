// DirectUI/Source/Animation/ValueInterpolators.cs
using System.Numerics;
using DirectUI.Drawing;

namespace DirectUI.Animation;

internal static class ValueInterpolators
{
    // Generic Lerp selector
    public static T Lerp<T>(T from, T to, float progress)
    {
        if (from is float fFrom && to is float fTo)
        {
            return (T)(object)LerpFloat(fFrom, fTo, progress);
        }
        if (from is Color cFrom && to is Color cTo)
        {
            return (T)(object)LerpColor(cFrom, cTo, progress);
        }
        if (from is Vector2 v2From && to is Vector2 v2To)
        {
            return (T)(object)LerpVector2(v2From, v2To, progress);
        }
        // If no specific lerp is found, snap to the target value at the end of the animation
        return progress >= 1.0f ? to : from;
    }

    private static float LerpFloat(float from, float to, float progress) => from + (to - from) * progress;

    private static Vector2 LerpVector2(Vector2 from, Vector2 to, float progress) => from + (to - from) * progress;

    private static Color LerpColor(Color from, Color to, float progress)
    {
        return new Color(
            (byte)(from.R + (to.R - from.R) * progress),
            (byte)(from.G + (to.G - from.G) * progress),
            (byte)(from.B + (to.B - from.B) * progress),
            (byte)(from.A + (to.A - from.A) * progress)
        );
    }
}