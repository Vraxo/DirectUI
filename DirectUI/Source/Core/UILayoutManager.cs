﻿using System.Numerics;
using Vortice.Mathematics;
using System;

namespace DirectUI;

public class UILayoutManager
{
    private readonly Stack<ILayoutContainer> _containerStack = new();
    private readonly Dictionary<int, object> _containerStateCache = new();
    private readonly Stack<Rect> _clipRectStack = new();

    public int ContainerStackCount => _containerStack.Count;
    public bool IsInLayoutContainer() => _containerStack.Count > 0;

    public void PushContainer(ILayoutContainer containerState) => _containerStack.Push(containerState);
    public ILayoutContainer PopContainer() => _containerStack.Pop();
    public ILayoutContainer PeekContainer() => _containerStack.Peek();
    public void ClearStack()
    {
        _containerStack.Clear();
        _clipRectStack.Clear();
    }

    public void PushClipRect(Rect rect) => _clipRectStack.Push(rect);
    public void PopClipRect()
    {
        if (_clipRectStack.Count > 0)
        {
            _clipRectStack.Pop();
        }
    }

    public Rect GetCurrentClipRect()
    {
        if (_clipRectStack.Count > 0)
        {
            return _clipRectStack.Peek();
        }
        // Return a very large rectangle representing no clipping
        return new Rect(float.MinValue / 2, float.MinValue / 2, float.MaxValue, float.MaxValue);
    }

    public bool IsRectVisible(Rect rect)
    {
        if (_clipRectStack.Count == 0) return true;

        var currentClip = GetCurrentClipRect();

        // Basic intersection test
        return rect.X < currentClip.X + currentClip.Width &&
               rect.X + rect.Width > currentClip.X &&
               rect.Y < currentClip.Y + currentClip.Height &&
               rect.Y + rect.Height > currentClip.Y;
    }

    public HBoxContainerState GetOrCreateHBoxState(int id)
    {
        if (!_containerStateCache.TryGetValue(id, out var state) || state is not HBoxContainerState hboxState)
        {
            hboxState = new HBoxContainerState(id);
            _containerStateCache[id] = hboxState;
        }
        return hboxState;
    }

    public VBoxContainerState GetOrCreateVBoxState(int id)
    {
        if (!_containerStateCache.TryGetValue(id, out var state) || state is not VBoxContainerState vboxState)
        {
            vboxState = new VBoxContainerState(id);
            _containerStateCache[id] = vboxState;
        }
        return vboxState;
    }

    public void BeginHBox(int id, Vector2 startPosition, float gap)
    {
        var hboxState = GetOrCreateHBoxState(id);

        // Reset per-frame properties
        hboxState.StartPosition = startPosition;
        hboxState.CurrentPosition = startPosition;
        hboxState.Gap = gap;
        hboxState.MaxElementHeight = 0f;
        hboxState.AccumulatedWidth = 0f;
        hboxState.ElementCount = 0;

        PushContainer(hboxState);
    }

    public void BeginVBox(int id, Vector2 startPosition, float gap)
    {
        VBoxContainerState vboxState = GetOrCreateVBoxState(id);

        // Reset per-frame properties
        vboxState.StartPosition = startPosition;
        vboxState.CurrentPosition = startPosition;
        vboxState.Gap = gap;
        vboxState.MaxElementWidth = 0f;
        vboxState.AccumulatedHeight = 0f;
        vboxState.ElementCount = 0;

        PushContainer(vboxState);
    }

    public Vector2 ApplyLayout(Vector2 defaultPosition)
    {
        return IsInLayoutContainer() ? GetCurrentPosition() : defaultPosition;
    }

    public void AdvanceLayout(Vector2 elementSize)
    {
        if (IsInLayoutContainer())
        {
            AdvanceContainerLayout(new Vector2(Math.Max(0, elementSize.X), Math.Max(0, elementSize.Y)));
        }
    }

    public Vector2 GetCurrentPosition()
    {
        if (_containerStack.Count == 0)
        {
            return Vector2.Zero;
        }
        // Polymorphic call to the container at the top of the stack.
        return _containerStack.Peek().GetCurrentPosition();
    }

    public void AdvanceContainerLayout(Vector2 elementSize)
    {
        if (_containerStack.Count == 0) return;

        // Polymorphic call to the container at the top of the stack.
        _containerStack.Peek().Advance(elementSize);
    }
}