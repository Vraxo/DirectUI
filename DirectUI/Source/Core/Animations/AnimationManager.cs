// DirectUI/Source/Animation/AnimationManager.cs
using System;
using System.Collections.Generic;

namespace DirectUI.Animation;

public class AnimationManager
{
    private readonly Dictionary<int, AnimatedValue> _animatedProperties = new();

    public void Update(float currentTime)
    {
        var keys = new List<int>(_animatedProperties.Keys);
        foreach (var key in keys)
        {
            if (_animatedProperties.TryGetValue(key, out var animValue))
            {
                animValue.Update(currentTime);
            }
        }
    }

    public T GetOrAnimate<T>(int propertyId, T targetValue, float currentTime, float duration, Func<float, float> easing)
    {
        if (!_animatedProperties.TryGetValue(propertyId, out var animValue))
        {
            animValue = new AnimatedValue(targetValue);
            _animatedProperties[propertyId] = animValue;
        }

        // Check if the target has changed
        if (animValue.TargetValue is not T || !EqualityComparer<T>.Default.Equals((T)animValue.TargetValue, targetValue))
        {
            animValue.Start(targetValue, currentTime, duration, easing);
        }

        return (T)animValue.CurrentValue;
    }
}