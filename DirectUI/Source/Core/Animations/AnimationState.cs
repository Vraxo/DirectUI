// DirectUI/Source/Animation/AnimationState.cs
using System.Collections.Generic;

namespace DirectUI.Animation;

public class AnimationState
{
    // Maps a unique animation ID to the time (TotalTime) its trigger became true.
    internal readonly Dictionary<int, float> _eventStartTimes = new();

    public float GetEventTime(int id)
    {
        return _eventStartTimes.TryGetValue(id, out var time) ? time : -1f;
    }

    public void SetEventTime(int id, float time)
    {
        _eventStartTimes[id] = time;
    }

    public void ClearEventTime(int id)
    {
        _eventStartTimes.Remove(id);
    }
}