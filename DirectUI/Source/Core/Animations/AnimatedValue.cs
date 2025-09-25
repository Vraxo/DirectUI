// DirectUI/Source/Animation/AnimatedValue.cs
using System;

namespace DirectUI.Animation;

internal class AnimatedValue
{
    public object CurrentValue { get; set; }
    public object StartValue { get; set; }
    public object TargetValue { get; set; }
    public float StartTime { get; set; }
    public float Duration { get; set; }
    public Func<float, float> Easing { get; set; }
    public bool IsAnimating { get; set; }

    public AnimatedValue(object initialValue)
    {
        CurrentValue = initialValue;
        StartValue = initialValue;
        TargetValue = initialValue;
        Easing = DirectUI.Animation.Easing.Linear;
    }

    public void Start(object newTarget, float currentTime, float duration, Func<float, float> easing)
    {
        // Don't restart if we are already animating towards the same target,
        // unless the new target is actually different from the current animated value.
        // This handles cases where the state flickers but the target value is the same.
        if (newTarget.Equals(TargetValue) && IsAnimating) return;

        StartValue = CurrentValue;
        TargetValue = newTarget;
        StartTime = currentTime;
        Duration = duration;
        Easing = easing;
        IsAnimating = true;
    }

    public bool Update(float currentTime)
    {
        if (!IsAnimating) return false; // Not running

        float elapsedTime = currentTime - StartTime;
        if (elapsedTime >= Duration)
        {
            CurrentValue = TargetValue;
            IsAnimating = false;
            return false; // Animation just finished
        }

        float progress = Easing(elapsedTime / Duration);
        CurrentValue = ValueInterpolators.Lerp(StartValue, TargetValue, progress);

        return true; // Still running
    }
}