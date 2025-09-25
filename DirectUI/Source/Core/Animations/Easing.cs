// DirectUI/Source/Animation/Easing.cs
namespace DirectUI.Animation;

public static class Easing
{
    public static float Linear(float t) => t;
    public static float EaseInQuad(float t) => t * t;
    public static float EaseOutQuad(float t) => t * (2 - t);
    public static float EaseInOutQuad(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
}