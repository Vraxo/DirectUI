// DirectUI/Source/Animation/Easing.cs
using System;
using System.Collections.Generic;

namespace DirectUI.Animation;

public static class Easing
{
    private static readonly Dictionary<string, Func<float, float>> s_easingFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Linear", Linear },
        { "EaseInQuad", EaseInQuad },
        { "EaseOutQuad", EaseOutQuad },
        { "EaseInOutQuad", EaseInOutQuad }
    };

    public static float Linear(float t) => t;
    public static float EaseInQuad(float t) => t * t;
    public static float EaseOutQuad(float t) => t * (2 - t);
    public static float EaseInOutQuad(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;

    public static Func<float, float> GetEasingFunction(string? name)
    {
        if (name != null && s_easingFunctions.TryGetValue(name, out var func))
        {
            return func;
        }
        return Linear; // Default to Linear if not found or name is null
    }
}