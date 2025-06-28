using System.Numerics;
using Vortice.Mathematics;
using System;

namespace DirectUI;

public class UILayoutManager
{
    private readonly Stack<object> _containerStack = new();
    private readonly Dictionary<int, object> _containerStateCache = new();
    private readonly Stack<Rect> _clipRectStack = new();

    public int ContainerStackCount => _containerStack.Count;
    public bool IsInLayoutContainer() => _containerStack.Count > 0;

    public void PushContainer(object containerState) => _containerStack.Push(containerState);
    public object PopContainer() => _containerStack.Pop();
    public object PeekContainer() => _containerStack.Peek();
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
        var vboxState = GetOrCreateVBoxState(id);

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

        return _containerStack.Peek() switch
        {
            HBoxContainerState hbox => hbox.CurrentPosition,
            VBoxContainerState vbox => vbox.CurrentPosition,
            GridContainerState grid => grid.CurrentDrawPosition,
            ScrollContainerState scroll => scroll.ContentVBox.CurrentPosition,
            ResizablePanelState panel => panel.InnerVBox.CurrentPosition,
            ResizableHPanelState hpanel => hpanel.InnerHBox.CurrentPosition,
            _ => Vector2.Zero,
        };
    }

    public void AdvanceContainerLayout(Vector2 elementSize)
    {
        if (_containerStack.Count == 0) return;

        object currentContainerState = _containerStack.Peek();

        // Handle nested containers
        if (currentContainerState is ScrollContainerState scrollState)
        {
            currentContainerState = scrollState.ContentVBox;
        }

        switch (currentContainerState)
        {
            case HBoxContainerState hbox:
                {
                    if (elementSize.Y > hbox.MaxElementHeight) hbox.MaxElementHeight = elementSize.Y;
                    hbox.AccumulatedWidth += elementSize.X;
                    if (hbox.ElementCount > 0)
                    {
                        hbox.AccumulatedWidth += hbox.Gap;
                    }
                    float advanceX = elementSize.X + hbox.Gap;
                    hbox.CurrentPosition = new Vector2(hbox.CurrentPosition.X + advanceX, hbox.CurrentPosition.Y);
                    hbox.ElementCount++;
                    break;
                }
            case VBoxContainerState vbox:
                {
                    if (elementSize.X > vbox.MaxElementWidth) vbox.MaxElementWidth = elementSize.X;
                    vbox.AccumulatedHeight += elementSize.Y;
                    if (vbox.ElementCount > 0)
                    {
                        vbox.AccumulatedHeight += vbox.Gap;
                    }
                    float advanceY = elementSize.Y + vbox.Gap;
                    vbox.CurrentPosition = new Vector2(vbox.CurrentPosition.X, vbox.CurrentPosition.Y + advanceY);
                    vbox.ElementCount++;
                    break;
                }
            case GridContainerState grid:
                {
                    grid.MoveToNextCell(elementSize);
                    break;
                }
            case ResizablePanelState panel:
                {
                    var innerVBox = panel.InnerVBox;
                    if (elementSize.X > innerVBox.MaxElementWidth) innerVBox.MaxElementWidth = elementSize.X;
                    innerVBox.AccumulatedHeight += elementSize.Y;
                    if (innerVBox.ElementCount > 0)
                    {
                        innerVBox.AccumulatedHeight += innerVBox.Gap;
                    }
                    float advanceY = elementSize.Y + innerVBox.Gap;
                    innerVBox.CurrentPosition = new Vector2(innerVBox.CurrentPosition.X, innerVBox.CurrentPosition.Y + advanceY);
                    innerVBox.ElementCount++;
                    break;
                }
            case ResizableHPanelState hpanel:
                {
                    var innerHBox = hpanel.InnerHBox;
                    if (elementSize.Y > innerHBox.MaxElementHeight) innerHBox.MaxElementHeight = elementSize.Y;
                    innerHBox.AccumulatedWidth += elementSize.X;
                    if (innerHBox.ElementCount > 0)
                    {
                        innerHBox.AccumulatedWidth += innerHBox.Gap;
                    }
                    float advanceX = elementSize.X + innerHBox.Gap;
                    innerHBox.CurrentPosition = new Vector2(innerHBox.CurrentPosition.X + advanceX, innerHBox.CurrentPosition.Y);
                    innerHBox.ElementCount++;
                    break;
                }
            default:
                {
                    Console.WriteLine("Error: AdvanceContainerLayout called with unexpected container type.");
                    break;
                }
        }
    }
}