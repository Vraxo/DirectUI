namespace DirectUI;

public class AnimationInfo
{
    public float Duration { get; }
    public Func<float, float> Easing { get; }

    public AnimationInfo(float duration = 0.15f, Func<float, float>? easing = null)
    {
        Duration = duration;
        Easing = easing ?? DirectUI.Animation.Easing.EaseOutQuad;
    }
}