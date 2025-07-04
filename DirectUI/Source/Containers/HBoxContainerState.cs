﻿using System.Numerics;

namespace DirectUI;

public class HBoxContainerState : ILayoutContainer
{
    internal int Id { get; }
    internal Vector2 StartPosition { get; set; }
    internal Vector2 CurrentPosition { get; set; }
    internal float Gap { get; set; }
    internal float MaxElementHeight { get; set; } = 0f;
    internal float AccumulatedWidth { get; set; } = 0f;
    internal int ElementCount { get; set; } = 0;

    internal HBoxContainerState(int id)
    {
        Id = id;
    }

    public Vector2 GetCurrentPosition() => CurrentPosition;

    public void Advance(Vector2 elementSize)
    {
        if (elementSize.Y > MaxElementHeight)
        {
            MaxElementHeight = elementSize.Y;
        }

        AccumulatedWidth += elementSize.X;
        if (ElementCount > 0)
        {
            AccumulatedWidth += Gap;
        }
        float advanceX = elementSize.X + Gap;
        CurrentPosition = new Vector2(CurrentPosition.X + advanceX, CurrentPosition.Y);
        ElementCount++;
    }
}